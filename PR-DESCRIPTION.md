# Performance optimizations for method lookup and symbol interning

## Summary

This PR implements a series of performance optimizations targeting the interpreter's most frequently executed code paths: method lookup and symbol interning. These optimizations collectively deliver significant speedups across all Smalltalk benchmarks.

## Key Optimizations

### 1. Method Lookup Cache (commits 8f62be47, e46e213c, 4b8a1885, 222d2247)
- Added method lookup cache to avoid repeated hierarchy walks
- Cache populates all intermediate classes during hierarchy traversal
- Checks cache at each hierarchy step for sibling class reuse
- Connected to Primitive 89 (flushCache) for proper invalidation

### 2. Hash-Based Method Dictionary Lookup (commits f51cdc7a, 1917894f)
- Replaced O(n) linear scan with hash-based dictionary lookup
- Refactored to use RuntimeDictionaryService for consistent dictionary operations
- Direct Symbol OOP comparison eliminates string matching overhead

### 3. IEquatable<SmalltalkOOP> Implementation (commit cb0cef1f)
- Added IEquatable interface to SmalltalkOOP struct
- Improves Dictionary<SmalltalkOOP, T> performance by avoiding boxing

### 4. Symbol Interning Optimization (commits c82c03e1, fa189b0c, 8dd87044, 08cbbabf)
- Added GetByteSpan() method for zero-copy byte access
- Fixed InternSymbol to use proper bucket-based USTable lookup
- Changed from O(n) linear scan of ~8000 symbols to O(1) bucket access + O(2-3) symbols per bucket
- Consolidated to single implementation in RuntimeHeapAccessor

### 5. Point Creation Optimization (commit 2047e658)
- Cached class and field constants for Point creation primitive
- Reduces per-call overhead for frequently-created Point objects

### 6. Float Primitives Optimization (commits eda31b2c, 97c62873)
- Zero-allocation bit conversion for Float operations
- Added GetFloatAsDouble/SetFloatFromDouble heap methods

## Benchmark Results

| Benchmark | Baseline | Optimized | Speedup |
|-----------|----------|-----------|---------|
| **Point x accessor (10x)** | 210.9 μs | 11.0 μs | **19x** |
| **Point creation 3@4 (10x)** | 2437 μs | 141 μs | **17x** |
| **Float conversion (20x)** | 3750 μs | 310 μs | **12x** |
| **Float addition (20x)** | 3600 μs | 320 μs | **11x** |
| String at: (20x) | 470 μs | 171 μs | 2.7x |
| String at:put: (20x) | 517 μs | 188 μs | 2.7x |
| Set basicAt: (20x) | 749 μs | 203 μs | 3.7x |
| Set basicAtPut: (20x) | 718 μs | 187 μs | 3.8x |
| String replace chars | 310 μs | 10 μs | 31x |
| Find implementors of #next | 500 ms | 187 ms | 2.7x |
| Create inspector view | 154.7 ms | 65.6 ms | 2.4x |
| Print class hierarchy | 118.7 ms | 54.7 ms | 2.2x |
| Decompile InputSensor | 2047 ms | 1094 ms | 1.9x |
| Compile dummy method | 840 ms | 547 ms | 1.5x |
| Activate and return (32K) | 828 ms | 516 ms | 1.6x |

## Technical Details

### USTable Structure
The Universal Symbol Table is an Array of Arrays (buckets) for collision chaining:
- Each bucket contains symbols with the same hash % tableSize
- Lookup uses Smalltalk's String>>hash algorithm (SymbolHasher.ComputeHash)
- Previous implementation incorrectly treated slots as direct Symbol references

### Method Cache Architecture
- Global cache keyed by (class OOP, selector OOP) → CompiledMethod OOP
- Populates cache entries for all classes in the hierarchy during lookup
- Invalidated via Primitive 89 when methods are added/removed

## Test Plan

- [x] All 385 C# unit tests pass
- [x] BlockContext regression test passes (120/120 tests, returns 1191)
- [x] Init expressions all succeed (286/286)
- [x] System Browser functional with source code display
- [x] Benchmark suite completes without errors
