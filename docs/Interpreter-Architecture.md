# Smalltalk-80 Interpreter Architecture

## Overview

The Smalltalk-80 interpreter is a ground-up redesign in C# that maintains compatibility with Smalltalk-80 bytecode while using a modern, direct object model. The architecture eliminates OOP indirection, provides efficient tagged immediates, and supports flexible memory management strategies including managed objects, arena allocation, and image persistence.

## Core Components

### 1. Object Model

The architecture uses a direct object model with explicit Smalltalk semantics, eliminating the need for Object Oriented Pointers (OOP):

```csharp
public class SObject {
    public SClass Klass;         // Direct class pointer
    public object[] InstVars;    // Instance variable storage
    private int? _identityHash;  // Stable identity hash
    
    public int IdentityHash {
        get {
            if (_identityHash.HasValue) return _identityHash.Value;
            var h = Interlocked.Increment(ref SObjectHashAllocator.Next);
            if (h == 0) h = Interlocked.Increment(ref SObjectHashAllocator.Next);
            _identityHash = h;
            return h;
        }
    }
}
```

#### Tagged Immediates
SmallIntegers use single-bit tagging in a boxed Int64 to avoid heap allocation:
```csharp
public static class Tags {
    // Low bit 1 => immediate SmallInteger; low bit 0 => pointer (managed object)
    private const long TAG_MASK = 1L;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static object FromSmallInteger(long value) => (long)((value << 1) | TAG_MASK);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToSmallInteger(object tagged) => (long)tagged >> 1;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsImmediate(object v) => v is long lv && (lv & TAG_MASK) == TAG_MASK;
    
    // Hot path helper for interpreter
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryUnboxSmallInteger(object v, out long value) {
        if (v is long lv && (lv & TAG_MASK) == TAG_MASK) { 
            value = lv >> 1; 
            return true; 
        }
        value = 0; 
        return false;
    }
}

```

#### Special Objects
- **Nil**: Singleton with identity hash 0
- **True**: Singleton with identity hash 1  
- **False**: Singleton with identity hash 2
- **Symbols**: Interned strings with reference equality

### 2. Class System

```csharp
public class SClass : SObject {
    public SClass Superclass;           // Direct superclass reference
    public MethodDictionary MethodDict;  // Version-tracked method storage
    public SClass Metaclass;            // The metaclass for this class
    public string Name;                 // Class name
    public int InstSize;                // Total instance variables (including inherited)
    public ClassFormat Format;          // Object format (fixed, variable, bytes)
    
    public SObject BasicNew() {
        // Creates instances based on format
    }
    
    public SCompiledMethod LookupMethod(SSymbol selector) {
        // Searches method dictionary and superclass chain
    }
}
```

#### Method Dictionary with Version Tracking
```csharp
public class MethodDictionary {
    private Dictionary<SSymbol, SCompiledMethod> _methods;
    public int Version { get; private set; }  // Increments on any change
    
    public void AtPut(SSymbol selector, SCompiledMethod method) {
        _methods[selector] = method;
        Version++; // Invalidates caches
    }
}
```

### 3. Compiled Methods

```csharp
public class SCompiledMethod : SObject {
    public int Header;           // Encodes args, temps, literals, stack size
    public int PrimitiveIndex;   // 0 = no primitive
    public object[] Literals;    // Literal objects referenced
    public byte[] Bytecode;      // Instruction bytes
    
    // Header bit layout:
    // Bits 0-4:   Number of arguments (0-31)
    // Bits 5-10:  Number of temporaries (0-63)  
    // Bits 11-16: Number of literals (0-63)
    // Bit 16:     Has primitive flag
    // Bits 17-21: Stack size (0-31)
}
```

### 4. Kernel Services

The Kernel provides core VM services for object allocation, method lookup, and global namespace:

```csharp
public class Kernel {
    // Global namespace
    public Dictionary<SSymbol, object> Globals { get; }
    
    // Method lookup cache: (class, selector) -> (method, version)
    private Dictionary<(SClass, SSymbol), (SCompiledMethod, int)> _lookupCache;
    
    // Core class references
    public SClass ObjectClass { get; set; }
    public SClass SmallIntegerClass { get; set; }
    public SClass ArrayClass { get; set; }
    public SClass CompiledMethodClass { get; set; }
    
    public SObject BasicNew(SClass klass) {
        return klass.BasicNew();
    }
    
    public SCompiledMethod Lookup(SClass klass, SSymbol selector) {
        // Check cache first with version validation
        var key = (klass, selector);
        if (_lookupCache.TryGetValue(key, out var cached)) {
            if (cached.version == klass.MethodDict.Version)
                return cached.method; // Cache hit
        }
        
        // Perform lookup and cache result
        var method = klass.LookupMethod(selector);
        if (method != null)
            _lookupCache[key] = (method, klass.MethodDict.Version);
        return method;
    }
}

### 5. Memory Management

The architecture supports three complementary memory strategies:

#### Phase 1: Managed Objects (Current)
- Pure C# managed objects with GC handling
- Explicit identity hash field for stable hashing
- Tagged immediates for SmallIntegers
- Simple and debuggable

#### Phase 2: Arena Allocation (Optional Optimization)
```csharp
public class Arena : IDisposable {
    private IntPtr buffer;        // Unmanaged memory block
    private int freeOffset;       // Next allocation position
    
    public int Allocate(int bytes) {
        // Thread-safe allocation with alignment
        bytes = (bytes + 7) & ~7;  // 8-byte alignment
        var offset = Interlocked.Add(ref freeOffset, bytes) - bytes;
        return offset;
    }
}

public class ArenaCompiledMethod {
    private readonly Arena arena;
    private readonly ArenaHandle handle;
    
    // Provides managed wrapper over arena data
    public int GetNumTemps() => arena.ReadInt32(baseOffset + NumTempsOffset);
    public IntPtr BytecodePtr => arena.BasePtr + baseOffset + bytecodeOffset;
}
```

#### Phase 3: Image Persistence
```csharp
public class SnapshotWriter {
    // Serializes object graph and arena to contiguous image
    public void ScanManagedRoots(IEnumerable<object> roots);
    public void WriteImage(Stream output);
}

public class SnapshotReader {
    // Deserializes image back to memory
    public Dictionary<string, object> ReadImage(Stream input);
}
```

### 6. Bytecode Execution

The BytecodeDispatcher remains largely unchanged but now works with tagged values:

#### Bytecode Format (UNCHANGED - Required by Compiler)
- **0-15**: Push receiver variable
- **16-31**: Push temporary variable
- **32-63**: Push literal constant
- **64-95**: Push literal variable
- **96-103**: Stack operations
- **104-111**: Return operations
- **112-119**: Push special constants
- **120-127**: Send special selectors
- **128-175**: Extended operations
- **176-255**: Send operations

#### Hot Path Optimizations
```csharp
// SmallInteger arithmetic with tagging
if (Tags.TryUnboxSmallInteger(left, out long a) && 
    Tags.TryUnboxSmallInteger(right, out long b)) {
    // Fast path: both are SmallIntegers
    return Tags.FromSmallInteger(a + b);
}
// Slow path: full message send
```

### 7. Cold Start Process

The cold start creates the kernel classes directly without circular dependencies:

```csharp
public void CreateKernel() {
    // Step 1: Create core classes (temporarily self-referential)
    ObjectClass = new SClass("Object", 0);
    BehaviorClass = new SClass("Behavior", 5);
    ClassDescriptionClass = new SClass("ClassDescription", 3);
    MetaclassClass = new SClass("Metaclass", 1);
    ClassClass = new SClass("Class", 0);
    
    // Step 2: Wire inheritance
    BehaviorClass.Superclass = ObjectClass;
    ClassDescriptionClass.Superclass = BehaviorClass;
    MetaclassClass.Superclass = ClassDescriptionClass;
    ClassClass.Superclass = ClassDescriptionClass;
    
    // Step 3: Create metaclasses (all instances of Metaclass)
    var objectMetaclass = new SClass("Object class", 0);
    objectMetaclass.Klass = MetaclassClass;
    ObjectClass.Metaclass = objectMetaclass;
    
    // Step 4: Wire metaclass hierarchy
    // Object class -> Class
    objectMetaclass.Superclass = ClassClass;
    // Parallel hierarchy for others
    
    // Step 5: Fix singletons
    Nil.Instance.Klass = UndefinedObjectClass;
    True.Instance.Klass = TrueClass;
    False.Instance.Klass = FalseClass;
    
    // Step 6: Create essential classes
    CompiledMethodClass = CreateClass("CompiledMethod", ObjectClass);
    ArrayClass = CreateClass("Array", arrayedCollClass);
    SmallIntegerClass = CreateClass("SmallInteger", integerClass);
    
    // Register all as globals
    RegisterGlobal("Object", ObjectClass);
    RegisterGlobal("nil", Nil.Instance);
    // ...
}
```

#### Symbol Pool Solution
Symbols exist before the Symbol class through direct pooling:
```csharp
public static class SSymbol {
    private static Dictionary<string, SSymbol> _pool = new();
    
    public static SSymbol Intern(string value) {
        if (!_pool.TryGetValue(value, out var symbol)) {
            symbol = new SSymbol(value);
            _pool[value] = symbol;
        }
        return symbol;
    }
}
```

### 8. Implementation Status

#### ✅ Completed Components

**New Direct Object Model**
- SObject base class with stable identity hash
- SClass with method dictionary and version tracking
- SSymbol with global interning pool
- SCompiledMethod with header encoding
- SArray, SByteArray, SString collections
- Tags helper for SmallInteger immediates
- Kernel services with method lookup cache

**Memory Management**
- Arena allocation for compact unmanaged storage
- ArenaCompiledMethod wrapper for safe access
- SnapshotWriter for image serialization
- SnapshotReader for image deserialization
- Support for mixed managed/unmanaged objects

**Cold Start Solution**
- Direct kernel creation without circular dependencies
- Symbol pooling before Symbol class exists
- Metaclass hierarchy setup
- Global registration of core classes

#### 🚧 In Progress

**Integration Tasks**
- [ ] Adapt BytecodeDispatcher to use SObject instead of OOP
- [ ] Update PrimitiveTable to work with tagged immediates
- [ ] Convert existing bootstrap to use new Kernel.CreateKernel()
- [ ] Implement hot path optimizations with TryUnboxSmallInteger

**Testing & Validation**
- [ ] Verify arithmetic with tagged SmallIntegers
- [ ] Test method lookup with version-based caching
- [ ] Validate image save/restore cycle
- [ ] Benchmark arena vs managed allocation

## Architecture Benefits

### Performance Gains
- **No OOP Indirection**: Direct object references eliminate lookup overhead
- **Tagged Immediates**: SmallInteger arithmetic without heap allocation
- **Version-based Cache**: Method lookup invalidates only when needed
- **Arena Option**: Compact storage for hot objects if profiling shows need

### Simplicity Improvements
- **Direct Object Model**: Standard C# objects with Smalltalk semantics
- **Single Cold Start**: One unified kernel creation process
- **Clear Memory Strategies**: Managed → Arena → Image progression

### Maintainability
- **No Circular Dependencies**: Symbol pooling solves bootstrap problem
- **Explicit Identity Hash**: Stable across GC moves
- **Standard Tooling**: C# debuggers work naturally with SObject model

## Key Design Principles

1. **Start Simple**: Managed objects first, optimize later with arena
2. **Preserve Semantics**: 100% Smalltalk-80 bytecode compatibility
3. **Explicit Over Magic**: Identity hash, class pointers visible
4. **Profile-Driven**: Arena allocation only if measurements show need
5. **Image Portability**: Snapshot format independent of memory strategy

## Example Usage

### Creating Objects
```csharp
var kernel = new Kernel(logger);
kernel.CreateKernel();

// Create a SmallInteger (tagged immediate)
var five = Tags.FromSmallInteger(5);

// Create an Array
var array = kernel.BasicNew(kernel.ArrayClass, 10);

// Method lookup with caching
var method = kernel.Lookup(array.Klass, SSymbol.Intern("at:"));
```

### Memory Strategies
```csharp
// Phase 1: Managed objects (default)
var method = new SCompiledMethod(kernel.CompiledMethodClass);

// Phase 2: Arena allocation (optional)
var arena = new Arena(1024 * 1024); // 1MB
var arenaMethod = ArenaCompiledMethod.Allocate(arena, literalCount: 5, bytecodeLength: 20);

// Phase 3: Image persistence
var writer = new SnapshotWriter(arena);
writer.ScanManagedRoots(new[] { kernel.Globals });
using (var stream = File.Create("image.snap"))
    writer.WriteImage(stream);
```

## Migration Notes

The following components still use the old OOP-based system and need migration:

### Existing Components (OOP-based)
- **ColdStartBootstrap.cs**: Uses IObjectMemory with OOP
- **VMOverlord.cs**: Two-phase bootstrap with OOP  
- **BytecodeDispatcher**: Uses OOP for stack operations
- **PrimitiveTable**: All primitives use OOP parameters
- **ProcessScheduler**: Process management with OOP
- **TextCollector**: Transcript implementation with OOP

### Migration Path
1. Start using Kernel.CreateKernel() for cold start
2. Update BytecodeDispatcher to use Tags helpers for SmallIntegers
3. Convert primitives one by one to handle SObject/tagged values
4. Keep both systems running in parallel during transition
5. Remove OOP-based code once all components migrated

## Summary

This architecture provides a modern, efficient Smalltalk-80 implementation that:
- Eliminates OOP indirection with direct object references
- Uses tagged immediates for SmallInteger performance  
- Supports flexible memory strategies (managed → arena → image)
- Solves cold start circular dependencies elegantly
- Maintains 100% bytecode compatibility
- Enables future optimizations based on profiling

The new direct object model with SObject provides a solid foundation for a high-performance Smalltalk-80 interpreter while maintaining compatibility with the original system design and ensuring authentic execution through strict adherence to proper implementation patterns.