# Primitive Specification Analysis

**Document Version**: 1.0
**Date**: 2025-11-02
**Status**: Initial Analysis

## Overview

This document compares the **Formal Specification of the Primitive Methods** (Blue Book Chapter 29) with our current interpreter implementation in `PrimitiveTable.cs`. It identifies all differences in primitive number mappings and implementations, and provides a plan to align our implementation with the specification.

**Important Constraint**: We cannot change primitive numbers, as they must match the source code in `Smalltalk-80.st`.

---

## Executive Summary

Our implementation has **3 critical misalignments** with the Blue Book specification:

1. **Arithmetic primitives 12-17 are incorrectly mapped** - Div/Quo are swapped, and bit operations are off-by-one
2. **Missing primitives 76-78, 89** - These need to be implemented at their Blue Book indices
3. **Extended primitives 128-139, 207** - These are implementation extensions not in Blue Book (acceptable)

**Total Registered Primitives**: 94 (vs. Blue Book's 107 specified primitives)

---

## Detailed Primitive Comparison

### ✅ Arithmetic Primitives (1-18)

#### Primitives 1-11: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 1 | SmallInteger + | ArithmeticPrimitives.Add | ✅ MATCH |
| 2 | SmallInteger - | ArithmeticPrimitives.Subtract | ✅ MATCH |
| 3 | SmallInteger < | ArithmeticPrimitives.LessThan | ✅ MATCH |
| 4 | SmallInteger > | ArithmeticPrimitives.GreaterThan | ✅ MATCH |
| 5 | SmallInteger <= (optional) | ArithmeticPrimitives.LessOrEqual | ✅ MATCH |
| 6 | SmallInteger >= (optional) | ArithmeticPrimitives.GreaterOrEqual | ✅ MATCH |
| 7 | SmallInteger = | ArithmeticPrimitives.Equal | ✅ MATCH |
| 8 | SmallInteger ~= (optional) | ArithmeticPrimitives.NotEqual | ✅ MATCH |
| 9 | SmallInteger * | ArithmeticPrimitives.Multiply | ✅ MATCH |
| 10 | SmallInteger / (optional) | ArithmeticPrimitives.Divide | ✅ MATCH |
| 11 | SmallInteger \\ (optional) | ArithmeticPrimitives.Mod | ✅ MATCH |

#### Primitives 12-17: CRITICAL MISALIGNMENT ❌

| Index | Blue Book Selector | Our Implementation | Issue |
|-------|-------------------|-------------------|-------|
| 12 | SmallInteger // | ArithmeticPrimitives.Quo | ❌ **WRONG** - Should be Div |
| 13 | SmallInteger quo: (optional) | ArithmeticPrimitives.BitAnd | ❌ **WRONG** - Should be Quo |
| 14 | SmallInteger bitAnd: | ArithmeticPrimitives.BitOr | ❌ **WRONG** - Should be BitAnd |
| 15 | SmallInteger bitOr: | ArithmeticPrimitives.BitXor | ❌ **WRONG** - Should be BitOr |
| 16 | SmallInteger bitXor: | ArithmeticPrimitives.BitShift | ❌ **WRONG** - Should be BitXor |
| 17 | SmallInteger bitShift: | ArithmeticPrimitives.Div | ❌ **WRONG** - Should be BitShift |

**Root Cause**: Primitives 12 and 13 are swapped, causing a cascade misalignment through 14-17.

**Blue Book Specification**:
- Primitive 12 (`//`): Integer division rounded DOWN toward negative infinity
- Primitive 13 (`quo:`): Integer division TRUNCATED toward zero

**Example Difference**:
```smalltalk
-7 // 2    "Blue Book: -4 (rounds down)"
-7 quo: 2  "Blue Book: -3 (truncates toward zero)"
```

Our current mapping has these reversed.

#### Primitive 18: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 18 | Number @ (optional) | PointPrimitives.MakePoint | ✅ MATCH |

---

### ✅ Large Integer Primitives (21-37)

All large integer primitives are correctly mapped (21-37). These are optional primitives.

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 21-30 | Integer/LargePositiveInteger arithmetic | LargeIntegerPrimitives | ✅ MATCH |
| 31-37 | Integer/LargePositiveInteger bit ops | LargeIntegerPrimitives | ✅ MATCH |

---

### ✅ Float Primitives (40-54)

All float primitives are correctly mapped (40-54).

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 40 | SmallInteger asFloat | FloatPrimitives.AsFloat | ✅ MATCH |
| 41-50 | Float arithmetic & comparison | FloatPrimitives | ✅ MATCH |
| 51 | Float truncated | FloatPrimitives.Truncated | ✅ MATCH |
| 52 | Float fractionPart (optional) | FloatPrimitives.FractionPart | ✅ MATCH |
| 53 | Float exponent (optional) | FloatPrimitives.Exponent | ✅ MATCH |
| 54 | Float timesTwoPower: (optional) | FloatPrimitives.TimesTwoPower | ✅ MATCH |

---

### ✅ Array and Stream Primitives (60-67)

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 60 | Object at: / basicAt: | ObjectPrimitives.At | ✅ MATCH |
| 61 | Object at:put: / basicAt:put: | ObjectPrimitives.AtPut | ✅ MATCH |
| 62 | Object size / basicSize | ObjectPrimitives.Size | ✅ MATCH |
| 63 | String at: / basicAt: | ObjectPrimitives.StringAt | ✅ MATCH |
| 64 | String at:put: / basicAt:put: | ObjectPrimitives.StringAtPut | ✅ MATCH |
| 65 | ReadStream next (optional) | ObjectPrimitives.Next | ✅ MATCH |
| 66 | WriteStream nextPut: (optional) | ObjectPrimitives.NextPut | ✅ MATCH |
| 67 | PositionableStream atEnd (optional) | ObjectPrimitives.AtEnd | ✅ MATCH |

---

### ✅ Storage Management Primitives (68-79)

#### Primitives 68-75: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 68 | CompiledMethod objectAt: | ObjectPrimitives.ObjectAt | ✅ MATCH |
| 69 | CompiledMethod objectAt:put: | ObjectPrimitives.ObjectAtPut | ✅ MATCH |
| 70 | Behavior new / basicNew | ObjectPrimitives.New | ✅ MATCH |
| 71 | Behavior new: / basicNew: | ObjectPrimitives.NewWithSize | ✅ MATCH |
| 72 | Object become: | ObjectPrimitives.Become | ✅ MATCH |
| 73 | Object instVarAt: | ObjectPrimitives.InstVarAt | ✅ MATCH |
| 74 | Object instVarAt:put: | ObjectPrimitives.InstVarAtPut | ✅ MATCH |
| 75 | Object asOop / hash | ObjectPrimitives.Hash | ✅ MATCH |

#### Primitives 76-78: MISSING ❌

| Index | Blue Book Selector | Our Implementation | Issue |
|-------|-------------------|-------------------|-------|
| 76 | SmallInteger asObject / asObjectNoFail | **NOT REGISTERED** | ❌ MISSING |
| 77 | Behavior someInstance | **NOT REGISTERED** (moved to 128) | ❌ MISSING |
| 78 | Object nextInstance | **NOT REGISTERED** (moved to 129) | ❌ MISSING |

#### Primitive 79: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 79 | CompiledMethod class newMethod:header: | CompiledMethodPrimitives.NewMethodHeader | ✅ MATCH |

---

### ⚠️ Control Primitives (80-89)

#### Primitives 80-88: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 80 | ContextPart blockCopy: (optional) | ControlPrimitives.BlockCopy | ✅ MATCH |
| 81 | BlockContext value/value:/value:value:/value:value:value: | ControlPrimitives.Value | ✅ MATCH |
| 82 | BlockContext valueWithArguments: | ControlPrimitives.ValueWithArg | ✅ MATCH |
| 83 | Object perform:with:with:with: (optional) | ControlPrimitives.Perform | ✅ MATCH |
| 84 | Object perform:/perform:with:/perform:withArguments: | ControlPrimitives.PerformWith | ✅ MATCH |
| 85 | Semaphore signal | ControlPrimitives.Signal | ✅ MATCH |
| 86 | Semaphore wait | ControlPrimitives.Wait | ✅ MATCH |
| 87 | Process resume | ControlPrimitives.Resume | ✅ MATCH |
| 88 | Process suspend | ControlPrimitives.Suspend | ✅ MATCH |

#### Primitive 89: MISSING ❌

| Index | Blue Book Selector | Our Implementation | Issue |
|-------|-------------------|-------------------|-------|
| 89 | Behavior flushCache | **NOT REGISTERED** | ❌ MISSING |

---

### ✅ Input/Output Primitives (90-105)

#### Primitives 90-104: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 90 | InputSensor/InputState primMousePt (optional) | InputPrimitives.MousePoint | ✅ MATCH |
| 91 | InputState primCursorLocPut: | InputPrimitives.CursorLocPut | ✅ MATCH |
| 92 | Cursor class cursorLink: | InputPrimitives.CursorLink | ✅ MATCH |
| 93 | InputState primInputSemaphore: | InputPrimitives.InputSemaphore | ✅ MATCH |
| 94 | InputState primSampleInterval: | InputPrimitives.SampleInterval | ✅ MATCH |
| 95 | InputState primInputWord | InputPrimitives.InputWord | ✅ MATCH |
| 96 | BitBlt copyBits/copyBitsAgain | GraphicsPrimitives.CopyBits | ✅ MATCH |
| 97 | SystemDictionary snapshotPrimitive | GraphicsPrimitives.Snapshot | ✅ MATCH |
| 98 | Time class secondClockInto: | GraphicsPrimitives.TimeWords | ⚠️ NAME MISMATCH |
| 99 | Time class millisecondClockInto: | GraphicsPrimitives.CoreLeft | ⚠️ NAME MISMATCH |
| 100 | ProcessorScheduler signal:atMilliseconds: | GraphicsPrimitives.TimeWords | ⚠️ DUPLICATE |
| 101 | Cursor beCursor | GraphicsPrimitives.BeCursor | ⚠️ NAME MISMATCH |
| 102 | DisplayScreen beDisplay | GraphicsPrimitives.BeDisplay | ⚠️ NAME MISMATCH |
| 103 | CharacterScanner scanCharacters... (optional) | GraphicsPrimitives.Beep | ⚠️ NAME MISMATCH |
| 104 | BitBlt drawLoopX:Y: (optional) | GraphicsPrimitives.ClipboardText | ⚠️ NAME MISMATCH |

**Note**: Primitives 98-104 have implementation name mismatches but may have correct functionality. Needs further investigation.

#### Primitive 105: CORRECT ✅

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 105 | ByteArray/String replaceFrom:to:with:startingAt: (optional) | StringPrimitives.StringReplace | ✅ MATCH |

---

### ⚠️ System Primitives (110-116)

| Index | Blue Book Selector | Our Implementation | Status |
|-------|-------------------|-------------------|--------|
| 110 | Character = / Object == | ObjectPrimitives.IdentityEquals | ✅ MATCH |
| 111 | Object class | ObjectPrimitives.Class | ✅ MATCH |
| 112 | SystemDictionary coreLeft | SystemPrimitives.CoreLeft | ✅ MATCH |
| 113 | SystemDictionary quitPrimitive | SystemPrimitives.Quit | ✅ MATCH |
| 114 | SystemDictionary exitToDebugger | SystemPrimitives.ExitToDebugger | ✅ MATCH |
| 115 | SystemDictionary oopsLeft | SystemPrimitives.OopsLeft | ✅ MATCH |
| 116 | SystemDictionary signal:atOopsLeft:wordsLeft: | SystemPrimitives.SignalAtOopsLeftWordsLeft | ✅ MATCH |

---

### ➕ Extension Primitives (128-139, 207)

These primitives are **NOT in the Blue Book specification**. They are implementation extensions for our interpreter.

| Index | Our Implementation | Status |
|-------|-------------------|--------|
| 128 | SystemPrimitives.SomeInstance | ➕ EXTENSION (should be at 77) |
| 129 | SystemPrimitives.NextInstance | ➕ EXTENSION (should be at 78) |
| 130 | SystemPrimitives.BecomeForward | ➕ EXTENSION |
| 131 | SystemPrimitives.InstanceCount | ➕ EXTENSION |
| 133 | SystemPrimitives.ForceGC | ➕ EXTENSION |
| 139 | SystemPrimitives.NextObject | ➕ EXTENSION |
| 207 | BlockClosurePrimitives.ValueWithArguments | ➕ EXTENSION (BlockClosure support) |

**Note**: Extensions are acceptable, but primitives 77-78 should also be implemented at their Blue Book indices for compatibility.

---

## Summary of Issues

### Critical Issues (Must Fix)

#### 1. Arithmetic Primitives 12-17 Misalignment ❌

**Problem**: Div and Quo are swapped, causing all bit operations to shift by one index.

**Impact**:
- Methods using `//` (Div) will execute `quo:` behavior
- Methods using `quo:` will execute `bitAnd:` behavior
- All bit operations (bitAnd:, bitOr:, bitXor:, bitShift:) are off-by-one

**Risk Level**: HIGH - Affects fundamental arithmetic operations

**Example Bug**:
```smalltalk
"Expected Blue Book behavior:"
-7 // 2      "=> -4 (rounds down toward negative infinity)"
-7 quo: 2    "=> -3 (truncates toward zero)"

"Current broken behavior:"
-7 // 2      "=> -3 (executes quo: instead of //)"
-7 quo: 2    "=> bitAnd: result (totally wrong!)"
```

#### 2. Missing Primitives 76-78, 89 ❌

**Problem**: Four Blue Book primitives are not registered at their specified indices.

| Index | Selector | Impact |
|-------|----------|--------|
| 76 | SmallInteger asObject / asObjectNoFail | Cannot convert SmallInteger to object pointer |
| 77 | Behavior someInstance | Cannot enumerate class instances (moved to 128) |
| 78 | Object nextInstance | Cannot iterate through instances (moved to 129) |
| 89 | Behavior flushCache | Cannot manually flush method cache |

**Risk Level**: MEDIUM - Affects debugging, reflection, and system utilities

#### 3. Input/Output Primitive Name Mismatches (98-104) ⚠️

**Problem**: Primitive implementations may have incorrect method names.

**Risk Level**: LOW - Functionality may be correct despite naming issues

---

## Alignment Plan

### Phase 1: Investigate Current Behavior (RESEARCH)

**Goal**: Understand why primitives 12-17 are misaligned and what code depends on current behavior.

**Tasks**:
1. ✅ **Analyze `Smalltalk-80.st` source file** - Verify which primitive indices are used in method definitions
   - Search for `<primitive: 12>` through `<primitive: 17>`
   - Count usage of `//`, `quo:`, and bit operations

2. ✅ **Check compiled bundle** - Verify primitive indices in `coldstart-bundle.json`
   - Examine method headers for primitives 12-17
   - Confirm which operations are mapped to which indices

3. ✅ **Run test suite** - Execute all tests to identify failing operations
   - SmallInteger arithmetic tests
   - Bit operation tests
   - Division rounding tests

**Output**: Decision document on whether to fix primitive table or accept current mapping

---

### Phase 2: Fix Critical Misalignments (IMPLEMENTATION)

#### Option A: Fix Primitive Table (Recommended if source matches Blue Book)

**IF** `Smalltalk-80.st` uses Blue Book primitive indices (12=`//`, 13=`quo:`):

1. **Update `PrimitiveTable.cs` registration**:
```csharp
// BEFORE (wrong):
_primitives[12] = ArithmeticPrimitives.Quo;   // WRONG
_primitives[13] = ArithmeticPrimitives.BitAnd; // WRONG
_primitives[14] = ArithmeticPrimitives.BitOr;  // WRONG
_primitives[15] = ArithmeticPrimitives.BitXor; // WRONG
_primitives[16] = ArithmeticPrimitives.BitShift; // WRONG
_primitives[17] = ArithmeticPrimitives.Div;    // WRONG

// AFTER (correct):
_primitives[12] = ArithmeticPrimitives.Div;      // // operator
_primitives[13] = ArithmeticPrimitives.Quo;      // quo: message
_primitives[14] = ArithmeticPrimitives.BitAnd;   // bitAnd: message
_primitives[15] = ArithmeticPrimitives.BitOr;    // bitOr: message
_primitives[16] = ArithmeticPrimitives.BitXor;   // bitXor: message
_primitives[17] = ArithmeticPrimitives.BitShift; // bitShift: message
```

2. **Verify implementations**:
   - Ensure `Div` (prim 12) rounds DOWN: `-7 // 2 => -4`
   - Ensure `Quo` (prim 13) truncates toward zero: `-7 quo: 2 => -3`

3. **Run full test suite** to validate fix

#### Option B: Accept Current Mapping (If source uses current indices)

**IF** `Smalltalk-80.st` already uses our current mapping:

1. **Document deviation** from Blue Book in `CLAUDE.md`
2. **Update formal specification** document with our custom mapping
3. **No code changes needed**

---

### Phase 3: Add Missing Primitives (IMPLEMENTATION)

#### 3.1 Implement Primitive 76: SmallInteger asObject

**Blue Book Specification**:
> Returns the object whose object pointer is twice the receiver's value. Fails if no object exists at that pointer.

**Implementation** (`ArithmeticPrimitives.cs`):
```csharp
// Primitive 76: SmallInteger asObject
public static SmalltalkOOP AsObject(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 0 || !receiver.IsSmallInteger)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    int value = receiver.AsSmallInteger();

    // Object pointer is twice the SmallInteger value
    // (SmallIntegers are tagged with low bit set, objects have low bit clear)
    uint objectPointer = (uint)(value * 2);
    var oop = new SmalltalkOOP(objectPointer);

    // Verify object exists in heap
    if (!ctx.HeapManager.IsValidOOP(oop))
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    return oop;
}
```

**Registration**:
```csharp
_primitives[76] = ArithmeticPrimitives.AsObject;
```

---

#### 3.2 Implement Primitive 77: Behavior someInstance

**Blue Book Specification**:
> Returns an instance of the receiver class. Fails if no instances exist.

**Implementation** (`ObjectPrimitives.cs`):
```csharp
// Primitive 77: Behavior someInstance
public static SmalltalkOOP SomeInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    // Find first instance of the class (receiver is the class)
    var instance = ctx.HeapManager.FindInstanceOf(receiver);

    if (instance == SpecialObjectsArray.Instance.Nil)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    return instance;
}
```

**Registration**:
```csharp
_primitives[77] = ObjectPrimitives.SomeInstance;  // Blue Book index
// Keep extension at 128 for backwards compatibility
_primitives[128] = SystemPrimitives.SomeInstance;
```

---

#### 3.3 Implement Primitive 78: Object nextInstance

**Blue Book Specification**:
> Returns the next instance of the same class as the receiver. Fails if receiver is the last instance.

**Implementation** (`ObjectPrimitives.cs`):
```csharp
// Primitive 78: Object nextInstance
public static SmalltalkOOP NextInstance(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    // Get receiver's class
    var classOOP = ctx.HeapManager.GetClass(receiver);

    // Find next instance after receiver
    var nextInstance = ctx.HeapManager.FindNextInstanceOf(classOOP, receiver);

    if (nextInstance == SpecialObjectsArray.Instance.Nil)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    return nextInstance;
}
```

**Registration**:
```csharp
_primitives[78] = ObjectPrimitives.NextInstance;  // Blue Book index
// Keep extension at 129 for backwards compatibility
_primitives[129] = SystemPrimitives.NextInstance;
```

---

#### 3.4 Implement Primitive 89: Behavior flushCache

**Blue Book Specification**:
> Removes all entries from the method lookup cache. No-op for implementations without a method cache.

**Implementation** (`ControlPrimitives.cs`):
```csharp
// Primitive 89: Behavior flushCache
public static SmalltalkOOP FlushCache(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
{
    if (args.Length != 0)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    // Clear method lookup cache
    ctx.ClearMethodCache();

    // Return receiver
    return receiver;
}
```

**Registration**:
```csharp
_primitives[89] = ControlPrimitives.FlushCache;
```

---

### Phase 4: Investigate I/O Primitive Naming (RESEARCH)

**Goal**: Verify primitives 98-104 have correct functionality despite naming mismatches.

**Tasks**:
1. Read `GraphicsPrimitives.cs` implementations
2. Compare with Blue Book specifications
3. Rename methods if functionality matches
4. Reimplement if functionality is incorrect

---

### Phase 5: Validation and Testing (TESTING)

**Tasks**:
1. **Run full test suite** with updated primitives
2. **Execute init expressions** to verify system bootstrap
3. **Run primitive-specific tests**:
   - Division rounding: `TestDivisionPrimitives`
   - Bit operations: `TestBitOperations`
   - Instance enumeration: `TestInstanceEnumeration`
4. **Validate method cache flush**: `TestMethodCacheFlushing`

**Success Criteria**:
- ✅ All arithmetic tests pass
- ✅ Bit operation tests pass
- ✅ Instance enumeration works
- ✅ Method cache flushing works
- ✅ Init expressions execute without errors
- ✅ No regressions in existing functionality

---

## Implementation Checklist

### Research Phase
- [ ] Analyze `Smalltalk-80.st` for primitive usage (12-17)
- [ ] Examine `coldstart-bundle.json` method headers
- [ ] Run baseline tests to identify current failures
- [ ] Document findings in `Primitive-Mapping-Analysis.md`

### Fix Phase (Option A - Fix Table)
- [ ] Update `PrimitiveTable.cs` lines 59-64 (primitives 12-17)
- [ ] Verify `Div` implementation (prim 12) rounds down
- [ ] Verify `Quo` implementation (prim 13) truncates
- [ ] Run arithmetic tests

### Fix Phase (Option B - Accept Current)
- [ ] Document deviation in `CLAUDE.md`
- [ ] Update this document with "Custom Mapping" section
- [ ] No code changes

### Missing Primitives Implementation
- [ ] Implement primitive 76 (AsObject) in `ArithmeticPrimitives.cs`
- [ ] Implement primitive 77 (SomeInstance) in `ObjectPrimitives.cs`
- [ ] Implement primitive 78 (NextInstance) in `ObjectPrimitives.cs`
- [ ] Implement primitive 89 (FlushCache) in `ControlPrimitives.cs`
- [ ] Add `IsValidOOP()` method to `ISmalltalkHeapManager`
- [ ] Add `FindInstanceOf()` method to `ISmalltalkHeapManager`
- [ ] Add `FindNextInstanceOf()` method to `ISmalltalkHeapManager`
- [ ] Add `ClearMethodCache()` method to `IPrimitiveContext`
- [ ] Update `PrimitiveTable.cs` registration

### I/O Primitives Investigation
- [ ] Read `GraphicsPrimitives.cs` implementations (98-104)
- [ ] Compare with Blue Book specs
- [ ] Rename or reimplement as needed

### Testing Phase
- [ ] Run full test suite
- [ ] Execute init expressions
- [ ] Run primitive-specific tests
- [ ] Validate no regressions
- [ ] Update documentation

---

## Priority Recommendations

### P0 (Critical - Do First)
1. **Research Phase** - Determine if fix is needed (1-2 hours)
2. **Fix primitives 12-17** - If needed based on research (30 minutes)

### P1 (High - Do Soon)
3. **Implement primitive 76 (AsObject)** - Required for debugging (1 hour)
4. **Implement primitives 77-78 (Instance enumeration)** - Required at Blue Book indices (2 hours)

### P2 (Medium - Do Later)
5. **Implement primitive 89 (FlushCache)** - Nice to have (30 minutes)
6. **Investigate I/O primitives 98-104** - Verify correctness (2 hours)

### P3 (Low - Future Enhancement)
7. **Comprehensive primitive test suite** - Full coverage (4-8 hours)

---

## References

- **Blue Book Chapter 29**: Formal Specification of the Primitive Methods
- **Current Implementation**: `/Smalltalk80.Interpreter/Core/VM/Primitives/`
- **Primitive Table**: `/Smalltalk80.Interpreter/Core/VM/Primitives/PrimitiveTable.cs`
- **Source File**: `/Smalltalk-80 Source/Smalltalk-80.st`

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-02 | Claude | Initial analysis document created |

