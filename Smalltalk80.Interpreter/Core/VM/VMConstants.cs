namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Blue Book architectural constants and VM extension constants.
/// These field indices are fixed by the Smalltalk-80 VM specification and cannot change at runtime.
/// </summary>
public static class VMConstants
{
    // ========================================================================
    // Blue Book Chapter 27: Behavior field indices
    // ========================================================================
    // Source: "SuperclassIndex := 0. MessageDictionaryIndex := 1. InstanceSpecificationIndex := 2."

    public const int BEHAVIOR_SUPERCLASS_INDEX = 0;
    public const int BEHAVIOR_METHODDICT_INDEX = 1;
    public const int BEHAVIOR_FORMAT_INDEX = 2;

    // ========================================================================
    // Blue Book Chapter 27: Context object field indices
    // ========================================================================
    // Source: "SenderIndex := 0. InstructionPointerIndex := 1. StackPointerIndex := 2.
    //          MethodIndex := 3. ReceiverIndex := 5."

    // Common to all contexts (from InstructionStream and ContextPart)
    public const int CONTEXT_SENDER_INDEX = 0;           // InstructionStream field
    public const int CONTEXT_PC_INDEX = 1;               // InstructionStream field
    public const int CONTEXT_STACKP_INDEX = 2;           // ContextPart field

    // MethodContext-specific fields
    // MethodContext inherits: InstructionStream (sender, pc) -> ContextPart (stackp) -> MethodContext
    public const int METHODCONTEXT_METHOD_INDEX = 3;     // MethodContext field
    public const int METHODCONTEXT_RECEIVERMAP_INDEX = 4; // MethodContext field (implementation-specific)
    public const int METHODCONTEXT_RECEIVER_INDEX = 5;   // MethodContext field
    public const int METHODCONTEXT_TEMP_FRAME_START = 6; // Start of indexed slots

    // BlockContext-specific fields
    // BlockContext inherits: InstructionStream (sender, pc) -> ContextPart (stackp) -> BlockContext
    public const int BLOCKCONTEXT_NARGS_INDEX = 3;       // BlockContext field - BlockArgumentCountIndex
    public const int BLOCKCONTEXT_STARTPC_INDEX = 4;     // BlockContext field - InitialIPIndex
    public const int BLOCKCONTEXT_HOME_INDEX = 5;        // BlockContext field - HomeIndex
    public const int BLOCKCONTEXT_TEMP_FRAME_START = 6;  // Start of indexed slots

    // ========================================================================
    // VM Extension: BlockClosure field indices
    // ========================================================================
    // BlockClosure is a modern VM extension (not in original Blue Book) for proper lexical closures.
    // These indices are fixed by our VM implementation and treated as architectural constants.

    public const int BLOCKCLOSURE_METHOD_INDEX = 0;
    public const int BLOCKCLOSURE_HOMECONTEXT_INDEX = 1;
    public const int BLOCKCLOSURE_RETURNCONTEXT_INDEX = 2;
    public const int BLOCKCLOSURE_NUMARGS_INDEX = 3;
}
