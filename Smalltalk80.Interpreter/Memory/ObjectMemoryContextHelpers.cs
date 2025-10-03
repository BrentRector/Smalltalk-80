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
        /// <summary>
        /// Index of the sender field in a MethodContext (offset 0).
        /// </summary>
        public const int SenderIndex = 0;

        /// <summary>
        /// Index of the instruction pointer field in a MethodContext (offset 1).
        /// </summary>
        public const int InstructionPointerIndex = 1;

        /// <summary>
        /// Index of the stack pointer field in a MethodContext (offset 2).
        /// </summary>
        public const int StackPointerIndex = 2;

        /// <summary>
        /// Index of the method field in a MethodContext (offset 3).
        /// </summary>
        public const int MethodIndex = 3;

        /// <summary>
        /// Index of the receiverMap field in a MethodContext (offset 4).
        /// </summary>
        public const int ReceiverMapIndex = 4;

        /// <summary>
        /// Index of the receiver field in a MethodContext (offset 5).
        /// </summary>
        public const int ReceiverIndex = 5;

        /// <summary>
        /// Base offset for temporary variables and stack in a MethodContext.
        /// Temporaries and stack start after all fixed fields (sender, ip, sp, method, receiverMap, receiver).
        /// </summary>
        public const int TemporaryBaseOffset = 6;
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
        // Stack is in indexed slots: sp - 6 = indexed slot index
        SetIndexedPointer(contextOop, sp - 6, value);
        SetContextStackPointer(contextOop, sp + 1);
    }

    /// <summary>
    /// Pops a value from the context's stack.
    /// </summary>
    public static SObjectRef ContextPop(nint contextOop)
    {
        var sp = GetContextStackPointer(contextOop);
        if (sp <= 6) // Stack starts after fixed fields (sender, ip, sp, method, receiverMap, receiver)
            throw new InvalidOperationException("Stack underflow");

        sp--;
        SetContextStackPointer(contextOop, sp);
        // Stack is in indexed slots: sp - 6 = indexed slot index
        return GetIndexedPointer(contextOop, sp - 6);
    }

    /// <summary>
    /// Gets the top value from the context's stack without popping.
    /// </summary>
    public static SObjectRef ContextTop(nint contextOop)
    {
        var sp = GetContextStackPointer(contextOop);
        if (sp <= 6) // Stack starts after fixed fields (sender, ip, sp, method, receiverMap, receiver)
            throw new InvalidOperationException("Stack is empty");

        // Stack is in indexed slots: (sp - 1) - 6 = indexed slot index
        return GetIndexedPointer(contextOop, sp - 1 - 6);
    }

    /// <summary>
    /// Gets a temporary variable from a context.
    /// Temps are stored in indexed slots (temp 0 = indexed slot 0).
    /// </summary>
    public static SObjectRef GetContextTemporary(nint contextOop, int index)
    {
        return GetIndexedPointer(contextOop, index);
    }

    /// <summary>
    /// Sets a temporary variable in a context.
    /// Temps are stored in indexed slots (temp 0 = indexed slot 0).
    /// </summary>
    public static void SetContextTemporary(nint contextOop, int index, SObjectRef value)
    {
        SetIndexedPointer(contextOop, index, value);
    }

    #endregion
}