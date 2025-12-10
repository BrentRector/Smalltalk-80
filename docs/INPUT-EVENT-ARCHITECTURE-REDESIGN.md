# Input Event Architecture Redesign

**Date**: 2025-12-09
**Status**: Design Document

## Problem Statement

The current implementation has **TWO competing event systems** that were never properly unified:

1. **InputWordRingBuffer** (new): Thread-safe ring buffer with shadow counter for Blue Book format words
2. **SmalltalkEventQueue** (legacy): Old event queue with duplicate word generation and button tracking

### Critical Bug

**Button events are enqueued but never processed** because:

- Ring buffer writes events and increments shadow counter ✓
- Ring buffer has callback support (`SetEventCallback`) ✓
- **BUT**: WinFormsDisplayProvider only wires the callback to the legacy SmalltalkEventQueue
- **Result**: Ring buffer never invokes callback when button events are enqueued
- **Consequence**: `_inputEventPending` flag never gets set, InputProcess never wakes, button events sit in ring buffer forever

## Blue Book Design Intent

The original Smalltalk-80 input architecture:

1. **Hardware interrupt**: Mouse/keyboard events trigger hardware interrupt
2. **Word generation**: Hardware writes Blue Book format words to buffer
3. **Signal semaphore**: Hardware signals InputSemaphore's excessSignals
4. **Process wakes**: InputProcess checks semaphore, dequeues words, updates state
5. **Controllers poll**: Controllers poll InputSensor state to check buttons/position

**Blue Book format words**:
```
Bits 12-15: event type (1=mouseX, 2=mouseY, 3=keyDown, 4=keyUp)
Bits 0-11:  parameter value (coordinate or key code)
```

**Mouse button encoding**:
```
Blue/Right   = 128 (8r200)
Yellow/Middle = 129 (8r201)
Red/Left     = 130 (8r202)
```

## Modern C# Implementation

**Thread architecture**:
- **UI thread** (WinForms): Receives OS events, writes words to ring buffer
- **Smalltalk thread**: Checks atomic flag, signals InputSemaphore, executes Smalltalk code

**Key constraint**: Only Smalltalk thread can modify heap (semaphore's excessSignals)

**Solution**: Shadow counter pattern
1. UI thread increments ring buffer's shadow counter (C# atomic int)
2. UI thread invokes callback to set atomic `_inputEventPending` flag
3. Smalltalk thread checks flag in interpreter loop
4. Smalltalk thread transfers shadow signals to semaphore's excessSignals

## Proposed Clean Architecture

### Data Flow

```
Windows Event (UI thread)
    ↓
WinFormsDisplayProvider event handler
    ↓
InputWordRingBuffer.Enqueue(word)
    ├── Write word to ring buffer [head]
    ├── Interlocked.Increment(_shadowSignalCount)
    └── _eventCallback?.Invoke()  ← Sets _inputEventPending = 1

VirtualMachine.InterpreterLoop() (Smalltalk thread)
    ↓
VirtualMachine.CheckAndSignalInputEvents()
    ├── Check/clear _inputEventPending flag
    ├── TryConsumeInputShadowSignal() in loop
    └── SignalSemaphore(InputSemaphore) for each consumed signal

InputProcess (Smalltalk-level, priority 6)
    ├── Wait on InputSemaphore
    ├── Primitive 95 (inputWord) → dequeues from ring buffer
    └── Update InputSensor state (cursorPoint, bitState)

Controllers (Smalltalk-level, various priorities)
    └── Poll InputSensor state
```

### Component Responsibilities

#### InputWordRingBuffer (d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\InputWordRingBuffer.cs)

**KEEP - This is the canonical event storage**

Responsibilities:
- Store Blue Book format input words in thread-safe ring buffer
- Track shadow signal count (mirrors semaphore excessSignals)
- Invoke callback when words are enqueued
- Provide TryDequeue for primitive 95
- Provide TryConsumeSignal for primitive 86 shadow signal transfer

Current status: ✓ **Already correct**, just needs callback wired up

#### SmalltalkEventQueue (d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\SmalltalkEventQueue.cs)

**REMOVE most functionality - keep only polling state**

Current problems:
- Duplicate word generation in `ProcessEventToWords()` ❌
- Duplicate word storage in `_inputWords` ConcurrentQueue ❌
- Duplicate signaling via `_eventSemaphore` ❌
- Duplicate callback invocation ❌

Keep only:
- Current mouse position (`_currentX`, `_currentY`) for primitive 90
- Current button state (`_currentButtons`) for primitive 199
- Button transition flag for ControllerSemaphore signaling

Responsibilities after cleanup:
- Track current input state for polling primitives (90, 199)
- Detect button transitions for ControllerSemaphore signaling
- NO word generation
- NO word storage
- NO semaphore signaling for InputSemaphore

#### WinFormsDisplayProvider (d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\Windows\WinFormsDisplayProvider.cs)

**MODIFY - Route all events through ring buffer**

Current problems:
- Writes to ring buffer ✓
- Also writes to legacy event queue ❌ (duplicate)
- SetEventCallback wired to legacy queue ❌ (wrong callback source)

Required changes:
1. Wire callback to ring buffer: `_inputWordBuffer.SetEventCallback(callback)`
2. Keep legacy queue writes ONLY for state tracking (mouse position, button state)
3. DO NOT generate words in legacy queue

#### VirtualMachine (d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\VirtualMachine.cs)

**KEEP - Already correct**

Current implementation:
- `InitializeEventArchitecture()` sets callback via `_platformBridge.SetEventCallback()`
- Callback sets atomic `_inputEventPending` flag
- `CheckAndSignalInputEvents()` transfers shadow signals to InputSemaphore

Status: ✓ **No changes needed**

#### Primitive 95 (InputWord) (d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Core\VM\Primitives\InputPrimitives.cs)

**KEEP - Already correct**

Current implementation:
```csharp
int word = context.Platform?.GetInputWord() ?? 0;
```

Routes to:
```csharp
WinFormsDisplayProvider.GetInputWord() → _inputWordBuffer.TryDequeue(out word)
```

Status: ✓ **No changes needed**

#### Primitive 90 (MousePoint) and Primitive 199 (MouseButtons)

**KEEP - Delegate to SmalltalkEventQueue for current state**

These primitives need polling access to current input state:
- Primitive 90: `GetMousePosition()` → reads `(_currentX, _currentY)`
- Primitive 199: `GetMouseButtons()` → reads `_currentButtons`

Status: ✓ **No changes needed** (SmalltalkEventQueue keeps state tracking)

## Implementation Plan

### Phase 1: Fix Ring Buffer Callback Wiring

**File**: `d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\Windows\WinFormsDisplayProvider.cs`

**Change**: Wire callback to ring buffer instead of legacy queue

```csharp
// OLD (line 595):
public void SetEventCallback(Action callback)
{
    _eventQueue.SetEventCallback(callback);  // ❌ Wrong!
}

// NEW:
public void SetEventCallback(Action callback)
{
    // Wire callback to ring buffer - this is the canonical event source
    _inputWordBuffer.SetEventCallback(callback);

    // Also wire to legacy queue for backward compatibility
    // (in case any code still uses composite events)
    _eventQueue.SetEventCallback(callback);
}
```

**Impact**: Button events will now trigger `_inputEventPending` flag, InputProcess will wake and dequeue words from ring buffer.

### Phase 2: Remove Duplicate Word Generation from SmalltalkEventQueue

**File**: `d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\SmalltalkEventQueue.cs`

**Changes**:

1. **Remove** `_inputWords` ConcurrentQueue (line 96)
2. **Remove** `ProcessEventToWords()` method (lines 250-323)
3. **Simplify** `Enqueue()` to only update state and detect transitions

```csharp
// NEW Enqueue() - only track state, no word generation
public void Enqueue(SmalltalkEvent evt)
{
    if (evt.Type == SmalltalkEventType.Mouse)
    {
        lock (_mouseLock)
        {
            // Detect button transitions before updating state
            bool buttonChanged =
                (evt.RedButton != _lastRedButton) ||
                (evt.YellowButton != _lastYellowButton) ||
                (evt.BlueButton != _lastBlueButton);

            if (buttonChanged)
            {
                _buttonTransitionPending = true;
            }

            // Update current state for polling (primitives 90, 199)
            _currentX = evt.X;
            _currentY = evt.Y;

            // Pack button state into single int
            int buttons = 0;
            if (evt.RedButton) buttons |= 1;
            if (evt.YellowButton) buttons |= 2;
            if (evt.BlueButton) buttons |= 4;
            _currentButtons = buttons;

            // Track last button state for next transition detection
            _lastRedButton = evt.RedButton;
            _lastYellowButton = evt.YellowButton;
            _lastBlueButton = evt.BlueButton;
        }
    }

    // NO WORD GENERATION - ring buffer handles that
    // NO SIGNALING - ring buffer handles that
}
```

4. **Remove** `GetNextInputWord()` method (lines 330-337) - dead code
5. **Simplify** `HasPendingEvents` property - only check button transition flag

```csharp
public bool HasPendingEvents => _buttonTransitionPending;
```

6. **Keep** `GetMousePosition()` and `GetMouseButtons()` for polling primitives

**Impact**: Eliminates duplicate word generation, reduces memory overhead, clarifies responsibilities.

### Phase 3: Update WinFormsDisplayProvider to Remove Legacy Queue Word Generation

**File**: `d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\Platform\Windows\WinFormsDisplayProvider.cs`

**No changes needed** - event handlers already write to both ring buffer (for words) and legacy queue (for state).

After Phase 2, the legacy queue won't generate words, so the duplication is eliminated.

### Phase 4: Clean Up Dead Code

**Files to check**:
- Remove any calls to `GetNextInputWord()` (should be none - dead code)
- Remove `DequeueCompositeEvent()` if not used (check if needed for debugging)
- Remove `_eventSemaphore` from SmalltalkEventQueue (replaced by ring buffer's shadow counter)

## Testing Plan

### Test 1: Button Events Are Processed

**Steps**:
1. Run interpreter with System Browser
2. Click mouse button
3. Verify debug output shows:
   - `[RingBuffer] Enqueue: KeyDown keyCode=130` (button down)
   - `[Callback] _inputEventPending set to 1`
   - `[CheckAndSignal] Transferred N shadow signals to InputSemaphore`
   - `[Prim95] GetInputWord returned type=3 param=130`

**Expected**: Button events flow through ring buffer and are processed by InputProcess

### Test 2: Mouse Movement Works

**Steps**:
1. Run interpreter with System Browser
2. Move mouse
3. Verify display cursor tracks mouse position

**Expected**: Mouse position updates work (uses ring buffer + polling state)

### Test 3: Keyboard Input Works

**Steps**:
1. Run interpreter with System Browser
2. Click in a text pane
3. Type characters
4. Verify characters appear

**Expected**: Keyboard events flow through ring buffer

### Test 4: System Browser Remains Functional

**Steps**:
1. Run interpreter with System Browser
2. Verify all browser operations work:
   - Select class
   - View methods
   - Edit method text
   - Browse categories

**Expected**: Full System Browser functionality with no regressions

## Migration Notes

**Backward Compatibility**:
- Primitive 90 (mousePoint): Still works - polls SmalltalkEventQueue state
- Primitive 95 (inputWord): Still works - reads from ring buffer
- Primitive 199 (mouseButtons): Still works - polls SmalltalkEventQueue state

**Performance Impact**:
- **Improvement**: Eliminates duplicate word generation (2x reduction in work)
- **Improvement**: Eliminates duplicate ConcurrentQueue allocations
- **No regression**: All primitives maintain same API

**Risk Assessment**:
- **Low risk**: Changes are mostly deletions, not rewrites
- **Safety**: Ring buffer already tested and working
- **Rollback**: Easy to revert if issues found (just git revert)

## Open Questions - RESOLVED

1. **DequeueCompositeEvent**: Is this still used anywhere? If not, remove it.
   - **Answer**: Not used for event processing. Keep for now (might be useful for debugging).

2. **EventAvailableHandle**: Legacy queue exposes a Semaphore WaitHandle - is this used for idle waiting?
   - **Answer**: NO - WaitAny is NOT used anywhere in production code. The VM currently busy-loops.

3. **IdleWake**: Does idle detection depend on the legacy queue's semaphore?
   - **Answer**: NO - idle detection is NOT implemented. The interpreter loop runs continuously.

**Key Finding**: The current implementation has NO idle waiting. The interpreter loop busy-spins checking `_inputEventPending` flag. This means:
- EventAvailableHandle is UNUSED (dead code)
- IdleWakeHandle is UNUSED (dead code)
- WaitAny infrastructure exists but is never called
- CPU usage is 100% even when Smalltalk is idle

**Implication for redesign**: We can safely ignore all the WaitHandle/WaitAny code paths. The critical bug fix is simply wiring the ring buffer callback correctly.

## Success Criteria

✓ Button events are processed immediately (no missed clicks)
✓ Mouse movement works smoothly
✓ Keyboard input works correctly
✓ System Browser fully functional
✓ No duplicate word generation
✓ Single canonical event source (ring buffer)
✓ Clean separation: ring buffer = words, event queue = polling state

## References

- Blue Book Chapter 30: The Virtual Image and Virtual Machine
- `InputWordRingBuffer.cs` - Canonical implementation reference
- `CLAUDE.md` - Project status and architecture notes
- `docs/Architecture/PROCESS-MANAGEMENT.md` - Process scheduling architecture
