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

    // PHASE 1: Keep old SClass properties but make them throw NotImplementedException
    // These will be removed in Phase 4 of the migration

    /// <summary>
    /// Gets the Object class. (DEPRECATED - use ObjectClassRef)
    /// </summary>
    public static SClass ObjectClass => throw new NotImplementedException("Use ObjectClassRef instead");

    /// <summary>
    /// Gets the Class class. (DEPRECATED - use ClassClassRef)
    /// </summary>
    public static SClass ClassClass => throw new NotImplementedException("Use ClassClassRef instead");

    /// <summary>
    /// Gets the Metaclass class. (DEPRECATED - use MetaclassClassRef)
    /// </summary>
    public static SClass MetaclassClass { get => throw new NotImplementedException("Use MetaclassClassRef instead"); internal set => throw new NotImplementedException(); }

    /// <summary>
    /// Gets the SmallInteger class. (DEPRECATED - use SmallIntegerClassRef)
    /// </summary>
    public static SClass SmallIntegerClass => throw new NotImplementedException("Use SmallIntegerClassRef instead");

    /// <summary>
    /// Gets the Character class. (DEPRECATED - use CharacterClassRef)
    /// </summary>
    public static SClass CharacterClass => throw new NotImplementedException("Use CharacterClassRef instead");

    /// <summary>
    /// Gets the String class. (DEPRECATED - use StringClassRef)
    /// </summary>
    public static SClass StringClass => throw new NotImplementedException("Use StringClassRef instead");

    /// <summary>
    /// Gets the Symbol class. (DEPRECATED - use SymbolClassRef)
    /// </summary>
    public static SClass SymbolClass => throw new NotImplementedException("Use SymbolClassRef instead");

    /// <summary>
    /// Gets the Array class. (DEPRECATED - use ArrayClassRef)
    /// </summary>
    public static SClass ArrayClass => throw new NotImplementedException("Use ArrayClassRef instead");

    /// <summary>
    /// Gets the CompiledMethod class. (DEPRECATED - use CompiledMethodClassRef)
    /// </summary>
    public static SClass CompiledMethodClass => throw new NotImplementedException("Use CompiledMethodClassRef instead");

    /// <summary>
    /// Gets the UndefinedObject class. (DEPRECATED - use UndefinedObjectClassRef)
    /// </summary>
    public static SClass UndefinedObjectClass => throw new NotImplementedException("Use UndefinedObjectClassRef instead");

    /// <summary>
    /// Gets the True class. (DEPRECATED - use TrueClassRef)
    /// </summary>
    public static SClass TrueClass => throw new NotImplementedException("Use TrueClassRef instead");

    /// <summary>
    /// Gets the False class. (DEPRECATED - use FalseClassRef)
    /// </summary>
    public static SClass FalseClass => throw new NotImplementedException("Use FalseClassRef instead");

    /// <summary>
    /// Gets the Boolean class. (DEPRECATED - use BooleanClassRef)
    /// </summary>
    public static SClass BooleanClass => throw new NotImplementedException("Use BooleanClassRef instead");

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

    /// <summary>
    /// Gets the symbol table containing all interned symbols.
    /// </summary>
    public static SDictionary SymbolTable { get; private set; } = null!;

    // Additional core classes from Blue Book - all deprecated properties

    public static SClass BehaviorClass => throw new NotImplementedException("Use BehaviorClassRef instead");
    public static SClass ClassDescriptionClass => throw new NotImplementedException("Use ClassDescriptionClassRef instead");
    public static SClass MethodDictionaryClass => throw new NotImplementedException("Use MethodDictionaryClassRef instead");
    public static SClass ByteArrayClass => throw new NotImplementedException("Use ByteArrayClassRef instead");
    public static SClass AssociationClass => throw new NotImplementedException("Use AssociationClassRef instead");
    public static SClass DictionaryClass => throw new NotImplementedException("Use DictionaryClassRef instead");
    public static SClass MagnitudeClass => throw new NotImplementedException("Use MagnitudeClassRef instead");
    public static SClass NumberClass => throw new NotImplementedException("Use NumberClassRef instead");
    public static SClass IntegerClass => throw new NotImplementedException("Use IntegerClassRef instead");
    public static SClass FloatClass => throw new NotImplementedException("Use FloatClassRef instead");
    public static SClass LargePositiveIntegerClass => throw new NotImplementedException("Use LargePositiveIntegerClassRef instead");
    public static SClass LargeNegativeIntegerClass => throw new NotImplementedException("Use LargeNegativeIntegerClassRef instead");
    public static SClass CollectionClass => throw new NotImplementedException("Use CollectionClassRef instead");
    public static SClass SequenceableCollectionClass => throw new NotImplementedException("Use SequenceableCollectionClassRef instead");
    public static SClass OrderedCollectionClass => throw new NotImplementedException("Use OrderedCollectionClassRef instead");
    public static SClass SetClass => throw new NotImplementedException("Use SetClassRef instead");
    public static SClass LinkClass => throw new NotImplementedException("Use LinkClassRef instead");
    public static SClass ProcessClass => throw new NotImplementedException("Use ProcessClassRef instead");
    public static SClass LinkedListClass => throw new NotImplementedException("Use LinkedListClassRef instead");
    public static SClass SemaphoreClass => throw new NotImplementedException("Use SemaphoreClassRef instead");
    public static SClass ProcessorSchedulerClass => throw new NotImplementedException("Use ProcessorSchedulerClassRef instead");
    public static SClass InstructionStreamClass => throw new NotImplementedException("Use InstructionStreamClassRef instead");
    public static SClass ContextPartClass => throw new NotImplementedException("Use ContextPartClassRef instead");
    public static SClass StreamClass => throw new NotImplementedException("Use StreamClassRef instead");
    public static SClass PositionableStreamClass => throw new NotImplementedException("Use PositionableStreamClassRef instead");
    public static SClass WriteStreamClass => throw new NotImplementedException("Use WriteStreamClassRef instead");
    public static SClass ReadStreamClass => throw new NotImplementedException("Use ReadStreamClassRef instead");
    public static SClass StringHolderClass => throw new NotImplementedException("Use StringHolderClassRef instead");
    public static SClass TextCollectorClass => throw new NotImplementedException("Use TextCollectorClassRef instead");
    public static SClass TextClass => throw new NotImplementedException("Use TextClassRef instead");
    public static SClass ParseNodeClass => throw new NotImplementedException("Use ParseNodeClassRef instead");
    public static SClass LeafNodeClass => throw new NotImplementedException("Use LeafNodeClassRef instead");
    public static SClass ScannerClass => throw new NotImplementedException("Use ScannerClassRef instead");
    public static SClass ParserClass => throw new NotImplementedException("Use ParserClassRef instead");
    public static SClass CompilerClass => throw new NotImplementedException("Use CompilerClassRef instead");
    public static SClass EncoderClass => throw new NotImplementedException("Use EncoderClassRef instead");
    public static SClass VariableNodeClass => throw new NotImplementedException("Use VariableNodeClassRef instead");
    public static SClass LiteralNodeClass => throw new NotImplementedException("Use LiteralNodeClassRef instead");
    public static SClass SelectorNodeClass => throw new NotImplementedException("Use SelectorNodeClassRef instead");
    public static SClass MessageNodeClass => throw new NotImplementedException("Use MessageNodeClassRef instead");
    public static SClass AssignmentNodeClass => throw new NotImplementedException("Use AssignmentNodeClassRef instead");
    public static SClass ReturnNodeClass => throw new NotImplementedException("Use ReturnNodeClassRef instead");
    public static SClass BlockNodeClass => throw new NotImplementedException("Use BlockNodeClassRef instead");
    public static SClass CascadeNodeClass => throw new NotImplementedException("Use CascadeNodeClassRef instead");
    public static SClass MethodNodeClass => throw new NotImplementedException("Use MethodNodeClassRef instead");
    public static SClass MethodContextClass => throw new NotImplementedException("Use MethodContextClassRef instead");
    public static SClass BlockContextClass => throw new NotImplementedException("Use BlockContextClassRef instead");

    // Legacy globals dictionary - still needed for now but will be migrated
    private static readonly Dictionary<string, SObject> globals = [];

    // New SObjectRef globals dictionary for pure SObjectRef architecture
    private static readonly Dictionary<string, SObjectRef> globalRefs = [];

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
        ContextPartClassRef = ObjectMemoryNew.CreateClassObject("ContextPart", InstructionStreamClassRef, ["stackp", "stack", "method", "tempframe"], ObjectFormat.FixedPointer);
        RegisterClassRef("ContextPart", ContextPartClassRef);

        LogColdStart("Creating MethodContext class");
        MethodContextClassRef = ObjectMemoryNew.CreateClassObject("MethodContext", ContextPartClassRef, ["receiver"], ObjectFormat.FixedPointer);
        RegisterClassRef("MethodContext", MethodContextClassRef);

        LogColdStart("Creating BlockContext class");
        BlockContextClassRef = ObjectMemoryNew.CreateClassObject("BlockContext", ContextPartClassRef, ["nargs", "startpc", "home"], ObjectFormat.FixedPointer);
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
        RegisterClassRef("Dictionary", DictionaryClassRef);

        LogColdStart("Creating IdentityDictionary class");
        IdentityDictionaryClassRef = ObjectMemoryNew.CreateClassObject("IdentityDictionary", DictionaryClassRef, ["valueArray"], ObjectFormat.PointerIndexed);
        RegisterClassRef("IdentityDictionary", IdentityDictionaryClassRef);

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

        // Create the symbol table as a proper Dictionary
        var symbolTableRef = DictionaryClassRef.CreateInstance(100);
        // TODO: ObjectMemoryNew.SetWellKnownObject("SymbolTable", symbolTableRef);

        // TODO: Reimplement SymbolTable using pure SObjectRef
        // For now, skip SymbolTable creation as SDictionary uses old architecture
        // SymbolTable = SDictionary.CreateSymbolTable();
        // globals["SymbolTable"] = SymbolTable;

        // Create SystemDictionary instance as a Dictionary
        Instance = DictionaryClassRef.CreateInstance(100);
        // TODO: ObjectMemoryNew.SetWellKnownObject("Smalltalk", Instance);

        LogColdStart($"Created {classRefRegistry.Count} core classes successfully using pure SObjectRef architecture");

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
    /// Registers a class in the system dictionary (legacy - for compatibility).
    /// </summary>
    internal static void RegisterClass(SClass sClass)
    {
        // No longer used - we only use RegisterClassRef now
        throw new NotImplementedException("Use RegisterClassRef instead");
    }

    /// <summary>
    /// Looks up a class by name (legacy - for compatibility).
    /// </summary>
    public static SClass? LookupClass(string name)
    {
        // This should eventually use classRefRegistry, but for now throw
        throw new NotImplementedException("Use LookupClassRef instead");
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
    }

    /// <summary>
    /// Registers a global object as an SObjectRef (for migration to pure SObjectRef).
    /// </summary>
    public static void RegisterGlobalRef(string name, SObjectRef valueRef)
    {
        globalRefs[name] = valueRef;
    }

    /// <summary>
    /// Looks up a global variable by name and returns it as an SObjectRef.
    /// </summary>
    public static SObjectRef? LookupGlobalRef(string name)
    {
        return globalRefs.TryGetValue(name, out var valueRef) ? valueRef : null;
    }

    /// <summary>
    /// Gets all class names.
    /// </summary>
    public static IEnumerable<string> GetAllClassNames() => classRefRegistry.Keys;

    /// <summary>
    /// Gets all classes in the system (legacy - returns empty).
    /// </summary>
    public static IEnumerable<SClass> GetAllClasses() => [];

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
        classRefRegistry.Clear();
        globals.Clear();
        SSymbol.ClearSymbolTable();
    }
}