# VM Field Knowledge Audit

**Date**: 2025-10-29
**Purpose**: Identify all hardcoded knowledge of Smalltalk class fields in the VM and determine which are legitimate Blue Book architectural constants vs. application-level knowledge.

## Executive Summary

The Blue Book Smalltalk-80 VM specification defines fixed field indices for **two categories** of objects:

1. **Behavior and subclasses** (Class, Metaclass)
2. **Context objects** (MethodContext, BlockContext, ContextPart)

All other field accesses represent **application-level knowledge** that should be handled via primitives or eliminated from the VM core.

---

## Category 1: Blue Book Architectural Constants (LEGITIMATE)

These are part of the VM specification and MUST be hardcoded.

### A. Behavior Fields (Chapter 27)

**Already Fixed** (commit 856b44a):
- `superclass` → index 0
- `methodDict` → index 1
- `format` → index 2

**Source**: Blue Book Chapter 27: "SuperclassIndex := 0. MessageDictionaryIndex := 1. InstanceSpecificationIndex := 2."

**VM Files Using These**:
- `VirtualMachine.cs` lines 72-77 (constants defined)
- `VirtualMachine.cs` lines 1757, 2949, 2981, 3122 (usage)

### B. Context Object Fields (Chapter 27)

**Inheritance Chain**:
```
Object
  └─ InstructionStream (sender, pc)
      └─ ContextPart (stackp)
          ├─ MethodContext (method, receiverMap?, receiver)
          └─ BlockContext (nargs, startpc, home)
```

**Blue Book Fixed Indices**:

**Common to both MethodContext and BlockContext**:
- `sender` (from InstructionStream) → index 0 (SenderIndex)
- `pc` (from InstructionStream) → index 1 (InstructionPointerIndex)
- `stackp` (from ContextPart) → index 2 (StackPointerIndex)

**MethodContext-specific**:
- `method` → index 3 (MethodIndex)
- `receiverMap` → index 4 (uncertain - not in Blue Book)
- `receiver` → index 5 (ReceiverIndex)
- Indexed slots start at index 6 (TempFrameStart)

**BlockContext-specific**:
- `nargs` → index 3 (BlockArgumentCountIndex)
- `startpc` → index 4 (InitialIPIndex)
- `home` → index 5 (HomeIndex)
- Indexed slots start at index 6 (TempFrameStart)

**Source**: Blue Book Chapter 27 specifies all these indices as VM constants.

**VM Files Using These** (90+ locations):
- `VirtualMachine.cs`: Lines 274-279, 334-336, 461, 946, 957, 972, 1143-1144, 1455, 1746, 2145, 2165-2166, 2459-2464, 2528, 2564, 2584, 2596, 2622, 2793, 2806, 3258
- `VirtualMachine.cs`: Lines 482, 489, 519, 536, 545, 592, 629, 635, 690, 696, 720, 732, 737, 760, 766, 817, 823, 862, 867, 879, 906, 1016, 1022, 1056, 1062, 1103, 1109, 1192, 1204, 1210, 1231, 1243, 1249, 1270, 1282, 1288, 2335-2337, 3286-3289, 3310-3311
- `InitExpressionExecutor.cs`: Lines 225-230, 338-340

**Status**: ❌ **NOT YET FIXED** - Still using `GetFieldIndex()` at runtime

---

## Category 2: VM Implementation Details (QUESTIONABLE)

These may be necessary for VM operation but are NOT part of Blue Book specification.

### C. Association Fields

**Fields Used**:
- `key` → used for global variable lookup
- `value` → used for global variable access

**VM Files Using These**:
- `VirtualMachine.cs` lines 666, 1312, 1324, 2234

**Analysis**: Associations are used extensively for:
- Global variable storage (Smalltalk dictionary entries)
- Method literal bindings
- Class pool variable storage

**Question**: Are Association fields part of the VM specification, or should primitives handle all Association access?

**Recommendation**: Research Blue Book Chapter 28-29 for primitive specifications. If primitives are meant to handle globals, these should be moved to primitive implementations.

### D. Process Scheduler Fields

**Fields Used**:
- `class:ProcessorScheduler` → `activeProcess`
- `class:Process` → `suspendedContext`, `priority`, `myList`

**VM Files Using These**:
- `InitExpressionExecutor.cs` lines 338-340

**Analysis**: Process scheduling is a core VM function. The Blue Book specifies primitive 87 (primitiveProcessYield) and related primitives that manipulate Process and ProcessorScheduler objects.

**Question**: Are these fields part of the VM specification for process primitives?

**Recommendation**: Review Blue Book Chapter 29 (Primitive Methods) to determine if these are architectural constants or should be accessed via primitives.

---

## Category 3: Application-Level Knowledge (ILLEGITIMATE)

These represent application-level class structures and should NOT be hardcoded in the VM.

### E. Dictionary Fields

**Fields Used**:
- `class:Dictionary` → `tally`, `array`

**VM Files**: None in grep output (may be in cold start only)

**Analysis**: Dictionary is an application-level collection class. The VM should not know about its internal structure.

**Recommendation**: Move to primitives or eliminate from VM core.

### F. Point Fields

**Fields Used**:
- `class:Point` → `x`, `y`

**VM Files**: None in grep output (may be in cold start only)

**Analysis**: Point is an application-level data structure. The VM should not know about it.

**Recommendation**: Move to primitives or eliminate from VM core.

### G. Class-Specific Fields

**Fields Used**:
- `class:Metaclass` → `thisClass`
- `class:Class` → `name`, `classPool`
- `class:MethodDictionary` → `tally`, `keys`, `values`

**VM Files Using These**:
- `Program.cs` lines 867, 871-874, 876-878
- `Loader.cs` lines 209, 214-217, 219-221

**Analysis**: These are used during cold start initialization, NOT during runtime bytecode execution. Cold start code is marked `[ColdStartOnly]` and is allowed to have detailed knowledge of class structures.

**Status**: ✅ **ACCEPTABLE** - Used only during cold start, not during runtime execution

### H. BlockContext Fields

**Fields Used**:
- `class:BlockContext` → `homeContext`, `returnContext`, `method`, `numArgs`

**VM Files Using These**:
- `VirtualMachine.cs` lines 482, 536, 592, 690, 732, 760, 817, 862, 867, 906, 1204, 1243, 1282

**Analysis**: BlockContext is a modern extension to Smalltalk-80, not part of the original Blue Book. It was added to support proper lexical closures. The Blue Book only specifies BlockContext (primitives 80-82).

**Question**: Should BlockContext fields be considered VM-level knowledge, or should they be accessed via primitives?

**Recommendation**:
- **Option A**: Declare BlockContext as a VM extension with fixed field indices (like we did for Behavior)
- **Option B**: Move all BlockContext access to primitive 207 (BlockContext>>value)

---

## Recommendations

### Immediate Actions (High Priority)

1. **Define Blue Book Constants for Context Objects** ✅ LEGITIMATE
   - Add constants to `VirtualMachine.cs`:
     ```csharp
     // Blue Book Chapter 27: Context field indices
     private const int CONTEXT_SENDER_INDEX = 0;      // InstructionStream
     private const int CONTEXT_PC_INDEX = 1;          // InstructionStream
     private const int CONTEXT_STACKP_INDEX = 2;      // ContextPart
     private const int METHODCONTEXT_METHOD_INDEX = 3;
     private const int METHODCONTEXT_RECEIVER_MAP_INDEX = 4; // Verify this!
     private const int METHODCONTEXT_RECEIVER_INDEX = 5;
     private const int METHODCONTEXT_TEMP_FRAME_START = 6;

     private const int BLOCKCONTEXT_NARGS_INDEX = 3;
     private const int BLOCKCONTEXT_STARTPC_INDEX = 4;
     private const int BLOCKCONTEXT_HOME_INDEX = 5;
     private const int BLOCKCONTEXT_TEMP_FRAME_START = 6;
     ```

2. **Enforce Context Field Indices in Compiler**
   - Update `ColdStartJsonGenerator.cs` to force these indices for InstructionStream, ContextPart, MethodContext, BlockContext
   - Similar to what we did for Behavior (commit 856b44a)

3. **Replace All Context GetFieldIndex Calls**
   - Replace 90+ calls in `VirtualMachine.cs` with constants
   - Replace calls in `InitExpressionExecutor.cs` with constants

### Research Required (Medium Priority)

4. **Association Fields** - Determine if VM-level or primitive-level
   - Review Blue Book Chapter 28-29 for global variable primitive specifications
   - If primitives should handle globals, refactor `pushGlobal`/`storeGlobal` bytecodes

5. **Process Scheduler Fields** - Determine if VM-level or primitive-level
   - Review Blue Book Chapter 29 for process primitive specifications
   - Determine if ProcessorScheduler/Process fields are architectural constants

6. **BlockContext Fields** - Determine VM extension vs primitive approach
   - Document BlockContext as VM extension with fixed field indices, OR
   - Move all BlockContext logic to primitive 207

### Future Cleanup (Low Priority)

7. **Eliminate Application-Level Knowledge**
   - Audit all primitives for hardcoded field access
   - Move Dictionary, Point, and other application class knowledge to Smalltalk-level code

---

## Verification Plan

After implementing fixes:

1. ✅ Compile generates valid bundles with fixed context field indices
2. ✅ VM loads bundles successfully using Blue Book constants
3. ✅ All 6 cold start phases complete without errors
4. ✅ Init expressions execute successfully
5. ✅ Runtime bytecode execution uses only architectural constants

---

## References

- Blue Book Chapter 27: *Specification of the Virtual Machine* - Defines all architectural constants
- Blue Book Chapter 28: *Formal Specification of the Interpreter* - Bytecode semantics
- Blue Book Chapter 29: *Formal Specification of Primitive Methods* - Primitive specifications
- Blue Book Chapter 30: *Formal Specification of the Object Memory* - Memory layout

---

## Conclusion

**The VM currently has too much application-level knowledge.**

**Legitimate VM Knowledge** (Blue Book specified):
- Behavior fields: superclass, methodDict, format (✅ Fixed)
- Context fields: sender, pc, stackp, method, receiver, nargs, startpc, home (❌ Not yet fixed)

**Questionable VM Knowledge** (needs research):
- Association fields (key, value)
- Process/ProcessorScheduler fields
- BlockContext fields (VM extension)

**Illegitimate VM Knowledge** (should be eliminated):
- Point fields (x, y)
- Dictionary internal structure
- Other application-level class fields

**Next Step**: Implement Blue Book constants for context objects (same approach as Behavior fields).
