# Resume Prompt: MVC Controller Loop and Compiler Optimization

**Session Date**: 2025-11-28
**Branch**: `claude/smalltalk-mvc-processes-013RydArs54dc78quMu5GZic`
**Status**: BLOCKED - Compiler optimization issue discovered

## Current Issue

### The Problem: `thisContext` Returns BlockContext Inside `ifTrue:` Blocks

When `Process>>terminate` executes:
```smalltalk
Processor activeProcess == self
    ifTrue:
        [thisContext sender == nil ifFalse: [thisContext sender release].
        thisContext removeSelf suspend]
```

`thisContext` returns a **BlockContext** (not MethodContext), and `removeSelf` only exists on MethodContext.

### Root Cause: Compiler Optimization Disabled

**File**: `Smalltalk80.Compiler/BytecodeCompiler.cs` lines 1004-1018

```csharp
/// DISABLED (2025-10-07): Blue Book Smalltalk-80 does NOT inline conditionals!
/// Control structures like ifTrue: and ifFalse: are ordinary message sends.
private bool TryInlineConditional(...)
{
    return false;  // ALWAYS disabled
}
```

**This comment is WRONG!** Blue Book Chapter 28 explicitly states:
> "The conditional jumps are used in the compiled form of messages to booleans (e.g., `ifTrue:` and `whileFalse:`)."

Blue Book compiles `ifTrue:`/`ifFalse:` to conditional jump bytecodes - NO BlockContext is created. The block body executes inline in the MethodContext. Therefore `thisContext` returns MethodContext and `removeSelf` works.

### Workaround Applied (Band-aid)

**File**: `VirtualMachine.MessageSending.cs` lines 254-264

```csharp
// SPECIAL CASE: BlockContext>>removeSelf forwards to home MethodContext
if (selectorName == "removeSelf" && classOOP == _metadata.BlockContextClassOOP)
{
    var homeOOP = _heapManager.GetInstanceVariable(receiverOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
    return SendMessage(homeOOP, selectorOOP, args);
}
```

This forwards `removeSelf` from BlockContext to home MethodContext at runtime. It's a workaround, not a fix.

---

## Proper Fix Required

### Option 1: Enable Compiler Optimization (Recommended)

Re-enable `TryInlineConditional` in `BytecodeCompiler.cs` to generate:
- Conditional jump bytecodes (152-159, 168-175)
- Block body compiled inline
- No `blockCopy:` bytecode
- No BlockContext created

**Benefits**:
- Blue Book compliance
- Better performance
- All classic Smalltalk-80 code works correctly

### Option 2: Additional VM Workarounds (Not Recommended)

Continue adding special cases in VM for messages that assume `thisContext` returns MethodContext. This is fragile and non-compliant.

---

## What Has Been Completed

### Process Primitives (85-88) ✅
- Primitive 85: Semaphore wait
- Primitive 86: Semaphore signal
- Primitive 87: Process resume
- Primitive 88: Process suspend

### MVC Startup Sequence ✅
1. `Processor hiddenBackgroundProcess` - Creates idle process at priority 1
2. `ControlManager new` - Creates controller manager
3. `ScheduledControllers` global installed using canonical Association from AssociationRegistry

### VM Changes ✅
- `VirtualMachine.cs` - HandleReturn logic distinguishes initial process vs forked process termination
- `VMConstants.cs` - Added LinkedList/Link constants for process queues
- `SmalltalkInterpreterThread.cs` - `StartControllerLoop()` and `InstallScheduledControllersGlobal()` methods

---

## Key Files

### C# Interpreter
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` - HandleReturn, process switching
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.MessageSending.cs` - removeSelf workaround
- `Smalltalk80.Interpreter/SmalltalkInterpreterThread.cs` - Controller loop startup

### C# Compiler (needs fix)
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - `TryInlineConditional()` disabled

### Smalltalk Source
- `Process>>terminate` (line 32612) - Uses `thisContext removeSelf`
- `MethodContext>>removeSelf` (line 26493) - Nils receiver, returns old value

---

## Blue Book References

### Chapter 28: Formal Specification of the Interpreter

**Conditional Jump Bytecodes** [Page 595]:
```
152-159  Pop and Jump On False (short form)
168-171  Pop and Jump On True (long form)
172-175  Pop and Jump On False (long form)
```

**Line 413**:
> "The conditional jumps are used in the compiled form of messages to booleans (e.g., `ifTrue:` and `whileFalse:`)."

---

## Test Commands

```bash
# Build and run
cd Smalltalk80.Interpreter
dotnet build
dotnet run

# Run BlockContext regression test (MUST PASS after any compiler changes)
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)
```

---

## Verification Test

To check if optimization is working, this Smalltalk code:
```smalltalk
testMethod
    true ifTrue: [^thisContext class name].
    ^nil
```

Should return:
- **With optimization (correct)**: `'MethodContext'`
- **Without optimization (current)**: `'BlockContext'`

---

## Model Selection

Always use model: **claude-sonnet-4-5**
