# Performance Benchmarks

**Last Updated**: 2025-11-18

## Overview

This document describes performance characteristics and benchmarking for the Smalltalk-80 interpreter after architectural refactoring.

---

## 1. Baseline Metrics

### BlockContext Test Suite

**Test**: `BlockContextTestRunner>>testAllCombinations`
**Expected Result**: 1191 (120 tests)

| Metric | Value | Notes |
|--------|-------|-------|
| Total bytecodes executed | ~4,000 | Per run |
| Test count | 120 | All combinations |
| Expected return value | 1191 | Sum of all test results |

### Init Expression Performance

| Init Expression | Instruction Count | Status |
|-----------------|-------------------|--------|
| Character initialize | ~500 | ✅ Pass |
| Float initialize | ~200 | ✅ Pass |
| SmallInteger initialize | ~100 | ✅ Pass |
| Text initialize | ~86,000 | ✅ Pass |

**Total**: 31/46 init expressions pass (67% success rate)

---

## 2. Refactoring Impact

### File Size Reduction

| File | Before | After | Reduction |
|------|--------|-------|-----------|
| VirtualMachine.cs | 4,639 lines | ~1,500 lines | 68% |
| BytecodeCompiler.cs | 3,459 lines | 2,330 lines | 33% |
| RuntimeHeapAccessor.cs | 498 lines | 393 lines | 21% |

### Interface Overhead

The refactoring introduced interfaces but maintained performance:

- **No virtual dispatch on hot paths** - Interpreter loop remains concrete
- **Struct-based contexts** - `PrimitiveExecutionContext` avoids heap allocation
- **Lazy caching** - Runtime caches populated on first use

---

## 3. Critical Hot Paths

### Interpreter Loop

```
VirtualMachine.ExecuteBytecodeStep()
    └── Fetch bytecode
    └── Decode instruction
    └── Execute (stack op, send, or primitive)
```

**No changes to this path** - Remains a tight loop in single file.

### Method Lookup

```
MessageDispatcher.LookupMethod()
    └── RuntimeHeapAccessor.GetClass()
        └── RuntimeClassCache.GetClass()  ← Cached
    └── Walk method dictionary
```

**Caching added** - Class lookups cached, invalidated by primitive 89.

### Primitive Dispatch

```
PrimitiveRegistry.Execute()
    └── Look up IPrimitive in table
    └── Call IPrimitive.Execute(context)
```

**Minimal overhead** - Direct array lookup, no reflection.

---

## 4. Benchmark Commands

### Full Test Suite

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

Expected output:
```
[292] BlockContextTestRunner initialize... ~4,000 instructions  ✓ SUCCESS - Returned: 1191
```

### Standard Init Expressions

```bash
cd Smalltalk80.Interpreter
dotnet run
```

Expected: 31/46 init expressions succeed

### Skip Init Expressions (Cold Start Only)

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart.json --skip-init-expressions
```

---

## 5. Performance Characteristics

### Memory

- **Heap size**: Configurable (default 10,000 objects)
- **Context pooling**: Reduces GC pressure
- **Cache invalidation**: Primitive 89 clears all caches

### Instruction Counting

The VM counts bytecode opcodes executed:

```csharp
private long _instructionCount = 0;
private const long INSTRUCTION_REPORT_INTERVAL = 100000;
```

Reports every 100K instructions for debugging infinite loops.

### Primitive Performance

All 110 primitives now use `IPrimitive` pattern:
- Stateless implementations
- Dependency injection via context
- No reflection overhead

---

## 6. Known Performance Characteristics

### Init Expressions with Issues

| Expression | Issue | Instruction Count |
|------------|-------|-------------------|
| Form | Infinite loop | 4,000,000+ |
| TextList | Infinite loop | 500,000+ |
| VariableNode | Infinite loop | 500,000+ |
| Delay | Process loop | Unbounded |

These are skipped during normal execution.

### Cache Effectiveness

Runtime caches significantly improve repeated lookups:

- **First lookup**: Full heap walk
- **Subsequent lookups**: O(1) hash table lookup
- **After primitive 89**: Caches cleared, next lookup rebuilds

---

## 7. Optimization Opportunities

### Not Yet Implemented

1. **Inline caching** - Specialize sends for monomorphic receivers
2. **Method cache** - Cache method lookup results
3. **JIT compilation** - Compile hot methods to native code

### Current Optimizations

1. **Tail recursion analysis** - In compiler (disabled by default)
2. **Class caching** - In RuntimeClassCache
3. **Special objects caching** - In RuntimeSpecialObjects

---

## 8. Regression Testing

### After Any Change

Always verify:

```bash
# Build
cd Smalltalk80.Interpreter
dotnet build

# Test
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
# Must return: 1191
```

### Performance Regression Indicators

- BlockContext test return value ≠ 1191
- Init expression failure that previously passed
- Instruction count significantly higher than baseline
- New infinite loops (100K+ instructions without progress)

---

## Summary

The architectural refactoring maintained performance while improving:

- **Code organization** - Smaller, focused files
- **Testability** - Interfaces enable unit testing
- **Maintainability** - Clear patterns for future changes

Critical hot paths (interpreter loop, primitive dispatch) remain optimized with minimal indirection.
