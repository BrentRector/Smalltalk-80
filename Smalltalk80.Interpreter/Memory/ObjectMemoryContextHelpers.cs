namespace Smalltalk80.Interpreter.Memory;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Helper methods for working with contexts in ObjectMemoryNew.
/// This is a temporary extension to add context manipulation helpers.
/// </summary>
public static partial class ObjectMemoryNew
{
    #region Context Helpers

    /// <summary>
    /// Context layout constants matching the Blue Book specification.
    /// </summary>
    public static class ContextLayout
    {
        public const int SenderIndex = 0;
        public const int InstructionPointerIndex = 1;
        public const int StackPointerIndex = 2;
        public const int MethodIndex = 3;
        public const int ReceiverIndex = 4;
        // Note: Arguments and temporaries start at index 5
    }

    /// <summary>
    /// Gets the sender from a context.
    /// </summary>
    public static SObjectRef GetContextSender(nint contextOop)
    {
        return GetInstanceVariable(contextOop, ContextLayout.SenderIndex);
    }

    /// <summary>
    /// Sets the sender of a context.
    /// </summary>
    public static void SetContextSender(nint contextOop, SObjectRef senderRef)
    {
        SetInstanceVariable(contextOop, ContextLayout.SenderIndex, senderRef);
    }

    /// <summary>
    /// Gets the instruction pointer from a context.
    /// </summary>
    public static int GetContextInstructionPointer(nint contextOop)
    {
        var ipRef = GetInstanceVariable(contextOop, ContextLayout.InstructionPointerIndex);
        return ipRef.IsSmallInteger ? ipRef.AsSmallInteger() : 0;
    }

    /// <summary>
    /// Sets the instruction pointer in a context.
    /// </summary>
    public static void SetContextInstructionPointer(nint contextOop, int ip)
    {
        SetInstanceVariable(contextOop, ContextLayout.InstructionPointerIndex,
            SObjectRef.FromSmallInteger(ip));
    }

    /// <summary>
    /// Gets the stack pointer from a context.
    /// </summary>
    public static int GetContextStackPointer(nint contextOop)
    {
        var spRef = GetInstanceVariable(contextOop, ContextLayout.StackPointerIndex);
        return spRef.IsSmallInteger ? spRef.AsSmallInteger() : 0;
    }

    /// <summary>
    /// Sets the stack pointer in a context.
    /// </summary>
    public static void SetContextStackPointer(nint contextOop, int sp)
    {
        SetInstanceVariable(contextOop, ContextLayout.StackPointerIndex,
            SObjectRef.FromSmallInteger(sp));
    }

    /// <summary>
    /// Gets the method from a context.
    /// </summary>
    public static SObjectRef GetContextMethod(nint contextOop)
    {
        return GetInstanceVariable(contextOop, ContextLayout.MethodIndex);
    }

    /// <summary>
    /// Sets the method in a context.
    /// </summary>
    public static void SetContextMethod(nint contextOop, SObjectRef methodRef)
    {
        SetInstanceVariable(contextOop, ContextLayout.MethodIndex, methodRef);
    }

    /// <summary>
    /// Gets the receiver from a context.
    /// </summary>
    public static SObjectRef GetContextReceiver(nint contextOop)
    {
        return GetInstanceVariable(contextOop, ContextLayout.ReceiverIndex);
    }

    /// <summary>
    /// Sets the receiver in a context.
    /// </summary>
    public static void SetContextReceiver(nint contextOop, SObjectRef receiverRef)
    {
        SetInstanceVariable(contextOop, ContextLayout.ReceiverIndex, receiverRef);
    }

    /// <summary>
    /// Pushes a value onto the context's stack.
    /// </summary>
    public static void ContextPush(nint contextOop, SObjectRef value)
    {
        var sp = GetContextStackPointer(contextOop);
        SetInstanceVariable(contextOop, sp, value);
        SetContextStackPointer(contextOop, sp + 1);
    }

    /// <summary>
    /// Pops a value from the context's stack.
    /// </summary>
    public static SObjectRef ContextPop(nint contextOop)
    {
        var sp = GetContextStackPointer(contextOop);
        if (sp <= 5) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack underflow");

        sp--;
        SetContextStackPointer(contextOop, sp);
        return GetInstanceVariable(contextOop, sp);
    }

    /// <summary>
    /// Gets the top value from the context's stack without popping.
    /// </summary>
    public static SObjectRef ContextTop(nint contextOop)
    {
        var sp = GetContextStackPointer(contextOop);
        if (sp <= 5) // Stack starts after fixed fields
            throw new InvalidOperationException("Stack is empty");

        return GetInstanceVariable(contextOop, sp - 1);
    }

    /// <summary>
    /// Gets a temporary variable from a context.
    /// </summary>
    public static SObjectRef GetContextTemporary(nint contextOop, int index)
    {
        return GetInstanceVariable(contextOop, 5 + index); // Temps start at index 5
    }

    /// <summary>
    /// Sets a temporary variable in a context.
    /// </summary>
    public static void SetContextTemporary(nint contextOop, int index, SObjectRef value)
    {
        SetInstanceVariable(contextOop, 5 + index, value); // Temps start at index 5
    }

    #endregion
}