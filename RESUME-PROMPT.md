# Resume Prompt: Smalltalk-80 Interpreter

**Session Date**: 2025-12-10
**Status**: BLOCKED - Crash on mouse/keyboard input

## Current State

### What's Working
- Build: 0 errors, 0 warnings
- Cold Start: Phases 1-6 all succeed
- Init Expressions: 286/286 (100%)
- BlockContext Tests: 120/120 passing (returns 1191)
- Primitive 202 (bitState): Atomic button state polling
- Interpreter runs stably when IDLE (no mouse/keyboard input)

### What's Broken
- Crashes when mouse moves or keyboard is used
- Error: `Instance variable index 15 out of range [0, -1] for object oop=2 (UndefinedObject)`
- Method `Process>>terminate` executing with nil receiver

---

## FALSE PATHS - DO NOT REPEAT THESE INVESTIGATIONS

### 1. Method Lookup Bug Theory - FALSE
**Wrong hypothesis**: Method lookup returning wrong method for receiver class.
**Why it's wrong**: The crash diagnostic shows the CORRECT method (`Process>>terminate`) is found. The problem is the receiver is nil, not that lookup failed.

### 2. ProcessorScheduler Receiving terminate - FALSE
**Wrong hypothesis**: `terminate` message being sent to ProcessorScheduler instead of Process.
**Why it's wrong**: The diagnostic "Sender chain: [0] Method terminate (recv: ProcessorScheduler)" means ProcessorScheduler is the SENDER (the object whose method contains `activeProcess terminate`), not the receiver. The receiver is nil.

### 3. Bytecode 0x0F Analysis - FALSE
**Wrong hypothesis**: Analyzing what instance variable 15 means and which class has 15+ ivars.
**Why it's wrong**: The crash happens because receiver is nil (oop=2), so ANY ivar access crashes. The specific index 15 is irrelevant - it's just wherever the method happens to access `self`.

### 4. Context Receiver Corruption Theory - UNPROVEN
**Wrong approach**: Searching for `SetActiveContext` and `CONTEXT_RECEIVER` to find corruption.
**Why it's unproductive**: Too many call sites, no clear path to the bug. Need a different approach.

### 5. Nil activeProcess in ProcessorScheduler - LIKELY ROOT CAUSE BUT UNPROVEN
**Hypothesis**: `ProcessorScheduler>>terminateActive` does `activeProcess terminate`, but `activeProcess` ivar is nil.
**Status**: This is probably correct but we haven't proven WHY activeProcess becomes nil.

---

## What We Actually Know

1. **Crash only happens with input** - Idle screen is stable
2. **InputProcess IS running** - Logs show `[FindHighestPriority] Selected InputProcess=oop=68942 from P6 queue`
3. **Signals ARE being transferred** - Logs show `[CheckAndSignal] Transferred N signals`
4. **The crash method is Process>>terminate** with nil receiver
5. **The sender is ProcessorScheduler>>terminateActive** which does `activeProcess terminate`

---

## Productive Investigation Directions

1. **Why does activeProcess become nil?**
   - When is activeProcess set?
   - What clears it?
   - Is there a race between process termination and new process activation?

2. **Add defensive check** in `ProcessorScheduler>>terminateActive`:
   ```smalltalk
   terminateActive
       activeProcess == nil ifTrue: [^self].  "Guard against nil"
       activeProcess terminate
   ```

3. **Trace activeProcess changes** - Add logging when activeProcess ivar is written

4. **Check process termination flow** - When a process terminates, what updates activeProcess?

---

## Key Files

### Input System
- `WinFormsDisplayProvider.cs` - Atomic mouse state, ring buffer, keyboard handling
- `InputWordRingBuffer.cs` - Thread-safe ring buffer
- `InputPrimitives.cs` - Primitives 90-95, 202

### Process Scheduling
- `VirtualMachine.ProcessScheduling.cs` - Process scheduling (~330 lines)
- `ControlPrimitives.cs` - Primitives 85-88 (suspend, resume, terminate, yield)

### Smalltalk Source
- `Smalltalk-80.st` lines 32752-32773: `Process>>terminate`
- `Smalltalk-80.st` lines 32976-32979: `ProcessorScheduler>>terminateActive`

---

## Test Commands

```bash
# Standard run (crashes on mouse/keyboard input)
cd Smalltalk80.Interpreter
dotnet run

# BlockContext regression test (MUST PASS)
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

---

## Model Selection

Always use model: **claude-sonnet-4-5**
