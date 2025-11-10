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
    // Unified Context Architecture (Single-Context Design)
    // ========================================================================
    // All contexts (methods and blocks) use MethodContext with unified field layout.
    // lexicalParent field enables proper lexical scoping for blocks.
    //
    // MethodContext layout (slot numbers are instance variable indices):
    //   Slot 0: sender (from InstructionStream) - dynamic call chain
    //   Slot 1: pc (from InstructionStream) - program counter
    //   Slot 2: stackp (from ContextPart) - stack pointer
    //   Slot 3: method (MethodContext field) - CompiledMethod being executed
    //   Slot 4: lexicalParent (MethodContext field) - lexical scope chain (nil for methods, non-nil for blocks)
    //   Slot 5: receiver (MethodContext field) - self
    //   Slot 6+: indexed temps (variable part)

    // Common context fields (shared by all contexts)
    public const int CONTEXT_SENDER_INDEX = 0;           // InstructionStream field (dynamic call chain)
    public const int CONTEXT_PC_INDEX = 1;               // InstructionStream field
    public const int CONTEXT_STACKP_INDEX = 2;           // ContextPart field
    public const int CONTEXT_METHOD_INDEX = 3;           // MethodContext field - CompiledMethod
    public const int CONTEXT_LEXICALPARENT_INDEX = 4;    // MethodContext field - lexical scope chain (NEW!)
    public const int CONTEXT_RECEIVER_INDEX = 5;         // MethodContext field - self
    public const int CONTEXT_TEMP_FRAME_START = 6;       // Start of indexed slots

    // Legacy MethodContext constants (DEPRECATED - use CONTEXT_* instead)
    [Obsolete("Use CONTEXT_METHOD_INDEX instead")]
    public const int METHODCONTEXT_METHOD_INDEX = 4;
    [Obsolete("Use CONTEXT_RECEIVER_INDEX instead")]
    public const int METHODCONTEXT_RECEIVER_INDEX = 6;
    [Obsolete("Use CONTEXT_TEMP_FRAME_START instead")]
    public const int METHODCONTEXT_TEMP_FRAME_START = 7;

    // ========================================================================
    // Blue Book: BlockContext field indices (Primitives 80-82)
    // ========================================================================
    // BlockContext inherits: InstructionStream (sender, pc) → ContextPart (stackp)
    // BlockContext adds: nargs, startpc (called initialIP in Blue Book), home
    //
    // Blue Book source (Smalltalk-80.st line 4683):
    //   ContextPart variableSubclass: #BlockContext
    //   instanceVariableNames: 'nargs startpc home'
    //
    // Complete layout:
    //   Slot 0: sender (from InstructionStream) - caller field, set by primitiveValue
    //   Slot 1: pc (from InstructionStream) - instruction pointer
    //   Slot 2: stackp (from ContextPart) - stack pointer
    //   Slot 3: nargs (BlockContext field) - block argument count
    //   Slot 4: startpc (BlockContext field) - saved PC (called initialIP in Blue Book)
    //   Slot 5: home (BlockContext field) - lexical parent MethodContext
    //   Slot 6+: indexed temps/stack (variable part)
    //
    // Blue Book BlockContext - used by primitives 80, 81, 82
    public const int BLOCKCONTEXT_CALLER_INDEX = 0;        // sender field (from InstructionStream)
    public const int BLOCKCONTEXT_PC_INDEX = 1;            // pc field (from InstructionStream)
    public const int BLOCKCONTEXT_STACKP_INDEX = 2;        // stackp field (from ContextPart)
    public const int BLOCKCONTEXT_NARGS_INDEX = 3;         // nargs field (BlockContext)
    public const int BLOCKCONTEXT_STARTPC_INDEX = 4;       // startpc/initialIP field (BlockContext)
    public const int BLOCKCONTEXT_HOME_INDEX = 5;          // home field (BlockContext)
    public const int BLOCKCONTEXT_TEMP_FRAME_START = 6;    // First indexed slot

    // ========================================================================
    // VM Extension: BlockClosure field indices
    // ========================================================================
    // BlockClosure is a modern VM extension (not in original Blue Book) for proper lexical closures.
    // These indices are fixed by our VM implementation and treated as architectural constants.

    public const int BLOCKCLOSURE_METHOD_INDEX = 0;
    public const int BLOCKCLOSURE_HOMECONTEXT_INDEX = 1;
    public const int BLOCKCLOSURE_RETURNCONTEXT_INDEX = 2;
    public const int BLOCKCLOSURE_NUMARGS_INDEX = 3;
    public const int BLOCKCLOSURE_EXECUTIONCONTEXT_INDEX = 4;

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
