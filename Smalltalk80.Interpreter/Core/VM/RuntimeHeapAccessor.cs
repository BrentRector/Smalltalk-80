using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Accessor for runtime heap data structures.
/// Provides safe, read-only access to Smalltalk objects and metadata during VM execution.
/// Replaces GlobalRegistry and LayoutResolver for runtime use.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
/// </summary>
public class RuntimeHeapAccessor
{
    private readonly ISmalltalkHeapManager _heapManager;

    // Caching layer (invalidated by primitive 89)
    private readonly RuntimeClassCache _classCache;

    public RuntimeHeapAccessor(ISmalltalkHeapManager heapManager)
    {
        _heapManager = heapManager;
        _classCache = new RuntimeClassCache(this, heapManager);
    }

    /// <summary>
    /// Invalidates all runtime caches. Called by primitive 89 (flushCache).
    /// </summary>
    public void InvalidateAllCaches()
    {
        _classCache.InvalidateCache();
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
        // CRITICAL: USTable is a CLASS VARIABLE of Symbol, not a global!
        // Symbol class definition: classVariableNames: 'SingleCharSymbols USTable'
        // We need to look up USTable from Symbol's classPool

        // CRITICAL: To avoid infinite recursion (InternSymbol → GetClass("Symbol") → InternSymbol),
        // we must look up Symbol class using direct byte comparison, not through InternSymbol
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;
        var symbolClassOOP = LookupGlobalByName(smalltalkDictOOP, "Symbol");
        if (symbolClassOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol class not found during InternSymbol");
        }

        // Get Symbol's classPool (instance variable index 7 for Class)
        var classPoolOOP = _heapManager.GetInstanceVariable(symbolClassOOP, 7);
        if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("Symbol classPool is nil during InternSymbol");
        }

        // Search classPool for USTable association
        var ustableOOP = LookupGlobalByName(classPoolOOP, "USTable");
        if (ustableOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException("USTable not found in Symbol classPool");
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
    /// Looks up a global variable by name using linear search with byte comparison.
    /// Used to break the circular dependency when looking up USTable.
    /// CRITICAL: Cannot use hash-based lookup because Symbol>>hash depends on OOP, not string content!
    /// </summary>
    public SmalltalkOOP LookupGlobalByName(SmalltalkOOP dictOOP, string globalName)
    {
        byte[] searchBytes = Encoding.Latin1.GetBytes(globalName);
        int size = _heapManager.GetIndexedSize(dictOOP);

        // Debug TextConstants lookups
        bool debugTextConstants = (globalName == "TextConstants");
        if (debugTextConstants)
        {
            Console.WriteLine($"[TC-DICT-SEARCH] Searching for '{globalName}' in dictionary {dictOOP} (size={size})");
        }

        // Linear search through entire dictionary (cannot use hash - Symbol>>hash is OOP-based!)
        for (int i = 0; i < size; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(dictOOP, i);

            // Skip nil slots
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
                    var valueOOP = _heapManager.GetInstanceVariable(associationOOP, 1);

                    if (debugTextConstants)
                    {
                        string valueDesc = valueOOP == SpecialObjectsArray.Instance.Nil ? "nil" : valueOOP.ToString();
                        Console.WriteLine($"[TC-DICT-FOUND] Found '{globalName}' at slot {i}: assoc={associationOOP}, value={valueDesc}");
                    }

                    return valueOOP;
                }
            }
        }

        if (debugTextConstants)
        {
            Console.WriteLine($"[TC-DICT-NOTFOUND] '{globalName}' not found in dictionary {dictOOP}");
        }

        return SpecialObjectsArray.Instance.Nil;  // Not found
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
        var nilOOP = SpecialObjectsArray.Instance.Nil;

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

    /// <summary>
    /// Builds complete field layout map for all classes by walking the Smalltalk heap.
    /// Reads ClassDescription.instanceVariables (slot 4) Array-of-Strings for each class,
    /// walks superclass chain to accumulate inherited fields, and returns complete layouts.
    ///
    /// This enables full runtime reflection and supports runtime class modification.
    /// Equivalent to Smalltalk's allInstVarNames method.
    /// </summary>
    public Dictionary<string, List<string>> BuildFieldMap()
    {
        var fieldMap = new Dictionary<string, List<string>>();

        // Get Smalltalk dictionary from SpecialObjectsArray
        var smalltalkDictOOP = SpecialObjectsArray.Instance.Smalltalk;

        // Iterate through all global bindings (Associations in SystemDictionary)
        int dictSize = _heapManager.GetIndexedSize(smalltalkDictOOP);
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        for (int i = 0; i < dictSize; i++)
        {
            var associationOOP = _heapManager.GetIndexedOOP(smalltalkDictOOP, i);
            if (associationOOP == nilOOP || associationOOP.IsSmallInteger)
                continue;

            // Get the value from Association (slot 1)
            var valueOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
            if (valueOOP == nilOOP || valueOOP.IsSmallInteger)
                continue;

            // Check if it's a class object (has ClassDescription as ancestor)
            // Classes have methodDict field (slot 1), which is typically a Dictionary
            try
            {
                var methodDictOOP = _heapManager.GetInstanceVariable(valueOOP, 1);
                if (methodDictOOP == nilOOP || methodDictOOP.IsSmallInteger)
                    continue;  // Not a class

                // Get class name from Association key (slot 0 - a Symbol)
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
    /// Reads ClassDescription.instanceVariables (slot 4) for each class in the hierarchy.
    /// </summary>
    private List<string> AccumulateInstanceVariableNames(SmalltalkOOP classOOP)
    {
        var fields = new List<string>();
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Walk superclass chain from current class to root (Object), pushing onto stack
        var superclassChain = new Stack<SmalltalkOOP>();
        var currentOOP = classOOP;

        while (currentOOP != nilOOP && !currentOOP.IsSmallInteger)
        {
            superclassChain.Push(currentOOP);
            // Read superclass field (slot 0 from Behavior)
            currentOOP = _heapManager.GetInstanceVariable(currentOOP, VMConstants.BEHAVIOR_SUPERCLASS_INDEX);
        }

        // Process from root to leaf, accumulating instance variables
        while (superclassChain.Count > 0)
        {
            var oop = superclassChain.Pop();

            // Read ClassDescription.instanceVariables field (slot 4)
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
}

/// <summary>
/// Cache for runtime class lookups with invalidation support.
/// </summary>
public class RuntimeClassCache
{
    private readonly RuntimeHeapAccessor _heapAccess;
    private readonly ISmalltalkHeapManager _heapManager;
    private Dictionary<string, SmalltalkOOP> _classCache = new();
    private bool _cacheValid = true;

    public RuntimeClassCache(RuntimeHeapAccessor heapAccess, ISmalltalkHeapManager heapManager)
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
    private SmalltalkOOP? _dictionaryClass;
    private SmalltalkOOP? _arrayClass;
    private SmalltalkOOP? _stringClass;
    private SmalltalkOOP? _symbolClass;
    private SmalltalkOOP? _associationClass;

    // Reference to heap access for lookups
    private RuntimeHeapAccessor? _heapAccess;

    public void Initialize(RuntimeHeapAccessor heapAccess)
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
        _dictionaryClass = null;
        _arrayClass = null;
        _stringClass = null;
        _symbolClass = null;
        _associationClass = null;
    }
}