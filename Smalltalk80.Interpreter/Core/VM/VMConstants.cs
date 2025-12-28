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
    // Blue Book: MethodContext field indices
    // ========================================================================
    // MethodContext inherits: InstructionStream (sender, pc) → ContextPart (stackp)
    // MethodContext adds: method, receiver
    //
    // Blue Book source (Smalltalk-80.st):
    //   ContextPart variableSubclass: #MethodContext
    //   instanceVariableNames: 'method lexicalParent receiver'
    //
    // Complete layout:
    //   Instance variable 0: sender (from InstructionStream) - caller context
    //   Instance variable 1: pc (from InstructionStream) - program counter
    //   Instance variable 2: stackp (from ContextPart) - stack pointer
    //   Instance variable 3: method (MethodContext field) - CompiledMethod being executed
    //   Instance variable 4: lexicalParent (MethodContext field) - context where method/block was defined
    //   Instance variable 5: receiver (MethodContext field) - self
    //   Instance variable 6+: indexed temps/stack (variable part)

    // Common context fields (shared by all contexts)
    public const int CONTEXT_SENDER_INDEX = 0;           // InstructionStream field (dynamic call chain)
    public const int CONTEXT_PC_INDEX = 1;               // InstructionStream field
    public const int CONTEXT_STACKP_INDEX = 2;           // ContextPart field
    public const int CONTEXT_METHOD_INDEX = 3;           // MethodContext field - CompiledMethod
    public const int CONTEXT_LEXICALPARENT_INDEX = 4;    // MethodContext field - lexical parent context
    public const int CONTEXT_RECEIVER_INDEX = 5;         // MethodContext field - self
    public const int CONTEXT_TEMP_FRAME_START = 6;       // Start of indexed slots

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
    // BLUE BOOK COMPLIANT UNIFIED TEMP FRAME ARCHITECTURE:
    // All temporaries (method args/temps + ALL block args/temps) are allocated in the
    // MethodContext's temp frame using absolute indices. The compiler performs static
    // analysis to allocate slots with reuse for sequential non-overlapping blocks.
    //
    // BLOCK ARGUMENT HANDLING (Blue Book Chapter 30):
    // - Primitive 81 (value/value:/value:value:) pushes args onto BlockContext's stack
    // - Block body begins with STORE_TEMP bytecodes to pop args into correct temp slots
    // - This handles non-contiguous slot mappings (e.g., block args reusing outer temps)
    //
    // Complete layout (Blue Book):
    //   Instance variable 0: sender (from InstructionStream) - caller field, set by primitiveValue
    //   Instance variable 1: pc (from InstructionStream) - instruction pointer
    //   Instance variable 2: stackp (from ContextPart) - stack pointer
    //   Instance variable 3: nargs (BlockContext field) - block argument count
    //   Instance variable 4: startpc (BlockContext field) - saved PC (called initialIP in Blue Book)
    //   Instance variable 5: home (BlockContext field) - lexical parent (always the MethodContext)
    //   Instance variable 6+: indexed slots - expression stack (also receives args from Primitive 81)
    //
    // NOTE: BlockContext shares the first 3 fields with MethodContext (sender, pc, stackp) but these
    // constants are defined separately for clarity in primitive implementations.
    //
    // Blue Book BlockContext - used by primitives 80, 81, 82
    public const int BLOCKCONTEXT_CALLER_INDEX = 0;        // sender field (from InstructionStream)
    public const int BLOCKCONTEXT_PC_INDEX = 1;            // pc field (from InstructionStream)
    public const int BLOCKCONTEXT_STACKP_INDEX = 2;        // stackp field (from ContextPart)
    public const int BLOCKCONTEXT_NARGS_INDEX = 3;         // nargs field (BlockContext)
    public const int BLOCKCONTEXT_STARTPC_INDEX = 4;       // startpc/initialIP field (BlockContext) - WAS 5
    public const int BLOCKCONTEXT_HOME_INDEX = 5;          // home field (BlockContext) - WAS 6

    // BLUE BOOK: BlockContext has 6 instance variables (indices 0-5)
    // Block arguments are now handled via STORE_TEMP bytecodes at block body start
    public const int BLOCKCONTEXT_TEMP_FRAME_START = 6;    // First indexed slot (after instance variables)

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
    // IdentityDictionary field indices
    // ========================================================================
    // IdentityDictionary inherits from Dictionary which inherits from Set
    // IdentityDictionary adds: 'valueArray'
    // Complete layout:
    //   Instance variable 0: tally (from Set)
    //   Instance variable 1: valueArray (IdentityDictionary field) - Array of values
    // Keys are stored in the indexed slots (PointerIndexed), values in parallel valueArray

    public const int IDENTITY_DICTIONARY_TALLY_INDEX = 0;       // SmallInteger count (inherited from Set)
    public const int IDENTITY_DICTIONARY_VALUE_ARRAY_INDEX = 1; // Array of values

    // MethodDictionary inherits from IdentityDictionary with no additional instance variables
    public const int METHOD_DICTIONARY_TALLY_INDEX = 0;         // SmallInteger count (inherited from Set)
    public const int METHOD_DICTIONARY_VALUE_ARRAY_INDEX = 1;   // Array of CompiledMethods

    // ========================================================================
    // Class field indices (Blue Book)
    // ========================================================================
    // Class inherits from: Behavior → ClassDescription
    // Class adds: 'name classPool sharedPools'
    // Complete layout:
    //   Instance variable 0: superclass (from Behavior)
    //   Instance variable 1: methodDict (from Behavior)
    //   Instance variable 2: format (from Behavior)
    //   Instance variable 3: subclasses (from Behavior)
    //   Instance variable 4: instanceVariables (from ClassDescription)
    //   Instance variable 5: organization (from ClassDescription)
    //   Instance variable 6: name (Class field) - Symbol
    //   Instance variable 7: classPool (Class field) - Dictionary of class variables
    //   Instance variable 8: sharedPools (Class field) - Array of pool dictionaries

    public const int CLASS_NAME_INDEX = 6;            // Symbol naming the class
    public const int CLASS_CLASSPOOL_INDEX = 7;       // Dictionary of class variables
    public const int CLASS_SHAREDPOOLS_INDEX = 8;     // Array of pool dictionaries

    // ========================================================================
    // Metaclass field indices
    // ========================================================================
    // Metaclass inherits from: Behavior → ClassDescription
    // Note: Metaclass does NOT inherit from Class - it's a parallel hierarchy
    // Metaclass adds: 'thisClass'
    // Complete layout:
    //   Instance variable 0: superclass (from Behavior)
    //   Instance variable 1: methodDict (from Behavior)
    //   Instance variable 2: format (from Behavior)
    //   Instance variable 3: subclasses (from Behavior)
    //   Instance variable 4: instanceVariables (from ClassDescription)
    //   Instance variable 5: organization (from ClassDescription)
    //   Instance variable 6: thisClass (Metaclass field) - points to the regular class
    // This field is critical for resolving class variables in metaclass methods

    public const int METACLASS_THISCLASS_INDEX = 6;  // Instance variable 6: thisClass (points to the regular class)

    // ========================================================================
    // Process and ProcessorScheduler field indices
    // ========================================================================
    // These are fixed by the Smalltalk-80 class definitions and won't change at runtime.

    // Process inherits from Link (which has instance variable 'nextLink')
    // Process adds: 'suspendedContext priority myList'
    // Complete layout:
    //   Instance variable 0: nextLink (from Link) - next process in linked list
    //   Instance variable 1: suspendedContext (Process field) - saved context when suspended
    //   Instance variable 2: priority (Process field) - process priority
    //   Instance variable 3: myList (Process field) - process list this process belongs to
    public const int PROCESS_NEXTLINK_INDEX = 0;
    public const int PROCESS_SUSPENDEDCONTEXT_INDEX = 1;
    public const int PROCESS_PRIORITY_INDEX = 2;
    public const int PROCESS_MYLIST_INDEX = 3;

    // ProcessorScheduler instanceVariableNames: 'quiescentProcessLists activeProcess'
    public const int PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX = 0;
    public const int PROCESSORSCHEDULER_ACTIVEPROCESS_INDEX = 1;

    // LinkedList instanceVariableNames: 'firstLink lastLink'
    // Note: LinkedList inherits from SequenceableCollection which has no instance vars
    public const int LINKEDLIST_FIRSTLINK_INDEX = 0;
    public const int LINKEDLIST_LASTLINK_INDEX = 1;

    // Semaphore instanceVariableNames: 'excessSignals'
    // Semaphore extends LinkedList, so firstLink=0, lastLink=1, excessSignals=2
    public const int SEMAPHORE_EXCESSSIGNALS_INDEX = 2;

    // Link instanceVariableNames: 'nextLink'
    // Process, Semaphore signal queues, etc. inherit from Link
    public const int LINK_NEXTLINK_INDEX = 0;

    // ========================================================================
    // CharacterTable and CharacterScanner constants
    // ========================================================================
    // CharacterTable is an Array of 256 unique Character objects (ASCII 0-255)
    // Used by Character class for identity-based character comparison

    public const int CHARACTER_TABLE_SIZE = 256;

    // StopConditions array for CharacterScanner
    // Needs 258 entries for Smalltalk 1-based indices 1-258
    // EndOfRun = 257, CrossedX = 258 are sentinel indices
    public const int STOP_CONDITIONS_SIZE = 258;
    public const int STOP_CONDITIONS_END_OF_RUN = 257;   // Smalltalk 1-based index
    public const int STOP_CONDITIONS_CROSSED_X = 258;    // Smalltalk 1-based index

    // ========================================================================
    // Point field indices (Blue Book)
    // ========================================================================
    // Point instanceVariableNames: 'x y'
    // Used by primitive 18 (@) for fast Point creation
    // These indices are fixed by the Smalltalk-80 class definition

    public const int POINT_X_INDEX = 0;  // First instance variable: x coordinate
    public const int POINT_Y_INDEX = 1;  // Second instance variable: y coordinate
}
