using System.Text;
using Core;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter.Bootstrap;

/// <summary>
/// Minimal cold start bootstrap that hardcodes only the essential class hierarchy
/// and critical methods, then reads Smalltalk source to build the rest.
/// </summary>
public sealed class ColdStartBootstrap
{
    private readonly IObjectMemory _memory;
    private readonly ILogger<ColdStartBootstrap> _logger;
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
    private OOP _smallIntegerClass;
    private OOP _characterClass;
    private OOP _trueClass;
    private OOP _falseClass;
    private OOP _undefinedObjectClass;
    
    // Singleton objects
    private OOP _nilObject;
    private OOP _trueObject;
    private OOP _falseObject;
    private OOP _specialObjectsArray;

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

    public ColdStartBootstrap(IObjectMemory memory, ILogger<ColdStartBootstrap> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    
    /// <summary>
    /// Gets all registered classes after bootstrap.
    /// </summary>
    public IReadOnlyDictionary<string, OOP> Classes => _classes;

    /// <summary>
    /// Performs the cold start bootstrap.
    /// </summary>
    public async Task BootstrapAsync(string smalltalkSourcePath)
    {
        _logger.LogInformation("[COLD START] Beginning minimal bootstrap...");
        
        // Phase 1: Create the minimal class hierarchy
        CreateMinimalClassHierarchy();
        
        // Phase 2: Install critical primitive methods
        InstallPrimitiveMethods();
        
        // Phase 3: Install hardcoded bootstrap methods
        InstallBootstrapMethods();
        
        // Phase 4: Read and execute Smalltalk source chunks
        await ProcessSmalltalkSourceAsync(smalltalkSourcePath);
        
        // Phase 5: Register Special Objects Array as GC root and test GC
        // RegisterGCRootsAndTestGC(); // Temporarily commented due to strange scoping issue
        
        // Inline the GC test for now
        _logger.LogInformation("[COLD START] Phase 5: Testing garbage collection...");
        
        // Set the Special Objects Array as the sole GC root
        if (_specialObjectsArray.Value != 0) // Check if it's been set
        {
            _memory.SetSpecialObjectsArray(_specialObjectsArray);
            _logger.LogInformation("[GC] Set Special Objects Array as the sole GC root");
        }
        
        // Run garbage collection (will display detailed statistics)
        _logger.LogInformation("[GC] Running garbage collection after cold start bootstrap...");
        _memory.CollectGarbage();
        
        // Check results
        var (objectsCollected, bytesFreed) = _memory.GetLastGCStats();
        if (objectsCollected == 0)
        {
            _logger.LogInformation("[GC] ✓ SUCCESS: All bootstrap objects are properly rooted!");
        }
        else
        {
            _logger.LogWarning("[GC] ⚠ WARNING: {Count} objects were collected - some bootstrap objects may not be properly rooted!", objectsCollected);
        }
        
        _logger.LogInformation("[COLD START] Bootstrap complete!");
    }

    /// <summary>
    /// Phase 1: Creates the minimal class hierarchy needed for bootstrap.
    /// Only creates the absolute minimum classes needed to execute class creation and method compilation.
    /// </summary>
    private void CreateMinimalClassHierarchy()
    {
        _logger.LogInformation("[COLD START] Phase 1: Creating minimal class hierarchy...");
        
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
        
        _logger.LogInformation("[COLD START] Created singleton placeholders: nil={0}, true={1}, false={2}", 
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
        _smallIntegerClass = _memory.AllocateObject(tempClassOop, 8);
        _characterClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Boolean classes  
        _trueClass = _memory.AllocateObject(tempClassOop, 8);
        _falseClass = _memory.AllocateObject(tempClassOop, 8);
        _undefinedObjectClass = _memory.AllocateObject(tempClassOop, 8);
        
        // Step 2: Create metaclasses for each class
        // Metaclass objects have 6 fields per Blue Book
        var objectMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var behaviorMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var classDescriptionMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var metaclassMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        var classMetaclass = _memory.AllocateObject(_metaclassClass, 6);
        
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
        
        // Step 5: Set up other essential classes
        SetClass(_compiledMethodClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_compiledMethodClass, _objectClass);
        
        SetClass(_methodDictionaryClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_methodDictionaryClass, _dictionaryClass);
        
        SetClass(_stringClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_stringClass, _byteArrayClass);
        
        SetClass(_symbolClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_symbolClass, _stringClass);
        
        SetClass(_arrayClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_arrayClass, _objectClass);
        
        SetClass(_byteArrayClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_byteArrayClass, _objectClass);
        
        SetClass(_dictionaryClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_dictionaryClass, _objectClass);
        
        SetClass(_associationClass, _memory.AllocateObject(_metaclassClass, 6));
        SetSuperclass(_associationClass, _objectClass);
        
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
        RegisterClass("SmallInteger", _smallIntegerClass);
        RegisterClass("Character", _characterClass);
        RegisterClass("True", _trueClass);
        RegisterClass("False", _falseClass);
        RegisterClass("UndefinedObject", _undefinedObjectClass);
        
        // Step 7.5: Singleton objects were already created in Phase 1
        // They're at indices 0, 1, 2 as required
        
        // Step 8: Create the Smalltalk global dictionary
        CreateSmalltalkGlobal();
        
        // Step 9: Create the Special Objects Array
        CreateSpecialObjectsArray();
        
        _logger.LogInformation("[COLD START] Created {Count} core classes", _classes.Count);
    }

    /// <summary>
    /// Initializes method dictionaries for all classes that were created.
    /// Each class needs a properly structured method dictionary even if empty.
    /// </summary>
    private void InitializeMethodDictionaries()
    {
        _logger.LogInformation("[COLD START] Initializing method dictionaries for all classes...");
        
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
        InitializeClassMethodDictionary(_smallIntegerClass);
        InitializeClassMethodDictionary(_characterClass);
        InitializeClassMethodDictionary(_trueClass);
        InitializeClassMethodDictionary(_falseClass);
        InitializeClassMethodDictionary(_undefinedObjectClass);
        
        // Also initialize method dictionaries for metaclasses
        var classes = new[] { _objectClass, _behaviorClass, _classDescriptionClass, 
            _metaclassClass, _classClass, _compiledMethodClass, _methodDictionaryClass,
            _methodContextClass, _blockContextClass, _stringClass, _symbolClass,
            _arrayClass, _byteArrayClass, _associationClass, _dictionaryClass,
            _smallIntegerClass, _characterClass, _trueClass, _falseClass, _undefinedObjectClass };
            
        foreach (var classOop in classes)
        {
            var metaclassOop = _memory.FetchClassOf(classOop);
            if (!metaclassOop.IsNil && !metaclassOop.IsSmallInteger)
            {
                InitializeClassMethodDictionary(metaclassOop);
            }
        }
        
        _logger.LogInformation("[COLD START] Method dictionaries initialized");
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
        _logger.LogInformation("[COLD START] Phase 2: Installing primitive methods...");
        
        // Object primitives
        InstallPrimitiveMethod(_objectClass, "class", 111, 0);           // Get class
        InstallPrimitiveMethod(_objectClass, "==", 110, 1);              // Identity
        InstallPrimitiveMethod(_objectClass, "=", 110, 1);               // Equality (defaults to identity for Object)
        InstallPrimitiveMethod(_objectClass, "basicAt:", 60, 1);         // Field access
        InstallPrimitiveMethod(_objectClass, "basicAt:put:", 61, 2);     // Field store
        
        // Initialize method - no primitive, just returns self
        InstallCustomPrimitiveMethod(_objectClass, "initialize", 0, 0,
            new byte[] { 0x70, 0x7C }, // pushReceiver (112), returnTop (124)
            Array.Empty<OOP>());
        
        // basicSize - special case: returns 0 if primitive fails
        InstallCustomPrimitiveMethod(_objectClass, "basicSize", 62, 0, 
            new byte[] { 0x75, 0x7C }, // pushZero (117), returnTop (124)
            Array.Empty<OOP>());
        
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
        
        _logger.LogInformation("[COLD START] Installed essential primitive methods");
    }

    /// <summary>
    /// Phase 3: Installs hardcoded bootstrap methods.
    /// These are the critical methods for class creation and method compilation.
    /// </summary>
    private void InstallBootstrapMethods()
    {
        _logger.LogInformation("[COLD START] Phase 3: Installing bootstrap methods...");
        
        // Install Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
        // Literals from the compiled Smalltalk-80.st:
        // Note: We need to create the Association for Smalltalk and resolve it later
        var smalltalkSymbol = GetOrCreateSymbol("Smalltalk");
        var smalltalkAssoc = CreateAssociation(smalltalkSymbol, GetOrCreateGlobal("Smalltalk"));
        
        var subclassLiterals = new OOP[]
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
        var compileLiterals = new OOP[]
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
        InstallNonPrimitiveMethod(_objectClass, "isNil", new byte[] { 0x72, 0x7C }); // pushFalse (114), returnTop (124)
        
        // notNil: ^true
        InstallNonPrimitiveMethod(_objectClass, "notNil", new byte[] { 0x71, 0x7C }); // pushTrue (113), returnTop (124)
        
        // yourself: ^self
        InstallNonPrimitiveMethod(_objectClass, "yourself", new byte[] { 0x78 }); // returnReceiver (120)
        
        _logger.LogInformation("[COLD START] Installed subclass creation and method compilation methods");
    }

    /// <summary>
    /// Phase 4: Processes Smalltalk source file.
    /// </summary>
    private async Task ProcessSmalltalkSourceAsync(string sourcePath)
    {
        _logger.LogInformation("[COLD START] Phase 4: Processing Smalltalk source from {Path}...", sourcePath);
        
        if (!File.Exists(sourcePath))
        {
            throw new FileNotFoundException($"Smalltalk source file not found: {sourcePath}");
        }
        
        var reader = new ChunkReader(sourcePath, _logger);
        var chunks = await reader.ReadAllChunksAsync();
        
        _logger.LogInformation("[COLD START] Read {Count} chunks from source file", chunks.Count);
        
        foreach (var chunk in chunks)
        {
            try
            {
                ProcessChunk(chunk);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[COLD START] Failed to process chunk at line {Line}", chunk.LineNumber);
                // Continue with next chunk
            }
        }
        
        // Process any deferred chunks
        ProcessDeferredChunks();
        
        _logger.LogInformation("[COLD START] Source processing complete");
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
        _logger.LogDebug("[COLD START] Creating class: {Content}", 
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Parse class definition and send subclass:instanceVariableNames:... message
        // This will use the hardcoded subclass creation method
    }

    private void ProcessMethodDefinition(Chunk chunk)
    {
        _logger.LogDebug("[COLD START] Compiling method: {Content}",
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Parse method and send compile:classified: message
        // This will use the hardcoded compile method
    }

    private void ProcessDoItExpression(Chunk chunk)
    {
        _logger.LogDebug("[COLD START] Executing expression: {Content}",
            chunk.Content.Length > 50 ? chunk.Content[..50] + "..." : chunk.Content);
        
        // TODO: Compile and execute DoIt expression
    }

    private void ProcessDeferredChunks()
    {
        if (_pendingChunks.Count > 0)
        {
            _logger.LogInformation("[COLD START] Processing {Count} deferred chunks", _pendingChunks.Count);
            
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

    private void RegisterClass(string name, OOP classOop)
    {
        _classes[name] = classOop;
        _logger.LogTrace("[COLD START] Registered class {Name} -> {OOP}", name, classOop);
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
        var method = CreateCompiledMethod(bytecode, Array.Empty<OOP>(), 0, 0, 2);
        InstallMethod(classOop, selector, method);
    }

    private OOP CreatePrimitiveMethod(int primitiveIndex, int numArgs)
    {
        // Default fallback: self primitiveFailed
        // Bytecode: pushReceiver (112), send literal 0 with 0 args (208), returnTop (124)
        var bytecodes = new byte[] { 0x70, 0xD0, 0x7C }; // pushReceiver, sendLiteralSelector0Args0, returnTop
        
        // Need #primitiveFailed as literal
        var literals = new OOP[] { GetOrCreateSymbol("primitiveFailed") };
        
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
            _logger.LogTrace("[COLD START] Created method dictionary for class {Class}", classOop);
        }
        
        // Add selector->method to dictionary
        var selectorSymbol = GetOrCreateSymbol(selector);
        AddToMethodDictionary(methodDict, selectorSymbol, methodOop);
        _logger.LogTrace("[COLD START] Installed method {Selector} in class {Class}", selector, classOop);
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
            _logger.LogError("[COLD START] Method dictionary has invalid array!");
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
        
        _logger.LogWarning("[COLD START] Method dictionary full!");
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
        
        _logger.LogTrace("[COLD START] Created symbol #{String} -> {OOP}", str, symbol);
        
        _symbols[str] = symbol;
        
        return symbol;
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
        
        _logger.LogInformation("[COLD START] Created singleton objects (nil={0}, true={1}, false={2})", 
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
        
        _logger.LogInformation("[COLD START] Created Smalltalk global dictionary with {Count} entries", _classes.Count + 1);
    }
    
    private void CreateSpecialObjectsArray()
    {
        // Create the Special Objects Array
        // This array contains well-known objects that the VM needs quick access to
        // Following the old interpreter's specification (exactly 9 slots):
        // 0: nil object
        // 1: false object
        // 2: true object
        // 3: Behavior class
        // 4: Metaclass class
        // 5: Smalltalk global dictionary (SystemDictionary instance)
        // 6: Symbol table (to be added)
        // 7: Process list (to be added)
        // 8: ProcessorScheduler (to be added)
        
        const int specialObjectsSize = 9; // Only 9 slots needed per old interpreter
        var specialObjects = _memory.AllocateObject(_arrayClass, specialObjectsSize);
        
        // Initialize all slots to the nil object first
        for (int i = 0; i < specialObjectsSize; i++)
        {
            _memory.StorePointer(specialObjects, i, _nilObject);
        }
        
        // Populate the essential objects following the old interpreter's SpecialObjects indices
        // CRITICAL: Indices 3-5 MUST contain Behavior, Metaclass, and Smalltalk for proper GC!
        _memory.StorePointer(specialObjects, 0, _nilObject);           // nil (the actual object)
        _memory.StorePointer(specialObjects, 1, _falseObject);         // false
        _memory.StorePointer(specialObjects, 2, _trueObject);          // true
        _memory.StorePointer(specialObjects, 3, _behaviorClass);       // Behavior class (CRITICAL GC ROOT!)
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
        
        _logger.LogInformation("[COLD START] Created Special Objects Array with {Count} slots", specialObjectsSize);
    }
    
    private OOP CreateDictionary()
    {
        // Create a basic Dictionary
        // Dictionary has more complex structure than MethodDictionary
        // For simplicity, using same structure as MethodDictionary for now
        var dict = _memory.AllocateObject(_dictionaryClass, 2);
        _memory.StorePointer(dict, 0, OOP.FromSmallInteger(0)); // tally
        
        var array = _memory.AllocateObject(_arrayClass, 128); // Larger capacity for globals
        for (int i = 0; i < 128; i++)
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
            _logger.LogError("[COLD START] Dictionary has invalid array!");
            return;
        }
        
        var tally = tallyOop.IsSmallInteger ? tallyOop.SmallIntegerValue : 0;
        
        // Simple linear search in the array
        for (int i = 0; i < 64; i++) // Using half the array capacity for key-value pairs
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
        
        _logger.LogWarning("[COLD START] Dictionary full!");
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