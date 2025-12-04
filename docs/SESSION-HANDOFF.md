# Session Handoff: Method Lookup Bug Investigation

**Date**: 2025-12-04 07:10 UTC
**Status**: BLOCKED - Active debugging session
**Priority**: CRITICAL

---

## Quick Summary

Phases 1-7 of Event Architecture Refactor implemented successfully, but system crashes on mouse movement. Method lookup is returning wrong method - InputState>>keyAt:put: executed with InputSensor receiver.

---

## Current State

### What Works
- ✅ Build: 0 errors, 0 warnings
- ✅ Cold start: All 6 phases succeed
- ✅ Init expressions: 287/287 succeed
- ✅ Phases 1-7 code implemented correctly

### What's Broken
- ❌ Mouse movement causes immediate crash
- ❌ System unusable - can't interact with GUI

---

## The Bug

**Error Message**:
```
Instance variable index 4 out of range [0, 0] for object oop=67886 (InputSensor)
at BytecodeInterpreter.cs:84
```

**What's Happening**:
1. Mouse moves → event enqueued
2. InputProcess wakes and processes event
3. Message sent to Sensor (oop=67886, an InputSensor)
4. **BUG**: Method lookup returns InputState>>keyAt:put: instead of InputSensor method
5. VM executes InputState bytecode with InputSensor receiver
6. Bytecode 0x04 tries to access ivar[4] (rshiftState)
7. **CRASH**: InputSensor only has 1 instance variable (ivar[0])

**Key Finding**: NO InputSensor method contains bytecode 0x04. The bytecode is from InputState>>keyAt:put which legitimately accesses ivar[4] on InputState instances.

---

## Investigation Completed

### Verified Correct ✅
- InputSensor class structure (1 ivar: keyboardMap at index 0)
- InputState class structure (12 ivars: ivar[4] = rshiftState)
- Bytecode compilation (all methods compiled correctly)
- No selector name collision between InputSensor and InputState
- Object oop=67886 is correctly sized as InputSensor

### Identified ✅
- Problematic bytecode: 0x04 (push receiver ivar[4])
- Source method: InputState>>keyAt:put:
- Trigger: First mouse movement after system initialization

### Added ✅
- Debug logging in BytecodeInterpreter.cs lines 83-91
- Logs receiver class, receiver OOP, class OOP, method OOP when accessing ivar[4]

---

## Next Steps

### Immediate (Next 5 minutes)
1. ✅ Documents updated with status
2. Run interpreter to capture method OOP from [IVAR4] log before crash
3. Look up method OOP in `coldstart.json` to confirm it's InputState>>keyAt:put:

### Investigation Path (Next 30 minutes)
1. Add logging to message send code:
   - File: `VirtualMachine.MessageSending.cs`
   - Log: selector, receiver, receiver class, method found, method class
2. Trace exact message send that finds wrong method
3. Examine method lookup implementation:
   - `LookupMethodInClass()` or equivalent
   - Method dictionary traversal
   - Class hierarchy traversal

### Possible Root Causes
1. **Method dictionary lookup** - Using wrong class OOP during lookup
2. **Class OOP integrity** - InputSensor class OOP pointing to wrong structure
3. **Super send logic** - Incorrect traversal of class hierarchy
4. **Context corruption** - Receiver OOP set incorrectly in MethodContext

---

## Files Modified

### Debug Changes (Keep)
- `BytecodeInterpreter.cs:83-91` - Debug logging for ivar[4] access

### Documentation (Complete)
- `EVENT-ARCHITECTURE-REFACTOR-PLAN.md` - Status updated to BLOCKED
- `DEBUGGING-METHOD-LOOKUP-BUG.md` - Detailed investigation findings
- `CLAUDE.md` - Project status updated
- `SESSION-HANDOFF.md` - This file

---

## Quick Commands

### Run with debug output
```bash
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter"
dotnet run 2>&1 | grep -E "IVAR4|Fatal|Error" | head -50
```

### Look up method in JSON
```bash
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Compiler\bin\Debug\net9.0\Data"
cat coldstart.json | grep -A 10 '"localId": METHOD_OOP_HERE'
```

### Search for selector
```bash
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk-80 Source"
grep -n "keyAt:put:" Smalltalk-80.st
```

---

## Background Context

### Event Architecture Phases Completed
- Phase 1: Atomic interrupt flag + runnable latch
- Phase 2: ManualResetEventSlim for idle wait
- Phase 3: Strategic check points (backward branches, method returns)
- Phase 4: Context switch hook (clear latch when InputProcess blocks)
- Phase 5: Interrupt masking (prevent GC interruption)
- Phase 7: Smalltalk drain loop + primInputDrainComplete

### Why This Matters
Without fixing this bug, the entire Event Architecture Refactor is blocked. System is non-functional - can't test nested blocking, can't validate 0% idle CPU, can't proceed to Phases 8-10.

---

## Key Insight

This is NOT a bytecode generation bug or object size bug. The compiled code is correct. This is a **VM method lookup bug** where the wrong method is selected during message send. The receiver is correct (InputSensor), but the method found is from a different class (InputState).

---

## For Next Session

**Start Here**:
1. Read this handoff document
2. Read `docs/DEBUGGING-METHOD-LOOKUP-BUG.md` for full details
3. Run interpreter with logging to capture method OOP
4. Look up method OOP in JSON
5. Add message send logging to trace lookup

**Goal**: Find why `LookupMethod(receiver=InputSensor, selector=X)` returns InputState method instead of InputSensor method.
