using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using System.Text;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Runtime service layer for accessing heap data structures during VM execution.
/// Provides safe, read-only access to Smalltalk objects and metadata.
/// Replaces GlobalRegistry and LayoutResolver for runtime use.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
/// </summary>
public class RuntimeServices : IRuntimeServices
{
    private readonly ISmalltalkHeap _heapManager;
    private readonly VMMetadata metadata;

    // Caching layer (invalidated by primitive 89)
    private readonly RuntimeClassCache _classCache;

    // Dictionary operations service
    private readonly RuntimeDictionaryService _dictService;

    // Cached CharacterTable OOP (invalidated by primitive 89)
    private SmalltalkOOP _characterTableCache;
    private bool _characterTableCacheValid = false;

    // Cached USTable OOP (invalidated by primitive 89)
    private SmalltalkOOP _ustableCache;
    private bool _ustableCacheValid = false;

    public RuntimeServices(ISmalltalkHeap heapManager, VMMetadata metadata)
    {
        _heapManager = heapManager;
        this.metadata = metadata;
        _classCache = new RuntimeClassCache(this, heapManager);
        _dictService = new RuntimeDictionaryService(heapManager);

        // Initialize cache fields to Nil
        _characterTableCache = heapManager.GetNil();
        _ustableCache = heapManager.GetNil();
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

        // Get Symbol's classPool
        var classPoolOOP = _heapManager.GetInstanceVariable(symbolClassOOP, VMConstants.CLASS_CLASSPOOL_INDEX);
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

        // USTable is an Array of Arrays (buckets) for collision chaining.
        // Use hash-based bucket lookup for O(1) access instead of linear scan.
        int ustableSize = _heapManager.GetIndexedSize(ustableOOP);
        var nilOOP = metadata.KnownObjects.Nil;

        // Compute bucket index using same hash as Smalltalk's stringhash
        // (matches String>>hash algorithm used by Symbol class>>hasInterned:ifTrue:)
        int hash = SymbolHasher.ComputeHash(symbolName);
        int bucketIndex = hash % ustableSize;

        // Get the bucket array at this index
        var bucketOOP = _heapManager.GetIndexedOOP(ustableOOP, bucketIndex);
        if (bucketOOP == nilOOP || bucketOOP.IsSmallInteger)
        {
            return nilOOP; // No bucket = symbol not found
        }

        // Search within this bucket only (typically 1-3 symbols)
        int bucketSize = _heapManager.GetIndexedSize(bucketOOP);

        for (int i = 0; i < bucketSize; i++)
        {
            var symbolOOP = _heapManager.GetIndexedOOP(bucketOOP, i);

            // Skip nil slots
            if (symbolOOP == nilOOP || symbolOOP.IsSmallInteger)
                continue;

            // Compare using GetByteSpan for zero-copy access
            var symbolBytes = _heapManager.GetByteSpan(symbolOOP);

            // Quick length check first
            if (symbolBytes.Length != symbolName.Length)
                continue;

            // Compare bytes (Symbol uses Latin-1 encoding)
            bool match = true;
            for (int j = 0; j < symbolName.Length; j++)
            {
                if (symbolBytes[j] != (byte)symbolName[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
                return symbolOOP;
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
    /// Looks up a key in a Smalltalk Dictionary object using hash-based lookup.
    /// Dictionary is a PointerIndexed subclass of Set with no additional instance variables.
    /// It stores Association objects directly in indexed slots (NOT in separate keys/values arrays).
    /// Implements Smalltalk-80 Dictionary>>findKeyOrNil: semantics with linear probing.
    /// </summary>
    private SmalltalkOOP DictionaryLookup(SmalltalkOOP dictOOP, SmalltalkOOP keyOOP)
    {
        // Dictionary structure:
        //   Instance variable 0: tally (SmallInteger count of entries, inherited from Set)
        //   Indexed slots 0..N: Association objects (PointerIndexed)

        int size = _heapManager.GetIndexedSize(dictOOP);
        if (size == 0) return metadata.KnownObjects.Nil;

        var nilOOP = metadata.KnownObjects.Nil;

        // Hash-based lookup: start at hash slot, linear probe until found or nil
        int startIndex = IdentityHashUtility.ComputeDictionaryHashIndex(keyOOP, size);
        int index = startIndex;

        do
        {
            var associationOOP = _heapManager.GetIndexedOOP(dictOOP, index);

            // Empty slot - key not in dictionary
            if (associationOOP == nilOOP)
                return nilOOP;

            // Skip non-Association entries (shouldn't happen, but defensive)
            if (!associationOOP.IsSmallInteger)
            {
                // Get the key from the Association (instance variable 0)
                var assocKeyOOP = _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);

                if (assocKeyOOP == keyOOP)  // Identity comparison for Symbols
                {
                    // Return the value from the Association (instance variable 1)
                    return _heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
                }
            }

            // Linear probe to next slot
            index = (index + 1) % size;
        } while (index != startIndex);

        return nilOOP;  // Wrapped around - not found
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
    /// Gets the canonical Character object for a given Character OOP.
    /// If CharacterTable is initialized, returns the canonical entry from CharacterTable.
    /// If CharacterTable is not yet initialized (before Character initialize runs),
    /// returns the original Character OOP unchanged.
    ///
    /// This is used by the bytecode interpreter to canonicalize Character literals
    /// at runtime. Character literals in method literal frames are created during
    /// cold start as NEW Character objects, but CharacterTable entries (created by
    /// Character initialize) are the canonical ones that String>>at: returns.
    /// Without canonicalization, identity comparison (==) would fail between
    /// a literal like $. and a Character from CharacterTable.
    /// </summary>
    /// <param name="characterOOP">A Character object OOP (may be from literal frame or CharacterTable)</param>
    /// <returns>The canonical Character from CharacterTable, or original if CharacterTable not initialized</returns>
    public virtual SmalltalkOOP GetCanonicalCharacter(SmalltalkOOP characterOOP)
    {
        // Fast path: if CharacterTable cache is not valid, it means Character initialize
        // hasn't run yet, so there's no canonical table to look up from
        if (!_characterTableCacheValid)
        {
            // Try to get CharacterTable - this will cache it if available
            var characterTableOOP = LookupClassVariable("Character", "CharacterTable");
            if (characterTableOOP == metadata.KnownObjects.Nil)
            {
                // CharacterTable not initialized yet - return original
                return characterOOP;
            }
        }

        // CharacterTable is available - get the code point from the Character
        // Character has instance variable 0 = value (the code point as SmallInteger)
        var codePointOOP = _heapManager.GetInstanceVariable(characterOOP, 0);
        if (!codePointOOP.IsSmallInteger)
        {
            // Invalid Character structure - return original
            return characterOOP;
        }

        int codePoint = codePointOOP.AsSmallInteger();

        // CharacterTable is an Array of 256 Characters indexed by ASCII value
        // Validate code point is in range
        int tableSize = _heapManager.GetIndexedSize(_characterTableCache);
        if (codePoint < 0 || codePoint >= tableSize)
        {
            // Code point out of range - return original
            return characterOOP;
        }

        // Get the canonical Character from CharacterTable
        var canonicalCharacter = _heapManager.GetIndexedOOP(_characterTableCache, codePoint);
        if (canonicalCharacter == metadata.KnownObjects.Nil)
        {
            // CharacterTable entry is nil - return original (shouldn't happen)
            return characterOOP;
        }

        return canonicalCharacter;
    }

    /// <summary>
    /// Checks if the given OOP is a Character object.
    /// </summary>
    public virtual bool IsCharacter(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return false;

        var classOOP = _heapManager.GetClassOOP(oop);
        var characterClassOOP = GetClass("Character");

        return classOOP == characterClassOOP;
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

        // Get classPool
        var classPoolOOP = _heapManager.GetInstanceVariable(classOOP, VMConstants.CLASS_CLASSPOOL_INDEX);
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

    #region Canonical Global Lookup (Smalltalk → Undeclared → undefined)

    /// <summary>
    /// Looks up a global variable following the canonical Smalltalk-80 lookup chain:
    /// 1. Search SystemDictionary (Smalltalk)
    /// 2. If not found, search Undeclared
    /// 3. If found in Undeclared and migrate=true, move it to SystemDictionary
    /// 4. If not found anywhere, return nil
    /// </summary>
    public virtual SmalltalkOOP LookupGlobalAssociation(string globalName, bool migrate = false)
    {
        var systemDictOOP = metadata.KnownObjects.Smalltalk;
        var nilOOP = metadata.KnownObjects.Nil;

        // Step 1: Search SystemDictionary first
        var assocOOP = _dictService.FindAssociationByName(systemDictOOP, globalName);
        if (assocOOP != nilOOP)
        {
            return assocOOP;
        }

        // Step 2: Search Undeclared
        var undeclaredOOP = _dictService.LookupValueByName(systemDictOOP, "Undeclared");
        if (undeclaredOOP == nilOOP)
        {
            return nilOOP;  // Undeclared doesn't exist
        }

        assocOOP = _dictService.FindAssociationByName(undeclaredOOP, globalName);
        if (assocOOP == nilOOP)
        {
            return nilOOP;  // Not found anywhere
        }

        // Step 3: If migrate=true, move from Undeclared to SystemDictionary
        if (migrate)
        {
            var keySymbolOOP = _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX);
            _dictService.InstallAssociation(systemDictOOP, assocOOP, keySymbolOOP);
            _dictService.RemoveAssociation(undeclaredOOP, keySymbolOOP);
        }

        return assocOOP;
    }

    /// <summary>
    /// Looks up a global variable's value following the canonical lookup chain.
    /// </summary>
    public virtual SmalltalkOOP LookupGlobalValue(string globalName)
    {
        var assocOOP = LookupGlobalAssociation(globalName, migrate: false);
        if (assocOOP == metadata.KnownObjects.Nil)
        {
            return metadata.KnownObjects.Nil;
        }
        return _heapManager.GetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
    }

    /// <summary>
    /// Sets a global variable's value, creating the Association if necessary.
    /// Follows the canonical lookup chain and migrates from Undeclared if found there.
    /// </summary>
    public virtual SmalltalkOOP SetGlobalValue(string globalName, SmalltalkOOP valueOOP)
    {
        var systemDictOOP = metadata.KnownObjects.Smalltalk;
        var nilOOP = metadata.KnownObjects.Nil;

        // Look up with migration enabled (moves from Undeclared to SystemDictionary)
        var assocOOP = LookupGlobalAssociation(globalName, migrate: true);

        if (assocOOP != nilOOP)
        {
            // Found existing Association - update its value
            _heapManager.SetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);
            return assocOOP;
        }

        // Not found anywhere - create new Association
        var keySymbolOOP = InternSymbol(globalName);
        if (keySymbolOOP == nilOOP)
        {
            throw new InvalidOperationException($"Symbol '{globalName}' not found in USTable");
        }

        // Allocate new Association
        var associationClassOOP = GetClass("Association");
        assocOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, fixedSlots: 2, indexedSize: 0);
        _heapManager.SetClassOOP(assocOOP, associationClassOOP);
        _heapManager.SetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_KEY_INDEX, keySymbolOOP);
        _heapManager.SetInstanceVariable(assocOOP, VMConstants.ASSOCIATION_VALUE_INDEX, valueOOP);

        // Install in SystemDictionary
        _dictService.InstallAssociation(systemDictOOP, assocOOP, keySymbolOOP);

        return assocOOP;
    }

    #endregion
}
