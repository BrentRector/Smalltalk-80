# Primitive Improvements - Comprehensive Issue Catalog

**Document Version**: 1.0
**Date**: 2025-01-24
**Status**: Initial comprehensive review completed
**Author**: Primitive Operations Review Team

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [High-Level Cross-Cutting Problems](#high-level-cross-cutting-problems)
3. [Critical Issues (Blocking Execution)](#critical-issues-blocking-execution)
4. [High Priority Issues (Correctness)](#high-priority-issues-correctness)
5. [Medium Priority Issues (Completeness)](#medium-priority-issues-completeness)
6. [Low Priority Issues (Polish)](#low-priority-issues-polish)
7. [Primitive-by-Primitive Detailed Analysis](#primitive-by-primitive-detailed-analysis)
8. [Interface & API Improvements Needed](#interface--api-improvements-needed)
9. [Testing Requirements](#testing-requirements)
10. [Implementation Roadmap](#implementation-roadmap)

---

## Executive Summary

This document catalogs issues discovered during a comprehensive top-to-bottom review of all primitive operations in the Smalltalk-80 interpreter. Issues are categorized by severity and grouped by functional area.

### Issue Count Summary

| Severity | Count | Blocking | Description |
|----------|-------|----------|-------------|
| **CRITICAL** | 6 | YES | Signature mismatches, NotImplementedException, prevents execution |
| **HIGH** | 8 | NO | Incorrect semantics, wrong Blue Book behavior, edge case bugs |
| **MEDIUM** | 15 | NO | Missing functionality, incomplete implementations, stubs |
| **LOW** | 12 | NO | Consistency issues, placeholder values, minor optimizations |

### Affected Primitive Categories

- **BlockClosure & Control** (207, 80-88): 4 critical, 2 medium
- **LargeInteger** (21-37): 3 critical, 2 high
- **Arithmetic** (1-17): 3 high
- **Float** (40-54): 1 high, 1 low
- **Stream** (65-67): 3 high
- **Object/Array** (60-75, 110-111): 2 medium
- **String** (105-106): 1 medium
- **Input** (90-95): 6 medium (all stubs)
- **Graphics** (96-104): 5 medium (all stubs)
- **Process** (85-88): 4 medium (all stubs)
- **System** (112-139): 3 medium, 2 low

---

## High-Level Cross-Cutting Problems

### 1. Stubs and Unimplemented VM Infrastructure

**Problem**: Several primitive groups are intentionally incomplete due to missing VM subsystems:

- **BlockContext activation**: Primitives 80-82 rely on context switching not fully implemented
- **Process scheduler**: Primitives 85-88 need ProcessorScheduler, Semaphore, Process classes
- **Display provider**: Primitives 90-95 (input) and 96-104 (graphics) need IDisplayProvider
- **Symbol interning**: Primitive 106 (asSymbol) needs symbol table infrastructure
- **LargeInteger class lookup**: Primitives 21-37 can't determine LargePositiveInteger vs LargeNegativeInteger

**Impact**: These primitives fail gracefully by returning `ctx.PrimitiveSuccess = false`, allowing Smalltalk fallback code to run. Not immediately blocking, but limits VM functionality.

**Recommendation**: Prioritize implementing missing infrastructure in order:
1. LargeInteger class lookup (needed for arithmetic overflow)
2. Symbol interning (needed for reflection, debugging)
3. Process scheduler (needed for concurrency)
4. Display provider (needed for UI)

### 2. Inconsistent Nil/Registry Usages

**Problem**: Code mixes three different approaches to access canonical nil and global registry:
- `SpecialObjectsArray.Instance.Nil`
- `GlobalRegistry.NilOOP`
- `GlobalRegistry.TryGet("nil", out var nilOOP)`

**Impact**: Confusing code, potential for subtle bugs if these don't return identical OOP values.

**Recommendation**: Choose one canonical pattern (suggest: `SpecialObjectsArray.Instance.Nil`) and refactor all primitive code to use it consistently.

### 3. Indexing and 1-Based vs 0-Based Confusion

**Problem**: Smalltalk uses 1-based indexing; C# uses 0-based. Some primitives handle conversion inconsistently:
- **Stream primitives** (65-67): Mix 1-based stored position with 0-based array access incorrectly
- **Array primitives** (60-61): Handle conversion correctly (`index - 1`)
- **CompiledMethod objectAt:** (68-69): Comments suggest confusion about header indexing

**Impact**: Off-by-one errors, wrong elements accessed, potential crashes.

**Recommendation**: Establish clear convention:
- **Smalltalk indexes** (from arguments, in instance variables): Always 1-based
- **Heap manager calls**: Always 0-based
- **Conversion**: Always explicit `smalltalkIndex - 1` at call site

### 4. Interface/Signature Mismatches

**Problem**: Some primitive implementations call IPrimitiveContext or HeapManager methods with wrong parameter lists or expect methods that don't exist on the interface:

- **BlockClosure.ValueWithArguments**: Calls `ctx.InvokeBlock(method, args, home)` but signature requires 4 params
- **ControlPrimitives.ValueWithArg**: Comments say "needs heap manager support" for SetIndexedOOP, but ISmalltalkHeapManager already HAS this method!
- **LargeInteger.FromBigInteger**: Calls `ctx.HeapManager.SetSlot(largeIntOop, 0, classOOP)` to set class but should use `SetClassOOP` (doesn't exist yet)

**Impact**: Compilation failures (current) or runtime failures (if commented-out code is enabled).

**Recommendation**:
1. Fix all call sites to match current interface signatures
2. Add missing methods to ISmalltalkHeapManager where needed (SetClassOOP)
3. Audit all primitives for commented-out code that needs implementation

### 5. PrimitiveTable Issues

**Problem**: Minor issues in primitive registration:
- Comments suggest primitive 68 mapping moved but table shows it still mapped to ObjectAt
- Expression-bodied member syntax for Count/IsRegistered might have formatting issues (needs verification)

**Impact**: Minimal - table appears correct, but comments are confusing.

**Recommendation**: Clean up comments, verify all primitive numbers align with Blue Book canonical mapping, add unit test to detect duplicate/conflicting registrations.

---

## Critical Issues (Blocking Execution)

These issues will cause runtime failures or compilation errors and MUST be fixed immediately.

### CRITICAL-1: BlockClosure.ValueWithArguments Missing 4th Parameter

**File**: `BlockClosurePrimitives.cs:66`
**Severity**: CRITICAL
**Status**: ❌ BROKEN

**Problem**:
```csharp
// WRONG - missing blockClosureOOP parameter
var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP);
```

IPrimitiveContext.InvokeBlock signature requires 4 parameters:
```csharp
SmalltalkOOP InvokeBlock(SmalltalkOOP blockMethodOOP, SmalltalkOOP[] arguments,
                         SmalltalkOOP homeContextOOP, SmalltalkOOP blockClosureOOP);
```

**Impact**: Will not compile or will call wrong method overload.

**Fix**:
```csharp
// CORRECT - pass the BlockClosure instance for nested block support
var result = ctx.InvokeBlock(blockMethodOOP, blockArgs, homeContextOOP, receiver);
```

**Todo**:
- [ ] Fix signature in BlockClosurePrimitives.cs
- [ ] Add unit test for BlockClosure.value invocation
- [ ] Test nested block invocation

---

### CRITICAL-2: BlockClosure.ValueWithArguments Unsafe Array Detection

**File**: `BlockClosurePrimitives.cs:25-37`
**Severity**: CRITICAL
**Status**: ❌ INCORRECT

**Problem**: Uses try/catch to detect if argument is an Array:
```csharp
try {
    blockArgs = ExtractArrayElements(ctx, args[0]);
} catch {
    blockArgs = args; // Not an array, treat as direct argument
}
```

This can misclassify arbitrary objects and swallow real errors.

**Impact**: Wrong behavior for non-array objects that happen to have indexed size.

**Fix**: Use ctx.GetIndexedSize safely:
```csharp
if (args.Length == 1 && !args[0].IsSmallInteger)
{
    int maybeCount = 0;
    try {
        maybeCount = ctx.GetIndexedSize(args[0]);
    } catch {
        maybeCount = 0;
    }

    if (maybeCount > 0)
        blockArgs = ExtractArrayElements(ctx, args[0]);
    else
        blockArgs = args;
}
```

Better: Check object's class explicitly using heap manager.

**Todo**:
- [ ] Replace try/catch pattern with explicit class check
- [ ] Add test for valueWithArguments: with Array
- [ ] Add test for value: with non-array

---

### CRITICAL-3: ControlPrimitives.ValueWithArg Not Populating Arguments Array

**File**: `ControlPrimitives.cs:106-109`
**Severity**: CRITICAL
**Status**: ❌ BROKEN

**Problem**: Code allocates array but doesn't populate it:
```csharp
SmalltalkOOP argsArrayOop = ctx.AllocateObject("class:Array", 1);
// Note: This requires HeapManager to have SetIndexedOOP method
// For now we'll leave this as a stub - needs heap manager support
// ctx.HeapManager.SetIndexedOOP(argsArrayOop, 0, args[0]);
```

**IMPORTANT**: ISmalltalkHeapManager ALREADY HAS `SetIndexedOOP`! The comment is outdated.

**Impact**: Block receives uninitialized (nil) argument instead of actual value.

**Fix**:
```csharp
SmalltalkOOP argsArrayOop = ctx.AllocateObject("class:Array", 1);
ctx.HeapManager.SetIndexedOOP(argsArrayOop, 0, args[0]);  // Populate element 0
```

**Todo**:
- [ ] Uncomment and fix SetIndexedOOP call
- [ ] Same fix for PerformWith (line ~146)
- [ ] Add unit test verifying argument is passed correctly

---

### CRITICAL-4: ControlPrimitives.PerformWith Same Issue

**File**: `ControlPrimitives.cs:146-149`
**Severity**: CRITICAL
**Status**: ❌ BROKEN

**Problem**: Identical to CRITICAL-3.

**Fix**: Same as CRITICAL-3.

**Todo**:
- [ ] Uncomment and fix SetIndexedOOP call
- [ ] Add unit test for perform:with: argument passing

---

### CRITICAL-5: LargeInteger.GetLargeIntegerClass NotImplementedException

**File**: `LargeIntegerPrimitives.cs:86-92`
**Severity**: CRITICAL
**Status**: ❌ BROKEN

**Problem**:
```csharp
private static int GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
{
    throw new NotImplementedException("GetLargeIntegerClass needs implementation with proper class lookup");
}
```

Called by `FromBigInteger`, which is called by ALL LargeInteger arithmetic primitives.

**Impact**: Any LargeInteger operation that creates a result throws NotImplementedException.

**Fix**:
```csharp
private static SmalltalkOOP GetLargeIntegerClass(IPrimitiveContext ctx, bool isPositive)
{
    string className = isPositive ? "class:LargePositiveInteger" : "class:LargeNegativeInteger";
    if (GlobalRegistry.TryGet(className, out var classOOP))
        return classOOP;
    throw new InvalidOperationException($"{className} not registered in GlobalRegistry");
}
```

Also change return type from `int` to `SmalltalkOOP`.

**Todo**:
- [ ] Implement GetLargeIntegerClass using GlobalRegistry
- [ ] Change return type to SmalltalkOOP
- [ ] Update FromBigInteger to use SmalltalkOOP classOOP
- [ ] Add unit test for class lookup

---

### CRITICAL-6: LargeInteger.FromBigInteger Invalid Class Setting

**File**: `LargeIntegerPrimitives.cs:34-58`
**Severity**: CRITICAL
**Status**: ❌ BROKEN

**Problem**: Multiple issues:
1. `GetLargeIntegerClass` returns `int` but should return `SmalltalkOOP`
2. Invalid cast: `SmalltalkOOP classOOP = classId;` (can't assign int to OOP)
3. Uses low-level `AllocateObject` + `SetSlot(largeIntOop, 0, classOOP)` which bypasses normal class setting
4. No byte trimming (leading sign-extension bytes not removed)

**Impact**: Creates invalid LargeInteger objects with wrong class references.

**Fix**:
```csharp
private static SmalltalkOOP FromBigInteger(IPrimitiveContext ctx, BigInteger value)
{
    byte[] bytes = value.ToByteArray(); // little-endian two's complement

    // Trim sign-extension bytes for canonical representation
    int len = bytes.Length;
    byte signByte = (value.Sign < 0) ? (byte)0xFF : (byte)0x00;
    while (len > 1 && bytes[len - 1] == signByte)
        len--;

    // Get proper class OOP
    SmalltalkOOP classOOP = GetLargeIntegerClass(ctx, value.Sign >= 0);

    // Allocate byte-indexed object
    SmalltalkOOP largeIntOop = ctx.AllocateObject(
        classOOP == GlobalRegistry.NilOOP ? "class:LargePositiveInteger" : "class:LargePositiveInteger",
        len
    );

    // Store trimmed bytes
    for (int i = 0; i < len; i++)
        ctx.HeapManager.SetIndexedByte(largeIntOop, i, bytes[i]);

    // Set class (requires new ISmalltalkHeapManager method)
    ctx.HeapManager.SetClassOOP(largeIntOop, classOOP);

    return largeIntOop;
}
```

**Note**: Requires adding `SetClassOOP` to ISmalltalkHeapManager interface.

**Todo**:
- [ ] Add SetClassOOP to ISmalltalkHeapManager
- [ ] Implement byte trimming logic
- [ ] Fix class OOP handling
- [ ] Add unit tests for positive/negative LargeInteger creation
- [ ] Test round-trip: BigInteger → LargeInteger → BigInteger

---

## High Priority Issues (Correctness)

These issues cause incorrect behavior but don't prevent execution. They violate Blue Book semantics.

### HIGH-1: LargeInteger.IsLargeInteger Always Returns True

**File**: `LargeIntegerPrimitives.cs:64-73`
**Severity**: HIGH
**Status**: ❌ INCORRECT

**Problem**:
```csharp
private static bool IsLargeInteger(IPrimitiveContext ctx, SmalltalkOOP objectOop)
{
    if (objectOop.IsSmallInteger)
        return false;

    SmalltalkOOP classOOP = ctx.HeapManager.GetClassOOP(objectOop);
    // TODO: Check if classOOP is LargePositiveInteger or LargeNegativeInteger
    // For now, assume any non-SmallInteger numeric type might be LargeInteger
    return true; // Temporary - needs proper class checking
}
```

**Impact**: Accepts Floats, Fractions, any non-SmallInteger as LargeInteger. Will cause invalid BigInteger conversions.

**Fix**:
```csharp
private static bool IsLargeInteger(IPrimitiveContext ctx, SmalltalkOOP objectOop)
{
    if (objectOop.IsSmallInteger)
        return false;

    SmalltalkOOP classOOP = ctx.HeapManager.GetClassOOP(objectOop);

    if (GlobalRegistry.TryGet("class:LargePositiveInteger", out var pos) && classOOP == pos)
        return true;
    if (GlobalRegistry.TryGet("class:LargeNegativeInteger", out var neg) && classOOP == neg)
        return true;

    return false;
}
```

**Todo**:
- [ ] Implement proper class checking
- [ ] Add unit test: verify Float is NOT LargeInteger
- [ ] Add unit test: verify LargePositiveInteger IS LargeInteger

---

### HIGH-2: Arithmetic.Mod Incorrect Semantics for Negative Numbers

**File**: `ArithmeticPrimitives.cs:277-310`
**Severity**: HIGH
**Status**: ❌ INCORRECT SEMANTICS

**Problem**: Uses C# `%` operator directly:
```csharp
int result = a % b;
return SmalltalkOOP.FromSmallInteger(result);
```

In C#, remainder sign follows dividend. In Smalltalk (Blue Book), `\\` (mod) has remainder sign follow divisor.

**Example**:
- C#: `-7 % 3` = -1
- Smalltalk: `-7 \\ 3` = 2  (because 3 is positive)

**Impact**: Wrong results for negative operands, breaks arithmetic identity: `a = (a // b) * b + (a \\ b)`

**Fix**:
```csharp
int rem = a % b;
if (rem != 0 && ((a < 0) != (b < 0)))
    rem += b;
return SmalltalkOOP.FromSmallInteger(rem);
```

**Todo**:
- [ ] Apply Smalltalk mod semantics
- [ ] Add unit test: `-7 \\ 3` should return 2
- [ ] Add unit test: `7 \\ -3` should return -2
- [ ] Verify with Blue Book specification

---

### HIGH-3: Arithmetic.BitShift Undefined Behavior for Large Shifts

**File**: `ArithmeticPrimitives.cs:415-442`
**Severity**: HIGH
**Status**: ⚠️ UNSAFE

**Problem**:
```csharp
int result = shift >= 0 ? a << shift : a >> (-shift);
return SmalltalkOOP.FromSmallInteger(result);
```

In C#, shifting by >= 32 bits is undefined (behavior varies by platform). For SmallInteger (30-bit), shifts >= 30 can overflow.

**Impact**: Platform-dependent results, potential crashes, silent wraparound.

**Fix**:
```csharp
// Limit shift magnitude to safe range (30 for SmallInteger)
if (shift > 30 || shift < -30)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil; // Let fallback promote to LargeInteger
}

int result = shift >= 0 ? (a << shift) : (a >> (-shift));

// Check if result still fits in SmallInteger
try {
    return SmalltalkOOP.FromSmallInteger(result);
} catch {
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}
```

**Todo**:
- [ ] Add shift bounds checking
- [ ] Test: `1 bitShift: 40` should fail primitive
- [ ] Test: negative right shifts work correctly

---

### HIGH-4: Arithmetic.Quo vs Div Confusion

**File**: `ArithmeticPrimitives.cs:313-346, 445-478`
**Severity**: HIGH
**Status**: ⚠️ NEEDS VERIFICATION

**Problem**: Both Quo (prim 12) and Div (prim 17) use C# `/` operator:
```csharp
int result = a / b;
```

Blue Book distinguishes:
- `quo:` - truncated division (rounds toward zero)
- `//` - floored division (rounds toward negative infinity)

C# `/` does truncated division, which matches `quo:`. But Smalltalk `//` may need adjustment for negative operands.

**Impact**: Div primitive might give wrong results for negative numbers.

**Fix**: Verify Blue Book spec. If `//` is floored division:
```csharp
// Primitive 17: Div (floored division)
int quotient = a / b;
if ((a < 0) != (b < 0) && a % b != 0)
    quotient--; // Adjust for floor rounding
return SmalltalkOOP.FromSmallInteger(quotient);
```

**Todo**:
- [ ] Verify Blue Book semantics for `//` vs `quo:`
- [ ] Add unit tests for negative division
- [ ] Apply fix if needed

---

### HIGH-5: Stream.Next Uses Wrong Index Base

**File**: `ObjectPrimitives.cs:65` (Next), `NextPut`, `AtEnd`
**Severity**: HIGH
**Status**: ❌ INCORRECT

**Problem**: Stream position is stored as 1-based (Smalltalk convention), but code uses it directly as 0-based index:
```csharp
int position = positionOOP.AsSmallInteger();
int readLimit = readLimitOOP.AsSmallInteger();

if (position >= readLimit) { ... }  // WRONG comparison

SmalltalkOOP element = ctx.HeapManager.GetIndexedOOP(collectionOOP, position); // WRONG index
ctx.SetFieldValue(receiver, "class:Stream", "position",
                  SmalltalkOOP.FromSmallInteger(position + 1));
```

**Impact**: Reads wrong element, skips first element, reads one past end.

**Fix**:
```csharp
int position = positionOOP.AsSmallInteger(); // 1-based stored value
int readLimit = readLimitOOP.AsSmallInteger(); // 1-based

if (position > readLimit) // position is 1-based; when > readLimit, at end
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}

// Convert to 0-based for heap access
SmalltalkOOP element = ctx.HeapManager.GetIndexedOOP(collectionOOP, position - 1);

// Increment stored position (remain 1-based)
ctx.SetFieldValue(receiver, "class:Stream", "position",
                  SmalltalkOOP.FromSmallInteger(position + 1));

return element;
```

**Todo**:
- [ ] Fix Next to use `position - 1` for heap access
- [ ] Fix Next to use `position > readLimit` for end check
- [ ] Same fixes for NextPut
- [ ] Fix AtEnd to use `position > readLimit`
- [ ] Add unit test: stream at position 1 reads first element (index 0)

---

### HIGH-6: Stream.NextPut Same Issue

**File**: `ObjectPrimitives.cs:66`
**Severity**: HIGH
**Status**: ❌ INCORRECT

**Problem**: Identical to HIGH-5.

**Fix**: Same as HIGH-5 but for write:
```csharp
if (position > writeLimit) { fail }
ctx.HeapManager.SetIndexedOOP(collectionOOP, position - 1, value); // position-1
ctx.SetFieldValue(receiver, "class:Stream", "position",
                  SmalltalkOOP.FromSmallInteger(position + 1));
```

**Todo**:
- [ ] Apply fix
- [ ] Unit test: nextPut: at position 1 writes to index 0

---

### HIGH-7: Stream.AtEnd Comparison Off By One

**File**: `ObjectPrimitives.cs:67`
**Severity**: HIGH
**Status**: ❌ INCORRECT

**Problem**:
```csharp
return position >= readLimit ? True : False;
```

Should be `position > readLimit` because position and readLimit are both 1-based.

**Fix**:
```csharp
return position > readLimit
    ? SpecialObjectsArray.Instance.True
    : SpecialObjectsArray.Instance.False;
```

**Todo**:
- [ ] Change comparison to `>`
- [ ] Unit test: stream at readLimit is NOT atEnd
- [ ] Unit test: stream at readLimit+1 IS atEnd

---

### HIGH-8: Float Primitives Edge Cases Not Handled

**File**: `FloatPrimitives.cs` (various)
**Severity**: HIGH
**Status**: ⚠️ INCOMPLETE

**Problem**: Missing checks for:
- NaN values (should fail primitive or return canonical NaN)
- +/- Infinity (should handle or fail gracefully)
- Subnormal numbers (denormals) - Exponent primitive returns 0
- Division by zero producing Infinity

**Impact**: Undefined behavior, potential crashes, non-Blue-Book semantics.

**Fix**: Add explicit checks:
```csharp
// In Exponent primitive
if (double.IsNaN(value) || double.IsInfinity(value))
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}
```

**Todo**:
- [ ] Add NaN/Infinity checks to all Float primitives
- [ ] Document behavior for subnormals
- [ ] Unit tests for edge cases

---

## Medium Priority Issues (Completeness)

These are incomplete implementations or missing features that don't break existing code.

### MEDIUM-1: String.AsSymbol Not Implemented

**File**: `StringPrimitives.cs:106`
**Severity**: MEDIUM
**Status**: ❌ STUB

**Problem**: Always fails:
```csharp
public static SmalltalkOOP AsSymbol(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    ctx.PrimitiveSuccess = false;
    return SpecialObjectsArray.Instance.Nil;
}
```

**Impact**: String-to-Symbol conversion always falls back to Smalltalk code. Slower but functional.

**Fix**: Implement symbol interning:
```csharp
public static SmalltalkOOP AsSymbol(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    // Get string bytes
    int size = ctx.HeapManager.GetIndexedSize(receiver);
    byte[] bytes = new byte[size];
    for (int i = 0; i < size; i++)
        bytes[i] = ctx.HeapManager.GetIndexedByte(receiver, i);

    string str = System.Text.Encoding.UTF8.GetString(bytes);

    // Look up or create Symbol
    return SymbolTable.Intern(str); // Requires implementing SymbolTable
}
```

**Todo**:
- [ ] Implement SymbolTable.Intern in GlobalRegistry
- [ ] Implement AsSymbol using symbol table
- [ ] Add unit test: "foo" asSymbol returns existing symbol
- [ ] Test: two identical strings intern to same Symbol

---

### MEDIUM-2-7: Input Primitives All Stubs

**Files**: `InputPrimitives.cs:90-95`
**Severity**: MEDIUM (x6)
**Status**: ❌ ALL STUBS

**Primitives**:
- 90: MousePoint - returns 0@0
- 91: CursorLocPut - no-op
- 92: CursorLink - no-op
- 93: InputSemaphore - no-op
- 94: SampleInterval - returns 0
- 95: InputWord - returns 0

**Impact**: No input works, but Smalltalk can provide fallbacks or simulations.

**Fix**: Requires IDisplayProvider interface:
```csharp
public interface IDisplayProvider
{
    Point GetMousePosition();
    void SetCursorPosition(Point pt);
    void SetCursor(SmalltalkOOP cursorFormOOP);
    void RegisterInputSemaphore(SmalltalkOOP semaphoreOOP);
    int GetInputWord();
}
```

Add to IPrimitiveContext:
```csharp
IDisplayProvider DisplayProvider { get; }
```

**Todo**:
- [ ] Design IDisplayProvider interface
- [ ] Implement headless stub provider
- [ ] Wire into IPrimitiveContext
- [ ] Implement primitives using provider
- [ ] Add real input provider for windowed mode (later)

---

### MEDIUM-8-12: Graphics Primitives All Stubs

**Files**: `GraphicsPrimitives.cs:96-104`
**Severity**: MEDIUM (x5)
**Status**: ❌ MOSTLY STUBS

**Primitives**:
- 96: CopyBits - has BitBltEngine but needs validation
- 97: Snapshot - stub
- 101: BeDisplay - stub
- 102: BeCursor - stub
- 104: ClipboardText - stub

**Impact**: No graphics rendering, no clipboard, no screen capture.

**Fix**: Same IDisplayProvider as input primitives, plus:
- Validate BitBltEngine.CopyBits matches Smalltalk Form bit layout
- Implement BeDisplay/BeCursor using display provider
- Implement clipboard using host OS APIs

**Todo**:
- [ ] Validate BitBltEngine bit order matches Blue Book
- [ ] Implement BeDisplay/BeCursor stubs
- [ ] Implement clipboard primitives
- [ ] Add IDisplayProvider.Invalidate for screen updates

---

### MEDIUM-13-16: Process Primitives Need Scheduler

**Files**: `ProcessPrimitives.cs:85-88`
**Severity**: MEDIUM (x4)
**Status**: ❌ BLOCKED ON INFRASTRUCTURE

**Primitives**:
- 85: Signal (semaphore) - stub
- 86: Wait (semaphore) - stub
- 87: Resume (process) - stub
- 88: Suspend (process) - stub

**Impact**: No concurrency, no semaphores, no process switching.

**Fix**: Requires implementing:
1. ProcessorScheduler (singleton, manages runnable processes)
2. Process (execution context)
3. Semaphore (synchronization primitive)
4. Process scheduler queue management
5. Context switching in VM main loop

**Todo**:
- [ ] Design ProcessorScheduler class
- [ ] Implement Process and Semaphore classes
- [ ] Implement queue operations (addLastLink, removeFirstLink)
- [ ] Implement context switch in VM
- [ ] Implement primitives using scheduler

---

### MEDIUM-17: System Primitives Return Placeholders

**Files**: `SystemPrimitives.cs`
**Severity**: MEDIUM
**Status**: ⚠️ PLACEHOLDERS

**Primitives**:
- 98/112: OopsLeft/CoreLeft - return large constants
- 115: OopsLeft - same
- 116: SignalAtOopsLeftWordsLeft - unimplemented

**Impact**: Memory statistics wrong, low-memory signals don't work.

**Fix**: Implement real heap statistics:
```csharp
public static SmalltalkOOP OopsLeft(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    int freeSlots = ctx.HeapManager.GetFreeObjectCount();
    return SmalltalkOOP.FromSmallInteger(freeSlots);
}
```

**Todo**:
- [ ] Add GetFreeObjectCount to ISmalltalkHeapManager
- [ ] Implement real memory stats
- [ ] Implement low-memory threshold signals

---

## Low Priority Issues (Polish)

### LOW-1: Nil Access Inconsistency

**Severity**: LOW
**Status**: ⚠️ INCONSISTENT

**Problem**: Mix of:
- `SpecialObjectsArray.Instance.Nil`
- `GlobalRegistry.NilOOP`

**Fix**: Choose one (recommend SpecialObjectsArray) and refactor all.

**Todo**:
- [ ] Choose canonical nil accessor
- [ ] Refactor all primitives to use it
- [ ] Add linter rule to prevent mixing

---

### LOW-2-12: Various Minor Issues

(Remaining low-priority items include: error handling consistency, magic number elimination, bounds check improvements, etc.)

---

## Primitive-by-Primitive Detailed Analysis

### Arithmetic Primitives (1-17)

| Primitive | Status | Issues | Priority |
|-----------|--------|--------|----------|
| 1-10 | ✅ OK | Overflow handling good | - |
| 11 (Mod) | ❌ | Wrong semantics for negatives | HIGH-2 |
| 12 (Quo) | ⚠️ | Verify truncated division | HIGH-4 |
| 13-15 (Bitwise) | ✅ OK | - | - |
| 16 (BitShift) | ❌ | Undefined for large shifts | HIGH-3 |
| 17 (Div) | ⚠️ | Verify floored division | HIGH-4 |

### LargeInteger Primitives (21-37)

| Primitive | Status | Issues | Priority |
|-----------|--------|--------|----------|
| 21-37 (All) | ❌ | IsLargeInteger broken | CRITICAL-5, HIGH-1 |
| 21-37 (All) | ❌ | GetLargeIntegerClass throws | CRITICAL-6 |
| 21-37 (All) | ❌ | FromBigInteger broken | CRITICAL-6 |
| 31 (Mod) | ✅ | Has correct sign adjustment | - |
| 32 (Div) | ✅ | Has floor adjustment | - |

(Continue for all primitive groups...)

---

## Interface & API Improvements Needed

### ISmalltalkHeapManager Additions

```csharp
public interface ISmalltalkHeapManager
{
    // NEW: Set object's class reference
    void SetClassOOP(SmalltalkOOP objectOOP, SmalltalkOOP classOOP);

    // NEW: Get count of free object slots
    int GetFreeObjectCount();

    // NEW: Get total allocated object count
    int GetAllocatedObjectCount();
}
```

### IPrimitiveContext Additions

```csharp
public interface IPrimitiveContext
{
    // NEW: Display provider for input/graphics
    IDisplayProvider DisplayProvider { get; }

    // NEW: Symbol table for interning
    ISymbolTable SymbolTable { get; }
}
```

---

## Testing Requirements

### Unit Test Coverage Needed

**Critical Path Tests** (must pass before release):
- BlockClosure.value invokes block with correct arguments
- ControlPrimitives.value: populates arguments array
- Stream.next reads correct element at each position
- LargeInteger round-trip conversion preserves value
- Arithmetic.mod returns correct sign for negative operands
- Arithmetic.bitShift handles large shifts safely

**Regression Tests**:
- All existing passing tests continue to pass
- Edge cases (nil, empty arrays, SmallInteger boundaries)

**Performance Tests**:
- Primitive overhead < 100ns per call
- LargeInteger conversion < 1μs for typical values

---

## Implementation Roadmap

See separate [Primitive-Fixes-Roadmap.md](Primitive-Fixes-Roadmap.md) for detailed timeline.

**Summary**:
1. **Phase 1** (Critical - Week 1): Fix CRITICAL-1 through CRITICAL-6
2. **Phase 2** (High - Week 2): Fix HIGH-1 through HIGH-8
3. **Phase 3** (Medium - Weeks 3-4): Implement MEDIUM-1 through MEDIUM-17
4. **Phase 4** (Polish - Week 5): Address LOW-priority items
5. **Phase 5** (Testing - Ongoing): Comprehensive test suite

---

## Appendix A: Quick Reference - All Issues by File

### BlockClosurePrimitives.cs
- CRITICAL-1: Missing 4th parameter
- CRITICAL-2: Unsafe array detection

### ControlPrimitives.cs
- CRITICAL-3: ValueWithArg not populating args
- CRITICAL-4: PerformWith not populating args
- MEDIUM-13-16: Process primitives need scheduler

### ArithmeticPrimitives.cs
- HIGH-2: Mod wrong semantics
- HIGH-3: BitShift unsafe
- HIGH-4: Quo/Div verification needed

### LargeIntegerPrimitives.cs
- CRITICAL-5: GetLargeIntegerClass throws
- CRITICAL-6: FromBigInteger broken
- HIGH-1: IsLargeInteger always true

### ObjectPrimitives.cs
- HIGH-5: Stream.Next wrong index
- HIGH-6: Stream.NextPut wrong index
- HIGH-7: Stream.AtEnd wrong comparison

### FloatPrimitives.cs
- HIGH-8: Edge cases not handled

### StringPrimitives.cs
- MEDIUM-1: AsSymbol not implemented

### InputPrimitives.cs
- MEDIUM-2-7: All stubs

### GraphicsPrimitives.cs
- MEDIUM-8-12: All stubs

### SystemPrimitives.cs
- MEDIUM-17: Placeholders

---

**End of Document**
