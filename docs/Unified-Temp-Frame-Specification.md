# Unified Temp Frame Specification

**Last Updated**: 2025-11-13
**Status**: CANONICAL REFERENCE
**Supersedes**: All previous variable access and temp frame documentation

## Purpose

This document provides the authoritative specification for Smalltalk-80 variable access using the **Unified Temp Frame** architecture, strictly adhering to the Blue Book design.

---

## 1. Core Principle: Lexical Flattening

**All method and block variables—arguments and temporaries—are assigned unique indices in a single unified temp frame allocated in the MethodContext.**

This frame is the sole source of truth for variable access at runtime.

### Key Design Points

1. **Single Storage Location**: All variables (method args/temps, block args/temps, nested block args/temps) stored in MethodContext's indexed slots
2. **Absolute Indexing**: Each variable gets a unique absolute index (0, 1, 2, ...) assigned at compile time
3. **Uniform Access**: All variable access uses `PUSH_TEMP index` and `STORE_TEMP index` bytecodes
4. **Block Closure**: BlockContext holds only `home` pointer to MethodContext (no variable storage)
5. **Context Retention**: MethodContext retained if any blocks still reference it after method returns

---

## 2. Data Structures

### 2.1 MethodContext Structure

```
MethodContext (subclass of Context Part)
  Instance Variables:
    - sender          (calling context)
    - instructionPointer
    - stackPointer
    - method          (CompiledMethod)
    - receiver        (self)

  Indexed Slots (Unified Temp Frame):
    [0] method argument 0
    [1] method argument 1
    ...
    [n] method temp 0
    [n+1] method temp 1
    ...
    [m] block 1 argument 0
    [m+1] block 1 argument 1
    [m+2] block 1 temp 0
    ...
    [p] block 2 argument 0
    ...
```

**Critical**: The indexed slots array size is determined at compile time based on the maximum simultaneous variable need across all code paths.

### 2.2 BlockContext Structure

```
BlockContext (subclass of MethodContext)
  Instance Variables:
    - sender          (calling context)
    - instructionPointer
    - stackPointer
    - argumentCount   (number of block parameters)
    - initialIP       (starting bytecode offset)
    - home            (MethodContext where block was defined)

  Indexed Slots:
    NONE - BlockContext has NO indexed slots for variables
    All variable access goes through home->MethodContext
```

**Critical**: BlockContext does NOT store any variables. It only provides execution state and the `home` pointer.

---

## 3. Compilation Phase

### 3.1 Source Parsing

**Example Source:**
```smalltalk
testMethod: x
  | methodTemp |
  methodTemp := x + 1.
  [:blockArg |
    | blockTemp |
    blockTemp := methodTemp + blockArg.
    ^blockTemp
  ] value: 10
```

**Parsed Structure:**
- Method arguments: `x`
- Method temporaries: `methodTemp`
- Block 1 arguments: `blockArg`
- Block 1 temporaries: `blockTemp`

### 3.2 Lexical Flattening and Index Assignment

The compiler assigns absolute indices to ALL variables:

| Variable | Scope | Index |
|----------|-------|-------|
| x | method arg | 0 |
| methodTemp | method temp | 1 |
| blockArg | block arg | 2 |
| blockTemp | block temp | 3 |

**Algorithm (TempFrameAllocator)**:
1. Start with index = 0
2. Allocate indices for method arguments (sequential)
3. Allocate indices for method temporaries (sequential)
4. For each block (in source order):
   - Allocate indices for block arguments (sequential)
   - Allocate indices for block temporaries (sequential)
5. Track maximum index used = temp frame size

**Sequential Block Optimization:**
Sequential blocks (not nested, not escaping) can reuse slots:
```smalltalk
method
  [| temp1 | temp1 := 1] value.
  [| temp2 | temp2 := 2] value.
```
- `temp1` gets index 0
- `temp2` can REUSE index 0 (blocks don't overlap)
- Frame size = 1 (not 2)

**Nested Block Allocation:**
Nested blocks cannot reuse slots:
```smalltalk
method
  [:outer |
    [:inner | outer + inner] value: 2
  ] value: 1
```
- `outer` gets index 0
- `inner` gets index 1
- Frame size = 2 (blocks overlap in lifetime)

### 3.3 Bytecode Emission

**Source:**
```smalltalk
blockTemp := methodTemp + blockArg
```

**Emitted Bytecodes:**
```
PUSH_TEMP 1      ; push methodTemp (index 1)
PUSH_TEMP 2      ; push blockArg (index 2)
SEND +           ; send + message
STORE_TEMP 3     ; store to blockTemp (index 3)
```

**Critical**: No special bytecodes for block arguments. All use `PUSH_TEMP` / `STORE_TEMP` with absolute indices.

### 3.4 Block Literal Compilation and blockCopy: Emission

**Purpose**: Block literals `[:x | x + 1]` must be compiled to invoke Primitive 80 (`blockCopy:`) which creates the BlockContext at runtime.

**Compiler Audit Checklist**:

1. **Block Literal Detection**
   - Parser identifies `[...]` syntax
   - Constructs BlockNode AST with arguments, temporaries, body
   - Records lexical nesting depth

2. **Instruction Pointer Offset Calculation**
   - Calculate byte offset from method start to first bytecode of block body
   - This offset is passed as argument to `blockCopy:`
   - For nested blocks, offset is relative to METHOD start, not parent block

3. **Emit blockCopy: Message Send**
   ```
   PUSH_SELF              ; thisContext (receiver)
   PUSH_CONSTANT offset   ; offset to block body bytecodes
   SEND blockCopy: 1      ; invoke Primitive 80
   ```

4. **Emit Block Body Bytecodes**
   - Block body bytecodes follow the blockCopy: send
   - All variable references use PUSH_TEMP/STORE_TEMP with absolute indices
   - Block ends with RETURN_STACK_TOP (block return)

5. **Selector Resolution**
   - `blockCopy:` must resolve to `ContextPart>>blockCopy:` with primitive index 80
   - Selector must be in method's literal frame

**Example Compilation:**

Source:
```smalltalk
method
  [:x | x + 1] value: 5
```

Bytecodes:
```
; Create block
PUSH_SELF              ; Push thisContext
PUSH_CONSTANT 8        ; Offset = 8 (block body starts at byte 8)
SEND blockCopy: 1      ; Invoke Primitive 80, creates BlockContext
; Block is now on stack

; Activate block
PUSH_CONSTANT 5        ; Argument for block
SEND value: 1          ; Invoke Primitive 81

; === Block body starts here (offset 8) ===
PUSH_TEMP 0            ; x (absolute index 0 in unified frame)
PUSH_CONSTANT 1        ; Constant 1
SEND + 1               ; Send +
RETURN_STACK_TOP       ; Block return
```

**Nested Blocks:**

Source:
```smalltalk
method
  [:a |
    [:b | a + b] value: 2
  ] value: 1
```

Bytecodes:
```
; Outer block creation
PUSH_SELF
PUSH_CONSTANT 10       ; Offset to outer block body
SEND blockCopy: 1
PUSH_CONSTANT 1
SEND value: 1

; === Outer block body (offset 10) ===
PUSH_SELF              ; For inner block
PUSH_CONSTANT 20       ; Offset to INNER block body (from method start!)
SEND blockCopy: 1      ; Create inner block
PUSH_CONSTANT 2
SEND value: 1

; === Inner block body (offset 20) ===
PUSH_TEMP 0            ; a (index 0)
PUSH_TEMP 1            ; b (index 1)
SEND + 1
RETURN_STACK_TOP
```

**Critical**: Offsets are always relative to the METHOD bytecode start, not the enclosing block

---

## 4. Runtime Phase

### 4.1 Method Activation

**Message send:** `obj testMethod: 42`

**Interpreter actions:**
1. Allocate MethodContext with size = temp frame size + stack space
2. Initialize instance variables:
   - `sender` → current active context
   - `receiver` → obj
   - `method` → CompiledMethod for testMethod:
   - `instructionPointer` → 0
   - `stackPointer` → temp frame size
3. Store arguments into temp frame:
   - `tempFrame[0]` ← 42 (argument x)
4. Initialize temporaries to nil:
   - `tempFrame[1]` ← nil (methodTemp)
5. Set active context → new MethodContext

### 4.2 Block Activation

**Block send:** `blockClosure value: 10`

**Interpreter actions (Primitive 81):**
1. Extract BlockContext (receiver)
2. Extract home MethodContext via BlockContext.home
3. Extract argument count from BlockContext.argumentCount
4. **Store arguments into home MethodContext temp frame:**
   - Arguments stored at pre-assigned indices
   - For our example: `home.tempFrame[2]` ← 10 (blockArg)
5. Initialize block temporaries to nil in home temp frame:
   - `home.tempFrame[3]` ← nil (blockTemp)
6. Create new BlockContext for execution:
   - `sender` → current active context
   - `home` → original home (unchanged)
   - `initialIP` → starting bytecode offset
   - `instructionPointer` → initialIP
7. Set active context → new BlockContext

**Critical**: Arguments are stored in the **home MethodContext**, not in BlockContext!

### 4.3 Variable Access During Execution

**Bytecode:** `PUSH_TEMP 2` (access blockArg)

**Interpreter algorithm:**
```csharp
SmalltalkOOP PushTemporaryVariable(int index)
{
    // Get the context holding variables
    SmalltalkOOP variableContext;

    if (activeContext.IsBlockContext)
    {
        // Block execution: traverse home to MethodContext
        variableContext = GetHome(activeContext);
    }
    else
    {
        // Method execution: use current context
        variableContext = activeContext;
    }

    // Access temp frame directly with absolute index
    SmalltalkOOP value = ReadIndexedSlot(variableContext, index);

    // Push onto stack
    Push(value);
    return value;
}
```

**Key Points:**
- No `nargs` check (no dual routing)
- Direct access to MethodContext temp frame
- Absolute index used directly
- Works for ALL variable types (method args/temps, block args/temps)

**Bytecode:** `STORE_TEMP 3` (store to blockTemp)

**Interpreter algorithm:**
```csharp
void StoreTemporaryVariable(int index)
{
    // Pop value from stack
    SmalltalkOOP value = Pop();

    // Get the context holding variables
    SmalltalkOOP variableContext;

    if (activeContext.IsBlockContext)
    {
        // Block execution: traverse home to MethodContext
        variableContext = GetHome(activeContext);
    }
    else
    {
        // Method execution: use current context
        variableContext = activeContext;
    }

    // Store into temp frame directly with absolute index
    WriteIndexedSlot(variableContext, index, value);
}
```

### 4.4 Escaping Blocks and Context Retention

**Problem**: What happens when a block outlives its defining method?

**Example:**
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

**Lifecycle Step-by-Step**:

**Step 1: Block Creation (Primitive 80)**
- `makeCounter` executes
- MethodContext allocated with `count` at temp frame index 0
- Block literal compiled as `blockCopy:` send
- Primitive 80 creates BlockContext:
  - `home` → MethodContext of `makeCounter`
  - `initialIP` → offset to block body bytecodes
  - `argumentCount` → 1
- BlockContext pushed onto stack

**Step 2: Variable Capture**
- Block body contains `PUSH_TEMP 0` (for `count`)
- `PUSH_TEMP 0` will access MethodContext temp frame via `home` pointer
- Block depends on MethodContext remaining accessible

**Step 3: Block Stored/Returned**
- Block returned from `makeCounter` (escapes lexical scope)
- Block stored in instance variable or global
- **CRITICAL**: Block is now escaping

**Step 4: Method Returns**
- `makeCounter` returns
- Normally, MethodContext would be recycled for reuse
- **BUT**: Interpreter must detect that MethodContext is referenced by live BlockContext

**Step 5: Context Retention**
- Interpreter checks: Are there any BlockContexts with `home` pointing to this MethodContext?
- YES: Block was returned
- MethodContext marked as RETAINED (moved to heap, not recycled)
- **Runtime Invariant**: MethodContext lives as long as any block referencing it exists

**Step 6: Later Evaluation (Primitive 81)**
- `counter value: 1` sends `value:` to BlockContext
- Primitive 81 activates block:
  1. Retrieve `home` pointer → retained MethodContext
  2. Store argument (1) into temp frame: `home.tempFrame[argumentStartIndex]`
  3. Create execution BlockContext with same `home`
  4. Execute block bytecodes

**Step 7: Variable Access**
- `PUSH_TEMP 0` executes:
  1. Check: Is activeContext a BlockContext? YES
  2. Get home: `variableContext = activeContext.home`
  3. Read: `value = variableContext.tempFrame[0]`  → reads `count`
  4. SUCCESS: MethodContext still alive, variable accessible

- `STORE_TEMP 0` executes:
  1. Pop value from stack
  2. Get home: `variableContext = activeContext.home`
  3. Write: `variableContext.tempFrame[0] = value`  → updates `count`
  4. SUCCESS: Variable modified, state persists

**Step 8: Multiple Activations**
- `counter value: 5` executes
- Same MethodContext accessed again
- `count` reads previous value (1), adds 5, stores 6
- **Closure semantics**: Block maintains state across activations

**Runtime Requirements:**

| Component | Requirement |
|-----------|-------------|
| **BlockContext** | Must retain `home` pointer to MethodContext |
| **MethodContext** | Must NOT be recycled if referenced by any live block |
| **Garbage Collector** | Must trace from blocks to their home contexts |
| **Interpreter** | Must detect escaping blocks and retain MethodContext |
| **Context Activation** | Must use `home` pointer for all variable access |

**Detection of Escaping Blocks:**

Blocks escape when:
- Returned from method (`^[:x | ...]`)
- Stored in instance variable (`@myBlock := [:x | ...]`)
- Stored in class variable
- Stored in global
- Passed to another method that stores it

**Context Retention Mechanism:**

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

bool HasLiveBlocksReferencingContext(SmalltalkOOP methodContext)
{
    // Scan all reachable BlockContexts
    // Check if any have home == methodContext
    // Implementation: GC marks objects reachable from stack/heap
    return GC.IsReferencedByLiveBlock(methodContext);
}
```

**Why This Matters:**

Without context retention:
- Block execution would access recycled memory
- Variable values would be corrupted
- Closures would not work
- Smalltalk semantics violated

With context retention:
- Blocks work as lexical closures
- Variables maintain state across activations
- Blocks can outlive their defining method
- Full Smalltalk-80 semantics implemented

---

## 5. Complete Example Walkthrough

### Source Code

```smalltalk
testMethod
  | x |
  x := 42.
  [:a :b |
    | y |
    y := a + b.
    [:c | x + y + c] value: 3
  ] value: 1 value: 2
```

### Compilation

**Variable Index Assignment:**
| Variable | Scope | Index |
|----------|-------|-------|
| x | method temp | 0 |
| a | outer block arg | 1 |
| b | outer block arg | 2 |
| y | outer block temp | 3 |
| c | inner block arg | 4 |

**Temp Frame Size:** 5

**Bytecodes for `x + y + c`:**
```
PUSH_TEMP 0   ; x
PUSH_TEMP 3   ; y
SEND +
PUSH_TEMP 4   ; c
SEND +
```

### Runtime Execution

**Step 1: Method activation**
```
MethodContext created:
  tempFrame[0] = nil  (x)
  tempFrame[1] = nil  (a - not yet assigned)
  tempFrame[2] = nil  (b - not yet assigned)
  tempFrame[3] = nil  (y - not yet assigned)
  tempFrame[4] = nil  (c - not yet assigned)
```

**Step 2: `x := 42`**
```
tempFrame[0] = 42
```

**Step 3: Outer block `value: 1 value: 2`**
```
Primitive 81 stores arguments:
  tempFrame[1] = 1   (a)
  tempFrame[2] = 2   (b)
```

**Step 4: `y := a + b`**
```
PUSH_TEMP 1 → pushes 1
PUSH_TEMP 2 → pushes 2
SEND + → result 3
STORE_TEMP 3 → tempFrame[3] = 3
```

**Step 5: Inner block `value: 3`**
```
Primitive 81 stores arguments:
  tempFrame[4] = 3   (c)
```

**Step 6: `x + y + c`**
```
PUSH_TEMP 0 → pushes 42  (from tempFrame[0])
PUSH_TEMP 3 → pushes 3   (from tempFrame[3])
SEND + → result 45
PUSH_TEMP 4 → pushes 3   (from tempFrame[4])
SEND + → result 48
```

**Final result:** 48

---

## 6. Implementation Components

### 6.1 Compiler (Smalltalk80.Compiler)

**TempFrameAllocator.cs**
- Assigns absolute indices to all variables
- Tracks max frame size needed
- Implements sequential block slot reuse optimization

**VariableResolver.cs**
- Resolves variable names to assigned indices
- Validates assignability (args read-only, temps read-write)
- Handles lexical scope traversal

**BytecodeEmitter.cs**
- Emits `PUSH_TEMP index` for variable reads
- Emits `STORE_TEMP index` for variable writes
- No special bytecodes for block arguments

### 6.2 Interpreter (Smalltalk80.Interpreter)

**VirtualMachine.cs**
- `PushTemporaryVariable(index)` - reads from temp frame via home pointer
- `StoreTemporaryVariable(index)` - writes to temp frame via home pointer
- `GetHome(blockContext)` - traverses to MethodContext

**Primitives**
- **Primitive 80** (`blockCopy: nargs`) - creates BlockContext with home pointer
- **Primitive 81** (`value`, `value:`, etc.) - stores arguments into home MethodContext temp frame

### 6.3 Object Memory (Smalltalk80.Interpreter.Core)

**MethodContext Allocation**
- Calculates size: temp frame size + stack depth
- Allocates indexed slots for temp frame
- Initializes all slots to nil

**BlockContext Allocation**
- NO indexed slots allocated
- Only instance variables initialized

---

## 7. Differences from Previous Designs

### What Changed

| Aspect | Old Design (OBSOLETE) | New Design (CANONICAL) |
|--------|----------------------|----------------------|
| Block argument storage | BlockContext indexed slots | MethodContext temp frame |
| Variable access routing | Dual path (`if index < nargs`) | Single path (always MethodContext) |
| Bytecodes | Special PUSH_BLOCK_ARG | Uniform PUSH_TEMP |
| Index type | Relative/hierarchical | Absolute |
| BlockContext slots | Stored arguments | NO variable storage |

### Why This Design

1. **Simplicity**: Single storage location, single access path
2. **Blue Book Compliance**: Matches original Smalltalk-80 specification
3. **Lexical Closure**: All variables naturally captured by home pointer
4. **Performance**: No runtime routing decisions, direct indexed access
5. **Correctness**: Nested blocks work naturally (no parent BlockContext traversal needed)

---

## 8. Validation and Testing

### Test Cases

**Test 1: Method variables**
```smalltalk
method: arg
  | temp |
  temp := arg + 1.
  ^temp
```
Expected: `temp` and `arg` both in MethodContext temp frame

**Test 2: Block arguments**
```smalltalk
method
  [:x | x + 1] value: 5
```
Expected: `x` stored in MethodContext temp frame when block activated

**Test 3: Nested blocks**
```smalltalk
method
  [:a |
    [:b | a + b] value: 2
  ] value: 1
```
Expected: Both `a` and `b` in MethodContext temp frame, inner block accesses `a` successfully

**Test 4: Escaping block**
```smalltalk
method
  | x |
  x := 0.
  ^[:inc | x := x + inc]
```
Expected: MethodContext retained, block can access and modify `x` after method returns

---

## 9. References

- **Blue Book**: Smalltalk-80: The Language and Its Implementation (Chapter 29-30)
- **Formal Specification of the Primitive Methods.md**: Primitives 80-81 specifications
- **Interpreter-Architecture.md**: Runtime system architecture
- **BlueBook-Context-Architecture.md**: Context structures and relationships

---

## Appendix A: Common Pitfalls

### Pitfall 1: Assuming BlockContext Stores Arguments

**WRONG:**
```csharp
// Reading block argument from BlockContext
value = ReadIndexedSlot(blockContext, argIndex);
```

**CORRECT:**
```csharp
// Reading block argument from home MethodContext
MethodContext home = GetHome(blockContext);
value = ReadIndexedSlot(home, argIndex);
```

### Pitfall 2: Using Relative Indices

**WRONG:**
```csharp
// Trying to calculate offset from block start
int relativeIndex = argIndex - blockStartIndex;
```

**CORRECT:**
```csharp
// Use absolute index assigned by compiler
value = ReadIndexedSlot(home, absoluteIndex);
```

### Pitfall 3: Special-Casing Block Arguments

**WRONG:**
```csharp
if (isBlockArgument)
    return AccessBlockContextSlot(index);
else
    return AccessMethodContextSlot(index);
```

**CORRECT:**
```csharp
// All variables accessed the same way
MethodContext varContext = GetVariableContext(activeContext);
return ReadIndexedSlot(varContext, index);
```

---

**END OF SPECIFICATION**
