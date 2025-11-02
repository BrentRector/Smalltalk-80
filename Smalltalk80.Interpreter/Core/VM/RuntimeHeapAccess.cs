using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides runtime access to Smalltalk heap data structures.
/// This class replaces GlobalRegistry and LayoutResolver for runtime use.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
/// </summary>
public class RuntimeHeapAccess
{
    private readonly ISmalltalkHeapManager _heapManager;

    // Caching layer (invalidated by primitive 89)
    private readonly RuntimeClassCache _classCache;
    private readonly RuntimeFieldLayoutCache _fieldLayoutCache;

    public RuntimeHeapAccess(ISmalltalkHeapManager heapManager)
    {
        _heapManager = heapManager;
        _classCache = new RuntimeClassCache(this, heapManager);
        _fieldLayoutCache = new RuntimeFieldLayoutCache(this, heapManager);
    }

    /// <summary>
    /// Invalidates all runtime caches. Called by primitive 89 (flushCache).
    /// </summary>
    public void InvalidateAllCaches()
    {
        _classCache.InvalidateCache();
        _fieldLayoutCache.InvalidateCache();
        RuntimeSpecialObjects.Instance.InvalidateCache();
    }

    /// <summary>
    /// Gets a class OOP by name, using cache if valid.
    /// </summary>
    public SmalltalkOOP GetClass(string className)
    {
        return _classCache.GetClass(className);
    }

    /// <summary>
    /// Gets the field index for a named instance variable in a class.
    /// </summary>
    public int GetFieldIndex(SmalltalkOOP classOOP, string fieldName)
    {
        return _fieldLayoutCache.GetFieldIndex(classOOP, fieldName);
    }

    /// <summary>
    /// Gets the total instance variable count for a class (including inherited).
    /// </summary>
    public int GetInstanceVariableCount(SmalltalkOOP classOOP)
    {
        return _fieldLayoutCache.GetInstanceVariableCount(classOOP);
    }

    /// <summary>
    /// Looks up a class in the Smalltalk global dictionary.
    /// This is the core heap-walking implementation.
    /// </summary>
    internal SmalltalkOOP LookupClassInHeap(string className)
    {
        // Step 1: Get Smalltalk dictionary from SpecialObjectsArray
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;

        // Step 2: Intern the class name as a Symbol
        var symbolOOP = InternSymbol(className);

        // Step 3: Look up the symbol in the Dictionary
        return DictionaryLookup(smalltalkDictOOP, symbolOOP);
    }

    /// <summary>
    /// Interns a string as a Symbol in the Smalltalk heap.
    /// Searches USTable for existing symbol or returns nil if not found.
    /// </summary>
    public SmalltalkOOP InternSymbol(string symbolName)
    {
        // Get USTable from Smalltalk dictionary (it's stored under 'USTable' global)
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;

        // We need USTable, but it's stored as a global variable
        // For now, do a simple global lookup by walking the dictionary
        // (This is a bootstrap issue - USTable is needed to intern symbols, but we need symbols to look up USTable!)
        // Solution: Use direct byte comparison for the hardcoded "USTable" string
        var ustableOOP = LookupGlobalByName(smalltalkDictOOP, "USTable");
        if (ustableOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("USTable not found in Smalltalk globals");
        }

        // USTable is an Array in the Smalltalk heap containing Symbol OOPs
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

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
    /// Looks up a global variable by name using byte comparison (no symbol interning needed).
    /// Used to break the circular dependency when looking up USTable.
    /// CRITICAL: SystemDictionary is an Array of Associations, NOT a Dictionary with keys/values arrays!
    /// </summary>
    public SmalltalkOOP LookupGlobalByName(SmalltalkOOP dictOOP, string globalName)
    {
        byte[] searchBytes = Encoding.Latin1.GetBytes(globalName);

        // SystemDictionary structure: Array of Association objects
        // Each Association has: slot 0 = key (Symbol), slot 1 = value (Object)
        int size = _heapManager.GetIndexedSize(dictOOP);
        for (int i = 0; i < size; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(dictOOP, i);
            if (associationOOP == SpecialObjectsArray.Instance.Nil || associationOOP.IsSmallInteger)
                continue;

            // Get the key from the Association (slot 0)
            var keyOOP = _heapManager.GetInstanceVariable(associationOOP, 0);
            if (keyOOP == SpecialObjectsArray.Instance.Nil || keyOOP.IsSmallInteger)
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
                    // Return the value from the Association (slot 1)
                    return _heapManager.GetInstanceVariable(associationOOP, 1);
                }
            }
        }

        return SpecialObjectsArray.Instance.Nil;  // Not found
    }

    /// <summary>
    /// Looks up a key in a Smalltalk Dictionary object.
    /// </summary>
    private SmalltalkOOP DictionaryLookup(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
    {
        // Dictionary layout (from ClassDescription):
        // Fixed slots: 0=class, 1=tally
        // Indexed slots: 0=keys array, 1=values array

        // Get the keys and values arrays
        var keysArrayOOP = _heapManager.GetIndexedOOP(dictOOP, 0);
        var valuesArrayOOP = _heapManager.GetIndexedOOP(dictOOP, 1);

        // Linear search through keys (Dictionary uses Set's hashing internally)
        int size = _heapManager.GetIndexedSize(keysArrayOOP);
        for (int i = 0; i < size; i++)
        {
            var key = _heapManager.GetIndexedOOP(keysArrayOOP, i);
            if (key == keyOOP)  // Identity comparison for Symbols
            {
                return _heapManager.GetIndexedOOP(valuesArrayOOP, i);
            }
        }

        return SpecialObjectsArray.Instance.Nil;  // Not found
    }

    /// <summary>
    /// Parses an instance variable string from a class.
    /// Instance variables are stored as a space-separated String in the heap.
    /// </summary>
    internal List<string> ParseInstanceVariableString(SmalltalkOOP instVarsOOP)
    {
        var result = new List<string>();

        if (instVarsOOP == SpecialObjectsArray.Instance.Nil)
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
}

/// <summary>
/// Cache for runtime class lookups with invalidation support.
/// </summary>
public class RuntimeClassCache
{
    private readonly RuntimeHeapAccess _heapAccess;
    private readonly ISmalltalkHeapManager _heapManager;
    private Dictionary<string, SmalltalkOOP> _classCache = new();
    private bool _cacheValid = true;

    public RuntimeClassCache(RuntimeHeapAccess heapAccess, ISmalltalkHeapManager heapManager)
    {
        _heapAccess = heapAccess;
        _heapManager = heapManager;
    }

    public SmalltalkOOP GetClass(string className)
    {
        if (!_cacheValid || !_classCache.TryGetValue(className, out var classOOP))
        {
            classOOP = _heapAccess.LookupClassInHeap(className);
            if (_cacheValid)
                _classCache[className] = classOOP;
        }
        return classOOP;
    }

    public void InvalidateCache()
    {
        _cacheValid = false;
        _classCache.Clear();
    }
}

/// <summary>
/// Cache for runtime field layout lookups with invalidation support.
/// </summary>
public class RuntimeFieldLayoutCache
{
    private readonly RuntimeHeapAccess _heapAccess;
    private readonly ISmalltalkHeapManager _heapManager;
    private Dictionary<SmalltalkOOP, Dictionary<string, int>> _layoutCache = new();
    private Dictionary<SmalltalkOOP, int> _instanceVarCountCache = new();
    private bool _cacheValid = true;

    public RuntimeFieldLayoutCache(RuntimeHeapAccess heapAccess, ISmalltalkHeapManager heapManager)
    {
        _heapAccess = heapAccess;
        _heapManager = heapManager;
    }

    public int GetFieldIndex(SmalltalkOOP classOOP, string fieldName)
    {
        if (!_cacheValid)
        {
            _layoutCache.Clear();
            _instanceVarCountCache.Clear();
            _cacheValid = true;
        }

        if (!_layoutCache.TryGetValue(classOOP, out var fieldMap))
        {
            fieldMap = BuildFieldMap(classOOP);
            _layoutCache[classOOP] = fieldMap;
        }

        if (!fieldMap.TryGetValue(fieldName, out int index))
        {
            throw new InvalidOperationException($"Field {fieldName} not found in class");
        }

        return index;
    }

    public int GetInstanceVariableCount(SmalltalkOOP classOOP)
    {
        if (!_cacheValid)
        {
            _layoutCache.Clear();
            _instanceVarCountCache.Clear();
            _cacheValid = true;
        }

        if (!_instanceVarCountCache.TryGetValue(classOOP, out int count))
        {
            // Build field map which also counts instance variables
            var fieldMap = BuildFieldMap(classOOP);
            count = fieldMap.Count;
            _instanceVarCountCache[classOOP] = count;
            _layoutCache[classOOP] = fieldMap;
        }

        return count;
    }

    private Dictionary<string, int> BuildFieldMap(SmalltalkOOP classOOP)
    {
        var fieldMap = new Dictionary<string, int>();
        var fieldNames = new List<string>();
        var currentClass = classOOP;

        // Walk up the inheritance chain collecting all instance variable names
        while (currentClass != SpecialObjectsArray.Instance.Nil)
        {
            // Get instanceVariables from the class (slot 4 in ClassDescription)
            // ClassDescription layout: superclass, methodDict, format, subclasses, instanceVariables, organization
            var instVarsOOP = _heapManager.GetInstanceVariable(currentClass, 4);

            // Parse the instance variable names
            var instVarNames = _heapAccess.ParseInstanceVariableString(instVarsOOP);

            // Prepend to maintain inheritance order (superclass fields first)
            fieldNames.InsertRange(0, instVarNames);

            // Move to superclass (slot 0 in Behavior)
            currentClass = _heapManager.GetInstanceVariable(currentClass, 0);
        }

        // Build the field name to index map
        for (int i = 0; i < fieldNames.Count; i++)
        {
            fieldMap[fieldNames[i]] = i;
        }

        return fieldMap;
    }

    public void InvalidateCache()
    {
        _cacheValid = false;
    }
}

/// <summary>
/// Singleton for frequently-accessed runtime classes with cache invalidation.
/// </summary>
public class RuntimeSpecialObjects
{
    private static RuntimeSpecialObjects? _instance;
    public static RuntimeSpecialObjects Instance => _instance ??= new();

    // Cached class OOPs (populated on first use)
    private SmalltalkOOP? _compiledMethodClass;
    private SmalltalkOOP? _byteArrayClass;
    private SmalltalkOOP? _methodContextClass;
    private SmalltalkOOP? _blockContextClass;
    private SmalltalkOOP? _blockClosureClass;
    private SmalltalkOOP? _dictionaryClass;
    private SmalltalkOOP? _arrayClass;
    private SmalltalkOOP? _stringClass;
    private SmalltalkOOP? _symbolClass;
    private SmalltalkOOP? _associationClass;

    // Reference to heap access for lookups
    private RuntimeHeapAccess? _heapAccess;

    public void Initialize(RuntimeHeapAccess heapAccess)
    {
        _heapAccess = heapAccess;
    }

    private SmalltalkOOP LookupClass(string className)
    {
        if (_heapAccess == null)
            throw new InvalidOperationException("RuntimeSpecialObjects not initialized");
        return _heapAccess.GetClass(className);
    }

    public SmalltalkOOP CompiledMethodClass =>
        _compiledMethodClass ??= LookupClass("CompiledMethod");

    public SmalltalkOOP ByteArrayClass =>
        _byteArrayClass ??= LookupClass("ByteArray");

    public SmalltalkOOP MethodContextClass =>
        _methodContextClass ??= LookupClass("MethodContext");

    public SmalltalkOOP BlockContextClass =>
        _blockContextClass ??= LookupClass("BlockContext");

    public SmalltalkOOP BlockClosureClass =>
        _blockClosureClass ??= LookupClass("BlockClosure");

    public SmalltalkOOP DictionaryClass =>
        _dictionaryClass ??= LookupClass("Dictionary");

    public SmalltalkOOP ArrayClass =>
        _arrayClass ??= LookupClass("Array");

    public SmalltalkOOP StringClass =>
        _stringClass ??= LookupClass("String");

    public SmalltalkOOP SymbolClass =>
        _symbolClass ??= LookupClass("Symbol");

    public SmalltalkOOP AssociationClass =>
        _associationClass ??= LookupClass("Association");

    public void InvalidateCache()
    {
        _compiledMethodClass = null;
        _byteArrayClass = null;
        _methodContextClass = null;
        _blockContextClass = null;
        _blockClosureClass = null;
        _dictionaryClass = null;
        _arrayClass = null;
        _stringClass = null;
        _symbolClass = null;
        _associationClass = null;
    }
}