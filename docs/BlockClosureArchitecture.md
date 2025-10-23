# BlockClosure Architecture

**Date**: 2025-01-23
**Status**: Defined & Ready for Implementation

## Overview

BlockClosure is the runtime representation of block literals in Smalltalk-80. It wraps a CompiledMethod and captures the lexical environment where the block was defined, enabling proper lexical scoping, non-local returns, and closure semantics.

This document describes the complete compilation and runtime pipeline for blocks: how the compiler emits method and block metadata, how the image/coldstart stores artifacts, and how the VM uses compiler-produced structures while interpreting bytecode so that blocks behave as closures with correct lexical scope and non-local return semantics.

### Example Method for Explanation

```smalltalk
BlockTest >> compute
    | a |
    a := 10.
    ^ [ a + 1 ] value
```

All subsequent sections explain how a compiler transforms this source into compiled artifacts and how the VM executes them so the block sees the lexical variable `a` and can perform normal and non-local returns.

## Architecture

### Class Structure

```smalltalk
Object subclass: #BlockClosure
    instanceVariableNames: 'method homeContext returnContext numArgs'
```

**Instance Variables**:
- `method` - The CompiledMethod containing the block's bytecode
- `homeContext` - The MethodContext where the block was defined (lexical environment)
- `returnContext` - The context to return to for non-local returns (^)
- `numArgs` - The number of arguments the block expects

### Current vs. Target Architecture

#### Current (As of 2025-01-23)
```
Block Literal → Raw CompiledMethod → InvokeBlock
                                        ↓
                              FindMethodContextByLocalId
```

**Issues**:
- No runtime object wraps the block
- Context must be searched at invocation time
- Cannot capture outer temporaries
- Non-local returns not supported

#### Target (After BlockClosure Integration)
```
Block Literal → BlockClosure (wraps CompiledMethod + homeContext)
                     ↓
                 InvokeBlock
                     ↓
            Extract homeContext from BlockClosure
```

**Benefits**:
- Context captured at creation time (when pushLiteral executes)
- Can capture outer temporaries in the future
- Non-local returns possible via returnContext
- GC-safe (contexts kept alive while block exists)

## Compilation and Runtime Pipeline

### Compiler Steps

#### 1. Source Analysis and Scope Resolution

**Tokenize and Parse**:
- Parse `BlockTest >> compute` into an AST
- Identify method body: temp declaration `| a |`, assignment `a := 10`, block literal `[ a + 1 ]`, message send `value`

**Build Lexical Scope Tree**:
- **Method scope**: `compute` with temporary `a` at index 0, receiver `self`
- **Nested block scope**: Block literal with no temps/args, captures method local `a` and receiver `self`

**Assign Indexes**:
- Method temporaries get numeric indexes for MethodContext indexed slots:
  - `a` → index 0 in method context indexed area
- Block temporaries/arguments get their own counts (in this case: 0 args, 0 temps)
- Receiver access via `pushReceiver` bytecode (reads from home context)

**Access Pattern Classification**:
- Local indexed slots: `a` (temporary)
- Receiver access: `self` (via pushReceiver from home context)
- Outer context reads: `a` inside block (requires PUSH_OUTER or access via home context)

#### 2. Create CompiledMethod Objects

**Emit CompiledMethod for outer method `compute`**:
```
Header: argCount=0, tempCount=1, literalCount=2, needsLargeFrame=false
Bytecode:
  pushLiteral 0        ; Push SmallInteger 10
  popStoreTemp 0       ; Store into temp slot 0 (a)
  pushLiteral 1        ; Push block CompiledMethod
  send #value 0        ; Send value with 0 args
  returnTop            ; Return result
Literals:
  [0] = SmallInteger(10)
  [1] = CompiledMethod(block) → references block below
```

**Emit separate CompiledMethod for block literal**:
```
Header: argCount=0, tempCount=0, literalCount=1, needsLargeFrame=false
Bytecode:
  pushOuter 0 0        ; Push outer context (depth=0) slot 0 (a)
  pushLiteral 0        ; Push SmallInteger 1
  send #+ 1            ; Send + with 1 arg
  returnTop            ; Return result (block-local return)
Literals:
  [0] = SmallInteger(1)
  [1] = Symbol(#+)
```

**Note**: If the block contained `^ a + 1` instead, the bytecode would use `returnNonLocal` to return from the defining method.

#### 3. Block Ownership and Metadata

**blockOwnerMap Entry**:
```json
{
  "block:a1b2c3d4...": "method:BlockTest>>compute"
}
```

- Maps block CompiledMethod local ID to owner method local ID
- Records lexical nesting level (depth=0 for direct nesting, depth=1 for nested blocks, etc.)
- Enables VM to validate ownership and find home context at runtime

**Compiler Markers**:
- Block CompiledMethod flagged as block literal (can check if localId starts with "block:")
- VM will wrap block literals in BlockClosure at runtime

#### 4. Literal and Header Layout

**CompiledMethod Header Fields**:
- `argCount`: Number of arguments (0 for blocks with no args)
- `tempCount`: Number of temporaries (0 for blocks with no temps)
- `literalCount`: Number of literals in literal table
- `needsLargeFrame`: Whether method needs large stack frame (48 vs 16 slots)

**Literal Table**:
- Stored in CompiledMethod indexed portion
- Index 0: Method header (SmallInteger encoding all metadata)
- Index 1+: Actual literals (classes, selectors, blocks, constants)
- Block CompiledMethods appear as literals in outer method

### Bytecode Patterns Relevant to Blocks

#### PUSH_LITERAL n (0x20-0x3F or extended)
- Push literal at index n from current method's literal table
- When literal is a block CompiledMethod, VM wraps it in BlockClosure

#### PUSH_TEMP idx (0x10-0x1F)
- Read indexed slot idx from current MethodContext (arguments + temps)
- Works for both methods and block activations

#### PUSH_RECEIVER (0x70)
- Push `self` from current context
- Inside block: resolves to home context's receiver

#### PUSH_IVAR idx (0x00-0x0F)
- Push instance variable idx from receiver
- Inside block: uses home context's receiver

#### PUSH_OUTER depth index (implementation-specific, may use extended bytecode)
- Push variable from outer lexical context at nest depth
- `depth=0`: immediate outer context
- `depth=1`: outer outer context (for nested blocks)
- Alternative: blocks access via home context instead of explicit PUSH_OUTER

#### SEND selector arity
- Perform message send (e.g., `send #value 0`)
- For BlockClosure, triggers block invocation

#### RETURN_TOP (0x7C)
- Block-local return: returns value from block activation to caller
- Does not unwind past block boundary

#### RETURN_NON_LOCAL (extended bytecode or flag in header)
- Non-local return: returns value from defining method context
- Compiler emits when `^` appears inside block
- VM unwinds contexts to reach returnContext

### Runtime Representations and VM Responsibilities

#### CompiledMethod Objects
- **Immutable** bytecode + literal table
- Exist in image/coldstart, pushed as literals
- Do not carry runtime captured lexical environment
- Contain metadata: argCount, tempCount, literalCount, bytecode

#### BlockClosure Objects at Runtime
**Created when**: Block literal evaluated at runtime (pushLiteral pushes block CompiledMethod)

**Contents**:
- `method`: Reference to block CompiledMethod
- `homeContext`: MethodContext that was active when block created (captures lexical environment)
- `returnContext`: Context to return to for non-local returns (usually same as homeContext)
- `numArgs`: Number of arguments block expects

**Purpose**: Pair code with lexical environment to create closure

#### MethodContext Objects
**Structure**:
- Instance variables: sender, pc, stackp, method, receiverMap, receiver
- Indexed slots: arguments + temporaries + execution stack
- Created for each method invocation

**Lifecycle**:
- Allocated when method invoked
- Kept alive by BlockClosures that capture it as homeContext
- GC'd when no longer referenced

#### BlockContext vs. Block Activation
**Option 1: Separate BlockContext**:
- Create BlockContext with instance variables: sender, pc, nargs, startpc, home
- Indexed slots: block arguments + block temps
- Links to homeContext for outer variable access

**Option 2: Small Activation Frame**:
- Keep home MethodContext alive
- Block activation is minimal (just stack frame)
- Direct access to home context indexed slots

**Our Implementation**: Uses MethodContext for block execution with _blockContextMapping

### VM Mapping: Compiler Constructs to Runtime Behavior

#### When PUSH_LITERAL pushes block CompiledMethod:

```csharp
// In pushLiteral bytecode handler
if (IsBlockLiteral(literalOOP))
{
    // Wrap block in BlockClosure with current context
    var closureOOP = CreateBlockClosure(
        literalOOP,              // block CompiledMethod
        _activeContextOOP,       // homeContext (captures lexical env)
        _activeContextOOP,       // returnContext (for non-local returns)
        blockArgCount            // from block method header
    );
    Push(closureOOP);
}
else
{
    Push(literalOOP);
}
```

#### When SEND #value invokes BlockClosure:

```csharp
// In SendMessage when receiver is BlockClosure
if (IsBlockClosure(receiverOOP))
{
    // Extract closure components
    var blockMethodOOP = GetInstanceVariable(receiverOOP, methodIndex);
    var homeContextOOP = GetInstanceVariable(receiverOOP, homeContextIndex);
    var returnContextOOP = GetInstanceVariable(receiverOOP, returnContextIndex);

    // Create block activation
    var blockActivationOOP = CreateMethodContext(
        blockMethodOOP,
        receiverFromHomeContext,  // Get receiver from homeContext
        args,
        _activeContextOOP         // sender = current context
    );

    // Set up home context linkage for outer variable access
    _blockContextMapping[blockActivationOOP] = homeContextOOP;

    // Execute block
    _activeContextOOP = blockActivationOOP;
    return ExecuteBytecodeLoop();
}
```

#### PUSH_RECEIVER inside block:

```csharp
// Use home context's receiver
int receiverIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "receiver");

if (_blockContextMapping.TryGetValue(_activeContextOOP, out var homeContextOOP))
{
    // Block context - get receiver from home
    receiverOOP = _heapManager.GetInstanceVariable(homeContextOOP, receiverIndex);
}
else
{
    // Regular method - get receiver from current context
    receiverOOP = _heapManager.GetInstanceVariable(_activeContextOOP, receiverIndex);
}

Push(receiverOOP);
```

#### PUSH_OUTER depth index (future):

```csharp
// Walk outer context chain
var currentContext = _activeContextOOP;
for (int i = 0; i < depth; i++)
{
    if (_blockContextMapping.TryGetValue(currentContext, out var home))
    {
        currentContext = home;
    }
    else
    {
        throw new InvalidOperationException($"Cannot resolve outer context at depth {depth}");
    }
}

// Read from outer context indexed slot
var valueOOP = _heapManager.GetIndexedOOP(currentContext, index);
Push(valueOOP);
```

### Execution Example Walkthrough

#### Setup: Compiled Artifacts
- Outer CompiledMethod `compute` with literal table where `literal[1]` = block CompiledMethod B
- Block CompiledMethod B contains bytecodes: `pushOuter 0 0` (read `a`), `send #+`, `returnTop`

#### Runtime Execution of `compute`

**1. Enter `compute` method**:
```csharp
// VM allocates MethodContext M
var M = CreateMethodContext(
    computeMethodOOP,
    blockTestInstance,  // receiver
    args: [],
    sender: callerContext
);
// M.indexed[0] will hold temp 'a'
_activeContextOOP = M;
```

**2. Execute `a := 10`**:
```csharp
// pushLiteral 0 → push SmallInteger(10)
Push(SmallInteger(10));

// popStoreTemp 0 → store into M.indexed[0]
var valueOOP = Pop();
_heapManager.SetIndexedOOP(M, 0, valueOOP);  // a = 10
```

**3. Execute `pushLiteral 1` (block literal)**:
```csharp
// Literal 1 is CompiledMethod B (block)
var literalOOP = GetMethodLiteral(computeMethodOOP, 1);

// VM detects this is a block literal
if (IsBlockLiteral(literalOOP))  // Check if localId starts with "block:"
{
    // Create BlockClosure C wrapping the block
    var C = CreateBlockClosure(
        method: literalOOP,           // Block CompiledMethod B
        homeContext: M,               // Capture current context (lexical env)
        returnContext: M,             // For non-local returns
        numArgs: 0                    // From block header
    );

    Push(C);  // Push BlockClosure, not raw CompiledMethod
}
```

**4. Execute `send #value 0`**:
```csharp
// Pop receiver (BlockClosure C) and invoke
var receiverOOP = Pop();  // C

if (IsBlockClosure(receiverOOP))
{
    // Extract components from BlockClosure C
    var blockMethodOOP = GetInstanceVariable(C, methodIndex);         // B
    var homeContextOOP = GetInstanceVariable(C, homeContextIndex);    // M
    var returnContextOOP = GetInstanceVariable(C, returnContextIndex);// M

    // Get receiver from home context (for pushReceiver inside block)
    var receiverFromHome = GetInstanceVariable(M, receiverIndex);  // blockTestInstance

    // Create block activation BA (MethodContext for block execution)
    var BA = CreateMethodContext(
        blockMethodOOP,       // Block CompiledMethod B
        receiverFromHome,     // blockTestInstance
        args: [],             // No arguments
        sender: M             // Current context
    );

    // Register home context mapping (for outer variable access)
    _blockContextMapping[BA] = homeContextOOP;  // BA → M

    // Switch to block activation
    _activeContextOOP = BA;

    // Execute block bytecodes...
}
```

**5. Inside block B bytecode execution**:

```csharp
// Bytecode: pushOuter 0 0 (or pushTemp if accessing home context directly)
// Read 'a' from home context M, indexed slot 0

var homeContextOOP = _blockContextMapping[BA];  // M
var valueOOP = _heapManager.GetIndexedOOP(homeContextOOP, 0);  // Get 'a' = 10
Push(valueOOP);

// Bytecode: pushLiteral 0
Push(SmallInteger(1));

// Bytecode: send #+ 1
var arg = Pop();    // 1
var recv = Pop();   // 10
var result = recv + arg;  // 11 (via primitive or send)
Push(result);

// Bytecode: returnTop
// Block-local return: return value from BA to caller (M)
var returnValue = Pop();  // 11
_activeContextOOP = BA.sender;  // Return to M
Push(returnValue);  // Push 11 onto M's stack
```

**6. Back in `compute` method**:
```csharp
// Bytecode: returnTop
// Return 11 from compute method
var finalResult = Pop();  // 11
_activeContextOOP = M.sender;  // Return to caller
Push(finalResult);
```

### Non-Local Return Semantics

#### Example with Non-Local Return

```smalltalk
BlockTest >> testNonLocal
    | result |
    result := self helperWithBlock: [ ^ 42 ].
    ^ result  "Never reached!"

BlockTest >> helperWithBlock: aBlock
    ^ aBlock value + 100  "Never reached either!"
```

#### Compilation

**`testNonLocal` CompiledMethod**:
```
Bytecode:
  pushSelf
  pushLiteral 0        ; Block with returnNonLocal
  send #helperWithBlock: 1
  popStoreTemp 0       ; Never executed
  pushTemp 0
  returnTop            ; Never executed
```

**Block CompiledMethod**:
```
Bytecode:
  pushLiteral 0        ; SmallInteger(42)
  returnNonLocal       ; Non-local return!
```

**`helperWithBlock:` CompiledMethod**:
```
Bytecode:
  pushTemp 0           ; aBlock
  send #value 0
  pushLiteral 0        ; SmallInteger(100)
  send #+ 1
  returnTop            ; Never executed
```

#### Runtime Execution

**1. Enter `testNonLocal`**:
- Create MethodContext T for `testNonLocal`
- T is the context where the block is DEFINED

**2. Create BlockClosure**:
```csharp
// When pushLiteral pushes the block
var blockClosure = CreateBlockClosure(
    method: blockCompiledMethod,
    homeContext: T,           // Define context
    returnContext: T,         // Non-local return target!
    numArgs: 0
);
```

**3. Pass block to `helperWithBlock:`**:
- Create MethodContext H for `helperWithBlock:`
- H.indexed[0] = blockClosure

**4. Inside `helperWithBlock:`, invoke block**:
- Create block activation BA
- BA.home = T (from blockClosure)
- BA.sender = H

**5. Execute `returnNonLocal` inside block**:
```csharp
// Bytecode: returnNonLocal
if (bytecode == RETURN_NON_LOCAL)
{
    var returnValue = Pop();  // 42
    var returnContextOOP = GetBlockReturnContext(BA);  // T (from BlockClosure)

    // Validate return context is still on stack
    if (!IsContextOnStack(returnContextOOP))
    {
        throw new InvalidOperationException(
            "Attempt to return to dead context - method has already returned"
        );
    }

    // Unwind contexts: BA → H → T
    // Return directly to T as if T's method returned 42
    _activeContextOOP = returnContextOOP.sender;  // Caller of testNonLocal
    Push(returnValue);  // Push 42 as return value

    // H's stack frame abandoned (never returns from helperWithBlock:)
    // T's remaining bytecodes never execute
}
```

#### Invalid Non-Local Return

```smalltalk
BlockTest >> makeBlock
    ^ [ ^ 42 ]  "Returns a block"

BlockTest >> testInvalid
    | block |
    block := self makeBlock.  "makeBlock returns, its context is gone"
    ^ block value  "ERROR: returnContext T no longer on stack!"
```

**Error Handling**:
```csharp
if (!IsContextOnStack(returnContextOOP))
{
    // The defining method (makeBlock) has already returned
    // Its MethodContext T has been discarded
    // Cannot perform non-local return

    SignalError("CannotReturn",
        "Attempt to return through an unwind-protected or already-returned method");
}
```

### Design Variants and Tradeoffs

#### Option 1: Keep MethodContext Alive
**Approach**: BlockClosure.homeContext references the MethodContext; GC keeps it alive

**Pros**:
- Simple implementation
- Block reads directly from live home context
- No copying of temporaries needed

**Cons**:
- Large contexts retained in memory
- Increased GC pressure if many blocks created
- MethodContext cannot be reclaimed until all blocks referencing it are GC'd

**Our Current Implementation**: Uses this approach

#### Option 2: Materialize Captured Locals in BlockClosure
**Approach**: Copy captured temporaries into BlockClosure at creation time

**Pros**:
- MethodContext can be reclaimed immediately after method returns
- Explicit closure semantics (clear what's captured)
- Reduced memory for short-lived contexts

**Cons**:
- Requires copying values on each block creation
- More VM complexity (track which variables are captured)
- Mutable captured variables need special handling (box them)

#### Option 3: Hybrid Approach
**Approach**: Capture only necessary variables; leave receiver and other data in MethodContext

**Pros**:
- Balance between memory and complexity
- Can optimize based on block usage patterns

**Cons**:
- Most complex implementation
- Requires sophisticated analysis at compile time

### Practical Implementation Checklist

#### Compiler Requirements
- [ ] Record block ownership in blockOwnerMap for each block literal
- [ ] Track lexical nesting depth for nested blocks
- [ ] Emit RETURN_NON_LOCAL bytecode when `^` appears inside block
- [ ] Mark block CompiledMethods with identifiable localId format (`block:...`)

#### Coldstart Requirements
- [ ] Populate blockOwnerMap during Phase 8 (Method Installation)
- [ ] Validate ownership relationships in Phase 0X
- [ ] Ensure all blocks have valid owner method references

#### VM Requirements: Block Creation
- [ ] Detect block literals in pushLiteral handler (check localId prefix)
- [ ] Wrap block CompiledMethod in BlockClosure with current _activeContextOOP as homeContext
- [ ] Store returnContext (same as homeContext for most cases)
- [ ] Extract numArgs from block method header

#### VM Requirements: Block Invocation
- [ ] Detect BlockClosure as message receiver for #value, #value:, etc.
- [ ] Extract homeContext from BlockClosure
- [ ] Create block activation (MethodContext or BlockContext)
- [ ] Register home context mapping in _blockContextMapping
- [ ] Get receiver from homeContext (not from caller)

#### VM Requirements: Bytecode Execution
- [ ] pushReceiver: Use home context receiver when in block
- [ ] pushIvar: Use home context receiver when in block
- [ ] pushOuter: Walk home context chain for outer variables (future)
- [ ] returnTop: Block-local return (normal return from block activation)
- [ ] returnNonLocal: Unwind to returnContext with validation

#### VM Requirements: Context Management
- [ ] Validate returnContext is on stack before non-local return
- [ ] Unwind intermediate contexts properly
- [ ] Signal error for invalid non-local returns
- [ ] GC: Keep homeContext alive while BlockClosure exists

#### Diagnostic Requirements
- [ ] Print BlockClosure with homeContext information
- [ ] Show captured environment (home context indexed slots)
- [ ] Trace block invocation and return paths
- [ ] Validate ownership chains during debugging

## Implementation Phases

### Phase 1: Class Definition ✅ COMPLETE
- [x] Define BlockClosure class in Smalltalk-80.st
- [x] Add complete API (value, value:, value:value:, etc.)
- [x] Add control structures (whileTrue:, whileFalse:, fork)
- [x] Add introspection methods

### Phase 2: Validation Infrastructure ✅ COMPLETE
- [x] Create BlockValidatorTest with comprehensive test suite
- [x] Create Phase0X_BlockOwnershipValidation for coldstart
- [x] Document architecture in CLAUDE.md

### Phase 3: Compiler Integration ⏳ PENDING
- [ ] Modify compiler to emit BlockClosure creation code
- [ ] Generate BlockClosure instances during cold start
- [ ] Update blockOwnerMap to include BlockClosure references

### Phase 4: VM Integration ⏳ PENDING
- [ ] Modify pushLiteral bytecode handler to detect block literals
- [ ] Wrap block literals in BlockClosure with current _activeContextOOP
- [ ] Update InvokeBlockInternal to extract homeContext from BlockClosure
- [ ] Implement primitive 207 (valueWithArguments:)

### Phase 5: Testing & Validation ⏳ PENDING
- [ ] Compile BlockClosure.st and BlockValidatorTest.st
- [ ] Run BlockValidatorTest new runAllTests
- [ ] Integrate Phase0X into coldstart sequence
- [ ] Verify init expressions work with BlockClosure

### Phase 6: Non-Local Returns ⏳ FUTURE
- [ ] Implement returnContext unwinding
- [ ] Detect invalid returns (after method has exited)
- [ ] Add tests for non-local returns

## Key Files

### Smalltalk-80 Source
- `BlockClosure.st` - Class definition with full API
- `BlockValidatorTest.st` - Comprehensive test suite

### C# Implementation
- `Phase0X_BlockOwnershipValidation.cs` - Coldstart validation phase
- `VirtualMachine.cs` - Lines 1492-1550: InvokeBlockInternal (needs BlockClosure support)
- `VirtualMachine.cs` - Lines 308-323: pushTemporary (fixed for proper temp access)
- `VirtualMachine.cs` - Lines 286-306: pushIvar (needs BlockClosure home context)

### Documentation
- `CLAUDE.md` - Updated with BlockClosure architecture
- `BlockClosureArchitecture.md` - This file

## Critical Implementation Details

### 1. Block Literal Detection in pushLiteral

**Current Code** (VirtualMachine.cs:333-339):
```csharp
if (BytecodeUtil.IsPushLiteralConst(bytecode))
{
    int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
    var literal = GetMethodLiteral(methodOOP, index + 1);
    Push(literal);
    return true;
}
```

**Target Code**:
```csharp
if (BytecodeUtil.IsPushLiteralConst(bytecode))
{
    int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
    var literal = GetMethodLiteral(methodOOP, index + 1);

    // Check if this literal is a block (CompiledMethod in blockOwnerMap)
    if (IsBlockLiteral(literal))
    {
        // Wrap in BlockClosure, capturing current context
        var closure = CreateBlockClosure(literal, _activeContextOOP);
        Push(closure);
    }
    else
    {
        Push(literal);
    }
    return true;
}
```

### 2. BlockClosure Invocation in InvokeBlockInternal

**Current Code** (VirtualMachine.cs:1492-1499):
```csharp
// Step 2: Determine contexts
var homeContextOOP = FindMethodContextByLocalId(ownerLocalId);
if (homeContextOOP == default)
{
    throw new InvalidOperationException($"No method context found for block owner {ownerLocalId}");
}
```

**Target Code**:
```csharp
// Step 2: Extract homeContext from BlockClosure
var homeContextOOP = blockClosure.homeContext;
if (homeContextOOP == default || homeContextOOP == GlobalRegistry.NilOOP)
{
    throw new InvalidOperationException("BlockClosure has invalid homeContext");
}
```

### 3. Primitive 207: valueWithArguments:

**New Primitive**:
```csharp
public SmalltalkOOP ValueWithArguments(SmalltalkOOP blockClosureOOP, SmalltalkOOP argumentArrayOOP)
{
    // Extract closure components
    var methodOOP = GetInstanceVariable(blockClosureOOP, methodIndex);
    var homeContextOOP = GetInstanceVariable(blockClosureOOP, homeContextIndex);
    var numArgsOOP = GetInstanceVariable(blockClosureOOP, numArgsIndex);

    // Extract arguments from array
    var args = ExtractArrayElements(argumentArrayOOP);

    // Validate argument count
    int expectedArgs = numArgsOOP.SmallIntegerValue();
    if (args.Length != expectedArgs)
    {
        throw new ArgumentException($"Block expects {expectedArgs} args, got {args.Length}");
    }

    // Invoke block using existing infrastructure
    return InvokeBlockInternal(methodOOP, args, homeContextOOP);
}
```

## Benefits of BlockClosure Architecture

### 1. Lexical Fidelity
- Blocks correctly access their defining method's receiver and instance variables
- No need to search context chains at invocation time
- Immutable capture at creation time

### 2. Performance
- Context lookup happens once (at creation) instead of every invocation
- Eliminates FindMethodContextByLocalId overhead
- Cleaner, faster execution path

### 3. Correctness
- Cannot invoke a block after its defining method has exited (context validation)
- Proper GC semantics (blocks keep their contexts alive)
- Type safety (BlockClosure vs. CompiledMethod distinction)

### 4. Future Extensions
- **Outer temporaries**: Capture temps from enclosing blocks
- **Non-local returns**: Unwind to returnContext
- **Continuations**: Full continuation support
- **Debugging**: Introspection of captured environment
- **Optimization**: JIT compilation of hot blocks

## Testing Strategy

### 1. Unit Tests (BlockValidatorTest)
```smalltalk
testSimpleBlock          "[ 42 ] value = 42"
testNestedBlock          "[ [ 42 ] value ] value = 42"
testLexicalScope         "Access outer variables"
testBlockPassing         "Pass through multiple methods"
testInstanceVariable     "Access self's instance variables"
testReceiverAccess       "Access self correctly"
```

### 2. Integration Tests (Init Expressions)
- init#1: WindowsFileSystem.initialize ✅
- init#2: WindowsFileDirectory.initialize ✅
- init#3: BinaryChoiceView.initialize (uses nested blocks in Point>>=)

### 3. Validation Tests (Phase0X)
- All blocks have valid owners
- No orphaned blocks
- No circular ownership
- Complete ownership chains

## Migration Path

### Step 1: Add BlockClosure to Compiler
- Recognize block literals during compilation
- Emit BlockClosure creation metadata
- Preserve existing CompiledMethod structure

### Step 2: Create BlockClosure Instances During Cold Start
- After Phase 8 (Method Installation)
- Wrap all block literals in BlockClosure objects
- Populate homeContext with defining method's context stub

### Step 3: Update VM Bytecode Handlers
- pushLiteral: Detect and wrap blocks
- InvokeBlock: Extract homeContext from closure
- Remove FindMethodContextByLocalId dependency

### Step 4: Test & Validate
- Run BlockValidatorTest suite
- Run init expressions
- Run Phase0X validation

## Known Issues & Future Work

### Current Limitations
- [ ] Non-local returns not implemented
- [ ] Outer temporary capture not supported
- [ ] Context validity not checked (can invoke after method exits)
- [ ] No optimization for hot blocks

### Future Enhancements
- [ ] Add BlockClosure>>isValid to check if home context is still alive
- [ ] Implement ensure: and ifCurtailed: exception handling
- [ ] Add full stack unwinding for non-local returns
- [ ] Consider BlockContext vs. BlockClosure unification

## References

- **Blue Book**: Smalltalk-80: The Language and its Implementation (Goldberg & Robson)
  - Section 18: Block Semantics
  - Section 30: Virtual Machine Implementation
- **CLAUDE.md**: Development guidelines and recent accomplishments
- **Commits**:
  - 1872089: fix: correct block context home resolution and temporary variable access
  - 4c8d4ea: feat: add BlockClosure class, validator tests, and ownership validation

## Conclusion

The BlockClosure architecture provides a solid foundation for implementing proper lexical closures in our Smalltalk-80 interpreter. By separating the runtime closure object (BlockClosure) from the immutable bytecode (CompiledMethod), we achieve:

1. **Correctness**: Blocks access their lexical environment accurately
2. **Performance**: Context lookup happens at creation, not invocation
3. **Extensibility**: Foundation for non-local returns, continuations, and optimization
4. **Debuggability**: Introspection and validation tools

The implementation is phased to allow incremental integration and testing, with clear rollback points if issues arise.
