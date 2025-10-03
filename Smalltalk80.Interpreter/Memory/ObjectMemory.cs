namespace Smalltalk80.Interpreter.Memory;

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// NEW ARCHITECTURE: ObjectMemory stores all Smalltalk object data internally.
///
/// SObjectRef is the ONLY type used throughout the VM - there is no SObject class.
///
/// ObjectMemory manages:
/// - OOP allocation (4-byte aligned pointers)
/// - Object data storage (class, format, instance variables, indexed data)
/// - Garbage collection
/// - become: primitive implementation
///
/// All object access goes through SObjectRef extension methods that delegate to ObjectMemory.
/// </summary>
public static partial class ObjectMemoryNew
{
    // Object table: Hybrid array-based table with O(1) allocation
    private static readonly ObjectTable objectTable = new();

    // Lock for OOP allocation (kept for compatibility, but ObjectTable has its own locking)
    private static readonly object allocationLock = new();

    // Bootstrap OOP counter (for old SObject compatibility during migration)
    private static nint nextOop = 0x1000;

    /// <summary>
    /// Gets the object table instance for GC access.
    /// </summary>
    internal static ObjectTable ObjectTable => objectTable;

    /// <summary>
    /// Internal storage for a Smalltalk object's data.
    /// This is NEVER exposed outside ObjectMemory - only accessed via SObjectRef methods.
    /// </summary>
    public class ObjectData
    {
        /// <summary>
        /// The class of this object (as SObjectRef to another object).
        /// </summary>
        public SObjectRef ClassRef { get; set; }

        /// <summary>
        /// The format of this object (determines indexed data type).
        /// </summary>
        public ObjectFormat Format { get; set; }

        /// <summary>
        /// Named instance variables (always SObjectRef[]).
        /// </summary>
        public SObjectRef[] InstanceVariables { get; set; }

        /// <summary>
        /// Indexed data (discriminated union based on Format).
        /// - FixedPointer: null (no indexed data)
        /// - PointerIndexed: SObjectRef[]
        /// - ByteIndexed: byte[]
        /// - WordIndexed: ushort[]
        /// - CompiledMethod: special encoding (literals + bytecodes)
        /// </summary>
        public object? IndexedData { get; set; }

        /// <summary>
        /// Mark bit for garbage collection.
        /// </summary>
        public bool Marked { get; set; }

        /// <summary>
        /// Initializes a new instance of the ObjectData class.
        /// </summary>
        /// <param name="classRef">The class of this object.</param>
        /// <param name="format">The format of this object.</param>
        /// <param name="instanceVarCount">The number of instance variables.</param>
        public ObjectData(SObjectRef classRef, ObjectFormat format, int instanceVarCount)
        {
            ClassRef = classRef;
            Format = format;
            InstanceVariables = new SObjectRef[instanceVarCount];

            // Initialize all instance variables to nil
            for (int i = 0; i < instanceVarCount; i++)
            {
                InstanceVariables[i] = SObjectRef.Nil;
            }
        }
    }

    /// <summary>
    /// Allocates a new object and returns its OOP.
    /// </summary>
    public static nint AllocateObject(
        SObjectRef classRef,
        ObjectFormat format,
        int instanceVarCount,
        int indexedSize)
    {
        // Create object data
        var data = new ObjectData(classRef, format, instanceVarCount);

        // Initialize indexed data based on format
        // IMPORTANT: PointerIndexed objects must ALWAYS have an array (even if empty)
        // to ensure GC can traverse them correctly
        data.IndexedData = format switch
        {
            ObjectFormat.PointerIndexed => InitializePointerIndexed(indexedSize),
            ObjectFormat.ByteIndexed => indexedSize > 0 ? new byte[indexedSize] : null,
            ObjectFormat.WordIndexed => indexedSize > 0 ? new ushort[indexedSize] : null,
            ObjectFormat.CompiledMethod => indexedSize > 0 ? new byte[indexedSize] : new byte[0],
            ObjectFormat.FixedPointer => null,  // Fixed pointer objects never have indexed data
            _ => null
        };

        // Allocate slot in object table (O(1) operation)
        int index = objectTable.Allocate(data);

        // Convert index to OOP with 4-byte alignment
        nint oop = ObjectTable.OopFromIndex(index);

        return oop;
    }

    /// <summary>
    /// Initializes a pointer-indexed array with nil values.
    /// </summary>
    private static SObjectRef[] InitializePointerIndexed(int size)
    {
        var array = new SObjectRef[size];
        for (int i = 0; i < size; i++)
        {
            array[i] = SObjectRef.Nil;
        }
        return array;
    }

    /// <summary>
    /// Gets object data for the given OOP.
    /// Throws if OOP is invalid or object doesn't exist.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ObjectData GetObjectData(nint oop)
    {
        if (oop == 0)
            throw new InvalidOperationException("Cannot access nil object data");

        int index = ObjectTable.IndexFromOop(oop);
        var data = objectTable.Get(index);

        if (data != null)
        {
            return data;
        }

        throw new InvalidOperationException($"Invalid OOP: 0x{oop:X} (index: {index})");
    }

    /// <summary>
    /// Checks if an OOP is valid (exists in object table).
    /// </summary>
    public static bool IsValidOop(nint oop)
    {
        if (oop == 0) return false;

        int index = ObjectTable.IndexFromOop(oop);
        return objectTable.Get(index) != null;
    }

    #region Instance Variable Access

    /// <summary>
    /// Gets an instance variable by index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef GetInstanceVariable(nint oop, int index)
    {
        var data = GetObjectData(oop);

        if (index < 0 || index >= data.InstanceVariables.Length)
            throw new ArgumentOutOfRangeException(nameof(index),
                $"Instance variable index {index} out of range (0-{data.InstanceVariables.Length - 1})");

        return data.InstanceVariables[index];
    }

    /// <summary>
    /// Sets an instance variable by index.
    /// </summary>
    public static void SetInstanceVariable(nint oop, int index, SObjectRef value)
    {
        var data = GetObjectData(oop);

        if (index < 0 || index >= data.InstanceVariables.Length)
        {
            // Add stack trace to identify caller
            var stackTrace = new System.Diagnostics.StackTrace(1, true);
            var caller = stackTrace.GetFrame(0);
            var callerMethod = caller?.GetMethod();
            var callerInfo = $"{callerMethod?.DeclaringType?.Name}.{callerMethod?.Name}";

            throw new InvalidOperationException(
                $"DETAILED ERROR: SetInstanceVariable failed - oop={oop}, index={index}, instVarCount={data.InstanceVariables.Length}, format={data.Format}, caller={callerInfo}\nStack trace:\n{stackTrace}");
        }

        data.InstanceVariables[index] = value;
    }

    /// <summary>
    /// Gets the instance variable count.
    /// </summary>
    public static int GetInstanceVariableCount(nint oop)
    {
        var data = GetObjectData(oop);
        return data.InstanceVariables.Length;
    }

    #endregion

    #region Indexed Data Access

    /// <summary>
    /// Gets an indexed byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte GetIndexedByte(nint oop, int index)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.ByteIndexed && data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format {data.Format} does not support byte indexing");

        var bytes = (byte[])data.IndexedData!;

        if (index < 0 || index >= bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return bytes[index];
    }

    /// <summary>
    /// Sets an indexed byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetIndexedByte(nint oop, int index, byte value)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.ByteIndexed && data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format {data.Format} does not support byte indexing");

        var bytes = (byte[])data.IndexedData!;

        if (index < 0 || index >= bytes.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        bytes[index] = value;
    }

    /// <summary>
    /// Gets an indexed word.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ushort GetIndexedWord(nint oop, int index)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {data.Format} does not support word indexing");

        var words = (ushort[])data.IndexedData!;

        if (index < 0 || index >= words.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return words[index];
    }

    /// <summary>
    /// Sets an indexed word.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetIndexedWord(nint oop, int index, ushort value)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.WordIndexed)
            throw new InvalidOperationException($"Object format {data.Format} does not support word indexing");

        var words = (ushort[])data.IndexedData!;

        if (index < 0 || index >= words.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        words[index] = value;
    }

    /// <summary>
    /// Gets an indexed pointer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef GetIndexedPointer(nint oop, int index)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object format {data.Format} does not support pointer indexing");

        var pointers = (SObjectRef[])data.IndexedData!;

        if (index < 0 || index >= pointers.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        return pointers[index];
    }

    /// <summary>
    /// Sets an indexed pointer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void SetIndexedPointer(nint oop, int index, SObjectRef value)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.PointerIndexed)
            throw new InvalidOperationException($"Object format {data.Format} does not support pointer indexing");

        var pointers = (SObjectRef[])data.IndexedData!;

        if (index < 0 || index >= pointers.Length)
            throw new ArgumentOutOfRangeException(nameof(index));

        pointers[index] = value;
    }

    /// <summary>
    /// Gets the indexed size (number of indexed elements).
    /// </summary>
    public static int GetIndexedSize(nint oop)
    {
        var data = GetObjectData(oop);

        if (data.IndexedData == null)
            return 0;

        return data.IndexedData switch
        {
            byte[] bytes => bytes.Length,
            ushort[] words => words.Length,
            SObjectRef[] pointers => pointers.Length,
            _ => 0
        };
    }

    #endregion

    #region Object Properties

    /// <summary>
    /// Gets the class of an object.
    /// </summary>
    public static SObjectRef GetClass(nint oop)
    {
        // TODO: Reimplement bootstrap map support when SObject classes are migrated
        // Check bootstrap map first
        // if (bootstrapObjectMap.TryGetValue(oop, out var bootstrapObj))
        // {
        //     // Get the class from the bootstrap object
        //     var objClass = bootstrapObj.Class;
        //     // Register the class if not already registered
        //     var classOop = RegisterBootstrapObject(objClass);
        //     return SObjectRef.FromObjectPointer(classOop);
        // }

        var data = GetObjectData(oop);
        return data.ClassRef;
    }

    /// <summary>
    /// Gets the format of an object.
    /// </summary>
    public static ObjectFormat GetFormat(nint oop)
    {
        var data = GetObjectData(oop);
        return data.Format;
    }

    #endregion

    #region Garbage Collection

    /// <summary>
    /// Implements the become: primitive (#72).
    /// Swaps the identities of two objects by swapping their OOPs.
    /// </summary>
    public static void Become(nint oop1, nint oop2)
    {
        if (oop1 == 0 || oop2 == 0)
            throw new ArgumentException("Cannot become: with nil");

        lock (allocationLock)
        {
            var data1 = GetObjectData(oop1);
            var data2 = GetObjectData(oop2);

            // Swap the object data
            objectTable[ObjectTable.IndexFromOop(oop1)] = data2;
            objectTable[ObjectTable.IndexFromOop(oop2)] = data1;

            // Note: All existing SObjectRef instances pointing to oop1 now resolve to data2
            // and vice versa. This is the magic of the indirection!
        }
    }

    /// <summary>
    /// Performs mark-and-sweep garbage collection.
    /// </summary>
    public static void CollectGarbage(SObjectRef[] roots)
    {
        // Mark phase: clear all mark bits
        foreach (var data in objectTable.Values)
        {
            data.Marked = false;
        }

        // Mark phase: mark all reachable objects from roots
        foreach (var root in roots)
        {
            if (!root.IsNil && root.IsHeapObject)
            {
                MarkObject(root.AsObjectPointer());
            }
        }

        // Sweep phase: remove unmarked objects
        var deadOops = new List<nint>();
        foreach (var kvp in objectTable)
        {
            if (!kvp.Value.Marked)
            {
                deadOops.Add(kvp.Key);
            }
        }

        foreach (var oop in deadOops)
        {
            objectTable.TryRemove(ObjectTable.IndexFromOop(oop), out _);
        }

        if (Program.RuntimeOptions?.EnableGCLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] GC: Removed {deadOops.Count} dead objects, {objectTable.Count} live objects");
        }
    }

    /// <summary>
    /// Recursively marks an object and all objects it references.
    /// </summary>
    public static void MarkObject(nint oop)
    {
        if (!IsValidOop(oop))
            return;

        var data = GetObjectData(oop);

        if (data.Marked)
            return;  // Already marked

        data.Marked = true;

        // Mark class
        if (!data.ClassRef.IsNil && data.ClassRef.IsHeapObject)
        {
            MarkObject(data.ClassRef.AsObjectPointer());
        }

        // Mark instance variables
        foreach (var instVar in data.InstanceVariables)
        {
            if (!instVar.IsNil && instVar.IsHeapObject)
            {
                MarkObject(instVar.AsObjectPointer());
            }
        }

        // Mark indexed pointers (if any)
        if (data.Format == ObjectFormat.PointerIndexed && data.IndexedData != null)
        {
            var pointers = (SObjectRef[])data.IndexedData;
            foreach (var ptr in pointers)
            {
                if (!ptr.IsNil && ptr.IsHeapObject)
                {
                    MarkObject(ptr.AsObjectPointer());
                }
            }
        }
    }

    /// <summary>
    /// Checks if an object is marked for garbage collection.
    /// </summary>
    public static bool IsMarked(nint oop)
    {
        if (!IsValidOop(oop))
            return false;

        var data = GetObjectData(oop);
        return data.Marked;
    }

    /// <summary>
    /// Clears the mark bit for an object.
    /// </summary>
    public static void ClearMark(nint oop)
    {
        if (!IsValidOop(oop))
            return;

        var data = GetObjectData(oop);
        data.Marked = false;
    }

    #endregion

    #region Compatibility Methods (for obsolete SObjectRef methods)

    /// <summary>
    /// Gets an SObject for a given OOP.
    /// Only works for bootstrap objects created during cold start.
    /// This is a compatibility method for the mixed SObject/SObjectRef architecture.
    /// </summary>
    public static SObject? GetObject(nint oop)
    {
        if (oop == 0)
            return null;

        // Check bootstrap map first
        if (bootstrapObjectMap.TryGetValue(oop, out var bootstrapObj))
            return bootstrapObj;

        // For non-bootstrap objects, we cannot convert them back to SObject
        // This is a limitation of the new architecture
        var stackTrace = Environment.StackTrace;
        throw new NotSupportedException(
            $"GetObject is not supported for dynamically allocated objects (OOP={oop}). " +
            "Only bootstrap objects registered with RegisterBootstrapObject can be retrieved. " +
            $"Use SObjectRef methods instead of SObject throughout the codebase.\nStack trace:\n{stackTrace}");
    }

    /// <summary>
    /// Gets the OOP for a given SObject.
    /// Only works for bootstrap objects created during cold start.
    /// This is a compatibility method for the mixed SObject/SObjectRef architecture.
    /// </summary>
    public static nint GetOop(SObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        // Search the bootstrap map for this object
        foreach (var kvp in bootstrapObjectMap)
        {
            if (ReferenceEquals(kvp.Value, obj))
                return kvp.Key;
        }

        // Not found - register it
        return RegisterBootstrapObject(obj);
    }

    #endregion

    /// <summary>
    /// TEMPORARY: Registers a bootstrap object that was created outside of ObjectMemory.
    /// This is ONLY for SystemDictionary initialization during cold start.
    /// This method will be removed once all object creation goes through ObjectMemory.
    /// </summary>
    internal static nint RegisterBootstrapObject(SObject obj)
    {
        if (obj == null)
            throw new ArgumentNullException(nameof(obj));

        lock (allocationLock)
        {
            // Check if we already have this object registered
            foreach (var kvp in bootstrapObjectMap)
            {
                if (ReferenceEquals(kvp.Value, obj))
                    return kvp.Key;
            }

            // Allocate next available OOP (ensure 4-byte alignment)
            nint oop = nextOop;
            nextOop += 4;  // Next OOP (4-byte aligned)

            // Store in bootstrap map
            bootstrapObjectMap[oop] = obj;

            return oop;
        }
    }

    // Temporary map for bootstrap objects
    private static readonly Dictionary<nint, SObject> bootstrapObjectMap = new();

    #region CompiledMethod Helpers

    /// <summary>
    /// Gets the literals array from a CompiledMethod.
    /// </summary>
    public static SObjectRef[] GetCompiledMethodLiterals(nint oop)
    {
        var data = GetObjectData(oop);
        if (data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException("Object is not a CompiledMethod");

        // For CompiledMethods, instance variables are the literals
        return data.InstanceVariables;
    }

    /// <summary>
    /// Gets the bytecodes from a CompiledMethod.
    /// </summary>
    public static byte[] GetCompiledMethodBytecodes(nint oop)
    {
        var data = GetObjectData(oop);
        if (data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException("Object is not a CompiledMethod");

        // For CompiledMethods, indexed data contains:
        // - 2 bytes: header
        // - 2 bytes: primitive index (if bit 15 of header is set)
        // - N bytes: bytecodes
        var fullData = (byte[])data.IndexedData!;

        if (fullData.Length < 2)
            return Array.Empty<byte>();

        // Check if extended header (bit 15 set)
        var header = (ushort)(fullData[0] | (fullData[1] << 8));
        var hasPrimitive = (header & 0x8000) != 0;
        var headerSize = hasPrimitive ? 4 : 2;

        if (fullData.Length <= headerSize)
            return Array.Empty<byte>();

        var bytecodes = new byte[fullData.Length - headerSize];
        Array.Copy(fullData, headerSize, bytecodes, 0, bytecodes.Length);
        return bytecodes;
    }

    /// <summary>
    /// Gets the header from a CompiledMethod.
    /// The header is stored as the first two bytes of the bytecode array.
    /// </summary>
    public static ushort GetCompiledMethodHeader(nint oop)
    {
        var data = GetObjectData(oop);
        if (data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException("Object is not a CompiledMethod");

        var bytes = (byte[])data.IndexedData!;
        if (bytes.Length < 2)
            return 0; // No header

        // Header is stored in the first two bytes (little-endian)
        return (ushort)(bytes[0] | (bytes[1] << 8));
    }

    #endregion

    #region Statistics and Debugging

    /// <summary>
    /// Gets statistics about the object memory.
    /// </summary>
    public static (int LiveObjects, int AllocatedOops) GetStatistics()
    {
        int liveCount = objectTable.Count;
        int allocatedOops = (int)((nextOop - 0x1000) / 4);
        return (liveCount, allocatedOops);
    }

    /// <summary>
    /// Clears all object mappings (for testing/reset only).
    /// </summary>
    internal static void Clear()
    {
        objectTable.Clear();
        nextOop = 0x1000;
        bootstrapSymbolTable.Clear();
        methodCache.Clear();
    }

    #endregion

    #region Symbol Table

    /// <summary>
    /// BOOTSTRAP-ONLY temporary symbol table used before USTable is created.
    /// Once USTable exists in Symbol class classPool, this is no longer used.
    /// All symbols created during bootstrap are migrated to USTable.
    /// </summary>
    private static readonly Dictionary<string, nint> bootstrapSymbolTable = new();

    /// <summary>
    /// Interns a string as a Symbol, returning an existing Symbol if one exists
    /// for the string, or creating a new one if not.
    ///
    /// ARCHITECTURE: Symbols are stored in USTable (Symbol class -> classPool -> #USTable).
    /// During bootstrap (before USTable exists), we temporarily use bootstrapSymbolTable.
    /// </summary>
    /// <param name="str">The string to intern as a Symbol.</param>
    /// <returns>An SObjectRef to the Symbol object.</returns>
    public static SObjectRef InternSymbol(string str)
    {
        // Try to get USTable from Symbol class classPool
        var usTableRef = ColdStart.SymbolTableBuilder.GetUSTable();

        if (usTableRef.HasValue && !usTableRef.Value.IsNil)
        {
            // USTable exists - use heap-based symbol table
            return InternSymbolInUSTable(str, usTableRef.Value);
        }
        else
        {
            // USTable doesn't exist yet (early bootstrap) - use temporary C# table
            return InternSymbolBootstrap(str);
        }
    }

    /// <summary>
    /// Bootstrap-only symbol interning using C# dictionary.
    /// Used only during early cold start before USTable is created.
    /// </summary>
    private static SObjectRef InternSymbolBootstrap(string str)
    {
        if (bootstrapSymbolTable.TryGetValue(str, out var oop))
            return SObjectRef.FromObjectPointer(oop);

        // Create new symbol
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var symbolOop = AllocateObject(
            GetSymbolClass(),
            ObjectFormat.ByteIndexed,
            0,
            bytes.Length);

        SetByteIndexedData(symbolOop, bytes);
        bootstrapSymbolTable[str] = symbolOop;
        return SObjectRef.FromObjectPointer(symbolOop);
    }

    /// <summary>
    /// Interns a symbol using the heap-based USTable.
    /// This is the correct Smalltalk-80 implementation.
    /// </summary>
    private static SObjectRef InternSymbolInUSTable(string str, SObjectRef usTable)
    {
        // Hash the string to find bucket
        int hash = str.GetHashCode();
        int bucketCount = GetIndexedSize(usTable.AsObjectPointer());
        int bucketIndex = (hash & 0x7FFFFFFF) % bucketCount;

        // Get the bucket (Array of Symbol instances)
        var bucket = usTable.GetIndexedPointer(bucketIndex);

        if (!bucket.IsNil)
        {
            // Search bucket for existing symbol
            int bucketSize = GetIndexedSize(bucket.AsObjectPointer());
            for (int i = 0; i < bucketSize; i++)
            {
                var symbolRef = bucket.GetIndexedPointer(i);
                if (!symbolRef.IsNil)
                {
                    // Compare symbol string value
                    var symbolStr = SymbolToString(symbolRef);
                    if (symbolStr == str)
                    {
                        return symbolRef; // Found existing symbol
                    }
                }
            }
        }

        // Symbol not found - create new one and add to bucket
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var newSymbolOop = AllocateObject(
            GetSymbolClass(),
            ObjectFormat.ByteIndexed,
            0,
            bytes.Length);
        SetByteIndexedData(newSymbolOop, bytes);
        var newSymbol = SObjectRef.FromObjectPointer(newSymbolOop);

        // Add to bucket (grow bucket array if needed)
        var arrayClassRef = SystemDictionary.ArrayClassRef;
        if (bucket.IsNil)
        {
            // Create new bucket with single symbol
            var newBucketOop = AllocateObject(arrayClassRef, ObjectFormat.PointerIndexed, 0, 1);
            var newBucket = SObjectRef.FromObjectPointer(newBucketOop);
            newBucket.SetIndexedPointer(0, newSymbol);
            usTable.SetIndexedPointer(bucketIndex, newBucket);
        }
        else
        {
            // Grow existing bucket
            int oldSize = GetIndexedSize(bucket.AsObjectPointer());
            var newBucketOop = AllocateObject(arrayClassRef, ObjectFormat.PointerIndexed, 0, oldSize + 1);
            var newBucket = SObjectRef.FromObjectPointer(newBucketOop);

            // Copy old symbols
            for (int i = 0; i < oldSize; i++)
            {
                newBucket.SetIndexedPointer(i, bucket.GetIndexedPointer(i));
            }

            // Add new symbol
            newBucket.SetIndexedPointer(oldSize, newSymbol);
            usTable.SetIndexedPointer(bucketIndex, newBucket);
        }

        return newSymbol;
    }

    /// <summary>
    /// Migrates all bootstrap symbols to USTable.
    /// Called after USTable is created during cold start.
    /// IMPORTANT: Adds the EXISTING bootstrap symbol objects to USTable, not new copies.
    /// </summary>
    public static void MigrateBootstrapSymbolsToUSTable()
    {
        var usTableRef = ColdStart.SymbolTableBuilder.GetUSTable();
        if (!usTableRef.HasValue || usTableRef.Value.IsNil)
        {
            throw new InvalidOperationException("USTable must exist before migrating bootstrap symbols");
        }

        var usTable = usTableRef.Value;
        var bucketCount = GetIndexedSize(usTable.AsObjectPointer());
        var arrayClassRef = SystemDictionary.ArrayClassRef;

        int migratedCount = 0;
        foreach (var (str, oop) in bootstrapSymbolTable)
        {
            // Hash the string to find bucket
            int hash = str.GetHashCode();
            int bucketIndex = (hash & 0x7FFFFFFF) % bucketCount;

            // Get the bucket (Array of Symbol instances)
            var bucket = usTable.GetIndexedPointer(bucketIndex);
            var symbolRef = SObjectRef.FromObjectPointer(oop);

            // Add existing bootstrap symbol to bucket
            if (bucket.IsNil)
            {
                // Create new bucket with single symbol
                var newBucketOop = AllocateObject(arrayClassRef, ObjectFormat.PointerIndexed, 0, 1);
                var newBucket = SObjectRef.FromObjectPointer(newBucketOop);
                newBucket.SetIndexedPointer(0, symbolRef);
                usTable.SetIndexedPointer(bucketIndex, newBucket);
            }
            else
            {
                // Grow existing bucket
                int oldSize = GetIndexedSize(bucket.AsObjectPointer());
                var newBucketOop = AllocateObject(arrayClassRef, ObjectFormat.PointerIndexed, 0, oldSize + 1);
                var newBucket = SObjectRef.FromObjectPointer(newBucketOop);

                // Copy old symbols
                for (int i = 0; i < oldSize; i++)
                {
                    newBucket.SetIndexedPointer(i, bucket.GetIndexedPointer(i));
                }

                // Add bootstrap symbol
                newBucket.SetIndexedPointer(oldSize, symbolRef);
                usTable.SetIndexedPointer(bucketIndex, newBucket);
            }

            migratedCount++;
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ObjectMemory] Migrated {migratedCount} bootstrap symbols to USTable");
        }

        // Clear bootstrap table - no longer needed
        bootstrapSymbolTable.Clear();
    }

    /// <summary>
    /// Converts a Symbol object to its string representation.
    /// </summary>
    /// <param name="symbolRef">The Symbol reference to convert.</param>
    /// <returns>The string representation of the Symbol.</returns>
    /// <exception cref="InvalidOperationException">If the reference is not a Symbol.</exception>
    public static string SymbolToString(SObjectRef symbolRef)
    {
        if (!symbolRef.IsHeapObject)
            throw new InvalidOperationException("Symbol must be a heap object");

        var bytes = GetByteIndexedData(symbolRef.AsObjectPointer());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    #endregion

    #region Well-Known Class References

    // Well-known class references for fast access
    private static SObjectRef nilClassRef;
    private static SObjectRef trueClassRef;
    private static SObjectRef falseClassRef;
    private static SObjectRef smallIntegerClassRef;
    private static SObjectRef arrayClassRef;
    private static SObjectRef symbolClassRef;
    private static SObjectRef stringClassRef;
    private static SObjectRef dictionaryClassRef;
    private static SObjectRef classClassRef;
    private static SObjectRef metaclassClassRef;
    private static SObjectRef compiledMethodClassRef;
    private static SObjectRef methodDictionaryClassRef;
    private static SObjectRef associationClassRef;

    /// <summary>
    /// Gets the Class class reference.
    /// </summary>
    public static SObjectRef GetClassClass() => classClassRef;

    /// <summary>
    /// Gets the Metaclass class reference.
    /// </summary>
    public static SObjectRef GetMetaclassClass() => metaclassClassRef;

    /// <summary>
    /// Gets the Symbol class reference.
    /// </summary>
    public static SObjectRef GetSymbolClass() => symbolClassRef;

    /// <summary>
    /// Gets the Array class reference.
    /// </summary>
    public static SObjectRef GetArrayClass() => arrayClassRef;

    /// <summary>
    /// Gets the Dictionary class reference.
    /// </summary>
    public static SObjectRef GetDictionaryClass() => dictionaryClassRef;

    /// <summary>
    /// Gets the CompiledMethod class reference.
    /// </summary>
    public static SObjectRef GetCompiledMethodClass() => compiledMethodClassRef;

    /// <summary>
    /// Gets the MethodDictionary class reference.
    /// </summary>
    public static SObjectRef GetMethodDictionaryClass() => methodDictionaryClassRef;

    /// <summary>
    /// Gets the Association class reference.
    /// </summary>
    public static SObjectRef GetAssociationClass() => associationClassRef;

    /// <summary>
    /// Gets the String class reference.
    /// </summary>
    public static SObjectRef GetStringClass() => stringClassRef;

    /// <summary>
    /// Gets the SmallInteger class reference.
    /// </summary>
    public static SObjectRef GetSmallIntegerClass() => smallIntegerClassRef;

    /// <summary>
    /// Gets the UndefinedObject (nil) class reference.
    /// </summary>
    public static SObjectRef GetNilClass() => nilClassRef;

    /// <summary>
    /// Gets the True class reference.
    /// </summary>
    public static SObjectRef GetTrueClass() => trueClassRef;

    /// <summary>
    /// Gets the False class reference.
    /// </summary>
    public static SObjectRef GetFalseClass() => falseClassRef;

    /// <summary>
    /// Sets a well-known class reference. Called during bootstrap to cache
    /// commonly used class references for performance.
    /// </summary>
    /// <param name="name">The name of the class.</param>
    /// <param name="classRef">The reference to the class object.</param>
    internal static void SetWellKnownClass(string name, SObjectRef classRef)
    {
        switch (name)
        {
            case "Class":
                classClassRef = classRef;
                break;
            case "Metaclass":
                metaclassClassRef = classRef;
                break;
            case "Symbol":
                symbolClassRef = classRef;
                break;
            case "Array":
                arrayClassRef = classRef;
                break;
            case "Dictionary":
                dictionaryClassRef = classRef;
                break;
            case "CompiledMethod":
                compiledMethodClassRef = classRef;
                break;
            case "MethodDictionary":
                methodDictionaryClassRef = classRef;
                break;
            case "Association":
                associationClassRef = classRef;
                break;
            case "String":
                stringClassRef = classRef;
                break;
            case "SmallInteger":
                smallIntegerClassRef = classRef;
                break;
            case "UndefinedObject":
                nilClassRef = classRef;
                break;
            case "True":
                trueClassRef = classRef;
                break;
            case "False":
                falseClassRef = classRef;
                break;
        }
    }

    #endregion

    #region Dictionary Operations

    /// <summary>
    /// Performs a dictionary lookup operation (at:).
    /// Searches for an Association with the given key and returns its value.
    /// </summary>
    /// <param name="dictionaryRef">The dictionary to search in.</param>
    /// <param name="key">The key to search for.</param>
    /// <returns>The value associated with the key, or nil if not found.</returns>
    public static SObjectRef DictionaryAt(SObjectRef dictionaryRef, SObjectRef key)
    {
        if (!dictionaryRef.IsHeapObject)
            return SObjectRef.Nil;

        // Dictionaries are implemented as Arrays of Associations
        // We need to search through the indexed portion for an Association with matching key
        var size = GetIndexedSize(dictionaryRef.AsObjectPointer());

        for (int i = 0; i < size; i++)
        {
            var association = GetIndexedPointer(dictionaryRef.AsObjectPointer(), i);
            if (association.IsNil)
                continue;

            // Association has two instance variables: key and value
            Generated.Layouts.STAssociation assoc = association;
            var assocKey = assoc.Key;

            // Check for key equality (identity comparison for now, should use = in real implementation)
            if (SObjectRef.AreIdentical(assocKey, key))
            {
                return assoc.Value;
            }
        }

        return SObjectRef.Nil;
    }

    /// <summary>
    /// Performs a dictionary store operation (at:put:).
    /// Stores or updates an Association with the given key and value.
    /// </summary>
    /// <param name="dictionaryRef">The dictionary to store in.</param>
    /// <param name="key">The key to store.</param>
    /// <param name="value">The value to associate with the key.</param>
    public static void DictionaryAtPut(SObjectRef dictionaryRef, SObjectRef key, SObjectRef value)
    {
        if (!dictionaryRef.IsHeapObject)
            throw new InvalidOperationException("Dictionary must be a heap object");

        var size = GetIndexedSize(dictionaryRef.AsObjectPointer());

        // First, try to find existing association
        for (int i = 0; i < size; i++)
        {
            var association = GetIndexedPointer(dictionaryRef.AsObjectPointer(), i);
            if (association.IsNil)
            {
                // Found empty slot, create new association
                var newAssoc = AllocateObject(
                    GetAssociationClass(),
                    ObjectFormat.FixedPointer,
                    Generated.Layouts.STAssociation.InstanceVarCount,
                    0);

                Generated.Layouts.STAssociation newAssocStruct = SObjectRef.FromObjectPointer(newAssoc);
                newAssocStruct.Key = key;
                newAssocStruct.Value = value;
                SetIndexedPointer(dictionaryRef.AsObjectPointer(), i, SObjectRef.FromObjectPointer(newAssoc));
                return;
            }

            // Check if this association has matching key
            Generated.Layouts.STAssociation assoc = association;
            if (SObjectRef.AreIdentical(assoc.Key, key))
            {
                // Update existing association
                assoc.Value = value;
                return;
            }
        }

        // Dictionary is full, would need to grow it
        // For now, throw an exception
        throw new InvalidOperationException("Dictionary is full and growing is not yet implemented");
    }

    /// <summary>
    /// Removes a key from a dictionary (removeKey:ifAbsent:).
    /// Sets the association to nil if found.
    /// </summary>
    /// <param name="dictionaryRef">The dictionary to remove from.</param>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was found and removed, false otherwise.</returns>
    public static bool DictionaryRemoveKey(SObjectRef dictionaryRef, SObjectRef key)
    {
        if (!dictionaryRef.IsHeapObject)
            return false;

        var size = GetIndexedSize(dictionaryRef.AsObjectPointer());

        // Search for the association with matching key
        for (int i = 0; i < size; i++)
        {
            var association = GetIndexedPointer(dictionaryRef.AsObjectPointer(), i);
            if (association.IsNil)
                continue;

            // Check if this association has matching key
            Generated.Layouts.STAssociation assoc = association;
            if (SObjectRef.AreIdentical(assoc.Key, key))
            {
                // Found it - set slot to nil to remove
                SetIndexedPointer(dictionaryRef.AsObjectPointer(), i, SObjectRef.Nil);
                return true;
            }
        }

        return false;
    }

    #endregion

    #region Class Creation

    /// <summary>
    /// Creates a new Class object with the specified properties.
    /// </summary>
    /// <param name="name">The name of the class as a string.</param>
    /// <param name="superclassRef">Reference to the superclass, or nil for no superclass.</param>
    /// <param name="instanceVariableNames">Array of instance variable names.</param>
    /// <param name="instanceFormat">The format for instances of this class.</param>
    /// <returns>An SObjectRef to the newly created class.</returns>
    public static SObjectRef CreateClassObject(
        string name,
        SObjectRef superclassRef,
        string[] instanceVariableNames,
        ObjectFormat instanceFormat)
    {
        // Get Class class (may be Nil during bootstrap)
        var classClass = GetClassClass();
        if (classClass.IsNil && name != "Class")
        {
            // During bootstrap, classes don't have a proper class yet
            // This is fixed later after Class class is created
            classClass = SObjectRef.Nil;
        }

        // Create the class object
        var classOop = AllocateObject(
            classClass,
            ObjectFormat.FixedPointer,
            Core.BlueBook.ClassLayout.InstanceVariableCount,
            0);

        var classRef = SObjectRef.FromObjectPointer(classOop);

        // Set superclass
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.Superclass, superclassRef);

        // Create and set method dictionary (empty initially)
        var methodDictClass = GetMethodDictionaryClass();
        int methodDictInstVars = 2;  // MethodDictionary has 2 instance variables: tally and array
        if (methodDictClass.IsNil)
        {
            // MethodDictionary class doesn't exist yet (bootstrap phase)
            // Create a minimal method dictionary without a proper class
            methodDictClass = SObjectRef.Nil;
        }
        var methodDict = AllocateObject(
            methodDictClass,
            ObjectFormat.PointerIndexed,
            methodDictInstVars,  // tally (0) and valueArray (1)
            128);  // Start with capacity for 128 methods (increased from 16 to avoid resizing during cold start)

        // Initialize method dictionary: set tally to 0 and create valueArray
        SetInstanceVariable(methodDict, 0, SObjectRef.FromSmallInteger(0));  // tally = 0

        // Create valueArray (parallel array of values)
        var valueArrayClass = GetArrayClass();
        if (valueArrayClass.IsNil)
            valueArrayClass = SObjectRef.Nil;  // Bootstrap phase

        var valueArray = AllocateObject(
            valueArrayClass,
            ObjectFormat.PointerIndexed,
            0,
            128);  // Same capacity as method dictionary
        SetInstanceVariable(methodDict, 1, SObjectRef.FromObjectPointer(valueArray));  // valueArray

        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.MethodDictionary, SObjectRef.FromObjectPointer(methodDict));

        // Calculate and set format
        int instVarCount = instanceVariableNames.Length;
        if (!superclassRef.IsNil)
        {
            // Add superclass instance variable count
            var superFormat = GetInstanceVariable(superclassRef.AsObjectPointer(), Core.BlueBook.ClassLayout.Format);
            if (superFormat.IsSmallInteger)
            {
                int superInstVars = superFormat.AsSmallInteger() & 0x3F; // Extract inst var count (bits 0-5)
                instVarCount += superInstVars;
            }
        }

        // Set subclasses to nil (will be populated when subclasses are created)
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.Subclasses, SObjectRef.Nil);

        // Encode format as SmallInteger
        // Format encoding: bits 0-5 = inst var count, bits 6-7 = indexability, bit 8 = pointers flag
        int formatValue = instVarCount & 0x3F;  // Instance variable count (6 bits)
        if (instanceFormat != ObjectFormat.FixedPointer)
        {
            formatValue |= 0x40;  // Set indexable bit
            if (instanceFormat == ObjectFormat.ByteIndexed)
                formatValue |= 0x80;  // Set byte-indexed bit
        }
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.Format, SObjectRef.FromSmallInteger(formatValue));

        // Create instance variables array
        var arrayClass = GetArrayClass();
        if (arrayClass.IsNil)
        {
            // Array class doesn't exist yet (bootstrap phase)
            arrayClass = SObjectRef.Nil;
        }
        var instVarArray = AllocateObject(
            arrayClass,
            ObjectFormat.PointerIndexed,
            0,
            instanceVariableNames.Length);

        for (int i = 0; i < instanceVariableNames.Length; i++)
        {
            var symbolRef = InternSymbol(instanceVariableNames[i]);
            SetIndexedPointer(instVarArray, i, symbolRef);
        }
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.InstanceVariables, SObjectRef.FromObjectPointer(instVarArray));

        // Set organization to nil (will be set later if needed)
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.Organization, SObjectRef.Nil);

        // Set name
        var nameSymbol = InternSymbol(name);
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.Name, nameSymbol);

        // Create empty class pool dictionary
        var dictionaryClass = GetDictionaryClass();
        int classPoolInstVars = 1;  // Dictionary inherits 'tally' from Set
        if (dictionaryClass.IsNil)
        {
            // Dictionary class doesn't exist yet (bootstrap phase)
            dictionaryClass = SObjectRef.Nil;
        }
        var classPool = AllocateObject(
            dictionaryClass,
            ObjectFormat.PointerIndexed,
            classPoolInstVars,  // tally (0)
            8);  // Start with capacity for 8 class variables

        // Initialize dictionary: set tally to 0
        SetInstanceVariable(classPool, 0, SObjectRef.FromSmallInteger(0));  // tally = 0

        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.ClassPool, SObjectRef.FromObjectPointer(classPool));

        // Set shared pools to empty array
        // Reuse arrayClass from above
        var sharedPools = AllocateObject(
            arrayClass,  // Already checked for Nil above
            ObjectFormat.PointerIndexed,
            0,
            0);
        SetInstanceVariable(classOop, Core.BlueBook.ClassLayout.SharedPools, SObjectRef.FromObjectPointer(sharedPools));

        return classRef;
    }

    /// <summary>
    /// Creates a new Metaclass object for the specified class.
    /// </summary>
    /// <param name="classRef">The class this metaclass is for.</param>
    /// <param name="superMetaclassRef">Reference to the superclass's metaclass.</param>
    /// <returns>An SObjectRef to the newly created metaclass.</returns>
    public static SObjectRef CreateMetaclassObject(SObjectRef classRef, SObjectRef superMetaclassRef)
    {
        // Create the metaclass object
        var metaclassOop = AllocateObject(
            GetMetaclassClass(),
            ObjectFormat.FixedPointer,
            Core.BlueBook.MetaclassLayout.InstanceVariableCount,
            0);

        var metaclassRef = SObjectRef.FromObjectPointer(metaclassOop);

        // Copy all Class instance variables from the Metaclass class
        // Then set the additional thisClass instance variable
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.Superclass, superMetaclassRef);

        // Create empty method dictionary for metaclass
        var methodDict = AllocateObject(
            GetMethodDictionaryClass(),
            ObjectFormat.PointerIndexed,
            0,
            8);  // Start with smaller capacity for class methods
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.MethodDictionary, SObjectRef.FromObjectPointer(methodDict));

        // Set format (metaclasses have 0 instance variables)
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.Format, SObjectRef.Zero);

        // Set subclasses to nil
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.Subclasses, SObjectRef.Nil);

        // Set empty instance variables array (Metaclass doesn't define any instance variables for its instances)
        var emptyArray = AllocateObject(GetArrayClass(), ObjectFormat.PointerIndexed, 0, 0);
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.InstanceVariables, SObjectRef.FromObjectPointer(emptyArray));

        // Set organization to nil
        SetInstanceVariable(metaclassOop, Core.BlueBook.ClassLayout.Organization, SObjectRef.Nil);

        // Set the thisClass instance variable (field 6 in Metaclass)
        // NOTE: Metaclass does NOT have name, classPool, or sharedPools fields!
        // Metaclass inherits from ClassDescription (6 fields), not Class (9 fields)
        SetInstanceVariable(metaclassOop, Core.BlueBook.MetaclassLayout.ThisClass, classRef);

        return metaclassRef;
    }

    #endregion

    #region Method Cache

    /// <summary>
    /// Method cache for fast method lookup.
    /// Key is (classOop, selector), value is method OOP.
    /// </summary>
    private static readonly Dictionary<(nint classOop, string selector), nint> methodCache = new();

    /// <summary>
    /// Tries to get a cached method for the given class and selector.
    /// </summary>
    /// <param name="key">The cache key (classOop, selector).</param>
    /// <param name="method">The cached method if found.</param>
    /// <returns>True if a cached method was found, false otherwise.</returns>
    public static bool TryGetCachedMethod((nint classOop, string selector) key, out SObjectRef method)
    {
        if (methodCache.TryGetValue(key, out var methodOop))
        {
            method = SObjectRef.FromObjectPointer(methodOop);
            return true;
        }

        method = SObjectRef.Nil;
        return false;
    }

    /// <summary>
    /// Caches a method for fast lookup.
    /// </summary>
    /// <param name="key">The cache key (classOop, selector).</param>
    /// <param name="method">The method to cache.</param>
    public static void CacheMethod((nint classOop, string selector) key, SObjectRef method)
    {
        if (!method.IsHeapObject)
            return;

        methodCache[key] = method.AsObjectPointer();
    }

    /// <summary>
    /// Clears cached methods for a specific class and selector.
    /// Called when a method is added or removed.
    /// </summary>
    /// <param name="classOop">The class OOP.</param>
    /// <param name="selector">The selector to clear, or null to clear all for the class.</param>
    public static void ClearMethodCache(nint classOop, string? selector = null)
    {
        if (selector != null)
        {
            methodCache.Remove((classOop, selector));
        }
        else
        {
            // Clear all entries for this class
            var keysToRemove = methodCache.Keys
                .Where(k => k.classOop == classOop)
                .ToList();

            foreach (var key in keysToRemove)
            {
                methodCache.Remove(key);
            }
        }
    }

    /// <summary>
    /// Clears the entire method cache.
    /// </summary>
    public static void ClearAllMethodCache()
    {
        methodCache.Clear();
    }

    #endregion

    #region Byte-Indexed Data Access

    /// <summary>
    /// Gets the byte array data from a byte-indexed object.
    /// </summary>
    /// <param name="oop">The OOP of the byte-indexed object.</param>
    /// <returns>The byte array data.</returns>
    /// <exception cref="InvalidOperationException">If the object is not byte-indexed.</exception>
    public static byte[] GetByteIndexedData(nint oop)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.ByteIndexed && data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format {data.Format} does not support byte indexing");

        if (data.IndexedData == null)
            return Array.Empty<byte>();

        if (data.Format == ObjectFormat.CompiledMethod)
        {
            // For CompiledMethod, return all bytes including header
            return (byte[])data.IndexedData;
        }

        return (byte[])data.IndexedData;
    }

    /// <summary>
    /// Sets the byte array data for a byte-indexed object.
    /// </summary>
    /// <param name="oop">The OOP of the byte-indexed object.</param>
    /// <param name="bytes">The byte array to set.</param>
    /// <exception cref="InvalidOperationException">If the object is not byte-indexed.</exception>
    public static void SetByteIndexedData(nint oop, byte[] bytes)
    {
        var data = GetObjectData(oop);

        if (data.Format != ObjectFormat.ByteIndexed && data.Format != ObjectFormat.CompiledMethod)
            throw new InvalidOperationException($"Object format {data.Format} does not support byte indexing");

        if (bytes == null)
            throw new ArgumentNullException(nameof(bytes));

        // Create a copy of the bytes to ensure immutability
        var bytesCopy = new byte[bytes.Length];
        Array.Copy(bytes, bytesCopy, bytes.Length);

        data.IndexedData = bytesCopy;
    }

    /// <summary>
    /// Creates a String object from a C# string.
    /// </summary>
    /// <param name="str">The string to convert.</param>
    /// <returns>An SObjectRef to a String object.</returns>
    public static SObjectRef CreateString(string str)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(str);
        var stringOop = AllocateObject(
            GetStringClass(),
            ObjectFormat.ByteIndexed,
            0,
            bytes.Length);

        SetByteIndexedData(stringOop, bytes);
        return SObjectRef.FromObjectPointer(stringOop);
    }

    /// <summary>
    /// Converts a String object to a C# string.
    /// </summary>
    /// <param name="stringRef">The String reference to convert.</param>
    /// <returns>The C# string representation.</returns>
    public static string StringToString(SObjectRef stringRef)
    {
        if (!stringRef.IsHeapObject)
            throw new InvalidOperationException("String must be a heap object");

        var bytes = GetByteIndexedData(stringRef.AsObjectPointer());
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    #endregion
}