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
    public static SObjectRef TrueObject { get; internal set; }

    /// <summary>
    /// Gets the singleton False object.
    /// </summary>
    public static SObjectRef FalseObject { get; internal set; }

    /// <summary>
    /// Gets the singleton nil object.
    /// </summary>
    public static SObjectRef NilObject { get; internal set; }

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

        LogColdStart("Starting Smalltalk-80 system initialization...");
        // TODO: Old Generated.ColdStartData removed - use JSON cold start instead
        // LogColdStart($"Creating complete class hierarchy ({Generated.ColdStartData.ClassCount} classes)");

        // Execute the new 4-pass + 2-phase bootstrap process
        // This handles all class creation, singleton creation, method installation, and symbol migration
        // TODO: Old ColdStartBootstrap removed - use JsonColdStartBootstrap instead
        // ColdStart.ColdStartBootstrap.Initialize();

        // The bootstrap process has already:
        // - Created all 128 class objects (64 classes + 64 metaclasses)
        // - Created SystemDictionary instance and registered all classes
        // - Created singleton objects (nil, true, false, ProcessorScheduler, ActiveProcess)
        // - Installed all methods from compiler data
        // - Created USTable and migrated all symbols

        LogColdStart("System initialization complete!");
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
    /// Creates the SystemDictionary instance from the Dictionary class.
    /// Called by ColdStartClassCreator when Dictionary class is created.
    /// </summary>
    internal static void CreateInstance(SObjectRef dictionaryClassRef, int initialCapacity)
    {
        Instance = dictionaryClassRef.CreateInstance(initialCapacity);
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
            // ALWAYS use bootstrap during registration to avoid infinite loops
            // After USTable migration, we rebuild the Instance with USTable symbols
            var nameSymbol = ObjectMemory.InternSymbolBootstrap(className);
            ObjectMemory.DictionaryAtPut(Instance, nameSymbol, classRef);
        }
    }

    /// <summary>
    /// Looks up a class by name and returns its SObjectRef from SystemDictionary.Instance.
    /// </summary>
    public static SObjectRef? LookupClassRef(string name)
    {
        if (Instance.IsNil)
            return null;

        // After USTable migration, SystemDictionary uses USTable symbols
        // Before migration, it uses bootstrap symbols
        // RegisterClassRef handles this automatically
        var nameSymbol = ObjectMemory.InternSymbol(name);
        return ObjectMemory.DictionaryAt(Instance, nameSymbol);
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
            var nameSymbol = ObjectMemory.InternSymbol(name);
            ObjectMemory.DictionaryAtPut(Instance, nameSymbol, valueRef);
        }
    }

    /// <summary>
    /// Looks up a global variable by name and returns it as an SObjectRef.
    /// </summary>
    public static SObjectRef? LookupGlobalRef(string name)
    {
        if (Instance.IsNil)
            return null;

        var nameSymbol = ObjectMemory.InternSymbol(name);
        return ObjectMemory.DictionaryAt(Instance, nameSymbol);
    }

    /// <summary>
    /// Removes a global variable from the SystemDictionary.
    /// In Smalltalk-80, this is: Smalltalk removeKey: #ClassName
    /// </summary>
    public static void UnregisterGlobalRef(string name)
    {
        if (!Instance.IsNil)
        {
            var nameSymbol = ObjectMemory.InternSymbol(name);
            ObjectMemory.DictionaryRemoveKey(Instance, nameSymbol);
        }
    }

    /// <summary>
    /// Gets all class names from SystemDictionary.Instance.
    /// </summary>
    public static IEnumerable<string> GetAllClassNames()
    {
        if (Instance.IsNil)
            return Enumerable.Empty<string>();

        var classNames = new List<string>();
        var dictOop = Instance.AsObjectPointer();

        // SystemDictionary stores associations as indexed elements
        var size = Memory.ObjectMemory.GetIndexedSize(dictOop);

        for (int i = 0; i < size; i++)
        {
            var assocRef = Memory.ObjectMemory.GetIndexedPointer(dictOop, i);
            if (!assocRef.IsNil)
            {
                // Get the key (Symbol) from the Association
                // TODO: Old Generated.Layouts.STAssociation removed - use LayoutManagement instead
                var assocOop = assocRef.AsObjectPointer();
                var keyRef = Memory.ObjectMemory.GetInstanceVariable(assocOop, 0); // Key is first field

                if (!keyRef.IsNil)
                {
                    var keyStr = Memory.ObjectMemory.SymbolToString(keyRef);

                    // Get the value to check if it's a class
                    var valueRef = Memory.ObjectMemory.GetInstanceVariable(assocOop, 1); // Value is second field
                    if (!valueRef.IsNil && valueRef.IsHeapObject)
                    {
                        // Check if value is a class by seeing if it has a 'superclass' inst var (index 0 of Behavior)
                        var valueOop = valueRef.AsObjectPointer();
                        var instVarCount = Memory.ObjectMemory.GetInstanceVariableCount(valueOop);

                        // Classes (instances of Class/Metaclass) have at least 9 inst vars (Behavior:4 + ClassDescription:2 + Class:3)
                        if (instVarCount >= 9)
                        {
                            classNames.Add(keyStr);
                        }
                    }
                }
            }
        }

        return classNames;
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