# Variable Indexing Architecture: Absolute Indices and Unified Temp Frame

**Date**: 2025-11-13
**Status**: CANONICAL SPECIFICATION
**Supersedes**: All previous variable indexing documentation

---

## CANONICAL REFERENCE

This document is the authoritative specification for variable indexing in the Smalltalk-80 interpreter. For the complete temp frame architecture, also see:

**[Unified-Temp-Frame-Specification.md](./Unified-Temp-Frame-Specification.md)**

---

## Executive Summary

The Smalltalk-80 interpreter uses **absolute indexing** for all variable access (method args/temps, block args/temps). The compiler assigns each variable a unique absolute index (0, 1, 2...) into the MethodContext's unified temp frame. BlockContext contains NO variable storage - only a `home` pointer to the MethodContext.

**Key Principle**: **One variable → One unique index → One storage location (MethodContext)**

---

## Table of Contents

1. [Core Concepts](#core-concepts)
2. [Compiler: TempFrameAllocator](#compiler-tempframeallocator)
3. [Runtime: Variable Access](#runtime-variable-access)
4. [Examples](#examples)
5. [Implementation Details](#implementation-details)

---

## Core Concepts

### Absolute vs Relative Indexing

**Absolute Indexing** (CORRECT - Used in Implementation):
- Each variable has ONE unique index across entire method
- Index is assigned at compile time by TempFrameAllocator
- Index never changes during execution
- All access uses same index (simple, consistent)

**Example**:
```smalltalk
method: x           "x → index 0"
    | temp |        "temp → index 1"
    [:a | a + x]    "a → index 2, x still index 0"
```

**Relative Indexing** (WRONG - Not Used):
- Each scope has local indices (0, 1, 2...)
- Must adjust index when traversing scopes
- Complex runtime arithmetic required
- Error-prone

### Lexical Flattening

The compiler "flattens" the lexical scope hierarchy into a single linear index space:

```
Lexical Structure:        Flattened Index Space:
method: x                 0: x (method arg)
  | temp |                1: temp (method temp)
  [:a |                   2: a (block arg)
    [:b |                 3: b (nested block arg)
      b + a + x           Access: b=3, a=2, x=0
    ]
  ]
```

**Key Insight**: Compiler performs the scope traversal at compile time, emitting absolute indices. Runtime just uses the indices directly.

---

## Compiler: TempFrameAllocator

### Allocation Algorithm

**File**: `Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs`

```csharp
public class TempFrameAllocator
{
    private int _nextSlot = 0;
    private int _maxSimultaneous = 0;
    private readonly Dictionary<string, int> _allocatedSlots = new();
    private readonly Stack<ScopeInfo> _scopeStack = new();

    private class ScopeInfo
    {
        public HashSet<int> AllocatedSlots { get; } = new();
        public int StartSlot { get; set; }
        public bool IsSequential { get; set; }  // Can reuse slots after exit?
    }

    public void EnterMethodScope(List<string> argNames, List<string> tempNames)
    {
        var scope = new ScopeInfo { StartSlot = _nextSlot, IsSequential = false };
        _scopeStack.Push(scope);

        // Allocate method arguments
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        // Allocate method temporaries
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
    {
        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = !isNested  // Sequential blocks can reuse, nested cannot
        };
        _scopeStack.Push(scope);

        // Allocate block arguments (YES - they go in unified frame!)
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        // Allocate block temporaries
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

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

    public int GetSlotIndex(string varName)
    {
        if (_allocatedSlots.TryGetValue(varName, out int slot))
        {
            return slot;
        }

        throw new InvalidOperationException($"Variable '{varName}' not found");
    }

    public int GetMaxFrameSize()
    {
        return _maxSimultaneous;
    }
}
```

### Sequential Block Slot Reuse

**Optimization**: Sequential (non-overlapping) blocks can reuse the same temp slots.

**Example**:
```smalltalk
method
    [:a | a + 1] value: 1.   "a → slot 0"
    [:b | b + 2] value: 2.   "b → slot 0 (REUSED!)"
```

**After first block exits**:
- Slot 0 is freed
- `_nextSlot` reset to 0
- Second block allocates: b → slot 0

**Maximum frame size**: 1 slot (not 2!)

### Nested Block Allocation

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

---

## Runtime: Variable Access

### VM Implementation

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

```csharp
private SmalltalkOOP GetTemporaryVariable(int index)
{
    // Find the home MethodContext (traverse if in BlockContext)
    var homeContext = FindHomeMethodContext(_activeContextOOP);

    // Direct access with absolute index
    return _heapManager.GetIndexedOOP(homeContext, index);
}

private void SetTemporaryVariable(int index, SmalltalkOOP value)
{
    var homeContext = FindHomeMethodContext(_activeContextOOP);
    _heapManager.SetIndexedOOP(homeContext, index, value);
}

private SmalltalkOOP FindHomeMethodContext(SmalltalkOOP contextOOP)
{
    while (true)
    {
        var classOOP = _heapManager.GetClassOOP(contextOOP);

        if (classOOP == _metadata.MethodContextClassOOP)
        {
            return contextOOP;  // Found the MethodContext
        }

        // Traverse home chain for BlockContext
        contextOOP = _heapManager.GetInstanceVariable(
            contextOOP,
            VMConstants.BLOCKCONTEXT_HOME_INDEX
        );

        if (contextOOP.IsNil)
        {
            throw new InvalidOperationException("Home chain broken");
        }
    }
}
```

**Key Points**:
1. **No index adjustment**: Absolute index used directly
2. **Simple traversal**: Just find home MethodContext
3. **No nargs check**: No `if (index < nargs)` dual routing
4. **Uniform access**: All variables accessed the same way

---

## Examples

### Example 1: Simple Block

```smalltalk
method: x
    [:a | a + x] value: 5
```

**Compiler Allocation**:
```
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

### Example 2: Nested Blocks

```smalltalk
method
    [:a |
        [:b | a + b] value: 2
    ] value: 1
```

**Compiler Allocation**:
```
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

### Example 3: Sequential Block Slot Reuse

```smalltalk
method: x
    | temp |
    [:a | a + 1] value: 5.
    [:b | b + 2] value: 10.
    ^temp
```

**Compiler Allocation**:
```
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

---

## Implementation Details

### Bytecode Emission

**File**: `Smalltalk80.Compiler/BytecodeCompiler.cs`

```csharp
private void EmitPushTemporary(string varName)
{
    // Get absolute index from allocator
    int absoluteIndex = _allocator.GetSlotIndex(varName);

    // Emit PUSH_TEMP with absolute index
    if (absoluteIndex < 16)
    {
        EmitBytecode(0x10 + absoluteIndex);  // Fast-path (0x10-0x1F)
    }
    else
    {
        EmitBytecode(0x03);                  // Extended PUSH_TEMP
        EmitBytecode((byte)absoluteIndex);
    }
}

private void EmitStoreTemporary(string varName)
{
    int absoluteIndex = _allocator.GetSlotIndex(varName);

    if (absoluteIndex < 8)
    {
        EmitBytecode(0x68 + absoluteIndex);  // Fast-path POP_STORE_TEMP
    }
    else
    {
        EmitBytecode(0x05);                  // Extended POP_STORE_TEMP
        EmitBytecode((byte)absoluteIndex);
    }
}
```

### Method Header Encoding

The method header includes the temp frame size:

```csharp
int header = (argCount << 0) |
             (tempCount << 3) |
             (literalCount << 8) |
             (needsLargeFrame ? 1 << 16 : 0);
```

Where `tempCount` = total temp frame size (method temps + max block temps needed).

### Context Allocation

```csharp
private SmalltalkOOP AllocateMethodContext(
    SmalltalkOOP methodOOP,
    SmalltalkOOP receiverOOP,
    SmalltalkOOP senderOOP,
    int tempFrameSize)
{
    // Parse method header to get temp count
    var header = ParseMethodHeader(methodOOP);
    int stackSize = header.NeedsLargeFrame ? 48 : 16;

    // Allocate: instance vars + temp frame + stack
    int totalIndexedSlots = tempFrameSize + stackSize;

    var contextOOP = _heapManager.AllocateObject(
        _metadata.MethodContextClassOOP,
        ObjectFormat.PointerIndexed,
        instanceVariableCount: 6,  // sender, pc, stackp, method, lexicalParent, receiver
        indexedSize: totalIndexedSlots
    );

    // Initialize instance variables
    _heapManager.SetInstanceVariable(contextOOP, CONTEXT_SENDER_INDEX, senderOOP);
    _heapManager.SetInstanceVariable(contextOOP, CONTEXT_METHOD_INDEX, methodOOP);
    _heapManager.SetInstanceVariable(contextOOP, CONTEXT_RECEIVER_INDEX, receiverOOP);
    _heapManager.SetInstanceVariable(contextOOP, CONTEXT_PC_INDEX, SmalltalkOOP.FromSmallInteger(0));
    _heapManager.SetInstanceVariable(contextOOP, CONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(tempFrameSize));

    // Initialize temp frame to nil
    var nilOOP = SpecialObjectsArray.Instance.Nil;
    for (int i = 0; i < tempFrameSize; i++)
    {
        _heapManager.SetIndexedOOP(contextOOP, i, nilOOP);
    }

    return contextOOP;
}
```

---

## Summary

**Core Principle**: Absolute indexing with lexical flattening

**Compiler Responsibilities**:
1. Assign unique absolute index to each variable
2. Track maximum simultaneous need (for frame size)
3. Implement slot reuse for sequential blocks
4. Emit absolute indices in bytecode

**Runtime Responsibilities**:
1. Find home MethodContext (if in BlockContext)
2. Access temp frame with absolute index
3. No index adjustment or dual routing

**Benefits**:
- ✅ Simple: One index per variable, used uniformly
- ✅ Correct: Matches compiler's allocation exactly
- ✅ Efficient: No runtime arithmetic or complex routing
- ✅ Maintainable: Clear, predictable behavior

---

**Document Version**: 1.0
**Last Updated**: 2025-11-13
**Status**: CANONICAL SPECIFICATION
