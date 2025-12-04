# Method Lookup Bug Investigation

**Date**: 2025-12-04
**Status**: Active Investigation
**Severity**: CRITICAL - System non-functional

---

## Problem Statement

After implementing Phase 7 of the Event Architecture Refactor, the interpreter crashes immediately when the mouse moves with the following error:

```
Instance variable index 4 out of range [0, 0] for object oop=67886 (InputSensor)
at Smalltalk80.Interpreter.HeapManager.SmalltalkHeapManager.GetInstanceVariable(SmalltalkOOP objectOOP, Int32 index)
at Smalltalk80.Interpreter.Core.VM.BytecodeInterpreter.ExecuteBytecode(Byte bytecode, SmalltalkOOP methodOOP)
```

**Location**: BytecodeInterpreter.cs:84 when executing bytecode 0x04 (push receiver instance variable at index 4)

---

## Investigation Timeline

### Step 1: Verified Object Structure (✅ CORRECT)

**InputSensor class definition** (from coldstart.json):
```json
{
  "localId": "class:InputSensor",
  "totalInstanceVarCount": 1,
  "instVarNames": [2437],
  "fieldLayout": [
    {"nameIndex": 2437, "slotIndex": 0}
  ]
}
```

**Smalltalk source**:
```smalltalk
Object subclass: #InputSensor
    instanceVariableNames: 'keyboardMap '
```

**Conclusion**: InputSensor correctly has 1 instance variable at index 0.

**Error message analysis**:
- Range `[0, 0]` means valid indices are 0 to 0 (inclusive), i.e., 1 instance variable
- Object oop=67886 is correctly sized as an InputSensor
- Attempting to access index 4 on an object with only index 0 valid = crash

### Step 2: Identified Source of Bytecode 0x04 (✅ FOUND)

**Analysis**: Searched all methods in coldstart.json for bytecode 0x04.

**Result**: NO InputSensor method contains bytecode 0x04.

**InputState class definition**:
```json
{
  "localId": "class:InputState",
  "totalInstanceVarCount": 12,
  "instVarNames": [5264, 5288, 564, 2652, 4056, 1156, 2628, 2780, 2441, 1302, 507, 4895]
}
```

**Field layout**:
- Index 0: x
- Index 1: y
- Index 2: bitState
- Index 3: lshiftState
- Index 4: **rshiftState** ← This is what bytecode 0x04 accesses
- Index 5-11: other variables

**InputState>>keyAt:put: method**:
```smalltalk
keyAt: index put: value
    | mask |
    index < 8r200
      ifTrue:  "Not a potential special character"
        [...]
      ifFalse:
        [index = LshiftKey
          ifTrue: [lshiftState _ value]
          ifFalse:
            [index = RshiftKey
              ifTrue: [rshiftState _ value]  ← Accesses ivar[4]
              ifFalse:
                [...]]]].
    metaState _ (ctrlState bitOr: (lshiftState bitOr: rshiftState)) bitOr: lockState]
```

**Conclusion**: Bytecode 0x04 belongs to InputState>>keyAt:put: which legitimately accesses rshiftState at index 4.

### Step 3: Checked for Selector Name Collision (✅ NO COLLISION)

**Analysis**: Compared all method selectors between InputSensor and InputState classes.

**Result**: Zero common selectors. These are completely separate classes with no overlapping method names.

### Step 4: Examined Delegation Pattern (✅ INTENTIONAL DESIGN)

**Architecture**:
```smalltalk
InputSensor class
    classVariableNames: 'CurrentCursor CurrentInputState DefaultKeyboardMap '

InputSensor>>buttons
    "Return button state from processed events (bitState), not polled current state."
    ^CurrentInputState mouseButtons!

InputSensor>>primMousePt
    "Poll the mouse to find out its position."
    <primitive: 90>
    ^CurrentInputState mousePoint!

InputSensor class>>install
    "Create an instance of InputState for accessing the hardware."
    CurrentInputState _ InputState new.
    CurrentInputState install!
```

**Pattern**: InputSensor delegates to CurrentInputState (an InputState instance stored in a class variable). This is intentional and correct.

### Step 5: Formulated Hypothesis (🔍 ACTIVE)

**The Bug**: Method lookup is returning the wrong method.

**Scenario**:
1. Message sent to `Sensor` (oop=67886, an InputSensor instance)
2. VM performs method lookup for the selector
3. **BUG**: Lookup returns `InputState>>keyAt:put:` instead of an InputSensor method
4. VM creates MethodContext with receiver=oop=67886 (InputSensor)
5. VM begins executing InputState>>keyAt:put: bytecode
6. Bytecode 0x04 tries to access receiver.ivar[4]
7. **CRASH**: InputSensor only has ivar[0], not ivar[4]

**Possible Root Causes**:
1. **Method dictionary lookup error**:
   - Looking up method in wrong class's method dictionary
   - Class OOP confusion between InputSensor and InputState

2. **Super send logic error**:
   - Incorrect superclass traversal during lookup
   - Class hierarchy corruption

3. **Context receiver slot corruption**:
   - Message send sets receiver incorrectly in MethodContext
   - Receiver OOP overwritten during context creation

4. **Class table corruption**:
   - Class OOP → Class structure mapping is wrong
   - InputSensor class OOP pointing to InputState class structure

---

## Debug Strategy

### Diagnostic Logging Added

**File**: `BytecodeInterpreter.cs:83-91`

```csharp
// DEBUG: Log when accessing ivar[4] to diagnose crash
if (index == 4)
{
    var receiverClass = heapManager.GetClassOOP(receiverOOP);
    var receiverClassName = heapManager.GetClassName(receiverOOP) ?? "Unknown";
    Console.WriteLine($"[IVAR4] Accessing ivar[4] in {receiverClassName}");
    Console.WriteLine($"[IVAR4] Receiver: {receiverOOP}, ReceiverClass: {receiverClass}");
    Console.WriteLine($"[IVAR4] Method OOP: {methodOOP}");
}
```

**Expected Output When Crash Occurs**:
```
[IVAR4] Accessing ivar[4] in InputSensor
[IVAR4] Receiver: oop=67886, ReceiverClass: oop=XXXXX
[IVAR4] Method OOP: oop=YYYYY
Fatal error: Instance variable index 4 out of range...
```

### Next Investigation Steps

1. **Capture Method OOP**:
   - Run interpreter with debug logging
   - Move mouse to trigger crash
   - Record Method OOP from output

2. **Verify Method Identity**:
   - Look up Method OOP in coldstart.json
   - Confirm it's InputState>>keyAt:put: (not an InputSensor method)

3. **Trace Message Send**:
   - Add logging to message send code (VirtualMachine.MessageSending.cs)
   - Log: selector, receiver OOP, receiver class, method found, method's class
   - Identify exact point where wrong method is selected

4. **Examine Method Lookup Code**:
   - Review `LookupMethodInClass()` implementation
   - Check method dictionary traversal
   - Verify class inheritance chain traversal
   - Look for off-by-one errors in OOP calculations

5. **Possible Quick Fixes to Test**:
   - Verify class OOP integrity: Does InputSensor class OOP point to correct class structure?
   - Check method dictionary format: Are method dictionaries correctly structured?
   - Validate class hierarchy: Is Object → InputSensor chain intact?

---

## Files Modified During Investigation

- `BytecodeInterpreter.cs`: Added debug logging at line 83-91
- `EVENT-ARCHITECTURE-REFACTOR-PLAN.md`: Updated status to BLOCKED

---

## Test Case for Reproduction

**Minimal Steps**:
1. Build interpreter with debug logging
2. Run: `cd Smalltalk80.Interpreter && dotnet run`
3. Wait ~45 seconds for system initialization
4. Move mouse cursor over window
5. **Expected**: Crash with [IVAR4] debug output showing Method OOP

**Observed During Testing**:
- System initializes successfully
- [IVAR4] logs appear during initialization for TextStyle, Paragraph, CompositionScanner (all valid)
- Crash occurs on first mouse movement (timing varies 1-5 seconds after window appears)

---

## Related Code References

- **Method Lookup**: [VirtualMachine.MessageSending.cs:150-250](../Smalltalk80.Interpreter/Core/VM/VirtualMachine.MessageSending.cs#L150-L250)
- **Bytecode Execution**: [BytecodeInterpreter.cs:78-96](../Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs#L78-L96)
- **Class Structure**: [Phase5Loader.cs:200-300](../Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5Loader.cs#L200-L300)
- **Input Architecture**: [InputState>>run](../Smalltalk-80%20Source/Smalltalk-80.st#L22800-L22850)

---

## Summary for Next Session

**Current State**:
- Phases 1-7 of Event Architecture Refactor implemented
- System crashes on mouse movement due to method lookup bug
- Debug logging in place to capture method OOP at crash point

**Priority**: CRITICAL - Must fix before proceeding with Phases 8-10

**Next Action**:
1. Run interpreter to capture Method OOP from crash
2. Look up Method OOP in JSON to confirm hypothesis
3. Add message send logging to trace lookup path
4. Fix method lookup bug based on findings
