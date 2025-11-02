# Primitive Mapping Research Findings

**Date**: 2025-11-02
**Phase**: Phase 1 Research (Complete)
**Status**: CRITICAL BUG CONFIRMED

---

## Executive Summary

✅ **Research Complete**: Definitive evidence shows primitives 12-17 are **INCORRECTLY MAPPED** in `PrimitiveTable.cs`.

❌ **Critical Bug Confirmed**: Our primitive table does NOT match the Smalltalk-80.st source file.

🔧 **Required Action**: Fix `PrimitiveTable.cs` lines 59-64 to match Blue Book specification.

---

## Evidence from Source Code

### Smalltalk-80.st Source Analysis

Searched `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st` and found definitive primitive declarations:

#### Primitive 12: Should be `//` (Integer Division - Round Down)
**Location**: Line 34796
```smalltalk
// aNumber
	"Divide the receiver by the argument and answer with the result.  Round the result
	down towards negative infinity to make it a whole integer.  Fail if the
	argument is 0 or is not a SmallInteger.  Essential.  No Lookup.  See Object
	documentation whatIsAPrimitive. "

	<primitive: 12>
	^super // aNumber"Do with quo: if primitive fails"!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.Quo` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.Div`

---

#### Primitive 13: Should be `quo:` (Integer Division - Truncate)
**Location**: Line 34803
```smalltalk
quo: aNumber
	"Divide the receiver by the argument and answer with the result.  Round the
	result down towards zero to make it a whole integer.  Fail if the argument is 0 or is
	not a SmallInteger.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 13>
	aNumber = 0 ifTrue: [^self error: 'Attempt to divide by zero'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [self primitiveFailed]
		ifFalse: [^super quo: aNumber]!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.BitAnd` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.Quo`

---

#### Primitive 14: Should be `bitAnd:`
**Location**: Line 34823
```smalltalk
bitAnd: arg
	"Logical AND the twos-complement representation of the receiver with the
	argument and answer with the result.  Fail if the argument is not a SmallInteger.
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 14>
	^arg bitAnd: self!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.BitOr` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.BitAnd`

---

#### Primitive 15: Should be `bitOr:`
**Location**: Line 34830
```smalltalk
bitOr: arg
	"Logical OR the twos-complement representation of the receiver with the
	argument and answer with the result.  Fail if the argument is not a SmallInteger.
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 15>
	^arg bitOr: self!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.BitXor` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.BitOr`

---

#### Primitive 16: Should be `bitXor:`
**Location**: Line 34848
```smalltalk
bitXor: arg
	"Exclusive OR the twos-complement representation of the receiver with the
	argument and answer with the result.  Fail if the argument is not a
	SmallInteger.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 16>
	^arg bitXor: self!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.BitShift` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.BitXor`

---

#### Primitive 17: Should be `bitShift:`
**Location**: Line 34841
```smalltalk
bitShift: arg
	"Answers with a SmallInteger whose value (in twos-complement
	representation) is the receiver's value (in twos-complement representation)
	shifted left by the number of bits indicated by the argument.  Negative
	arguments shift right.  Zeros are shifted in from the right in left shifts.  The sign
	bit is extended in right shifts.  Fail if the result cannot be represented as a
	SmallInteger.  Essential.  No Lookup.  See Object documentation
	whatIsAPrimitive. "

	<primitive: 17>
	^super bitShift: arg!
```

**Current Mapping**: ❌ `ArithmeticPrimitives.Div` (WRONG)
**Correct Mapping**: ✅ `ArithmeticPrimitives.BitShift`

---

## Current vs. Correct Mapping

### PrimitiveTable.cs (Current - WRONG)

```csharp
// Lines 59-64 in PrimitiveTable.cs
_primitives[12] = ArithmeticPrimitives.Quo;        // ❌ WRONG - Should be Div
_primitives[13] = ArithmeticPrimitives.BitAnd;     // ❌ WRONG - Should be Quo
_primitives[14] = ArithmeticPrimitives.BitOr;      // ❌ WRONG - Should be BitAnd
_primitives[15] = ArithmeticPrimitives.BitXor;     // ❌ WRONG - Should be BitOr
_primitives[16] = ArithmeticPrimitives.BitShift;   // ❌ WRONG - Should be BitXor
_primitives[17] = ArithmeticPrimitives.Div;        // ❌ WRONG - Should be BitShift
```

### Required Fix (CORRECT)

```csharp
// Lines 59-64 in PrimitiveTable.cs (CORRECTED)
_primitives[12] = ArithmeticPrimitives.Div;        // ✅ CORRECT - // operator
_primitives[13] = ArithmeticPrimitives.Quo;        // ✅ CORRECT - quo: message
_primitives[14] = ArithmeticPrimitives.BitAnd;     // ✅ CORRECT - bitAnd: message
_primitives[15] = ArithmeticPrimitives.BitOr;      // ✅ CORRECT - bitOr: message
_primitives[16] = ArithmeticPrimitives.BitXor;     // ✅ CORRECT - bitXor: message
_primitives[17] = ArithmeticPrimitives.BitShift;   // ✅ CORRECT - bitShift: message
```

---

## Impact Analysis

### Critical Bugs Created by Misalignment

#### Bug 1: Division Operations Execute Wrong Algorithm
```smalltalk
"Expected Blue Book behavior:"
-7 // 2     "Should return -4 (rounds DOWN toward negative infinity)"
-7 quo: 2   "Should return -3 (truncates toward zero)"

"Current BROKEN behavior:"
-7 // 2     "Returns -3 (executes quo: instead of //)"
-7 quo: 2   "Returns bitAnd: result (completely wrong!)"
```

#### Bug 2: Bit Operations Execute Wrong Operations
```smalltalk
"Expected Blue Book behavior:"
5 bitAnd: 3    "Should return 1 (101 AND 011 = 001)"
5 bitOr: 3     "Should return 7 (101 OR 011 = 111)"
5 bitXor: 3    "Should return 6 (101 XOR 011 = 110)"
5 bitShift: 2  "Should return 20 (101 << 2 = 10100)"

"Current BROKEN behavior:"
5 bitAnd: 3    "Executes bitOr: - returns 7 (WRONG)"
5 bitOr: 3     "Executes bitXor: - returns 6 (WRONG)"
5 bitXor: 3    "Executes bitShift: - returns 20 (WRONG)"
5 bitShift: 2  "Executes //, which fails or returns wrong result"
```

#### Bug 3: Cascade Effect
Because primitive 13 executes `BitAnd` instead of `Quo`, and the Smalltalk fallback method for `quo:` likely tries primitive 13 again, this creates potential infinite recursion or falls back to slow arithmetic implementations.

---

## Why This Happened

### Root Cause Analysis

Looking at the misalignment pattern:
- Primitive 12 and 13 are **swapped** (Quo ↔ Div)
- This swap caused all subsequent primitives (14-17) to shift by one index
- The final primitive 17 wraps around to use `Div` (which should be at 12)

**Hypothesis**: Someone manually edited `PrimitiveTable.cs` and accidentally swapped lines 59-60, not realizing it would cascade through lines 61-64.

**Alternative Hypothesis**: The primitive table was initially built from a different source (possibly another Smalltalk implementation) that uses a different numbering scheme.

---

## Verification

### Source File Authority

The **Smalltalk-80.st source file is the authoritative specification** for our interpreter because:

1. ✅ It explicitly declares `<primitive: N>` in each method
2. ✅ The compiler reads this file to generate the bundle
3. ✅ The interpreter must execute primitives matching the compiled bundle
4. ✅ Changing the source file would require recompiling (not done)

### Blue Book Confirmation

The Smalltalk-80.st source **exactly matches the Blue Book specification**:
- ✅ Primitive 12 = `//` (Chapter 29, page 633)
- ✅ Primitive 13 = `quo:` (Chapter 29, page 633)
- ✅ Primitives 14-17 = bit operations (Chapter 29, page 633)

---

## Recommendation

### IMMEDIATE ACTION REQUIRED ✅

**Severity**: 🔴 **CRITICAL P0**

**Fix Required**: Update `PrimitiveTable.cs` lines 59-64 to match source specification.

**Estimated Effort**: 5 minutes (6 line changes)

**Risk**: **LOW** - This is a bug fix, not a feature change. The fix restores correct behavior.

**Testing Required**:
- ✅ Run arithmetic test suite
- ✅ Run bit operation tests
- ✅ Execute init expressions to verify system bootstrap
- ✅ Validate division edge cases (-7 // 2, -7 quo: 2)

---

## Decision

### ✅ DECISION: FIX PRIMITIVE TABLE

**Rationale**:
1. Source file (`Smalltalk-80.st`) is the authoritative specification
2. Source matches Blue Book exactly
3. Current mapping is demonstrably wrong
4. Fix is simple and low-risk
5. No backwards compatibility concerns (this is a bug fix)

**Rejected Alternative**: Accept current mapping
- ❌ Would require changing source file (breaks Blue Book compatibility)
- ❌ Would require recompiling entire system
- ❌ Would diverge from standard Smalltalk-80 semantics
- ❌ Would break any external tools that expect Blue Book primitives

---

## Next Steps

### Phase 2: Implement Fix

1. **Update PrimitiveTable.cs** (lines 59-64)
   - Change primitive 12 from `Quo` to `Div`
   - Change primitive 13 from `BitAnd` to `Quo`
   - Change primitive 14 from `BitOr` to `BitAnd`
   - Change primitive 15 from `BitXor` to `BitOr`
   - Change primitive 16 from `BitShift` to `BitXor`
   - Change primitive 17 from `Div` to `BitShift`

2. **Rebuild interpreter**
   ```bash
   dotnet build -c Debug
   ```

3. **Run validation tests**
   ```bash
   # Test division
   # Test: -7 // 2 should return -4
   # Test: -7 quo: 2 should return -3

   # Test bit operations
   # Test: 5 bitAnd: 3 should return 1
   # Test: 5 bitOr: 3 should return 7
   # Test: 5 bitXor: 3 should return 6
   # Test: 5 bitShift: 2 should return 20
   ```

4. **Execute init expressions**
   ```bash
   ./bin/Debug/net9.0/Smalltalk80.Interpreter.exe --new-loader
   ```

5. **Commit fix with detailed message**

---

## Appendices

### Appendix A: Semantic Difference Between `//` and `quo:`

| Operation | Description | Example | Result |
|-----------|-------------|---------|--------|
| `//` | Rounds DOWN toward negative infinity | `-7 // 2` | `-4` |
| `quo:` | Truncates toward zero | `-7 quo: 2` | `-3` |

**Why it matters**: Division rounding behavior affects:
- Array indexing calculations
- Modulo operations
- Range calculations
- Financial calculations
- Any algorithm relying on division semantics

### Appendix B: Test Cases

```smalltalk
"Division tests"
7 // 2       "=> 3"
-7 // 2      "=> -4 (rounds DOWN, not up)"
7 quo: 2     "=> 3"
-7 quo: 2    "=> -3 (truncates toward zero)"

"Bit operation tests"
5 bitAnd: 3     "=> 1  (binary: 101 AND 011 = 001)"
5 bitOr: 3      "=> 7  (binary: 101 OR 011 = 111)"
5 bitXor: 3     "=> 6  (binary: 101 XOR 011 = 110)"
5 bitShift: 2   "=> 20 (binary: 101 << 2 = 10100)"
20 bitShift: -2 "=> 5  (binary: 10100 >> 2 = 101)"
```

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-02 | Claude | Phase 1 research complete - definitive findings |

---

## References

- **Source File**: `/home/user/Smalltalk-80/Smalltalk-80 Source/Smalltalk-80.st`
  - Line 34796: Primitive 12 = `//`
  - Line 34803: Primitive 13 = `quo:`
  - Line 34823: Primitive 14 = `bitAnd:`
  - Line 34830: Primitive 15 = `bitOr:`
  - Line 34841: Primitive 17 = `bitShift:`
  - Line 34848: Primitive 16 = `bitXor:`

- **Current Implementation**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/PrimitiveTable.cs`
  - Lines 59-64: INCORRECT mappings

- **Blue Book**: Chapter 29 - Formal Specification of the Primitive Methods
  - Pages 633-635: SmallInteger arithmetic primitives

- **Analysis Document**: `/home/user/Smalltalk-80/docs/Primitive-Specification-Analysis.md`

