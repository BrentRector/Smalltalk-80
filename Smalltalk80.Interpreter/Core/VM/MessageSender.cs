using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides a high-level API for sending messages from C# to Smalltalk objects.
/// This is the foundation for --execute, REPL, debugger, and other interactive features.
///
/// Usage:
///   var sender = new MessageSender(heap, vm, metadata);
///   var result = sender.SendUnary(receiverOOP, "size");
///   var result = sender.SendKeyword(receiverOOP, "at:put:", indexOOP, valueOOP);
/// </summary>
public class MessageSender
{
    private readonly ISmalltalkHeap _heap;
    private readonly VirtualMachine _vm;
    private readonly VMMetadata _metadata;
    private readonly IMessageDispatcher _messageDispatcher;
    private readonly IContextManager _contextManager;

    public MessageSender(
        ISmalltalkHeap heap,
        VirtualMachine vm,
        VMMetadata metadata,
        IMessageDispatcher messageDispatcher,
        IContextManager contextManager)
    {
        _heap = heap;
        _vm = vm;
        _metadata = metadata;
        _messageDispatcher = messageDispatcher;
        _contextManager = contextManager;
    }

    /// <summary>
    /// Sends a unary message (no arguments) to a receiver.
    /// Example: SendUnary(arrayOOP, "size") => arrayOOP size
    /// </summary>
    public SmalltalkOOP SendUnary(SmalltalkOOP receiver, string selector)
    {
        return SendMessage(receiver, selector, Array.Empty<SmalltalkOOP>());
    }

    /// <summary>
    /// Sends a keyword message (with arguments) to a receiver.
    /// Example: SendKeyword(arrayOOP, "at:put:", indexOOP, valueOOP) => arrayOOP at: indexOOP put: valueOOP
    /// </summary>
    public SmalltalkOOP SendKeyword(SmalltalkOOP receiver, string selector, params SmalltalkOOP[] args)
    {
        return SendMessage(receiver, selector, args);
    }

    /// <summary>
    /// Sends a binary message (single argument) to a receiver.
    /// Example: SendBinary(aOOP, "+", bOOP) => aOOP + bOOP
    /// </summary>
    public SmalltalkOOP SendBinary(SmalltalkOOP receiver, string selector, SmalltalkOOP arg)
    {
        return SendMessage(receiver, selector, new[] { arg });
    }

    /// <summary>
    /// Core message sending implementation.
    /// Follows the standard Smalltalk message sending protocol:
    /// 1. Look up method in receiver's class hierarchy
    /// 2. Create MethodContext with receiver and arguments
    /// 3. Execute context on VM
    /// 4. Return result
    /// </summary>
    private SmalltalkOOP SendMessage(SmalltalkOOP receiver, string selector, SmalltalkOOP[] args)
    {
        // Step 1: Look up method in receiver's class
        var classOOP = _heap.GetClassOOP(receiver);
        var methodInfo = _messageDispatcher.LookupMethod(classOOP, selector);

        if (methodInfo == null)
        {
            var className = GetClassName(classOOP);
            throw new InvalidOperationException(
                $"Message not understood: {selector} sent to instance of {className}");
        }

        // Step 2: Create MethodContext
        // Pass nil as sender since we're starting a new execution chain
        var contextOOP = _contextManager.CreateMethodContext(
            methodInfo.MethodOOP,
            receiver,
            args,
            callerContextOOP: _heap.GetNil());

        // Step 3: Execute the context and get result
        var resultOOP = _vm.ExecuteExistingContext(contextOOP);

        return resultOOP;
    }

    /// <summary>
    /// Gets the class name for a class OOP.
    /// </summary>
    private string GetClassName(SmalltalkOOP classOOP)
    {
        if (classOOP == _metadata.KnownObjects.Nil)
        {
            return "nil";
        }

        try
        {
            // Get class name from instance variable (Behavior>>name)
            var nameOOP = _heap.GetInstanceVariable(classOOP, VMConstants.BEHAVIOR_NAME_INDEX);
            if (nameOOP == _metadata.KnownObjects.Nil)
            {
                return "<unnamed class>";
            }

            // Symbol is a String, read bytes
            int length = _heap.GetIndexedSize(nameOOP);
            var bytes = new byte[length];
            for (int i = 0; i < length; i++)
            {
                bytes[i] = _heap.GetIndexedByte(nameOOP, i);
            }

            return System.Text.Encoding.ASCII.GetString(bytes);
        }
        catch
        {
            return "<unknown class>";
        }
    }
}
