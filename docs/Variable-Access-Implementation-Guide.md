# Variable Access Implementation Guide

**Last Updated**: 2025-11-13
**Status**: IMPLEMENTATION GUIDE
**Audience**: Developers implementing the Smalltalk-80 compiler and interpreter

## Purpose

This document provides step-by-step implementation guidance for the unified temp frame variable access architecture. It translates the canonical specification into concrete implementation steps.

**Prerequisites**: Read [Unified-Temp-Frame-Specification.md](./Unified-Temp-Frame-Specification.md) first.

---

## Table of Contents

1. [Compiler Implementation](#1-compiler-implementation)
2. [Interpreter Implementation](#2-interpreter-implementation)
3. [Primitives Implementation](#3-primitives-implementation)
4. [Testing Strategy](#4-testing-strategy)
5. [Common Implementation Errors](#5-common-implementation-errors)

---

## 1. Compiler Implementation

### 1.1 TempFrameAllocator Class

**Purpose**: Assign absolute indices to all variables across method and block scopes.

**Implementation**:

```csharp
public class TempFrameAllocator
{
    private int _nextSlot = 0;
    private int _highestSlotIndex = -1;
    private readonly Dictionary<string, int> _allocatedSlots = new();
    private readonly Stack<ScopeInfo> _scopeStack = new();

    private class ScopeInfo
    {
        public HashSet<int> AllocatedSlots { get; } = new();
        public int StartSlot { get; set; }
        public bool IsSequential { get; set; }  // Can reuse slots?
    }

    // Step 1: Enter method scope
    public void EnterMethodScope(List<string> argNames, List<string> tempNames)
    {
        var scope = new ScopeInfo { StartSlot = _nextSlot, IsSequential = false };
        _scopeStack.Push(scope);

        // Allocate slots for method args
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }

        // Allocate slots for method temps
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }
    }

    // Step 2: Enter block scope
    public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
    {
        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = !isNested  // Sequential blocks can reuse
        };
        _scopeStack.Push(scope);

        // Allocate slots for BOTH block args AND temps in unified frame
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }

        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }
    }

    // Step 3: Exit scope (potentially reuse slots)
    public void ExitScope()
    {
        var scope = _scopeStack.Pop();

        if (scope.IsSequential && scope.AllocatedSlots.Count > 0)
        {
            // Reuse slots from this sequential block
            int minSlot = scope.AllocatedSlots.Min();
            _nextSlot = minSlot;

            // Remove variables from allocation map
            var varsToRemove = _allocatedSlots
                .Where(kvp => scope.AllocatedSlots.Contains(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var varName in varsToRemove)
            {
                _allocatedSlots.Remove(varName);
            }
        }
    }

    // Step 4: Get assigned index for variable
    public int GetSlotIndex(string varName)
    {
        if (_allocatedSlots.TryGetValue(varName, out int slot))
        {
            return slot;
        }
        throw new InvalidOperationException($"Variable '{varName}' not found");
    }

    // Step 5: Get max frame size for method header
    public int GetMaxFrameSize()
    {
        return _highestSlotIndex + 1;  // Convert 0-based index to count
    }
}
```

### 1.2 VariableResolver Integration

**Purpose**: Resolve variable names to temp frame indices during compilation.

```csharp
public class VariableResolver
{
    public ResolutionResult ResolveVariable(
        string varName,
        CompilationEnvironment context,
        TempFrameAllocator allocator)
    {
        // Step 1: Check pseudo-variables (self, super, true, false, nil, thisContext)
        if (TryResolvePseudoVariable(varName, out var pseudoResult))
            return pseudoResult;

        // Step 2: Try to get index from allocator
        if (allocator.TryGetSlotIndex(varName, out int index))
        {
            // Determine variable type and assignability
            VariableType type = DetermineVariableType(varName, context);
            bool isAssignable = type != VariableType.MethodArgument &&
                               type != VariableType.BlockArgument;

            return new ResolutionResult(type, index, isAssignable, varName);
        }

        // Step 3: Check instance variables
        if (context.InstanceVariables.TryGetValue(varName, out int fieldIndex))
        {
            return new ResolutionResult(
                VariableType.InstanceVariable,
                fieldIndex,
                true,
                varName);
        }

        // Step 4: Check class/pool/global variables
        // ... (literal association handling)

        throw new InvalidOperationException($"Variable '{varName}' not found");
    }

    private VariableType DetermineVariableType(
        string varName,
        CompilationEnvironment context)
    {
        // Check if it's an argument or temporary
        if (context.ScopeDepth == 0)
        {
            // Method scope
            if (context.LocalArgumentIndices.ContainsKey(varName))
                return VariableType.MethodArgument;
            else
                return VariableType.MethodTemporary;
        }
        else
        {
            // Block scope
            if (context.LocalArgumentIndices.ContainsKey(varName))
                return VariableType.BlockArgument;
            else
                return VariableType.BlockTemporary;
        }
    }
}
```

### 1.3 Bytecode Emission

**Purpose**: Emit uniform PUSH_TEMP/STORE_TEMP for all variable access.

```csharp
public class BytecodeEmitter
{
    public void EmitVariablePush(ResolutionResult resolution)
    {
        switch (resolution.Type)
        {
            case VariableType.MethodArgument:
            case VariableType.MethodTemporary:
            case VariableType.BlockArgument:
            case VariableType.BlockTemporary:
                // ALL temp frame variables use PUSH_TEMP
                EmitPushTemp(resolution.Index);
                break;

            case VariableType.PseudoVariable:
                EmitPushPseudo(resolution.Name);
                break;

            case VariableType.InstanceVariable:
                EmitPushInstanceVariable(resolution.Index);
                break;

            // ... other types
        }
    }

    public void EmitVariableStore(ResolutionResult resolution)
    {
        if (!resolution.IsAssignable)
        {
            throw new CompilationException(
                $"Cannot assign to read-only variable '{resolution.Name}'");
        }

        switch (resolution.Type)
        {
            case VariableType.MethodTemporary:
            case VariableType.BlockTemporary:
                // ALL temp frame temps use STORE_TEMP
                EmitStoreTemp(resolution.Index);
                break;

            case VariableType.InstanceVariable:
                EmitStoreInstanceVariable(resolution.Index);
                break;

            // ... other types
        }
    }

    private void EmitPushTemp(int index)
    {
        if (index < 16)
        {
            // Short form: 0x10 + index (bytecodes 16-31)
            EmitByte((byte)(0x10 + index));
        }
        else
        {
            // Extended form: 0x80 0x00 index
            EmitByte(0x80);
            EmitByte(0x00);
            EmitByte((byte)index);
        }
    }

    private void EmitStoreTemp(int index)
    {
        if (index < 8)
        {
            // Short form: 0x68 + index (bytecodes 104-111)
            EmitByte((byte)(0x68 + index));
        }
        else
        {
            // Extended form: 0x81 0x40 index
            EmitByte(0x81);
            EmitByte(0x40);
            EmitByte((byte)index);
        }
    }
}
```

### 1.4 Block Compilation

```csharp
public void CompileBlock(BlockNode block, CompilationEnvironment context)
{
    // Step 1: Determine if block is nested or sequential
    bool isNested = IsBlockNested(block, context);

    // Step 2: Enter block scope in allocator
    allocator.EnterBlockScope(
        block.Arguments.Select(a => a.Name).ToList(),
        block.Temporaries.Select(t => t.Name).ToList(),
        isNested);

    // Step 3: Create new compilation environment for block
    var blockContext = new CompilationEnvironment
    {
        Parent = context,
        ScopeDepth = context.ScopeDepth + 1,
        // ... copy other context
    };

    // Step 4: Compile block body
    foreach (var statement in block.Statements)
    {
        CompileStatement(statement, blockContext);
    }

    // Step 5: Emit blockCopy: send
    int nargs = block.Arguments.Count;
    EmitBlockCopy(nargs);

    // Step 6: Exit block scope
    allocator.ExitScope();
}

private void EmitBlockCopy(int nargs)
{
    // Emit send of blockCopy: nargs
    // This will invoke Primitive 80 at runtime
    EmitPushConstant(nargs);
    EmitSend("blockCopy:", 1);
}
```

### 1.5 Compiler Audit: Correct blockCopy: Emission

**Purpose**: Verify that your compiler correctly emits `blockCopy:` messages for block literals.

**Audit Steps:**

**1. Block Literal Detection**

Verify parser creates BlockNode for `[...]` syntax:

```csharp
public class BlockNode : ASTNode
{
    public List<ParameterNode> Arguments { get; set; }
    public List<TemporaryNode> Temporaries { get; set; }
    public List<StatementNode> Body { get; set; }
    public int NestingDepth { get; set; }
}
```

**2. Offset Calculation**

Calculate byte offset from method start to block body:

```csharp
int CalculateBlockBodyOffset(BlockNode block, BytecodeEmitter emitter)
{
    // Offset = current position in bytecode stream + size of blockCopy: send
    int currentPosition = emitter.CurrentPosition;
    int blockCopySize = 1 + 2 + 3;  // PUSH_SELF + PUSH_CONST + SEND
    return currentPosition + blockCopySize;
}
```

**3. Emit blockCopy: Sequence**

```csharp
void EmitBlockLiteral(BlockNode block)
{
    // Calculate offset to block body
    int offset = CalculateBlockBodyOffset(block);

    // Emit: thisContext blockCopy: offset
    EmitPushSelf();                    // PUSH_SELF (1 byte)
    EmitPushConstant(offset);          // PUSH_CONST offset (2 bytes)
    EmitSend("blockCopy:", 1);         // SEND blockCopy: 1 (3 bytes)

    // Mark where block body will start
    int actualBodyStart = emitter.CurrentPosition;
    Debug.Assert(actualBodyStart == offset, "Offset calculation mismatch!");

    // Emit block body
    foreach (var statement in block.Body)
    {
        EmitStatement(statement);
    }

    // Block must end with return
    if (!EndsWithReturn(block.Body))
    {
        EmitReturnStackTop();
    }
}
```

**4. Verify Selector Resolution**

```csharp
void VerifyBlockCopySelector()
{
    // blockCopy: must resolve to ContextPart>>blockCopy:
    var method = LookupMethod("ContextPart", "blockCopy:");
    Debug.Assert(method != null, "blockCopy: method not found!");
    Debug.Assert(method.PrimitiveIndex == 80, "blockCopy: must have primitive 80!");

    // Selector must be in literal frame
    Debug.Assert(literalFrame.Contains("blockCopy:"), "blockCopy: not in literals!");
}
```

**5. Test with Nested Blocks**

```csharp
void TestNestedBlockOffsets()
{
    // Source:
    // [:a | [:b | a + b] value: 2] value: 1

    // Expected bytecodes:
    // PUSH_SELF
    // PUSH_CONST 8          ; Offset to outer block body
    // SEND blockCopy: 1
    // PUSH_CONST 1
    // SEND value: 1
    //
    // === Outer block body (offset 8) ===
    // PUSH_SELF
    // PUSH_CONST 20         ; Offset to INNER block body (from method start)
    // SEND blockCopy: 1
    // PUSH_CONST 2
    // SEND value: 1
    // RETURN_STACK_TOP
    //
    // === Inner block body (offset 20) ===
    // PUSH_TEMP 0           ; a
    // PUSH_TEMP 1           ; b
    // SEND + 1
    // RETURN_STACK_TOP

    VerifyBytecodeSequence(compiledMethod);
}
```

**Verification Checklist:**

- [ ] Parser creates BlockNode for each `[...]`
- [ ] Offset calculated relative to METHOD start (not block start)
- [ ] `PUSH_SELF` emitted before `PUSH_CONSTANT offset`
- [ ] Offset value matches actual block body position
- [ ] `blockCopy:` selector in literal frame
- [ ] Block body uses absolute temp frame indices
- [ ] Block body ends with RETURN_STACK_TOP
- [ ] Nested blocks have correct offsets from method start

**What to Look For in Bytecode Dump:**

```
Method: testBlock
Bytecodes:
  0: PUSH_SELF            ; thisContext
  1: PUSH_CONST 8         ; offset
  3: SEND blockCopy: 1    ; Create block
  6: PUSH_CONST 5         ; Argument
  8: SEND value: 1        ; Activate block

  ; === Block body starts at offset 8 ===
  11: PUSH_TEMP 0         ; Block argument
  12: PUSH_CONST 1
  14: SEND + 1
  17: RETURN_STACK_TOP
```

Verify:
- Offset (8) matches where block body actually starts
- Block body uses PUSH_TEMP with unified frame indices
- Sequence is correct: PUSH_SELF, PUSH_CONST, SEND

---

## 2. Interpreter Implementation

### 2.1 GetHome Helper

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

### 2.2 PushTemporaryVariable

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

### 2.3 StoreTemporaryVariable

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

### 2.4 Context Retention Implementation

**Purpose**: Ensure MethodContexts are retained when blocks escape their defining method.

**Implementation Steps:**

**Step 1: Mark Escaping Blocks**

During method return, check if any blocks reference the returning context:

```csharp
private void ReturnFromMethod(SmalltalkOOP returnValue)
{
    SmalltalkOOP returningContext = _activeContext;

    // Check if context must be retained
    if (MustRetainContext(returningContext))
    {
        MarkContextAsRetained(returningContext);
    }

    // Resume sender
    SmalltalkOOP sender = GetSender(returningContext);
    _activeContext = sender;
    Push(returnValue);
}
```

**Step 2: Detect Live Block References**

```csharp
private bool MustRetainContext(SmalltalkOOP methodContext)
{
    // Context must be retained if:
    // 1. Return value is a BlockContext with home = methodContext
    // 2. Any reachable object holds a BlockContext with home = methodContext

    // Check return value
    SmalltalkOOP returnValue = Peek();
    if (IsBlockContext(returnValue))
    {
        SmalltalkOOP home = GetHome(returnValue);
        if (home == methodContext)
            return true;
    }

    // Check all reachable objects (conservative approach)
    return _heapManager.HasLiveBlockReferencingContext(methodContext);
}
```

**Step 3: Mark Context as Retained**

```csharp
private void MarkContextAsRetained(SmalltalkOOP context)
{
    // Set flag indicating context should not be recycled
    SetContextRetainedFlag(context, true);

    // Move context to heap (if using context pooling)
    _heapManager.RetainContext(context);
}
```

**Step 4: Garbage Collection Integration**

```csharp
// During GC mark phase
void MarkReachableObjects(SmalltalkOOP root)
{
    if (IsBlockContext(root))
    {
        // Mark the block itself
        Mark(root);

        // Mark the home MethodContext (critical!)
        SmalltalkOOP home = GetHome(root);
        Mark(home);

        // This keeps MethodContext alive as long as block is reachable
    }
}
```

**Step 5: Context Pooling (Optional Optimization)**

```csharp
private SmalltalkOOP AllocateMethodContext(int tempFrameSize)
{
    // Try to reuse a context from pool
    SmalltalkOOP context = _contextPool.Allocate();

    if (context.IsNull())
    {
        // No pooled context available, allocate new
        context = _heapManager.InstantiateClass(
            _methodContextClass,
            indexedSlotCount: tempFrameSize);
    }

    return context;
}

private void RecycleContext(SmalltalkOOP context)
{
    // Only recycle if NOT retained
    if (!IsContextRetained(context))
    {
        _contextPool.Recycle(context);
    }
}
```

**Runtime Invariants:**

1. **Block Home Pointer**: Every BlockContext has valid `home` pointer to MethodContext
2. **Context Lifetime**: MethodContext lives at least as long as any block referencing it
3. **GC Tracing**: Garbage collector traces from blocks to their home contexts
4. **No Dangling Pointers**: Variable access via `home` always succeeds

**Testing Context Retention:**

```csharp
[Test]
public void TestEscapingBlock()
{
    // Create block that captures method variable
    Execute(@"
        testMethod
          | count |
          count := 0.
          ^[:inc | count := count + inc]
    ");

    SmalltalkOOP block = Pop();

    // Method has returned - context should be retained
    Assert.IsTrue(IsBlockContext(block));
    SmalltalkOOP home = GetHome(block);
    Assert.IsTrue(IsMethodContext(home));
    Assert.IsTrue(IsContextRetained(home));

    // Block should execute successfully
    Push(block);
    Push(MakeSmallInteger(5));
    Send("value:", 1);

    SmalltalkOOP result = Pop();
    Assert.AreEqual(5, GetSmallInteger(result));

    // Execute again - state should persist
    Push(block);
    Push(MakeSmallInteger(3));
    Send("value:", 1);

    result = Pop();
    Assert.AreEqual(8, GetSmallInteger(result));  // 5 + 3
}
```

**Common Errors:**

1. **Not Checking Return Value**: Forgetting to check if returned value is a block
2. **Recycling Too Early**: Recycling context before checking for live blocks
3. **Missing GC Trace**: Not tracing from BlockContext to home in GC
4. **Pool Corruption**: Recycling retained contexts back to pool

---

## 3. Primitives Implementation

### 3.1 Primitive 80: blockCopy:

**Purpose**: Create a BlockContext with home pointer to current MethodContext.

```csharp
[Primitive(80, "ContextPart>>blockCopy:")]
private void Primitive_BlockCopy()
{
    // Pop argument: number of block parameters
    SmalltalkOOP nargsOOP = Pop();
    int nargs = GetSmallInteger(nargsOOP);

    // Get current context (must be MethodContext)
    SmalltalkOOP homeContext = _activeContext;

    // Determine initialIP (bytecode offset for block body)
    int initialIP = _instructionPointer + 3;  // Skip jump instruction

    // Allocate BlockContext
    SmalltalkOOP blockContext = AllocateBlockContext(
        home: homeContext,
        initialIP: initialIP,
        nargs: nargs);

    // Push BlockContext onto stack
    Push(blockContext);
}

private SmalltalkOOP AllocateBlockContext(
    SmalltalkOOP home,
    int initialIP,
    int nargs)
{
    // BlockContext has NO indexed slots (no variable storage)
    SmalltalkOOP blockContext = InstantiateClass(
        GetWellKnownClass("BlockContext"),
        indexedSlotCount: 0);  // CRITICAL: 0 indexed slots

    // Set instance variables
    SetInstanceVariable(blockContext, BLOCKCONTEXT_HOME_INDEX, home);
    SetInstanceVariable(blockContext, BLOCKCONTEXT_INITIALIP_INDEX, MakeSmallInteger(initialIP));
    SetInstanceVariable(blockContext, BLOCKCONTEXT_NARGS_INDEX, MakeSmallInteger(nargs));

    return blockContext;
}
```

### 3.2 Primitive 81: value / value: / value:value: etc.

**Purpose**: Activate BlockContext and store arguments into MethodContext temp frame.

```csharp
[Primitive(81, "BlockContext>>value")]
[Primitive(81, "BlockContext>>value:")]
[Primitive(81, "BlockContext>>value:value:")]
// ... (all value: variants)
private void Primitive_BlockValue()
{
    // Pop arguments from stack
    int argCount = GetArgumentCountForCurrentPrimitive();
    SmalltalkOOP[] args = new SmalltalkOOP[argCount];
    for (int i = argCount - 1; i >= 0; i--)
    {
        args[i] = Pop();
    }

    // Pop receiver (BlockContext)
    SmalltalkOOP blockContext = Pop();

    // Get home MethodContext
    SmalltalkOOP homeContext = GetInstanceVariable(blockContext, BLOCKCONTEXT_HOME_INDEX);

    // Get number of expected arguments
    SmalltalkOOP nargsOOP = GetInstanceVariable(blockContext, BLOCKCONTEXT_NARGS_INDEX);
    int nargs = GetSmallInteger(nargsOOP);

    // Validate argument count
    if (args.Length != nargs)
    {
        PrimitiveFail();
        return;
    }

    // CRITICAL: Store arguments into HOME MethodContext temp frame
    // Arguments are stored at pre-assigned indices determined by compiler
    int startIndex = GetBlockArgumentStartIndex(blockContext);
    for (int i = 0; i < args.Length; i++)
    {
        WriteIndexedSlot(homeContext, startIndex + i, args[i]);
    }

    // Get initialIP
    SmalltalkOOP initialIPOOP = GetInstanceVariable(blockContext, BLOCKCONTEXT_INITIALIP_INDEX);
    int initialIP = GetSmallInteger(initialIPOOP);

    // Create new BlockContext for execution
    SmalltalkOOP newContext = AllocateExecutionBlockContext(
        sender: _activeContext,
        home: homeContext,
        initialIP: initialIP,
        nargs: nargs);

    // Switch to new context
    _activeContext = newContext;
    _instructionPointer = initialIP;
}
```

**Note**: `GetBlockArgumentStartIndex` needs compiler metadata (stored in CompiledMethod literal frame or separate metadata structure).

---

## 4. Testing Strategy

### 4.1 Unit Tests

**Test 1: Method Variable Access**
```smalltalk
testMethodVariables
    | temp |
    temp := 42.
    self assert: temp = 42
```
Expected: `temp` at index 0 in MethodContext temp frame

**Test 2: Block Argument Access**
```smalltalk
testBlockArgument
    [:x | self assert: x = 5] value: 5
```
Expected: `x` stored in MethodContext temp frame when block activated

**Test 3: Nested Block Access**
```smalltalk
testNestedBlockAccess
    [:outer |
        [:inner | self assert: (outer + inner) = 3] value: 2
    ] value: 1
```
Expected: Both `outer` and `inner` in MethodContext temp frame, inner block accesses outer successfully

**Test 4: Escaping Block**
```smalltalk
testEscapingBlock
    | counter |
    counter := self makeCounter.
    counter value: 1.
    counter value: 5.
    self assert: (counter value: 0) = 6

makeCounter
    | count |
    count := 0.
    ^[:inc | count := count + inc. count]
```
Expected: MethodContext retained, block maintains state

### 4.2 Integration Tests

Run existing BlockContextTest suite (163 tests) to validate end-to-end behavior.

---

## 5. Common Implementation Errors

### Error 1: Storing Block Arguments in BlockContext

**WRONG**:
```csharp
// Storing in BlockContext indexed slots
WriteIndexedSlot(blockContext, argIndex, arg);
```

**CORRECT**:
```csharp
// Storing in home MethodContext temp frame
SmalltalkOOP home = GetHome(blockContext);
WriteIndexedSlot(home, absoluteIndex, arg);
```

### Error 2: Using Relative Indices

**WRONG**:
```csharp
int relativeIndex = index - blockStartOffset;
```

**CORRECT**:
```csharp
// Use absolute index from compiler
ReadIndexedSlot(home, absoluteIndex);
```

### Error 3: Dual Routing in Variable Access

**WRONG**:
```csharp
if (index < nargs)
    return ReadBlockContextSlot(index);
else
    return ReadMethodContextSlot(index - nargs);
```

**CORRECT**:
```csharp
// Single path: always MethodContext
SmalltalkOOP home = GetHome(activeContext);
return ReadIndexedSlot(home, index);
```

### Error 4: Allocating Indexed Slots for BlockContext

**WRONG**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: nargs);
```

**CORRECT**:
```csharp
InstantiateClass(blockContextClass, indexedSlotCount: 0);
```

### Error 5: Not Storing Arguments During Block Activation

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

## References

- [Unified-Temp-Frame-Specification.md](./Unified-Temp-Frame-Specification.md) - Canonical architecture
- [Variable-Indexing-Architecture.md](./Variable-Indexing-Architecture.md) - Indexing details
- [BlueBook-Context-Architecture.md](./BlueBook-Context-Architecture.md) - Context structures
- Blue Book Chapter 29-30 - Primitives and Contexts

---

**END OF GUIDE**
