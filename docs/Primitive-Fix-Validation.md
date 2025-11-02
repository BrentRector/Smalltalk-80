# Primitive 12-17 Fix Validation Guide

**Date**: 2025-11-02
**Status**: Fix Applied - Testing Required
**Commit**: b3f6240

---

## Summary

Primitives 12-17 have been **corrected** in PrimitiveTable.cs to match the Blue Book specification and Smalltalk-80.st source file.

**Fix Applied**: ✅ COMPLETE
**Rebuild Required**: ⚠️ YES - Run `dotnet build -c Debug`
**Testing Required**: ⚠️ YES - Run validation tests below

---

## What Was Fixed

### PrimitiveTable.cs Changes (lines 59-64)

```csharp
// BEFORE (WRONG):
_primitives[12] = ArithmeticPrimitives.Quo;        // Executed quo: for //
_primitives[13] = ArithmeticPrimitives.BitAnd;     // Executed bitAnd: for quo:
_primitives[14] = ArithmeticPrimitives.BitOr;      // Executed bitOr: for bitAnd:
_primitives[15] = ArithmeticPrimitives.BitXor;     // Executed bitXor: for bitOr:
_primitives[16] = ArithmeticPrimitives.BitShift;   // Executed bitShift: for bitXor:
_primitives[17] = ArithmeticPrimitives.Div;        // Executed // for bitShift:

// AFTER (CORRECT):
_primitives[12] = ArithmeticPrimitives.Div;        // SmallInteger // (rounds down)
_primitives[13] = ArithmeticPrimitives.Quo;        // SmallInteger quo: (truncates)
_primitives[14] = ArithmeticPrimitives.BitAnd;     // SmallInteger bitAnd:
_primitives[15] = ArithmeticPrimitives.BitOr;      // SmallInteger bitOr:
_primitives[16] = ArithmeticPrimitives.BitXor;     // SmallInteger bitXor:
_primitives[17] = ArithmeticPrimitives.BitShift;   // SmallInteger bitShift:
```

---

## Validation Test Plan

### Step 1: Rebuild Interpreter

```bash
cd /home/user/Smalltalk-80
dotnet build -c Debug
```

**Expected Output**: Build succeeds with no errors

---

### Step 2: Division Operation Tests

These tests verify that `//` and `quo:` now execute correct algorithms.

#### Test 2.1: Positive Division (No Rounding)
```smalltalk
7 // 2       "Expected: 3"
7 quo: 2     "Expected: 3"
```
✅ **Expected**: Both return 3 (no difference for positive results)

#### Test 2.2: Negative Division (Rounding Difference)
```smalltalk
-7 // 2      "Expected: -4 (rounds DOWN toward negative infinity)"
-7 quo: 2    "Expected: -3 (truncates toward zero)"
```
✅ **Expected**:
- `//` returns **-4** (rounds down: -3.5 → -4)
- `quo:` returns **-3** (truncates: -3.5 → -3)

❌ **Before Fix**:
- `//` returned **-3** (executed quo: instead)
- `quo:` executed **bitAnd:** (completely wrong)

#### Test 2.3: Division Edge Cases
```smalltalk
0 // 5       "Expected: 0"
0 quo: 5     "Expected: 0"
-1 // 2      "Expected: -1 (rounds down)"
-1 quo: 2    "Expected: 0 (truncates)"
```

#### Test 2.4: Division by Zero (Should Fail Primitive)
```smalltalk
7 // 0       "Expected: Primitive fails, Smalltalk error handling takes over"
7 quo: 0     "Expected: Primitive fails, Smalltalk error handling takes over"
```
✅ **Expected**: Both should trigger "Attempt to divide by zero" error

---

### Step 3: Bit Operation Tests

These tests verify that bit operations execute correct implementations.

#### Test 3.1: bitAnd: (Logical AND)
```smalltalk
5 bitAnd: 3     "Expected: 1"
"Binary: 101 AND 011 = 001"

15 bitAnd: 7    "Expected: 7"
"Binary: 1111 AND 0111 = 0111"

-1 bitAnd: 255  "Expected: 255"
"Two's complement AND"
```
✅ **Expected**: Correct AND operation
❌ **Before Fix**: Executed **bitOr:** instead (returned 7 for first test)

#### Test 3.2: bitOr: (Logical OR)
```smalltalk
5 bitOr: 3      "Expected: 7"
"Binary: 101 OR 011 = 111"

8 bitOr: 4      "Expected: 12"
"Binary: 1000 OR 0100 = 1100"
```
✅ **Expected**: Correct OR operation
❌ **Before Fix**: Executed **bitXor:** instead (returned 6 for first test)

#### Test 3.3: bitXor: (Logical XOR)
```smalltalk
5 bitXor: 3     "Expected: 6"
"Binary: 101 XOR 011 = 110"

15 bitXor: 15   "Expected: 0"
"Binary: 1111 XOR 1111 = 0000"
```
✅ **Expected**: Correct XOR operation
❌ **Before Fix**: Executed **bitShift:** instead (returned 20 for first test)

#### Test 3.4: bitShift: (Bit Shift)
```smalltalk
5 bitShift: 2   "Expected: 20"
"Binary: 101 << 2 = 10100"

20 bitShift: -2 "Expected: 5"
"Binary: 10100 >> 2 = 101"

1 bitShift: 10  "Expected: 1024"
"Binary: 1 << 10 = 10000000000"
```
✅ **Expected**: Correct shift operation (left for positive, right for negative)
❌ **Before Fix**: Executed **//** (division) instead

---

### Step 4: Run Init Expressions

Execute system bootstrap to verify no regressions:

```bash
./bin/Debug/net9.0/Smalltalk80.Interpreter.exe --new-loader
```

**Expected Output**: All init expressions execute successfully without errors

**Critical Check**: Look for any division or bit operation errors during:
- Character table initialization
- Symbol table initialization
- Method dictionary population

---

### Step 5: Comprehensive Test Suite

If available, run the full interpreter test suite:

```bash
dotnet test
```

**Expected**: All tests pass (or same pass/fail rate as before fix)

---

## Test Script (Smalltalk)

Create a test class to automate validation:

```smalltalk
Object subclass: #PrimitiveFix1217Test
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Primitives'!

!PrimitiveFix1217Test methodsFor: 'tests'!

testDivisionPositive
	"Test positive division"
	self assert: (7 // 2) = 3.
	self assert: (7 quo: 2) = 3.
!

testDivisionNegative
	"Test negative division - KEY TEST for fix"
	self assert: (-7 // 2) = -4.       "Rounds down"
	self assert: (-7 quo: 2) = -3.     "Truncates"
	self assert: (-1 // 2) = -1.       "Rounds down"
	self assert: (-1 quo: 2) = 0.      "Truncates"
!

testBitAnd
	"Test bitAnd: operation"
	self assert: (5 bitAnd: 3) = 1.    "101 AND 011 = 001"
	self assert: (15 bitAnd: 7) = 7.   "1111 AND 0111 = 0111"
!

testBitOr
	"Test bitOr: operation"
	self assert: (5 bitOr: 3) = 7.     "101 OR 011 = 111"
	self assert: (8 bitOr: 4) = 12.    "1000 OR 0100 = 1100"
!

testBitXor
	"Test bitXor: operation"
	self assert: (5 bitXor: 3) = 6.    "101 XOR 011 = 110"
	self assert: (15 bitXor: 15) = 0.  "1111 XOR 1111 = 0000"
!

testBitShift
	"Test bitShift: operation"
	self assert: (5 bitShift: 2) = 20.    "101 << 2 = 10100"
	self assert: (20 bitShift: -2) = 5.   "10100 >> 2 = 101"
	self assert: (1 bitShift: 10) = 1024. "1 << 10 = 10000000000"
!

! !

!PrimitiveFix1217Test class methodsFor: 'running'!

run
	"Run all tests"
	| test |
	test := self new.

	Transcript show: 'Running Primitive 12-17 validation tests...'; cr.

	test testDivisionPositive.
	Transcript show: '✓ Positive division tests passed'; cr.

	test testDivisionNegative.
	Transcript show: '✓ Negative division tests passed (KEY FIX VERIFIED)'; cr.

	test testBitAnd.
	Transcript show: '✓ bitAnd: tests passed'; cr.

	test testBitOr.
	Transcript show: '✓ bitOr: tests passed'; cr.

	test testBitXor.
	Transcript show: '✓ bitXor: tests passed'; cr.

	test testBitShift.
	Transcript show: '✓ bitShift: tests passed'; cr.

	Transcript show: 'All tests PASSED! Primitive fix validated.'; cr.
!

! !

"Execute tests:"
PrimitiveFix1217Test run!
```

---

## Success Criteria

### ✅ Tests Pass If:

1. **Division Tests**:
   - `-7 // 2` returns **-4** (not -3)
   - `-7 quo: 2` returns **-3** (not bitAnd result)

2. **Bit Operation Tests**:
   - `5 bitAnd: 3` returns **1** (not 7)
   - `5 bitOr: 3` returns **7** (not 6)
   - `5 bitXor: 3` returns **6** (not 20)
   - `5 bitShift: 2` returns **20** (not division result)

3. **System Bootstrap**:
   - Init expressions execute without primitive errors
   - No stack overflows or infinite recursion
   - All 280 init expressions complete

4. **Regression Tests**:
   - Existing functionality unchanged
   - No new test failures

---

## Failure Scenarios

### ❌ If Tests Fail:

#### Scenario 1: Build Failed
**Symptom**: `dotnet build` errors
**Cause**: Syntax error in PrimitiveTable.cs
**Fix**: Review lines 59-64, ensure correct syntax

#### Scenario 2: Division Still Wrong
**Symptom**: `-7 // 2` still returns -3
**Cause**: Old binary still running (rebuild didn't work)
**Fix**:
```bash
dotnet clean
dotnet build -c Debug
```

#### Scenario 3: Bit Operations Wrong
**Symptom**: `5 bitAnd: 3` returns 7
**Cause**: Primitives still misaligned
**Fix**: Verify PrimitiveTable.cs lines 59-64 match correct mapping

#### Scenario 4: Init Expressions Fail
**Symptom**: Bootstrap errors during init
**Cause**: Code depends on old (wrong) primitive behavior
**Fix**: Review error trace, may need Smalltalk code updates

---

## Rollback Plan

If the fix causes unexpected issues:

```bash
git revert b3f6240
dotnet build -c Debug
```

This will restore the old (incorrect but possibly working) primitive mapping.

**Note**: Rollback should be temporary. The fix is correct per Blue Book specification.

---

## Expected Timeline

| Phase | Duration | Status |
|-------|----------|--------|
| Apply Fix | 5 min | ✅ DONE |
| Rebuild | 2 min | ⚠️ PENDING |
| Division Tests | 5 min | ⚠️ PENDING |
| Bit Op Tests | 5 min | ⚠️ PENDING |
| Init Expressions | 10 min | ⚠️ PENDING |
| Full Test Suite | 30 min | ⚠️ PENDING |
| **Total** | **~1 hour** | **IN PROGRESS** |

---

## References

- **Fix Commit**: b3f6240 - CRITICAL FIX: Correct primitives 12-17
- **Research**: docs/Primitive-Mapping-Research-Findings.md
- **Analysis**: docs/Primitive-Specification-Analysis.md
- **Source Evidence**: Smalltalk-80.st lines 34796-34848
- **Blue Book**: Chapter 29, pages 633-635

---

## Document History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-11-02 | Claude | Initial validation guide |

