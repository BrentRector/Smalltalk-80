# Block Context and Variable Access - Comprehensive Reference

**Last Updated**: 2025-11-13
**Status**: CANONICAL REFERENCE
**Supersedes**: All previous block and variable access documentation

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Core Architecture: Unified Temp Frame](#2-core-architecture-unified-temp-frame)
3. [Data Structures](#3-data-structures)
4. [Compilation Phase](#4-compilation-phase)
5. [Runtime Phase](#5-runtime-phase)
6. [Variable Access Implementation](#6-variable-access-implementation)
7. [Block Primitives](#7-block-primitives)
8. [Complete Examples](#8-complete-examples)
9. [Common Pitfalls](#9-common-pitfalls)
10. [Implementation Checklist](#10-implementation-checklist)

---

## 1. Executive Summary

### 1.1 Core Principle

**All method and block variables (arguments and temporaries) are stored in the MethodContext's unified temp frame using absolute indices assigned at compile time.**

BlockContext contains:
- **Instance variables**: sender, pc, stackp, argumentCount, initialIP, home
- **NO indexed slots** for variable storage
- Only a `home` pointer to the MethodContext

### 1.2 Key Design Points

1. **Single Storage Location**: MethodContext indexed slots hold ALL variables
2. **Absolute Indexing**: Compiler assigns unique absolute index (0, 1, 2...) to each variable
3. **Uniform Access**: All variable access uses `PUSH_TEMP index` / `STORE_TEMP index` bytecodes
4. **Block Closure**: BlockContext's `home` pointer enables lexical scoping
5. **Context Retention**: MethodContext retained if any blocks reference it

### 1.3 What This Document Covers

This document consolidates information from:
- Unified-Temp-Frame-Specification.md (authoritative architecture)
- Variable-Access-Implementation-Guide.md (implementation details)
- BlueBook-Context-Architecture.md (Blue Book context structures)
- Compiler-Block-Allocation-Strategy.md (compiler strategy)
- Variable-Indexing-Architecture.md (indexing details)
- CRITICAL-Block-Compilation-Discovery.md (historical evolution)

---

## 2. Core Architecture: Unified Temp Frame

### 2.1 The Unified Temp Frame Model

All temporary variables across a method and all its blocks share a single contiguous index space in the MethodContext.

**Conceptual Model**:
```
Source Code:
  method: x
    | temp |
    temp := 1.
    [:a |
      | btemp |
      [:b | a + b + temp] value: 3
    ] value: 2

Unified Temp Frame:
  Slot 0: x (method arg)
  Slot 1: temp (method temp)
  Slot 2: a (outer block arg)
  Slot 3: btemp (outer block temp)
  Slot 4: b (inner block arg)

ALL stored in MethodContext indexed slots [0-4]
```

### 2.2 Why Unified Frame?

**Benefits**:
1. **Simplicity**: Single storage location, single access path
2. **Blue Book Compliance**: Matches Smalltalk-80 specification
3. **Lexical Closure**: Variables naturally captured by home pointer
4. **Performance**: Direct indexed access, no runtime routing
5. **Correctness**: Nested blocks work naturally

**What Changed from Earlier Designs**:
- ❌ OLD: Block arguments in BlockContext indexed slots + MethodContext temp frame (dual storage)
- ✅ NEW: All variables in MethodContext temp frame only (unified storage)
- ❌ OLD: Dual routing with `if (index < nargs)` checks
- ✅ NEW: Single path - always traverse to MethodContext via home pointer

---

## 3. Data Structures

### 3.1 MethodContext Structure

```
MethodContext (subclass of Context)
  Instance Variables (Fixed Slots):
    [0] sender          - calling context (for return)
    [1] instructionPointer - program counter
    [2] stackPointer    - evaluation stack pointer
    [3] method          - CompiledMethod object
    [4] receiver        - self

  Indexed Slots (Unified Temp Frame + Stack):
    [0..n-1]           - Method args, method temps, ALL block args/temps
    [n..m]             - Expression stack (grows during execution)
```

**Size Calculation**:
```
totalSize = maxTempFrameSize + stackDepth
  where maxTempFrameSize = compiler-determined maximum simultaneous variable need
        stackDepth = 16 (small frame) or 48 (large frame)
```

### 3.2 BlockContext Structure

```
BlockContext (subclass of MethodContext)
  Instance Variables (Fixed Slots):
    [0] sender          - calling context (for return)
    [1] instructionPointer - program counter
    [2] stackPointer    - evaluation stack pointer
    [3] argumentCount   - number of block parameters
    [4] initialIP       - starting bytecode offset (for re-execution)
    [5] home            - MethodContext where block was defined

  Indexed Slots:
    NONE - BlockContext has NO indexed slots for variables
    All variable access goes through home → MethodContext
```

**CRITICAL**: BlockContext does NOT store any variables. It only provides execution state and the `home` pointer.

### 3.3 Memory Layout Diagram

```
┌──────────────────────────────────┐
│ MethodContext                    │
├──────────────────────────────────┤
│ Instance Variables:              │
│  [0] sender                      │
│  [1] pc                          │
│  [2] stackp                      │
│  [3] method                      │
│  [4] receiver                    │
├──────────────────────────────────┤
│ Indexed Slots (Unified Frame):  │
│  [0] method arg 0                │
│  [1] method temp 0               │
│  [2] block 1 arg 0               │◄─── ALL variables here
│  [3] block 1 temp 0              │
│  [4] block 2 arg 0               │
│  ...                             │
│  [n] expression stack            │
└──────────────────────────────────┘

┌──────────────────────────────────┐
│ BlockContext                     │
├──────────────────────────────────┤
│ Instance Variables:              │
│  [0] sender                      │
│  [1] pc                          │
│  [2] stackp                      │
│  [3] argumentCount               │
│  [4] initialIP                   │
│  [5] home ──────────────────────►│ (points to MethodContext above)
├──────────────────────────────────┤
│ Indexed Slots:                   │
│  (NONE - no variable storage)    │
└──────────────────────────────────┘
```

---

## 4. Compilation Phase

### 4.1 TempFrameAllocator: Variable Index Assignment

The compiler's TempFrameAllocator assigns absolute indices to all variables.

**Algorithm**:
```csharp
class TempFrameAllocator
{
    private int _nextSlot = 0;
    private int _maxSimultaneous = 0;
    private Dictionary<string, int> _allocatedSlots = new();
    private Stack<ScopeInfo> _scopeStack = new();

    // Step 1: Enter method scope
    public void EnterMethodScope(List<string> argNames, List<string> tempNames)
    {
        foreach (var argName in argNames)
        {
            _allocatedSlots[argName] = _nextSlot++;
        }
        foreach (var tempName in tempNames)
        {
            _allocatedSlots[tempName] = _nextSlot++;
        }
        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    // Step 2: Enter block scope
    public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
    {
        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = !isNested  // Can reuse slots?
        };

        // Allocate block args AND temps in unified frame
        foreach (var argName in argNames)
        {
            _allocatedSlots[argName] = _nextSlot++;
        }
        foreach (var tempName in tempNames)
        {
            _allocatedSlots[tempName] = _nextSlot++;
        }
        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    // Step 3: Exit scope (potentially reuse slots)
    public void ExitScope()
    {
        var scope = _scopeStack.Pop();
        if (scope.IsSequential)
        {
            // Sequential blocks can reuse slots
            _nextSlot = scope.StartSlot;
            // Remove variables from map...
        }
    }

    // Step 4: Get index for variable
    public int GetSlotIndex(string varName)
    {
        return _allocatedSlots[varName];
    }

    // Step 5: Get max frame size for method header
    public int GetMaxFrameSize()
    {
        return _maxSimultaneous;
    }
}
```

### 4.2 Sequential Block Slot Reuse

**Optimization**: Sequential (non-nested) blocks can reuse temp slots.

**Example**:
```smalltalk
method
    [:a | a + 1] value: 1.   "a → slot 0"
    [:b | b + 2] value: 2.   "b → slot 0 (REUSED!)"
```

After first block exits:
- Slot 0 freed
- `_nextSlot` reset to 0
- Second block allocates b → slot 0

**Maximum frame size**: 1 slot (not 2!)

**Why safe**: First block completes before second starts (sequential execution).

### 4.3 Nested Block Allocation

**No Reuse**: Nested blocks cannot reuse slots (lifetimes overlap).

**Example**:
```smalltalk
method
    [:a |
        [:b | a + b] value: 2
    ] value: 1
```

**Allocation**:
- a → slot 0
- b → slot 1
- Frame size: 2 slots

**Why no reuse**: When inner block executes, outer block's variables must still be accessible.

### 4.4 Bytecode Emission

**Variable Access**:
```csharp
void EmitVariablePush(string varName)
{
    int absoluteIndex = _allocator.GetSlotIndex(varName);

    if (absoluteIndex < 16)
    {
        EmitByte(0x10 + absoluteIndex);  // PUSH_TEMP (short form)
    }
    else
    {
        EmitByte(0x80);                   // Extended
        EmitByte(0x00);
        EmitByte((byte)absoluteIndex);
    }
}

void EmitVariableStore(string varName)
{
    int absoluteIndex = _allocator.GetSlotIndex(varName);

    if (absoluteIndex < 8)
    {
        EmitByte(0x68 + absoluteIndex);  // STORE_TEMP (short form)
    }
    else
    {
        EmitByte(0x81);                   // Extended
        EmitByte(0x40);
        EmitByte((byte)absoluteIndex);
    }
}
```

**Key Point**: Bytecode contains absolute indices from unified frame, no special handling for block variables.

### 4.5 Block Compilation and blockCopy: Emission

**Block Literal Detection**: Parser identifies `[:args | body]` syntax.

**Offset Calculation**:
```csharp
int CalculateBlockBodyOffset(BlockNode block)
{
    // Offset relative to METHOD bytecode start (not current position)
    int currentPosition = _emitter.CurrentBytecodePosition;
    int blockCopySize = 6;  // PUSH_SELF + PUSH_CONST + SEND blockCopy:
    return currentPosition + blockCopySize;
}
```

**Emission Sequence**:
```csharp
void EmitBlockLiteral(BlockNode block)
{
    int offset = CalculateBlockBodyOffset(block);

    // Emit blockCopy: send
    Emit(Bytecode.PUSH_ACTIVE_CONTEXT);  // 0x89
    EmitPushConstant(block.Arguments.Count);  // nargs
    EmitSend("blockCopy:", 1);  // Primitive 80

    // Reserve jump over block body
    int jumpPos = EmitJumpPlaceholder();

    // Emit block body at calculated offset
    int bodyStart = CurrentPosition;
    Debug.Assert(bodyStart == offset);

    foreach (var stmt in block.Body)
    {
        EmitStatement(stmt);  // Uses absolute indices from allocator
    }

    EmitReturnStackTop();  // Block return

    // Patch jump
    int bodyEnd = CurrentPosition;
    PatchJump(jumpPos, bodyEnd - jumpPos);
}
```

**Nested Blocks**:
```smalltalk
[:a | [:b | a + b]]
```

**Bytecodes**:
```
0:  PUSH_ACTIVE_CONTEXT
1:  PUSH_CONST 1        ; Outer nargs
3:  SEND blockCopy: 1
6:  JUMP 12             ; Skip outer body

; === Outer block body (offset 9) ===
9:  PUSH_ACTIVE_CONTEXT
10: PUSH_CONST 1        ; Inner nargs
12: SEND blockCopy: 1
15: JUMP 8              ; Skip inner body

; === Inner block body (offset 18) ===
18: PUSH_TEMP 0         ; a (absolute index)
19: PUSH_TEMP 1         ; b (absolute index)
20: SEND + 1
23: RETURN_STACK_TOP

; === Outer block continues ===
24: RETURN_STACK_TOP
```

**CRITICAL**: Inner block offset (18) calculated from METHOD start, not outer block start.

---

## 5. Runtime Phase

### 5.1 Method Activation

**Message send**: `obj testMethod: 42`

**Interpreter actions**:
1. Allocate MethodContext with size = temp frame size + stack space
2. Initialize instance variables:
   - sender → current active context
   - receiver → obj
   - method → CompiledMethod for testMethod:
   - instructionPointer → 0
   - stackPointer → temp frame size
3. Store arguments into temp frame:
   - `tempFrame[0]` ← 42 (argument x)
4. Initialize temporaries to nil:
   - `tempFrame[1]` ← nil (methodTemp)
5. Set active context → new MethodContext

### 5.2 Block Activation (Primitive 81)

**Block send**: `blockClosure value: 10`

**Interpreter actions**:
1. Extract BlockContext (receiver)
2. Extract home MethodContext via BlockContext.home
3. Extract argument count from BlockContext.argumentCount
4. **Store arguments into home MethodContext temp frame**:
   - Arguments stored at pre-assigned indices
   - For example: `home.tempFrame[2]` ← 10 (blockArg)
5. Initialize block temporaries to nil in home temp frame:
   - `home.tempFrame[3]` ← nil (blockTemp)
6. Create new BlockContext for execution:
   - sender → current active context
   - home → original home (unchanged)
   - initialIP → starting bytecode offset
   - instructionPointer → initialIP
7. Set active context → new BlockContext

**CRITICAL**: Arguments are stored in the **home MethodContext**, not in BlockContext!

### 5.3 Context Retention (Escaping Blocks)

**Problem**: What happens when a block outlives its defining method?

**Example**:
```smalltalk
makeCounter
  | count |
  count := 0.
  ^[:inc | count := count + inc]  "Block escapes method scope"

laterMethod
  counter := self makeCounter.
  counter value: 1.  "count now 1"
  counter value: 5.  "count now 6"
```

**Lifecycle**:

**Step 1: Block Creation (Primitive 80)**
- MethodContext allocated with `count` at temp frame index 0
- BlockContext created with `home` → MethodContext

**Step 2: Block Returned**
- Block escapes lexical scope
- **CRITICAL**: Interpreter must detect escaping block

**Step 3: Context Retention**
- Interpreter checks: Are there any BlockContexts with `home` pointing to this MethodContext?
- YES: Block was returned
- MethodContext marked as RETAINED (moved to heap, not recycled)

**Step 4: Later Evaluation**
- `counter value: 1` activates block
- Retrieves `home` pointer → retained MethodContext
- Accesses `count` via home → SUCCESS

**Runtime Requirements**:
- BlockContext must retain `home` pointer to MethodContext
- MethodContext must NOT be recycled if referenced by any live block
- Garbage collector must trace from blocks to their home contexts

**Detection of Escaping Blocks**:
```csharp
void ReturnFromMethod(SmalltalkOOP result)
{
    SmalltalkOOP methodContext = _activeContext;

    // Check if any blocks reference this context
    if (HasLiveBlocksReferencingContext(methodContext))
    {
        // Mark context as retained (don't recycle)
        MarkContextAsRetained(methodContext);
    }

    // Resume sender
    _activeContext = GetSender(methodContext);
    Push(result);
}
```

---

## 6. Variable Access Implementation

### 6.1 VM Implementation: PushTemporaryVariable

```csharp
private void PushTemporaryVariable(int index)
{
    // Step 1: Find the context holding variables
    SmalltalkOOP variableContext;

    if (IsBlockContext(_activeContext))
    {
        // Block execution: traverse to MethodContext via home pointer
        variableContext = GetHome(_activeContext);
    }
    else
    {
        // Method execution: use current context
        variableContext = _activeContext;
    }

    // Step 2: Read from temp frame with absolute index
    SmalltalkOOP value = ReadIndexedSlot(variableContext, index);

    // Step 3: Push onto evaluation stack
    Push(value);
}
```

**Key Points**:
1. **No nargs check**: No `if (index < nargs)` dual routing
2. **Direct access**: MethodContext temp frame accessed with absolute index
3. **Simple traversal**: Just find home MethodContext, then direct access
4. **Works for ALL variable types**: method args/temps, block args/temps

### 6.2 VM Implementation: StoreTemporaryVariable

```csharp
private void StoreTemporaryVariable(int index)
{
    // Step 1: Pop value from stack
    SmalltalkOOP value = Pop();

    // Step 2: Find the context holding variables
    SmalltalkOOP variableContext;

    if (IsBlockContext(_activeContext))
    {
        // Block execution: traverse to MethodContext via home pointer
        variableContext = GetHome(_activeContext);
    }
    else
    {
        // Method execution: use current context
        variableContext = _activeContext;
    }

    // Step 3: Write to temp frame with absolute index
    WriteIndexedSlot(variableContext, index, value);
}
```

### 6.3 Helper: GetHome

```csharp
private SmalltalkOOP GetHome(SmalltalkOOP context)
{
    // BlockContext has 'home' field pointing to MethodContext
    if (IsBlockContext(context))
    {
        return GetInstanceVariable(context, BLOCKCONTEXT_HOME_INDEX);
    }
    return context;  // Already a MethodContext
}
```

### 6.4 Helper: FindHomeMethodContext (for nested blocks)

```csharp
private SmalltalkOOP FindHomeMethodContext(SmalltalkOOP contextOOP)
{
    // Traverse home chain until we find a MethodContext
    while (true)
    {
        var classOOP = GetClassOOP(contextOOP);

        if (classOOP == _metadata.MethodContextClassOOP)
        {
            return contextOOP;  // Found it
        }

        // Must be BlockContext - follow home link
        contextOOP = GetInstanceVariable(contextOOP, BLOCKCONTEXT_HOME_INDEX);

        if (contextOOP.IsNil)
        {
            throw new InvalidOperationException("Home chain broken");
        }
    }
}
```

---

## 7. Block Primitives

### 7.1 Primitive 80: blockCopy:

**Purpose**: Creates BlockContext for block literal.

**Blue Book Specification**: Lines 1146-1175

**Receiver**: Current executing context (MethodContext or BlockContext)
**Argument**: Block argument count (SmallInteger)
**Returns**: New BlockContext instance

**Implementation**:
```csharp
public static void PrimitiveBlockCopy(VirtualMachine vm)
{
    // Pop arguments
    var blockArgumentCountOOP = vm.PopStack();
    var contextOOP = vm.PopStack();

    // Validate
    if (!blockArgumentCountOOP.IsSmallInteger)
    {
        vm.PrimitiveFail();
        return;
    }

    int blockArgumentCount = blockArgumentCountOOP.SmallIntegerValue;

    // Find home context
    SmalltalkOOP methodContextOOP;
    var contextClass = vm.GetClass(contextOOP);

    if (contextClass == vm.ClassBlockContextPointer)
    {
        // Context is BlockContext - use its home
        methodContextOOP = vm.GetInstanceVariable(
            contextOOP,
            VMConstants.BLOCKCONTEXT_HOME_INDEX
        );
    }
    else if (contextClass == vm.ClassMethodContextPointer)
    {
        // Context is MethodContext - it IS the home
        methodContextOOP = contextOOP;
    }
    else
    {
        vm.PrimitiveFail();
        return;
    }

    // Allocate BlockContext (NO indexed slots)
    var newContextOOP = vm.InstantiateClass(
        vm.ClassBlockContextPointer,
        indexedSlotCount: 0  // CRITICAL: 0 indexed slots
    );

    // Calculate initialIP (current IP + 3 for blockCopy: send)
    int initialIP = vm.InstructionPointer + 3;
    var initialIPOOP = SmalltalkOOP.FromSmallInteger(initialIP);

    // Initialize BlockContext fields
    vm.SetInstanceVariable(newContextOOP, BLOCKCONTEXT_INITIALIP_INDEX, initialIPOOP);
    vm.SetInstanceVariable(newContextOOP, BLOCKCONTEXT_PC_INDEX, initialIPOOP);
    vm.SetInstanceVariable(newContextOOP, BLOCKCONTEXT_STACKP_INDEX,
        SmalltalkOOP.FromSmallInteger(0));
    vm.SetInstanceVariable(newContextOOP, BLOCKCONTEXT_NARGS_INDEX,
        blockArgumentCountOOP);
    vm.SetInstanceVariable(newContextOOP, BLOCKCONTEXT_HOME_INDEX, methodContextOOP);

    // Push result
    vm.Push(newContextOOP);
}
```

### 7.2 Primitive 81: value / value: / value:value:

**Purpose**: Activates BlockContext and stores arguments into MethodContext temp frame.

**Blue Book Specification**: Lines 1179-1204

**Receiver**: BlockContext
**Arguments**: 0-N arguments matching block's nargs
**Returns**: Does NOT return - switches active context (trampoline)

**Implementation**:
```csharp
public static void PrimitiveValue(VirtualMachine vm, int argumentCount)
{
    // Get receiver (BlockContext)
    var blockContextOOP = vm.StackValue(argumentCount);

    // Validate receiver is BlockContext
    var classOOP = vm.GetClass(blockContextOOP);
    if (classOOP != vm.ClassBlockContextPointer)
    {
        vm.PrimitiveFail();
        return;
    }

    // Get expected argument count
    var nargsOOP = vm.GetInstanceVariable(
        blockContextOOP,
        VMConstants.BLOCKCONTEXT_NARGS_INDEX
    );
    int expectedArgs = nargsOOP.SmallIntegerValue;

    // Validate argument count
    if (argumentCount != expectedArgs)
    {
        vm.PrimitiveFail();
        return;
    }

    // Get home MethodContext
    SmalltalkOOP homeContext = vm.GetInstanceVariable(
        blockContextOOP,
        VMConstants.BLOCKCONTEXT_HOME_INDEX
    );

    // CRITICAL: Store arguments into HOME MethodContext temp frame
    // Arguments are stored at pre-assigned indices determined by compiler
    int startIndex = GetBlockArgumentStartIndex(blockContextOOP);
    for (int i = 0; i < argumentCount; i++)
    {
        var argOOP = vm.StackValue(argumentCount - 1 - i);
        vm.WriteIndexedSlot(homeContext, startIndex + i, argOOP);
    }

    // Pop arguments + receiver from stack
    vm.PopN(argumentCount + 1);

    // Get initialIP
    var initialIPOOP = vm.GetInstanceVariable(
        blockContextOOP,
        VMConstants.BLOCKCONTEXT_INITIALIP_INDEX
    );
    int initialIP = initialIPOOP.SmallIntegerValue;

    // Create new BlockContext for execution
    var newContext = vm.AllocateExecutionBlockContext(
        sender: vm._activeContext,
        home: homeContext,
        initialIP: initialIP,
        nargs: expectedArgs
    );

    // Switch to new context (trampoline)
    vm._activeContext = newContext;
    vm._instructionPointer = initialIP;
}
```

**Note**: `GetBlockArgumentStartIndex` needs compiler metadata to determine where block arguments start in the unified frame.

---

## 8. Complete Examples

### 8.1 Example: Simple Block

**Source Code**:
```smalltalk
method: x
    [:a | a + x] value: 5
```

**Compilation**:
```
Variable Index Assignment:
  x → index 0 (method arg)
  a → index 1 (block arg)
Max frame size: 2
```

**Runtime Execution**:
```
1. Method activation creates MethodContext:
   indexedSlots[0] = <x value from caller>

2. Block activation (Primitive 81):
   - Stores argument: homeContext.indexedSlots[1] = 5

3. Block bytecode execution:
   - PUSH_TEMP 1: Read indexedSlots[1] → 5
   - PUSH_TEMP 0: Read indexedSlots[0] → x value
   - SEND +
```

### 8.2 Example: Nested Blocks

**Source Code**:
```smalltalk
method
    [:a |
        [:b | a + b] value: 2
    ] value: 1
```

**Compilation**:
```
Variable Index Assignment:
  a → index 0 (outer block arg)
  b → index 1 (inner block arg)
Max frame size: 2
```

**Runtime Execution**:
```
1. Outer block activation:
   - homeContext.indexedSlots[0] = 1 (a)

2. Inner block activation:
   - homeContext.indexedSlots[1] = 2 (b)

3. Inner block execution (a + b):
   - PUSH_TEMP 0: Read indexedSlots[0] → 1
   - PUSH_TEMP 1: Read indexedSlots[1] → 2
   - SEND +
   - Result: 3
```

### 8.3 Example: Sequential Block Slot Reuse

**Source Code**:
```smalltalk
method: x
    | temp |
    [:a | a + 1] value: 5.
    [:b | b + 2] value: 10.
    ^temp
```

**Compilation**:
```
Variable Index Assignment:
  x → index 0 (method arg)
  temp → index 1 (method temp)
  a → index 2 (first block arg)
  b → index 2 (second block arg - REUSED!)
Max frame size: 3 (not 4!)
```

**Why reuse works**:
- First block completes before second starts
- Slot 2 is freed after first block
- Second block can safely reuse slot 2

### 8.4 Example: Escaping Block

**Source Code**:
```smalltalk
makeCounter
  | count |
  count := 0.
  ^[:inc | count := count + inc]
```

**Compilation**:
```
Variable Index Assignment:
  count → index 0 (method temp)
  inc → index 1 (block arg)
Max frame size: 2
```

**Runtime**:
```
1. Method activation:
   - MethodContext.indexedSlots[0] = 0 (count)

2. Block creation (Primitive 80):
   - BlockContext.home → MethodContext

3. Block returned:
   - Interpreter detects escaping block
   - MethodContext marked as RETAINED

4. Later evaluation (counter value: 5):
   - Block activated
   - Accesses count via home pointer
   - homeContext.indexedSlots[0] = 5
   - Block maintains state across activations
```

---

## 9. Common Pitfalls

### 9.1 Pitfall: Assuming BlockContext Stores Arguments

**WRONG**:
```csharp
// Reading block argument from BlockContext
value = ReadIndexedSlot(blockContext, argIndex);
```

**CORRECT**:
```csharp
// Reading block argument from home MethodContext
MethodContext home = GetHome(blockContext);
value = ReadIndexedSlot(home, argIndex);
```

### 9.2 Pitfall: Using Relative Indices

**WRONG**:
```csharp
// Trying to calculate offset from block start
int relativeIndex = argIndex - blockStartIndex;
```

**CORRECT**:
```csharp
// Use absolute index assigned by compiler
value = ReadIndexedSlot(home, absoluteIndex);
```

### 9.3 Pitfall: Special-Casing Block Arguments

**WRONG**:
```csharp
if (isBlockArgument)
    return AccessBlockContextSlot(index);
else
    return AccessMethodContextSlot(index);
```

**CORRECT**:
```csharp
// All variables accessed the same way
MethodContext varContext = GetVariableContext(activeContext);
return ReadIndexedSlot(varContext, index);
```

### 9.4 Pitfall: Allocating Indexed Slots for BlockContext

**WRONG**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: nargs);
```

**CORRECT**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: 0);
```

### 9.5 Pitfall: Not Storing Arguments During Block Activation

**WRONG**:
```csharp
// Arguments left on stack, not stored
```

**CORRECT**:
```csharp
// Store into home MethodContext temp frame
for (int i = 0; i < args.Length; i++)
{
    WriteIndexedSlot(homeContext, startIndex + i, args[i]);
}
```

---

## 10. Implementation Checklist

### 10.1 Compiler (Smalltalk80.Compiler)

**TempFrameAllocator.cs**:
- [ ] Assigns absolute indices to all variables
- [ ] Tracks max frame size needed
- [ ] Implements sequential block slot reuse optimization
- [ ] NO special handling for block arguments (same as temps)

**VariableResolver.cs**:
- [ ] Resolves variable names to assigned indices
- [ ] Validates assignability (args read-only, temps read-write)
- [ ] Handles lexical scope traversal
- [ ] Returns absolute indices (not relative)

**BytecodeEmitter.cs**:
- [ ] Emits `PUSH_TEMP index` for variable reads
- [ ] Emits `STORE_TEMP index` for variable writes
- [ ] Uses absolute indices from TempFrameAllocator
- [ ] No special bytecodes for block arguments

**BlockNode Compilation**:
- [ ] Emits PUSH_ACTIVE_CONTEXT (0x89)
- [ ] Emits PUSH_CONSTANT with nargs (NOT ntemps)
- [ ] Emits SEND blockCopy: 1 (primitive 80)
- [ ] Emits jump over block body
- [ ] Compiles block body inline with absolute indices
- [ ] Emits RETURN_STACK_TOP at end of block

### 10.2 Interpreter (Smalltalk80.Interpreter)

**VirtualMachine.cs**:
- [ ] `PushTemporaryVariable(index)` - reads from temp frame via home pointer
- [ ] `StoreTemporaryVariable(index)` - writes to temp frame via home pointer
- [ ] `GetHome(blockContext)` - traverses to MethodContext
- [ ] `FindHomeMethodContext` - handles nested blocks
- [ ] NO `if (index < nargs)` checks
- [ ] NO index adjustment when traversing home

**Primitives**:
- [ ] **Primitive 80** (`blockCopy: nargs`) - creates BlockContext with home pointer
- [ ] **Primitive 81** (`value`, `value:`, etc.) - stores arguments into home MethodContext temp frame
- [ ] Primitive 80 allocates BlockContext with 0 indexed slots
- [ ] Primitive 81 stores args at compiler-determined indices

**Object Memory**:
- [ ] MethodContext allocation calculates size: temp frame size + stack depth
- [ ] BlockContext allocation: NO indexed slots allocated
- [ ] All slots initialized to nil

**Context Retention**:
- [ ] Detect escaping blocks (blocks returned from methods)
- [ ] Mark MethodContext as retained if referenced by live blocks
- [ ] GC traces from BlockContext to home MethodContext
- [ ] Don't recycle retained contexts

### 10.3 Constants (VMConstants.cs)

**MethodContext**:
- [ ] METHODCONTEXT_SENDER_INDEX = 0
- [ ] METHODCONTEXT_PC_INDEX = 1
- [ ] METHODCONTEXT_STACKP_INDEX = 2
- [ ] METHODCONTEXT_METHOD_INDEX = 3
- [ ] METHODCONTEXT_RECEIVER_INDEX = 4
- [ ] METHODCONTEXT_TEMP_FRAME_START = 5

**BlockContext**:
- [ ] BLOCKCONTEXT_SENDER_INDEX = 0
- [ ] BLOCKCONTEXT_PC_INDEX = 1
- [ ] BLOCKCONTEXT_STACKP_INDEX = 2
- [ ] BLOCKCONTEXT_NARGS_INDEX = 3
- [ ] BLOCKCONTEXT_INITIALIP_INDEX = 4
- [ ] BLOCKCONTEXT_HOME_INDEX = 5
- [ ] NO BLOCKCONTEXT_TEMP_FRAME_START (no indexed slots!)

### 10.4 Testing

**Unit Tests**:
- [ ] Method variable access
- [ ] Block argument access
- [ ] Nested block access
- [ ] Escaping block (context retention)
- [ ] Sequential block slot reuse
- [ ] All 194 BlockContextTest tests pass

**Integration Tests**:
- [ ] Run cold start with --skip-init-expressions
- [ ] Execute BlockContextTest testAllCombinations
- [ ] Verify all control structures work (whileTrue:, etc.)
- [ ] Test method-level and nested blocks

---

## Appendix A: Key File Locations

**Compiler**:
- `Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs` - Variable allocation
- `Smalltalk80.Compiler/VariableResolver.cs` - Variable resolution
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Bytecode generation

**Interpreter**:
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs` - Variable access
- `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` - Block primitives
- `Smalltalk80.Interpreter/Core/VM/VMConstants.cs` - Field indices

**Documentation**:
- `docs/Unified-Temp-Frame-Specification.md` - Authoritative spec
- `docs/Variable-Access-Implementation-Guide.md` - Implementation guide
- `docs/BlueBook-Context-Architecture.md` - Blue Book reference

**Tests**:
- `Smalltalk-80 Source/Tests/BlockContextTest.st` - Test suite (194 tests)

---

## Appendix B: Differences from Blue Book

The unified temp frame architecture differs from the Blue Book in one key aspect:

**Blue Book (Dual Storage)**:
- Block arguments in BlockContext indexed slots
- All temps in MethodContext indexed slots
- Dual routing: `if (index < nargs)` check

**Our Implementation (Unified Storage)**:
- ALL variables in MethodContext indexed slots
- BlockContext has NO indexed slots
- Single routing: always traverse to MethodContext

**Why we diverged**:
1. **Simplicity**: Single storage location eliminates dual routing complexity
2. **Correctness**: Nested blocks work naturally without special handling
3. **Maintainability**: Clear, predictable behavior

**Compatibility**: Both approaches are functionally equivalent and produce correct results. The Blue Book uses dual storage for expression stack optimization, while we optimize for code clarity.

---

**END OF COMPREHENSIVE REFERENCE**
