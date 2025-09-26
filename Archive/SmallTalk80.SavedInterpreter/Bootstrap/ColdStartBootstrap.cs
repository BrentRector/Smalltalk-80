using System.Text;
using Core;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;
using Smalltalk80.Interpreter.Config;

namespace Smalltalk80.Interpreter.Bootstrap;

/// <summary>
/// Minimal cold start bootstrap that hardcodes only the essential class hierarchy
/// and critical methods, then reads Smalltalk source to build the rest.
/// </summary>
public sealed class ColdStartBootstrap
{
    private readonly IObjectMemory _memory;
    private readonly ILogger<ColdStartBootstrap> _logger;
    private readonly RuntimeOptions _runtimeOptions;
    private readonly Dictionary<string, OOP> _classes = new();
    private readonly Dictionary<string, OOP> _symbols = new();
    private readonly Dictionary<string, OOP> _globals = new(); // Global variables
    private readonly List<Chunk> _pendingChunks = new();
    
    // Core class OOPs - will be allocated during bootstrap
    private OOP _objectClass;
    private OOP _behaviorClass;
    private OOP _classDescriptionClass;
    private OOP _metaclassClass;
    private OOP _classClass;
    private OOP _compiledMethodClass;
    private OOP _stringClass;
    private OOP _symbolClass;
    private OOP _arrayClass;
    private OOP _byteArrayClass;
    private OOP _associationClass;
    private OOP _dictionaryClass;
    private OOP _methodDictionaryClass;
    private OOP _methodContextClass;
    private OOP _blockContextClass;
    
    // Numeric classes hierarchy
    private OOP _magnitudeClass;
    private OOP _numberClass;
    private OOP _integerClass;
    private OOP _smallIntegerClass;
    private OOP _floatClass;
    private OOP _largePositiveIntegerClass;
    private OOP _largeNegativeIntegerClass;
    
    private OOP _characterClass;
    private OOP _trueClass;
    private OOP _falseClass;
    private OOP _undefinedObjectClass;
    
    // Process-related classes
    private OOP _linkClass;
    private OOP _processClass;
    private OOP _linkedListClass;
    private OOP _semaphoreClass;
    private OOP _processorSchedulerClass;
    private OOP _instructionStreamClass;
    private OOP _contextPartClass;
    
    // I/O and Transcript classes
    private OOP _streamClass;
    private OOP _positionableStreamClass;
    private OOP _writeStreamClass;
    private OOP _readStreamClass;
    private OOP _stringHolderClass;
    private OOP _textCollectorClass;
    private OOP _textClass;
    
    // Singleton objects
    private OOP _nilObject;
    private OOP _trueObject;
    private OOP _falseObject;
    private OOP _specialObjectsArray;
    private OOP _processorScheduler;  // The Processor singleton

    /// <summary>
    /// Bytecode for Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
    /// Compiled from actual Smalltalk-80.st source using Smalltalk80.Compiler
    /// </summary>
    private static readonly byte[] SubclassCreationBytecode = 
    [
        112, 208, 137, 117, 200, 160, 47, 112, 210, 137, 117, 200, 160, 9,
        112, 16, 17, 18, 19, 20, 131, 163, 125, 125, 225, 135,
        112, 212, 137, 117, 200, 160, 9,
        112, 16, 17, 18, 19, 20, 131, 165, 125, 125, 225, 135,
        112, 16, 17, 18, 19, 20, 131, 166, 125, 125, 225, 135,
        112, 199, 16, 72, 112, 17, 114, 113, 113, 18, 19, 20, 115, 114,
        132, 7, 12, 124
    ];
    
    /// <summary>
    /// Bytecode for Behavior>>compile:classified:
    /// Compiled from actual Smalltalk-80.st source using Smalltalk80.Compiler
    /// </summary>
    private static readonly byte[] CompileMethodBytecode = 
    [
        112, 16, 17, 115, 131, 96, 124
    ];

    public ColdStartBootstrap(IObjectMemory memory, ILogger<ColdStartBootstrap> logger, RuntimeOptions? runtimeOptions = null)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _runtimeOptions = runtimeOptions ?? RuntimeOptions.Default;
    }
    
    /// <summary>
    /// Gets all registered classes after bootstrap.
    /// </summary>
    public IReadOnlyDictionary<string, OOP> Classes => _classes;
    
    /// <summary>
    /// Logs a message with the [COLD START] prefix if the flag is enabled.
    /// </summary>
    private void LogColdStart(LogLevel level, string message, params object[] args)
    {
        if (_runtimeOptions.EnableColdStartLogging)
        {
            var formattedMessage = "[COLD START] " + message;
            _logger.Log(level, formattedMessage, args);
        }
    }

    /// <summary>
    /// Performs the cold start bootstrap.
    /// </summary>
    public async Task BootstrapAsync(string smalltalkSourcePath)
    {
        LogColdStart(LogLevel.Information, "Beginning minimal bootstrap...");
        
        // Phase 1: Create the minimal class hierarchy
        CreateMinimalClassHierarchy();
        
        // Phase 2: Install critical primitive methods
        InstallPrimitiveMethods();
        
        // Phase 2b: Initialize Process infrastructure
        InitializeProcessInfrastructure();
        
        // Phase 2c: Create the Special Objects Array (must be after Process infrastructure)
        CreateSpecialObjectsArray();
        
        // Phase 3: Install hardcoded bootstrap methods
        InstallBootstrapMethods();
        
        // Phase 4: Read and execute Smalltalk source chunks
        await ProcessSmalltalkSourceAsync(smalltalkSourcePath);
        
        // Phase 4b: Initialize TextCollector and create global Transcript
        InitializeTranscript();
        
        // Phase 5: Register Special Objects Array as GC root and test GC
        // RegisterGCRootsAndTestGC(); // Temporarily commented due to strange scoping issue
        
        // Inline the GC test for now
        LogColdStart(LogLevel.Information, "Phase 5: Testing garbage collection...");
        
        // Set the Special Objects Array as the sole GC root
        if (_specialObjectsArray.Value != 0) // Check if it's been set
        {
            _memory.SetSpecialObjectsArray(_specialObjectsArray);
        }
        
        // Run garbage collection (will display detailed statistics)
        if (_runtimeOptions.EnableGCLogging)
        {
            _logger.LogInformation("[GC] Running garbage collection after cold start bootstrap...");
        }
        _memory.CollectGarbage();
        
        // Check results
        var (objectsCollected, bytesFreed) = _memory.GetLastGCStats();
        if (objectsCollected == 0)
        {
            if (_runtimeOptions.EnableGCLogging)
            {
                _logger.LogInformation("[GC] ✓ SUCCESS: All bootstrap objects are properly rooted!");
            }
        }
        else
        {
            if (_runtimeOptions.EnableGCLogging)
            {
                _logger.LogWarning("[GC] ⚠ WARNING: {Count} objects were collected - some bootstrap objects may not be properly rooted!", objectsCollected);
            }
        }
        
        LogColdStart(LogLevel.Information, "Bootstrap complete!");
    }

    /// <summary>
    /// Phase 1: Creates the minimal class hierarchy needed for bootstrap.
    /// Only creates the absolute minimum classes needed to execute class creation and method compilation.
    /// </summary>
    private void CreateMinimalClassHierarchy()
    {
        LogColdStart(LogLevel.Information, "Phase 1: Creating minimal class hierarchy...");
        
        // Create nil, true, false singleton objects first
        // They need temporary class pointers that will be fixed later
        var tempClassOop = OOP.FromSmallInteger(999999); // Temporary marker
        
        // Create the singleton objects with temporary classes
        _nilObject = _memory.AllocateObject(tempClassOop, 0);   // nil object
        _trueObject = _memory.AllocateObject(tempClassOop, 0);  // true object  
        _falseObject = _memory.AllocateObject(tempClassOop, 0); // false object
        
        // Update the static OOP values to match the actual allocated objects
        OOP.Nil = _nilObject;
        OOP.True = _trueObject;
        OOP.False = _falseObject;
        
        // Also tell the memory system which OOP is nil
        _memory.SetNilOop(_nilObject);
        
        LogColdStart(LogLevel.Information, "Created singleton placeholders: nil={0}, true={1}, false={2}", 
            _nilObject, _trueObject, _falseObject);
        
        // Step 1: Now allocate all class objects (they'll start at index 3)
        
        // Core object model classes (5 classes per Blue Book)
        _objectClass = _memory.AllocateObject(tempClassOop, 8); // 8 fields per Blue Book
        _behaviorClass = _memory.AllocateObject(tempClassOop, 8);
        _classDescriptionClass = _memory.AllocateObject(tempClassOop, 8);
        _metaclassClass = _memory.AllocateObject(tempClassOop, 8);
        _classClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Method storage classes
        _compiledMethodClass = _memory.AllocateObject(tempClassOop, 8);
        _methodDictionaryClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Execution classes
        _methodContextClass = _memory.AllocateObject(tempClassOop, 8);
        _blockContextClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Essential data type classes
        _stringClass = _memory.AllocateObject(tempClassOop, 8);
        _symbolClass = _memory.AllocateObject(tempClassOop, 8);
        _arrayClass = _memory.AllocateObject(tempClassOop, 8);
        _byteArrayClass = _memory.AllocateObject(tempClassOop, 8);
        _associationClass = _memory.AllocateObject(tempClassOop, 8);
        _dictionaryClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Numeric classes hierarchy
        _magnitudeClass = _memory.AllocateObject(tempClassOop, 8);
        _numberClass = _memory.AllocateObject(tempClassOop, 8);
        _integerClass = _memory.AllocateObject(tempClassOop, 8);
        _smallIntegerClass = _memory.AllocateObject(tempClassOop, 8);
        _floatClass = _memory.AllocateObject(tempClassOop, 8);
        _largePositiveIntegerClass = _memory.AllocateObject(tempClassOop, 8);
        _largeNegativeIntegerClass = _memory.AllocateObject(tempClassOop, 8);
        
        _characterClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Boolean classes  
        _trueClass = _memory.AllocateObject(tempClassOop, 8);
        _falseClass = _memory.AllocateObject(tempClassOop, 8);
        _undefinedObjectClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Process-related classes
        _linkClass = _memory.AllocateObject(tempClassOop, 8);
        _processClass = _memory.AllocateObject(tempClassOop, 8);
        _linkedListClass = _memory.AllocateObject(tempClassOop, 8);
        _semaphoreClass = _memory.AllocateObject(tempClassOop, 8);
        _processorSchedulerClass = _memory.AllocateObject(tempClassOop, 8);
        _instructionStreamClass = _memory.AllocateObject(tempClassOop, 8);
        _contextPartClass = _memory.AllocateObject(tempClassOop, 8);
        
        // I/O and Transcript classes
        _streamClass = _memory.AllocateObject(tempClassOop, 8);
        _positionableStreamClass = _memory.AllocateObject(tempClassOop, 8);
        _writeStreamClass = _memory.AllocateObject(tempClassOop, 8);
        _readStreamClass = _memory.AllocateObject(tempClassOop, 8);
        _stringHolderClass = _memory.AllocateObject(tempClassOop, 8);
        _textCollectorClass = _memory.AllocateObject(tempClassOop, 8);
        _textClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Step 2: Create metaclasses for each class
        // Metaclass objects have 6 fields per Blue Book
        var objectMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var behaviorMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var classDescriptionMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var metaclassMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var classMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        
        // Step 2b: Set up metaclass superclass chain - CRITICAL!
        // According to Smalltalk-80 Blue Book:
        // - Object class superclass is Class (special case!)
        // - All other metaclass superclasses parallel the class hierarchy
        SetSuperclass(objectMetaclass, _classClass);  // SPECIAL: Object class superclass = Class
        SetSuperclass(behaviorMetaclass, objectMetaclass);  // Behavior class superclass = Object class
        SetSuperclass(classDescriptionMetaclass, behaviorMetaclass);  // ClassDescription class superclass = Behavior class
        SetSuperclass(metaclassMetaclass, classDescriptionMetaclass);  // Metaclass class superclass = ClassDescription class
        SetSuperclass(classMetaclass, classDescriptionMetaclass);  // Class class superclass = ClassDescription class
        
        // Step 3: Wire up the class pointers
        // First fix the singleton objects
        SetClass(_nilObject, _undefinedObjectClass);
        SetClass(_trueObject, _trueClass);
        SetClass(_falseObject, _falseClass);
        
        // Then fix the class objects
        SetClass(_objectClass, objectMetaclass);
        SetClass(_behaviorClass, behaviorMetaclass);
        SetClass(_classDescriptionClass, classDescriptionMetaclass);
        SetClass(_metaclassClass, metaclassMetaclass);
        SetClass(_classClass, classMetaclass);
        
        // Step 4: Set up the inheritance chain
        // Object has no superclass (nil)
        SetSuperclass(_objectClass, _nilObject); // Object has no superclass
        
        // Behavior inherits from Object
        SetSuperclass(_behaviorClass, _objectClass);
        
        // ClassDescription inherits from Behavior
        SetSuperclass(_classDescriptionClass, _behaviorClass);
        
        // Both Metaclass and Class inherit from ClassDescription
        SetSuperclass(_metaclassClass, _classDescriptionClass);
        SetSuperclass(_classClass, _classDescriptionClass);
        
        // Step 5: Set up other essential classes and their metaclasses
        // CompiledMethod and its metaclass
        var compiledMethodMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_compiledMethodClass, compiledMethodMetaclass);
        SetSuperclass(_compiledMethodClass, _objectClass);
        SetSuperclass(compiledMethodMetaclass, objectMetaclass); // CompiledMethod class superclass = Object class
        
        // MethodDictionary and its metaclass (note: Dictionary metaclass will be created later)
        var methodDictionaryMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_methodDictionaryClass, methodDictionaryMetaclass);
        SetSuperclass(_methodDictionaryClass, _dictionaryClass);
        
        // ByteArray and its metaclass (must come before String)
        var byteArrayMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_byteArrayClass, byteArrayMetaclass);
        SetSuperclass(_byteArrayClass, _objectClass);
        SetSuperclass(byteArrayMetaclass, objectMetaclass); // ByteArray class superclass = Object class
        
        // String and its metaclass
        var stringMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_stringClass, stringMetaclass);
        SetSuperclass(_stringClass, _byteArrayClass);
        SetSuperclass(stringMetaclass, byteArrayMetaclass); // String class superclass = ByteArray class
        
        // Symbol and its metaclass
        var symbolMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_symbolClass, symbolMetaclass);
        SetSuperclass(_symbolClass, _stringClass);
        SetSuperclass(symbolMetaclass, stringMetaclass); // Symbol class superclass = String class
        
        // Array and its metaclass
        var arrayMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_arrayClass, arrayMetaclass);
        SetSuperclass(_arrayClass, _objectClass);
        SetSuperclass(arrayMetaclass, objectMetaclass); // Array class superclass = Object class
        
        // Dictionary and its metaclass
        var dictionaryMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_dictionaryClass, dictionaryMetaclass);
        SetSuperclass(_dictionaryClass, _objectClass);
        SetSuperclass(dictionaryMetaclass, objectMetaclass); // Dictionary class superclass = Object class
        
        // Now set MethodDictionary metaclass superclass (it inherits from Dictionary)
        SetSuperclass(methodDictionaryMetaclass, dictionaryMetaclass); // MethodDictionary class superclass = Dictionary class
        
        // Association and its metaclass
        var associationMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_associationClass, associationMetaclass);
        SetSuperclass(_associationClass, _objectClass);
        SetSuperclass(associationMetaclass, objectMetaclass); // Association class superclass = Object class
        
        // Numeric class hierarchy
        // Magnitude and its metaclass
        var magnitudeMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_magnitudeClass, magnitudeMetaclass);
        SetSuperclass(_magnitudeClass, _objectClass);
        SetSuperclass(magnitudeMetaclass, objectMetaclass); // Magnitude class superclass = Object class
        
        // Number and its metaclass
        var numberMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_numberClass, numberMetaclass);
        SetSuperclass(_numberClass, _magnitudeClass);
        SetSuperclass(numberMetaclass, magnitudeMetaclass); // Number class superclass = Magnitude class
        
        // Integer and its metaclass
        var integerMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_integerClass, integerMetaclass);
        SetSuperclass(_integerClass, _numberClass);
        SetSuperclass(integerMetaclass, numberMetaclass); // Integer class superclass = Number class
        
        // SmallInteger and its metaclass
        var smallIntegerMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_smallIntegerClass, smallIntegerMetaclass);
        SetSuperclass(_smallIntegerClass, _integerClass);
        SetSuperclass(smallIntegerMetaclass, integerMetaclass); // SmallInteger class superclass = Integer class
        
        // Float and its metaclass (variableWordSubclass of Number)
        var floatMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_floatClass, floatMetaclass);
        SetSuperclass(_floatClass, _numberClass);
        SetSuperclass(floatMetaclass, numberMetaclass); // Float class superclass = Number class
        // Note: Float is a variableWordSubclass - this will be set in its format field
        
        // LargePositiveInteger and its metaclass (variableByteSubclass of Integer)
        var largePositiveIntegerMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_largePositiveIntegerClass, largePositiveIntegerMetaclass);
        SetSuperclass(_largePositiveIntegerClass, _integerClass);
        SetSuperclass(largePositiveIntegerMetaclass, integerMetaclass); // LargePositiveInteger class superclass = Integer class
        // Note: LargePositiveInteger is a variableByteSubclass - this will be set in its format field
        
        // LargeNegativeInteger and its metaclass (variableByteSubclass of Integer)
        var largeNegativeIntegerMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_largeNegativeIntegerClass, largeNegativeIntegerMetaclass);
        SetSuperclass(_largeNegativeIntegerClass, _integerClass);
        SetSuperclass(largeNegativeIntegerMetaclass, integerMetaclass); // LargeNegativeInteger class superclass = Integer class
        // Note: LargeNegativeInteger is a variableByteSubclass - this will be set in its format field
        
        // Character and its metaclass
        var characterMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_characterClass, characterMetaclass);
        SetSuperclass(_characterClass, _magnitudeClass);
        SetSuperclass(characterMetaclass, magnitudeMetaclass); // Character class superclass = Magnitude class
        
        // Boolean classes and their metaclasses
        // True and its metaclass
        var trueMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_trueClass, trueMetaclass);
        SetSuperclass(_trueClass, _objectClass);
        SetSuperclass(trueMetaclass, objectMetaclass); // True class superclass = Object class
        
        // False and its metaclass
        var falseMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_falseClass, falseMetaclass);
        SetSuperclass(_falseClass, _objectClass);
        SetSuperclass(falseMetaclass, objectMetaclass); // False class superclass = Object class
        
        // UndefinedObject and its metaclass
        var undefinedObjectMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_undefinedObjectClass, undefinedObjectMetaclass);
        SetSuperclass(_undefinedObjectClass, _objectClass);
        SetSuperclass(undefinedObjectMetaclass, objectMetaclass); // UndefinedObject class superclass = Object class
        
        // Process-related class hierarchies
        // Link and its metaclass
        var linkMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_linkClass, linkMetaclass);
        SetSuperclass(_linkClass, _objectClass);
        SetSuperclass(linkMetaclass, objectMetaclass); // Link class superclass = Object class
        
        // Process and its metaclass
        var processMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_processClass, processMetaclass);
        SetSuperclass(_processClass, _linkClass);  // Process inherits from Link
        SetSuperclass(processMetaclass, linkMetaclass); // Process class superclass = Link class
        
        // LinkedList and its metaclass
        var linkedListMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_linkedListClass, linkedListMetaclass);
        SetSuperclass(_linkedListClass, _objectClass);  // LinkedList inherits from SequenceableCollection (but we'll use Object for now)
        SetSuperclass(linkedListMetaclass, objectMetaclass); // LinkedList class superclass = Object class
        
        // Semaphore and its metaclass
        var semaphoreMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_semaphoreClass, semaphoreMetaclass);
        SetSuperclass(_semaphoreClass, _linkedListClass);  // Semaphore inherits from LinkedList
        SetSuperclass(semaphoreMetaclass, linkedListMetaclass); // Semaphore class superclass = LinkedList class
        
        // ProcessorScheduler and its metaclass
        var processorSchedulerMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_processorSchedulerClass, processorSchedulerMetaclass);
        SetSuperclass(_processorSchedulerClass, _objectClass);
        SetSuperclass(processorSchedulerMetaclass, objectMetaclass); // ProcessorScheduler class superclass = Object class
        
        // InstructionStream and its metaclass
        var instructionStreamMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_instructionStreamClass, instructionStreamMetaclass);
        SetSuperclass(_instructionStreamClass, _objectClass);
        SetSuperclass(instructionStreamMetaclass, objectMetaclass); // InstructionStream class superclass = Object class
        
        // ContextPart and its metaclass
        var contextPartMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_contextPartClass, contextPartMetaclass);
        SetSuperclass(_contextPartClass, _instructionStreamClass);  // ContextPart inherits from InstructionStream
        SetSuperclass(contextPartMetaclass, instructionStreamMetaclass); // ContextPart class superclass = InstructionStream class
        
        // MethodContext and its metaclass (created early but set up here)
        var methodContextMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_methodContextClass, methodContextMetaclass);
        SetSuperclass(_methodContextClass, _contextPartClass);
        SetSuperclass(methodContextMetaclass, contextPartMetaclass); // MethodContext class superclass = ContextPart class
        
        // BlockContext and its metaclass (created early but set up here)
        var blockContextMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_blockContextClass, blockContextMetaclass);
        SetSuperclass(_blockContextClass, _contextPartClass);
        SetSuperclass(blockContextMetaclass, contextPartMetaclass); // BlockContext class superclass = ContextPart class
        
        // I/O and Transcript class hierarchies
        // Stream and its metaclass
        var streamMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_streamClass, streamMetaclass);
        SetSuperclass(_streamClass, _objectClass);
        SetSuperclass(streamMetaclass, objectMetaclass); // Stream class superclass = Object class
        
        // PositionableStream and its metaclass
        var positionableStreamMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_positionableStreamClass, positionableStreamMetaclass);
        SetSuperclass(_positionableStreamClass, _streamClass);  // PositionableStream inherits from Stream
        SetSuperclass(positionableStreamMetaclass, streamMetaclass); // PositionableStream class superclass = Stream class
        
        // WriteStream and its metaclass
        var writeStreamMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_writeStreamClass, writeStreamMetaclass);
        SetSuperclass(_writeStreamClass, _positionableStreamClass);  // WriteStream inherits from PositionableStream
        SetSuperclass(writeStreamMetaclass, positionableStreamMetaclass); // WriteStream class superclass = PositionableStream class
        
        // ReadStream and its metaclass
        var readStreamMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_readStreamClass, readStreamMetaclass);
        SetSuperclass(_readStreamClass, _positionableStreamClass);  // ReadStream inherits from PositionableStream
        SetSuperclass(readStreamMetaclass, positionableStreamMetaclass); // ReadStream class superclass = PositionableStream class
        
        // StringHolder and its metaclass
        var stringHolderMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_stringHolderClass, stringHolderMetaclass);
        SetSuperclass(_stringHolderClass, _objectClass);
        SetSuperclass(stringHolderMetaclass, objectMetaclass); // StringHolder class superclass = Object class
        
        // TextCollector and its metaclass
        var textCollectorMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_textCollectorClass, textCollectorMetaclass);
        SetSuperclass(_textCollectorClass, _stringHolderClass);  // TextCollector inherits from StringHolder
        SetSuperclass(textCollectorMetaclass, stringHolderMetaclass); // TextCollector class superclass = StringHolder class
        
        // Text and its metaclass
        var textMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        SetClass(_textClass, textMetaclass);
        SetSuperclass(_textClass, _arrayClass);  // Text inherits from ArrayedCollection (we'll use Array for now)
        SetSuperclass(textMetaclass, arrayMetaclass); // Text class superclass = Array class
        
        // Step 5.5: Set format fields for variable subclasses
        SetClassFormats();
        
        // Step 6: Initialize method dictionaries for all classes
        InitializeMethodDictionaries();
        
        // Step 7: Register classes for lookup
        RegisterClass("Object", _objectClass);
        RegisterClass("Behavior", _behaviorClass);
        RegisterClass("ClassDescription", _classDescriptionClass);
        RegisterClass("Metaclass", _metaclassClass);
        RegisterClass("Class", _classClass);
        RegisterClass("CompiledMethod", _compiledMethodClass);
        RegisterClass("MethodDictionary", _methodDictionaryClass);
        RegisterClass("MethodContext", _methodContextClass);
        RegisterClass("BlockContext", _blockContextClass);
        RegisterClass("String", _stringClass);
        RegisterClass("Symbol", _symbolClass);
        RegisterClass("Array", _arrayClass);
        RegisterClass("ByteArray", _byteArrayClass);
        RegisterClass("Association", _associationClass);
        RegisterClass("Dictionary", _dictionaryClass);
        
        // Numeric classes
        RegisterClass("Magnitude", _magnitudeClass);
        RegisterClass("Number", _numberClass);
        RegisterClass("Integer", _integerClass);
        RegisterClass("SmallInteger", _smallIntegerClass);
        RegisterClass("Float", _floatClass);
        RegisterClass("LargePositiveInteger", _largePositiveIntegerClass);
        RegisterClass("LargeNegativeInteger", _largeNegativeIntegerClass);
        
        RegisterClass("Character", _characterClass);
        RegisterClass("True", _trueClass);
        RegisterClass("False", _falseClass);
        RegisterClass("UndefinedObject", _undefinedObjectClass);
        
        // Register Process-related classes
        RegisterClass("Link", _linkClass);
        RegisterClass("Process", _processClass);
        RegisterClass("LinkedList", _linkedListClass);
        RegisterClass("Semaphore", _semaphoreClass);
        RegisterClass("ProcessorScheduler", _processorSchedulerClass);
        RegisterClass("InstructionStream", _instructionStreamClass);
        RegisterClass("ContextPart", _contextPartClass);
        
        // Register I/O and Transcript classes
        RegisterClass("Stream", _streamClass);
        RegisterClass("PositionableStream", _positionableStreamClass);
        RegisterClass("WriteStream", _writeStreamClass);
        RegisterClass("ReadStream", _readStreamClass);
        RegisterClass("StringHolder", _stringHolderClass);
        RegisterClass("TextCollector", _textCollectorClass);
        RegisterClass("Text", _textClass);
        
        // Step 7.5: Singleton objects were already created in Phase 1
        // They're at indices 0, 1, 2 as required
        
        // Step 8: Create the Smalltalk global dictionary (needed before Process infrastructure)
        CreateSmalltalkGlobal();
        
        LogColdStart(LogLevel.Information, "Created {Count} core classes", _classes.Count);
    }

    /// <summary>
    /// Initializes method dictionaries for all classes that were created.
    /// Each class needs a properly structured method dictionary even if empty.
    /// </summary>
    private void InitializeMethodDictionaries()
    {
        LogColdStart(LogLevel.Information, "Initializing method dictionaries for all classes...");
        
        // Initialize method dictionaries for all classes
        InitializeClassMethodDictionary(_objectClass);
        InitializeClassMethodDictionary(_behaviorClass);
        InitializeClassMethodDictionary(_classDescriptionClass);
        InitializeClassMethodDictionary(_metaclassClass);
        InitializeClassMethodDictionary(_classClass);
        InitializeClassMethodDictionary(_compiledMethodClass);
        InitializeClassMethodDictionary(_methodDictionaryClass);
        InitializeClassMethodDictionary(_methodContextClass);
        InitializeClassMethodDictionary(_blockContextClass);
        InitializeClassMethodDictionary(_stringClass);
        InitializeClassMethodDictionary(_symbolClass);
        InitializeClassMethodDictionary(_arrayClass);
        InitializeClassMethodDictionary(_byteArrayClass);
        InitializeClassMethodDictionary(_associationClass);
        InitializeClassMethodDictionary(_dictionaryClass);
        
        // Numeric classes
        InitializeClassMethodDictionary(_magnitudeClass);
        InitializeClassMethodDictionary(_numberClass);
        InitializeClassMethodDictionary(_integerClass);
        InitializeClassMethodDictionary(_smallIntegerClass);
        InitializeClassMethodDictionary(_floatClass);
        InitializeClassMethodDictionary(_largePositiveIntegerClass);
        InitializeClassMethodDictionary(_largeNegativeIntegerClass);
        
        InitializeClassMethodDictionary(_characterClass);
        InitializeClassMethodDictionary(_trueClass);
        InitializeClassMethodDictionary(_falseClass);
        InitializeClassMethodDictionary(_undefinedObjectClass);
        InitializeClassMethodDictionary(_linkClass);
        InitializeClassMethodDictionary(_processClass);
        InitializeClassMethodDictionary(_linkedListClass);
        InitializeClassMethodDictionary(_semaphoreClass);
        InitializeClassMethodDictionary(_processorSchedulerClass);
        InitializeClassMethodDictionary(_instructionStreamClass);
        InitializeClassMethodDictionary(_contextPartClass);
        
        // Also initialize method dictionaries for metaclasses
        var classes = new[] { _objectClass, _behaviorClass, _classDescriptionClass, 
            _metaclassClass, _classClass, _compiledMethodClass, _methodDictionaryClass,
            _methodContextClass, _blockContextClass, _stringClass, _symbolClass,
            _arrayClass, _byteArrayClass, _associationClass, _dictionaryClass,
            _magnitudeClass, _numberClass, _integerClass, _smallIntegerClass, 
            _floatClass, _largePositiveIntegerClass, _largeNegativeIntegerClass,
            _characterClass, _trueClass, _falseClass, _undefinedObjectClass,
            _linkClass, _processClass, _linkedListClass, _semaphoreClass, _processorSchedulerClass,
            _instructionStreamClass, _contextPartClass };
            
        foreach (var classOop in classes)
        {
            var metaclassOop = _memory.FetchClassOf(classOop);
            if (!metaclassOop.IsNil && !metaclassOop.IsSmallInteger)
            {
                InitializeClassMethodDictionary(metaclassOop);
            }
        }
        
        LogColdStart(LogLevel.Information, "Method dictionaries initialized");
    }
    
    private void InitializeClassMethodDictionary(OOP classOop)
    {
        // Check if class already has a method dictionary
        var existingDict = _memory.FetchPointer(classOop, 1); // Field 1 in Behavior
        
        if (existingDict.IsNil || existingDict.IsSmallInteger)
        {
            // Create and install a new method dictionary
            var methodDict = CreateMethodDictionary();
            _memory.StorePointer(classOop, 1, methodDict);
        }
    }

    /// <summary>
    /// Phase 2: Installs critical primitive methods.
    /// These are the absolute minimum methods needed to bootstrap.
    /// </summary>
    private void InstallPrimitiveMethods()
    {
        LogColdStart(LogLevel.Information, "Phase 2: Installing primitive methods...");
        
        // Object primitives
        InstallPrimitiveMethod(_objectClass, "class", 111, 0);           // Get class
        InstallPrimitiveMethod(_objectClass, "==", 110, 1);              // Identity
        InstallPrimitiveMethod(_objectClass, "=", 110, 1);               // Equality (defaults to identity for Object)
        InstallPrimitiveMethod(_objectClass, "basicAt:", 60, 1);         // Field access
        InstallPrimitiveMethod(_objectClass, "basicAt:put:", 61, 2);     // Field store
        
        // Initialize method - no primitive, just returns self
        InstallCustomPrimitiveMethod(_objectClass, "initialize", 0, 0,
            [0x70, 0x7C], // pushReceiver (112), returnTop (124)
            []);
        
        // basicSize - special case: returns 0 if primitive fails
        InstallCustomPrimitiveMethod(_objectClass, "basicSize", 62, 0, 
            [0x75, 0x7C], // pushZero (117), returnTop (124)
            []);
        
        // Behavior primitives for object creation  
        InstallPrimitiveMethod(_behaviorClass, "new", 70, 0);            // Allocate
        InstallPrimitiveMethod(_behaviorClass, "new:", 71, 1);           // Allocate with size
        InstallPrimitiveMethod(_behaviorClass, "basicNew", 70, 0);       // Allocate
        InstallPrimitiveMethod(_behaviorClass, "basicNew:", 71, 1);      // Allocate with size
        
        // SmallInteger arithmetic primitives
        InstallPrimitiveMethod(_smallIntegerClass, "+", 1, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "-", 2, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "<", 3, 1);
        InstallPrimitiveMethod(_smallIntegerClass, ">", 4, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "<=", 5, 1);
        InstallPrimitiveMethod(_smallIntegerClass, ">=", 6, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "=", 7, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "~=", 8, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "*", 9, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "/", 10, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "\\\\", 11, 1);
        InstallPrimitiveMethod(_smallIntegerClass, "//", 12, 1);
        
        // Array/ByteArray primitives
        InstallPrimitiveMethod(_arrayClass, "at:", 60, 1);
        InstallPrimitiveMethod(_arrayClass, "at:put:", 61, 2);
        InstallPrimitiveMethod(_arrayClass, "size", 62, 0);
        InstallPrimitiveMethod(_byteArrayClass, "at:", 63, 1);
        InstallPrimitiveMethod(_byteArrayClass, "at:put:", 64, 2);
        InstallPrimitiveMethod(_byteArrayClass, "size", 62, 0);
        
        // Symbol primitives
        InstallPrimitiveMethod(_symbolClass, "=", 110, 1);
        InstallPrimitiveMethod(_symbolClass, "hash", 75, 0);
        
        LogColdStart(LogLevel.Information, "Installed essential primitive methods");
    }

    /// <summary>
    /// Phase 3: Installs hardcoded bootstrap methods.
    /// These are the critical methods for class creation and method compilation.
    /// </summary>
    private void InstallBootstrapMethods()
    {
        LogColdStart(LogLevel.Information, "Phase 3: Installing bootstrap methods...");
        
        // Install Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
        // Literals from the compiled Smalltalk-80.st:
        // Note: We need to create the Association for Smalltalk and resolve it later
        var smalltalkSymbol = GetOrCreateSymbol("Smalltalk");
        var smalltalkAssoc = CreateAssociation(smalltalkSymbol, GetOrCreateGlobal("Smalltalk"));
        
        var subclassLiterals = new[]
        {
            GetOrCreateSymbol("isVariable"),
            GetOrCreateSymbol("ifTrue:"),
            GetOrCreateSymbol("isPointers"),
            GetOrCreateSymbol("variableSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"),
            GetOrCreateSymbol("isBytes"),
            GetOrCreateSymbol("variableByteSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"),
            GetOrCreateSymbol("variableWordSubclass:instanceVariableNames:classVariableNames:poolDictionaries:category:"),
            GetOrCreateSymbol("name:inEnvironment:subclassOf:instanceVariableNames:variable:words:pointers:classVariableNames:poolDictionaries:category:comment:changed:"),
            smalltalkAssoc
        };
        
        var subclassMethod = CreateCompiledMethod(
            SubclassCreationBytecode,
            subclassLiterals,
            5, // 5 arguments
            0, // No temporaries
            15 // Stack depth from compiler
        );
        InstallMethod(_classClass, "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", subclassMethod);
        
        // Install Behavior>>compile:classified:
        var compileLiterals = new[]
        {
            GetOrCreateSymbol("compile:classified:notifying:")
        };
        
        var compileMethod = CreateCompiledMethod(
            CompileMethodBytecode,
            compileLiterals,
            2, // 2 arguments
            0, // No temporaries
            4  // Stack depth from compiler
        );
        InstallMethod(_behaviorClass, "compile:classified:", compileMethod);
        
        // Also install some helper methods that are needed
        // isNil: ^false
        InstallNonPrimitiveMethod(_objectClass, "isNil", [0x72, 0x7C]); // pushFalse (114), returnTop (124)
        
        // notNil: ^true
        InstallNonPrimitiveMethod(_objectClass, "notNil", [0x71, 0x7C]); // pushTrue (113), returnTop (124)
        
        // yourself: ^self
        InstallNonPrimitiveMethod(_objectClass, "yourself", [0x78]); // returnReceiver (120)
        
        LogColdStart(LogLevel.Information, "Installed subclass creation and method compilation methods");
    }
    
    /// <summary>
    /// Phase 2b: Initializes the Process infrastructure including ProcessorScheduler and initial process.
    /// </summary>
    private void InitializeProcessInfrastructure()
    {
        LogColdStart(LogLevel.Information, "Phase 2b: Initializing Process infrastructure...");
        
        // Create the ProcessorScheduler singleton
        _processorScheduler = CreateProcessorScheduler();
        
        // Store it in the Smalltalk global dictionary as 'Processor'
        var processorSymbol = GetOrCreateSymbol("Processor");
        if (_globals.TryGetValue("Smalltalk", out var smalltalkDict))
        {
            AddToDictionary(smalltalkDict, processorSymbol, _processorScheduler);
        }
        _globals["Processor"] = _processorScheduler;
        
        // Create the initial process
        var initialProcess = CreateInitialProcess();
        
        // Set the initial process as the active process
        _memory.StorePointer(_processorScheduler, 1, initialProcess); // Field 1: activeProcess
        
        LogColdStart(LogLevel.Information, "Process infrastructure initialized with initial process at priority 4");
    }
    
    /// <summary>
    /// Creates the ProcessorScheduler singleton with 8 priority queues.
    /// </summary>
    private OOP CreateProcessorScheduler()
    {
        // ProcessorScheduler has 2 instance variables:
        // Field 0: quiescentProcessLists (Array of 8 LinkedLists for priorities 1-8)
        // Field 1: activeProcess (the currently running Process)
        var scheduler = _memory.AllocateObject(_processorSchedulerClass, 2);
        
        // Create array of 8 LinkedLists for priority queues
        var priorityQueues = _memory.AllocateObject(_arrayClass, 8);
        
        // Initialize each priority queue as an empty LinkedList
        for (int priority = 0; priority < 8; priority++)
        {
            var linkedList = CreateEmptyLinkedList();
            _memory.StorePointer(priorityQueues, priority, linkedList);
        }
        
        // Store the priority queues array
        _memory.StorePointer(scheduler, 0, priorityQueues);
        
        // activeProcess will be set later
        _memory.StorePointer(scheduler, 1, _nilObject);
        
        return scheduler;
    }
    
    /// <summary>
    /// Creates an empty LinkedList for process queues.
    /// </summary>
    private OOP CreateEmptyLinkedList()
    {
        // LinkedList has 2 instance variables:
        // Field 0: firstLink (initially nil)
        // Field 1: lastLink (initially nil)
        var linkedList = _memory.AllocateObject(_linkedListClass, 2);
        _memory.StorePointer(linkedList, 0, _nilObject);
        _memory.StorePointer(linkedList, 1, _nilObject);
        return linkedList;
    }
    
    /// <summary>
    /// Creates the initial Process with priority 4 (UserSchedulingPriority).
    /// </summary>
    private OOP CreateInitialProcess()
    {
        // Process instance variables (inherits nextLink from Link):
        // Field 0: nextLink (from Link superclass)
        // Field 1: suspendedContext (the MethodContext to resume)
        // Field 2: priority (SmallInteger 4 for UserSchedulingPriority)
        // Field 3: myList (the LinkedList this process is in, or nil)
        
        var process = _memory.AllocateObject(_processClass, 4);
        
        // nextLink is nil (not linked to other processes yet)
        _memory.StorePointer(process, 0, _nilObject);
        
        // Create initial MethodContext for the process
        var initialContext = CreateInitialMethodContext();
        _memory.StorePointer(process, 1, initialContext);
        
        // Priority 4 (UserSchedulingPriority)
        _memory.StorePointer(process, 2, OOP.FromSmallInteger(4));
        
        // myList is nil (not in a queue yet)
        _memory.StorePointer(process, 3, _nilObject);
        
        return process;
    }
    
    /// <summary>
    /// Creates an initial MethodContext for the initial process.
    /// This context will execute a startup method.
    /// </summary>
    private OOP CreateInitialMethodContext()
    {
        // MethodContext instance variables:
        // From InstructionStream:
        //   Field 0: sender (nil for initial context)
        //   Field 1: pc (program counter, starts at 0)
        // From ContextPart:
        //   Field 2: stackp (stack pointer, starts at 0)
        //   Field 3: stack (temporary stack array)
        //   Field 4: method (CompiledMethod being executed)
        //   Field 5: tempframe (temporary variables array)
        // From MethodContext:
        //   Field 6: receiver (the object receiving the message)
        
        var context = _memory.AllocateObject(_methodContextClass, 7);
        
        // sender is nil (this is the root context)
        _memory.StorePointer(context, 0, _nilObject);
        
        // pc starts at 0
        _memory.StorePointer(context, 1, OOP.FromSmallInteger(0));
        
        // stackp starts at 0
        _memory.StorePointer(context, 2, OOP.FromSmallInteger(0));
        
        // Create stack array (16 slots should be enough for startup)
        var stack = _memory.AllocateObject(_arrayClass, 16);
        for (int i = 0; i < 16; i++)
        {
            _memory.StorePointer(stack, i, _nilObject);
        }
        _memory.StorePointer(context, 3, stack);
        
        // Create a simple startup method that just returns self
        // This will be replaced with actual startup code later
        var startupMethod = CreateStartupMethod();
        _memory.StorePointer(context, 4, startupMethod);
        
        // Create tempframe array (no temps needed for simple startup)
        var tempframe = _memory.AllocateObject(_arrayClass, 0);
        _memory.StorePointer(context, 5, tempframe);
        
        // Set receiver to the Smalltalk global dictionary (SystemDictionary)
        // This allows the startup method to access globals
        var receiver = _globals.TryGetValue("Smalltalk", out var smalltalk) ? smalltalk : _nilObject;
        _memory.StorePointer(context, 6, receiver);
        
        return context;
    }
    
    /// <summary>
    /// Creates a simple startup method for the initial process.
    /// </summary>
    private OOP CreateStartupMethod()
    {
        // Create a method that performs: Smalltalk at: #startup put: true
        // Then returns self
        // Bytecode: pushReceiver, pushLiteral0 (#startup), pushTrue, send #at:put: (2 args), pop, returnReceiver
        var bytecodes = new byte[] { 
            0x70,       // pushReceiver (Smalltalk)
            0x40,       // pushLiteral0 (#startup)
            0x71,       // pushTrue
            0xE1,       // send literal 1 with 2 args (#at:put:)
            0x87,       // pop
            0x78        // returnReceiver
        };
        
        // Literals needed: #startup, #at:put:
        var literals = new[] {
            GetOrCreateSymbol("startup"),
            GetOrCreateSymbol("at:put:")
        };
        
        return CreateCompiledMethod(bytecodes, literals, 0, 0, 3);
    }

    /// <summary>
    /// Phase 4: Processes Smalltalk source file.
    /// </summary>
    private async Task ProcessSmalltalkSourceAsync(string sourcePath)
    {
        LogColdStart(LogLevel.Information, "Phase 4: Processing Smalltalk source from {Path}...", sourcePath);
        
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Smalltalk source file not found: {sourcePath}");
        }
        
        var reader = new ChunkReader(sourcePath, _logger);
        var chunks = await reader.ReadAllChunksAsync();
        
        LogColdStart(LogLevel.Information, "Read {Count} chunks from source file", chunks.Count);
        
        foreach (var chunk in chunks)
        {
            try
            {
                ProcessChunk(chunk);
            }
            catch (Exception ex)
            {
                if (_runtimeOptions.EnableColdStartLogging)
                {
                    _logger.LogWarning(ex, "[COLD START] Failed to process chunk at line {Line}", chunk.LineNumber);
                }
                // Continue with next chunk
            }
        }
        
        // Process any deferred chunks
        ProcessDeferredChunks();
        
        LogColdStart(LogLevel.Information, "Source processing complete");
    }
    
    /// <summary>
    /// Phase 4b: Initializes TextCollector and creates the global Transcript instance.
    /// </summary>
    private void InitializeTranscript()
    {
        LogColdStart(LogLevel.Information, "Phase 4b: Initializing Transcript...");
        
        // Find TextCollector class
        if (!_classes.TryGetValue("TextCollector", out var textCollectorClass))
        {
            LogColdStart(LogLevel.Warning, "TextCollector class not found, skipping Transcript initialization");
            return;
        }
        
        // Install console-aware methods into TextCollector
        InstallTranscriptMethods(textCollectorClass);
        
        // Create Transcript instance
        var transcript = CreateTranscriptInstance(textCollectorClass);
        
        // Store in globals
        var transcriptSymbol = GetOrCreateSymbol("Transcript");
        _globals["Transcript"] = transcript;
        
        // Add to Smalltalk dictionary
        if (_globals.TryGetValue("Smalltalk", out var smalltalkDict))
        {
            AddToDictionary(smalltalkDict, transcriptSymbol, transcript);
        }
        
        LogColdStart(LogLevel.Information, "Transcript initialized and stored as global");
    }
    
    /// <summary>
    /// Installs console-aware methods into TextCollector class.
    /// </summary>
    private void InstallTranscriptMethods(OOP textCollectorClass)
    {
        // Install show: method with primitive 253
        // show: aString
        //     <primitive: 253>
        //     self endEntry
        InstallCustomPrimitiveMethod(textCollectorClass, "show:", 253, 1,
            [0x70, 0xD0, 0x87, 0x78], // pushReceiver, send endEntry, pop, returnReceiver
            [GetOrCreateSymbol("endEntry")]);
        
        // Install cr method - special version that writes newline directly
        // cr
        //     Transcript show: '\r\n'
        //     ^self
        // We'll create a special cr primitive method
        var crMethod = CreateCrMethod();
        InstallMethod(textCollectorClass, "cr", crMethod);
        
        // Install endEntry method with primitive 251 (flush)
        // endEntry
        //     <primitive: 251>
        InstallCustomPrimitiveMethod(textCollectorClass, "endEntry", 251, 0,
            [0x70, 0x7C], // pushReceiver, returnTop
            []);
        
        // Install clear method with primitive 252
        // clear
        //     <primitive: 252>
        InstallCustomPrimitiveMethod(textCollectorClass, "clear", 252, 0,
            [0x70, 0x7C], // pushReceiver, returnTop
            []);
    }
    
    /// <summary>
    /// Creates and initializes the Transcript instance.
    /// </summary>
    private OOP CreateTranscriptInstance(OOP textCollectorClass)
    {
        // TextCollector has 2 instance variables:
        // Field 0: contents (inherited from StringHolder)
        // Field 1: entryStream
        var transcript = _memory.AllocateObject(textCollectorClass, 2);
        
        // Initialize contents with empty Text
        var textClass = _classes.GetValueOrDefault("Text", OOP.Nil);
        OOP emptyText;
        if (!textClass.IsNil)
        {
            emptyText = _memory.AllocateObject(textClass, 2);
            var emptyString = CreateString("");
            _memory.StorePointer(emptyText, 0, emptyString); // string field
            _memory.StorePointer(emptyText, 1, OOP.Nil); // runs field
        }
        else
        {
            // Fallback to empty string if Text not available
            emptyText = CreateString("");
        }
        _memory.StorePointer(transcript, 0, emptyText);
        
        // Initialize entryStream with WriteStream
        var writeStreamClass = _classes.GetValueOrDefault("WriteStream", OOP.Nil);
        if (!writeStreamClass.IsNil)
        {
            var stream = _memory.AllocateObject(writeStreamClass, 5);
            var streamString = CreateString("");
            _memory.StorePointer(stream, 0, streamString); // collection
            _memory.StorePointer(stream, 1, OOP.FromSmallInteger(1)); // position
            _memory.StorePointer(stream, 2, OOP.FromSmallInteger(0)); // readLimit
            _memory.StorePointer(stream, 3, OOP.FromSmallInteger(200)); // writeLimit
            _memory.StorePointer(transcript, 1, stream);
        }
        else
        {
            // Leave entryStream as nil if WriteStream not available
            _memory.StorePointer(transcript, 1, OOP.Nil);
        }
        
        return transcript;
    }
    
    /// <summary>
    /// Creates a special cr method that outputs a newline to console.
    /// </summary>
    private OOP CreateCrMethod()
    {
        // Create bytecode for: self show: '\n'. ^self
        var bytecode = new byte[]
        {
            0x70,   // 0: pushReceiver (self)
            0x21,   // 1: pushConstant 1 (literal at index 1 - the newline string)
            0xE0,   // 2: send show: (literal at index 0)
            0x87,   // 3: pop
            0x78    // 4: returnReceiver
        };
        
        var literals = new OOP[]
        {
            GetOrCreateSymbol("show:"),
            CreateString("\n")  // Just newline for Unix-style line endings
        };
        
        return CreateCompiledMethod(bytecode, literals, 0, 0, 3, 0); // No primitive
    }

    private void ProcessChunk(Chunk chunk)
    {
        switch (chunk.Type)
        {
            case ChunkType.ClassDefinition:
                ProcessClassDefinition(chunk);
                break;
                
            case ChunkType.MethodDefinition:
                ProcessMethodDefinition(chunk);
                break;
                
            case ChunkType.DoIt:
                ProcessDoItExpression(chunk);
                break;
                
            case ChunkType.Comment:
                // Skip comments during bootstrap
                break;
        }
    }

    private void ProcessClassDefinition(Chunk chunk)
    {
        LogColdStart(LogLevel.Debug, "Creating class: {Content}", 
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Parse class definition and send subclass:instanceVariableNames:... message
        // This will use the hardcoded subclass creation method
    }

    private void ProcessMethodDefinition(Chunk chunk)
    {
        LogColdStart(LogLevel.Debug, "Compiling method: {Content}",
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Parse method and send compile:classified: message
        // This will use the hardcoded compile method
    }

    private void ProcessDoItExpression(Chunk chunk)
    {
        LogColdStart(LogLevel.Debug, "Executing expression: {Content}",
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Compile and execute DoIt expression
    }

    private void ProcessDeferredChunks()
    {
        if (_pendingChunks.Count > 0)
        {
            LogColdStart(LogLevel.Information, "Processing {Count} deferred chunks", _pendingChunks.Count);
            
            foreach (var chunk in _pendingChunks)
            {
                ProcessChunk(chunk);
            }
            _pendingChunks.Clear();
        }
    }

    // Helper methods

    private void SetClass(OOP objectOop, OOP classOop)
    {
        // The class of an object is stored in the object's header in the ObjectTable
        // This is managed by ObjectMemory, not as a field in the object itself
        _memory.SetClassOf(objectOop, classOop);
    }

    private void SetSuperclass(OOP classOop, OOP superclassOop)
    {
        // Superclass is field 0 in Behavior per Blue Book
        // Field 0: superclass
        // Field 1: methodDict
        // Field 2: format
        _memory.StorePointer(classOop, 0, superclassOop);
    }
    
    /// <summary>
    /// Sets the format field for classes to indicate their instance structure.
    /// </summary>
    private void SetClassFormats()
    {
        // Format field (field 2 in Behavior) encodes the instance structure
        // For most classes, format is 0 (regular fixed-size instances)
        // For variableByteSubclass: format = 1 (stores bytes)
        // For variableWordSubclass: format = 2 (stores words/pointers)
        // For variableSubclass: format = 3 (stores pointers with named instance variables)
        
        // Most classes have regular format (0)
        // We'll set this as default for all classes first
        var allClasses = new[] { 
            _objectClass, _behaviorClass, _classDescriptionClass, _metaclassClass, _classClass,
            _compiledMethodClass, _methodDictionaryClass, _methodContextClass, _blockContextClass,
            _stringClass, _symbolClass, _arrayClass, _byteArrayClass, _associationClass, _dictionaryClass,
            _magnitudeClass, _numberClass, _integerClass, _smallIntegerClass, _floatClass,
            _largePositiveIntegerClass, _largeNegativeIntegerClass, _characterClass,
            _trueClass, _falseClass, _undefinedObjectClass,
            _linkClass, _processClass, _linkedListClass, _semaphoreClass, _processorSchedulerClass,
            _instructionStreamClass, _contextPartClass, _streamClass, _positionableStreamClass,
            _writeStreamClass, _readStreamClass, _stringHolderClass, _textCollectorClass, _textClass
        };
        
        foreach (var classOop in allClasses)
        {
            // Default format is 0 (regular fixed-size instances)
            _memory.StorePointer(classOop, 2, OOP.FromSmallInteger(0));
        }
        
        // Now set special formats for variable subclasses
        
        // ByteArray, String, Symbol are variableByteSubclass (format = 1)
        _memory.StorePointer(_byteArrayClass, 2, OOP.FromSmallInteger(1));
        _memory.StorePointer(_stringClass, 2, OOP.FromSmallInteger(1));
        _memory.StorePointer(_symbolClass, 2, OOP.FromSmallInteger(1));
        
        // LargePositiveInteger and LargeNegativeInteger are variableByteSubclass (format = 1)
        _memory.StorePointer(_largePositiveIntegerClass, 2, OOP.FromSmallInteger(1));
        _memory.StorePointer(_largeNegativeIntegerClass, 2, OOP.FromSmallInteger(1));
        
        // Float is variableWordSubclass (format = 2) - stores floating point words
        _memory.StorePointer(_floatClass, 2, OOP.FromSmallInteger(2));
        
        // Array and its subclasses are variableSubclass (format = 3) - variable pointer objects
        _memory.StorePointer(_arrayClass, 2, OOP.FromSmallInteger(3));
        _memory.StorePointer(_textClass, 2, OOP.FromSmallInteger(3)); // Text inherits from Array
        
        // MethodDictionary and Dictionary are also variable pointer objects
        _memory.StorePointer(_dictionaryClass, 2, OOP.FromSmallInteger(3));
        _memory.StorePointer(_methodDictionaryClass, 2, OOP.FromSmallInteger(3));
        
        // CompiledMethod has a special format (contains both pointers and bytes)
        // Format = 4 for compiled methods (special handling)
        _memory.StorePointer(_compiledMethodClass, 2, OOP.FromSmallInteger(4));
        
        LogColdStart(LogLevel.Information, "Set class formats for variable subclasses");
    }

    private void RegisterClass(string name, OOP classOop)
    {
        _classes[name] = classOop;
        LogColdStart(LogLevel.Trace, "Registered class {Name} -> {OOP}", name, classOop);
    }

    private void InstallPrimitiveMethod(OOP classOop, string selector, int primitiveIndex, int numArgs)
    {
        // Create a minimal CompiledMethod that just invokes a primitive
        var method = CreatePrimitiveMethod(primitiveIndex, numArgs);
        InstallMethod(classOop, selector, method);
    }
    
    private void InstallCustomPrimitiveMethod(OOP classOop, string selector, int primitiveIndex, int numArgs, 
        byte[] fallbackBytecode, OOP[] literals)
    {
        // Create a primitive method with custom fallback bytecode
        var method = CreateCompiledMethod(fallbackBytecode, literals, numArgs, 0, 3, primitiveIndex);
        InstallMethod(classOop, selector, method);
    }

    private void InstallNonPrimitiveMethod(OOP classOop, string selector, byte[] bytecode)
    {
        var method = CreateCompiledMethod(bytecode, [], 0, 0, 2);
        InstallMethod(classOop, selector, method);
    }

    private OOP CreatePrimitiveMethod(int primitiveIndex, int numArgs)
    {
        // Default fallback: self primitiveFailed
        // Bytecode: pushReceiver (112), send literal 0 with 0 args (208), returnTop (124)
        var bytecodes = new byte[] { 0x70, 0xD0, 0x7C }; // pushReceiver, sendLiteralSelector0Args0, returnTop
        
        // Need #primitiveFailed as literal
        var literals = new[] { GetOrCreateSymbol("primitiveFailed") };
        
        return CreateCompiledMethod(bytecodes, literals, numArgs, 0, 2, primitiveIndex);
    }

    private OOP CreateCompiledMethod(byte[] bytecodes, OOP[] literals, int numArgs, int numTemps, int stackSize, int primitiveIndex = 0)
    {
        // Create a CompiledMethod object
        // Fields: selector (not used here), bytecodes, literals, header
        var method = _memory.AllocateObject(_compiledMethodClass, 4);
        
        // Build method header per Blue Book format
        var header = (numArgs & 0x1F) | 
                    ((numTemps & 0x3F) << 5) | 
                    ((literals.Length & 0x3F) << 11) | 
                    ((stackSize & 0x1F) << 17);
        
        if (primitiveIndex > 0)
        {
            header |= 0x10000; // Set primitive flag
        }
        
        // Store header
        _memory.StorePointer(method, 0, OOP.FromSmallInteger(header));
        
        // Store primitive index
        _memory.StorePointer(method, 1, OOP.FromSmallInteger(primitiveIndex));
        
        // Store literals array
        var literalsArray = _memory.AllocateObject(_arrayClass, literals.Length);
        for (int i = 0; i < literals.Length; i++)
        {
            _memory.StorePointer(literalsArray, i, literals[i]);
        }
        _memory.StorePointer(method, 2, literalsArray);
        
        // Store bytecodes as ByteArray
        var bytecodeArray = _memory.AllocateByteObject(_byteArrayClass, bytecodes.Length);
        for (int i = 0; i < bytecodes.Length; i++)
        {
            _memory.StoreByte(bytecodeArray, i, bytecodes[i]);
        }
        _memory.StorePointer(method, 3, bytecodeArray);
        
        return method;
    }

    private void InstallMethod(OOP classOop, string selector, OOP methodOop)
    {
        // Get or create method dictionary
        var methodDict = _memory.FetchPointer(classOop, 1); // Field 1 in Behavior is methodDict
        
        if (methodDict.IsNil || methodDict.IsSmallInteger)
        {
            // Create new method dictionary
            methodDict = CreateMethodDictionary();
            _memory.StorePointer(classOop, 1, methodDict);
            LogColdStart(LogLevel.Trace, "Created method dictionary for class {Class}", classOop);
        }
        
        // Add selector->method to dictionary
        var selectorSymbol = GetOrCreateSymbol(selector);
        AddToMethodDictionary(methodDict, selectorSymbol, methodOop);
        LogColdStart(LogLevel.Trace, "Installed method {Selector} in class {Class}", selector, classOop);
    }

    private OOP CreateMethodDictionary()
    {
        // MethodDictionary structure per Blue Book:
        // Field 0: tally (number of elements)
        // Field 1: array (contains alternating keys and values)
        var dict = _memory.AllocateObject(_methodDictionaryClass, 2);
        
        // Initialize tally to 0
        _memory.StorePointer(dict, 0, OOP.FromSmallInteger(0));
        
        // Create array for key-value pairs (capacity for 32 entries = 64 slots)
        var array = _memory.AllocateObject(_arrayClass, 64);
        
        // Initialize all slots to nil
        for (int i = 0; i < 64; i++)
        {
            _memory.StorePointer(array, i, _nilObject);
        }
        
        _memory.StorePointer(dict, 1, array);
        
        return dict;
    }

    private void AddToMethodDictionary(OOP dictOop, OOP selectorOop, OOP methodOop)
    {
        // Get tally and array from method dictionary
        var tallyOop = _memory.FetchPointer(dictOop, 0);
        var arrayOop = _memory.FetchPointer(dictOop, 1);
        
        if (arrayOop.IsNil || arrayOop.IsSmallInteger)
        {
            LogColdStart(LogLevel.Error, "Method dictionary has invalid array!");
            return;
        }
        
        var tally = tallyOop.IsSmallInteger ? tallyOop.SmallIntegerValue : 0;
        
        // Simple linear search in the array
        for (int i = 0; i < 32; i++)
        {
            var key = _memory.FetchPointer(arrayOop, i * 2);
            if (key.IsNil)
            {
                // Found empty slot
                _memory.StorePointer(arrayOop, i * 2, selectorOop);
                _memory.StorePointer(arrayOop, i * 2 + 1, methodOop);
                
                // Update tally
                _memory.StorePointer(dictOop, 0, OOP.FromSmallInteger(tally + 1));
                return;
            }
            else if (key.Value == selectorOop.Value)
            {
                // Replace existing method
                _memory.StorePointer(arrayOop, i * 2 + 1, methodOop);
                return;
            }
        }
        
        LogColdStart(LogLevel.Warning, "Method dictionary full!");
    }

    private OOP GetOrCreateSymbol(string str)
    {
        if (_symbols.TryGetValue(str, out var symbol))
        {
            return symbol;
        }
        
        // Create a proper Symbol object
        // Symbols are ByteArrays (subclass of ByteArray via String)
        // We need to allocate the bytes for the string content
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        
        // AllocateByteObject takes the byte size, not word size
        symbol = _memory.AllocateByteObject(_symbolClass, bytes.Length);
        
        // Store the bytes into the Symbol
        for (int i = 0; i < bytes.Length; i++)
        {
            _memory.StoreByte(symbol, i, bytes[i]);
        }
        
        LogColdStart(LogLevel.Trace, "Created symbol #{String} -> {OOP}", str, symbol);
        
        _symbols[str] = symbol;
        
        return symbol;
    }
    
    /// <summary>
    /// Creates a String object from a C# string.
    /// </summary>
    private OOP CreateString(string str)
    {
        // Create a String object
        // Strings are ByteArrays (like Symbols but not interned)
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        
        // AllocateByteObject takes the byte size, not word size
        var stringOop = _memory.AllocateByteObject(_stringClass, bytes.Length);
        
        // Store the bytes into the String
        for (int i = 0; i < bytes.Length; i++)
        {
            _memory.StoreByte(stringOop, i, bytes[i]);
        }
        
        LogColdStart(LogLevel.Trace, "Created string \"{String}\" -> {OOP}", str, stringOop);
        
        return stringOop;
    }
    
    private OOP CreateAssociation(OOP key, OOP value)
    {
        // Create an Association object with key and value
        var assoc = _memory.AllocateObject(_associationClass, 2);
        _memory.StorePointer(assoc, 0, key);  // Field 0: key
        _memory.StorePointer(assoc, 1, value); // Field 1: value
        return assoc;
    }
    
    private void CreateSingletonObjects()
    {
        // Create nil as a proper object (singleton instance of UndefinedObject)
        _nilObject = _memory.AllocateObject(_undefinedObjectClass, 0); // nil has no instance variables
        
        // Create the singleton true object
        _trueObject = _memory.AllocateObject(_trueClass, 0); // True has no instance variables
        
        // Create the singleton false object  
        _falseObject = _memory.AllocateObject(_falseClass, 0); // False has no instance variables
        
        LogColdStart(LogLevel.Information, "Created singleton objects (nil={0}, true={1}, false={2})", 
            _nilObject, _trueObject, _falseObject);
    }
    
    private void CreateSmalltalkGlobal()
    {
        // Create the Smalltalk global dictionary
        // This holds all global variables including class names
        var smalltalkDict = CreateDictionary();
        
        // Add all classes to the Smalltalk dictionary
        foreach (var kvp in _classes)
        {
            var nameSymbol = GetOrCreateSymbol(kvp.Key);
            AddToDictionary(smalltalkDict, nameSymbol, kvp.Value);
        }
        
        // Add the Smalltalk dictionary itself as a global
        var smalltalkSymbol = GetOrCreateSymbol("Smalltalk");
        AddToDictionary(smalltalkDict, smalltalkSymbol, smalltalkDict);
        
        // Store in globals
        _globals["Smalltalk"] = smalltalkDict;
        
        LogColdStart(LogLevel.Information, "Created Smalltalk global dictionary with {Count} entries", _classes.Count + 1);
    }
    
    private void CreateSpecialObjectsArray()
    {
        // Create the Special Objects Array
        // This array contains well-known objects that the VM needs quick access to
        // Following the old interpreter's specification:
        // 0: nil object
        // 1: false object
        // 2: true object
        // 3: ProcessorScheduler singleton (Processor)
        // 4: Metaclass class
        // 5: Smalltalk global dictionary (SystemDictionary instance)
        // 6: Symbol table (to be added)
        // 7: Process list (to be added)
        // 8: Behavior class
        
        const int specialObjectsSize = 9; // Standard slots per old interpreter
        var specialObjects = _memory.AllocateObject(_arrayClass, specialObjectsSize);
        
        // Initialize all slots to the nil object first
        for (int i = 0; i < specialObjectsSize; i++)
        {
            _memory.StorePointer(specialObjects, i, _nilObject);
        }
        
        // Populate the essential objects following the old interpreter's SpecialObjects indices
        // Store essential objects at their expected indices
        _memory.StorePointer(specialObjects, 0, _nilObject);           // nil (the actual object)
        _memory.StorePointer(specialObjects, 1, _falseObject);         // false
        _memory.StorePointer(specialObjects, 2, _trueObject);          // true
        _memory.StorePointer(specialObjects, 3, _processorScheduler.Value != 0 ? _processorScheduler : _nilObject); // ProcessorScheduler (set in InitializeProcessInfrastructure)
        _memory.StorePointer(specialObjects, 4, _metaclassClass);      // Metaclass class (CRITICAL GC ROOT!)
        
        // Store the Smalltalk global dictionary at index 5 - it contains all classes
        if (_globals.TryGetValue("Smalltalk", out var smalltalkDict))
        {
            _memory.StorePointer(specialObjects, 5, smalltalkDict);    // Smalltalk global (CRITICAL GC ROOT!)
        }
        else
        {
            _logger.LogError("[ERROR] Smalltalk dictionary not found in globals!");
        }
        
        // Store Behavior class at index 8 (for VM class operations)
        _memory.StorePointer(specialObjects, 8, _behaviorClass);
        
        // 6: Create and store the Symbol table
        // This is critical for selector interning and symbol uniqueness
        var symbolTable = CreateDictionary(); // Create a Dictionary to hold all symbols
        _memory.StorePointer(specialObjects, 6, symbolTable);
        
        // Store all existing symbols in the Symbol table
        foreach (var kvp in _symbols)
        {
            AddToDictionary(symbolTable, kvp.Value, kvp.Value); // Symbol maps to itself
        }
        _globals["SymbolTable"] = symbolTable; // Also make it accessible as a global
        
        // 7: Process list - not created yet (will be needed for scheduling)
        // 8: ProcessorScheduler - not created yet (will be needed for process management)
        
        // Note: Other classes like SmallInteger, String, Array etc. are accessible
        // through the Smalltalk global dictionary and don't need to be in Special Objects
        
        // Store the special objects array in globals for VM access
        _globals["SpecialObjectsArray"] = specialObjects;
        _specialObjectsArray = specialObjects;
        
        LogColdStart(LogLevel.Information, "Created Special Objects Array with {Count} slots", specialObjectsSize);
    }
    
    private OOP CreateDictionary()
    {
        // Create a basic Dictionary
        // Dictionary has more complex structure than MethodDictionary
        // For simplicity, using same structure as MethodDictionary for now
        var dict = _memory.AllocateObject(_dictionaryClass, 2);
        _memory.StorePointer(dict, 0, OOP.FromSmallInteger(0)); // tally
        
        var array = _memory.AllocateObject(_arrayClass, 256); // Increased capacity to prevent "Dictionary full!" warnings
        for (int i = 0; i < 256; i++)
        {
            _memory.StorePointer(array, i, _nilObject);
        }
        _memory.StorePointer(dict, 1, array);
        
        return dict;
    }
    
    private void AddToDictionary(OOP dictOop, OOP key, OOP value)
    {
        // Similar to AddToMethodDictionary but for regular Dictionary
        var tallyOop = _memory.FetchPointer(dictOop, 0);
        var arrayOop = _memory.FetchPointer(dictOop, 1);
        
        if (arrayOop.IsNil || arrayOop.IsSmallInteger)
        {
            LogColdStart(LogLevel.Error, "Dictionary has invalid array!");
            return;
        }
        
        var tally = tallyOop.IsSmallInteger ? tallyOop.SmallIntegerValue : 0;
        
        // Simple linear search in the array
        for (int i = 0; i < 128; i++) // Using half the array capacity for key-value pairs (256/2)
        {
            var existingKey = _memory.FetchPointer(arrayOop, i * 2);
            if (existingKey.IsNil)
            {
                // Found empty slot
                _memory.StorePointer(arrayOop, i * 2, key);
                _memory.StorePointer(arrayOop, i * 2 + 1, value);
                
                // Update tally
                _memory.StorePointer(dictOop, 0, OOP.FromSmallInteger(tally + 1));
                return;
            }
            else if (existingKey.Value == key.Value)
            {
                // Replace existing value
                _memory.StorePointer(arrayOop, i * 2 + 1, value);
                return;
            }
        }
        
        LogColdStart(LogLevel.Warning, "Dictionary full!");
    }
    
    private OOP GetOrCreateGlobal(string name)
    {
        if (_globals.TryGetValue(name, out var global))
        {
            return global;
        }
        return _nilObject;
    }

    private OOP[]? GetObject(OOP oop)
    {
        // Helper to get object fields array
        // This would need proper implementation based on ObjectMemory
        return null;
    }
}

/// <summary>
/// Reads chunks from a Smalltalk source file.
/// </summary>
public sealed class ChunkReader
{
    private readonly string _filePath;
    private readonly ILogger _logger;
    
    public ChunkReader(string filePath, ILogger logger)
    {
        _filePath = filePath;
        _logger = logger;
    }
    
    public async Task<List<Chunk>> ReadAllChunksAsync()
    {
        var chunks = new List<Chunk>();
        var content = await File.ReadAllTextAsync(_filePath);
        var lines = content.Split('\n');
        var currentChunk = new StringBuilder();
        var lineNumber = 1;
        var chunkStartLine = 1;
        var inMethodBody = false;
        
        foreach (var line in lines)
        {
            // Detect chunk boundaries (! marks)
            if (line.TrimEnd() == "!")
            {
                if (currentChunk.Length > 0)
                {
                    var chunkContent = currentChunk.ToString();
                    var chunkType = DetermineChunkType(chunkContent, inMethodBody);
                    chunks.Add(new Chunk(chunkType, chunkContent, chunkStartLine));
                    currentChunk.Clear();
                    chunkStartLine = lineNumber + 1;
                    inMethodBody = false;
                }
            }
            else if (line.Contains("methodsFor:"))
            {
                inMethodBody = true;
                currentChunk.AppendLine(line);
            }
            else
            {
                currentChunk.AppendLine(line);
            }
            
            lineNumber++;
        }
        
        // Add final chunk if any
        if (currentChunk.Length > 0)
        {
            var chunkContent = currentChunk.ToString();
            var chunkType = DetermineChunkType(chunkContent, inMethodBody);
            chunks.Add(new Chunk(chunkType, chunkContent, chunkStartLine));
        }
        
        return chunks;
    }
    
    private ChunkType DetermineChunkType(string content, bool inMethodBody)
    {
        if (string.IsNullOrWhiteSpace(content))
            return ChunkType.Comment;
            
        if (inMethodBody)
            return ChunkType.MethodDefinition;
            
        if (content.Contains("subclass:") && content.Contains("instanceVariableNames:"))
            return ChunkType.ClassDefinition;
            
        if (content.TrimStart().StartsWith("\""))
            return ChunkType.Comment;
            
        return ChunkType.DoIt;
    }
    
}

public enum ChunkType
{
    ClassDefinition,
    MethodDefinition,
    DoIt,
    Comment
}

public sealed record Chunk(ChunkType Type, string Content, int LineNumber);