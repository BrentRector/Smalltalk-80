# Primitive Fixes - Implementation Roadmap

**Document Version**: 1.0
**Date**: 2025-01-24
**Status**: Ready for execution
**Related**: [Primitive-Improvements.md](Primitive-Improvements.md)

---

## Overview

This document provides a detailed, prioritized implementation plan for fixing all primitive operation issues identified in the comprehensive review. Work is organized into 5 phases with clear dependencies, milestones, and success criteria.

---

## Phase Summary

| Phase | Duration | Focus | Issues Addressed | Status |
|-------|----------|-------|------------------|--------|
| **Phase 1** | 2-3 days | Critical Fixes | CRITICAL-1 through CRITICAL-6 | 🔴 Not Started |
| **Phase 2** | 3-4 days | Correctness | HIGH-1 through HIGH-8 | 🔴 Not Started |
| **Phase 3** | 5-7 days | Infrastructure | MEDIUM-1 through MEDIUM-17 | 🔴 Not Started |
| **Phase 4** | 2-3 days | Polish | LOW-1 through LOW-12 | 🔴 Not Started |
| **Phase 5** | Ongoing | Testing | All fixes validated | 🔴 Not Started |

**Total Estimated Duration**: 3-4 weeks (assuming single developer, part-time)

---

## Phase 1: Critical Fixes (BLOCKING)

**Goal**: Fix all issues that prevent compilation or cause runtime crashes.
**Duration**: 2-3 days
**Priority**: MUST DO FIRST

### Tasks

#### Task 1.1: Fix BlockClosure Invocation Signature ✅
**File**: `BlockClosurePrimitives.cs`
**Issue**: CRITICAL-1
**Estimated Time**: 30 minutes

**Steps**:
1. Change line 66 from:
   ```csharp
   var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP);
   ```
   To:
   ```csharp
   var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP, receiver);
   ```

2. Add comment explaining 4th parameter:
   ```csharp
   // Pass the BlockClosure instance (receiver) for nested block support and proper lexical scoping
   ```

3. Verify compilation succeeds

**Success Criteria**:
- [ ] Code compiles without errors
- [ ] InvokeBlock called with 4 parameters
- [ ] Comment explains blockClosureOOP purpose

**Dependencies**: None

---

#### Task 1.2: Fix BlockClosure Array Detection ✅
**File**: `BlockClosurePrimitives.cs`
**Issue**: CRITICAL-2
**Estimated Time**: 1 hour

**Steps**:
1. Replace try/catch pattern (lines 25-37) with explicit GetIndexedSize check
2. Add class verification using heap manager
3. Document behavior for both valueWithArguments: and value: calls

**Implementation**:
```csharp
else if (args.Length == 1 && !args[0].IsSmallInteger)
{
    // Distinguish between value: (direct arg) and valueWithArguments: (array arg)
    // by checking if argument has indexed size (is array-like)
    int indexedSize = 0;
    try
    {
        indexedSize = ctx.GetIndexedSize(args[0]);
    }
    catch
    {
        indexedSize = 0; // Not an indexed object
    }

    if (indexedSize > 0)
    {
        // valueWithArguments: - arg is an Array, extract elements
        blockArgs = ExtractArrayElements(ctx, args[0]);
    }
    else
    {
        // value: - arg is a direct parameter
        blockArgs = args;
    }
}
```

**Success Criteria**:
- [ ] No try/catch on ExtractArrayElements
- [ ] Uses GetIndexedSize to detect array
- [ ] Both value: and valueWithArguments: work correctly
- [ ] Unit test added

**Dependencies**: None

---

#### Task 1.3: Enable SetIndexedOOP in ValueWithArg ✅
**File**: `ControlPrimitives.cs`
**Issue**: CRITICAL-3
**Estimated Time**: 15 minutes

**Steps**:
1. Remove comment on line 108
2. Uncomment line 109:
   ```csharp
   ctx.HeapManager.SetIndexedOOP(argsArrayOop, 0, args[0]);
   ```
3. Verify ISmalltalkHeapManager has SetIndexedOOP method (it does!)

**Success Criteria**:
- [ ] SetIndexedOOP call uncommented
- [ ] Comment removed
- [ ] Compiles successfully

**Dependencies**: None

---

#### Task 1.4: Enable SetIndexedOOP in PerformWith ✅
**File**: `ControlPrimitives.cs`
**Issue**: CRITICAL-4
**Estimated Time**: 15 minutes

**Steps**:
1. Same as Task 1.3 but for PerformWith (lines 146-149)

**Success Criteria**:
- [ ] SetIndexedOOP call uncommented
- [ ] Compiles successfully

**Dependencies**: None

---

#### Task 1.5: Implement GetLargeIntegerClass ✅
**File**: `LargeIntegerPrimitives.cs`
**Issue**: CRITICAL-5
**Estimated Time**: 30 minutes

**Steps**:
1. Change return type from `int` to `SmalltalkOOP` (line 86)
2. Replace NotImplementedException with GlobalRegistry lookup
3. Update all call sites (FromBigInteger)

**Implementation**:
```csharp
private static SmalltalkOOP GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
{
    string className = isPositive ? "class:LargePositiveInteger" : "class:LargeNegativeInteger";

    if (GlobalRegistry.TryGet(className, out var classOOP))
        return classOOP;

    throw new InvalidOperationException(
        $"{className} not registered in GlobalRegistry during cold start. " +
        "This indicates a bootstrap phase error.");
}
```

**Success Criteria**:
- [ ] Returns SmalltalkOOP, not int
- [ ] Uses GlobalRegistry.TryGet
- [ ] Throws clear error if class not registered
- [ ] All call sites updated

**Dependencies**: None (GlobalRegistry should already have classes from cold start)

---

#### Task 1.6: Fix FromBigInteger Implementation ✅
**File**: `LargeIntegerPrimitives.cs`
**Issue**: CRITICAL-6
**Estimated Time**: 2 hours

**Steps**:
1. Add SetClassOOP to ISmalltalkHeapManager interface
2. Implement byte trimming logic
3. Fix class OOP handling
4. Update allocation approach

**ISmalltalkHeapManager Addition**:
```csharp
public interface ISmalltalkHeapManager
{
    // ... existing methods ...

    /// <summary>
    /// Sets the class reference of an object (slot 0).
    /// Used by primitives that create instances of dynamically determined classes.
    /// </summary>
    /// <param name="objectOOP">Object to modify</param>
    /// <param name="classOOP">Class OOP to set</param>
    void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP);
}
```

**FromBigInteger Implementation**:
```csharp
private static SmalltalkOOP FromBigInteger(IPrimitiveContext ctx, BigInteger value)
{
    byte[] bytes = value.ToByteArray(); // little-endian two's complement

    // Trim leading sign-extension bytes for canonical representation
    // Keep at least one byte even for zero
    int len = bytes.Length;
    byte signByte = (value.Sign < 0) ? (byte)0xFF : (byte)0x00;
    while (len > 1 && bytes[len - 1] == signByte)
        len--;

    // Determine proper class based on sign
    SmalltalkOOP classOOP = GetLargeIntegerClass(ctx, value.Sign >= 0);

    // Allocate byte-indexed object
    // Use low-level allocation since we need to set class explicitly
    SmalltalkOOP largeIntOop = ctx.HeapManager.AllocateObject(
        format: ObjectFormat.ByteIndexed,
        fixedSlots: 0,
        indexedSize: len
    );

    // Store trimmed bytes
    for (int i = 0; i < len; i++)
    {
        ctx.HeapManager.SetIndexedByte(largeIntOop, i, bytes[i]);
    }

    // Set class reference
    ctx.HeapManager.SetClassOOP(largeIntOop, classOOP);

    return largeIntOop;
}
```

**Success Criteria**:
- [ ] SetClassOOP added to ISmalltalkHeapManager
- [ ] SetClassOOP implemented in SmalltalkHeapManager
- [ ] Byte trimming works (no leading 0x00 or 0xFF)
- [ ] Correct class set (LargePositiveInteger vs LargeNegativeInteger)
- [ ] Round-trip test: BigInteger → LargeInteger → BigInteger preserves value

**Dependencies**: Task 1.5 (GetLargeIntegerClass)

---

### Phase 1 Milestone

**Acceptance Criteria**:
- ✅ All CRITICAL issues resolved
- ✅ No NotImplementedException in primitives
- ✅ No commented-out SetIndexedOOP calls
- ✅ BlockClosure primitives work with correct signatures
- ✅ LargeInteger primitives don't crash
- ✅ All changes compile successfully

**Deliverables**:
- [ ] Updated primitive files
- [ ] SetClassOOP added to ISmalltalkHeapManager
- [ ] Unit tests for BlockClosure and LargeInteger
- [ ] Git commits for each fix (atomic, with clear messages)

---

## Phase 2: Correctness Fixes (HIGH PRIORITY)

**Goal**: Fix all semantic errors and Blue Book violations.
**Duration**: 3-4 days
**Priority**: SHOULD DO NEXT

### Tasks

#### Task 2.1: Implement Proper IsLargeInteger Check ✅
**File**: `LargeIntegerPrimitives.cs`
**Issue**: HIGH-1
**Estimated Time**: 30 minutes

**Implementation**:
```csharp
private static bool IsLargeInteger(IPrimitiveContext ctx, SmalltalkOOP objectOop)
{
    if (objectOop.IsSmallInteger)
        return false;

    SmalltalkOOP classOOP = ctx.HeapManager.GetClassOOP(objectOop);

    // Check against both LargeInteger classes
    if (GlobalRegistry.TryGet("class:LargePositiveInteger", out var posClass) && classOOP == posClass)
        return true;
    if (GlobalRegistry.TryGet("class:LargeNegativeInteger", out var negClass) && classOOP == negClass)
        return true;

    return false;
}
```

**Success Criteria**:
- [ ] Returns false for Float, Fraction, other non-LargeInteger objects
- [ ] Returns true for LargePositiveInteger
- [ ] Returns true for LargeNegativeInteger
- [ ] Unit tests verify class checking

**Dependencies**: Phase 1 (GetLargeIntegerClass infrastructure)

---

#### Task 2.2: Fix Mod Semantics for Negative Numbers ✅
**File**: `ArithmeticPrimitives.cs`
**Issue**: HIGH-2
**Estimated Time**: 1 hour

**Implementation**:
```csharp
public static SmalltalkOOP Mod(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // ... arg validation ...

    int a = receiver.AsSmallInteger();
    int b = args[0].AsSmallInteger();

    if (b == 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    try
    {
        // C# remainder (sign follows dividend)
        int rem = a % b;

        // Adjust to Smalltalk mod semantics (sign follows divisor)
        // Blue Book: a \\ b has same sign as b
        if (rem != 0 && ((a < 0) != (b < 0)))
            rem += b;

        return SmalltalkOOP.FromSmallInteger(rem);
    }
    catch
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }
}
```

**Success Criteria**:
- [ ] `-7 \\ 3` returns 2 (not -1)
- [ ] `7 \\ -3` returns -2 (not 1)
- [ ] `10 \\ 3` returns 1 (unchanged)
- [ ] Unit tests cover all sign combinations
- [ ] Verify arithmetic identity: `a = (a // b) * b + (a \\ b)`

**Dependencies**: None

---

#### Task 2.3: Add BitShift Bounds Checking ✅
**File**: `ArithmeticPrimitives.cs`
**Issue**: HIGH-3
**Estimated Time**: 1 hour

**Implementation**:
```csharp
public static SmalltalkOOP BitShift(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // ... arg validation ...

    int a = receiver.AsSmallInteger();
    int shift = args[0].AsSmallInteger();

    // SmallInteger is 30-bit; limit shifts to safe range
    // Shifts >= 30 bits will overflow, shifts >= 32 are undefined in C#
    if (shift > 30 || shift < -30)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil; // Fallback will promote to LargeInteger
    }

    try
    {
        int result = shift >= 0 ? (a << shift) : (a >> (-shift));

        // Verify result still fits in SmallInteger range
        return SmalltalkOOP.FromSmallInteger(result);
    }
    catch (OverflowException)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }
}
```

**Success Criteria**:
- [ ] `1 bitShift: 40` fails primitive (returns nil, PrimitiveSuccess=false)
- [ ] `1 bitShift: 20` succeeds (returns 1048576)
- [ ] Negative shifts work: `1024 bitShift: -10` returns 1
- [ ] Unit tests cover boundary cases

**Dependencies**: None

---

#### Task 2.4: Verify and Fix Quo/Div Semantics ✅
**File**: `ArithmeticPrimitives.cs`
**Issue**: HIGH-4
**Estimated Time**: 2 hours (includes Blue Book research)

**Steps**:
1. Research Blue Book specification for `//` (integer division) vs `quo:`
2. Verify C# `/` operator behavior matches expected semantics
3. Implement adjustments if needed

**Current Understanding**:
- `quo:` (prim 12) - truncated division (rounds toward zero) - matches C# `/`
- `//` (prim 17, called "Div") - may be floored division (rounds toward -infinity)

**Potential Fix for Div** (if floored):
```csharp
public static SmalltalkOOP Div(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // ... validation ...

    int a = receiver.AsSmallInteger();
    int b = args[0].AsSmallInteger();

    if (b == 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    try
    {
        int quotient = a / b; // Truncated division

        // Adjust for floor rounding if signs differ and there's a remainder
        if ((a < 0) != (b < 0) && a % b != 0)
            quotient--; // Floor adjustment

        return SmalltalkOOP.FromSmallInteger(quotient);
    }
    catch
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }
}
```

**Success Criteria**:
- [ ] Blue Book specification verified
- [ ] Semantics documented in code comments
- [ ] Unit tests for both positive and negative operands
- [ ] Quo and Div behavior clearly differentiated

**Dependencies**: None (independent research)

---

#### Task 2.5: Fix Stream.Next Index Semantics ✅
**File**: `ObjectPrimitives.cs`
**Issue**: HIGH-5
**Estimated Time**: 1 hour

**Implementation**: See Primitive-Improvements.md HIGH-5 for full code.

**Success Criteria**:
- [ ] Stream at position 1 reads element at index 0
- [ ] Stream position increments correctly
- [ ] AtEnd works correctly with 1-based position
- [ ] Unit tests cover edge cases

**Dependencies**: None

---

#### Task 2.6: Fix Stream.NextPut Index Semantics ✅
**File**: `ObjectPrimitives.cs`
**Issue**: HIGH-6
**Estimated Time**: 30 minutes

**Success Criteria**: Same as Task 2.5 but for writing.

**Dependencies**: Task 2.5 (same pattern)

---

#### Task 2.7: Fix Stream.AtEnd Comparison ✅
**File**: `ObjectPrimitives.cs`
**Issue**: HIGH-7
**Estimated Time**: 15 minutes

**Success Criteria**:
- [ ] Comparison changed from `>=` to `>`
- [ ] Stream at readLimit is NOT atEnd
- [ ] Stream at readLimit+1 IS atEnd

**Dependencies**: Task 2.5

---

#### Task 2.8: Add Float Edge Case Handling ✅
**File**: `FloatPrimitives.cs`
**Issue**: HIGH-8
**Estimated Time**: 2 hours

**Steps**:
1. Add NaN/Infinity checks to all Float primitives
2. Document behavior for subnormal numbers
3. Add unit tests

**Implementation Pattern**:
```csharp
public static SmalltalkOOP FloatOperation(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    double value = ExtractDouble(ctx, receiver);

    // Check for special values
    if (double.IsNaN(value) || double.IsInfinity(value))
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil; // Let Smalltalk handle special cases
    }

    // ... perform operation ...
}
```

**Success Criteria**:
- [ ] NaN operations fail primitive gracefully
- [ ] Infinity operations fail primitive gracefully
- [ ] Division by zero handled consistently
- [ ] Unit tests for all edge cases

**Dependencies**: None

---

### Phase 2 Milestone

**Acceptance Criteria**:
- ✅ All HIGH priority issues resolved
- ✅ Blue Book semantics verified and implemented
- ✅ Arithmetic operations correct for all sign combinations
- ✅ Stream operations use correct index base
- ✅ Float edge cases handled safely
- ✅ Comprehensive unit tests pass

**Deliverables**:
- [ ] Updated primitive files
- [ ] Blue Book compliance documentation
- [ ] Expanded unit test suite
- [ ] Git commits with detailed explanations

---

## Phase 3: Infrastructure and Completeness (MEDIUM PRIORITY)

**Goal**: Implement missing features and complete stub primitives.
**Duration**: 5-7 days
**Priority**: NICE TO HAVE

### Task Groups

#### Group 3A: Symbol Interning (1-2 days)
**Tasks**:
- Design SymbolTable class
- Implement intern/lookup methods
- Integrate with GlobalRegistry
- Implement AsSymbol primitive

#### Group 3B: Input/Graphics Stubs (2-3 days)
**Tasks**:
- Design IDisplayProvider interface
- Implement headless stub provider
- Wire into IPrimitiveContext
- Implement input primitives (90-95)
- Validate BitBltEngine and graphics primitives (96-104)

#### Group 3C: Process Scheduler Stubs (2-3 days)
**Tasks**:
- Design ProcessorScheduler architecture
- Implement Process and Semaphore classes
- Implement queue management primitives
- Stub context switching (mark as TODO for Phase 4)
- Implement process primitives (85-88)

#### Group 3D: System Primitives (1 day)
**Tasks**:
- Add heap statistics methods to ISmalltalkHeapManager
- Implement OopsLeft/CoreLeft with real values
- Implement SignalAtOopsLeftWordsLeft

---

### Phase 3 Milestone

**Acceptance Criteria**:
- ✅ Symbol interning works
- ✅ Input primitives have stub infrastructure
- ✅ Process primitives have stub infrastructure
- ✅ System primitives return real values
- ✅ All stub infrastructure documented
- ✅ Clear TODOs for future full implementations

**Deliverables**:
- [ ] New infrastructure classes
- [ ] Updated interfaces
- [ ] Stub implementations with clear documentation
- [ ] Architecture docs for scheduler/display

---

## Phase 4: Polish and Consistency (LOW PRIORITY)

**Goal**: Clean up inconsistencies and improve code quality.
**Duration**: 2-3 days
**Priority**: OPTIONAL

### Tasks
- Standardize nil accessor usage
- Refactor error handling patterns
- Eliminate magic numbers
- Add comprehensive comments
- Performance profiling and optimization

---

## Phase 5: Testing and Validation (ONGOING)

**Goal**: Ensure all fixes work correctly and don't introduce regressions.
**Duration**: Ongoing throughout all phases

### Test Categories

#### Unit Tests (Per-Primitive)
- Input validation
- Edge cases
- Error handling
- Return value correctness

#### Integration Tests
- BlockClosure with nested blocks
- Stream operations on real collections
- LargeInteger overflow from SmallInteger
- Process scheduling (when implemented)

#### Regression Tests
- All existing passing tests continue to pass
- Cold start still completes successfully
- Init expressions still run

#### Performance Tests
- Primitive overhead < 100ns
- No performance regressions

---

## Success Metrics

### Code Quality
- Zero NotImplementedException in primitives
- Zero commented-out code in critical paths
- 100% Blue Book compliance for implemented primitives
- Clear documentation for all stubs

### Test Coverage
- 90%+ line coverage for primitive code
- 100% coverage of CRITICAL/HIGH issues
- Edge cases documented and tested

### Functional
- BlockClosure evaluation works
- Arithmetic correct for all operand combinations
- Stream operations work correctly
- LargeInteger arithmetic functional

---

## Risk Management

| Risk | Impact | Mitigation |
|------|--------|------------|
| Blue Book spec unclear | Medium | Consult multiple sources, test against reference implementation |
| Heap manager changes break cold start | High | Test cold start after every change, atomic commits |
| Performance regression | Medium | Profile before/after, benchmark critical paths |
| Test suite incomplete | Medium | Code review for coverage, TDD approach |

---

## Appendix: Git Patch Files

Git-style patches for critical fixes are available in `docs/patches/` directory:
- `001-blockclosure-signature-fix.patch`
- `002-blockclosure-array-detection.patch`
- `003-control-setindexedoop.patch`
- `004-largeinteger-class-lookup.patch`
- `005-largeinteger-frombiginteger.patch`
- `006-arithmetic-mod-semantics.patch`
- `007-arithmetic-bitshift-bounds.patch`
- `008-stream-index-semantics.patch`

Apply in order using: `git apply docs/patches/<patch-file>`

---

**End of Roadmap**
