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

    /// <summary>
    /// Hardcoded bytecode for Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:
    /// This is the minimal bytecode needed to create new classes.
    /// </summary>
    private static readonly byte[] SubclassCreationBytecode = 
    [
        // Simplified bytecode - actual implementation would be more complex
        0x70, // pushReceiver (self - the superclass)
        0x10, // pushTemp: 0 (className)
        0x11, // pushTemp: 1 (instanceVars) 
        0x12, // pushTemp: 2 (classVars)
        0x13, // pushTemp: 3 (poolDicts)
        0x14, // pushTemp: 4 (category)
        0xE0, // send: primitiveCreateClass with 5 args (would be a primitive)
        0x7C  // returnTop
    ];
    
    /// <summary>
    /// Hardcoded bytecode for Behavior>>compile:classified:
    /// This is the minimal bytecode needed to compile methods.
    /// </summary>
    private static readonly byte[] CompileMethodBytecode = 
    [
        // Simplified bytecode for method compilation
        0x70, // pushReceiver (self - the class)
        0x10, // pushTemp: 0 (sourceCode)
        0x11, // pushTemp: 1 (category)
        0xE1, // send: primitiveCompileMethod with 2 args (would be a primitive)
        0x7C  // returnTop
    ];

    public ColdStartBootstrap(IObjectMemory memory, ILogger<ColdStartBootstrap> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

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
        
        _logger.LogInformation("[COLD START] Bootstrap complete!");
    }

    /// <summary>
    /// Phase 1: Creates the minimal class hierarchy needed for bootstrap.
    /// Only creates the absolute minimum classes needed to execute class creation and method compilation.
    /// </summary>
    private void CreateMinimalClassHierarchy()
    {
        _logger.LogInformation("[COLD START] Phase 1: Creating minimal class hierarchy...");
        
        // Step 1: Allocate all class objects first (with temporary class pointers)
        // We use a temporary class OOP that will be fixed up later
        var tempClassOop = OOP.FromSmallInteger(999999); // Temporary marker
        
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
        SetClass(_objectClass, objectMetaclass);
        SetClass(_behaviorClass, behaviorMetaclass);
        SetClass(_classDescriptionClass, classDescriptionMetaclass);
        SetClass(_metaclassClass, metaclassMetaclass);
        SetClass(_classClass, classMetaclass);
        
        // Step 4: Set up the inheritance chain
        // Object has no superclass (nil)
        SetSuperclass(_objectClass, OOP.Nil);
        
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
        
        // Step 6: Register classes for lookup
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
        
        _logger.LogInformation("[COLD START] Created {Count} core classes", _classes.Count);
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
        InstallPrimitiveMethod(_objectClass, "basicAt:", 60, 1);         // Field access
        InstallPrimitiveMethod(_objectClass, "basicAt:put:", 61, 2);     // Field store  
        InstallPrimitiveMethod(_objectClass, "basicSize", 62, 0);        // Size
        
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
        var subclassMethod = CreateCompiledMethod(
            SubclassCreationBytecode,
            Array.Empty<OOP>(), // No literals needed for primitive
            5, // 5 arguments
            0, // No temporaries
            10 // Stack size
        );
        InstallMethod(_classClass, "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:", subclassMethod);
        
        // Install Behavior>>compile:classified:
        var compileMethod = CreateCompiledMethod(
            CompileMethodBytecode,
            Array.Empty<OOP>(), // No literals needed for primitive
            2, // 2 arguments
            0, // No temporaries
            8  // Stack size
        );
        InstallMethod(_behaviorClass, "compile:classified:", compileMethod);
        
        // Also install some helper methods that are needed
        InstallNonPrimitiveMethod(_objectClass, "isNil", new byte[] { 0x79, 0x7C }); // pushFalse, returnTop
        InstallNonPrimitiveMethod(_objectClass, "notNil", new byte[] { 0x78, 0x7C }); // pushTrue, returnTop
        InstallNonPrimitiveMethod(_objectClass, "yourself", new byte[] { 0x70, 0x7C }); // pushReceiver, returnTop
        
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
        // In the object header, set the class pointer
        // This depends on the ObjectMemory implementation
        // For now, we'll store it as the first field (field 0)
        var obj = GetObject(objectOop);
        if (obj != null && obj.Length > 0)
        {
            obj[0] = classOop;
        }
    }

    private void SetSuperclass(OOP classOop, OOP superclassOop)
    {
        // Superclass is field 0 in Behavior per Blue Book
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

    private void InstallNonPrimitiveMethod(OOP classOop, string selector, byte[] bytecode)
    {
        var method = CreateCompiledMethod(bytecode, Array.Empty<OOP>(), 0, 0, 2);
        InstallMethod(classOop, selector, method);
    }

    private OOP CreatePrimitiveMethod(int primitiveIndex, int numArgs)
    {
        // Minimal bytecode: just return self if primitive fails
        var bytecodes = new byte[] { 0x70, 0x7C }; // pushReceiver, returnTop
        return CreateCompiledMethod(bytecodes, Array.Empty<OOP>(), numArgs, 0, 1, primitiveIndex);
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
        var methodDict = _memory.FetchPointer(classOop, 2); // Field 2 in Behavior is methodDict
        
        if (methodDict.IsNil)
        {
            // Create new method dictionary
            methodDict = CreateMethodDictionary();
            _memory.StorePointer(classOop, 2, methodDict);
        }
        
        // Add selector->method to dictionary
        var selectorSymbol = GetOrCreateSymbol(selector);
        AddToMethodDictionary(methodDict, selectorSymbol, methodOop);
    }

    private OOP CreateMethodDictionary()
    {
        // Create a simple dictionary with initial capacity
        // For now, use an array-based implementation
        var dict = _memory.AllocateObject(_methodDictionaryClass, 64); // 32 key-value pairs
        return dict;
    }

    private void AddToMethodDictionary(OOP dictOop, OOP selectorOop, OOP methodOop)
    {
        // Simple linear search for now
        // In a real implementation, this would use hashing
        
        for (int i = 0; i < 32; i++)
        {
            var key = _memory.FetchPointer(dictOop, i * 2);
            if (key.IsNil)
            {
                // Found empty slot
                _memory.StorePointer(dictOop, i * 2, selectorOop);
                _memory.StorePointer(dictOop, i * 2 + 1, methodOop);
                return;
            }
            else if (key.Value == selectorOop.Value)
            {
                // Replace existing method
                _memory.StorePointer(dictOop, i * 2 + 1, methodOop);
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
        
        // Create new symbol
        var bytes = Encoding.UTF8.GetBytes(str);
        symbol = _memory.AllocateByteObject(_symbolClass, bytes.Length);
        
        for (int i = 0; i < bytes.Length; i++)
        {
            _memory.StoreByte(symbol, i, bytes[i]);
        }
        
        _symbols[str] = symbol;
        return symbol;
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