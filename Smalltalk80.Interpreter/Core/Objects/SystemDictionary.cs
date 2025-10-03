namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Config;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// System dictionary holding all core Smalltalk classes.
/// Creates the complete Smalltalk-80 class hierarchy with 60 classes using pure SObjectRef architecture.
/// </summary>
public static partial class SystemDictionary
{
    private static bool initialized = false;


    // Singleton instances

    /// <summary>
    /// Gets the singleton True object.
    /// </summary>
    public static SObjectRef TrueObject { get; private set; }

    /// <summary>
    /// Gets the singleton False object.
    /// </summary>
    public static SObjectRef FalseObject { get; private set; }

    /// <summary>
    /// Gets the singleton nil object.
    /// </summary>
    public static SObjectRef NilObject { get; private set; }

    /// <summary>
    /// Gets the system dictionary instance itself as an SObjectRef.
    /// </summary>
    public static SObjectRef Instance { get; private set; }

    // Core class references (SObjectRef-based)

    /// <summary>Reference to the Object class.</summary>
    public static SObjectRef ObjectClassRef { get; private set; }
    /// <summary>Reference to the Behavior class.</summary>
    public static SObjectRef BehaviorClassRef { get; private set; }
    /// <summary>Reference to the ClassDescription class.</summary>
    public static SObjectRef ClassDescriptionClassRef { get; private set; }
    /// <summary>Reference to the Metaclass class.</summary>
    public static SObjectRef MetaclassClassRef { get; private set; }
    /// <summary>Reference to the Class class.</summary>
    public static SObjectRef ClassClassRef { get; private set; }
    /// <summary>Reference to the CompiledMethod class.</summary>
    public static SObjectRef CompiledMethodClassRef { get; private set; }
    /// <summary>Reference to the InstructionStream class.</summary>
    public static SObjectRef InstructionStreamClassRef { get; private set; }
    /// <summary>Reference to the ContextPart class.</summary>
    public static SObjectRef ContextPartClassRef { get; private set; }
    /// <summary>Reference to the MethodContext class.</summary>
    public static SObjectRef MethodContextClassRef { get; private set; }
    /// <summary>Reference to the BlockContext class.</summary>
    public static SObjectRef BlockContextClassRef { get; private set; }
    /// <summary>Reference to the Collection class.</summary>
    public static SObjectRef CollectionClassRef { get; private set; }
    /// <summary>Reference to the SequenceableCollection class.</summary>
    public static SObjectRef SequenceableCollectionClassRef { get; private set; }
    /// <summary>Reference to the OrderedCollection class.</summary>
    public static SObjectRef OrderedCollectionClassRef { get; private set; }
    /// <summary>Reference to the Set class.</summary>
    public static SObjectRef SetClassRef { get; private set; }
    /// <summary>Reference to the Dictionary class.</summary>
    public static SObjectRef DictionaryClassRef { get; private set; }
    /// <summary>Reference to the IdentityDictionary class.</summary>
    public static SObjectRef IdentityDictionaryClassRef { get; private set; }
    /// <summary>Reference to the MethodDictionary class.</summary>
    public static SObjectRef MethodDictionaryClassRef { get; private set; }
    /// <summary>Reference to the Array class.</summary>
    public static SObjectRef ArrayClassRef { get; private set; }
    /// <summary>Reference to the ByteArray class.</summary>
    public static SObjectRef ByteArrayClassRef { get; private set; }
    /// <summary>Reference to the String class.</summary>
    public static SObjectRef StringClassRef { get; private set; }
    /// <summary>Reference to the Symbol class.</summary>
    public static SObjectRef SymbolClassRef { get; private set; }
    /// <summary>Reference to the Magnitude class.</summary>
    public static SObjectRef MagnitudeClassRef { get; private set; }
    /// <summary>Reference to the Character class.</summary>
    public static SObjectRef CharacterClassRef { get; private set; }
    /// <summary>Reference to the Number class.</summary>
    public static SObjectRef NumberClassRef { get; private set; }
    /// <summary>Reference to the Integer class.</summary>
    public static SObjectRef IntegerClassRef { get; private set; }
    /// <summary>Reference to the SmallInteger class.</summary>
    public static SObjectRef SmallIntegerClassRef { get; private set; }
    /// <summary>Reference to the Float class.</summary>
    public static SObjectRef FloatClassRef { get; private set; }
    /// <summary>Reference to the LargePositiveInteger class.</summary>
    public static SObjectRef LargePositiveIntegerClassRef { get; private set; }
    /// <summary>Reference to the LargeNegativeInteger class.</summary>
    public static SObjectRef LargeNegativeIntegerClassRef { get; private set; }
    /// <summary>Reference to the UndefinedObject class.</summary>
    public static SObjectRef UndefinedObjectClassRef { get; private set; }
    /// <summary>Reference to the Boolean class.</summary>
    public static SObjectRef BooleanClassRef { get; private set; }
    /// <summary>Reference to the True class.</summary>
    public static SObjectRef TrueClassRef { get; private set; }
    /// <summary>Reference to the False class.</summary>
    public static SObjectRef FalseClassRef { get; private set; }
    /// <summary>Reference to the Link class.</summary>
    public static SObjectRef LinkClassRef { get; private set; }
    /// <summary>Reference to the Process class.</summary>
    public static SObjectRef ProcessClassRef { get; private set; }
    /// <summary>Reference to the LinkedList class.</summary>
    public static SObjectRef LinkedListClassRef { get; private set; }
    /// <summary>Reference to the Semaphore class.</summary>
    public static SObjectRef SemaphoreClassRef { get; private set; }
    /// <summary>Reference to the ProcessorScheduler class.</summary>
    public static SObjectRef ProcessorSchedulerClassRef { get; private set; }
    /// <summary>Reference to the LookupKey class.</summary>
    public static SObjectRef LookupKeyClassRef { get; private set; }
    /// <summary>Reference to the Association class.</summary>
    public static SObjectRef AssociationClassRef { get; private set; }
    /// <summary>Reference to the SystemDictionary class.</summary>
    public static SObjectRef SystemDictionaryClassRef { get; private set; }
    /// <summary>Reference to the Stream class.</summary>
    public static SObjectRef StreamClassRef { get; private set; }
    /// <summary>Reference to the PositionableStream class.</summary>
    public static SObjectRef PositionableStreamClassRef { get; private set; }
    /// <summary>Reference to the ReadStream class.</summary>
    public static SObjectRef ReadStreamClassRef { get; private set; }
    /// <summary>Reference to the WriteStream class.</summary>
    public static SObjectRef WriteStreamClassRef { get; private set; }
    /// <summary>Reference to the Text class.</summary>
    public static SObjectRef TextClassRef { get; private set; }
    /// <summary>Reference to the TextCollector class.</summary>
    public static SObjectRef TextCollectorClassRef { get; private set; }
    /// <summary>Reference to the StringHolder class.</summary>
    public static SObjectRef StringHolderClassRef { get; private set; }
    /// <summary>Reference to the ParseNode class.</summary>
    public static SObjectRef ParseNodeClassRef { get; private set; }
    /// <summary>Reference to the LeafNode class.</summary>
    public static SObjectRef LeafNodeClassRef { get; private set; }
    /// <summary>Reference to the VariableNode class.</summary>
    public static SObjectRef VariableNodeClassRef { get; private set; }
    /// <summary>Reference to the LiteralNode class.</summary>
    public static SObjectRef LiteralNodeClassRef { get; private set; }
    /// <summary>Reference to the SelectorNode class.</summary>
    public static SObjectRef SelectorNodeClassRef { get; private set; }
    /// <summary>Reference to the MessageNode class.</summary>
    public static SObjectRef MessageNodeClassRef { get; private set; }
    /// <summary>Reference to the AssignmentNode class.</summary>
    public static SObjectRef AssignmentNodeClassRef { get; private set; }
    /// <summary>Reference to the ReturnNode class.</summary>
    public static SObjectRef ReturnNodeClassRef { get; private set; }
    /// <summary>Reference to the BlockNode class.</summary>
    public static SObjectRef BlockNodeClassRef { get; private set; }
    /// <summary>Reference to the CascadeNode class.</summary>
    public static SObjectRef CascadeNodeClassRef { get; private set; }
    /// <summary>Reference to the MethodNode class.</summary>
    public static SObjectRef MethodNodeClassRef { get; private set; }
    /// <summary>Reference to the Scanner class.</summary>
    public static SObjectRef ScannerClassRef { get; private set; }
    /// <summary>Reference to the Parser class.</summary>
    public static SObjectRef ParserClassRef { get; private set; }
    /// <summary>Reference to the Compiler class.</summary>
    public static SObjectRef CompilerClassRef { get; private set; }
    /// <summary>Reference to the Encoder class.</summary>
    public static SObjectRef EncoderClassRef { get; private set; }

    /// <summary>
    /// Initializes the system dictionary with the complete Smalltalk-80 class hierarchy.
    /// Creates 60 core classes using pure SObjectRef architecture.
    /// </summary>
    public static void Initialize()
    {
        if (initialized) return;

        LogColdStart("Starting cold start initialization with pure SObjectRef architecture...");
        LogColdStart("Creating complete Smalltalk-80 class hierarchy (60 classes)");

        // Phase 1: Core Object Model Classes (5 classes per Blue Book)
        // Special handling for the bootstrap classes due to circular dependencies
        // Note: CreateClassObject handles the case where MethodDictionary class doesn't exist yet

        LogColdStart("Creating Object class");
        ObjectClassRef = ObjectMemoryNew.CreateClassObject("Object", SObjectRef.Nil, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Object", ObjectClassRef);

        LogColdStart("Creating Behavior class");
        BehaviorClassRef = ObjectMemoryNew.CreateClassObject("Behavior", ObjectClassRef, ["superclass", "methodDict", "format"], ObjectFormat.FixedPointer);
        RegisterClassRef("Behavior", BehaviorClassRef);

        LogColdStart("Creating ClassDescription class");
        ClassDescriptionClassRef = ObjectMemoryNew.CreateClassObject("ClassDescription", BehaviorClassRef, ["instanceVariables", "organization"], ObjectFormat.FixedPointer);
        RegisterClassRef("ClassDescription", ClassDescriptionClassRef);

        LogColdStart("Creating Metaclass class");
        MetaclassClassRef = ObjectMemoryNew.CreateClassObject("Metaclass", ClassDescriptionClassRef, ["thisClass"], ObjectFormat.FixedPointer);
        RegisterClassRef("Metaclass", MetaclassClassRef);

        LogColdStart("Creating Class class");
        ClassClassRef = ObjectMemoryNew.CreateClassObject("Class", ClassDescriptionClassRef, ["name", "classPool", "sharedPools"], ObjectFormat.FixedPointer);
        RegisterClassRef("Class", ClassClassRef);

        // Phase 2: Method Storage Classes
        LogColdStart("Creating CompiledMethod class");
        // CompiledMethod instances have no named instance variables - literals are stored in indexed section
        CompiledMethodClassRef = ObjectMemoryNew.CreateClassObject("CompiledMethod", ObjectClassRef, [], ObjectFormat.CompiledMethod);
        RegisterClassRef("CompiledMethod", CompiledMethodClassRef);
        ObjectMemoryNew.SetWellKnownClass("CompiledMethod", CompiledMethodClassRef);

        // MethodDictionary will be created later after IdentityDictionary (Phase 4)

        // Phase 3: Execution Context Classes
        LogColdStart("Creating InstructionStream class");
        InstructionStreamClassRef = ObjectMemoryNew.CreateClassObject("InstructionStream", ObjectClassRef, ["sender", "pc"], ObjectFormat.FixedPointer);
        RegisterClassRef("InstructionStream", InstructionStreamClassRef);

        LogColdStart("Creating ContextPart class");
        ContextPartClassRef = ObjectMemoryNew.CreateClassObject("ContextPart", InstructionStreamClassRef, ["sender", "ip", "sp", "method", "receiverMap"], ObjectFormat.PointerIndexed);
        RegisterClassRef("ContextPart", ContextPartClassRef);

        LogColdStart("Creating MethodContext class");
        MethodContextClassRef = ObjectMemoryNew.CreateClassObject("MethodContext", ContextPartClassRef, ["receiver"], ObjectFormat.PointerIndexed);
        RegisterClassRef("MethodContext", MethodContextClassRef);

        LogColdStart("Creating BlockContext class");
        BlockContextClassRef = ObjectMemoryNew.CreateClassObject("BlockContext", ContextPartClassRef, ["nargs", "startpc", "home"], ObjectFormat.PointerIndexed);
        RegisterClassRef("BlockContext", BlockContextClassRef);

        // Phase 4: Collection Classes
        LogColdStart("Creating Collection class");
        CollectionClassRef = ObjectMemoryNew.CreateClassObject("Collection", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Collection", CollectionClassRef);

        LogColdStart("Creating SequenceableCollection class");
        SequenceableCollectionClassRef = ObjectMemoryNew.CreateClassObject("SequenceableCollection", CollectionClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("SequenceableCollection", SequenceableCollectionClassRef);

        LogColdStart("Creating OrderedCollection class");
        OrderedCollectionClassRef = ObjectMemoryNew.CreateClassObject("OrderedCollection", SequenceableCollectionClassRef, ["firstIndex", "lastIndex"], ObjectFormat.PointerIndexed);
        RegisterClassRef("OrderedCollection", OrderedCollectionClassRef);

        LogColdStart("Creating Set class");
        SetClassRef = ObjectMemoryNew.CreateClassObject("Set", CollectionClassRef, ["tally"], ObjectFormat.PointerIndexed);
        RegisterClassRef("Set", SetClassRef);

        LogColdStart("Creating ByteArray class");
        ByteArrayClassRef = ObjectMemoryNew.CreateClassObject("ByteArray", SequenceableCollectionClassRef, [], ObjectFormat.ByteIndexed);
        RegisterClassRef("ByteArray", ByteArrayClassRef);

        LogColdStart("Creating String class");
        StringClassRef = ObjectMemoryNew.CreateClassObject("String", ByteArrayClassRef, [], ObjectFormat.ByteIndexed);
        RegisterClassRef("String", StringClassRef);

        LogColdStart("Creating Symbol class");
        SymbolClassRef = ObjectMemoryNew.CreateClassObject("Symbol", StringClassRef, [], ObjectFormat.ByteIndexed);
        RegisterClassRef("Symbol", SymbolClassRef);
        ObjectMemoryNew.SetWellKnownClass("Symbol", SymbolClassRef);

        LogColdStart("Creating Array class");
        ArrayClassRef = ObjectMemoryNew.CreateClassObject("Array", SequenceableCollectionClassRef, [], ObjectFormat.PointerIndexed);
        RegisterClassRef("Array", ArrayClassRef);

        // Phase 5: Numeric Class Hierarchy (moved before collections to provide Magnitude for LookupKey)
        LogColdStart("Creating Magnitude class");
        MagnitudeClassRef = ObjectMemoryNew.CreateClassObject("Magnitude", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Magnitude", MagnitudeClassRef);

        LogColdStart("Creating LookupKey class");
        LookupKeyClassRef = ObjectMemoryNew.CreateClassObject("LookupKey", MagnitudeClassRef, ["key"], ObjectFormat.FixedPointer);
        RegisterClassRef("LookupKey", LookupKeyClassRef);

        LogColdStart("Creating Association class");
        AssociationClassRef = ObjectMemoryNew.CreateClassObject("Association", LookupKeyClassRef, ["value"], ObjectFormat.FixedPointer);
        RegisterClassRef("Association", AssociationClassRef);

        LogColdStart("Creating Dictionary class");
        DictionaryClassRef = ObjectMemoryNew.CreateClassObject("Dictionary", SetClassRef, [], ObjectFormat.PointerIndexed);

        // Create SystemDictionary instance NOW, before registering any more classes
        // This allows us to use it as the sole source of truth for class storage
        LogColdStart("Creating SystemDictionary instance");
        Instance = DictionaryClassRef.CreateInstance(100);

        // Add all previously created classes to the Instance
        // (These were created before Dictionary class existed)
        var preDictionaryClasses = new[] {
            ("Object", ObjectClassRef),
            ("Behavior", BehaviorClassRef),
            ("ClassDescription", ClassDescriptionClassRef),
            ("Metaclass", MetaclassClassRef),
            ("Class", ClassClassRef),
            ("CompiledMethod", CompiledMethodClassRef),
            ("InstructionStream", InstructionStreamClassRef),
            ("ContextPart", ContextPartClassRef),
            ("MethodContext", MethodContextClassRef),
            ("BlockContext", BlockContextClassRef),
            ("Collection", CollectionClassRef),
            ("SequenceableCollection", SequenceableCollectionClassRef),
            ("OrderedCollection", OrderedCollectionClassRef),
            ("Set", SetClassRef),
            ("ByteArray", ByteArrayClassRef),
            ("String", StringClassRef),
            ("Symbol", SymbolClassRef),
            ("Array", ArrayClassRef),
            ("Magnitude", MagnitudeClassRef),
            ("LookupKey", LookupKeyClassRef),
            ("Association", AssociationClassRef)
        };

        foreach (var (name, classRef) in preDictionaryClasses)
        {
            var nameSymbol = ObjectMemoryNew.InternSymbol(name);
            ObjectMemoryNew.DictionaryAtPut(Instance, nameSymbol, classRef);
        }

        // Now register Dictionary class and all subsequent classes will go directly into Instance
        RegisterClassRef("Dictionary", DictionaryClassRef);

        LogColdStart("Creating IdentityDictionary class");
        IdentityDictionaryClassRef = ObjectMemoryNew.CreateClassObject("IdentityDictionary", DictionaryClassRef, ["valueArray"], ObjectFormat.PointerIndexed);
        RegisterClassRef("IdentityDictionary", IdentityDictionaryClassRef);

        LogColdStart("Creating MethodDictionary class");
        MethodDictionaryClassRef = ObjectMemoryNew.CreateClassObject("MethodDictionary", IdentityDictionaryClassRef, [], ObjectFormat.PointerIndexed);
        RegisterClassRef("MethodDictionary", MethodDictionaryClassRef);

        LogColdStart("Creating SystemDictionary class");
        SystemDictionaryClassRef = ObjectMemoryNew.CreateClassObject("SystemDictionary", DictionaryClassRef, [], ObjectFormat.PointerIndexed);
        RegisterClassRef("SystemDictionary", SystemDictionaryClassRef);

        // Continue Numeric Class Hierarchy
        LogColdStart("Creating Number class");
        NumberClassRef = ObjectMemoryNew.CreateClassObject("Number", MagnitudeClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Number", NumberClassRef);

        LogColdStart("Creating Integer class");
        IntegerClassRef = ObjectMemoryNew.CreateClassObject("Integer", NumberClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Integer", IntegerClassRef);

        LogColdStart("Creating SmallInteger class");
        SmallIntegerClassRef = ObjectMemoryNew.CreateClassObject("SmallInteger", IntegerClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("SmallInteger", SmallIntegerClassRef);

        LogColdStart("Creating Float class");
        FloatClassRef = ObjectMemoryNew.CreateClassObject("Float", NumberClassRef, [], ObjectFormat.WordIndexed);
        RegisterClassRef("Float", FloatClassRef);

        LogColdStart("Creating LargePositiveInteger class");
        LargePositiveIntegerClassRef = ObjectMemoryNew.CreateClassObject("LargePositiveInteger", IntegerClassRef, [], ObjectFormat.ByteIndexed);
        RegisterClassRef("LargePositiveInteger", LargePositiveIntegerClassRef);

        LogColdStart("Creating LargeNegativeInteger class");
        LargeNegativeIntegerClassRef = ObjectMemoryNew.CreateClassObject("LargeNegativeInteger", IntegerClassRef, [], ObjectFormat.ByteIndexed);
        RegisterClassRef("LargeNegativeInteger", LargeNegativeIntegerClassRef);

        LogColdStart("Creating Character class");
        CharacterClassRef = ObjectMemoryNew.CreateClassObject("Character", MagnitudeClassRef, ["value"], ObjectFormat.FixedPointer);
        RegisterClassRef("Character", CharacterClassRef);

        // Phase 6: Boolean Classes
        LogColdStart("Creating Boolean class");
        BooleanClassRef = ObjectMemoryNew.CreateClassObject("Boolean", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Boolean", BooleanClassRef);

        LogColdStart("Creating True class");
        TrueClassRef = ObjectMemoryNew.CreateClassObject("True", BooleanClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("True", TrueClassRef);

        LogColdStart("Creating False class");
        FalseClassRef = ObjectMemoryNew.CreateClassObject("False", BooleanClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("False", FalseClassRef);

        LogColdStart("Creating UndefinedObject class");
        UndefinedObjectClassRef = ObjectMemoryNew.CreateClassObject("UndefinedObject", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("UndefinedObject", UndefinedObjectClassRef);

        // Phase 7: Process-Related Classes
        LogColdStart("Creating Link class");
        LinkClassRef = ObjectMemoryNew.CreateClassObject("Link", ObjectClassRef, ["nextLink"], ObjectFormat.FixedPointer);
        RegisterClassRef("Link", LinkClassRef);

        LogColdStart("Creating Process class");
        ProcessClassRef = ObjectMemoryNew.CreateClassObject("Process", LinkClassRef, ["suspendedContext", "priority", "myList"], ObjectFormat.FixedPointer);
        RegisterClassRef("Process", ProcessClassRef);

        LogColdStart("Creating LinkedList class");
        LinkedListClassRef = ObjectMemoryNew.CreateClassObject("LinkedList", ObjectClassRef, ["firstLink", "lastLink"], ObjectFormat.FixedPointer);
        RegisterClassRef("LinkedList", LinkedListClassRef);

        LogColdStart("Creating Semaphore class");
        SemaphoreClassRef = ObjectMemoryNew.CreateClassObject("Semaphore", LinkedListClassRef, ["excessSignals"], ObjectFormat.FixedPointer);
        RegisterClassRef("Semaphore", SemaphoreClassRef);

        LogColdStart("Creating ProcessorScheduler class");
        ProcessorSchedulerClassRef = ObjectMemoryNew.CreateClassObject("ProcessorScheduler", ObjectClassRef, ["quiescentProcessLists", "activeProcess"], ObjectFormat.FixedPointer);
        RegisterClassRef("ProcessorScheduler", ProcessorSchedulerClassRef);

        // Phase 8: I/O and Transcript Classes
        LogColdStart("Creating Stream class");
        StreamClassRef = ObjectMemoryNew.CreateClassObject("Stream", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("Stream", StreamClassRef);

        LogColdStart("Creating PositionableStream class");
        PositionableStreamClassRef = ObjectMemoryNew.CreateClassObject("PositionableStream", StreamClassRef, ["collection", "position", "readLimit"], ObjectFormat.FixedPointer);
        RegisterClassRef("PositionableStream", PositionableStreamClassRef);

        LogColdStart("Creating WriteStream class");
        WriteStreamClassRef = ObjectMemoryNew.CreateClassObject("WriteStream", PositionableStreamClassRef, ["writeLimit"], ObjectFormat.FixedPointer);
        RegisterClassRef("WriteStream", WriteStreamClassRef);

        LogColdStart("Creating ReadStream class");
        ReadStreamClassRef = ObjectMemoryNew.CreateClassObject("ReadStream", PositionableStreamClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("ReadStream", ReadStreamClassRef);

        LogColdStart("Creating StringHolder class");
        StringHolderClassRef = ObjectMemoryNew.CreateClassObject("StringHolder", ObjectClassRef, ["contents"], ObjectFormat.FixedPointer);
        RegisterClassRef("StringHolder", StringHolderClassRef);

        LogColdStart("Creating TextCollector class");
        TextCollectorClassRef = ObjectMemoryNew.CreateClassObject("TextCollector", StringHolderClassRef, ["entryStream"], ObjectFormat.FixedPointer);
        RegisterClassRef("TextCollector", TextCollectorClassRef);

        LogColdStart("Creating Text class");
        TextClassRef = ObjectMemoryNew.CreateClassObject("Text", ArrayClassRef, ["string", "runs"], ObjectFormat.PointerIndexed);
        RegisterClassRef("Text", TextClassRef);

        // Phase 9: Compilation Support Classes (15 classes)
        LogColdStart("Creating ParseNode class");
        ParseNodeClassRef = ObjectMemoryNew.CreateClassObject("ParseNode", ObjectClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("ParseNode", ParseNodeClassRef);

        LogColdStart("Creating LeafNode class");
        LeafNodeClassRef = ObjectMemoryNew.CreateClassObject("LeafNode", ParseNodeClassRef, ["key", "code"], ObjectFormat.FixedPointer);
        RegisterClassRef("LeafNode", LeafNodeClassRef);

        LogColdStart("Creating Scanner class");
        ScannerClassRef = ObjectMemoryNew.CreateClassObject("Scanner", ObjectClassRef, ["source", "mark", "hereChar", "aheadChar", "token", "tokenType", "currentComment", "buffer", "typeTable"], ObjectFormat.FixedPointer);
        RegisterClassRef("Scanner", ScannerClassRef);

        LogColdStart("Creating Parser class");
        ParserClassRef = ObjectMemoryNew.CreateClassObject("Parser", ScannerClassRef, ["here", "hereType", "hereMark", "prevToken", "prevMark", "encoder", "requestor", "parseNode", "failBlock", "lastTempMark", "correctionDelta"], ObjectFormat.FixedPointer);
        RegisterClassRef("Parser", ParserClassRef);

        LogColdStart("Creating Compiler class");
        CompilerClassRef = ObjectMemoryNew.CreateClassObject("Compiler", ObjectClassRef, ["sourceStream", "requestor", "class", "context"], ObjectFormat.FixedPointer);
        RegisterClassRef("Compiler", CompilerClassRef);

        LogColdStart("Creating Encoder class");
        EncoderClassRef = ObjectMemoryNew.CreateClassObject("Encoder", ParseNodeClassRef, ["scopeTable", "nTemps", "supered", "requestor", "class", "literalStream", "selectorSet", "litIndSet", "litSet", "sourceRanges", "lastTempPos"], ObjectFormat.FixedPointer);
        RegisterClassRef("Encoder", EncoderClassRef);

        LogColdStart("Creating VariableNode class");
        VariableNodeClassRef = ObjectMemoryNew.CreateClassObject("VariableNode", LeafNodeClassRef, ["name", "index"], ObjectFormat.FixedPointer);
        RegisterClassRef("VariableNode", VariableNodeClassRef);

        LogColdStart("Creating LiteralNode class");
        LiteralNodeClassRef = ObjectMemoryNew.CreateClassObject("LiteralNode", LeafNodeClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("LiteralNode", LiteralNodeClassRef);

        LogColdStart("Creating SelectorNode class");
        SelectorNodeClassRef = ObjectMemoryNew.CreateClassObject("SelectorNode", LeafNodeClassRef, [], ObjectFormat.FixedPointer);
        RegisterClassRef("SelectorNode", SelectorNodeClassRef);

        LogColdStart("Creating MessageNode class");
        MessageNodeClassRef = ObjectMemoryNew.CreateClassObject("MessageNode", ParseNodeClassRef, ["receiver", "selector", "precedence", "special", "arguments", "sizes", "pc"], ObjectFormat.FixedPointer);
        RegisterClassRef("MessageNode", MessageNodeClassRef);

        LogColdStart("Creating AssignmentNode class");
        AssignmentNodeClassRef = ObjectMemoryNew.CreateClassObject("AssignmentNode", ParseNodeClassRef, ["variable", "value"], ObjectFormat.FixedPointer);
        RegisterClassRef("AssignmentNode", AssignmentNodeClassRef);

        LogColdStart("Creating ReturnNode class");
        ReturnNodeClassRef = ObjectMemoryNew.CreateClassObject("ReturnNode", ParseNodeClassRef, ["expr", "pc"], ObjectFormat.FixedPointer);
        RegisterClassRef("ReturnNode", ReturnNodeClassRef);

        LogColdStart("Creating BlockNode class");
        BlockNodeClassRef = ObjectMemoryNew.CreateClassObject("BlockNode", ParseNodeClassRef, ["arguments", "statements", "returns", "nArgsNode", "size", "remoteCopyNode", "sourceRange", "endPC"], ObjectFormat.FixedPointer);
        RegisterClassRef("BlockNode", BlockNodeClassRef);

        LogColdStart("Creating CascadeNode class");
        CascadeNodeClassRef = ObjectMemoryNew.CreateClassObject("CascadeNode", ParseNodeClassRef, ["receiver", "messages"], ObjectFormat.FixedPointer);
        RegisterClassRef("CascadeNode", CascadeNodeClassRef);

        LogColdStart("Creating MethodNode class");
        MethodNodeClassRef = ObjectMemoryNew.CreateClassObject("MethodNode", ParseNodeClassRef, ["selectorOrFalse", "precedence", "arguments", "block", "literals", "primitive", "encoder", "temporaries"], ObjectFormat.FixedPointer);
        RegisterClassRef("MethodNode", MethodNodeClassRef);

        // Phase 10: Create singleton objects as proper Smalltalk objects
        LogColdStart("Creating singleton objects (nil, true, false)");

        // Create nil as instance of UndefinedObject
        NilObject = UndefinedObjectClassRef.CreateInstance();
        // TODO: ObjectMemoryNew.SetWellKnownObject("nil", NilObject);

        // Create true as instance of True
        TrueObject = TrueClassRef.CreateInstance();
        // TODO: ObjectMemoryNew.SetWellKnownObject("true", TrueObject);

        // Create false as instance of False
        FalseObject = FalseClassRef.CreateInstance();
        // TODO: ObjectMemoryNew.SetWellKnownObject("false", FalseObject);

        // Create USTable (Unique String Table) for Symbol class
        // This MUST be done before any symbol interning that expects USTable to exist
        LogColdStart("Creating Symbol class USTable (symbol hash table)");
        var usTable = ColdStart.SymbolTableBuilder.CreateUSTable();
        ColdStart.SymbolTableBuilder.InstallUSTable(usTable);

        // Migrate all bootstrap symbols to USTable
        LogColdStart("Migrating bootstrap symbols to USTable");
        ObjectMemoryNew.MigrateBootstrapSymbolsToUSTable();

        // SystemDictionary.Instance was already created when Dictionary class was created
        // All classes have been stored directly in Instance via RegisterClassRef
        LogColdStart($"SystemDictionary instance contains all 61 core classes");
        // TODO: ObjectMemoryNew.SetWellKnownObject("Smalltalk", Instance);

        initialized = true;
    }

    /// <summary>
    /// Logs a cold start message if cold start logging is enabled.
    /// </summary>
    private static void LogColdStart(string message)
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] {message}");
        }
    }

    /// <summary>
    /// Registers a class in the SystemDictionary instance (sole source of truth).
    /// </summary>
    internal static void RegisterClassRef(string className, SObjectRef classRef)
    {
        // Store in the actual SystemDictionary instance if it exists
        // This is the ONLY storage - no C# dictionary duplication
        if (!Instance.IsNil)
        {
            var nameSymbol = ObjectMemoryNew.InternSymbol(className);
            ObjectMemoryNew.DictionaryAtPut(Instance, nameSymbol, classRef);
        }
    }

    /// <summary>
    /// Looks up a class by name and returns its SObjectRef from SystemDictionary.Instance.
    /// </summary>
    public static SObjectRef? LookupClassRef(string name)
    {
        if (Instance.IsNil)
            return null;

        var nameSymbol = ObjectMemoryNew.InternSymbol(name);
        return ObjectMemoryNew.DictionaryAt(Instance, nameSymbol);
    }

    /// <summary>
    /// Registers a class in the system dictionary (legacy - for compatibility).
    /// </summary>

    /// <summary>
    /// Looks up a class by name (legacy - for compatibility).
    /// </summary>


    /// <summary>
    /// Registers a global object as an SObjectRef (for migration to pure SObjectRef).
    /// </summary>
    public static void RegisterGlobalRef(string name, SObjectRef valueRef)
    {
        // Store in SystemDictionary.Instance (sole source of truth)
        if (!Instance.IsNil)
        {
            var nameSymbol = ObjectMemoryNew.InternSymbol(name);
            ObjectMemoryNew.DictionaryAtPut(Instance, nameSymbol, valueRef);
        }
    }

    /// <summary>
    /// Looks up a global variable by name and returns it as an SObjectRef.
    /// </summary>
    public static SObjectRef? LookupGlobalRef(string name)
    {
        if (Instance.IsNil)
            return null;

        var nameSymbol = ObjectMemoryNew.InternSymbol(name);
        return ObjectMemoryNew.DictionaryAt(Instance, nameSymbol);
    }

    /// <summary>
    /// Removes a global variable from the SystemDictionary.
    /// In Smalltalk-80, this is: Smalltalk removeKey: #ClassName
    /// </summary>
    public static void UnregisterGlobalRef(string name)
    {
        if (!Instance.IsNil)
        {
            var nameSymbol = ObjectMemoryNew.InternSymbol(name);
            ObjectMemoryNew.DictionaryRemoveKey(Instance, nameSymbol);
        }
    }

    /// <summary>
    /// Gets all class names.
    /// TODO: Implement by iterating through SystemDictionary.Instance
    /// </summary>
    public static IEnumerable<string> GetAllClassNames()
    {
        // TODO: Implement by reading from SystemDictionary.Instance
        // For now, return the class property names via reflection as a fallback
        return typeof(SystemDictionary)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(p => p.Name.EndsWith("ClassRef") && p.PropertyType == typeof(SObjectRef))
            .Select(p => p.Name.Replace("ClassRef", ""));
    }

    /// <summary>
    /// Gets all classes in the system (legacy - returns empty).
    /// </summary>


    /// <summary>
    /// Resets the system dictionary (for testing).
    /// </summary>
    internal static void Reset()
    {
        initialized = false;
        Instance = SObjectRef.Nil;
        // Note: Symbol table is now in heap (Symbol class -> classPool -> #USTable)
        // Bootstrap symbol table is only used during early cold start
    }
}