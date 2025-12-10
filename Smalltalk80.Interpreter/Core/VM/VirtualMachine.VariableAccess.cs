using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing variable access and method literal helper methods.
///
/// This partial class contains:
/// - GetMethodBytecodes() / GetLiteralCount() / GetMethodLiteral() - Method structure access
/// - GetTemporaryVariable() / SetTemporaryVariable() - Unified temp frame variable access
/// - FindHomeMethodContext() - Home context chain traversal
///
/// These methods remain in VirtualMachine because they:
/// - Implement the unified temp frame architecture (core VM data structure)
/// - Are called from both bytecode interpretation and context management
/// - Require access to VM metadata (_metadata.BlockContextClassOOP)
/// </summary>
public partial class VirtualMachine
{
    // Helper methods for heap access during refactoring
    private byte[] GetMethodBytecodes(SmalltalkOOP methodOOP)
    {
        // CompiledMethod structure: [header, literal1, ..., literalN, bytecodeArrayOOP]
        // The last indexed slot contains OOP to ByteArray with bytecodes
        // Returns ALL bytes including the 3-byte source file offset at the end
        int indexedSize = _heapManager.GetIndexedSize(methodOOP);
        if (indexedSize > 0)
        {
            // Get the last indexed slot which should be the bytecode array OOP
            var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, indexedSize - 1);
            int byteCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
            byte[] bytecodes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                bytecodes[i] = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
            }
            return bytecodes;
        }
        return Array.Empty<byte>();
    }

    private int GetLiteralCount(SmalltalkOOP methodOOP)
    {
        // Get the total indexed size of the CompiledMethod
        int indexedSize = _heapManager.GetIndexedSize(methodOOP);

        // The indexed portion contains: [header, literal1, literal2, ..., literalN, bytecode array]
        // So literal count = indexedSize - 2 (subtract header at index 0 and bytecode array at end)
        return indexedSize - 2;
    }

    private SmalltalkOOP GetMethodLiteral(SmalltalkOOP methodOOP, int index)
    {
        // Validate that methodOOP is a CompiledMethod (PointerIndexed)
        var format = _heapManager.GetObjectFormat(methodOOP);
        if (format != ObjectFormat.PointerIndexed)
        {
            var className = GetClassName(GetClass(methodOOP));
            var localId = GetLocalId(methodOOP);
            throw new InvalidOperationException(
                $"GetMethodLiteral called on non-PointerIndexed object: {methodOOP}, " +
                $"format={format}, class={className}, localId={localId}. " +
                $"Expected CompiledMethod (PointerIndexed), got {format}. " +
                $"Index={index}");
        }

        // Get the literal from indexed slots
        var literalOOP = _heapManager.GetIndexedOOP(methodOOP, index);

        // Note: Literals can be Symbols (ByteIndexed), Strings (ByteIndexed),
        // SmallIntegers, Arrays (PointerIndexed), CompiledMethods (PointerIndexed), etc.
        // ByteIndexed literals (Symbols/Strings) are normal and expected.

        return literalOOP;
    }

    /// <summary>
    /// Gets a temporary variable using unified temp frame architecture.
    /// All variables (method args/temps, block args/temps) are stored in MethodContext
    /// temp frame using absolute indices assigned by compiler.
    /// </summary>
    /// <param name="contextOOP">Current context (MethodContext or BlockContext)</param>
    /// <param name="index">Absolute index in unified temp frame</param>
    /// <returns>Variable value</returns>
    private SmalltalkOOP GetTemporaryVariable(SmalltalkOOP contextOOP, int index)
    {
        var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        if (isBlockContext)
        {
            // UNIFIED TEMP FRAME: All variables in MethodContext
            // Traverse to home MethodContext and use absolute index directly
            var homeOOP = _heapManager.GetInstanceVariable(
                contextOOP,
                VMConstants.BLOCKCONTEXT_HOME_INDEX
            );

            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.GetTemporaryVariable] BlockContext -> home, index={index}");
            }

            // Recursive call (may traverse through nested blocks)
            return GetTemporaryVariable(homeOOP, index);
        }
        else
        {
            // MethodContext: Direct access with absolute index
            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.GetTemporaryVariable] MethodContext index={index}");
            }

            return _heapManager.GetIndexedOOP(contextOOP, index);
        }
    }

    /// <summary>
    /// Sets a temporary variable using unified temp frame architecture.
    /// All variables (method args/temps, block args/temps) are stored in MethodContext
    /// temp frame using absolute indices assigned by compiler.
    /// </summary>
    /// <param name="contextOOP">Current context (MethodContext or BlockContext)</param>
    /// <param name="index">Absolute index in unified temp frame</param>
    /// <param name="valueOOP">Value to store</param>
    private void SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP)
    {
        var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
        bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

        if (isBlockContext)
        {
            // UNIFIED TEMP FRAME: All variables in MethodContext
            // Traverse to home MethodContext and use absolute index directly
            var homeOOP = _heapManager.GetInstanceVariable(
                contextOOP,
                VMConstants.BLOCKCONTEXT_HOME_INDEX
            );

            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.SetTemporaryVariable] BlockContext -> home, index={index}, value={valueOOP}");
            }

            // Recursive call (may traverse through nested blocks)
            SetTemporaryVariable(homeOOP, index, valueOOP);
        }
        else
        {
            // MethodContext: Direct write with absolute index
            if (VerboseDebug)
            {
                Console.WriteLine($"[VM.SetTemporaryVariable] MethodContext index={index}, value={valueOOP}");
            }

            _heapManager.SetIndexedOOP(contextOOP, index, valueOOP);
        }
    }

    /// <summary>
    /// Finds the home MethodContext by traversing the home chain.
    /// For MethodContext: returns itself.
    /// For BlockContext: follows home chain until MethodContext is found.
    /// Blue Book Chapter 30: BlueBook-Context-Architecture.md Section 4.3
    /// </summary>
    /// <param name="contextOOP">Starting context (MethodContext or BlockContext)</param>
    /// <returns>The MethodContext at the root of the home chain</returns>
    private SmalltalkOOP FindHomeMethodContext(SmalltalkOOP contextOOP)
    {
        // Traverse home chain until we find a MethodContext
        while (true)
        {
            var contextClassOOP = _heapManager.GetClassOOP(contextOOP);

            // Check if this is a BlockContext
            if (contextClassOOP == _metadata.BlockContextClassOOP)
            {
                // Follow home link
                contextOOP = _heapManager.GetInstanceVariable(
                    contextOOP,
                    VMConstants.BLOCKCONTEXT_HOME_INDEX
                );

                if (contextOOP == _metadata.KnownObjects.Nil)
                {
                    throw new InvalidOperationException("Home chain broken - no MethodContext found");
                }

                continue;  // Keep traversing
            }

            // Must be MethodContext (or some other context) - return it
            return contextOOP;
        }
    }
}
