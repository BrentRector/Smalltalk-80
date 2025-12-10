# Resume Prompt: Smalltalk-80 Interpreter

**Session Date**: 2025-12-09
**Status**: ⚠️ BLOCKED - Method Lookup Bug

## Current State

### What's Working
- ✅ **Build**: 0 errors, 0 warnings
- ✅ **Cold Start**: Phases 1-6 all succeed
- ✅ **Init Expressions**: 286/286 (100%)
- ✅ **InputProcess**: Working - processes mouse/keyboard events correctly
- ✅ **Process Primitives 85-88**: suspend, resume, terminate, yield
- ✅ **BlockContext Tests**: 120/120 passing (returns 1191)
- ✅ **Primitive 202 (bitState)**: Atomic button state polling for tight loops

### What's Broken
- ❌ **Method Lookup Bug**: Crashes on mouse movement with "Instance variable index 15 out of range [0, -1] for object oop=2 (UndefinedObject)"
- ❌ **System Browser**: Non-functional (crashes before GUI appears due to above bug)

## Recent Changes (This Session)

### Dead Code Cleanup
Removed incomplete "dual-semaphore architecture" code that was never finished:

**Smalltalk-80.st**:
- Removed `MouseEvent` class entirely (239 → 238 classes)
- Removed `InputSensor` methods: `waitForControllerEvent`, `waitForEvent`, `nextMouseEvent`, `peekMouseEvent`, `hasMouseEvent`
- Removed `InputState` methods: `primControllerSemaphore:`, `primInputDrainComplete`, `controllerSemaphore`, `inputSemaphore`, `mouseEventQueue`
- Reverted `InputSensor>>buttons` to original: `^self primMouseButtons`
- Reverted `InputSensor>>primMouseButtons` to original: `^CurrentInputState mouseButtons`

**C# Primitives**:
- Removed primitives 160, 199, 200, 201 (all dead code)
- Kept primitive 202 (bitState) - the only useful change

### Atomic Mouse State Architecture
Primitive 202 (`bitState`) provides atomic button state polling using packed `long`:

```csharp
// WinFormsDisplayProvider.cs
private long _mouseState;  // Packed: X | (Y << 16) | (buttons << 32)

// UI thread writes atomically
Interlocked.Exchange(ref _mouseState, PackMouseState(x, y, buttons));

// Smalltalk thread reads atomically (primitive 202)
public int GetButtonState()
{
    long state = Interlocked.Read(ref _mouseState);
    return (int)((state >> 32) & 0x7);  // red=4, yellow=2, blue=1
}
```

Call chain: `redButtonPressed` → `buttons` → `mouseButtons` → `bitState` → primitive 202

---

## The Critical Bug

**Error**: `Instance variable index 15 out of range [0, -1] for object oop=2 (UndefinedObject)`

**Symptom**: Interpreter crashes when mouse moves over window. InputProcess is working correctly (receiving events, signaling semaphores), but something in the controller loop causes method lookup to fail.

**Investigation Status**: Debug logging added. See `docs/DEBUGGING-METHOD-LOOKUP-BUG.md`.

**Next Steps**:
1. Capture method OOP at crash point
2. Trace message send to identify why lookup returns wrong method
3. The error suggests a method is being executed with nil receiver when it expects an object with 16+ instance variables

---

## Key Files

### Modified This Session
- `Smalltalk-80.st` - Removed dead code, reverted to original InputSensor implementations
- `InputPrimitives.cs` - Removed primitives 160, 199, 200, 201; kept 202
- `PrimitiveRegistry.cs` - Removed registrations for dead primitives

### Input System
- `WinFormsDisplayProvider.cs` - Atomic mouse state, ring buffer
- `InputWordRingBuffer.cs` - Thread-safe ring buffer for Blue Book input words
- `InputPrimitives.cs` - Primitives 90-95, 202

### Process Scheduling
- `VirtualMachine.ProcessScheduling.cs` - Process scheduling (~330 lines)
- `VirtualMachine.cs` - InterpreterLoop with WaitHandle.WaitAny

---

## Test Commands

```bash
# Standard run (crashes on mouse movement)
cd Smalltalk80.Interpreter
dotnet run

# BlockContext regression test (MUST PASS)
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)

# C# unit tests
cd Smalltalk80.Interpreter.Tests
dotnet test

# Regenerate coldstart after source changes
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80.st"
```

---

## Model Selection

Always use model: **claude-sonnet-4-5**
