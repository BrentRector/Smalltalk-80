# Clean-Slate Event Architecture Refactor - Implementation Plan

**Status**: Phase 5 - Complete ✅
**Started**: 2025-12-03
**Estimated Scope**: ~840 lines of new/modified code across 10 phases
**Goal**: Fix nested blocking loop deadlock while preserving 0% idle CPU

---

## Executive Summary

This plan implements a **clean-slate refactor** of the event handling system based on the proposal document. The current architecture deadlocks when nested consumers (popup menus) block while waiting for input because events are only signaled when the VM is idle.

**Core Solution**: Software interrupt simulation with edge-triggered signaling
- Atomic interrupt flag (`_inputInterruptPendingInt`) using `Interlocked.Exchange`
- Runnable/draining latch to prevent re-signaling during event drain
- Strategic check points (backward branches, method returns) for tight loop responsiveness
- InputProcess (priority 6) preempts controllers (priority 4) when input arrives

---

## Implementation Status

- [x] **Phase 1**: Core VM Infrastructure (~60 lines) - **COMPLETE** ✅
- [x] **Phase 2**: Idle Path Hardening (~50 lines) - **COMPLETE** ✅
- [x] **Phase 3**: Strategic Check Points (~80 lines) - **COMPLETE** ✅
- [x] **Phase 4**: Scheduler Context Switch Hook (~30 lines) - **COMPLETE** ✅
- [x] **Phase 5**: Interrupt Masking (~40 lines) - **COMPLETE** ✅
- [ ] **Phase 6**: Diagnostics Infrastructure (~200 lines)
- [x] **Phase 7**: Smalltalk-Side Implementation (~150 lines) - **COMPLETE** ✅
- [ ] **Phase 8**: Event Coalescing and Fairness (~80 lines)
- [ ] **Phase 9**: OS Integration (~50 lines)
- [ ] **Phase 10**: Testing and Validation (~100 lines)

---

## Current Architecture Analysis

### Key Files
- `SmalltalkEventQueue.cs` - Uses counting semaphore, provides `DequeueCompositeEvent()`
- `VirtualMachine.cs` - Idle detection every iteration, `WaitHandle.WaitAny` in `EnterIdleWait()`
- `VirtualMachine.ProcessScheduling.cs` - Process switching and semaphore signaling
- `InputPrimitives.cs` - Primitive 95 returns composite event Array
- `Smalltalk-80.st` - InputState>>run, InputSensor, PopUpMenu, ProcessorScheduler
- `WinFormsDisplayProvider.cs` - Captures OS events, enqueues to queue
- `TimerManager.cs` - Uses `System.Threading.Timer` with `ManualResetEventSlim`

### The Problem
Events are **only signaled when VM is idle**. When a controller (priority 4) blocks waiting for input while NOT idle (e.g., popup menu tracking), the InputProcess never gets signaled, events accumulate, and the controller deadlocks.

---

## Phase 1: Core VM Infrastructure (~60 lines)

**Status**: ⏳ In Progress
**Goal**: Establish atomic edge-triggered interrupt flag and runnable guard

### Files to Modify
- `VirtualMachine.cs`
- `SmalltalkEventQueue.cs`
- `InputPrimitives.cs`
- `PrimitiveTable.cs`

### Tasks
- [x] Step 1.1: Add atomic interrupt flag fields to VirtualMachine.cs
- [ ] Step 1.2: Update SmalltalkEventQueue.Enqueue to arm flag atomically
- [ ] Step 1.3: Implement CheckAndSignalPendingEvents method
- [ ] Step 1.4: Implement InputDrainComplete primitive (199)
- [ ] Step 1.5: Register primitive in PrimitiveTable

---

## Phase 2: Idle Path Hardening (~50 lines)

**Status**: ⏸️ Not Started
**Goal**: Fix idle wait to use ManualResetEventSlim with proper guards

### Files to Modify
- `VirtualMachine.cs`
- `SmalltalkEventQueue.cs`

### Tasks
- [ ] Step 2.1: Add ManualResetEventSlim for idle wake (keep counting semaphore for events)
- [ ] Step 2.2: Rewrite EnterIdleWait loop with hardened guards
- [ ] Step 2.3: Update UI thread Enqueue to set idle wake handle
- [ ] Step 2.4: Remove legacy ForceResetEventSignal calls

---

## Phase 3: Strategic Check Points (~80 lines)

**Status**: ⏸️ Not Started
**Goal**: Add event checks at backward branches and method returns

### Files to Create
- `EventConfig.cs` (NEW)

### Files to Modify
- `VirtualMachine.cs` (bytecode execution)

### Tasks
- [ ] Step 3.1: Add backward branch check in jump bytecodes
- [ ] Step 3.2: Add method return check in HandleReturn
- [ ] Step 3.3: Create EventConfig class with flags and profiles
- [ ] Step 3.4: Wire up configuration in VM constructor

---

## Phase 4: Scheduler Context Switch Hook (~30 lines)

**Status**: ⏸️ Not Started
**Goal**: Clear runnable latch when process switches away from InputProcess

### Files to Modify
- `VirtualMachine.cs`
- `VirtualMachine.ProcessScheduling.cs`
- `InputPrimitives.cs`

### Tasks
- [ ] Step 4.1: Add SetInputProcessOOP method and primitive
- [ ] Step 4.2: Add context switch hook in TransferTo
- [ ] Step 4.3: Add IsProcessBlocked helper method

---

## Phase 5: Interrupt Masking (~40 lines)

**Status**: ⏸️ Not Started
**Goal**: Prevent signaling during GC and long primitives

### Files to Modify
- `VirtualMachine.cs`
- `HeapManager.cs`

### Tasks
- [ ] Step 5.1: Add interrupt mask counter and MaskInterrupts/UnmaskInterrupts methods
- [ ] Step 5.2: Modify CheckAndSignalPendingEvents to respect mask
- [ ] Step 5.3: Wrap GC with interrupt masking

---

## Phase 6: Diagnostics Infrastructure (~200 lines)

**Status**: ⏸️ Not Started
**Goal**: Add comprehensive monitoring and debugging support

### Files to Create
- `EventDiagnostics.cs` (NEW)

### Files to Modify
- `VirtualMachine.cs`

### Tasks
- [ ] Step 6.1: Create EventDiagnostics class with counters
- [ ] Step 6.2: Add trace hooks with [Conditional("TRACE_EVENTS")]
- [ ] Step 6.3: Add debug assertions for invariants
- [ ] Step 6.4: Instrument key points in event flow

---

## Phase 7: Smalltalk-Side Implementation (~150 lines)

**Status**: ⏸️ Not Started
**Goal**: Implement InputProcess drain loop and consumer protocols

### Files to Modify
- `Smalltalk-80.st`

### Tasks
- [ ] Step 7.1: Update InputState>>run with full queue drain loop
- [ ] Step 7.2: Add primInputDrainComplete primitive method
- [ ] Step 7.3: Update InputState>>install to create keyboard event queue (optional)
- [ ] Step 7.4: Recompile Smalltalk source and regenerate coldstart bundle

---

## Phase 8: Event Coalescing and Fairness (~80 lines)

**Status**: ⏸️ Not Started
**Goal**: Implement move coalescing and event class ordering

### Files to Modify
- `SmalltalkEventQueue.cs`
- `Smalltalk-80.st`

### Tasks
- [ ] Step 8.1: Add move coalescing logic (gated by EventConfig flag)
- [ ] Step 8.2: Add backpressure soft cap for pending moves
- [ ] Step 8.3: Add event class ordering (discrete before moves)

---

## Phase 9: OS Integration (~50 lines)

**Status**: ⏸️ Not Started
**Goal**: Handle focus loss, capture loss, suspend/resume

### Files to Modify
- `WinFormsDisplayProvider.cs`
- `Smalltalk-80.st`
- `SmalltalkEvent.cs`

### Tasks
- [ ] Step 9.1: Add focus/capture loss event handlers in WinForms
- [ ] Step 9.2: Add SystemEventType enum for system events
- [ ] Step 9.3: Update PopUpMenu with checkForCancel method
- [ ] Step 9.4: Recompile Smalltalk source

---

## Phase 10: Testing and Validation (~100 lines)

**Status**: ⏸️ Not Started
**Goal**: Execute test plan and validate all invariants

### Tests to Create
- `EventArchitectureTests.cs` (NEW)

### Tasks
- [ ] Step 10.1: Create unit tests for atomic flag operations
- [ ] Step 10.2: Test runnable guard latch behavior
- [ ] Step 10.3: Test interrupt masking
- [ ] Step 10.4: Manual integration tests (popup menu, idle CPU, burst stress)
- [ ] Step 10.5: Regression tests (BlockContextTest, timer/delay, keyboard, display)
- [ ] Step 10.6: Advanced tests (focus loss, mixed input, suspend/resume, GC under load)

---

## Critical Files Summary

### To Create (New Files)
- `EventConfig.cs` - Configuration constants and profiles
- `EventDiagnostics.cs` - Diagnostics counters and metrics
- `EventArchitectureTests.cs` - Unit tests

### To Modify (Existing Files)
- `VirtualMachine.cs` - Add flag, latch, CheckAndSignalPendingEvents, checks
- `VirtualMachine.ProcessScheduling.cs` - Add context switch hook
- `SmalltalkEventQueue.cs` - Add ManualResetEventSlim, arm flag
- `InputPrimitives.cs` - Add primitive 199, register InputProcess OOP
- `HeapManager.cs` - Wrap GC with interrupt masking
- `WinFormsDisplayProvider.cs` - Add focus/capture loss handlers
- `Smalltalk-80.st` - Update InputState>>run, add primInputDrainComplete
- `PrimitiveTable.cs` - Register new primitives

---

## Implementation Order

1. ✅ Phase 1 (Core Infrastructure) - CURRENT
2. Phase 2 (Idle Path)
3. Phase 3 (Check Points)
4. Phase 4 (Scheduler Hook)
5. Phase 5 (Interrupt Masking)
6. Phase 6 (Diagnostics)
7. Phase 7 (Smalltalk)
8. Phase 8 (Coalescing) - Optional
9. Phase 9 (OS Integration)
10. Phase 10 (Testing)

**Minimal Path to Fix Deadlock**: Phases 1-4 + Phase 7

---

## Validation Gates

**Checkpoint 1 (Correctness)**:
- Edge-trigger flag integrated
- InputProcess drains queue fully
- Idle path coalesces events
- Popup menu deadlock resolved
- All regression tests pass

**Checkpoint 2 (Performance)**:
- CPU ~0% when idle
- No preemption storm under burst load
- Overhead < 0.1% CPU during active use

**Checkpoint 3 (Robustness)**:
- No lost transitions
- Latency < 1ms in tight loops
- Stress test passes

---

## Change Log

### 2025-12-03 - Phase 5 Complete ✅
- ✅ Added interrupt mask counter field `_interruptsMaskedCounter` to VirtualMachine.cs
  - Tracks nesting level of mask/unmask calls
  - Initialized to 0 in VM constructor
- ✅ Implemented `MaskInterrupts()` method
  - Increments counter to prevent event signaling during critical sections
  - Supports nesting - each call must be paired with UnmaskInterrupts
- ✅ Implemented `UnmaskInterrupts()` method
  - Decrements counter
  - When counter reaches 0, signals any events that arrived while masked
- ✅ Modified `CheckAndSignalPendingEvents()` to respect interrupt mask
  - Returns early if `_interruptsMaskedCounter > 0`
- ✅ Added GC callback infrastructure to SmalltalkHeapManager
  - Added `_gcBeginCallback` and `_gcEndCallback` fields
  - Implemented `SetGCCallbacks()` method
  - Wrapped `GarbageCollect()` call with callbacks in try/finally
- ✅ Wired up GC callbacks in VM constructor
  - Calls `SetGCCallbacks(MaskInterrupts, UnmaskInterrupts)` on heap manager
- ✅ Build succeeds: 0 errors, 0 warnings
- **Status**: Interrupt masking complete, GC will not be interrupted by input events

### 2025-12-03 - Phase 4 Complete ✅
- ✅ Added `SetInputProcessOOP()` method to VirtualMachine.cs
  - Stores InputProcess OOP in `_inputProcessOOP` field for context switch hook
- ✅ Added `IsProcessBlocked()` helper method to VirtualMachine.ProcessScheduling.cs
  - Checks if process is waiting on a semaphore (myList != nil)
- ✅ Added context switch hook in `ActivateProcess()` method
  - Detects when InputProcess is being switched out while blocked
  - Clears runnable latch to allow next signal from event queue
  - Logic: `if (oldProcess == InputProcess && switching away && blocked) clear latch`
- ✅ Implemented primitive 201: `registerInputProcess`
  - Takes process OOP as argument
  - Calls `SetInputProcessOOP()` to register the InputProcess
- ✅ Registered primitive 201 in PrimitiveRegistry
- ✅ Build succeeds: 0 errors, 0 warnings
- **Status**: Context switch hook complete, latch will be cleared when InputProcess blocks

### 2025-12-03 - Phase 3 Complete ✅
- ✅ Created EventConfig.cs with configuration flags and profiles
  - PeriodicCheckInterval = 1000 bytecodes
  - EnableBackwardBranchCheck = true (default)
  - EnableMethodReturnCheck = true (default)
  - EnablePrimitiveReturnCheck = false (optional optimization)
  - Profiles: conservative, responsive, stress, debug
- ✅ Added event check callback to IBytecodeInterpreter interface
- ✅ Implemented backward branch check in long jump handler
  - Checks if jump offset < 0 (backward branch indicates loop)
  - Invokes callback when EventConfig.EnableBackwardBranchCheck is true
- ✅ Implemented method return check in HandleReturnFromMessage
  - Reduces latency for deeply nested call chains
  - Invokes callback when EventConfig.EnableMethodReturnCheck is true
- ✅ Wired up CheckAndSignalPendingEvents callback in VirtualMachine.cs
- ✅ Build succeeds: 0 errors, 0 warnings
- **Status**: Strategic check points complete, tight loops can now respond to input interrupts

### 2025-12-03 - Phase 7 Complete ✅ TESTED
- ✅ **CRITICAL BUG FIX**: Runnable latch logic reordered to prevent flag consumption when latch blocks signaling
  - **Problem**: `Interlocked.Exchange` consumed atomic flag BEFORE checking latch
  - **Symptom**: Only first mouse event worked, subsequent events lost
  - **Fix**: Check `_inputProcessRunnableOrDraining` first, only consume flag if we can signal
  - **Applied to 3 locations**: CheckAndSignalPendingEvents, EnterIdleWait guard, EnterIdleWait batch wake
- ✅ **Testing confirmed**: Mouse events now work correctly
  - Red, yellow, blue buttons all captured
  - Mouse movement tracked continuously
  - Drain loop processes batches of events
  - primInputDrainComplete clears latch correctly
- **Status**: Phase 7 complete and verified working

### 2025-12-03 - Phase 7 Complete ✅
- ✅ Updated InputState>>run to use drain loop pattern
  - Changed from processing ONE event per wait to draining ALL events
  - Added inner `[event _ self primInputWord. event notNil] whileTrue:` loop
  - Event processing logic preserved (mouse events, keyboard events)
- ✅ Added primInputDrainComplete method (primitive 200)
  - Placed after primInputWord in Smalltalk-80.st
  - Signals VM that InputProcess has drained the queue
- ✅ Re-enabled runnable/draining latch in VirtualMachine.cs
  - Uncommented `_inputProcessRunnableOrDraining` field declaration
  - Uncommented initialization in constructor
  - Updated CheckAndSignalPendingEvents to set latch when signaling
  - Updated EnterIdleWait self-healing guard to use latch
  - Updated EnterIdleWait batch wake to use latch
  - Uncommented InputDrainComplete method body to clear latch
- ✅ Recompiled Smalltalk-80.st source
  - Compiler generated new coldstart.json with drain loop
  - Build automatically copied Data folder to interpreter bin directory
- ✅ Build succeeds: 0 errors, 0 warnings
- ✅ Interpreter starts successfully and begins executing init expressions
- **Status**: Phase 7 complete - drain loop implemented, latch enabled, ready for testing

### 2025-12-03 - Phase 2 Complete (with fixes) ✅
- ✅ Added ManualResetEventSlim (`_idleWake`) to SmalltalkEventQueue
- ✅ Exposed IdleWakeHandle and IdleWake properties in IPlatformBridge
- ✅ Completely rewrote EnterIdleWait with hardened guards
- ✅ Added pre-park guard: don't park if events pending (self-healing)
- ✅ Check timers and events FIRST before parking
- ✅ Use ManualResetEventSlim with explicit reset (not AutoResetEvent)
- ✅ Updated Enqueue to call `_idleWake.Set()` when VM is idle
- ✅ Implemented IdleWakeHandle in TestPlatformBridge and WinFormsDisplayProvider
- ✅ **CRITICAL FIX**: Added synchronization to prevent events before callbacks registered
  - Added `EventCallbacksReady` ManualResetEventSlim to SmalltalkThreadContext
  - Smalltalk thread signals after InitializeEventArchitecture completes
  - Main thread waits before showing window to prevent race condition
  - This fixes the "callbacks are NULL" issue where events arrived before infrastructure was ready
- ✅ **DEFERRED**: Runnable/draining latch temporarily disabled until Phase 7 (Smalltalk drain loop) implemented
  - Without Smalltalk-side primInputDrainComplete, latch would stay true forever
  - Edge-triggered atomic flag still works for signaling
- ✅ Build succeeds: 0 errors, 0 warnings
- ✅ Mouse events working: InputSemaphore being signaled correctly
- **Status**: Idle path complete with synchronization fix, runnable latch deferred to Phase 7

### 2025-12-03 - Phase 1 Complete ✅
- ✅ Added atomic interrupt flag (`_inputInterruptPendingInt`) to VirtualMachine.cs
- ✅ Added runnable/draining latch (`_inputProcessRunnableOrDraining`)
- ✅ Updated SmalltalkEventQueue.Enqueue to arm flag atomically via callback
- ✅ Implemented CheckAndSignalPendingEvents method with edge-trigger logic
- ✅ Implemented InputDrainComplete primitive (200)
- ✅ Registered primitive 200 in PrimitiveTable
- ✅ Added SetEventArchitectureCallbacks to IPlatformBridge interface
- ✅ Wired up callbacks in VirtualMachine.InitializeEventArchitecture
- ✅ Build succeeds: 0 errors, 0 warnings
- **Status**: Core infrastructure complete, ready for Phase 2

### 2025-12-03 - Phase 1 Started
- Created implementation plan document
- Beginning Phase 1: Core VM Infrastructure
