namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Config;

/// <summary>
/// System dictionary holding all core Smalltalk classes.
/// Creates the complete Smalltalk-80 class hierarchy with 43+ classes as per Blue Book specification.
/// </summary>
public static class SystemDictionary
{
    private static bool initialized = false;
    
    /// <summary>
    /// Gets the Object class.
    /// </summary>
    public static SClass ObjectClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Class class.
    /// </summary>
    public static SClass ClassClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Metaclass class.
    /// </summary>
    public static SClass MetaclassClass { get; internal set; } = null!;
    
    /// <summary>
    /// Gets the SmallInteger class.
    /// </summary>
    public static SClass SmallIntegerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Character class.
    /// </summary>
    public static SClass CharacterClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the String class.
    /// </summary>
    public static SClass StringClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Symbol class.
    /// </summary>
    public static SClass SymbolClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Array class.
    /// </summary>
    public static SClass ArrayClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the CompiledMethod class.
    /// </summary>
    public static SClass CompiledMethodClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the UndefinedObject class (for nil).
    /// </summary>
    public static SClass UndefinedObjectClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the True class.
    /// </summary>
    public static SClass TrueClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the False class.
    /// </summary>
    public static SClass FalseClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Boolean class.
    /// </summary>
    public static SClass BooleanClass { get; private set; } = null!;
    
    // Additional core classes from Blue Book
    
    /// <summary>
    /// Gets the Behavior class.
    /// </summary>
    public static SClass BehaviorClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ClassDescription class.
    /// </summary>
    public static SClass ClassDescriptionClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the MethodDictionary class.
    /// </summary>
    public static SClass MethodDictionaryClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ByteArray class.
    /// </summary>
    public static SClass ByteArrayClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Association class.
    /// </summary>
    public static SClass AssociationClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Dictionary class.
    /// </summary>
    public static SClass DictionaryClass { get; private set; } = null!;
    
    // Numeric hierarchy
    
    /// <summary>
    /// Gets the Magnitude class.
    /// </summary>
    public static SClass MagnitudeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Number class.
    /// </summary>
    public static SClass NumberClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Integer class.
    /// </summary>
    public static SClass IntegerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Float class.
    /// </summary>
    public static SClass FloatClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the LargePositiveInteger class.
    /// </summary>
    public static SClass LargePositiveIntegerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the LargeNegativeInteger class.
    /// </summary>
    public static SClass LargeNegativeIntegerClass { get; private set; } = null!;
    
    // Collection hierarchy classes
    
    /// <summary>
    /// Gets the Collection class.
    /// </summary>
    public static SClass CollectionClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the SequenceableCollection class.
    /// </summary>
    public static SClass SequenceableCollectionClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the OrderedCollection class.
    /// </summary>
    public static SClass OrderedCollectionClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Set class.
    /// </summary>
    public static SClass SetClass { get; private set; } = null!;
    
    // Process-related classes
    
    /// <summary>
    /// Gets the Link class.
    /// </summary>
    public static SClass LinkClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Process class.
    /// </summary>
    public static SClass ProcessClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the LinkedList class.
    /// </summary>
    public static SClass LinkedListClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Semaphore class.
    /// </summary>
    public static SClass SemaphoreClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ProcessorScheduler class.
    /// </summary>
    public static SClass ProcessorSchedulerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the InstructionStream class.
    /// </summary>
    public static SClass InstructionStreamClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ContextPart class.
    /// </summary>
    public static SClass ContextPartClass { get; private set; } = null!;
    
    // I/O and Transcript classes
    
    /// <summary>
    /// Gets the Stream class.
    /// </summary>
    public static SClass StreamClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the PositionableStream class.
    /// </summary>
    public static SClass PositionableStreamClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the WriteStream class.
    /// </summary>
    public static SClass WriteStreamClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ReadStream class.
    /// </summary>
    public static SClass ReadStreamClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the StringHolder class.
    /// </summary>
    public static SClass StringHolderClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the TextCollector class.
    /// </summary>
    public static SClass TextCollectorClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Text class.
    /// </summary>
    public static SClass TextClass { get; private set; } = null!;
    
    // Compilation Support Classes
    
    /// <summary>
    /// Gets the ParseNode class.
    /// </summary>
    public static SClass ParseNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the LeafNode class.
    /// </summary>
    public static SClass LeafNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Scanner class.
    /// </summary>
    public static SClass ScannerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Parser class.
    /// </summary>
    public static SClass ParserClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Compiler class.
    /// </summary>
    public static SClass CompilerClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the Encoder class.
    /// </summary>
    public static SClass EncoderClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the VariableNode class.
    /// </summary>
    public static SClass VariableNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the LiteralNode class.
    /// </summary>
    public static SClass LiteralNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the SelectorNode class.
    /// </summary>
    public static SClass SelectorNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the MessageNode class.
    /// </summary>
    public static SClass MessageNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the AssignmentNode class.
    /// </summary>
    public static SClass AssignmentNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the ReturnNode class.
    /// </summary>
    public static SClass ReturnNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the BlockNode class.
    /// </summary>
    public static SClass BlockNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the CascadeNode class.
    /// </summary>
    public static SClass CascadeNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the MethodNode class.
    /// </summary>
    public static SClass MethodNodeClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the MethodContext class.
    /// </summary>
    public static SClass MethodContextClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets the BlockContext class.
    /// </summary>
    public static SClass BlockContextClass { get; private set; } = null!;
    
    /// <summary>
    /// Gets all classes in the system.
    /// </summary>
    private static readonly Dictionary<string, SClass> allClasses = [];
    
    /// <summary>
    /// Gets all global variables.
    /// </summary>
    private static readonly Dictionary<string, SObject> globals = [];
    
    /// <summary>
    /// Initializes the system dictionary with the complete Smalltalk-80 class hierarchy.
    /// Creates 56 core classes (41 system + 15 compilation support), all hardcoded in proper inheritance order.
    /// </summary>
    public static void Initialize()
    {
        if (initialized) return;
        
        LogColdStart("Starting cold start initialization...");
        LogColdStart("Creating complete Smalltalk-80 class hierarchy (56 classes)");
        
        // Phase 1: Core Object Model Classes (5 classes per Blue Book)
        LogColdStart("Creating Object class");
        ObjectClass = new SClass("Object", null, []);
        RegisterClass(ObjectClass);
        
        LogColdStart("Creating Behavior class");
        BehaviorClass = new SClass("Behavior", ObjectClass, ["superclass", "methodDict", "format"]);
        RegisterClass(BehaviorClass);
        
        LogColdStart("Creating ClassDescription class");
        ClassDescriptionClass = new SClass("ClassDescription", BehaviorClass, ["instanceVariables", "organization"]);
        RegisterClass(ClassDescriptionClass);
        
        LogColdStart("Creating Metaclass class");
        MetaclassClass = new SClass("Metaclass", ClassDescriptionClass, ["thisClass"]);
        RegisterClass(MetaclassClass);
        
        LogColdStart("Creating Class class");
        ClassClass = new SClass("Class", ClassDescriptionClass, ["name", "classPool", "sharedPools"]);
        RegisterClass(ClassClass);
        
        // Phase 2: Method Storage Classes
        LogColdStart("Creating CompiledMethod class");
        CompiledMethodClass = new SClass("CompiledMethod", ObjectClass, ["bytecodes", "literals", "selector"]);
        RegisterClass(CompiledMethodClass);
        
        LogColdStart("Creating MethodDictionary class");
        MethodDictionaryClass = new SClass("MethodDictionary", ObjectClass, ["tally", "array"]);
        RegisterClass(MethodDictionaryClass);
        
        // Phase 3: Execution Context Classes
        LogColdStart("Creating InstructionStream class");
        InstructionStreamClass = new SClass("InstructionStream", ObjectClass, ["sender", "pc"]);
        RegisterClass(InstructionStreamClass);
        
        LogColdStart("Creating ContextPart class");
        ContextPartClass = new SClass("ContextPart", InstructionStreamClass, ["stackp", "stack", "method", "tempframe"]);
        RegisterClass(ContextPartClass);
        
        LogColdStart("Creating MethodContext class");
        MethodContextClass = new SClass("MethodContext", ContextPartClass, ["receiver"]);
        RegisterClass(MethodContextClass);
        
        LogColdStart("Creating BlockContext class");
        BlockContextClass = new SClass("BlockContext", ContextPartClass, ["nargs", "startpc", "home"]);
        RegisterClass(BlockContextClass);
        
        // Phase 4: Collection Classes
        LogColdStart("Creating Collection class");
        CollectionClass = new SClass("Collection", ObjectClass, []);
        RegisterClass(CollectionClass);
        
        LogColdStart("Creating SequenceableCollection class");
        SequenceableCollectionClass = new SClass("SequenceableCollection", CollectionClass, []);
        RegisterClass(SequenceableCollectionClass);
        
        LogColdStart("Creating OrderedCollection class");
        OrderedCollectionClass = new SClass("OrderedCollection", SequenceableCollectionClass, ["firstIndex", "lastIndex"]);
        RegisterClass(OrderedCollectionClass);
        
        LogColdStart("Creating Set class");
        SetClass = new SClass("Set", CollectionClass, ["tally", "array"]);
        RegisterClass(SetClass);
        
        LogColdStart("Creating ByteArray class");
        ByteArrayClass = new SClass("ByteArray", SequenceableCollectionClass, []);
        RegisterClass(ByteArrayClass);
        
        LogColdStart("Creating String class");
        StringClass = new SClass("String", ByteArrayClass, []);
        RegisterClass(StringClass);
        
        LogColdStart("Creating Symbol class");
        SymbolClass = new SClass("Symbol", StringClass, []);
        RegisterClass(SymbolClass);
        
        LogColdStart("Creating Array class");
        ArrayClass = new SClass("Array", SequenceableCollectionClass, []);
        RegisterClass(ArrayClass);
        
        LogColdStart("Creating Association class");
        AssociationClass = new SClass("Association", ObjectClass, ["key", "value"]);
        RegisterClass(AssociationClass);
        
        LogColdStart("Creating Dictionary class");
        DictionaryClass = new SClass("Dictionary", SetClass, []);
        RegisterClass(DictionaryClass);
        
        // Phase 5: Numeric Class Hierarchy
        LogColdStart("Creating Magnitude class");
        MagnitudeClass = new SClass("Magnitude", ObjectClass, []);
        RegisterClass(MagnitudeClass);
        
        LogColdStart("Creating Number class");
        NumberClass = new SClass("Number", MagnitudeClass, []);
        RegisterClass(NumberClass);
        
        LogColdStart("Creating Integer class");
        IntegerClass = new SClass("Integer", NumberClass, []);
        RegisterClass(IntegerClass);
        
        LogColdStart("Creating SmallInteger class");
        SmallIntegerClass = new SClass("SmallInteger", IntegerClass, []);
        RegisterClass(SmallIntegerClass);
        
        LogColdStart("Creating Float class");
        FloatClass = new SClass("Float", NumberClass, []);
        RegisterClass(FloatClass);
        
        LogColdStart("Creating LargePositiveInteger class");
        LargePositiveIntegerClass = new SClass("LargePositiveInteger", IntegerClass, []);
        RegisterClass(LargePositiveIntegerClass);
        
        LogColdStart("Creating LargeNegativeInteger class");
        LargeNegativeIntegerClass = new SClass("LargeNegativeInteger", IntegerClass, []);
        RegisterClass(LargeNegativeIntegerClass);
        
        LogColdStart("Creating Character class");
        CharacterClass = new SClass("Character", MagnitudeClass, ["value"]);
        RegisterClass(CharacterClass);
        
        // Phase 6: Boolean Classes
        LogColdStart("Creating Boolean class");
        BooleanClass = new SClass("Boolean", ObjectClass, []);
        RegisterClass(BooleanClass);
        
        LogColdStart("Creating True class");
        TrueClass = new SClass("True", BooleanClass, []);
        RegisterClass(TrueClass);
        
        LogColdStart("Creating False class");
        FalseClass = new SClass("False", BooleanClass, []);
        RegisterClass(FalseClass);
        
        LogColdStart("Creating UndefinedObject class");
        UndefinedObjectClass = new SClass("UndefinedObject", ObjectClass, []);
        RegisterClass(UndefinedObjectClass);
        
        // Phase 7: Process-Related Classes
        LogColdStart("Creating Link class");
        LinkClass = new SClass("Link", ObjectClass, ["nextLink"]);
        RegisterClass(LinkClass);
        
        LogColdStart("Creating Process class");
        ProcessClass = new SClass("Process", LinkClass, ["suspendedContext", "priority", "myList"]);
        RegisterClass(ProcessClass);
        
        LogColdStart("Creating LinkedList class");
        LinkedListClass = new SClass("LinkedList", ObjectClass, ["firstLink", "lastLink"]);
        RegisterClass(LinkedListClass);
        
        LogColdStart("Creating Semaphore class");
        SemaphoreClass = new SClass("Semaphore", LinkedListClass, ["excessSignals"]);
        RegisterClass(SemaphoreClass);
        
        LogColdStart("Creating ProcessorScheduler class");
        ProcessorSchedulerClass = new SClass("ProcessorScheduler", ObjectClass, ["quiescentProcessLists", "activeProcess"]);
        RegisterClass(ProcessorSchedulerClass);
        
        // Phase 8: I/O and Transcript Classes
        LogColdStart("Creating Stream class");
        StreamClass = new SClass("Stream", ObjectClass, []);
        RegisterClass(StreamClass);
        
        LogColdStart("Creating PositionableStream class");
        PositionableStreamClass = new SClass("PositionableStream", StreamClass, ["collection", "position", "readLimit"]);
        RegisterClass(PositionableStreamClass);
        
        LogColdStart("Creating WriteStream class");
        WriteStreamClass = new SClass("WriteStream", PositionableStreamClass, ["writeLimit"]);
        RegisterClass(WriteStreamClass);
        
        LogColdStart("Creating ReadStream class");
        ReadStreamClass = new SClass("ReadStream", PositionableStreamClass, []);
        RegisterClass(ReadStreamClass);
        
        LogColdStart("Creating StringHolder class");
        StringHolderClass = new SClass("StringHolder", ObjectClass, ["contents"]);
        RegisterClass(StringHolderClass);
        
        LogColdStart("Creating TextCollector class");
        TextCollectorClass = new SClass("TextCollector", StringHolderClass, ["entryStream"]);
        RegisterClass(TextCollectorClass);
        
        LogColdStart("Creating Text class");
        TextClass = new SClass("Text", ArrayClass, ["string", "runs"]);
        RegisterClass(TextClass);
        
        // Phase 9: Compilation Support Classes (15 classes)
        LogColdStart("Creating ParseNode class");
        ParseNodeClass = new SClass("ParseNode", ObjectClass, []);
        RegisterClass(ParseNodeClass);
        
        LogColdStart("Creating LeafNode class");
        LeafNodeClass = new SClass("LeafNode", ParseNodeClass, ["key", "code"]);
        RegisterClass(LeafNodeClass);
        
        LogColdStart("Creating Scanner class");
        ScannerClass = new SClass("Scanner", ObjectClass, ["source", "mark", "hereChar", "aheadChar", "token", "tokenType", "currentComment", "buffer", "typeTable"]);
        RegisterClass(ScannerClass);
        
        LogColdStart("Creating Parser class");
        ParserClass = new SClass("Parser", ScannerClass, ["here", "hereType", "hereMark", "prevToken", "prevMark", "encoder", "requestor", "parseNode", "failBlock", "lastTempMark", "correctionDelta"]);
        RegisterClass(ParserClass);
        
        LogColdStart("Creating Compiler class");
        CompilerClass = new SClass("Compiler", ObjectClass, ["sourceStream", "requestor", "class", "context"]);
        RegisterClass(CompilerClass);
        
        LogColdStart("Creating Encoder class");
        EncoderClass = new SClass("Encoder", ParseNodeClass, ["scopeTable", "nTemps", "supered", "requestor", "class", "literalStream", "selectorSet", "litIndSet", "litSet", "sourceRanges", "lastTempPos"]);
        RegisterClass(EncoderClass);
        
        LogColdStart("Creating VariableNode class");
        VariableNodeClass = new SClass("VariableNode", LeafNodeClass, ["name", "index"]);
        RegisterClass(VariableNodeClass);
        
        LogColdStart("Creating LiteralNode class");
        LiteralNodeClass = new SClass("LiteralNode", LeafNodeClass, []);
        RegisterClass(LiteralNodeClass);
        
        LogColdStart("Creating SelectorNode class");
        SelectorNodeClass = new SClass("SelectorNode", LeafNodeClass, []);
        RegisterClass(SelectorNodeClass);
        
        LogColdStart("Creating MessageNode class");
        MessageNodeClass = new SClass("MessageNode", ParseNodeClass, ["receiver", "selector", "precedence", "special", "arguments", "sizes", "pc"]);
        RegisterClass(MessageNodeClass);
        
        LogColdStart("Creating AssignmentNode class");
        AssignmentNodeClass = new SClass("AssignmentNode", ParseNodeClass, ["variable", "value"]);
        RegisterClass(AssignmentNodeClass);
        
        LogColdStart("Creating ReturnNode class");
        ReturnNodeClass = new SClass("ReturnNode", ParseNodeClass, ["expr", "pc"]);
        RegisterClass(ReturnNodeClass);
        
        LogColdStart("Creating BlockNode class");
        BlockNodeClass = new SClass("BlockNode", ParseNodeClass, ["arguments", "statements", "returns", "nArgsNode", "size", "remoteCopyNode", "sourceRange", "endPC"]);
        RegisterClass(BlockNodeClass);
        
        LogColdStart("Creating CascadeNode class");
        CascadeNodeClass = new SClass("CascadeNode", ParseNodeClass, ["receiver", "messages"]);
        RegisterClass(CascadeNodeClass);
        
        LogColdStart("Creating MethodNode class");
        MethodNodeClass = new SClass("MethodNode", ParseNodeClass, ["selectorOrFalse", "precedence", "arguments", "block", "literals", "primitive", "encoder", "temporaries"]);
        RegisterClass(MethodNodeClass);
        
        // Register global constants
        globals["nil"] = SNil.Instance;
        globals["true"] = STrue.Instance;
        globals["false"] = SFalse.Instance;
        
        LogColdStart($"Created {allClasses.Count} core classes successfully");
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
    /// Registers a class in the system dictionary.
    /// </summary>
    internal static void RegisterClass(SClass sClass)
    {
        allClasses[sClass.Name] = sClass;
        globals[sClass.Name] = sClass;
    }
    
    /// <summary>
    /// Looks up a class by name.
    /// </summary>
    public static SClass? LookupClass(string name)
    {
        return allClasses.TryGetValue(name, out var sClass) ? sClass : null;
    }
    
    /// <summary>
    /// Looks up a global variable by name.
    /// </summary>
    public static SObject? LookupGlobal(string name)
    {
        return globals.TryGetValue(name, out var value) ? value : null;
    }
    
    /// <summary>
    /// Registers a global variable.
    /// </summary>
    public static void RegisterGlobal(string name, SObject value)
    {
        globals[name] = value;
        
        // If it's a class, also add to allClasses
        if (value is SClass sClass && sClass.Name == name)
        {
            allClasses[name] = sClass;
        }
    }
    
    /// <summary>
    /// Gets all class names.
    /// </summary>
    public static IEnumerable<string> GetAllClassNames() => allClasses.Keys;
    
    /// <summary>
    /// Gets all classes in the system.
    /// </summary>
    public static IEnumerable<SClass> GetAllClasses() => allClasses.Values;
    
    /// <summary>
    /// Gets all global variables.
    /// </summary>
    public static IEnumerable<SObject> GetAllGlobals() => globals.Values;
    
    /// <summary>
    /// Resets the system dictionary (for testing).
    /// </summary>
    internal static void Reset()
    {
        initialized = false;
        allClasses.Clear();
        globals.Clear();
        SSymbol.ClearSymbolTable();
    }
}