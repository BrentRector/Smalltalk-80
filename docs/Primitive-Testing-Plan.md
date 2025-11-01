# Primitive Testing Plan

**Created**: 2025-10-31
**Status**: In Progress
**Current Coverage**: 27/120 primitives tested (22.5%)

---

## Executive Summary

This document outlines a phased approach to testing all 120 primitives defined in Smalltalk-80.st. Tests validate that primitives execute correctly, don't fall back to Smalltalk code when they should succeed, and return expected results matching Blue Book semantics.

### Testing Principles

1. **Canonical Source**: Only test primitives that exist in Smalltalk-80.st - never add methods to the source
2. **No Fallback**: Tests verify primitives execute (not fallback to Smalltalk code)
3. **Expected Results**: Tests verify correct Blue Book semantics
4. **Isolated Tests**: Each primitive tested independently
5. **Progressive Coverage**: Start with high-priority core features

---

## Current Coverage (27 primitives tested)

### ✅ SmallInteger Arithmetic & Bitwise (16 primitives)
- **1**: `+` (add)
- **2**: `-` (subtract)
- **3**: `<` (less than)
- **4**: `>` (greater than)
- **5**: `<=` (less or equal)
- **6**: `>=` (greater or equal)
- **7**: `=` (equal)
- **8**: `~=` (not equal)
- **9**: `*` (multiply)
- **10**: `/` (divide)
- **11**: `\\` (mod)
- **12**: `//` (floor division)
- **13**: `bitAnd:`
- **14**: `bitOr:`
- **15**: `bitXor:`
- **16**: `bitShift:`

### ✅ Other Currently Tested (11 primitives)
- **17**: `bitShift:` (SmallInteger)
- **18**: `@` (Point creation)
- **60**: `at:` (array access)
- **61**: `at:put:` (array modification)
- **62**: `size` (array size)
- **70**: `new` (object instantiation)
- **71**: `new:` (sized object instantiation)
- **73**: `instVarAt:` (instance variable access)
- **74**: `instVarAt:put:` (instance variable modification)
- **75**: `hash` (object hash)
- **110**: `==` (identity comparison)
- **111**: `class` (object class)

---

## Phase 1: High-Priority Core Features (10 primitives)

**Goal**: Test essential language features used by most Smalltalk programs
**Duration**: 1-2 days
**Priority**: HIGH

### String/ByteArray Access (2 primitives)
- **63**: `basicAt:` - Character/byte access from String/ByteArray
  - Test: `'hello' basicAt: 1` → `$h`
  - Test: `'hello' basicAt: 5` → `$o`
  - Test boundary conditions

- **64**: `basicAt:put:` - Character/byte modification
  - Test: Create mutable string, modify character
  - Test boundary conditions
  - Test type checking (must be Character)

### Message Sending (2 primitives)
- **83**: `perform:`, `perform:with:`, `perform:with:with:`
  - Test: `42 perform: #negated` → `-42`
  - Test: `3 perform: #+ with: 4` → `7`
  - Test: Dynamic dispatch works correctly

- **84**: `perform:withArguments:`
  - Test: `3 perform: #+ withArguments: #(4)` → `7`
  - Test: Multiple arguments in array
  - Test empty argument array

### Stream Operations (3 primitives)
- **65**: `next` - Read next element
  - Test: ReadStream on array, read elements sequentially
  - Test: atEnd behavior

- **66**: `nextPut:` - Write element
  - Test: WriteStream, append elements
  - Test: Stream position updates

- **67**: `atEnd` - Test exhaustion
  - Test: Returns false when elements remain
  - Test: Returns true when exhausted

### Block Operations (Legacy, 3 primitives)
- **80**: `blockCopy:` - Create block closure
  - Test: Block creation with argument count
  - Test: Lexical scope capture

- **81**: `value`, `value:`, `value:value:`
  - Test: Zero-argument block evaluation
  - Test: One-argument block evaluation
  - Test: Two-argument block evaluation

- **82**: `valueWithArguments:`
  - Test: Block evaluation with argument array
  - Test: Argument count validation

### Bulk Operations (1 primitive)
- **105**: `replaceFrom:to:with:startingAt:`
  - Test: Copy portion of one array to another
  - Test: String copy
  - Test: Overlapping regions
  - Test: Boundary conditions

**Success Criteria**: All 10 primitives execute without fallback and return correct results

---

## Phase 2: Extended Numeric Operations (32 primitives)

**Goal**: Complete numeric type support (LargeInteger, Float)
**Duration**: 2-3 days
**Priority**: MEDIUM-HIGH

### LargeInteger Operations (17 primitives)
- **21-28**: Basic operations (initialize, comparison, arithmetic)
- **29-37**: Advanced operations (quo:, negated, bitwise)

**Test Strategy**: Create test cases that overflow SmallInteger range, verify LargeInteger creation and operations

### Float Operations (15 primitives)
- **40**: `asFloat` - Integer → Float conversion
- **41-47**: Arithmetic (+, -, *, /, sin, cos, etc.)
- **48-54**: Conversion & manipulation (hash, truncated, fractionPart, exponent, timesTwoPower:)

**Test Strategy**: Test floating-point arithmetic, trigonometry, conversion operations

**Success Criteria**: All extended numeric primitives work correctly for large/fractional values

---

## Phase 3: Advanced Object Operations (12 primitives)

**Goal**: Test reflection, object manipulation, method access
**Duration**: 2-3 days
**Priority**: MEDIUM

### Object Creation & Reflection (5 primitives)
- **72**: `become:` - Swap object identities
- **76**: `asObject` - Convert OOP to object
- **77**: `someInstance` - Get first instance of class
- **78**: `nextInstance` - Enumerate instances
- **79**: `newMethod:header:` - Create CompiledMethod

### CompiledMethod Access (2 primitives)
- **68**: `objectAt:` - Access method literal
- **69**: `objectAt:put:` - Modify method literal

### Process/Semaphore (5 primitives)
- **85**: `signal` - Signal semaphore
- **86**: `wait` - Wait on semaphore
- **87**: `resume` - Resume process
- **88**: `suspend` - Suspend process
- **100**: `signal:atMilliseconds:` - Timed semaphore

**Success Criteria**: Reflection and process primitives work correctly

---

## Phase 4: System & I/O Operations (51 primitives)

**Goal**: Test system primitives, I/O, file operations
**Duration**: 3-5 days
**Priority**: LOW (can be stubbed for headless interpreter)

### System Information (4 primitives)
- **89**: `flushCache`
- **112**: `coreLeft`
- **115**: `oopsLeft`
- **116**: `signal:atOopsLeft:wordsLeft:`

### System Control (3 primitives)
- **97**: `snapshotPrimitive`
- **113**: `quitPrimitive`
- **114**: `exitToDebugger`

### Input/Output (9 primitives)
- **90-96**: Mouse, keyboard, cursor, BitBlt
- **98-99**: File open/close
- **101-104**: Display, text rendering, graphics

### Windows File System (32 primitives)
- **120-135, 160**: Modern file operations

**Success Criteria**: System primitives either work or gracefully fail with clear error messages

---

## Test File Organization

### Current Test Files
- `PrimitiveTest.st` - 27 tests (SmallInteger, Point, Array, Object basics)
- `IfTrueTest.st` - 6 tests (control flow validation)
- `BlockContextTest.st` - 104 tests (block closures)

### Planned Test Files

#### Phase 1 Tests
- `StringPrimitiveTest.st` - Primitives 63-64 (String/ByteArray access)
- `MessageSendTest.st` - Primitives 83-84 (perform:)
- `StreamPrimitiveTest.st` - Primitives 65-67 (Stream operations)
- `BlockPrimitiveTest.st` - Primitives 80-82 (legacy blocks)
- `ArrayCopyTest.st` - Primitive 105 (bulk copy)

#### Phase 2 Tests
- `LargeIntegerTest.st` - Primitives 21-37
- `FloatTest.st` - Primitives 40-54

#### Phase 3 Tests
- `ReflectionTest.st` - Primitives 72, 76-79
- `ProcessTest.st` - Primitives 85-88, 100
- `MethodAccessTest.st` - Primitives 68-69

#### Phase 4 Tests
- `SystemTest.st` - Primitives 89, 97, 112-116
- `FileIOTest.st` - Primitives 98-99, 120-135, 160
- `GraphicsTest.st` - Primitives 90-96, 101-104

---

## Testing Infrastructure

### Test Execution
1. Compile test file with main Smalltalk-80.st source
2. Run interpreter with `--test-mode` flag
3. Tests execute as init expressions
4. Report pass/fail for each test

### Test Pattern
```smalltalk
testPrimitiveName
    "Test description"
    | result |
    result := ... "invoke primitive" ...
    ^result = expectedValue  "returns true/false"
```

### Success Criteria
- Test returns `true` → PASS
- Test returns `false` → FAIL
- Test throws exception → FAIL (with diagnostic)

---

## Progress Tracking

### Phase 1: High-Priority (10 primitives) - 🔄 IN PROGRESS
- [ ] StringPrimitiveTest.st (primitives 63-64)
- [ ] MessageSendTest.st (primitives 83-84)
- [ ] StreamPrimitiveTest.st (primitives 65-67)
- [ ] BlockPrimitiveTest.st (primitives 80-82)
- [ ] ArrayCopyTest.st (primitive 105)

### Phase 2: Extended Numeric (32 primitives) - 🔴 NOT STARTED
- [ ] LargeIntegerTest.st (primitives 21-37)
- [ ] FloatTest.st (primitives 40-54)

### Phase 3: Advanced Objects (12 primitives) - 🔴 NOT STARTED
- [ ] ReflectionTest.st (primitives 72, 76-79)
- [ ] ProcessTest.st (primitives 85-88, 100)
- [ ] MethodAccessTest.st (primitives 68-69)

### Phase 4: System & I/O (51 primitives) - 🔴 NOT STARTED
- [ ] SystemTest.st (primitives 89, 97, 112-116)
- [ ] FileIOTest.st (primitives 98-99, 120-135, 160)
- [ ] GraphicsTest.st (primitives 90-96, 101-104)

### Overall Progress
- **Tested**: 27/120 primitives (22.5%)
- **Remaining**: 93/120 primitives (77.5%)
- **Target**: 100% coverage for core primitives (phases 1-3)

---

## Next Steps

1. **Immediate**: Implement Phase 1 tests (10 high-priority primitives)
2. **Week 1**: Complete Phase 1, start Phase 2
3. **Week 2**: Complete Phase 2 (numeric operations)
4. **Week 3**: Complete Phase 3 (advanced objects)
5. **Future**: Phase 4 (system/I/O) as needed

**Estimated Total Duration**: 3-4 weeks for complete coverage
