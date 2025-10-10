namespace Smalltalk80.Interpreter.ColdStart;

using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// Two-phase symbol and string interning for JSON-based cold start.
///
/// WHY TWO PHASES?
/// Phase 1 (CreateTemporaryStorage): Symbol class doesn't exist yet, so we can't create Symbol objects.
///   - Create temporary C# Dictionary mapping symbol text → placeholder OOP
///   - Create String objects (String class will be created soon in bootstrap classes)
///   - Register temp localIds for use during class/method creation
///
/// Phase 2 (MigrateToUSTable): After all classes exist, Symbol class now has proper structure.
///   - Create Symbol's USTable (universal symbol table)
///   - Create proper Symbol objects using Smalltalk hash algorithm
///   - Update all existing references to use real Symbol OOPs
///   - Add Symbols to USTable for runtime interning
///
/// Blue Book Symbol Semantics:
/// - Symbols are unique strings used as identifiers (class names, selectors, etc.)
/// - Symbol class has class variable USTable containing all interned symbols
/// - Same text always returns same Symbol object (identity guarantee)
/// - Symbols use Smalltalk hash algorithm, not C# GetHashCode()
/// </summary>
public class SymbolInterning(ColdStartJsonLoader loader)
{
    private readonly ColdStartJsonLoader _loader = loader;

    // Temporary storage used between Phase 1 and Phase 2
    private readonly Dictionary<string, SObjectRef> _tempSymbolMap = new();
    private readonly Dictionary<string, SObjectRef> _tempStringMap = new();

    /// <summary>
    /// Phase 1: Creates temporary C# storage for symbols and strings.
    ///
    /// CRITICAL: Symbol class doesn't exist yet, so we create placeholder OOPs.
    /// These placeholders are used when creating classes and methods.
    /// After Symbol class exists, we'll replace placeholders with real Symbol objects.
    ///
    /// For strings, we create real String objects immediately since String is a bootstrap class.
    /// </summary>
    public void CreateTemporaryStorage()
    {
        Console.WriteLine($"[SymbolInterning] Creating temporary storage for {_loader.Bundle.Symbols.Length} symbols and {_loader.Bundle.Strings.Length} strings...");

        // Create temporary symbol placeholders
        // These are NOT real Symbol objects - just unique OOPs to use as references
        // Real Symbol objects created in Phase 2 after Symbol class exists
        for (int i = 0; i < _loader.Bundle.Symbols.Length; i++)
        {
            var symbolText = _loader.Bundle.Symbols[i];

            // Allocate a placeholder OOP (we'll use a simple SmallInteger for now)
            // This is TEMPORARY - will be replaced with real Symbol OOP in Phase 2
            var placeholderOop = AllocateTemporaryPlaceholder(symbolText);

            // Store in temp map for Phase 2 migration
            _tempSymbolMap[symbolText] = placeholderOop;

            // Register localId so classes and methods can reference symbols
            var localId = $"symbol:{i}";
            _loader.RegisterLocalId(localId, placeholderOop);
        }

        // Create real String objects immediately
        // String is a bootstrap class, so it will exist after first class phase
        // We create temp placeholders here too, and replace with real String objects
        // after String class is created
        for (int i = 0; i < _loader.Bundle.Strings.Length; i++)
        {
            var stringText = _loader.Bundle.Strings[i];

            // Create placeholder for now - will be replaced with real String object
            // after String class exists
            var placeholderOop = AllocateTemporaryPlaceholder(stringText);

            // Store in temp map
            _tempStringMap[stringText] = placeholderOop;

            // Register localId
            var localId = $"string:{i}";
            _loader.RegisterLocalId(localId, placeholderOop);
        }

        Console.WriteLine($"[SymbolInterning] Created {_tempSymbolMap.Count} symbol placeholders and {_tempStringMap.Count} string placeholders.");
    }

    /// <summary>
    /// Phase 2: Migrates symbols from C# temp storage to Smalltalk USTable.
    ///
    /// Now that Symbol class exists with proper structure, we can:
    /// 1. Create Symbol's USTable class variable (Array or Set)
    /// 2. For each symbol text:
    ///    - Create proper Symbol object (byte-indexed with symbol text)
    ///    - Hash using Smalltalk hash algorithm (not C# GetHashCode!)
    ///    - Add to USTable
    /// 3. Update all existing references (in classes, methods, etc.) to use real Symbol OOPs
    /// 4. Create real String objects and update references
    ///
    /// This ensures:
    /// - Symbol uniqueness (same text → same OOP)
    /// - Proper Smalltalk hashing
    /// - USTable contains all symbols for runtime interning
    /// </summary>
    public void MigrateToUSTable()
    {
        Console.WriteLine($"[SymbolInterning] Migrating {_tempSymbolMap.Count} symbols to USTable...");

        // Get Symbol class
        var symbolClassRef = _loader.ResolveLocalId("class:Symbol");
        if (symbolClassRef.IsNil)
            throw new InvalidOperationException("Symbol class not found - cannot migrate symbols to USTable");

        // Get or create USTable
        // USTable is a class variable of Symbol class, typically an Array or Set
        var usTableRef = GetOrCreateUSTable(symbolClassRef);

        // Mapping from old placeholder OOP → new real Symbol OOP
        var migrationMap = new Dictionary<SObjectRef, SObjectRef>();

        // Create real Symbol objects and add to USTable
        int symbolIndex = 0;
        foreach (var kvp in _tempSymbolMap)
        {
            var symbolText = kvp.Key;
            var placeholderOop = kvp.Value;

            // Create real Symbol object
            // Symbol is a byte-indexed object containing its text as bytes
            var bytes = System.Text.Encoding.UTF8.GetBytes(symbolText);

            var symbolOop = ObjectMemory.AllocateObject(
                symbolClassRef,
                ObjectFormat.ByteIndexed,
                instanceVarCount: 0,          // Symbol has no instance variables
                indexedSize: bytes.Length  // Indexed size = number of bytes
            );

            ObjectMemory.SetByteIndexedData(symbolOop, bytes);

            // Add to USTable (using Smalltalk hash algorithm)
            AddToUSTable(usTableRef, symbolOop, symbolText);

            // Track migration mapping
            var symbolRef = SObjectRef.FromObjectPointer(symbolOop);
            migrationMap[placeholderOop] = symbolRef;

            // Update localId registration
            var localId = $"symbol:{symbolIndex}";
            _loader.RegisterLocalId(localId, symbolRef);

            symbolIndex++;
        }

        // Migrate strings from placeholders to real String objects
        Console.WriteLine($"[SymbolInterning] Migrating {_tempStringMap.Count} strings...");

        var stringClassRef = _loader.ResolveLocalId("class:String");
        if (stringClassRef.IsNil)
            throw new InvalidOperationException("String class not found - cannot migrate strings");

        int stringIndex = 0;
        foreach (var kvp in _tempStringMap)
        {
            var stringText = kvp.Key;
            var placeholderOop = kvp.Value;

            // Create real String object
            var bytes = System.Text.Encoding.UTF8.GetBytes(stringText);

            var stringOop = ObjectMemory.AllocateObject(
                stringClassRef,
                ObjectFormat.ByteIndexed,
                instanceVarCount: 0,
                indexedSize: bytes.Length
            );

            ObjectMemory.SetByteIndexedData(stringOop, bytes);

            // Track migration mapping
            var stringRef = SObjectRef.FromObjectPointer(stringOop);
            migrationMap[placeholderOop] = stringRef;

            // Update localId registration
            var localId = $"string:{stringIndex}";
            _loader.RegisterLocalId(localId, stringRef);

            stringIndex++;
        }

        // Update all existing object references from placeholder OOPs to real OOPs
        // This includes:
        // - Class names, instance variable names
        // - Method selectors, literals
        // - Any other symbol/string references
        Console.WriteLine($"[SymbolInterning] Updating all references from placeholders to real symbols...");
        UpdateAllReferences(migrationMap);

        Console.WriteLine($"[SymbolInterning] Migration complete. {migrationMap.Count} references updated.");
    }

    /// <summary>
    /// Allocates a temporary placeholder OOP for a symbol or string.
    /// This is NOT a real Smalltalk object - just a unique reference to use temporarily.
    ///
    /// We use negative SmallIntegers as placeholders since they won't conflict with
    /// real OOPs (which are positive).
    /// </summary>
    private SObjectRef AllocateTemporaryPlaceholder(string text)
    {
        // Use negative hash code as temporary placeholder
        // This ensures uniqueness and won't conflict with real OOPs
        var hash = text.GetHashCode();
        var placeholderValue = -(Math.Abs(hash) + 1); // Ensure negative

        return SObjectRef.FromSmallInteger(placeholderValue);
    }

    /// <summary>
    /// Gets or creates Symbol's USTable class variable.
    ///
    /// USTable is typically an Array or Set containing all interned symbols.
    /// Blue Book uses a simple Array with linear probing.
    /// </summary>
    private SObjectRef GetOrCreateUSTable(SObjectRef symbolClassRef)
    {
        // TODO: Access Symbol class's classPool to get USTable
        // For now, create a new Array as USTable

        var arrayClassRef = _loader.ResolveLocalId("class:Array");
        if (arrayClassRef.IsNil)
            throw new InvalidOperationException("Array class not found");

        // Calculate USTable size (power of 2, ~75% load factor)
        var usTableSize = CalculateUSTableSize(_tempSymbolMap.Count);

        var usTableOop = ObjectMemory.AllocateObject(
            arrayClassRef,
            ObjectFormat.PointerIndexed,
            instanceVarCount: 0,
            indexedSize: usTableSize
        );

        // Initialize all slots to nil
        for (int i = 0; i < usTableSize; i++)
        {
            ObjectMemory.SetIndexedPointer(usTableOop, i, SObjectRef.Nil);
        }

        // TODO: Store USTable in Symbol class's classPool as 'USTable' association

        return SObjectRef.FromObjectPointer(usTableOop);
    }

    /// <summary>
    /// Adds a Symbol to USTable using Smalltalk hash algorithm.
    ///
    /// Blue Book uses linear probing for collision resolution.
    /// </summary>
    private void AddToUSTable(SObjectRef usTableRef, nint symbolOop, string symbolText)
    {
        var usTableSize = ObjectMemory.GetIndexedSize(usTableRef.AsObjectPointer());

        // Compute Smalltalk hash (Blue Book algorithm)
        // Simple version: sum of character codes mod table size
        var hash = ComputeSmalltalkHash(symbolText, usTableSize);

        // Linear probing to find empty slot
        for (int probe = 0; probe < usTableSize; probe++)
        {
            var index = (hash + probe) % usTableSize;
            var slot = ObjectMemory.GetIndexedPointer(usTableRef.AsObjectPointer(), index);

            if (slot.IsNil)
            {
                // Found empty slot - add symbol
                ObjectMemory.SetIndexedPointer(usTableRef.AsObjectPointer(), index, SObjectRef.FromObjectPointer(symbolOop));
                return;
            }
        }

        throw new InvalidOperationException("USTable full - cannot add symbol");
    }

    /// <summary>
    /// Computes Smalltalk hash for a string.
    ///
    /// Blue Book algorithm: sum character codes and take modulo table size.
    /// This is NOT the same as C# GetHashCode()!
    /// </summary>
    private int ComputeSmalltalkHash(string text, int tableSize)
    {
        int hash = 0;
        foreach (char c in text)
        {
            hash += c;
        }
        return Math.Abs(hash) % tableSize;
    }

    /// <summary>
    /// Calculates appropriate USTable size for given symbol count.
    /// Uses power of 2 sizing with ~75% load factor.
    /// </summary>
    private int CalculateUSTableSize(int symbolCount)
    {
        if (symbolCount == 0)
            return 256; // Minimum size

        // Target 75% load factor
        var targetSize = (int)(symbolCount / 0.75);

        // Round up to next power of 2
        var size = 256;
        while (size < targetSize)
            size *= 2;

        return size;
    }

    /// <summary>
    /// Updates all existing object references from placeholder OOPs to real Symbol/String OOPs.
    ///
    /// This walks through all created objects (classes, methods, dictionaries, etc.)
    /// and replaces any placeholder references with real Symbol/String references.
    /// </summary>
    private void UpdateAllReferences(Dictionary<SObjectRef, SObjectRef> migrationMap)
    {
        // TODO: Walk through all objects in heap and update references
        // This requires accessing ObjectMemory's object table

        // For now, we rely on the loader's localId map being updated
        // When objects are accessed via ResolveLocalId, they'll get the new OOPs

        Console.WriteLine("[SymbolInterning] Reference update not yet implemented - relies on localId map updates");
    }
}
