using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Accessor for runtime heap data structures.
/// Provides safe, read-only access to Smalltalk objects and metadata during VM execution.
/// Replaces GlobalRegistry and LayoutResolver for runtime use.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
/// </summary>
public class RuntimeHeapAccessor : IRuntimeHeapAccessor
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly VMMetadata metadata;

    // Caching layer (invalidated by primitive 89)
    private readonly RuntimeClassCache _classCache;

    // Cached CharacterTable OOP (invalidated by primitive 89)
    private SmalltalkOOP _characterTableCache = SmalltalkOOP.Nil;
    private bool _characterTableCacheValid = false;

    // Cached USTable OOP (invalidated by primitive 89)
    private SmalltalkOOP _ustableCache = SmalltalkOOP.Nil;
    private bool _ustableCacheValid = false;

    public RuntimeHeapAccessor(ISmalltalkHeap heapManager, VMMetadata metadata)
    {
        _heapManager = heapManager;
        this.metadata = metadata;
        _classCache = new RuntimeClassCache(this, heapManager);
    }

    /// <summary>
    /// Invalidates all runtime caches. Called by primitive 89 (flushCache).
    /// </summary>
    public virtual void InvalidateAllCaches()
    {
        _classCache.InvalidateCache();
        RuntimeSpecialObjects.Instance.InvalidateCache();
        _characterTableCacheValid = false;
        _ustableCacheValid = false;
    }

    /// <summary>
    /// Gets a class OOP by name, using cache if valid.
    /// </summary>
    public virtual SmalltalkOOP GetClass(string className)
    {
        return _classCache.GetClass(className);
    }

    /// <summary>
    /// Looks up a class in the Smalltalk global dictionary.
    /// This is the core heap-walking implementation.
    /// </summary>
    internal SmalltalkOOP LookupClassInHeap(string className)
    {
        // Step 1: Get Smalltalk dictionary from KnownObjects
        var smalltalkDictOOP = metadata.KnownObjects.Smalltalk;

        // Step 2: Intern the class name as a Symbol
        var symbolOOP = InternSymbol(className);

        // Step 3: Look up the symbol in the Dictionary
        return DictionaryLookup(smalltalkDictOOP, symbolOOP);
    }

    /// <summary>
    /// Interns a string as a Symbol in the Smalltalk heap.
    /// Searches USTable for existing symbol or returns nil if not found.
    /// </summary>
    public virtual SmalltalkOOP InternSymbol(string symbolName)
    {
        // CRITICAL: USTable is a CLASS VARIABLE of Symbol, not a global!
        // Symbol class definition: classVariableNames: 'SingleCharSymbols USTable'
        // We need to look up USTable from Symbol's classPool

        // CRITICAL: To avoid infinite recursion (InternSymbol → GetClass("Symbol") → InternSymbol),
        // we must look up Symbol class using direct byte comparison, not through InternSymbol
        var smalltalkDictOOP = metadata.KnownObjects.Smalltalk;
        var symbolClassOOP = LookupGlobalByName(smalltalkDictOOP, "Symbol");
        if (symbolClassOOP == metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during InternSymbol");
        }

        // Get Symbol's classPool (instance variable index 7 for Class)
        var classPoolOOP = _heapManager.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("Symbol classPool is nil during InternSymbol");
        }

        // Search classPool for USTable association
        var ustableOOP = LookupGlobalByName(classPoolOOP, "USTable");
        if (ustableOOP == metadata.KnownObjects.Nil)
        {
            throw new InvalidOperationException("USTable not found in Symbol classPool");
        }

        // USTable is an Array in the Smalltalk heap containing Symbol OOPs
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = metadata.KnownObjects.Nil;

        // Convert search string to bytes (Latin-1 encoding)
        byte[] searchBytes = Encoding.Latin1.GetBytes(symbolName);

        // Search through USTable (Array-of-Arrays structure for collision chaining)
        for (int i = 0; i < ustableSize; i++)
        {
            var bucketOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

            // Skip nil buckets (empty slots)
            if (bucketOOP == nilOOP)
                continue;

            // Bucket is an Array of Symbols - iterate through it
            int bucketSize = _heapManager.GetIndexedSize(bucketOOP);
            for (int k = 0; k < bucketSize; k++)
            {
                var symbolOOP = _heapManager.GetIndexedOOP(bucketOOP, k);

                // Skip nil slots
                if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                    continue;

                // Compare Symbol bytes in heap
                int symbolLength = _heapManager.GetIndexedSize(symbolOOP);
                if (symbolLength == searchBytes.Length)
                {
                    bool match = true;
                    for (int j = 0; j < searchBytes.Length; j++)
                    {
                        byte symbolByte = _heapManager.GetIndexedByte(symbolOOP, j);
                        if (symbolByte != searchBytes[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                        return symbolOOP;
                }
            }
        }

        return nilOOP;  // Symbol not found
    }

    /// <summary>
    /// Looks up a global variable by name using linear search with byte comparison.
    /// Used to break the circular dependency when looking up USTable.
    /// CRITICAL: Cannot use hash-based lookup because Symbol>>hash depends on OOP, not string content!
    /// </summary>
    public virtual SmalltalkOOP LookupGlobalByName(SmalltalkOOP dictOOP, string globalName)
    {
        byte[] searchBytes = Encoding.Latin1.GetBytes(globalName);
        int size = _heapManager.GetIndexedSize(dictOOP);

        // Linear search through entire dictionary (cannot use hash - Symbol>>hash is OOP-based!)
        for (int i = 0; i < size; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(dictOOP, i);

            // Skip nil slots
            if (associationOOP == metadata.KnownObjects.Nil || associationOOP.IsSmallInteger)
                continue;

            // Get the key from the Association (instance variable 0)
            var keyOOP = _heapManager.GetInstanceVariable(associationOOP, 0);
            if (keyOOP == metadata.KnownObjects.Nil || keyOOP.IsSmallInteger)
                continue;

            // Compare Symbol bytes directly
            int symbolLength = _heapManager.GetIndexedSize(keyOOP);
            if (symbolLength == searchBytes.Length)
            {
                bool match = true;
                for (int j = 0; j < searchBytes.Length; j++)
                {
                    byte symbolByte = _heapManager.GetIndexedByte(keyOOP, j);
                    if (symbolByte != searchBytes[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    // Return the value from the Association (instance variable 1)
                    var valueOOP = _heapManager.GetInstanceVariable(associationOOP, 1);

                    return valueOOP;
                }
            }
        }

        return metadata.KnownObjects.Nil;  // Not found
    }

    /// <summary>
    /// Looks up a key in a Smalltalk Dictionary object.
    /// Dictionary is a PointerIndexed subclass of Set with no additional instance variables.
    /// It stores Association objects directly in indexed slots (NOT in separate keys/values arrays).
    /// </summary>
    private SmalltalkOOP DictionaryLookup(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
    {
        // Dictionary structure:
        //   Instance variable 0: tally (SmallInteger count of entries, inherited from Set)
        //   Indexed slots 0..N: Association objects (PointerIndexed)

        // Search through Dictionary's indexed slots for matching key
        int size = _heapManager.GetIndexedSize(dictOOP);
        var nilOOP = metadata.KnownObjects.Nil;

        for (int i = 0; i < size; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(dictOOP, i);

            // Skip nil slots (empty hash table slots)
            if (associationOOP == nilOOP || associationOOP.IsSmallInteger)
                continue;

            // Get the key from the Association (instance variable 0)
            var assocKeyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);

            if (assocKeyOOP == keyOOP)  // Identity comparison for Symbols
            {
                // Return the value from the Association (instance variable 1)
                return _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            }
        }

        return metadata.KnownObjects.Nil;  // Not found
    }

    /// <summary>
    /// Parses an instance variable string from a class.
    /// Instance variables are stored as a space-separated String in the heap.
    /// </summary>
    internal List<string> ParseInstanceVariableString(SmalltalkOOP instVarsOOP)
    {
        var result = new List<string>();

        if (instVarsOOP == metadata.KnownObjects.Nil)
            return result;

        // Read the String bytes from the heap
        int length = _heapManager.GetIndexedSize(instVarsOOP);
        if (length == 0)
            return result;

        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(instVarsOOP, i);
        }

        // Parse the space-separated string
        string instVarsString = Encoding.Latin1.GetString(bytes);
        string[] parts = instVarsString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        result.AddRange(parts);

        return result;
    }

    /// <summary>
    /// Builds complete field layout map for all classes by walking the Smalltalk heap.
    /// Reads ClassDescription.instanceVariables (instance variable 4) Array-of-Strings for each class,
    /// walks superclass chain to accumulate inherited fields, and returns complete layouts.
    ///
    /// This enables full runtime reflection and supports runtime class modification.
    /// Equivalent to Smalltalk's allInstVarNames method.
    /// </summary>
    public virtual Dictionary<string, List<string>> BuildFieldMap()
    {
        var fieldMap = new Dictionary<string, List<string>>();

        // Get Smalltalk dictionary from KnownObjects
        var smalltalkDictOOP = metadata.KnownObjects.Smalltalk;

        // Iterate through all global bindings (Associations in SystemDictionary)
        int dictSize = _heapManager.GetIndexedSize(smalltalkDictOOP);
        var nilOOP = metadata.KnownObjects.Nil;

        for (int i = 0; i < dictSize; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(smalltalkDictOOP, i);
            if (associationOOP == nilOOP || associationOOP.IsSmallInteger)
                continue;

            // Get the value from Association (instance variable 1)
            var valueOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            if (valueOOP == nilOOP || valueOOP.IsSmallInteger)
                continue;

            // Check if it's a class object (has ClassDescription as ancestor)
            // Classes have methodDict field (instance variable 1), which is typically a Dictionary
            try
            {
                var methodDictOOP = _heapManager.GetInstanceVariable(valueOOP, 1);
                if (methodDictOOP == nilOOP || methodDictOOP.IsSmallInteger)
                    continue;  // Not a class

                // Get class name from Association key (instance variable 0 - a Symbol)
                var keySymbolOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
                string className = ReadSymbolName(keySymbolOOP);

                // Build complete field layout by walking inheritance chain
                var fields = AccumulateInstanceVariableNames(valueOOP);

                // Store in map using "class:ClassName" format
                fieldMap[$"class:{className}"] = fields;
            }
            catch
            {
                // Not a class object, skip
                continue;
            }
        }

        return fieldMap;
    }

    /// <summary>
    /// Accumulates instance variable names by walking the superclass chain from root to leaf.
    /// Reads ClassDescription.instanceVariables (instance variable 4) for each class in the hierarchy.
    /// </summary>
    private List<string> AccumulateInstanceVariableNames(SmalltalkOOP classOOP)
    {
        var fields = new List<string>();
        var nilOOP = metadata.KnownObjects.Nil;

        // Walk superclass chain from current class to root (Object), pushing onto stack
        var superclassChain = new Stack<SmalltalkOOP>();
        var currentOOP = classOOP;

        while (currentOOP != nilOOP && !currentOOP.IsSmallInteger)
        {
            superclassChain.Push(currentOOP);
            // Read superclass field (instance variable 0 from Behavior)
            currentOOP = _heapManager.GetInstanceVariable(currentOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
        }

        // Process from root to leaf, accumulating instance variables
        while (superclassChain.Count > 0)
        {
            var oop = superclassChain.Pop();

            // Read ClassDescription.instanceVariables field (instance variable 4)
            // This is an Array of String objects containing the class's own instance variables
            var instVarsArrayOOP = _heapManager.GetInstanceVariable(oop, 4);

            if (instVarsArrayOOP == nilOOP || instVarsArrayOOP.IsSmallInteger)
                continue;  // No instance variables for this class

            // instVarsArrayOOP is an Array - read its indexed slots
            int arraySize = _heapManager.GetIndexedSize(instVarsArrayOOP);
            for (int i = 0; i < arraySize; i++)
            {
                var stringOOP = _heapManager.GetIndexedOOP(instVarsArrayOOP, i);
                if (stringOOP == nilOOP || stringOOP.IsSmallInteger)
                    continue;

                // Convert String object to C# string
                string fieldName = ReadStringValue(stringOOP);
                fields.Add(fieldName);
            }
        }

        return fields;
    }

    /// <summary>
    /// Reads a Symbol object and returns its name as a C# string.
    /// </summary>
    private string ReadSymbolName(SmalltalkOOP symbolOOP)
    {
        int length = _heapManager.GetIndexedSize(symbolOOP);
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(symbolOOP, i);
        }
        return Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// Reads a String object and returns its value as a C# string.
    /// </summary>
    private string ReadStringValue(SmalltalkOOP stringOOP)
    {
        int length = _heapManager.GetIndexedSize(stringOOP);
        var bytes = new byte[length];
        for (int i = 0; i < length; i++)
        {
            bytes[i] = _heapManager.GetIndexedByte(stringOOP, i);
        }
        return Encoding.Latin1.GetString(bytes);
    }

    /// <summary>
    /// Gets the field index for a named field in a class by walking the heap.
    /// This is used by primitives at runtime - the heap is the source of truth.
    /// </summary>
    /// <param name="classOOP">The class OOP to query</param>
    /// <param name="fieldName">The field name to find</param>
    /// <returns>The zero-based field index, or -1 if not found</returns>
    public virtual int GetFieldIndex(SmalltalkOOP classOOP, string fieldName)
    {
        // Build the field list for this class by walking the heap
        var fields = AccumulateInstanceVariableNames(classOOP);

        // Find the index of the requested field
        return fields.IndexOf(fieldName);
    }

    /// <summary>
    /// Looks up a class variable from a class's classPool dictionary.
    /// Runtime-safe: uses only heap access, no cold start structures.
    /// </summary>
    /// <param name="className">Name of the class that owns the class variable</param>
    /// <param name="varName">Name of the class variable</param>
    /// <returns>The value OOP of the class variable, or Nil if not found</returns>
    public virtual SmalltalkOOP LookupClassVariable(string className, string varName)
    {
        // OPTIMIZATION: Cache CharacterTable since it's accessed on every String>>at: call
        if (className == "Character" && varName == "CharacterTable")
        {
            if (_characterTableCacheValid)
                return _characterTableCache;

            // Cache miss - look it up and cache the result
            var characterTableOOP = LookupClassVariableUncached(className, varName);
            if (characterTableOOP != metadata.KnownObjects.Nil)
            {
                _characterTableCache = characterTableOOP;
                _characterTableCacheValid = true;
            }
            return characterTableOOP;
        }

        // OPTIMIZATION: Cache USTable since it's accessed frequently by InternSymbol
        if (className == "Symbol" && varName == "USTable")
        {
            if (_ustableCacheValid)
                return _ustableCache;

            // Cache miss - look it up and cache the result
            var ustableOOP = LookupClassVariableUncached(className, varName);
            if (ustableOOP != metadata.KnownObjects.Nil)
            {
                _ustableCache = ustableOOP;
                _ustableCacheValid = true;
            }
            return ustableOOP;
        }

        // For all other class variables, lookup without caching
        return LookupClassVariableUncached(className, varName);
    }

    /// <summary>
    /// Looks up a class variable without using the cache.
    /// Used internally by LookupClassVariable.
    /// </summary>
    private SmalltalkOOP LookupClassVariableUncached(string className, string varName)
    {
        // Optimize for well-known classes using VMMetadata
        SmalltalkOOP classOOP;
        if (className == "Symbol")
        {
            classOOP = metadata.SymbolClassOOP;
        }
        else
        {
            // For other classes, look up in Smalltalk dictionary
            classOOP = GetClass(className);
            if (classOOP == metadata.KnownObjects.Nil)
            {
                Console.WriteLine($"[LookupClassVariable] ERROR: Class {className} not found!");
                return metadata.KnownObjects.Nil;
            }
        }

        // Get classPool (instance variable 7 of Class)
        var classPoolOOP = _heapManager.GetInstanceVariable(classOOP, 7);
        if (classPoolOOP == metadata.KnownObjects.Nil)
        {
            Console.WriteLine($"[LookupClassVariable] ERROR: classPool is nil for {className}!");
            return metadata.KnownObjects.Nil;
        }

        // Find the symbol OOP for the variable name
        var varSymbolOOP = InternSymbol(varName);
        if (varSymbolOOP == metadata.KnownObjects.Nil)
        {
            Console.WriteLine($"[LookupClassVariable] ERROR: Symbol '{varName}' not found!");
            return metadata.KnownObjects.Nil;
        }

        // Look up the symbol in the classPool dictionary
        var result = DictionaryLookup(classPoolOOP, varSymbolOOP);
        if (result == metadata.KnownObjects.Nil)
        {
            Console.WriteLine($"[LookupClassVariable] ERROR: {className}>>{varName} not found in classPool!");
        }
        return result;
    }
}