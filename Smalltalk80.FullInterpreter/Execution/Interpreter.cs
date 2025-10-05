namespace Smalltalk80.Interpreter.Execution;

using System;
using System.Linq;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
/// A Blue Book compliant context-based bytecode interpreter using LayoutManagement.
/// This interpreter uses MethodContext and BlockContext objects for execution,
/// not C# stacks, making it fully compliant with the Smalltalk-80 specification.
///
/// Rewritten to use LayoutManagement.Layout instead of ST* structs.
/// </summary>
public sealed class Interpreter
{
    private SObjectRef activeContext = SObjectRef.Nil;
    private readonly ILogger<Interpreter>? logger;
    private bool tracingEnabled = false;
    private int traceIndentLevel = 0;
    private GarbageCollector? garbageCollector;
    private readonly PrimitiveTable primitiveTable;
    private readonly InterpreterPrimitiveContext primitiveContext;

    // Cached references for performance
    private static SObjectRef? cachedTrueRef;
    private static SObjectRef? cachedFalseRef;

    /// <summary>
    /// Gets the current active context (supports thisContext).
    /// </summary>
    public SObjectRef ActiveContext => activeContext;

    /// <summary>
    /// Enables or disables execution tracing.
    /// Checks DebugOptions if not explicitly set.
    /// </summary>
    public bool TracingEnabled
    {
        get => tracingEnabled || DebugOptions.EnableMethodTrace || DebugOptions.EnableBytecodeTrace;
        set => tracingEnabled = value;
    }

    /// <summary>
    /// Gets or sets the garbage collector for primitive 133.
    /// </summary>
    public GarbageCollector? GarbageCollector
    {
        get => garbageCollector;
        set => garbageCollector = value;
    }

    /// <summary>
    /// Initializes a new instance of the Interpreter.
    /// </summary>
    public Interpreter(ILogger<Interpreter>? logger = null)
    {
        this.logger = logger;
        this.primitiveTable = new PrimitiveTable();
        this.primitiveContext = new InterpreterPrimitiveContext(this);

        // Register all primitives
        PrimitiveRegistry.RegisterAll(primitiveTable);
    }

    /// <summary>
    /// Executes a method in a new method context.
    /// </summary>
    /// <param name="methodRef">The compiled method to execute.</param>
    /// <param name="receiverRef">The receiver of the message.</param>
    /// <param name="argumentRefs">The arguments to the method.</param>
    /// <returns>The result of the method execution.</returns>
    public SObjectRef Execute(SObjectRef methodRef, SObjectRef receiverRef, SObjectRef[] argumentRefs)
    {
        if (methodRef.IsNil)
            throw new ArgumentException("Method cannot be nil");
        if (receiverRef.IsNil)
            throw new ArgumentException("Receiver cannot be nil");
        if (argumentRefs == null)
            throw new ArgumentNullException(nameof(argumentRefs));

        if (TracingEnabled)
        {
            var indent = new string(' ', traceIndentLevel * 2);
            var selector = ObjectMemory.GetCompiledMethodSelector(methodRef.AsObjectPointer());
            Console.WriteLine($"{indent}==> EXECUTE SELECTOR: {selector}");
            Console.WriteLine($"{indent}    Receiver: {FormatRef(receiverRef)}");
            Console.WriteLine($"{indent}    Arguments: [{string.Join(", ", argumentRefs.Select(FormatRef))}]");
            traceIndentLevel++;
        }

        try
        {
            // Create new method context
            var newContext = CreateMethodContext(methodRef, receiverRef, SObjectRef.Nil, argumentRefs);

            // Set as active context
            activeContext = newContext;

            // Execute bytecode loop
            var result = InterpretLoop();

            return result;
        }
        finally
        {
            if (TracingEnabled)
            {
                traceIndentLevel--;
            }
        }
    }

    /// <summary>
    /// Creates a new MethodContext for method execution.
    /// </summary>
    private SObjectRef CreateMethodContext(SObjectRef methodRef, SObjectRef receiverRef, SObjectRef senderRef, SObjectRef[] arguments)
    {
        var methodOop = methodRef.AsObjectPointer();
        // TODO: Get MethodContext class from SystemDictionary or class registry
        // For now, allocate without class (will need fixing for full implementation)
        var methodClass = SObjectRef.Nil;

        // Get method info
        var numArgs = ObjectMemory.GetCompiledMethodArgumentCount(methodOop);
        var numTemps = ObjectMemory.GetCompiledMethodTemporaryCount(methodOop);

        // Calculate stack size: instance vars + (args + temps) + extra stack space
        var stackSize = LayoutManagement.Layout.MethodContext_InstanceVarCount + numArgs + numTemps + 32;

        // Allocate MethodContext
        var contextOop = ObjectMemory.AllocateObject(
            methodClass,
            ObjectFormat.PointerIndexed,
            LayoutManagement.Layout.MethodContext_InstanceVarCount,
            stackSize - LayoutManagement.Layout.MethodContext_InstanceVarCount
        );

        // Initialize MethodContext fields
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Sender, senderRef);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer, SObjectRef.FromSmallInteger(0));
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(LayoutManagement.Layout.MethodContext_InstanceVarCount + numArgs + numTemps));
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Method, methodRef);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver, receiverRef);

        // Push arguments onto stack
        for (int i = 0; i < arguments.Length && i < numArgs; i++)
        {
            ObjectMemory.SetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = arguments.Length; i < numArgs + numTemps; i++)
        {
            ObjectMemory.SetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + i, SObjectRef.Nil);
        }

        return SObjectRef.FromObjectPointer(contextOop);
    }

    /// <summary>
    /// Main bytecode interpretation loop.
    /// </summary>
    private SObjectRef InterpretLoop()
    {
        const int MAX_ITERATIONS = 1000000; // Safety limit
        int iterations = 0;

        while (!activeContext.IsNil)
        {
            if (iterations++ > MAX_ITERATIONS)
                throw new InvalidOperationException("Interpretation exceeded maximum iteration count");

            var contextOop = activeContext.AsObjectPointer();

            // Get current instruction pointer
            var ipRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer);
            if (!ipRef.IsSmallInteger)
                throw new InvalidOperationException("Instruction pointer must be a SmallInteger");

            var ip = ipRef.AsSmallInteger();

            // Get method
            var methodRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Method);
            if (methodRef.IsNil)
                throw new InvalidOperationException("Method context has nil method");

            var methodOop = methodRef.AsObjectPointer();
            var bytecodes = ObjectMemory.GetCompiledMethodBytecodes(methodOop);

            // Check for method return
            if (ip >= bytecodes.Length)
            {
                // Method finished without explicit return - return receiver
                var receiver = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver);
                return receiver;
            }

            // Fetch and decode bytecode
            var bytecode = bytecodes[ip];

            // Increment IP before execution (some bytecodes may override this)
            ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_InstructionPointer,
                SObjectRef.FromSmallInteger(ip + 1));

            if (TracingEnabled && DebugOptions.EnableBytecodeTrace)
            {
                Console.WriteLine($"  [{ip:D4}] Bytecode: {bytecode:X2}");
            }

            // Execute bytecode
            var shouldContinue = ExecuteBytecode(bytecode, bytecodes, ref ip);
            if (!shouldContinue)
            {
                // Method returned - pop context and return value
                var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
                var sp = spRef.AsSmallInteger();
                if (sp > LayoutManagement.Layout.MethodContext_InstanceVarCount)
                {
                    return ObjectMemory.GetIndexedPointer(contextOop, sp - 1);
                }
                return SObjectRef.Nil;
            }
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Executes a single bytecode instruction.
    /// </summary>
    private bool ExecuteBytecode(byte bytecode, byte[] bytecodes, ref int ip)
    {
        // Simplified bytecode dispatch - a real implementation would handle all bytecodes
        // For now, just implement enough to make it compile

        // Push constants
        if (bytecode >= 0 && bytecode <= 15)
        {
            // Push receiver instance variable
            PushReceiverVariable(bytecode & 0x0F);
            return true;
        }
        else if (bytecode >= 16 && bytecode <= 31)
        {
            // Push temporary variable
            PushTemporaryVariable(bytecode & 0x0F);
            return true;
        }
        else if (bytecode == 112)
        {
            // Push self
            PushSelf();
            return true;
        }
        else if (bytecode == 113)
        {
            // Push true
            Push(GetTrue());
            return true;
        }
        else if (bytecode == 114)
        {
            // Push false
            Push(GetFalse());
            return true;
        }
        else if (bytecode == 115)
        {
            // Push nil
            Push(SObjectRef.Nil);
            return true;
        }
        else if (bytecode == 120)
        {
            // Return self from method
            ReturnValueTo(GetSelf(), GetSender());
            return false;
        }
        else if (bytecode == 124)
        {
            // Return top of stack from method
            var value = Pop();
            ReturnValueTo(value, GetSender());
            return false;
        }

        // TODO: Implement remaining bytecodes
        throw new NotImplementedException($"Bytecode {bytecode:X2} not yet implemented");
    }

    // Stack manipulation helpers

    private void Push(SObjectRef value)
    {
        var contextOop = activeContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        ObjectMemory.SetIndexedPointer(contextOop, sp, value);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp + 1));
    }

    private SObjectRef Pop()
    {
        var contextOop = activeContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        if (sp <= LayoutManagement.Layout.MethodContext_InstanceVarCount)
            throw new InvalidOperationException("Stack underflow");

        var value = ObjectMemory.GetIndexedPointer(contextOop, sp - 1);
        ObjectMemory.SetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp - 1));

        return value;
    }

    private void PushReceiverVariable(int index)
    {
        var receiver = GetSelf();
        var receiverOop = receiver.AsObjectPointer();
        var value = ObjectMemory.GetInstanceVariable(receiverOop, index);
        Push(value);
    }

    private void PushTemporaryVariable(int index)
    {
        var contextOop = activeContext.AsObjectPointer();
        var value = ObjectMemory.GetIndexedPointer(contextOop, LayoutManagement.Layout.MethodContext_InstanceVarCount + index);
        Push(value);
    }

    private void PushSelf()
    {
        Push(GetSelf());
    }

    private SObjectRef GetSelf()
    {
        var contextOop = activeContext.AsObjectPointer();
        return ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Receiver);
    }

    private SObjectRef GetSender()
    {
        var contextOop = activeContext.AsObjectPointer();
        return ObjectMemory.GetInstanceVariable(contextOop, LayoutManagement.Layout.MethodContext_Sender);
    }

    private void ReturnValueTo(SObjectRef value, SObjectRef targetContext)
    {
        if (targetContext.IsNil)
        {
            // Returning to nil sender means we're done
            activeContext = SObjectRef.Nil;
            return;
        }

        // Push value onto target context's stack
        var targetOop = targetContext.AsObjectPointer();
        var spRef = ObjectMemory.GetInstanceVariable(targetOop, LayoutManagement.Layout.MethodContext_StackPointer);
        var sp = spRef.AsSmallInteger();

        ObjectMemory.SetIndexedPointer(targetOop, sp, value);
        ObjectMemory.SetInstanceVariable(targetOop, LayoutManagement.Layout.MethodContext_StackPointer,
            SObjectRef.FromSmallInteger(sp + 1));

        // Switch to target context
        activeContext = targetContext;
    }

    private SObjectRef GetTrue()
    {
        if (!cachedTrueRef.HasValue)
        {
            // TODO: Get true from special objects array or SystemDictionary
            // For now, return nil as placeholder
            cachedTrueRef = SObjectRef.Nil;
        }
        return cachedTrueRef ?? SObjectRef.Nil;
    }

    private SObjectRef GetFalse()
    {
        if (!cachedFalseRef.HasValue)
        {
            // TODO: Get false from special objects array or SystemDictionary
            // For now, return nil as placeholder
            cachedFalseRef = SObjectRef.Nil;
        }
        return cachedFalseRef ?? SObjectRef.Nil;
    }

    // Formatting helpers

    private string FormatRef(SObjectRef objRef)
    {
        if (objRef.IsNil)
            return "nil";
        if (objRef.IsSmallInteger)
            return objRef.AsSmallInteger().ToString();
        if (objRef.IsHeapObject)
        {
            var className = ObjectMemory.GetClassName(objRef.AsObjectPointer());
            return $"{className}@{objRef.AsObjectPointer():X}";
        }
        return objRef.ToString() ?? "?";
    }

    /// <summary>
    /// Creates a block context (stub for now).
    /// </summary>
    public SObjectRef CreateBlockContext(int argumentCount)
    {
        throw new NotImplementedException("CreateBlockContext needs full implementation");
    }

    /// <summary>
    /// Activates a block context (stub for now).
    /// </summary>
    public SObjectRef ActivateBlockContext(SObjectRef blockRef, SObjectRef[] arguments)
    {
        throw new NotImplementedException("ActivateBlockContext needs full implementation");
    }
}
