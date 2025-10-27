# Bug Report: Instance Variable Access on nil During Init Expression Execution

**Date**: 2025-01-27
**Status**: Under Investigation
**Severity**: High - Blocks init expression execution

## Executive Summary

After successfully implementing non-local returns and fixing the bytecode disassembler, the interpreter encounters runtime errors when executing init expression #0. The error manifests as attempts to access instance variable index 0 on the `nil` object (oop=9112), which has no instance variables (range [0, -1]).

## Error Symptoms

### Primary Error Message
```
Exception: Instance variable index 0 out of range [0, -1] for object oop=9112 (Parameter 'index')
```

### Error Locations
Multiple methods fail with the same error pattern:
1. **False>>ifTrue:ifFalse:** - Attempting to push temp[1] which contains nil
2. **String>>replaceFrom:to:with:startingAt:** - Instance variable access on nil
3. **Symbol>>copyReplaceFrom:to:with:** - Instance variable access on nil
4. **Symbol>>,** - Instance variable access on nil

### Execution Context When Error Occurs

**Init Expression #0 Call Chain:**
```
ActionMenu class>>comment: (context=58328)
  → ClassDescription>>comment: (context=58332)
    → False>>ifTrue:ifFalse: (context=58338) ✓ Works
      → BlockClosure>>value (primitive 207) (context=58342)
        → WriteStream class>>on: (context=58346)
          → WriteStream>>on: (context=58350)
            → Symbol>>, (context=58380)
              → SequenceableCollection>>copyReplaceFrom:to:with: (context=58382)
                → String>>replaceFrom:to:with:startingAt: (context=58388)
                  → False>>ifTrue:ifFalse: (context=58468) ✓ Works
                    → BlockClosure>>value (primitive 207) (context=58472)
                      → SEND_SUPER_N to super.replaceFrom:to:with:startingAt:
                        → False>>ifTrue:ifFalse: ❌ FAILS - temp[1] is nil
```

## Technical Details

### The Failing SEND_SUPER_N Instruction

**Context**: oop=58472 (BlockClosure execution context)
**BlockClosure**: oop=58466
**Home Context**: oop=58388 (String>>replaceFrom:to:with:startingAt:)

**Bytecode Sequence:**
```
ip=0: PUSH_SELF (0x70)           → pushes self (the receiver)
ip=1: PUSH_TEMP (0x10)           → pushes temp[0] = SmallInteger(11)
ip=2: PUSH_TEMP (0x11)           → pushes temp[1] = SmallInteger(10)
ip=3: PUSH_TEMP (0x12)           → pushes temp[2] = Symbol(#ActionMenu)
ip=4: PUSH_TEMP (0x13)           → pushes temp[3] = SmallInteger(1)
ip=5: SEND_SUPER_N (0x80 0x11)   → sends super.replaceFrom:to:with:startingAt: with 4 args
```

**Stack Before SEND_SUPER_N:**
```
[bottom]
  self (receiver for super send)
  arg0 = SmallInteger(11)
  arg1 = SmallInteger(10)
  arg2 = Symbol(#ActionMenu)
  arg3 = SmallInteger(1)
[top]
```

**Debug Output Confirms:**
- All 5 values are pushed successfully
- All temps are correctly read from home context (blockLocalCount=0)
- SEND_SUPER_N executes and looks up method in superclass

**What Happens Next:**
- `SendSuperMessage()` is called with receiver + 4 arguments
- Method lookup finds `False>>ifTrue:ifFalse:` (somehow?)
- New MethodContext is created for `False>>ifTrue:ifFalse:`
- **Problem**: This context has temp[1] = nil instead of a BlockClosure
- When execution starts, bytecode `PUSH_TEMP 0x11` tries to push temp[1]
- Accessing instance variable 0 on nil (trying to get class?) causes error

### Key Observations

1. **Method Lookup Confusion**: SEND_SUPER_N at ip=5 should send `replaceFrom:to:with:startingAt:` to a superclass, but somehow execution ends up in `False>>ifTrue:ifFalse:` which is completely unrelated!

2. **Argument Count Mismatch**:
   - SEND_SUPER_N pushes 5 values (receiver + 4 args)
   - But ends up calling `False>>ifTrue:ifFalse:` which expects 2 args (2 BlockClosures)

3. **BlockClosure Execution**: The failing code is inside a BlockClosure (oop=58466) being executed via primitive 207

4. **Super Send Context**: The super send is happening from within a block, not from a direct method

## Files Modified Recently

### Working Files (Successfully Fixed)
1. **Smalltalk80.Interpreter/Core/VM/BytecodeDisassembler.cs**
   - Added `GetExtendedSendString()` method (lines 263-272)
   - Added `GetExtendedSendNString()` method (lines 274-284)
   - Updated `DisassembleExtendedPrefix()` to handle all SEND variants (lines 229-247)
   - **Status**: ✅ Working correctly - disassembler now properly shows SEND_N and SEND_SUPER_N instructions

2. **Smalltalk80.Interpreter/Core/VM/NonLocalReturnException.cs**
   - Created exception class for non-local returns
   - **Status**: ✅ Working correctly - NLRs function properly

3. **Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs**
   - Implemented non-local return support in BLOCK_RETURN_TOP (0x7E) and RETURN_SELF_POP (0x7D)
   - Added catch blocks for NonLocalReturnException in SendMessage methods
   - **Status**: ✅ Working correctly - NLRs unwind stack properly

### Potentially Problematic Files

1. **Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs**
   - **ExecuteExtendedBytecode()** - Case 0x11 (SEND_SUPER_N) at lines 1345-1361
   - **SendSuperMessage()** - Lines 1647-1727
   - **CreateMethodContext()** - Lines 241-318
   - **pushTemp implementation** - Lines 442-493

## Relevant Code Sections

### SEND_SUPER_N Implementation (VirtualMachine.cs:1345-1361)
```csharp
case 0x11: // SEND_SUPER_N - Send to super with N args (N >= 3)
    {
        byte selectorIndex = bytecodes[ip++];
        byte argCount = bytecodes[ip++];
        _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex, SmalltalkOOP.FromSmallInteger(ip));
        var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1); // +1 to skip header

        var args = new SmalltalkOOP[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
        }
        var receiver = Pop();
        var outcome = SendSuperMessage(receiver, selectorOOP, args);
        if (!ConsumeCallOutcome(outcome)) return true;
        return true;
    }
```

**Potential Issues:**
- IP update happens before reading selector/argCount bytes
- Selector index calculation: `selectorIndex + 1` to skip header
- Argument popping order: argN-1, argN-2, ..., arg0, then receiver

### SendSuperMessage Implementation (VirtualMachine.cs:1647-1727)
```csharp
private CallOutcome SendSuperMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
{
    // Gets current method's home class
    var currentMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
    var homeClassOOP = _heapManager.GetInstanceVariable(currentMethodOOP, 0);

    // Gets superclass of home class
    var superclassOOP = _heapManager.GetInstanceVariable(homeClassOOP, superclassIndex);

    // Looks up method in superclass
    var methodInfo = GetMethod(superclassOOP, selectorName!);

    // Creates new context and executes
    var previousContextOOP = _activeContextOOP;
    _activeContextOOP = CreateMethodContext(methodInfo.MethodOOP, receiverOOP, args, previousContextOOP);

    // ... executes bytecode loop ...
}
```

**Potential Issues:**
- When called from within a BlockClosure, `_activeContextOOP` is the block execution context, not the method context
- Getting `currentMethodOOP` from block execution context might return the block's method, not the defining method
- The `homeClassOOP` might be wrong if block's method doesn't have correct homeClass

### CreateMethodContext - Argument Storage (VirtualMachine.cs:296-299)
```csharp
// Copy arguments into arg slots
for (int i = 0; i < header.NumArgs; i++)
{
    _heapManager.SetIndexedOOP(contextOOP, i, arguments[i]);
}
```

**Potential Issues:**
- If `arguments.Length < header.NumArgs`, this would access out of bounds
- If `header.NumArgs` is wrong, arguments won't be stored correctly

## Smalltalk Source Code Context

### Symbol>>, Method
**Location**: Smalltalk-80 Source/Smalltalk-80.st (around line 34380)
**Class**: SequenceableCollection (inherited by Symbol via String → ArrayedCollection)

The `,` (comma) method concatenates two sequenceable collections by calling:
```smalltalk
self copyReplaceFrom: (self size + 1) to: self size with: aSequenceableCollection
```

### SequenceableCollection>>copyReplaceFrom:to:with:
**Location**: Smalltalk-80 Source/Smalltalk-80.st:34363-34392

This method creates a new collection using `replaceFrom:to:with:startingAt:`:
```smalltalk
copyReplaceFrom: start to: stop with: replacementCollection
    | newSequenceableCollection newSize endReplacement |
    newSize _ self size - (stop - start + 1) + replacementCollection size.
    endReplacement _ start - 1 + replacementCollection size.
    newSequenceableCollection _ self species new: newSize.
    newSequenceableCollection
        replaceFrom: 1
        to: start - 1
        with: self
        startingAt: 1.
    newSequenceableCollection
        replaceFrom: start
        to: endReplacement
        with: replacementCollection
        startingAt: 1.
    newSequenceableCollection
        replaceFrom: endReplacement + 1
        to: newSize
        with: self
        startingAt: stop + 1.
    ^newSequenceableCollection
```

**Note**: There's a DIFFERENT version in OrderedCollection (line 27872) that uses `add:` instead of `replaceFrom:to:with:startingAt:`. String/Symbol should use the SequenceableCollection version.

### String>>replaceFrom:to:with:startingAt:
This is where the BlockClosure with SEND_SUPER_N is being executed. It likely contains validation blocks that send `ifTrue:ifFalse:` to check preconditions.

## Debugging Already Performed

### 1. Bytecode Disassembler Fixed ✅
- Added support for SEND_0, SEND_1, SEND_2, SEND_N (extended 0x0A-0x0D)
- Added support for SEND_SUPER_0, SEND_SUPER_1, SEND_SUPER_2, SEND_SUPER_N (extended 0x0E-0x11)
- Confirmed bytecode streams are being disassembled correctly
- Confirmed SEND_N consumes 4 bytes: 0x80, 0x0D, selectorIndex, argCount

### 2. Execution Trace Analysis
- Verified non-local returns work correctly (multiple successful NLRs in trace)
- Verified BlockClosure primitive 207 works correctly (multiple successful executions)
- Verified pushTemp correctly reads from home context when blockLocalCount=0
- Verified all 5 values are pushed onto stack before SEND_SUPER_N
- Confirmed temps are correctly retrieved from home context (SmallInteger values, not nil)

### 3. Stack State Verification
```
Before SEND_SUPER_N execution:
  Stack[top-4] = receiver (self)
  Stack[top-3] = SmallInteger(11)  ← temp[0] from home context
  Stack[top-2] = SmallInteger(10)  ← temp[1] from home context
  Stack[top-1] = Symbol(#ActionMenu) ← temp[2] from home context
  Stack[top-0] = SmallInteger(1)   ← temp[3] from home context
```

All values confirmed non-nil before SEND_SUPER_N executes.

### 4. Context Mapping Verification
- BlockClosure oop=58466 is correctly mapped to execution context oop=58472
- Home context oop=58388 is correctly retrieved
- Temps are correctly read from home context, not block context

## Hypotheses

### Hypothesis 1: Method Lookup Returns Wrong Method ❗ MOST LIKELY
When `SendSuperMessage()` is called from within a BlockClosure:
1. `_activeContextOOP` = 58472 (block execution context)
2. Gets method from context: might return block's compiled method, not the defining method
3. Gets homeClass from method: might be wrong class
4. Gets superclass: might be wrong superclass
5. Method lookup in wrong superclass finds unrelated method

**Test**: Check what `currentMethodOOP` and `homeClassOOP` are when SendSuperMessage is called from context 58472.

### Hypothesis 2: Selector OOP is Wrong
The selector being looked up might be corrupted or incorrect:
- `selectorIndex` from bytecode might be wrong
- `GetMethodLiteral(methodOOP, selectorIndex + 1)` might return wrong selector
- If getting literal from block's method instead of home method, selector would be wrong

**Test**: Add debug output to show exact selector OOP and string being looked up.

### Hypothesis 3: Argument Array Construction Error
The arguments array passed to `SendSuperMessage()` might be corrupted:
- Stack popping might pop wrong values
- Array might have wrong length
- Values might be overwritten after popping

**Test**: Add debug output in SEND_SUPER_N to print all popped values.

### Hypothesis 4: Context Creation Receives Wrong Arguments
`CreateMethodContext()` might receive correct arguments but store them incorrectly:
- `header.NumArgs` might be wrong (expects 2, gets 4)
- Loop only stores first `header.NumArgs` arguments
- Remaining arguments are ignored, temps stay nil

**Test**: Add debug output in CreateMethodContext to show header.NumArgs vs arguments.Length.

## Recommended Investigation Steps

### Step 1: Add Debug Output to SEND_SUPER_N
```csharp
case 0x11: // SEND_SUPER_N
    {
        byte selectorIndex = bytecodes[ip++];
        byte argCount = bytecodes[ip++];

        Console.WriteLine($"[SEND_SUPER_N] selectorIndex={selectorIndex}, argCount={argCount}");

        var selectorOOP = GetMethodLiteral(methodOOP, selectorIndex + 1);
        Console.WriteLine($"[SEND_SUPER_N] selector={GetSymbolName(selectorOOP)}");

        var args = new SmalltalkOOP[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            args[i] = Pop();
            Console.WriteLine($"[SEND_SUPER_N] args[{i}] = {args[i]}, class={GetClassName(GetClass(args[i]))}");
        }
        var receiver = Pop();
        Console.WriteLine($"[SEND_SUPER_N] receiver = {receiver}, class={GetClassName(GetClass(receiver))}");

        // Continue with SendSuperMessage...
    }
```

### Step 2: Add Debug Output to SendSuperMessage
```csharp
private CallOutcome SendSuperMessage(SmalltalkOOP receiverOOP, SmalltalkOOP selectorOOP, SmalltalkOOP[] args)
{
    Console.WriteLine($"[SendSuperMessage] ENTRY: receiver={receiverOOP}, selector={GetSymbolName(selectorOOP)}, args.Length={args.Length}");

    // Get current method
    var currentMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
    Console.WriteLine($"[SendSuperMessage] currentMethodOOP={currentMethodOOP}");

    // Get home class
    var homeClassOOP = _heapManager.GetInstanceVariable(currentMethodOOP, 0);
    Console.WriteLine($"[SendSuperMessage] homeClassOOP={homeClassOOP}, className={GetClassName(homeClassOOP)}");

    // Get superclass
    var superclassOOP = _heapManager.GetInstanceVariable(homeClassOOP, superclassIndex);
    Console.WriteLine($"[SendSuperMessage] superclassOOP={superclassOOP}, superclassName={GetClassName(superclassOOP)}");

    // Method lookup
    var methodInfo = GetMethod(superclassOOP, selectorName!);
    Console.WriteLine($"[SendSuperMessage] Found method: {methodInfo.MethodOOP} in class {methodInfo.DefiningClassName}");

    int primitiveNumber = GetPrimitiveNumber(methodInfo.MethodOOP);
    Console.WriteLine($"[SendSuperMessage] Primitive number: {primitiveNumber}");

    // ... rest of method ...
}
```

### Step 3: Add Debug Output to CreateMethodContext
```csharp
private SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
{
    var header = ParseMethodHeader(methodOOP);

    Console.WriteLine($"[CreateMethodContext] methodOOP={methodOOP}, receiverOOP={receiverOOP}");
    Console.WriteLine($"[CreateMethodContext] header.NumArgs={header.NumArgs}, arguments.Length={arguments.Length}");
    Console.WriteLine($"[CreateMethodContext] header.NumTemps={header.NumTemps}");

    for (int i = 0; i < arguments.Length; i++)
    {
        Console.WriteLine($"[CreateMethodContext] arguments[{i}] = {arguments[i]}, class={GetClassName(GetClass(arguments[i]))}");
    }

    // ... rest of method ...

    // After storing arguments
    for (int i = 0; i < header.NumArgs; i++)
    {
        var stored = _heapManager.GetIndexedOOP(contextOOP, i);
        Console.WriteLine($"[CreateMethodContext] Verified: indexed[{i}] = {stored}");
    }
}
```

### Step 4: Check Block's CompiledMethod Structure
Verify that blocks have the correct homeClass reference:
```csharp
// When creating BlockClosure, check its method's homeClass
int methodIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "method");
var blockMethodOOP = _heapManager.GetInstanceVariable(blockClosureOOP, methodIndex);
var blockHomeClassOOP = _heapManager.GetInstanceVariable(blockMethodOOP, 0);
Console.WriteLine($"[Block Method] homeClass={GetClassName(blockHomeClassOOP)}");
```

## Expected Behavior

For the failing SEND_SUPER_N at context=58472, ip=5:
1. Should read selector index and arg count from bytecode
2. Should pop 4 arguments + receiver from stack (all non-nil SmallIntegers and Symbol)
3. Should get current method from context 58472 (should be the block's compiled method)
4. Should get homeClass from that method (should be String or a superclass)
5. Should get superclass of homeClass
6. Should look up `replaceFrom:to:with:startingAt:` in that superclass
7. Should create MethodContext with 4 arguments correctly stored
8. Should execute the found method

**What actually happens**: Somehow ends up calling `False>>ifTrue:ifFalse:` instead!

## Key Questions to Answer

1. **What selector is actually being looked up?**
   - Is selectorIndex correct?
   - Is GetMethodLiteral returning the right selector?
   - Is the selector `replaceFrom:to:with:startingAt:` or something else?

2. **What method is actually being found?**
   - What class does method lookup search?
   - What method OOP is returned?
   - Is it really `False>>ifTrue:ifFalse:` or is the error dump misleading?

3. **What are the actual argument values passed to CreateMethodContext?**
   - Are all 4 arguments present?
   - Are they the correct SmallInteger/Symbol values?
   - Or have they been corrupted?

4. **What does the method header say?**
   - Does the found method expect 2 args (ifTrue:ifFalse:) or 4 args (replaceFrom:to:with:startingAt:)?
   - Is NumArgs=2 when it should be 4?

5. **Is the block's CompiledMethod properly formed?**
   - Does it have the correct homeClass in slot 0?
   - Does it have the correct literals?
   - Is it actually a block method or something else?

## Related Documentation

- **CLAUDE.md** - Contains architectural principles including "Runtime Index Resolution" warning
- **docs/BlockClosure-Phase2-Complete.md** - BlockClosure implementation details
- **docs/Interpreter-Architecture.md** - Sections 10 & 11 on BlockClosure support
- **Smalltalk-80 Source/Smalltalk-80.st** - Complete Smalltalk-80 source code

## Build and Run Instructions

```bash
# Build interpreter
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter"
dotnet build

# Run interpreter (will fail with error)
cd "d:\Users\brent\OneDrive\Src\Smalltalk-80\bin\Debug\net9.0"
pwsh -Command ".\Smalltalk80.Interpreter.exe 2>&1 | Select-Object -First 500"

# Search for specific error
pwsh -Command ".\Smalltalk80.Interpreter.exe 2>&1 | Select-String -Pattern 'ERROR IN: False>>ifTrue:ifFalse' -Context 5,20"
```

## Summary

The bug appears to be a **method lookup error** where SEND_SUPER_N, when executed from within a BlockClosure, somehow resolves to the wrong method (`False>>ifTrue:ifFalse:` instead of `replaceFrom:to:with:startingAt:`). This causes a method expecting 2 arguments to receive 4 arguments (or receive the wrong arguments), resulting in nil values in temporary slots that should contain BlockClosures.

The most likely root cause is that `SendSuperMessage()` is using the wrong context to determine the home class for super sends when called from within a block, leading to method lookup in the wrong class hierarchy.
