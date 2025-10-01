// This is a temporary partial class to add SObjectRef properties to SystemDictionary
// Once confirmed working, this will be merged into SystemDictionary.cs

namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

public static partial class SystemDictionary
{
    // Dictionary to track all class refs by name for fast lookup
    private static readonly Dictionary<string, SObjectRef> classRefRegistry = new();

    // SObjectRef versions of all class properties for the new architecture

    /// <summary>
    /// Gets the Object class reference.
    /// </summary>
    public static SObjectRef ObjectClassRef { get; private set; }

    /// <summary>
    /// Gets the Class class reference.
    /// </summary>
    public static SObjectRef ClassClassRef { get; private set; }

    /// <summary>
    /// Gets the Metaclass class reference.
    /// </summary>
    public static SObjectRef MetaclassClassRef { get; private set; }

    /// <summary>
    /// Gets the SmallInteger class reference.
    /// </summary>
    public static SObjectRef SmallIntegerClassRef { get; private set; }

    /// <summary>
    /// Gets the Character class reference.
    /// </summary>
    public static SObjectRef CharacterClassRef { get; private set; }

    /// <summary>
    /// Gets the String class reference.
    /// </summary>
    public static SObjectRef StringClassRef { get; private set; }

    /// <summary>
    /// Gets the Symbol class reference.
    /// </summary>
    public static SObjectRef SymbolClassRef { get; private set; }

    /// <summary>
    /// Gets the Array class reference.
    /// </summary>
    public static SObjectRef ArrayClassRef { get; private set; }

    /// <summary>
    /// Gets the CompiledMethod class reference.
    /// </summary>
    public static SObjectRef CompiledMethodClassRef { get; private set; }

    /// <summary>
    /// Gets the UndefinedObject class reference.
    /// </summary>
    public static SObjectRef UndefinedObjectClassRef { get; private set; }

    /// <summary>
    /// Gets the True class reference.
    /// </summary>
    public static SObjectRef TrueClassRef { get; private set; }

    /// <summary>
    /// Gets the False class reference.
    /// </summary>
    public static SObjectRef FalseClassRef { get; private set; }

    /// <summary>
    /// Gets the Boolean class reference.
    /// </summary>
    public static SObjectRef BooleanClassRef { get; private set; }

    /// <summary>
    /// Gets the Behavior class reference.
    /// </summary>
    public static SObjectRef BehaviorClassRef { get; private set; }

    /// <summary>
    /// Gets the ClassDescription class reference.
    /// </summary>
    public static SObjectRef ClassDescriptionClassRef { get; private set; }

    /// <summary>
    /// Gets the MethodDictionary class reference.
    /// </summary>
    public static SObjectRef MethodDictionaryClassRef { get; private set; }

    /// <summary>
    /// Gets the ByteArray class reference.
    /// </summary>
    public static SObjectRef ByteArrayClassRef { get; private set; }

    /// <summary>
    /// Gets the Association class reference.
    /// </summary>
    public static SObjectRef AssociationClassRef { get; private set; }

    /// <summary>
    /// Gets the Dictionary class reference.
    /// </summary>
    public static SObjectRef DictionaryClassRef { get; private set; }
    public static SObjectRef IdentityDictionaryClassRef { get; private set; }

    /// <summary>
    /// Gets the Magnitude class reference.
    /// </summary>
    public static SObjectRef MagnitudeClassRef { get; private set; }
    public static SObjectRef LookupKeyClassRef { get; private set; }

    /// <summary>
    /// Gets the Number class reference.
    /// </summary>
    public static SObjectRef NumberClassRef { get; private set; }

    /// <summary>
    /// Gets the Integer class reference.
    /// </summary>
    public static SObjectRef IntegerClassRef { get; private set; }

    /// <summary>
    /// Gets the Float class reference.
    /// </summary>
    public static SObjectRef FloatClassRef { get; private set; }

    /// <summary>
    /// Gets the LargePositiveInteger class reference.
    /// </summary>
    public static SObjectRef LargePositiveIntegerClassRef { get; private set; }

    /// <summary>
    /// Gets the LargeNegativeInteger class reference.
    /// </summary>
    public static SObjectRef LargeNegativeIntegerClassRef { get; private set; }

    /// <summary>
    /// Gets the Collection class reference.
    /// </summary>
    public static SObjectRef CollectionClassRef { get; private set; }

    /// <summary>
    /// Gets the SequenceableCollection class reference.
    /// </summary>
    public static SObjectRef SequenceableCollectionClassRef { get; private set; }

    /// <summary>
    /// Gets the OrderedCollection class reference.
    /// </summary>
    public static SObjectRef OrderedCollectionClassRef { get; private set; }

    /// <summary>
    /// Gets the Set class reference.
    /// </summary>
    public static SObjectRef SetClassRef { get; private set; }

    /// <summary>
    /// Gets the Link class reference.
    /// </summary>
    public static SObjectRef LinkClassRef { get; private set; }

    /// <summary>
    /// Gets the Process class reference.
    /// </summary>
    public static SObjectRef ProcessClassRef { get; private set; }

    /// <summary>
    /// Gets the LinkedList class reference.
    /// </summary>
    public static SObjectRef LinkedListClassRef { get; private set; }

    /// <summary>
    /// Gets the Semaphore class reference.
    /// </summary>
    public static SObjectRef SemaphoreClassRef { get; private set; }

    /// <summary>
    /// Gets the ProcessorScheduler class reference.
    /// </summary>
    public static SObjectRef ProcessorSchedulerClassRef { get; private set; }

    /// <summary>
    /// Gets the InstructionStream class reference.
    /// </summary>
    public static SObjectRef InstructionStreamClassRef { get; private set; }

    /// <summary>
    /// Gets the ContextPart class reference.
    /// </summary>
    public static SObjectRef ContextPartClassRef { get; private set; }

    /// <summary>
    /// Gets the MethodContext class reference.
    /// </summary>
    public static SObjectRef MethodContextClassRef { get; private set; }

    /// <summary>
    /// Gets the BlockContext class reference.
    /// </summary>
    public static SObjectRef BlockContextClassRef { get; private set; }

    /// <summary>
    /// Gets the Stream class reference.
    /// </summary>
    public static SObjectRef StreamClassRef { get; private set; }

    /// <summary>
    /// Gets the PositionableStream class reference.
    /// </summary>
    public static SObjectRef PositionableStreamClassRef { get; private set; }

    /// <summary>
    /// Gets the WriteStream class reference.
    /// </summary>
    public static SObjectRef WriteStreamClassRef { get; private set; }

    /// <summary>
    /// Gets the ReadStream class reference.
    /// </summary>
    public static SObjectRef ReadStreamClassRef { get; private set; }

    /// <summary>
    /// Gets the StringHolder class reference.
    /// </summary>
    public static SObjectRef StringHolderClassRef { get; private set; }

    /// <summary>
    /// Gets the TextCollector class reference.
    /// </summary>
    public static SObjectRef TextCollectorClassRef { get; private set; }

    /// <summary>
    /// Gets the Text class reference.
    /// </summary>
    public static SObjectRef TextClassRef { get; private set; }

    /// <summary>
    /// Gets the ParseNode class reference.
    /// </summary>
    public static SObjectRef ParseNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the LeafNode class reference.
    /// </summary>
    public static SObjectRef LeafNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the Scanner class reference.
    /// </summary>
    public static SObjectRef ScannerClassRef { get; private set; }

    /// <summary>
    /// Gets the Parser class reference.
    /// </summary>
    public static SObjectRef ParserClassRef { get; private set; }

    /// <summary>
    /// Gets the Compiler class reference.
    /// </summary>
    public static SObjectRef CompilerClassRef { get; private set; }

    /// <summary>
    /// Gets the Encoder class reference.
    /// </summary>
    public static SObjectRef EncoderClassRef { get; private set; }

    /// <summary>
    /// Gets the VariableNode class reference.
    /// </summary>
    public static SObjectRef VariableNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the LiteralNode class reference.
    /// </summary>
    public static SObjectRef LiteralNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the SelectorNode class reference.
    /// </summary>
    public static SObjectRef SelectorNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the MessageNode class reference.
    /// </summary>
    public static SObjectRef MessageNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the AssignmentNode class reference.
    /// </summary>
    public static SObjectRef AssignmentNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the ReturnNode class reference.
    /// </summary>
    public static SObjectRef ReturnNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the BlockNode class reference.
    /// </summary>
    public static SObjectRef BlockNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the CascadeNode class reference.
    /// </summary>
    public static SObjectRef CascadeNodeClassRef { get; private set; }

    /// <summary>
    /// Gets the MethodNode class reference.
    /// </summary>
    public static SObjectRef MethodNodeClassRef { get; private set; }

    /// <summary>
    /// Initializes SObjectRef properties after the SClass objects are created.
    /// This method should be called at the end of the Initialize() method.
    /// </summary>
    internal static void InitializeSObjectRefProperties()
    {
        // All classes have been registered in ObjectMemory, so we can now get their OOPs
        // We use RegisterBootstrapObject to ensure we get the same OOP if already registered
        ObjectClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ObjectClass ?? throw new InvalidOperationException("ObjectClass is null")));
        ClassClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ClassClass ?? throw new InvalidOperationException("ClassClass is null")));
        MetaclassClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MetaclassClass ?? throw new InvalidOperationException("MetaclassClass is null")));
        SmallIntegerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SmallIntegerClass ?? throw new InvalidOperationException("SmallIntegerClass is null")));
        CharacterClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(CharacterClass ?? throw new InvalidOperationException("CharacterClass is null")));
        StringClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(StringClass ?? throw new InvalidOperationException("StringClass is null")));
        SymbolClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SymbolClass ?? throw new InvalidOperationException("SymbolClass is null")));
        ArrayClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ArrayClass ?? throw new InvalidOperationException("ArrayClass is null")));
        CompiledMethodClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(CompiledMethodClass ?? throw new InvalidOperationException("CompiledMethodClass is null")));
        UndefinedObjectClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(UndefinedObjectClass ?? throw new InvalidOperationException("UndefinedObjectClass is null")));
        TrueClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(TrueClass ?? throw new InvalidOperationException("TrueClass is null")));
        FalseClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(FalseClass ?? throw new InvalidOperationException("FalseClass is null")));
        BooleanClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(BooleanClass ?? throw new InvalidOperationException("BooleanClass is null")));
        BehaviorClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(BehaviorClass ?? throw new InvalidOperationException("BehaviorClass is null")));
        ClassDescriptionClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ClassDescriptionClass));
        MethodDictionaryClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MethodDictionaryClass));
        ByteArrayClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ByteArrayClass));
        AssociationClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(AssociationClass));
        DictionaryClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(DictionaryClass));
        MagnitudeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MagnitudeClass));
        NumberClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(NumberClass));
        IntegerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(IntegerClass));
        FloatClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(FloatClass));
        LargePositiveIntegerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LargePositiveIntegerClass));
        LargeNegativeIntegerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LargeNegativeIntegerClass));
        CollectionClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(CollectionClass));
        SequenceableCollectionClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SequenceableCollectionClass));
        OrderedCollectionClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(OrderedCollectionClass));
        SetClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SetClass));
        LinkClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LinkClass));
        ProcessClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ProcessClass));
        LinkedListClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LinkedListClass));
        SemaphoreClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SemaphoreClass));
        ProcessorSchedulerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ProcessorSchedulerClass));
        InstructionStreamClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(InstructionStreamClass));
        ContextPartClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ContextPartClass));
        MethodContextClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MethodContextClass));
        BlockContextClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(BlockContextClass));
        StreamClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(StreamClass));
        PositionableStreamClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(PositionableStreamClass));
        WriteStreamClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(WriteStreamClass));
        ReadStreamClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ReadStreamClass));
        StringHolderClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(StringHolderClass));
        TextCollectorClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(TextCollectorClass));
        TextClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(TextClass));
        ParseNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ParseNodeClass));
        LeafNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LeafNodeClass));
        ScannerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ScannerClass));
        ParserClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ParserClass));
        CompilerClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(CompilerClass));
        EncoderClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(EncoderClass));
        VariableNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(VariableNodeClass));
        LiteralNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(LiteralNodeClass));
        SelectorNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(SelectorNodeClass));
        MessageNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MessageNodeClass));
        AssignmentNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(AssignmentNodeClass));
        ReturnNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(ReturnNodeClass));
        BlockNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(BlockNodeClass));
        CascadeNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(CascadeNodeClass));
        MethodNodeClassRef = SObjectRef.FromObjectPointer(ObjectMemoryNew.RegisterBootstrapObject(MethodNodeClass));

        // Now register all the well-known classes in both registries
        RegisterWellKnownClasses();
    }

    /// <summary>
    /// Registers all well-known classes in the class ref registry and with ObjectMemoryNew.
    /// </summary>
    private static void RegisterWellKnownClasses()
    {
        // Register core classes
        RegisterClassRef("Object", ObjectClassRef);
        RegisterClassRef("Class", ClassClassRef);
        RegisterClassRef("Metaclass", MetaclassClassRef);
        RegisterClassRef("SmallInteger", SmallIntegerClassRef);
        RegisterClassRef("Character", CharacterClassRef);
        RegisterClassRef("String", StringClassRef);
        RegisterClassRef("Symbol", SymbolClassRef);
        RegisterClassRef("Array", ArrayClassRef);
        RegisterClassRef("CompiledMethod", CompiledMethodClassRef);
        RegisterClassRef("UndefinedObject", UndefinedObjectClassRef);
        RegisterClassRef("True", TrueClassRef);
        RegisterClassRef("False", FalseClassRef);
        RegisterClassRef("Boolean", BooleanClassRef);

        // Register behavior hierarchy
        RegisterClassRef("Behavior", BehaviorClassRef);
        RegisterClassRef("ClassDescription", ClassDescriptionClassRef);
        RegisterClassRef("MethodDictionary", MethodDictionaryClassRef);
        RegisterClassRef("ByteArray", ByteArrayClassRef);
        RegisterClassRef("Association", AssociationClassRef);
        RegisterClassRef("Dictionary", DictionaryClassRef);

        // Register numeric hierarchy
        RegisterClassRef("Magnitude", MagnitudeClassRef);
        RegisterClassRef("Number", NumberClassRef);
        RegisterClassRef("Integer", IntegerClassRef);
        RegisterClassRef("Float", FloatClassRef);
        RegisterClassRef("LargePositiveInteger", LargePositiveIntegerClassRef);
        RegisterClassRef("LargeNegativeInteger", LargeNegativeIntegerClassRef);

        // Register collection hierarchy
        RegisterClassRef("Collection", CollectionClassRef);
        RegisterClassRef("SequenceableCollection", SequenceableCollectionClassRef);
        RegisterClassRef("OrderedCollection", OrderedCollectionClassRef);
        RegisterClassRef("Set", SetClassRef);

        // Register process-related classes
        RegisterClassRef("Link", LinkClassRef);
        RegisterClassRef("Process", ProcessClassRef);
        RegisterClassRef("LinkedList", LinkedListClassRef);
        RegisterClassRef("Semaphore", SemaphoreClassRef);
        RegisterClassRef("ProcessorScheduler", ProcessorSchedulerClassRef);
        RegisterClassRef("InstructionStream", InstructionStreamClassRef);
        RegisterClassRef("ContextPart", ContextPartClassRef);
        RegisterClassRef("MethodContext", MethodContextClassRef);
        RegisterClassRef("BlockContext", BlockContextClassRef);

        // Register stream classes
        RegisterClassRef("Stream", StreamClassRef);
        RegisterClassRef("PositionableStream", PositionableStreamClassRef);
        RegisterClassRef("WriteStream", WriteStreamClassRef);
        RegisterClassRef("ReadStream", ReadStreamClassRef);
        RegisterClassRef("StringHolder", StringHolderClassRef);
        RegisterClassRef("TextCollector", TextCollectorClassRef);
        RegisterClassRef("Text", TextClassRef);

        // Register compilation support classes
        RegisterClassRef("ParseNode", ParseNodeClassRef);
        RegisterClassRef("LeafNode", LeafNodeClassRef);
        RegisterClassRef("Scanner", ScannerClassRef);
        RegisterClassRef("Parser", ParserClassRef);
        RegisterClassRef("Compiler", CompilerClassRef);
        RegisterClassRef("Encoder", EncoderClassRef);
        RegisterClassRef("VariableNode", VariableNodeClassRef);
        RegisterClassRef("LiteralNode", LiteralNodeClassRef);
        RegisterClassRef("SelectorNode", SelectorNodeClassRef);
        RegisterClassRef("MessageNode", MessageNodeClassRef);
        RegisterClassRef("AssignmentNode", AssignmentNodeClassRef);
        RegisterClassRef("ReturnNode", ReturnNodeClassRef);
        RegisterClassRef("BlockNode", BlockNodeClassRef);
        RegisterClassRef("CascadeNode", CascadeNodeClassRef);
        RegisterClassRef("MethodNode", MethodNodeClassRef);
    }

    /// <summary>
    /// Registers a class reference in the registry and sets it as a well-known class in ObjectMemoryNew.
    /// </summary>
    /// <param name="name">The name of the class.</param>
    /// <param name="classRef">The SObjectRef for the class.</param>
    internal static void RegisterClassRef(string name, SObjectRef classRef)
    {
        // Store in our local registry
        classRefRegistry[name] = classRef;

        // Register as a well-known class in ObjectMemoryNew
        ObjectMemoryNew.SetWellKnownClass(name, classRef);
    }

    /// <summary>
    /// Looks up an SObjectRef for a class by its name.
    /// </summary>
    /// <param name="name">The name of the class to look up.</param>
    /// <returns>The SObjectRef for the class if found; otherwise, null.</returns>
    public static SObjectRef? LookupClassRef(string name)
    {
        return classRefRegistry.TryGetValue(name, out var classRef) ? classRef : null;
    }
}