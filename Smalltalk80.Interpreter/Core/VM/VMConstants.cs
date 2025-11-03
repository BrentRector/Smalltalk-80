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
    //
    // CRITICAL ARCHITECTURAL CONSTRAINT:
    // MethodContext slot 3 = method (CompiledMethod)
    // BlockContext slot 3 = nargs (SmallInteger)
    //
    // These are DIFFERENT and cannot be changed (defined by Smalltalk-80 Blue Book specification).
    // Code that reads slot 3 MUST verify the context type first!
    //
    // Our VM uses MethodContext for block execution (CreateBlockExecutionContext creates MethodContext),
    // so _activeContextOOP is always a MethodContext. BlockContext is only used as a data holder.
    // This is validated at runtime in SendSuperMessage().
    public const int BLOCKCONTEXT_NARGS_INDEX = 3;       // BlockContext field - BlockArgumentCountIndex (DIFFERENT from MethodContext!)
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

    // ========================================================================
    // Blue Book: Association field indices
    // ========================================================================
    // Association is a fundamental class used for global bindings, class pools, etc.
    // These indices are fixed by the Smalltalk-80 specification.

    public const int ASSOCIATION_KEY_INDEX = 0;    // First instance variable: key
    public const int ASSOCIATION_VALUE_INDEX = 1;  // Second instance variable: value

    // ========================================================================
    // Dictionary and Set field indices
    // ========================================================================
    // Set instanceVariableNames: 'tally'
    // Dictionary is a variableSubclass of Set with no additional instance variables
    // Dictionary stores Association objects in its indexed slots (PointerIndexed)

    public const int SET_TALLY_INDEX = 0;           // SmallInteger count of entries
    public const int DICTIONARY_TALLY_INDEX = 0;    // Same as SET_TALLY_INDEX (inherited from Set)

    // ========================================================================
    // Metaclass field indices
    // ========================================================================
    // Metaclass instanceVariableNames: 'thisClass' (plus inherited from Class/ClassDescription/Behavior)
    // This field is critical for resolving class variables in metaclass methods

    public const int METACLASS_THISCLASS_INDEX = 6;  // Slot 6: thisClass (points to the regular class)

    // ========================================================================
    // Process and ProcessorScheduler field indices
    // ========================================================================
    // These are fixed by the Smalltalk-80 class definitions and won't change at runtime.

    // Process instanceVariableNames: 'suspendedContext priority myList'
    public const int PROCESS_SUSPENDEDCONTEXT_INDEX = 0;
    public const int PROCESS_PRIORITY_INDEX = 1;
    public const int PROCESS_MYLIST_INDEX = 2;

    // ProcessorScheduler instanceVariableNames: 'quiescentProcessLists activeProcess'
    public const int PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX = 0;
    public const int PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX = 1;
}
