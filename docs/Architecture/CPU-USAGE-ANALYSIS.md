# CPU Usage Analysis: Fixing 3% Idle CPU Consumption

**Date**: 2025-12-03
**Status**: Analysis Complete - Fixes Proposed
**Updated**: 2025-12-03 - Corrected analysis per architectural review

## Executive Summary

The Smalltalk-80 interpreter currently consumes **~3% CPU when completely idle** despite implementing `WaitHandle.WaitAny` for event/timer waiting.

**Key Insight**: The system should be **100% idle** when the background process is active. We should **never execute the background process's bytecodes**. The only wake sources are:
1. **UI event** → WaitAny returns → signal input semaphore → process switch
2. **Timer expires** → WaitAny returns → signal timer semaphore → process switch

**Root Causes**:
1. **Breaking on `HasPendingEvents()`** - WRONG! Events don't wake processes; they sit in queue until input semaphore is signaled
2. **Executing bytecodes before/after idle check** - We should never execute background process bytecodes

**Expected Result**: 0% CPU when idle (true kernel-level blocking in WaitAny)

---

## Root Cause Analysis

### The Correct Architecture

When the background process is active, the system is **idle**. The background process exists solely as a sentinel - its bytecodes (`[true] whileTrue: []`) should **never execute**. Instead:

1. **Detect idle immediately** when background process becomes active
2. **Block in WaitAny** indefinitely (0% CPU)
3. **On wake**: Handle the event/timer, check if process switch occurred
4. **If no process switch**: Return to step 2 (stay idle)
5. **If process switch**: Exit idle, execute the new process

The only legitimate wake sources are:
- **UI event** → signal input semaphore → wake Controller → process switch
- **Timer expires** → signal timer semaphore → wake delayed process → process switch

### Issue #1: Breaking on `HasPendingEvents()` (PRIMARY BUG)

**Location**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` lines 335-339

```csharp
while (true)  // Idle wait loop
{
    // ❌ THIS IS WRONG - causes busy-loop!
    if (_platformBridge.HasPendingEvents())
        break; // Events to process - exit idle loop

    CheckAndSignalExpiredTimer();

    var newActiveProcess = GetActiveProcess();
    if (newActiveProcess != activeProcess)
        break; // ✅ This is correct - process switch occurred

    WaitHandle.WaitAny(handles);  // Blocks here (0% CPU)
    _platformBridge.ResetEventSignalIfEmpty();
}
```

**Why This Is Wrong**:

Events in `SmalltalkEventQueue` do **not** directly wake processes. They sit in the queue until:
1. The **input semaphore** is signaled (via primitive 93 registration)
2. A **waiting process** wakes up due to that signal
3. That process becomes **ready to run**
4. A **process switch** occurs

Breaking on `HasPendingEvents()` causes:
1. WaitAny returns (event signal set)
2. `HasPendingEvents()` = true → **Break from idle loop**
3. Execute background process bytecodes (WRONG!)
4. Re-enter idle check, `HasPendingEvents()` still true
5. **Break immediately** → busy-loop

**The Fix**: Remove the `HasPendingEvents()` check entirely. Only break when `GetActiveProcess() != backgroundProcess`.

### Issue #2: Executing Background Process Bytecodes

**Location**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` lines 304-399

The current structure allows background process bytecodes to execute:
- After exiting idle loop (due to `HasPendingEvents()` bug)
- Before idle is detected (1000 bytecode interval)

**The Problem**:
```csharp
while (true)
{
    if (++periodicCheckCounter >= PeriodicCheckInterval)  // Every 1000 iterations
    {
        // ... idle detection here ...
    }

    // This executes even when background process is active!
    InterpreterStatus status = ExecuteBytecodeStep();
}
```

**The Fix**: Check for background process **every iteration** and skip bytecode execution:
```csharp
while (true)
{
    if (GetActiveProcess() == _backgroundProcessOOP)
    {
        EnterIdleWait();  // Block until process switch
        continue;         // Skip bytecode execution
    }

    InterpreterStatus status = ExecuteBytecodeStep();
}
```

### Correct Idle Loop Implementation

```csharp
private void EnterIdleWait()
{
    var backgroundProcess = GetActiveProcess();

    while (true)
    {
        // ✅ NO HasPendingEvents() check - events don't wake processes directly

        // Check for expired timer - may wake a higher-priority process
        CheckAndSignalExpiredTimer();

        // Check if a process switch occurred
        var currentProcess = GetActiveProcess();
        if (currentProcess != backgroundProcess)
            return;  // Higher-priority process ready - exit idle

        // Block indefinitely until event, timer, or shutdown
        WaitHandle.WaitAny(new[] { eventHandle, timerHandle, shutdownHandle });
        _platformBridge.ResetEventSignalIfEmpty();

        if (ShutdownToken.IsCancellationRequested)
            throw new OperationCanceledException();
    }
}
```

**Key Points**:
1. **No `HasPendingEvents()` check** - events signal the input semaphore, which causes process switch
2. **No timeout on WaitAny** - wait indefinitely; all wake sources use signals
3. **Only exit on process switch** - when `GetActiveProcess()` changes
4. **Handle timer on wake** - `CheckAndSignalExpiredTimer()` may wake a process

---

## The Fix

### Summary

1. **Remove `HasPendingEvents()` check** from the idle wait loop
2. **Check for background process every iteration** and skip bytecode execution
3. **Stay in WaitAny** until a process switch actually occurs

### Complete Implementation

```csharp
private SmalltalkOOP? _backgroundProcessOOP = null;

private SmalltalkOOP InterpreterLoop()
{
    _initialContextOOP = _contextManager.ActiveContext;
    _initialProcessOOP = GetActiveProcess();

    // Cache background process OOP for fast comparison
    _backgroundProcessOOP = FindBackgroundProcess();

    int periodicCheckCounter = 0;
    const int PeriodicCheckInterval = 1000;

    while (true)
    {
        // ✅ Check idle EVERY iteration (cheap pointer comparison)
        var activeProcess = GetActiveProcess();
        if (_platformBridge != null && _backgroundProcessOOP != null &&
            activeProcess == _backgroundProcessOOP)
        {
            // Idle detected - block until process switch
            EnterIdleWait(activeProcess);
            continue;  // Skip bytecode execution - never execute background process
        }

        // Periodic checks (shutdown and timer) for non-idle execution
        if (++periodicCheckCounter >= PeriodicCheckInterval)
        {
            periodicCheckCounter = 0;

            if (ShutdownToken.IsCancellationRequested)
                throw new OperationCanceledException("VM shutdown requested", ShutdownToken);

            CheckAndSignalExpiredTimer();
        }

        // Execute bytecode (only for real processes, NEVER background)
        InterpreterStatus status = ExecuteBytecodeStep();

        switch (status)
        {
            case InterpreterStatus.Continue:
                continue;
            case InterpreterStatus.Return:
                return _returnValue;
            default:
                throw new InvalidOperationException($"Unknown interpreter status: {status}");
        }
    }
}

/// <summary>
/// Enters idle wait loop - blocks indefinitely until work is available.
/// Only exits when a higher-priority process becomes ready (process switch).
///
/// KEY DESIGN: No polling. We wait in WaitAny until:
/// - UI event arrives → signals input semaphore → process switch
/// - Timer expires → signals timer semaphore → process switch
/// </summary>
private void EnterIdleWait(SmalltalkOOP backgroundProcess)
{
    var eventHandle = _platformBridge?.EventAvailableHandle;
    var timerHandle = _timerManager.TimerExpiredHandle;

    while (true)
    {
        // ✅ NO HasPendingEvents() check!
        // Events signal input semaphore → wake process → process switch
        // We detect that via GetActiveProcess() below

        // Check for expired timer and signal semaphore
        // This may wake a higher-priority process
        CheckAndSignalExpiredTimer();

        // Check if a process switch occurred
        var currentProcess = GetActiveProcess();
        if (currentProcess != backgroundProcess)
            return;  // Higher-priority process ready - exit idle

        // Block indefinitely until event, timer, or shutdown
        // TRUE 0% CPU - kernel-level wait on multiple handles
        if (eventHandle != null)
        {
            var handles = new WaitHandle[] {
                eventHandle,
                timerHandle,
                ShutdownToken.WaitHandle
            };
            WaitHandle.WaitAny(handles);  // Blocks here (0% CPU)
            _platformBridge.ResetEventSignalIfEmpty();
        }
        else
        {
            // Fallback for test mode
            Thread.Sleep(10);
        }

        if (ShutdownToken.IsCancellationRequested)
            throw new OperationCanceledException("VM shutdown requested", ShutdownToken);
    }
}

/// <summary>
/// Finds the background process OOP for idle detection.
/// The background process runs at priority 1 (SystemRockBottomPriority).
/// </summary>
private SmalltalkOOP? FindBackgroundProcess()
{
    var schedulerOOP = _metadata.ProcessorScheduler;
    if (schedulerOOP == _metadata.KnownObjects.Nil)
        return null;

    var processListsOOP = _heapManager.GetInstanceVariable(schedulerOOP,
        VMConstants.PROCESSORSCHEDULER_QUIESCENTPROCESSLISTS_INDEX);

    if (processListsOOP == _metadata.KnownObjects.Nil)
        return null;

    // Priority 1 is at index 0
    var priority1ListOOP = _heapManager.GetIndexedOOP(processListsOOP, 0);
    if (priority1ListOOP == _metadata.KnownObjects.Nil)
        return null;

    var backgroundProcessOOP = _heapManager.GetInstanceVariable(priority1ListOOP,
        VMConstants.LINKEDLIST_FIRSTLINK_INDEX);

    return backgroundProcessOOP == _metadata.KnownObjects.Nil ? null : backgroundProcessOOP;
}
```

### Key Points

1. **Never execute background process bytecodes** - detect idle immediately and block
2. **No `HasPendingEvents()` check** - events don't wake processes directly
3. **WaitAny blocks indefinitely** - no timeout needed; all wake sources use signals
4. **Only exit on process switch** - when `GetActiveProcess()` changes
5. **Handle timer on every wake** - `CheckAndSignalExpiredTimer()` may cause process switch

---

## Expected Results

| Metric | Before | After |
|--------|--------|-------|
| Idle CPU | ~3% | **0%** |
| Background bytecodes executed | 1000/wake | **0** |
| Idle detection | Every 1000 bytecodes | Every iteration |
| Normal execution overhead | 0% | ~0.5% |

### Verification

1. Start interpreter with System Browser
2. Wait for system to stabilize
3. Don't touch mouse/keyboard
4. Observe CPU usage: should be **0%**

When you move the mouse:
1. UI event → `eventHandle` signaled
2. WaitAny returns
3. Input semaphore signaled (if registered)
4. Controller process wakes
5. `GetActiveProcess()` returns Controller (not background)
6. Exit idle loop
7. Execute Controller bytecodes
8. Controller sleeps (Delay wait)
9. Background becomes active
10. Immediately re-enter idle (no bytecodes executed)

---

## Implementation Files

**File to modify**:
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`
  - Add `_backgroundProcessOOP` field
  - Add `FindBackgroundProcess()` method
  - Modify `InterpreterLoop()` to check idle every iteration
  - Modify `EnterIdleWait()` to remove `HasPendingEvents` check

**No changes needed**:
- `SmalltalkEventQueue.cs` - Already correct (ManualResetEventSlim)
- `TimerManager.cs` - Already correct (interrupt-driven)
- `IPlatformBridge.cs` - Already correct

---

## Conclusion

The 3% idle CPU is caused by two bugs:
1. **Breaking on `HasPendingEvents()`** - events don't wake processes directly; they signal input semaphore
2. **Executing background process bytecodes** - the background process is a sentinel, not work

**The Fix**:
- Remove `HasPendingEvents()` check entirely
- Check for background process every iteration
- Never execute background process bytecodes
- Stay in `WaitAny` until a real process switch occurs

**Result**: True 0% CPU when idle. The system blocks in the kernel until events or timers require action.
