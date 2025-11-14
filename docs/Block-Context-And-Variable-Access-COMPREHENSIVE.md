# Block Context and Variable Access - Comprehensive Reference

**Last Updated**: 2025-11-13 (Added Advanced Topics section)
**Status**: CANONICAL REFERENCE
**Supersedes**: All previous block and variable access documentation
**Companion Document**: Block-Context-Design-Validation.md (Expert analysis and implementation guidance)

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
9. [Advanced Topics: Shadowing, Slot Reuse, and Escaping Blocks](#9-advanced-topics-shadowing-slot-reuse-and-escaping-blocks)
   - 9.1 [Variable Shadowing](#91-variable-shadowing)
   - 9.2 [Home Context Traversal for Nested Blocks](#92-home-context-traversal-for-nested-blocks)
   - 9.3 [Slot Reuse with Escaping Blocks](#93-slot-reuse-with-escaping-blocks)
   - 9.4 [GetBlockArgumentStartIndex Implementation](#94-getblockargumentstartindex-implementation)
   - 9.5 [Strict Allocator Policy for Nested vs Sequential Blocks](#95-strict-allocator-policy-for-nested-vs-sequential-blocks)
   - 9.6 [Detecting Escaping Blocks](#96-detecting-escaping-blocks)
10. [Common Pitfalls](#10-common-pitfalls)
11. [Implementation Checklist](#11-implementation-checklist)

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
- **Block-Context-Design-Validation.md** (Expert validation and advanced semantics)

**New in this revision**: Section 9 provides detailed semantic and implementation guidance for:
- Variable shadowing with unique absolute indices
- Home context traversal (all blocks point to same MethodContext)
- Slot reuse policies with escaping block detection
- BlockContext extension field for argument start indices
- Strict allocator policies for nested vs sequential blocks
- Three-tier escaping block detection strategy (compile-time + runtime)

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

## 9. Advanced Topics: Shadowing, Slot Reuse, and Escaping Blocks

### 9.1 Variable Shadowing

**Definition**: Inner scopes can declare variables with the same names as outer scopes, creating distinct bindings.

**Example**:
```smalltalk
method
  | x |
  x := 1.
  [:x | x + 1] value: 2.  "Inner x shadows outer x"
  ^x  "Returns 1 (outer x unchanged)"
```

**Allocation**:
- Outer x → index 0
- Inner x → index 1
- Both are distinct slots in the unified temp frame

**Blue Book Compliance**: ✅ CORRECT

**Blue Book Chapter 5** (pages 52-53): "Variables in a block may have the same names as variables in enclosing scopes. The block's variables shadow the outer variables during block execution."

#### Implementation in TempFrameAllocator

The allocator's `_allocatedSlots` dictionary is **scope-aware**:

```csharp
public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
{
    var scope = new ScopeInfo
    {
        StartSlot = _nextSlot,
        SavedBindings = new Dictionary<string, int>()
    };

    // Save outer bindings that will be shadowed
    foreach (var argName in argNames.Concat(tempNames))
    {
        if (_allocatedSlots.TryGetValue(argName, out int outerIndex))
        {
            scope.SavedBindings[argName] = outerIndex;
        }
    }
    _scopeStack.Push(scope);

    // Allocate new slots (shadows outer bindings in map)
    foreach (var argName in argNames)
    {
        int slot = _nextSlot++;
        _allocatedSlots[argName] = slot;  // Overwrites outer binding
        // Outer binding preserved in bytecode already emitted
    }
    // ... same for tempNames
}

public void ExitBlockScope()
{
    var scope = _scopeStack.Pop();

    // Restore outer bindings
    foreach (var (varName, outerIndex) in scope.SavedBindings)
    {
        _allocatedSlots[varName] = outerIndex;
    }

    // ... handle slot reuse if sequential
}
```

**Key Insight**:
1. New binding shadows old binding in the map
2. Bytecode already emitted for outer scope uses old index
3. New bytecode for inner scope uses new index
4. On ExitScope, inner binding removed, outer binding restored

**Compiler Warnings**: NOT RECOMMENDED

Shadowing is intentional in Smalltalk-80. Common patterns:
- `[:index | ...]` blocks shadow method's `index` temp
- Generic names like `x`, `each`, `item` for isolation
- No ambiguity - compiler always resolves correctly

#### Edge Cases

**Triple Nesting**:
```smalltalk
method
  | x |
  x := 1.
  [:x |
    x := 2.
    [:x | x + 3] value: 0
  ] value: 0
```

**Allocation**:
- Method x → index 0
- Outer block x → index 1
- Inner block x → index 2

**Sequential Blocks with Same Names**:
```smalltalk
method
  [:x | x + 1] value: 10.
  [:x | x + 2] value: 20.
```

**Allocation** (with slot reuse):
- First block x → index 0
- Second block x → index 0 (REUSED)

**Pseudo-Variables** (ILLEGAL):
```smalltalk
method
  [:self | self class]  "COMPILE ERROR: cannot shadow pseudo-variable"
```

VariableResolver already handles this correctly - pseudo-variables checked BEFORE block argument resolution.

### 9.2 Home Context Traversal for Nested Blocks

**Critical Finding**: All blocks in the same method have `home` pointing to the **SAME MethodContext**, not chained through parent blocks.

**Why**: With unified temp frame, all variables are in MethodContext, so a single home pointer is sufficient.

**Blue Book Primitive 80 `homeContext:` method** (page 1175):

```smalltalk
homeContext: aContext
    "If context is a BlockContext, return its home.
     If context is a MethodContext, return itself."

    (self isBlockContext: aContext)
        ifTrue: [↑ self fetchPointer: HomeIndex ofObject: aContext]
        ifFalse: [↑ aContext]
```

When creating a nested block:
1. Active context is the outer BlockContext
2. `homeContext:` returns the outer block's HOME field
3. This HOME field points to the MethodContext!
4. Nested blocks inherit the same home

**Correct Architecture**:
```
┌──────────────────┐
│ MethodContext    │◄────────────────┐
│ temp frame: [a,b,c,d] │          │
└──────────────────┘          │
                              │ home
┌──────────────────┐          │
│ Outer Block      │          │
│ home ────────────┼──────────┘
└──────────────────┘
         ▲ home
         │
┌──────────────────┐
│ Inner Block      │
│ home ────────────┘  (points to MethodContext, NOT outer block!)
└──────────────────┘
```

#### Current Implementation Bug

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` (lines 96-109)

**CURRENT (INCORRECT)**:
```csharp
if (contextClassName == "BlockContext")
{
    // Nested block - home points to active BlockContext (creates lexical chain)
    homeContext = activeContext;  // BUG!
}
```

**CORRECT**:
```csharp
if (contextClassOOP == ctx.GetBlockContextClassOOP())
{
    // Nested block - inherit the outer block's home
    // This will be the MethodContext, NOT the outer BlockContext
    homeContext = ctx.HeapManager.GetInstanceVariable(
        activeContext,
        VMConstants.BLOCKCONTEXT_HOME_INDEX
    );
}
else
{
    // Top-level block - active context IS the MethodContext
    homeContext = activeContext;
}
```

**Result**: All blocks in the same method share the same home (MethodContext).

#### Why This Works for Variable Access

**Example**:
```smalltalk
method: arg1
  | temp1 |
  [:arg2 |
    | temp2 |
    [:arg3 | arg1 + temp1 + arg2 + temp2 + arg3] value: 3
  ] value: 2
```

**Unified Temp Frame** (MethodContext indexed slots):
- Slot 0: arg1
- Slot 1: temp1
- Slot 2: arg2
- Slot 3: temp2
- Slot 4: arg3

**Inner block access to arg1**:
1. Inner BlockContext.home → MethodContext
2. Read MethodContext.tempFrame[0] → arg1 ✅

**Inner block access to arg2** (outer block argument):
1. Inner BlockContext.home → MethodContext
2. Read MethodContext.tempFrame[2] → arg2 ✅

**Key Insight**: No chain traversal needed! All variables in MethodContext, so single home pointer suffices.

### 9.3 Slot Reuse with Escaping Blocks

**Optimization**: Sequential (non-nested) blocks can reuse temp slots.

**Safe to Reuse**:
```smalltalk
method
  [:a | a + 1] value: 1.  "Block dies here"
  [:b | b + 2] value: 2.  "Safe to reuse slot 0"
```

**Unsafe to Reuse**:
```smalltalk
method
  | blocks |
  blocks := Array new: 2.
  blocks at: 1 put: [:a | a + 1].
  blocks at: 2 put: [:b | b + 2].
  ^blocks  "Both blocks alive!"
```

If block 1's arg 'a' uses slot 0, can block 2's arg 'b' reuse slot 0?
**Answer**: ❌ NO - blocks escape (stored in array), so both must be live simultaneously.

#### Compile-Time Escape Detection

**Conservative Heuristics**:

1. **Block is returned from method**
   ```smalltalk
   method
     ^[:x | x + 1]  "ESCAPES"
   ```

2. **Block assigned to instance/class/global variable**
   ```smalltalk
   method
     BlockHolder := [:x | x + 1]  "ESCAPES"
   ```

3. **Block passed to mutation method**
   ```smalltalk
   method
     | blocks |
     blocks := Array new.
     blocks add: [:x | x + 1]  "ESCAPES"
   ```

4. **Block assigned to temp then returned/stored**
   ```smalltalk
   method
     | block |
     block := [:x | x + 1].
     ^block  "ESCAPES"
   ```

**Safe Patterns** (provably non-escaping):

1. **Immediate evaluation**
   ```smalltalk
   [:x | x + 1] value: 5  "Does NOT escape"
   ```

2. **Control structures**
   ```smalltalk
   condition ifTrue: [self doSomething]  "Does NOT escape"
   ```

3. **Collection iteration**
   ```smalltalk
   array do: [:each | each print]  "Does NOT escape"
   ```

#### Recommended Policy

**CONSERVATIVE APPROACH** (recommended for correctness):

```csharp
public class TempFrameAllocator
{
    private bool _methodMightHaveEscapingBlocks = false;

    public void EnterBlockScope(List<string> argNames, List<string> tempNames,
                                bool isNested, bool mightEscape)
    {
        if (mightEscape)
        {
            _methodMightHaveEscapingBlocks = true;
        }

        // If ANY block might escape, disable ALL slot reuse
        bool canReuseSlots = !_methodMightHaveEscapingBlocks && !isNested;

        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = canReuseSlots
        };

        // ... rest of allocation
    }
}
```

**Benefits**:
- ✅ Guaranteed correctness
- ✅ Simple to reason about
- ✅ No runtime overhead

**Drawbacks**:
- ⚠️ Larger temp frames if method has escaping blocks
- ⚠️ Missed optimization for provably-safe sequential blocks

**Phase 2 Optimization** (future work):
```csharp
bool canReuseSlots = !escapeInfo.ThisBlockMightEscape && !isNested;
// Per-block escape analysis instead of method-wide flag
```

### 9.4 GetBlockArgumentStartIndex Implementation

**Problem**: Primitive 81 must store block arguments into the **home MethodContext temp frame** at the correct indices.

**Question**: How does Primitive 81 know where block arguments start in the unified frame?

#### Recommended Solution: BlockContext Extension Field

Add `BLOCKCONTEXT_ARG_START_INDEX = 6` as an extension field to BlockContext.

**VMConstants.cs**:
```csharp
// BlockContext fields (Blue Book: 0-5)
public const int BLOCKCONTEXT_SENDER_INDEX = 0;
public const int BLOCKCONTEXT_PC_INDEX = 1;
public const int BLOCKCONTEXT_STACKP_INDEX = 2;
public const int BLOCKCONTEXT_NARGS_INDEX = 3;
public const int BLOCKCONTEXT_INITIALIP_INDEX = 4;
public const int BLOCKCONTEXT_HOME_INDEX = 5;

// Extension field for unified temp frame (beyond Blue Book)
public const int BLOCKCONTEXT_ARG_START_INDEX = 6;
```

**Why Extension Field**:
- ✅ Simpler than metadata in literal frame
- ✅ No change to Primitive 80 signature
- ✅ Direct access in Primitive 81
- ✅ Better encapsulation (metadata travels with block)
- ⚠️ Acceptable Blue Book deviation (internal optimization)

#### Compiler Modification

**BytecodeCompiler.cs** - Block compilation:

```csharp
void CompileBlock(BlockNode blockNode)
{
    // Allocate slots for block args and temps (unified frame)
    _allocator.EnterBlockScope(blockNode.Arguments, blockNode.Temporaries, isNested);

    // Get index of first block argument
    int argStartIndex = blockNode.Arguments.Count > 0
        ? _allocator.GetSlotIndex(blockNode.Arguments[0])
        : 0;  // No args

    // Store in metadata for Primitive 80
    _currentBlockMetadata.ArgumentStartIndex = argStartIndex;

    // ... emit blockCopy: with argCount
}
```

#### Primitive 80 Modification

**ControlPrimitives.cs**:

```csharp
public static SmalltalkOOP BlockCopy(IPrimitiveContext ctx,
                                      SmalltalkOOP receiver,
                                      SmalltalkOOP[] args)
{
    // ... existing validation and BlockContext creation

    // Calculate argStartIndex from compiler metadata
    // (Passed via method call or embedded in bytecode)
    int argStartIndex = GetCompilerProvidedArgStartIndex(ctx);

    // Store in BlockContext extension field
    ctx.HeapManager.SetInstanceVariable(newBlockContext,
        VMConstants.BLOCKCONTEXT_ARG_START_INDEX,
        SmalltalkOOP.FromSmallInteger(argStartIndex));

    // ... rest of initialization
}
```

#### Primitive 81 Modification

**ControlPrimitives.cs**:

```csharp
public static void PrimitiveValue(IPrimitiveContext ctx, int argumentCount)
{
    // Get BlockContext receiver
    var blockContextOOP = ctx.StackValue(argumentCount);

    // Get argument start index from BlockContext extension field
    var argStartIndexOOP = ctx.HeapManager.GetInstanceVariable(
        blockContextOOP,
        VMConstants.BLOCKCONTEXT_ARG_START_INDEX
    );
    int argStartIndex = argStartIndexOOP.SmallIntegerValue;

    // Get home MethodContext
    var homeContextOOP = ctx.HeapManager.GetInstanceVariable(
        blockContextOOP,
        VMConstants.BLOCKCONTEXT_HOME_INDEX
    );

    // CRITICAL: Store arguments into HOME MethodContext temp frame
    // at compiler-determined indices
    for (int i = 0; i < argumentCount; i++)
    {
        var argOOP = ctx.StackValue(argumentCount - 1 - i);
        ctx.HeapManager.SetIndexedSlot(
            homeContextOOP,
            argStartIndex + i,  // Absolute index in unified frame
            argOOP
        );
    }

    // ... rest of block activation
}
```

### 9.5 Strict Allocator Policy for Nested vs Sequential Blocks

**Rules**:

**Nested blocks** (overlapping lifetimes): NEVER reuse slots
```smalltalk
[:a | [:b | a + b]]  " a→0, b→1, no reuse "
```

**Sequential blocks** (non-overlapping): MAY reuse slots
```smalltalk
[:a | a + 1] value: 1.
[:b | b + 2] value: 2.  " a→0, b→0 (reused), safe "
```

#### Proving Sequential Execution

**AST Patterns that PROVE Sequential Execution**:

1. **Same Statement Sequence**
   ```smalltalk
   method
     stmt1.
     [:a | ...] value: 1.  "Block 1 executes"
     stmt2.
     [:b | ...] value: 2.  "Block 2 executes after Block 1"
   ```

2. **No Assignments to Temps**
   ```smalltalk
   method
     [:a | ...] value: 1.
     [:b | ...] value: 2.
     "Safe because blocks not stored in temps"
   ```

**AST Patterns that PREVENT Sequential Reuse**:

1. **Nested Blocks** - overlapping lifetimes
2. **Block Assignment** - might escape
3. **Conditional Execution** - order unknown
4. **Loop Bodies** - might be retained

#### Implementation

**TempFrameAllocator.cs**:

```csharp
public void EnterBlockScope(List<string> argNames,
                            List<string> tempNames,
                            BlockAnalysisInfo info)
{
    bool isNested = _blockStack.Count > 0;  // Inside another block?
    bool mightEscape = info.IsAssigned || info.IsReturned || info.IsPassedToMethod;
    bool canReuseSlots = !isNested && !mightEscape && !_methodHasEscapingBlocks;

    var scope = new ScopeInfo
    {
        StartSlot = _nextSlot,
        IsSequential = canReuseSlots
    };
    _scopeStack.Push(scope);

    // ... allocation
}

public void ExitBlockScope()
{
    var scope = _scopeStack.Pop();

    if (scope.IsSequential)
    {
        // Reuse slots
        _nextSlot = scope.StartSlot;
    }
}
```

**Phase 1 Policy**:
- ✅ Nested blocks: NO reuse
- ✅ Blocks assigned/returned/passed: NO reuse
- ✅ Blocks immediately evaluated in sequence: YES reuse

### 9.6 Detecting Escaping Blocks

**Three-Tier Strategy**:

1. **Fast Path** (compile-time proved safe): No runtime checks
2. **Medium Path** (uncertain): Targeted runtime checks
3. **Slow Path** (proved escapes): Always retain context

#### Compile-Time Analysis

**BlockEscapeAnalyzer.cs**:

```csharp
public class BlockEscapeAnalyzer
{
    public EscapeInfo AnalyzeBlock(BlockNode blockNode)
    {
        var info = new EscapeInfo();

        // Check 1: Block is return value
        if (IsReturnValue(blockNode))
        {
            info.DefinitelyEscapes = true;
            info.Reason = "Returned from method";
        }

        // Check 2: Block assigned to non-local variable
        if (IsAssignedToNonLocal(blockNode))
        {
            info.DefinitelyEscapes = true;
            info.Reason = "Assigned to instance/class/global variable";
        }

        // Check 3: Block passed to method
        if (IsMethodArgument(blockNode))
        {
            string selector = GetTargetSelector(blockNode);

            // Whitelist of known-safe selectors
            if (IsSafeIterator(selector))  // do:, select:, collect:, etc.
            {
                info.MightEscape = false;
                info.Reason = "Passed to safe iterator";
            }
            else
            {
                info.MightEscape = true;
                info.Reason = $"Passed to method #{selector}";
            }
        }

        // Check 4: Block immediately evaluated
        if (IsImmediateReceiver(blockNode))
        {
            info.DefinitelyEscapes = false;
            info.Reason = "Immediately evaluated (value/value:/etc.)";
        }

        return info;
    }

    private bool IsSafeIterator(string selector)
    {
        return selector switch
        {
            "do:" => true,
            "select:" => true,
            "collect:" => true,
            "reject:" => true,
            "detect:" => true,
            "ifTrue:" => true,
            "ifFalse:" => true,
            "ifTrue:ifFalse:" => true,
            "whileTrue:" => true,
            "whileFalse:" => true,
            _ => false
        };
    }
}
```

#### Runtime Detection

**VirtualMachine.cs**:

```csharp
void HandleMethodReturn(SmalltalkOOP methodContext, SmalltalkOOP returnValue)
{
    var method = GetMethod(methodContext);
    var metadata = GetMethodMetadata(method);

    // Fast path: no blocks at all
    if (!metadata.HasBlocks)
    {
        RecycleContext(methodContext);
        return;
    }

    // Fast path: proved safe at compile time
    if (!metadata.MightHaveEscapingBlocks)
    {
        RecycleContext(methodContext);
        return;
    }

    // Fast path: always retain
    if (metadata.DefinitelyHasEscapingBlocks)
    {
        MarkContextAsRetained(methodContext);
        return;
    }

    // Medium path: targeted runtime checks
    bool hasEscapingBlocks = false;

    // Check 1: Return value is a block?
    if (IsBlockContext(returnValue))
    {
        var blockHome = GetInstanceVariable(returnValue, BLOCKCONTEXT_HOME_INDEX);
        if (blockHome == methodContext)
        {
            hasEscapingBlocks = true;
        }
    }

    // Check 2: Scan instance variables for blocks (if method assigns to ivars)
    if (!hasEscapingBlocks && metadata.AssignsToInstanceVariables)
    {
        SmalltalkOOP receiver = GetInstanceVariable(methodContext, CONTEXT_RECEIVER_INDEX);
        if (ReceiverContainsBlockReferencingContext(receiver, methodContext))
        {
            hasEscapingBlocks = true;
        }
    }

    if (hasEscapingBlocks)
    {
        MarkContextAsRetained(methodContext);
    }
    else
    {
        RecycleContext(methodContext);
    }
}
```

#### Compile-Time Metadata

**CompiledMethod metadata**:

```csharp
public class MethodMetadata
{
    public bool HasBlocks { get; set; }
    public bool MightHaveEscapingBlocks { get; set; }
    public bool DefinitelyHasEscapingBlocks { get; set; }
    public bool AssignsToInstanceVariables { get; set; }
}
```

**Phase 1 Approach**:
- Conservative compile-time analysis
- Simple runtime checks (return value only)
- NO slot reuse if uncertain

**Phase 2 Optimization** (future):
- Dataflow analysis for temp escapes
- Whitelist of safe methods
- Targeted runtime checks

---

## 10. Common Pitfalls

### 10.1 Pitfall: Assuming BlockContext Stores Arguments

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

### 10.2 Pitfall: Using Relative Indices

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

### 10.3 Pitfall: Special-Casing Block Arguments

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

### 10.4 Pitfall: Allocating Indexed Slots for BlockContext

**WRONG**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: nargs);
```

**CORRECT**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: 0);
```

### 10.5 Pitfall: Not Storing Arguments During Block Activation

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

## 11. Implementation Checklist

### 11.0 Critical Bug Fixes (IMMEDIATE)

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` (lines 96-109)

**Bug**: Primitive 80 sets nested block's home to outer BlockContext instead of MethodContext

**Impact**: Variable access from nested blocks will fail

**Fix Required**:
```csharp
// CURRENT (WRONG):
if (contextClassName == "BlockContext")
{
    homeContext = activeContext;  // BUG: Creates chain BlockContext → BlockContext
}

// CORRECT:
if (contextClassOOP == ctx.GetBlockContextClassOOP())
{
    // Inherit outer block's home (which points to MethodContext)
    homeContext = ctx.HeapManager.GetInstanceVariable(
        activeContext,
        VMConstants.BLOCKCONTEXT_HOME_INDEX
    );
}
```

**Verification**: All 194 BlockContextTest tests must pass after fix

---

### 11.1 Compiler (Smalltalk80.Compiler)

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

### 11.2 Interpreter (Smalltalk80.Interpreter)

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

### 11.3 Constants (VMConstants.cs)

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

### 11.4 Testing

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
- `Smalltalk-80 Source/Tests/BlockContextTest.st` - Comprehensive test suite (194 tests)

---

## Appendix A.1: Test Suite Validation

**Last Validated**: 2025-11-14

### Test Suite Status: ✅ ALL TESTS PASSING

The BlockContext implementation has been validated against a comprehensive test suite of **194 test methods** covering all aspects of block execution, variable access, and control flow.

**Full Test Results**:
- `BlockContextTest>>testAllCombinations`: Returns **3503** ✅
- All 20 test categories validated individually
- Total test coverage: 190+ test methods

### Test Categories Verified

| Category | Tests | Status | Description |
|----------|-------|--------|-------------|
| **s*** | 9 | ✅ PASS | Single-level blocks with varying arg/temp counts |
| **t*** | 81 | ✅ PASS | Two-level nested blocks (all combinations) |
| **n*** | 27 | ✅ PASS | Three-level nested blocks |
| **m*** | 6 | ✅ PASS | Method multi-temp access from nested blocks |
| **mut*** | 3 | ✅ PASS | Temp mutation from blocks |
| **ma*** | 6 | ✅ PASS | Method argument access from nested blocks |
| **rw*** | 5 | ✅ PASS | Real-world patterns (whileTrue:, whileFalse:, do:, select:) |
| **e*** | 7 | ✅ PASS | Edge cases (empty blocks, multiple blocks, etc.) |
| **nlr*** | 3 | ✅ PASS | Non-local returns |
| **stress*** | 3 | ✅ PASS | Complex mutation, max nesting, shadowing |
| **bl*** | 2 | ✅ PASS | Block lifetime and storage |
| **cf*** | 2 | ✅ PASS | Control flow (early exit, recursion) |
| **iv*** | 10 | ✅ PASS | Instance variable access from blocks |
| **cv*** | 5 | ✅ PASS | Class variable access from blocks |
| **pv*** | 3 | ✅ PASS | Pool variable access from blocks |
| **gv*** | 5 | ✅ PASS | Global variable access from blocks |
| **ps*** | 5 | ✅ PASS | Pseudo-variables (self, true, false, nil, super) |
| **mx*** | 5 | ✅ PASS | Mixed variable types |
| **es*** | 2 | ✅ PASS | Self-modifying and mixed access patterns |
| **sd*** | 1 | ✅ PASS | Deep nesting (10 levels) |

### Key Achievements

✅ **Nested Block Variable Access**: All 3-level nesting patterns work correctly  
✅ **Real-World Control Structures**: whileTrue:, whileFalse:, do:, select: all operational  
✅ **Variable Mutations**: Outer temp mutation from inner blocks works correctly  
✅ **Non-Local Returns**: Block returns to method work correctly  
✅ **Complex Patterns**: Method args + temps with non-contiguous access patterns work  
✅ **Variable Types**: Instance vars, class vars, pool vars, globals all accessible from blocks  

### Running the Test Suite

**Location**: `Smalltalk-80 Source/Tests/BlockContextTest.st`

**Generate Test Bundle**:
```bash
cd "Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st > Smalltalk-80-with-tests.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-tests.st"
cp ../bin/Debug/net9.0/coldstart.json ../bin/Debug/net9.0/coldstart_with_tests.json
```

**Run Tests**:
```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json \
              --skip-init-expressions \
              --execute "BlockContextTest new testAllCombinations"
```

**Expected Output**: Test should return SmallInteger(3503) indicating all tests passed.

### Test Design

The test suite uses numeric return values to enable automated validation:
- Most basic tests return **10** when passing
- Complex tests return computed sums to validate correct arithmetic
- The full suite sums all test results to produce **3503**
- Any failure returns 0 or incorrect value, changing the total

This design allows testing without a full assertion framework and provides immediate feedback on test execution.

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
