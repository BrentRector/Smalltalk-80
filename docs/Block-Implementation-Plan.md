# Block Implementation Plan - Primitives 80-81 + Compiler Changes

**Date**: 2025-11-06
**Status**: PLANNING - Implementation not yet started
**Priority**: CRITICAL - Required for basic block functionality

---

## Executive Summary

This document provides a step-by-step implementation plan to correctly support block compilation and execution in the Smalltalk-80 interpreter. The plan addresses two major components:

1. **Interpreter**: Implement primitives 80-81 (blockCopy: and value) per Blue Book specification
2. **Offline Compiler**: Modify bytecode generation to emit inline block bytecode instead of CompiledMethod literals

**Estimated Effort**: 2-3 days (1 day interpreter + 1-2 days compiler + testing)

---

## Background: The Problem

### Current (Incorrect) Implementation

**Offline Compiler**:
- Creates separate `CompiledMethod` objects for each block
- Stores them in method's literal array
- Emits `PUSH_LITERAL` bytecode to push CompiledMethod onto stack

**Runtime**:
- Block is a CompiledMethod object
- Sending `whileTrue:` to CompiledMethod → **FAILS** (no such method)
- CompiledMethod has no lexical scope (no `home` pointer)

### Correct (Blue Book) Implementation

**Offline Compiler**:
- Generates **inline bytecode** for blocks
- Emits: `LdThisContext → PUSH numArgs → SEND blockCopy: → LONG_JUMP → [block body] → EndRemote`
- Block body embedded in method bytecode stream, jumped over during normal execution

**Runtime**:
- Primitive 80 (blockCopy:) creates BlockContext at runtime
- BlockContext has `home` pointer to defining MethodContext (lexical scope)
- BlockContext responds to `whileTrue:`, `value`, etc. → **WORKS**

---

## Part 1: Interpreter Implementation (Primitives 80-81)

### Prerequisites

**1. Determine BlockContext Field Indices**

BlockContext inherits from ContextPart (which inherits from InstructionStream). Need to determine actual slot indices:

```
InstructionStream:
  slot 0: sender
  slot 1: instructionPointer

ContextPart (adds):
  slot 2: stackPointer

BlockContext (adds):
  slot 3: nargs            (BlockArgumentCountIndex)
  slot 4: initialIP        (InitialIPIndex)
  slot 5: home             (HomeIndex)

Indexed slots start at: slot 6 (TempFrameStart)
```

**Action Items**:
1. Examine `Smalltalk-80.st` line 4682 (BlockContext class definition)
2. Count instance variables from inheritance chain
3. Define constants in `VMConstants.cs`:
   ```csharp
   // BlockContext field indices (Blue Book specification)
   public const int BLOCKCONTEXT_NARGS_INDEX = 3;
   public const int BLOCKCONTEXT_INITIALIP_INDEX = 4;
   public const int BLOCKCONTEXT_HOME_INDEX = 5;
   public const int BLOCKCONTEXT_CALLER_INDEX = 0;  // sender field from InstructionStream
   public const int BLOCKCONTEXT_TEMP_FRAME_START = 6;  // First indexed slot
   ```

**2. Verify BlockContext Class OOP Available**

The interpreter needs `ClassBlockContextPointer` to instantiate BlockContext objects.

**Action Items**:
1. Check if `ClassBlockContextPointer` is defined in SpecialObjectsArray or GlobalRegistry
2. If not, add to appropriate registry during cold start Phase 2 (Class Infrastructure)

---

### Step 1: Implement Primitive 80 (blockCopy:)

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/BlockContextPrimitives.cs` (NEW)

**Purpose**: Creates a new BlockContext instance when block literal is evaluated

**Blue Book Reference**: Lines 1146-1175

**Implementation**:

```csharp
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Core.VM.Primitives
{
    /// <summary>
    /// Primitive 80-82: BlockContext primitives
    /// Blue Book: "Formal Specification of the Primitive Methods", lines 1146-1238
    /// </summary>
    public static class BlockContextPrimitives
    {
        /// <summary>
        /// Primitive 80: blockCopy:
        /// Creates a new BlockContext for a block literal.
        ///
        /// Blue Book: Lines 1146-1175
        ///
        /// Receiver: MethodContext or BlockContext (current context)
        /// Argument: SmallInteger (block argument count)
        /// Returns: New BlockContext instance
        /// </summary>
        public static void PrimitiveBlockCopy(VirtualMachine vm)
        {
            // Blue Book line 1150: blockArgumentCount ← self popStack
            var blockArgumentCount = vm.PopStack();

            // Blue Book line 1151: context ← self popStack
            var context = vm.PopStack();

            // Validate blockArgumentCount is SmallInteger
            if (!vm.IsSmallInteger(blockArgumentCount))
            {
                vm.PrimitiveFail();
                vm.UnPop(2);
                return;
            }

            // Blue Book lines 1152-1155: Resolve home context
            SmalltalkOOP methodContext;
            var contextClass = vm.FetchClassOf(context);

            if (contextClass == vm.ClassBlockContextPointer)
            {
                // If receiver is BlockContext, use its home
                methodContext = vm.FetchPointer(VMConstants.BLOCKCONTEXT_HOME_INDEX, context);
            }
            else if (contextClass == vm.ClassMethodContextPointer)
            {
                // If receiver is MethodContext, it IS the home
                methodContext = context;
            }
            else
            {
                // Invalid receiver class
                vm.PrimitiveFail();
                vm.UnPop(2);
                return;
            }

            // Blue Book line 1156: contextSize ← memory fetchWordLengthOf: methodContext
            int contextSize = vm.FetchWordLengthOf(methodContext);

            // Blue Book lines 1157-1158: Allocate new BlockContext
            var newContext = vm.InstantiateClass(
                vm.ClassBlockContextPointer,
                contextSize  // Same indexed size as home
            );

            // Blue Book line 1159: initialIP ← memory integerObjectOf: instructionPointer + 3
            // NOTE: +3 because current PC points to blockCopy: send, need to skip 2-byte LONG_JUMP
            var currentIP = vm.InstructionPointer;
            var initialIP = vm.IntegerObjectOf(currentIP + 3);

            // Blue Book lines 1160-1162: Store initialIP
            vm.StorePointer(VMConstants.BLOCKCONTEXT_INITIALIP_INDEX, newContext, initialIP);

            // Blue Book lines 1163-1165: Store instructionPointer (set to initialIP)
            vm.StorePointer(VMConstants.CONTEXT_PC_INDEX, newContext, initialIP);

            // Blue Book lines 1166-1167: Initialize stack pointer to 0
            vm.StoreStackPointerValue(0, newContext);

            // Blue Book lines 1168-1170: Store block argument count
            vm.StorePointer(VMConstants.BLOCKCONTEXT_NARGS_INDEX, newContext, blockArgumentCount);

            // Blue Book lines 1171-1173: Store home context
            vm.StorePointer(VMConstants.BLOCKCONTEXT_HOME_INDEX, newContext, methodContext);

            // Blue Book line 1174: Push result
            vm.Push(newContext);
        }
    }
}
```

**Required VirtualMachine Helper Methods**:

Add to `VirtualMachine.cs`:

```csharp
/// <summary>
/// Gets the word length (indexed slot count) of an object.
/// Blue Book reference: line 1156
/// </summary>
public int FetchWordLengthOf(SmalltalkOOP objectOOP)
{
    var obj = _heapManager.GetObject(objectOOP);
    return obj.IndexedSlotCount;
}

/// <summary>
/// Stores stack pointer value in a context.
/// Blue Book reference: lines 1166-1167, 1198-1199
/// </summary>
public void StoreStackPointerValue(int value, SmalltalkOOP contextOOP)
{
    var stackPointerOOP = IntegerObjectOf(value);
    StorePointer(VMConstants.CONTEXT_STACKP_INDEX, contextOOP, stackPointerOOP);
}

/// <summary>
/// Instantiates a class with pointer/indexed slots.
/// Blue Book reference: line 1157
/// </summary>
public SmalltalkOOP InstantiateClass(SmalltalkOOP classOOP, int indexedSize)
{
    // Use heap manager to allocate PointerIndexed object
    var instanceVarCount = _layoutResolver.GetInstanceVariableCount(classOOP);
    return _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount,
        indexedSize
    );
}

/// <summary>
/// Creates SmallInteger from int value.
/// Blue Book reference: line 1159
/// </summary>
public SmalltalkOOP IntegerObjectOf(int value)
{
    return SmalltalkOOP.FromSmallInteger(value);
}

/// <summary>
/// Checks if OOP is a SmallInteger.
/// </summary>
public bool IsSmallInteger(SmalltalkOOP oop)
{
    return oop.IsSmallInteger;
}

/// <summary>
/// Marks primitive as failed.
/// </summary>
public void PrimitiveFail()
{
    _primitiveSuccess = false;
}

/// <summary>
/// Un-pops count items from stack (restores stack after primitive failure).
/// </summary>
public void UnPop(int count)
{
    _stackPointer += count;
}
```

---

### Step 2: Implement Primitive 81 (value)

**File**: Same file as Step 1 (`BlockContextPrimitives.cs`)

**Purpose**: Executes a BlockContext by transferring arguments and switching contexts

**Blue Book Reference**: Lines 1179-1204

**Implementation**:

```csharp
/// <summary>
/// Primitive 81: value (and value:, value:value:, etc.)
/// Executes a BlockContext by switching to it.
///
/// Blue Book: Lines 1179-1204
///
/// Receiver: BlockContext
/// Arguments: 0-N arguments matching block's nargs
/// Returns: Does NOT return - switches active context (trampoline)
/// </summary>
public static void PrimitiveValue(VirtualMachine vm)
{
    // Blue Book line 1183: blockContext ← self stackValue: argumentCount
    var blockContext = vm.StackValue(vm.ArgumentCount);

    // Validate receiver is BlockContext
    var blockClass = vm.FetchClassOf(blockContext);
    if (blockClass != vm.ClassBlockContextPointer)
    {
        vm.PrimitiveFail();
        return;
    }

    // Blue Book line 1184: blockArgumentCount ← self argumentCountOfBlock: blockContext
    var nargsOOP = vm.FetchPointer(VMConstants.BLOCKCONTEXT_NARGS_INDEX, blockContext);
    var blockArgumentCount = vm.IntegerValueOf(nargsOOP);

    // Blue Book line 1185: self success: argumentCount=blockArgumentCount
    if (vm.ArgumentCount != blockArgumentCount)
    {
        vm.PrimitiveFail();
        return;
    }

    // Blue Book lines 1187-1191: Transfer arguments from active context to block context
    // Copy from: activeContext stack[stackPointer - argumentCount + 1 .. stackPointer]
    // Copy to: blockContext indexed slots[TempFrameStart .. TempFrameStart + argumentCount - 1]
    var activeContext = vm.ActiveContext;
    var stackPointer = vm.StackPointer;

    for (int i = 0; i < vm.ArgumentCount; i++)
    {
        int fromIndex = stackPointer - vm.ArgumentCount + 1 + i;
        int toIndex = VMConstants.BLOCKCONTEXT_TEMP_FRAME_START + i;

        var value = vm.FetchPointer(fromIndex, activeContext);
        vm.StorePointer(toIndex, blockContext, value);
    }

    // Blue Book line 1192: Pop arguments + receiver from stack
    vm.PopN(vm.ArgumentCount + 1);

    // Blue Book lines 1193-1197: Reset instruction pointer to initialIP
    var initialIP = vm.FetchPointer(VMConstants.BLOCKCONTEXT_INITIALIP_INDEX, blockContext);
    vm.StorePointer(VMConstants.CONTEXT_PC_INDEX, blockContext, initialIP);

    // Blue Book lines 1198-1199: Set stack pointer to argument count
    vm.StoreStackPointerValue(vm.ArgumentCount, blockContext);

    // Blue Book lines 1200-1202: Set caller to active context
    vm.StorePointer(VMConstants.BLOCKCONTEXT_CALLER_INDEX, blockContext, activeContext);

    // Blue Book line 1203: Activate block context (CONTEXT SWITCH - no return!)
    vm.NewActiveContext(blockContext);
}
```

**Required VirtualMachine Helper Methods**:

Add to `VirtualMachine.cs`:

```csharp
/// <summary>
/// Returns value at index from top of stack (0 = top).
/// Blue Book reference: line 1183
/// </summary>
public SmalltalkOOP StackValue(int index)
{
    return FetchPointer(_stackPointer - index, _activeContextOOP);
}

/// <summary>
/// Extracts integer value from SmallInteger OOP.
/// Blue Book reference: line 1184
/// </summary>
public int IntegerValueOf(SmalltalkOOP oop)
{
    if (!oop.IsSmallInteger)
        throw new InvalidOperationException("OOP is not a SmallInteger");
    return oop.SmallIntegerValue;
}

/// <summary>
/// Pops N items from stack.
/// Blue Book reference: line 1192
/// </summary>
public void PopN(int count)
{
    _stackPointer -= count;
}

/// <summary>
/// Switches to a new active context (trampoline operation).
/// Blue Book reference: line 1203
/// </summary>
public void NewActiveContext(SmalltalkOOP newContextOOP)
{
    // Store current stack pointer in old context
    StoreStackPointerValue(_stackPointer, _activeContextOOP);

    // Switch to new context
    _activeContextOOP = newContextOOP;

    // Load instruction pointer from new context
    var ipOOP = FetchPointer(VMConstants.CONTEXT_PC_INDEX, newContextOOP);
    _instructionPointer = IntegerValueOf(ipOOP);

    // Load stack pointer from new context
    var spOOP = FetchPointer(VMConstants.CONTEXT_STACKP_INDEX, newContextOOP);
    _stackPointer = IntegerValueOf(spOOP);
}

/// <summary>
/// Public accessor for argument count (set by message send).
/// </summary>
public int ArgumentCount { get; private set; }

/// <summary>
/// Public accessor for active context.
/// </summary>
public SmalltalkOOP ActiveContext => _activeContextOOP;

/// <summary>
/// Public accessor for stack pointer.
/// </summary>
public int StackPointer => _stackPointer;

/// <summary>
/// Public accessor for instruction pointer.
/// </summary>
public int InstructionPointer => _instructionPointer;

/// <summary>
/// Public accessor for ClassBlockContextPointer.
/// </summary>
public SmalltalkOOP ClassBlockContextPointer { get; private set; }

/// <summary>
/// Public accessor for ClassMethodContextPointer.
/// </summary>
public SmalltalkOOP ClassMethodContextPointer { get; private set; }
```

---

### Step 3: Register Primitives in PrimitiveTable

**File**: `Smalltalk80.Interpreter/Core/VM/PrimitiveTable.cs`

**Action**:

```csharp
// In RegisterPrimitives() method:

// Control primitives (80-89)
_primitives[80] = BlockContextPrimitives.PrimitiveBlockCopy;
_primitives[81] = BlockContextPrimitives.PrimitiveValue;
// Primitive 82 (valueWithArguments:) can be added later
```

---

### Step 4: Update VMConstants.cs

**File**: `Smalltalk80.Interpreter/Core/VM/VMConstants.cs`

**Action**:

Add BlockContext field constants (verify indices match actual class layout):

```csharp
// BlockContext field indices (Blue Book specification)
// BlockContext inherits: InstructionStream (sender, pc) → ContextPart (stackp)
public const int BLOCKCONTEXT_NARGS_INDEX = 3;         // Block argument count
public const int BLOCKCONTEXT_INITIALIP_INDEX = 4;     // Saved PC for re-execution
public const int BLOCKCONTEXT_HOME_INDEX = 5;          // Lexical parent MethodContext
public const int BLOCKCONTEXT_CALLER_INDEX = 0;        // sender field (from InstructionStream)
public const int BLOCKCONTEXT_TEMP_FRAME_START = 6;    // First indexed slot
```

**Note**: Remove or update obsolete BLOCKCONTEXT_* constants mentioned in `DEAD_CODE_REPORT.md` if they conflict.

---

### Step 5: Initialize ClassBlockContextPointer

**File**: `Smalltalk80.Interpreter/Engine/Loader.cs` or appropriate Phase file

**Action**:

Ensure `ClassBlockContextPointer` is initialized during cold start:

```csharp
// In Phase 2 (Class Infrastructure) or similar:
var blockContextClassOOP = GlobalRegistry.Get("class:BlockContext");
vm.ClassBlockContextPointer = blockContextClassOOP;
```

---

### Step 6: Testing Primitives

**Unit Tests** (create `BlockContextPrimitivesTests.cs`):

```csharp
[Test]
public void Primitive80_CreatesBlockContext_WithCorrectFields()
{
    // Setup: Create MethodContext, push it and numArgs onto stack
    var methodContext = CreateMethodContext();
    vm.Push(methodContext);
    vm.Push(SmalltalkOOP.FromSmallInteger(2)); // 2 args

    // Execute: primitive 80
    BlockContextPrimitives.PrimitiveBlockCopy(vm);

    // Verify: BlockContext created with correct fields
    var blockContext = vm.PopStack();
    Assert.That(vm.FetchClassOf(blockContext), Is.EqualTo(vm.ClassBlockContextPointer));

    var nargsOOP = vm.FetchPointer(VMConstants.BLOCKCONTEXT_NARGS_INDEX, blockContext);
    Assert.That(vm.IntegerValueOf(nargsOOP), Is.EqualTo(2));

    var homeOOP = vm.FetchPointer(VMConstants.BLOCKCONTEXT_HOME_INDEX, blockContext);
    Assert.That(homeOOP, Is.EqualTo(methodContext));
}

[Test]
public void Primitive81_SwitchesContext_AndTransfersArguments()
{
    // Setup: Create BlockContext with nargs=2, push args and block onto stack
    var blockContext = CreateBlockContext(nargs: 2, initialIP: 100);
    vm.Push(SmalltalkOOP.FromSmallInteger(10)); // arg1
    vm.Push(SmalltalkOOP.FromSmallInteger(20)); // arg2
    vm.Push(blockContext);
    vm.ArgumentCount = 2;

    // Execute: primitive 81
    BlockContextPrimitives.PrimitiveValue(vm);

    // Verify: Context switched, arguments transferred
    Assert.That(vm.ActiveContext, Is.EqualTo(blockContext));
    Assert.That(vm.InstructionPointer, Is.EqualTo(100));

    var arg1 = vm.FetchPointer(VMConstants.BLOCKCONTEXT_TEMP_FRAME_START + 0, blockContext);
    var arg2 = vm.FetchPointer(VMConstants.BLOCKCONTEXT_TEMP_FRAME_START + 1, blockContext);
    Assert.That(vm.IntegerValueOf(arg1), Is.EqualTo(10));
    Assert.That(vm.IntegerValueOf(arg2), Is.EqualTo(20));
}
```

---

## Part 2: Offline Compiler Modifications

### Overview

The offline compiler must generate **inline bytecode** for blocks instead of creating separate CompiledMethod objects.

**Current bytecode for `[1 + 2]`**:
```
PUSH_LITERAL 5    ; Literal 5 = CompiledMethod for block
```

**Required bytecode for `[1 + 2]`**:
```
LdThisContext (137)         ; Push current context onto stack
PUSH 0                      ; Push SmallInteger 0 (nargs)
SEND #blockCopy:, 1         ; Primitive 80 creates BlockContext
LONG_JUMP +8                ; Jump over block body (offset filled later)
  ; Block body starts here (PC = initialIP)
  PUSH 1                    ; Block body: 1
  PUSH 2                    ; Block body: 2
  SEND #+, 1                ; Block body: +
  EndRemote (125)           ; Return from block
; Jump lands here
```

---

### Step 1: Analyze Current Block Compilation

**Files to Examine**:
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Main compiler
- Search for BlockNode handling, block-related methods

**Action Items**:
1. Find where BlockNode is compiled (likely `VisitBlockNode` or similar)
2. Identify code that creates CompiledMethod for blocks
3. Identify code that adds blocks to literal array
4. Document current approach for comparison

---

### Step 2: Design Inline Bytecode Generation

**Key Design Decisions**:

1. **LdThisContext bytecode**: Need to emit bytecode 137 (0x89)
   - Check if this bytecode is already defined/handled
   - May need to add to bytecode dispatcher

2. **LONG_JUMP bytecode**: Need 2-byte jump instruction
   - Format: `[opcode] [offset]` where offset is signed byte
   - Need mechanism to "reserve" jump, emit block body, then fill offset
   - Similar to existing jump handling in compiler

3. **EndRemote bytecode**: Need to emit bytecode 125 (0x7D)
   - Check if this bytecode is already defined
   - Terminates block execution, returns to caller

4. **Nested blocks**: Must handle recursively
   - Inner block generates its own inline bytecode
   - Jumps nest correctly

**Pseudo-code**:

```csharp
void CompileBlockNode(BlockNode blockNode)
{
    // 1. Emit LdThisContext
    Emit(0x89);  // LdThisContext (137)

    // 2. Emit PUSH numArgs as SmallInteger
    int numArgs = blockNode.Parameters.Count;
    EmitPushLiteral(numArgs);  // Will push SmallInteger literal

    // 3. Emit SEND blockCopy:
    int blockCopySelector = LookupSelector("blockCopy:");
    EmitSend(blockCopySelector, argumentCount: 1);

    // 4. Emit LONG_JUMP with placeholder offset
    int jumpInstructionOffset = _bytecode.Count;
    Emit(0xA8);  // LONG_JUMP opcode (placeholder)
    Emit(0x00);  // Offset placeholder (will fill later)

    // 5. Compile block body inline
    int blockBodyStart = _bytecode.Count;

    // Save/restore compiler state (temps, etc.)
    var savedScope = PushBlockScope(blockNode);

    // Compile statements in block
    foreach (var statement in blockNode.Statements)
    {
        CompileStatement(statement);
    }

    // Emit EndRemote if last statement isn't already a return
    if (!IsReturn(blockNode.Statements.Last()))
    {
        Emit(0x7D);  // EndRemote (125)
    }

    RestoreScope(savedScope);

    // 6. Calculate and fill LONG_JUMP offset
    int blockBodyEnd = _bytecode.Count;
    int offset = blockBodyEnd - jumpInstructionOffset - 2;  // -2 for jump instruction size

    if (offset < 0 || offset > 255)
        throw new Exception($"Block body too large for LONG_JUMP: {offset} bytes");

    _bytecode[jumpInstructionOffset + 1] = (byte)offset;

    // Note: BlockContext is now on stack, ready for use
}
```

---

### Step 3: Identify Required Bytecode Handlers

**Check if these bytecodes exist in interpreter**:

1. **LdThisContext (137 / 0x89)**: Pushes active context onto stack
   - Search `VirtualMachine.cs` for handling of bytecode 137
   - If missing, add to bytecode dispatcher:
     ```csharp
     case 0x89:  // LdThisContext
         Push(_activeContextOOP);
         break;
     ```

2. **EndRemote (125 / 0x7D)**: Returns from block to caller
   - Search for handling of bytecode 125
   - This is a block return, similar to method return but uses `caller` field:
     ```csharp
     case 0x7D:  // EndRemote (block return)
         var returnValue = Pop();
         var callerOOP = FetchPointer(VMConstants.BLOCKCONTEXT_CALLER_INDEX, _activeContextOOP);
         NewActiveContext(callerOOP);
         Push(returnValue);
         break;
     ```

3. **LONG_JUMP (0xA8?)**: Forward jump by offset
   - Check what jump bytecodes exist
   - May need to use existing jump or add new one
   - Format: `opcode + 1-byte offset`

---

### Step 4: Modify BytecodeCompiler.cs

**File**: `Smalltalk80.Compiler/BytecodeCompiler.cs`

**Changes Required**:

1. **Remove block-as-CompiledMethod logic**:
   ```csharp
   // OLD CODE TO REMOVE:
   private int CompileBlock(BlockNode node)
   {
       var blockMethod = CreateCompiledMethod(node);
       var literalIndex = AddLiteral(blockMethod);
       return literalIndex;
   }
   ```

2. **Add inline block compilation**:
   ```csharp
   // NEW CODE:
   private void CompileBlockInline(BlockNode node)
   {
       // Implementation from Step 2 pseudo-code above
   }
   ```

3. **Update VisitBlockNode** (or equivalent):
   ```csharp
   // OLD:
   public override void VisitBlockNode(BlockNode node)
   {
       int literalIndex = CompileBlock(node);
       EmitPushLiteral(literalIndex);
   }

   // NEW:
   public override void VisitBlockNode(BlockNode node)
   {
       CompileBlockInline(node);
       // BlockContext now on stack
   }
   ```

4. **Handle block scope for variable access**:
   - Block parameters become temps in block's temp frame
   - Block temps also go in temp frame
   - Outer temps accessed via home context (handled by runtime)

---

### Step 5: Add Bytecode Emission Helpers

**File**: `Smalltalk80.Compiler/BytecodeCompiler.cs`

```csharp
/// <summary>
/// Emits LdThisContext bytecode (pushes current context).
/// Blue Book bytecode 137 (0x89).
/// </summary>
private void EmitLdThisContext()
{
    Emit(0x89);
}

/// <summary>
/// Emits EndRemote bytecode (returns from block).
/// Blue Book bytecode 125 (0x7D).
/// </summary>
private void EmitEndRemote()
{
    Emit(0x7D);
}

/// <summary>
/// Emits LONG_JUMP bytecode with offset.
/// Returns the offset location for later patching.
/// </summary>
private int EmitLongJump()
{
    int offsetLocation = _bytecode.Count + 1;
    Emit(0xA8);  // LONG_JUMP opcode (verify this is correct)
    Emit(0x00);  // Placeholder offset
    return offsetLocation;
}

/// <summary>
/// Patches a previously emitted jump offset.
/// </summary>
private void PatchJumpOffset(int offsetLocation, int targetAddress)
{
    int offset = targetAddress - offsetLocation - 1;

    if (offset < 0 || offset > 255)
        throw new CompilerException($"Jump offset out of range: {offset}");

    _bytecode[offsetLocation] = (byte)offset;
}
```

---

### Step 6: Handle Nested Blocks

**Requirement**: Blocks can contain blocks (nested blocks).

**Example**:
```smalltalk
[:x | [:y | x + y] value: 2] value: 1
```

**Generated bytecode**:
```
; Outer block
LdThisContext
PUSH 1              ; nargs=1
SEND blockCopy:
LONG_JUMP +20       ; Jump over outer block body
  ; Outer block body
  LdThisContext
  PUSH 1            ; nargs=1  (inner block)
  SEND blockCopy:
  LONG_JUMP +8      ; Jump over inner block body
    ; Inner block body
    PUSH_TEMP 0     ; x (from grandparent via home chain)
    PUSH_TEMP 1     ; y (from parent)
    SEND +
    EndRemote
  ; Inner jump lands here
  PUSH 2
  SEND value:
  EndRemote
; Outer jump lands here
PUSH 1
SEND value:
```

**Implementation**:
- Recursive compilation: When compiling block body, encountering BlockNode calls `CompileBlockInline` recursively
- Each block generates its own inline sequence
- Jumps calculate offsets correctly for their level

---

### Step 7: Testing Compiler Changes

**Unit Tests** (create `BlockCompilerTests.cs`):

```csharp
[Test]
public void CompileBlock_GeneratesInlineBytecode()
{
    // Source: [1 + 2]
    var source = "[1 + 2]";
    var method = CompileExpression(source);

    var bytecode = method.Bytecode;

    // Verify: LdThisContext
    Assert.That(bytecode[0], Is.EqualTo(0x89));

    // Verify: PUSH 0 (numArgs)
    // Verify: SEND blockCopy:
    // Verify: LONG_JUMP
    // Verify: Block body: PUSH 1, PUSH 2, SEND +
    // Verify: EndRemote (125)
}

[Test]
public void CompileBlock_WithArguments_GeneratesCorrectNargs()
{
    // Source: [:x :y | x + y]
    var source = "[:x :y | x + y]";
    var method = CompileExpression(source);

    // Verify: PUSH 2 (numArgs=2)
    // ... rest of verification
}

[Test]
public void CompileNestedBlocks_GeneratesCorrectJumps()
{
    // Source: [:x | [:y | x + y]]
    var source = "[:x | [:y | x + y]]";
    var method = CompileExpression(source);

    // Verify: Outer block generated
    // Verify: Inner block generated inline within outer
    // Verify: Jumps calculated correctly
}
```

---

## Part 3: Integration Testing

### Phase 1: Primitive Tests

**Goal**: Verify primitives 80-81 work correctly in isolation

**Tests**:
1. Primitive 80 creates BlockContext with correct fields
2. Primitive 81 switches context and transfers arguments
3. Field indices match BlockContext class definition

### Phase 2: Simple Block Tests

**Goal**: Verify compiler generates correct bytecode and runtime executes it

**Tests**:
```smalltalk
; Test 1: Simple 0-arg block
[1 + 2] value.          ; Should return 3

; Test 2: Block with 1 argument
[:x | x + 1] value: 5.  ; Should return 6

; Test 3: Block with 2 arguments
[:x :y | x + y] value: 3 value: 4.  ; Should return 7
```

### Phase 3: Control Structure Tests

**Goal**: Verify whileTrue:, whileFalse:, etc. work

**Tests**:
```smalltalk
; Test 1: whileTrue:
| i sum |
i := 0.
sum := 0.
[i < 5] whileTrue: [
    sum := sum + i.
    i := i + 1
].
sum.  ; Should be 10

; Test 2: ifTrue:ifFalse:
3 > 2 ifTrue: [10] ifFalse: [20].  ; Should be 10
```

### Phase 4: Nested Block Tests

**Goal**: Verify lexical scoping via home chain

**Tests**:
```smalltalk
; Test: Nested block accessing outer temp
[:x |
    [:y | x + y] value: 2
] value: 1.  ; Should be 3
```

### Phase 5: BlockContextTest Suite

**Goal**: Run comprehensive 120-test suite

**Command**:
```bash
dotnet run -- --coldstart coldstart_with_tests.json \
              --execute "BlockContextTest testAllCombinations"
```

**Expected**: All 120 tests pass (sum = 1191)

---

## Part 4: Cleanup and Documentation

### Remove Dead Code

**File**: `VirtualMachine.cs`

Based on `DEAD_CODE_REPORT.md`, remove:
1. Old BlockContext methods (CreateBlockContext, etc.)
2. Obsolete _blockContextMapping code
3. Obsolete VMConstants (BLOCKCONTEXT_STARTPC_INDEX, etc.)

### Update Documentation

**Files to Update**:

1. **CLAUDE.md**:
   - Mark block implementation complete
   - Document primitive 80-81 behavior
   - Update init expression status

2. **Single-Context-Architecture.md**:
   - Update to reflect Blue Book model
   - Document inline bytecode generation
   - Clarify BlockContext vs MethodContext

3. **Variable-Indexing-Architecture.md**:
   - Document BlockContext field layout
   - Explain temp frame structure

---

## Risk Assessment

### High Risk Areas

1. **Field Index Mismatch**:
   - **Risk**: BlockContext field indices don't match actual class layout
   - **Mitigation**: Verify indices from Smalltalk-80.st class definition
   - **Test**: Unit tests verify field access works

2. **Jump Offset Calculation**:
   - **Risk**: LONG_JUMP offsets calculated incorrectly
   - **Mitigation**: Test with various block sizes
   - **Test**: Nested blocks with different sizes

3. **Context Switching**:
   - **Risk**: NewActiveContext doesn't properly save/restore state
   - **Mitigation**: Follow Blue Book specification exactly
   - **Test**: Verify caller field set, IP/SP restored correctly

### Medium Risk Areas

1. **Bytecode Handler Missing**:
   - **Risk**: LdThisContext or EndRemote not implemented
   - **Mitigation**: Search existing bytecode handlers first
   - **Test**: Simple block execution

2. **Block Scope Management**:
   - **Risk**: Block temps conflict with method temps
   - **Mitigation**: Careful scope push/pop in compiler
   - **Test**: Blocks with temps same name as outer temps

---

## Success Criteria

### Milestone 1: Primitives Working
- [ ] Primitive 80 creates BlockContext
- [ ] Primitive 81 executes BlockContext
- [ ] Unit tests pass

### Milestone 2: Compiler Working
- [ ] Simple blocks compile to inline bytecode
- [ ] Blocks with arguments compile correctly
- [ ] Nested blocks compile correctly

### Milestone 3: Integration Working
- [ ] `[1 + 2] value` returns 3
- [ ] `[:x | x + 1] value: 5` returns 6
- [ ] `whileTrue:` loop executes

### Milestone 4: Full Validation
- [ ] BlockContextTest suite passes (120/120)
- [ ] Init expressions execute (45 with blocks)
- [ ] No regressions in existing tests

---

## Estimated Timeline

**Day 1: Interpreter**
- Morning: Implement primitive 80 (2 hours)
- Afternoon: Implement primitive 81 (2 hours)
- Evening: Unit tests (2 hours)

**Day 2: Compiler**
- Morning: Analyze current block compilation (1 hour)
- Morning: Design inline bytecode generation (2 hours)
- Afternoon: Implement CompileBlockInline (3 hours)
- Evening: Handle nested blocks (2 hours)

**Day 3: Testing**
- Morning: Simple block tests (2 hours)
- Afternoon: Control structure tests (2 hours)
- Evening: BlockContextTest suite (2 hours)

**Total**: ~18-20 hours of focused development

---

## Appendices

### Appendix A: Blue Book Bytecode Reference

**Relevant Bytecodes**:
- **137 (0x89)**: LdThisContext - Push active context onto stack
- **125 (0x7D)**: EndRemote - Return from block to caller
- **Jump bytecodes**: Need to verify which is LONG_JUMP

### Appendix B: Field Index Verification Checklist

```
[ ] Examine Smalltalk-80.st line 4682 (BlockContext class)
[ ] Count instance variables from inheritance chain:
    InstructionStream: sender, instructionPointer
    ContextPart: stackPointer
    BlockContext: nargs, initialIP, home
[ ] Verify indices match VMConstants
[ ] Test with unit test accessing each field
```

### Appendix C: Compiler Change Checklist

```
[ ] Remove CreateCompiledMethod for blocks
[ ] Remove AddBlockToLiterals
[ ] Implement EmitLdThisContext
[ ] Implement EmitEndRemote
[ ] Implement EmitLongJump with offset patching
[ ] Update VisitBlockNode to call CompileBlockInline
[ ] Handle block scope (parameters, temps)
[ ] Test simple blocks
[ ] Test blocks with arguments
[ ] Test nested blocks
```

---

**Document Version**: 1.0
**Last Updated**: 2025-11-06
**Status**: Ready for implementation
