# Smalltalk-80 Interpreter Optimization Plan

**Based on**: `Benchmark testStandardTests` results from `InitialTiming.txt`
**Date**: 2025-12-28

## Executive Summary

Analysis of the benchmark results reveals several significant performance bottlenecks in the interpreter. The most impactful optimizations target:

1. **Global variable lookup** (29x slower than expected)
2. **Object creation** (Point, new)
3. **Float arithmetic**
4. **Method dispatch overhead** (perform:, accessor methods)
5. **Stack operations** (excessive validation in hot path)

## Benchmark Results Analysis

### Performance Categories

#### Fast Operations (< 150 µs per repetition) ✅
| Test | Time (µs) | Notes |
|------|-----------|-------|
| store into instance variable | 81.3 | Good - direct heap write |
| store into temp | 82.8 | Good - context slot write |
| short branch on false | 98.4 | Good - inlined conditionals working |
| add 20000 + 20000 | 109.0 | Good - SmallInteger fast path |
| load instance variable | 112.6 | Good |
| load thisContext | 114.0 | Good |
| arithmetic (+, <, *) | ~117 | Good - SmallInteger fast path |
| send #class | 140.0 | Good - special selector fast path |
| send == | 148.4 | Good - identity comparison fast path |

#### Problem Areas (> 500 µs per repetition) ⚠️
| Test | Time (µs) | Expected | Slowdown | Priority |
|------|-----------|----------|----------|----------|
| load literal indirect | 2900 | ~120 | **24x** | **P0** |
| create 3@4 (Point) | 2516 | ~300 | 8x | **P1** |
| create 20 points | 4437 | ~500 | 9x | **P1** |
| perform: #+ with: | 4844 | ~200 | 24x | **P1** |
| Float asFloat | 3910 | ~200 | 20x | **P2** |
| Float addition | 3600 | ~150 | 24x | **P2** |
| execute aPoint x | 337.5 | ~50 | 7x | **P2** |
| ReadWriteStream nextPut: | 2220 | ~300 | 7x | P3 |

### Critical Anomaly: `load literal indirect`

The test "load literal indirect (overflow refct), 20 times" takes **2900 µs** vs ~120 µs for similar load operations. This is loading `Point` (a global variable) 20 times.

**Root Cause**: Global variable lookup via Association requires:
1. Push literal variable (bytecode 0x40-0x5F)
2. Fetch Association from literal frame
3. Read `value` slot from Association (heap access)

Each global access does an Association indirection that other loads don't need.

---

## Optimization Plan

### Priority 0: Critical Path Optimizations

#### O1: Remove Diagnostic Code from StackManager.Push()
**File**: `Smalltalk80.Interpreter/Core/VM/StackManager.cs:31-76`
**Impact**: Every bytecode that pushes a value

**Current Code** (lines 42-60):
```csharp
// DIAGNOSTIC: Validate activeContextOOP is a valid context
var activeContextFormat = heapManager.GetObjectFormat(activeContextOOP);
var activeContextClass = helpers.GetClassName(helpers.GetClass(activeContextOOP));
// ... 20 lines of validation ...
```

**Problem**: `GetObjectFormat()`, `GetClassName()`, `GetClass()` called on EVERY push!

**Fix**:
- Remove diagnostic validation from release builds
- Use `#if DEBUG` conditional or remove entirely
- Estimated speedup: 20-40% on all stack operations

#### O2: Remove Diagnostic Code from StackManager.Pop()
**File**: `Smalltalk80.Interpreter/Core/VM/StackManager.cs:79-136`
**Impact**: Every bytecode that pops a value

**Current Code** (lines 94-95):
```csharp
var contextClassOOP = heapManager.GetClassOOP(activeContextOOP);
bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;
```

**Problem**: Class lookup on every pop to determine if BlockContext.

**Fix**:
- Cache context type in ExecutionContext or use a flag
- Avoid heap access on every pop
- Estimated speedup: 15-25% on all stack operations

#### O3: Inline Association Value Access for Globals
**File**: `Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs:354-464`
**Impact**: Global variable access (every `Point`, `Array`, etc. reference)

**Current**: Each global access reads Association then reads `value` slot.

**Fix Options**:
1. **Cache global values**: Build a direct OOP lookup table for common globals
2. **Inline constant globals**: Classes like Point, Array, String rarely change
3. **Specialize literal variable bytecode**: Detect Association vs direct value

---

### Priority 1: Object Creation & Method Dispatch

#### O4: Optimize Object Allocation
**File**: `Smalltalk80.Interpreter.HeapManager/SmalltalkHeapManager.cs:103-204`
**Impact**: `Point new`, `Array new:`, all object creation

**Current Issues**:
1. `AllocateInstanceOf()` reads class format from heap every time
2. Nil-initialization loop for every slot
3. Interlocked increment for allocation counter

**Fix**:
- Cache class format information in VMMetadata
- Use `Array.Fill()` or `Span.Fill()` for nil initialization
- Batch allocations where possible (e.g., 20 points)

#### O5: Cache @ (Point Creation) Primitive
**File**: Primitive handling for `@` selector
**Impact**: `3@4` takes 2516 µs (should be ~300 µs)

**Current**: Falls back to message send for `@`.

**Fix**:
- Add `@` to arithmetic fast-path (it's bytecode 0xBB)
- Implement inline Point creation without full message dispatch
- Pattern: `receiver @ arg` → allocate Point, set x/y, return

#### O6: Optimize perform:with:
**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs`
**Impact**: `perform:` takes 4844 µs (should be ~200 µs)

**Current**: Full method lookup + dispatch for each perform:.

**Fix**:
- Method lookup cache (inline cache or polymorphic inline cache)
- Cache last (receiver-class, selector) → method mapping
- Avoid re-walking class hierarchy for repeated calls

---

### Priority 2: Type-Specific Optimizations

#### O7: Float Arithmetic Fast Path
**File**: `Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs:1049-1098`
**Impact**: Float +, -, *, / operations

**Current**: No fast path for Float - always falls back to message send.

**Fix**:
- Extend `ExecuteSendArithmetic()` to handle Float operands
- Direct C# double arithmetic when both operands are Float
- Pattern matching: `if (IsFloat(receiver) && IsFloat(arg)) { ... }`

#### O8: Accessor Method Fast Path
**File**: `Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs` (special selectors)
**Impact**: `aPoint x`, `aPoint y` take 337.5 µs (should be ~50 µs)

**Current**: `x` and `y` are special selectors (index 14, 15) but no fast path.

**Fix**:
- Add fast path for `x` and `y` on Point class
- Direct instance variable access without method lookup
- Pattern: `if (classOOP == metadata.PointClassOOP) { return ivar[0/1]; }`

#### O9: String at:/at:put: Fast Path
**Impact**: String at: is 687 µs vs Array at: 234 µs (3x slower)

**Current**: String not in whitelist for at: fast path.

**Fix**:
- Add String to whitelist in `ExecuteSendSpecial()` (line 1223)
- Handle Character return (wrap byte in Character object)
- Use CharacterTable for O(1) Character lookup

---

### Priority 3: Structural Optimizations

#### O10: Method Lookup Cache
**File**: `Smalltalk80.Interpreter/Core/VM/MessageDispatcher.cs`
**Impact**: All message sends without fast path

**Current**: Linear search through method dictionary on every lookup.

**Fix**:
- Implement inline cache per call site (store last class→method)
- Or global method cache: hash(class, selector) → method
- Invalidate on method changes (class>>compile:, etc.)

#### O11: Context Field Caching
**Impact**: SP, PC, method read from heap on every bytecode

**Current**: Every bytecode fetches SP, PC via `heapManager.GetInstanceVariable()`.

**Fix**:
- Cache SP, PC, method in C# locals during bytecode execution
- Only write back to heap on context switch
- Use `ref` locals for direct heap access

#### O12: Bytecode Dispatch Table
**File**: `Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs:84-782`
**Impact**: All bytecode execution

**Current**: Large if-else chain for bytecode dispatch.

**Fix**:
- Replace with delegate array: `Action<byte>[] handlers = new Action<byte>[256]`
- Direct index: `handlers[bytecode](bytecode)`
- Eliminates branch prediction overhead

---

## Implementation Order

### Phase 1: Quick Wins (1-2 days)
1. **O1**: Remove Push() diagnostics - immediate 20%+ speedup
2. **O2**: Optimize Pop() context check - 15%+ speedup
3. **O8**: Add x/y fast path for Point - 7x speedup on accessor

### Phase 2: High Impact (3-5 days)
4. **O5**: Inline @ Point creation - 8x speedup on Point creation
5. **O7**: Float arithmetic fast path - 20x speedup on Float ops
6. **O3**: Optimize global variable lookup - 24x speedup

### Phase 3: Architectural (1-2 weeks)
7. **O10**: Method lookup cache - improves all message sends
8. **O6**: Optimize perform: - 24x speedup
9. **O4**: Object allocation optimization - 8x speedup on new

### Phase 4: Advanced (future)
10. **O11**: Context field caching
11. **O12**: Bytecode dispatch table
12. **O9**: String at: fast path

---

## Metrics for Success

After optimizations, target times (per repetition):

| Test | Current | Target | Improvement |
|------|---------|--------|-------------|
| load literal indirect | 2900 µs | 150 µs | 19x |
| create 3@4 | 2516 µs | 300 µs | 8x |
| perform: #+ | 4844 µs | 300 µs | 16x |
| Float addition | 3600 µs | 200 µs | 18x |
| aPoint x | 337.5 µs | 50 µs | 7x |
| create 20 points | 4437 µs | 600 µs | 7x |

**Overall target**: 5-10x improvement on benchmark suite.

---

## Phase 2 Deep Analysis: Remaining Slow Paths

After implementing Phase 1 optimizations (O1, O2, O8), benchmark results show 6-30x speedups on stack operations and accessors. However, several tests showed minimal improvement (1-6%):

| Test | Before | After | Speedup | Status |
|------|--------|-------|---------|--------|
| create 3@4 (Point) | 2516 µs | 2463 µs | 1.02x | ❌ Still slow |
| create 20 points | 4437 µs | 4403 µs | 1.01x | ❌ Still slow |
| perform: #+ | 4844 µs | 4626 µs | 1.05x | ❌ Still slow |
| load literal indirect | 2900 µs | 2740 µs | 1.06x | ❌ Still slow |
| Float asFloat | 3910 µs | 3910 µs | 1.00x | ❌ Still slow |
| Float addition | 3600 µs | 3600 µs | 1.00x | ❌ Still slow |

### Root Cause Analysis

#### 1. Point Creation - STRING-Based Lookups (PointPrimitives.cs:26-35)

```csharp
// BOTTLENECK: String-based allocation
var pointOOP = context.AllocateObject("Point", 0);

// BOTTLENECK: String-based field lookups (EVERY Point creation!)
int xIndex = context.LayoutResolver.GetFieldIndex("class:Point", "x");
int yIndex = context.LayoutResolver.GetFieldIndex("class:Point", "y");
```

**Cost per Point creation:**
1. `AllocateObject("Point", 0)` → calls `RuntimeHeapAccess.GetClass("Point")`
2. `GetClass("Point")` → calls `LookupGlobalByName()` with O(n) byte-by-byte comparison
3. `GetFieldIndex("class:Point", "x")` → calls `_fieldMap["class:Point"].IndexOf("x")` (string dict lookup + List.IndexOf)
4. Same for "y" field

**Fix: Cache class OOP and field indices in VMMetadata**
```csharp
// Before (current code):
var pointOOP = context.AllocateObject("Point", 0);
int xIndex = context.LayoutResolver.GetFieldIndex("class:Point", "x");

// After (optimized):
var pointOOP = context.HeapManager.AllocateInstanceOf(context.Metadata.PointClassOOP, 0);
context.HeapManager.SetInstanceVariable(pointOOP, VMConstants.POINT_X_INDEX, xOop);
context.HeapManager.SetInstanceVariable(pointOOP, VMConstants.POINT_Y_INDEX, yOop);
```

#### 2. Float Arithmetic - Allocation + Byte Copying (FloatPrimitives.cs:14-38)

```csharp
// BOTTLENECK 1: Allocates byte array on every ToDouble
private static double ToDouble(ISmalltalkHeap heap, SmalltalkOOP floatOop)
{
    byte[] bytes = new byte[8];  // <-- GC allocation every call!
    for (int i = 0; i < 4; i++)  // <-- Loop instead of bulk copy
    {
        ushort word = heap.GetIndexedWord(floatOop, i);
        bytes[i * 2] = (byte)(word & 0xFF);
        bytes[i * 2 + 1] = (byte)((word >> 8) & 0xFF);
    }
    return BitConverter.ToDouble(bytes, 0);
}

// BOTTLENECK 2: String-based class lookup
private static SmalltalkOOP FromDouble(PrimitiveExecutionContext ctx, double value)
{
    var floatOop = ctx.AllocateObject("Float", 4);  // <-- STRING LOOKUP!
    // ...
}
```

**Cost per Float operation:**
1. `ToDouble`: 1 allocation (8 bytes) + 4 GetIndexedWord + 8 byte manipulations
2. `FromDouble`: 1 string lookup + 1 allocation + 4 SetIndexedWord + 8 byte manipulations

**Fix: Use Span<byte> + cached FloatClassOOP**
```csharp
// Optimized ToDouble - no allocation
private static double ToDouble(ISmalltalkHeap heap, SmalltalkOOP floatOop)
{
    Span<byte> bytes = stackalloc byte[8];
    for (int i = 0; i < 4; i++)
    {
        ushort word = heap.GetIndexedWord(floatOop, i);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.Slice(i * 2), word);
    }
    return BinaryPrimitives.ReadDoubleLittleEndian(bytes);
}

// Optimized FromDouble - use cached class OOP
private static SmalltalkOOP FromDouble(PrimitiveExecutionContext ctx, double value)
{
    var floatOop = ctx.HeapManager.AllocateInstanceOf(ctx.Metadata.FloatClassOOP, 4);
    // ... rest same
}
```

#### 3. Method Lookup (perform:) - O(n) with String Allocations (MessageDispatcher.cs:88-102)

```csharp
// BOTTLENECK: Called for EVERY method in dictionary during lookup
int dictSize = heapManager.GetIndexedSize(methodDictOOP);
for (int i = 0; i < dictSize; i++)
{
    var selectorOOP = heapManager.GetIndexedOOP(methodDictOOP, i);

    // ALLOCATES new byte[] + string for EVERY method examined!
    string? currentSelectorName = GetSymbolName(selectorOOP);

    if (currentSelectorName == selectorName)
    {
        var methodOOP = heapManager.GetIndexedOOP(valuesArrayOOP, i);
        return new MethodLookupResult { MethodOOP = methodOOP };
    }
}
```

**Cost per method lookup:**
- O(n) where n = methods in class + all superclass methods
- For each method examined: 1 byte[] allocation + 1 string allocation + byte-by-byte copy

**Fix: Compare Symbol OOPs directly (identity)**

Since Symbols are interned, we can compare OOPs directly instead of strings:

```csharp
// Optimized - compare interned Symbol OOPs
public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, SmalltalkOOP selectorOOP)
{
    for (int i = 0; i < dictSize; i++)
    {
        var keySelectorOOP = heapManager.GetIndexedOOP(methodDictOOP, i);

        // Identity comparison - no string allocation!
        if (keySelectorOOP == selectorOOP)
        {
            var methodOOP = heapManager.GetIndexedOOP(valuesArrayOOP, i);
            return new MethodLookupResult { MethodOOP = methodOOP };
        }
    }
}
```

Additionally, add method cache:
```csharp
// Global method cache
private readonly Dictionary<(SmalltalkOOP class, SmalltalkOOP selector), SmalltalkOOP> _methodCache;

public MethodLookupResult? LookupMethod(SmalltalkOOP classOOP, SmalltalkOOP selectorOOP)
{
    if (_methodCache.TryGetValue((classOOP, selectorOOP), out var cachedMethod))
        return new MethodLookupResult { MethodOOP = cachedMethod };

    // ... lookup and cache result
}
```

#### 4. Global Lookup (load literal indirect) - RuntimeHeapAccessor.InternSymbol

```csharp
// BOTTLENECK: InternSymbol searches entire USTable (nested loops!)
public virtual SmalltalkOOP InternSymbol(string symbolName)
{
    // Search through USTable (Array-of-Arrays)
    for (int i = 0; i < ustableSize; i++)
    {
        var bucketOOP = _heapManager.GetIndexedOOP(ustableOOP, i);

        int bucketSize = _heapManager.GetIndexedSize(bucketOOP);
        for (int k = 0; k < bucketSize; k++)  // Nested loop!
        {
            var symbolOOP = _heapManager.GetIndexedOOP(bucketOOP, k);

            // Byte-by-byte comparison for EVERY symbol
            for (int j = 0; j < searchBytes.Length; j++)
            {
                if (symbolByte != searchBytes[j]) { ... }
            }
        }
    }
}
```

**Fix: Use Symbol cache or pass Symbol OOP directly**

For `load literal indirect` bytecode, the literal IS already a Symbol OOP - no need to intern:

```csharp
// In BytecodeInterpreter, the literal variable IS the Association OOP
var associationOOP = GetLiteral(literalIndex);
var valueOOP = heapManager.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_VALUE_INDEX);
Push(valueOOP);
```

The slowdown is likely in cold start code that uses string-based lookups. Runtime bytecode should NOT call InternSymbol.

---

### Priority Phase 2 Implementation

| Optimization | Impact | Complexity | Speedup |
|-------------|--------|------------|---------|
| **P2.1** Cache PointClassOOP + field indices | Point creation | Low | 5-8x |
| **P2.2** Use FloatClassOOP + Span | Float ops | Low | 3-5x |
| **P2.3** Symbol OOP comparison in method lookup | All message sends | Medium | 2-4x |
| **P2.4** Method lookup cache | perform:, all sends | Medium | 5-10x |

### VMMetadata Extensions Required

Add these cached values to VMMetadata:

```csharp
public record VMMetadata
{
    // Existing...
    public required SmalltalkOOP PointClassOOP { get; init; }
    public required SmalltalkOOP FloatClassOOP { get; init; }

    // NEW: Cached field indices (Point x/y are always 0/1)
    public const int POINT_X_INDEX = 0;
    public const int POINT_Y_INDEX = 1;
}
```

### VMConstants Extensions Required

```csharp
public static class VMConstants
{
    // Point field indices (fixed by Blue Book)
    public const int POINT_X_INDEX = 0;
    public const int POINT_Y_INDEX = 1;

    // Float word indices
    public const int FLOAT_WORD_COUNT = 4;
}
```

---

## Appendix: Code Locations

### Phase 1 Files (Stack & Fast Path)
| Component | File | Lines |
|-----------|------|-------|
| Stack Push | StackManager.cs | 31-52 |
| Stack Pop | StackManager.cs | 54-83 |
| Bytecode Dispatch | BytecodeInterpreter.cs | 84-782 |
| Arithmetic Fast Path | BytecodeInterpreter.cs | 1049-1098 |
| Special Selectors | BytecodeInterpreter.cs | 1105-1339 |
| Integer Arithmetic | BytecodeInterpreter.cs | 1349-1385 |

### Phase 2 Files (Primitives & Dispatch)
| Component | File | Lines | Bottleneck |
|-----------|------|-------|------------|
| Point Creation | PointPrimitives.cs | 14-39 | STRING lookup |
| Float ToDouble | FloatPrimitives.cs | 14-24 | byte[] allocation |
| Float FromDouble | FloatPrimitives.cs | 27-38 | STRING lookup |
| Method Lookup | MessageDispatcher.cs | 25-120 | O(n) + string alloc |
| GetSymbolName | MessageDispatcher.cs | 134-154 | byte[] + string alloc |
| AllocateObject | PrimitiveInterfaces.cs | 219-226 | STRING lookup |
| GetFieldValue | PrimitiveInterfaces.cs | 149-164 | STRING lookup |
| InternSymbol | RuntimeHeapAccessor.cs | 83-160 | O(n²) + byte compare |
| LookupGlobalByName | RuntimeHeapAccessor.cs | 167-212 | O(n) + byte compare |
| LayoutResolver | LayoutResolver.cs | 44-47 | Dict + List.IndexOf |

### Heap & Allocation
| Component | File | Lines |
|-----------|------|-------|
| Object Allocation | SmalltalkHeapManager.cs | 103-250 |
| Heap Access | SmalltalkHeapManager.cs | 295-445 |
| VMMetadata | VMMetadata.cs | 1-52 |
| VMConstants | VMConstants.cs | - |
