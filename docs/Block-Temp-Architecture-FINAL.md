# Block Temporary Variable Architecture - FINAL SPECIFICATION

**Date**: 2025-11-12
**Status**: ⚠️ **SUPERSEDED** - See Canonical Reference Below

---

## ⚠️ SUPERSEDED NOTICE

**This document describes an obsolete dual-storage architecture.**

**For the current authoritative specification, see:**

**[Unified-Temp-Frame-Specification.md](./Unified-Temp-Frame-Specification.md)**

The information below is retained for historical reference but should NOT be used for implementation guidance.

---

## Historical Context

This document previously described a dual-storage model where block arguments were stored in both BlockContext indexed slots and MethodContext unified temp frame. This approach has been replaced with the simpler unified temp frame model where:

1. **ALL variables** (method args/temps + block args/temps) are stored in MethodContext indexed slots
2. **BlockContext has NO indexed slots** for variable storage (only instance variables)
3. **All access** uses absolute indices via home pointer traversal
4. **No dual routing** - no `if (index < nargs)` checks

---

## Legacy Documentation (Obsolete)

---

## Executive Summary

**Block arguments are stored in TWO locations** for different architectural purposes mandated by the Blue Book specification:

1. **BlockContext indexed slots [0..nargs-1]**: Expression stack + variable shadowing
2. **MethodContext unified temp frame**: Persistent storage with absolute indices

This dual storage is **required by the Blue Book**, not an optimization.

---

## 1. The Dual Storage Architecture

### 1.1 BlockContext Indexed Slots

**Purpose**: Expression stack with block arguments occupying initial slots

**Contents**:
- Slots `[0..nargs-1]`: Block arguments (copied by Primitive 81)
- Slots `[nargs..SP-1]`: Expression stack (grows during execution)

**Lifetime**: Single block activation (isolated per call)

**Why Required**:
- **Expression stack**: Blocks need to push intermediate values during bytecode execution
- **Variable shadowing**: Nested blocks with same-named arguments must access local values
- **Blue Book compliance**: Primitive 81 specification requires copying arguments here

### 1.2 MethodContext Unified Temp Frame

**Purpose**: Persistent storage for ALL temporary variables

**Contents**:
- Method arguments (absolute indices 0..numArgs-1)
- Method temps (absolute indices numArgs..numArgs+numTemps-1)
- Block arguments (absolute indices, allocated by TempFrameAllocator)
- Block temps (absolute indices, allocated by TempFrameAllocator)

**Lifetime**: Entire method execution (survives across block activations)

**Why Required**:
- **Persistent storage**: Values survive across multiple block activations
- **Shared access**: Accessible from nested blocks
- **Slot reuse**: Sequential non-overlapping blocks can reuse temp slots

---

## 2. VM Implementation

### 2.1 GetTemporaryVariable (VirtualMachine.cs:4114-4155)

```csharp
private SmalltalkOOP GetTemporaryVariable(SmalltalkOOP contextOOP, int index)
{
    var contextClassOOP = _heapManager.GetClassOOP(contextOOP);

    if (contextClassOOP == _metadata.BlockContextClassOOP)
    {
        var nargsOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
        int nargs = nargsOOP.AsSmallInteger();

        if (index < nargs)
        {
            // Block argument - read from BlockContext indexed slots (for shadowing)
            return _heapManager.GetIndexedOOP(contextOOP, index);
        }
        else
        {
            // Block temp or method var - read from MethodContext with ABSOLUTE index
            var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            return GetTemporaryVariable(homeOOP, index);  // NO adjustment! Absolute indexing!
        }
    }
    else
    {
        // MethodContext: Direct access to method's temp frame (base case)
        return _heapManager.GetIndexedOOP(contextOOP, index);
    }
}
```

### 2.2 SetTemporaryVariable (VirtualMachine.cs:4166-4195)

```csharp
private void SetTemporaryVariable(SmalltalkOOP contextOOP, int index, SmalltalkOOP valueOOP)
{
    var contextClassOOP = _heapManager.GetClassOOP(contextOOP);
    bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

    if (isBlockContext)
    {
        var nargsOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_NARGS_INDEX);
        int nargs = nargsOOP.AsSmallInteger();

        if (index < nargs)
        {
            // Block argument - write to BlockContext indexed slots
            _heapManager.SetIndexedOOP(contextOOP, index, valueOOP);
        }
        else
        {
            // Block temp or method var - write to MethodContext with ABSOLUTE index
            var homeOOP = _heapManager.GetInstanceVariable(contextOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            SetTemporaryVariable(homeOOP, index, valueOOP);  // NO adjustment! Absolute indexing!
        }
    }
    else
    {
        // MethodContext: Direct access to method's temp frame (base case)
        _heapManager.SetIndexedOOP(contextOOP, index, valueOOP);
    }
}
```

### 2.3 Key Implementation Points

1. **`if (index < nargs)` check**: Routes to BlockContext indexed slots for block arguments
2. **Absolute indexing**: When traversing to MethodContext, uses same index (no adjustment)
3. **Recursive traversal**: Automatically handles nested blocks via home chain
4. **Base case**: MethodContext access is direct indexed slot access

---

## 3. The Critical Fix (2025-11-12)

### 3.1 The Bug

**BEFORE** (lines 4147, 4187):
```csharp
return GetTemporaryVariable(homeOOP, index - nargs);  // WRONG - hierarchical adjustment
```

**Problem**:
- Compiler emits ABSOLUTE indices via TempFrameAllocator
- VM was doing hierarchical adjustment: `index - nargs`
- This created an architectural mismatch

### 3.2 The Fix

**AFTER** (lines 4147, 4187):
```csharp
return GetTemporaryVariable(homeOOP, index);  // CORRECT - absolute indexing
```

**Solution**: Removed index adjustment. Compiler emits absolute indices, VM uses them directly.

---

## 4. Compiler Responsibilities

The compiler (TempFrameAllocator) must:

1. **Allocate ALL temps in unified frame**: Method args/temps + block args/temps
2. **Emit absolute indices**: PUSH_TEMP/STORE_TEMP use absolute indices (0, 1, 2...)
3. **Enable slot reuse**: Sequential non-overlapping blocks can reuse slots
4. **Track maximum**: Record maximum simultaneous slot need for method header

**Example**:
```smalltalk
myMethod: x
    | temp1 |                          "Slot 0: x, Slot 1: temp1"
    temp1 := x + 1.
    [| btemp1 | btemp1 := temp1].      "Slot 2: btemp1"
    [:y | y + temp1]                   "Slot 2: y (REUSES slot 2!)"
```

---

## 5. Runtime Behavior

### 5.1 Primitive 81 (primitiveValue)

When a block is activated:

1. **Create BlockContext**: Allocate new BlockContext object
2. **Copy arguments**: Copy supplied arguments to BlockContext indexed slots [0..nargs-1]
3. **Set stack pointer**: `stackp = nargs` (expression stack begins after arguments)
4. **Link home**: Set `home` pointer to defining MethodContext

**Blue Book specification** (lines 1198-1199):
```smalltalk
self storeStackPointerValue: argumentCount
     inContext: blockContext
```

### 5.2 Expression Stack Usage

During block execution:

```smalltalk
[:x :y | x + y * 2] value: 3 value: 4

After Primitive 81:
  indexedSlots[0] = 3 (x - PERSISTENT)
  indexedSlots[1] = 4 (y - PERSISTENT)
  stackp = 2

During 'x + y':
  indexedSlots[0] = 3 (x - still there)
  indexedSlots[1] = 4 (y - still there)
  indexedSlots[2] = 3 (PUSHED for addition)
  indexedSlots[3] = 4 (PUSHED for addition)
  stackp = 4

After addition:
  indexedSlots[0] = 3 (x - still there)
  indexedSlots[1] = 4 (y - still there)
  indexedSlots[2] = 7 (result on expression stack)
  stackp = 3
```

### 5.3 Variable Shadowing

Nested blocks with same-named arguments:

```smalltalk
[:x |                    ; Outer block, x in BlockContext1.indexedSlots[0]
    [:x | x + 1]         ; Inner block, x in BlockContext2.indexedSlots[0]
      value: 5.          ; Inner x = 5
    x                    ; Outer x still = original value!
] value: 10
```

Without the `if (index < nargs)` fast path, both blocks would read/write the same MethodContext slot, breaking shadowing semantics.

---

## 6. Why Alternative Designs Don't Work

### 6.1 Only MethodContext Unified Frame (No BlockContext slots)

**Fatal problems**:
1. **No expression stack space**: Blocks need to push intermediate values
2. **Stack pointer meaningless**: What does stackp point to?
3. **Multiple active blocks conflict**: Sequential blocks overwrite each other's stacks
4. **Nested blocks fail**: Outer and inner blocks need isolated stacks

### 6.2 Only BlockContext Indexed Slots (No unified frame)

**Fatal problems**:
1. **No persistence**: Values lost between block activations
2. **Sequential blocks can't share**: Each activation is isolated
3. **Slot reuse impossible**: Can't optimize memory usage
4. **Nested access fails**: Inner blocks can't access outer block temps

---

## 7. Blue Book Compliance

**Key specifications**:

1. **Primitive 80 (blockCopy:)**: Takes 1 argument (nargs only)
2. **Primitive 81 (primitiveValue)**: Copies arguments to BlockContext indexed slots
3. **BlockContext structure**: 5 instance fields (nargs, startpc, home) + indexed slots
4. **Stack pointer**: Set to nargs, expression stack grows after arguments

**References**:
- Blue Book Chapter 30, pages 596-608
- `docs/BlueBook-Context-Architecture.md` Section 4.3
- `docs/Block-Argument-Dual-Storage-Analysis.md`

---

## 8. Testing and Verification

**Test Suite**: 194 comprehensive BlockContext tests

**Key test scenarios**:
- Single-level blocks (9 tests)
- Two-level nested blocks (81 tests)
- Three-level nested blocks (27 tests)
- Variable shadowing
- Method argument access from blocks
- Block temp mutation
- Expression stack usage

**Verification**:
```bash
cd "Smalltalk-80 Source"
cat Smalltalk-80.st Tests/BlockContextTest.st > Smalltalk-80-with-tests.st
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- "Smalltalk-80-with-tests.st"
cp bin/Debug/net9.0/coldstart.json bin/Debug/net9.0/coldstart_with_tests.json

cd ../Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_tests.json --skip-init-expressions
```

---

## 9. Summary

✅ **Block arguments stored in TWO locations** (required by Blue Book)
✅ **BlockContext indexed slots**: Expression stack + shadowing
✅ **MethodContext unified frame**: Persistent storage with absolute indices
✅ **VM uses absolute indexing**: No adjustment when traversing home chain
✅ **Compiler emits absolute indices**: TempFrameAllocator manages allocation
✅ **Slot reuse enabled**: Sequential blocks can share temp slots
✅ **Blue Book compliant**: Matches canonical Smalltalk-80 implementation

---

**Document Version**: 1.0
**Last Updated**: 2025-11-12
**Status**: ✅ IMPLEMENTED AND VERIFIED
**Files**: VirtualMachine.cs (lines 4114-4195)
