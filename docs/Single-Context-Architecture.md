# Single-Context Architecture: Unified Block and Method Execution

**Document Version**: 3.0
**Date**: 2025-11-06
**Status**: DEFINITIVE ARCHITECTURAL SPECIFICATION
**Author**: Simplified clean-slate design

---

## Table of Contents

1. [Core Insight](#1-core-insight)
2. [Architecture Overview](#2-architecture-overview)
3. [Runtime Objects](#3-runtime-objects)
4. [Variable Access Algorithm](#4-variable-access-algorithm)
5. [Block Invocation](#5-block-invocation)
6. [Non-Local Returns](#6-non-local-returns)
7. [Complete Algorithms](#7-complete-algorithms)
8. [Implementation Plan](#8-implementation-plan)
9. [Migration Path](#9-migration-path)

---

## 1. Core Insight

### 1.1 Fundamental Truth

**A block is just an anonymous method.**

The only conceptual differences are:
1. **Lexical scope**: Blocks can access variables from their defining context
2. **Non-local return**: `^` in a block returns from the defining method
3. **First-class value**: Blocks can be passed as arguments

At the bytecode execution level: **A block IS a CompiledMethod that accesses parent variables.**

### 1.2 Design Principle

**One context type for everything: MethodContext**

- Regular methods use MethodContext
- Blocks use MethodContext
- Both execute bytecode identically
- Both store args/temps in indexed slots identically

**The ONLY difference**: Blocks have a lexicalParent link for variable access.

### 1.3 Benefits

✅ **Simplicity**: One context type, one execution model
✅ **Correctness**: Direct mapping to compiler's hierarchical index generation
✅ **Performance**: No type checking, no special cases
✅ **Maintainability**: Less code, clearer semantics

---

## 2. Architecture Overview

### 2.1 Object Types

```
┌─────────────────────────────────────────────────────┐
│ CompiledMethod (Bytecode container)                 │
│   - bytecode: ByteArray                             │
│   - literals: Array                                 │
│   - header: SmallInteger                            │
│                                                      │
│ Used for: Regular methods AND blocks                │
│ Stored in: Method dictionaries (methods)            │
│           Literal arrays (blocks)                   │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ MethodContext (Execution state)                     │
│                                                      │
│ Instance Variables (fixed slots):                   │
│   slot 0: class (MethodContext class OOP)           │
│   slot 1: sender (return point - dynamic chain)     │
│   slot 2: pc (program counter)                      │
│   slot 3: stackp (stack pointer)                    │
│   slot 4: method (CompiledMethod OOP)               │
│   slot 5: lexicalParent (scope chain - NEW!)        │
│   slot 6: receiver (self)                           │
│                                                      │
│ Indexed Slots (variable length):                    │
│   [arg0, arg1, ..., temp0, temp1, ..., stack...]    │
│                                                      │
│ Used for: Regular methods AND blocks                │
│ Created: On method/block invocation                 │
│ Pooled: On return                                   │
└─────────────────────────────────────────────────────┘
```

### 2.2 Chains

**Dynamic Chain** (sender links):
```
Represents the call stack (where control returns)

method1
  ↓ sender
method2
  ↓ sender
block1
  ↓ sender
method3
  ↓ sender
block2
```

**Lexical Chain** (lexicalParent links):
```
Represents the scope hierarchy (where variables live)

method1 (lexicalParent = nil)
  ↑ lexicalParent
block1 (defined in method1)
  ↑ lexicalParent
block2 (defined in block1)
```

**Key Insight**: These are DIFFERENT chains!
- sender = dynamic (follows execution)
- lexicalParent = static (follows definition)

### 2.3 Lifetime

```
METHOD INVOCATION:
1. Create MethodContext
   - sender = _activeContext
   - method = target CompiledMethod
   - receiver = receiver argument
   - lexicalParent = nil (methods have no parent scope)
   - indexed slots = [args..., temps..., stack...]
2. Switch _activeContext to new context
3. Execute bytecode
4. On return: restore sender, pool context

BLOCK INVOCATION (from any context):
1. Extract block CompiledMethod from literal
2. Create MethodContext
   - sender = _activeContext (where to return)
   - method = block CompiledMethod
   - receiver = _activeContext.receiver (inherit self)
   - lexicalParent = _activeContext (THE UNIVERSAL RULE!)
   - indexed slots = [args..., temps..., stack...]
3. Switch _activeContext to new context
4. Execute bytecode
5. On return: restore sender, pool context

CRITICAL INSIGHT: lexicalParent = _activeContext ALWAYS
  - If _activeContext is a method → block's lexicalParent = method
  - If _activeContext is a block → block's lexicalParent = outer block
  - This naturally builds the lexical chain for any nesting depth!

NESTED BLOCK EXAMPLE:
  method (lexicalParent=nil)
    ↓ creates outer block
  outer block (lexicalParent=method)
    ↓ creates inner block
  inner block (lexicalParent=outer block)
    ↓ creates deeply nested block
  deeply nested block (lexicalParent=inner block)

The chain builds automatically because we always set:
  newBlock.lexicalParent = _activeContext (whoever is running NOW)
```

---

## 3. Runtime Objects

### 3.1 MethodContext Layout

**Smalltalk Class Definition** (in Smalltalk-80.st):
```smalltalk
Object subclass: #MethodContext
    instanceVariableNames: 'sender pc stackp method lexicalParent receiver'
    ...
```

**Memory Layout**:
```
Slot Index | Field           | Type              | Purpose
-----------|-----------------|-------------------|----------------------------------
0          | class           | ClassOOP          | MethodContext class reference
1          | sender          | MethodContext OOP | Return point (dynamic chain)
2          | pc              | SmallInteger      | Program counter
3          | stackp          | SmallInteger      | Stack pointer
4          | method          | CompiledMethod    | Bytecode being executed
5          | lexicalParent   | MethodContext OOP | Scope parent (lexical chain)
6          | receiver        | Any OOP           | self
-----------+                 +                   +
Indexed    | arg0            | Any OOP           | First argument
Indexed+1  | arg1            | Any OOP           | Second argument
...        | ...             | ...               | ...
Indexed+N  | temp0           | Any OOP           | First temporary
...        | ...             | ...               | ...
Indexed+M  | stack slot      | Any OOP           | Evaluation stack
```

**Constants** (VMConstants.cs):
```csharp
public const int CONTEXT_SENDER_INDEX = 1;
public const int CONTEXT_PC_INDEX = 2;
public const int CONTEXT_STACKP_INDEX = 3;
public const int CONTEXT_METHOD_INDEX = 4;
public const int CONTEXT_LEXICALPARENT_INDEX = 5;
public const int CONTEXT_RECEIVER_INDEX = 6;
public const int CONTEXT_TEMP_FRAME_START = 7; // Start of indexed slots
```

### 3.2 VM State

The VM maintains minimal state:

```csharp
public class VirtualMachine
{
    // Execution state
    private SmalltalkOOP _activeContextOOP;  // Current executing context

    // Context pool (for performance)
    private List<SmalltalkOOP> _contextPool;

    // Non-local return state
    private bool _nlrActive;
    private SmalltalkOOP _nlrTargetContext;
    private SmalltalkOOP _nlrReturnValue;

    // That's it! No BlockContext cache, no block mappings, no complexity.
}
```

---

## 4. Variable Access Algorithm

### 4.1 Compiler Contract

The compiler generates hierarchical indices using this formula:

```
Index = LocalOffset + Σ(BlockLocalCounts of all child scopes)
```

**Example**:
```smalltalk
method: arg          "← method arg0 = index 2 (after block locals)"
    | temp |         "← method temp0 = index 3 (after block locals)"
    [:blockArg |     "← block arg0 = index 0"
        | blockTemp | "← block temp0 = index 1"
        arg          "← PUSH_TEMP 2 (0+1+1=2, skip block's 2 locals)"
        temp         "← PUSH_TEMP 3 (1+1+1=3, skip block's 2 locals)"
        blockArg     "← PUSH_TEMP 0 (direct access)"
        blockTemp    "← PUSH_TEMP 1 (direct access)"
    ]
```

### 4.2 Interpreter Algorithm

**PUSH_TEMP (read variable)**:
```
INPUT: Bytecode index (from compiler)
OUTPUT: Variable value pushed onto stack

1. Start at _activeContext
2. Calculate remaining index = index
3. Get context's local count (args + temps from method header)

4. LOOP until variable found:
   IF remainingIndex < localCount:
       # Found it in this context
       value = context.indexedSlots[remainingIndex]
       Push(value)
       RETURN
   ELSE:
       # Not here, traverse to parent
       remainingIndex -= localCount
       context = context.lexicalParent
       IF context == nil:
           ERROR: "Variable index out of bounds"
       localCount = GetLocalCount(context.method)
       CONTINUE
```

**STORE_TEMP (write variable)**:
```
Same algorithm, but:
   context.indexedSlots[remainingIndex] = value
```

### 4.3 Implementation

```csharp
private void PushTemporary(int index)
{
    SmalltalkOOP contextOOP = _activeContextOOP;
    int remainingIndex = index;

    while (true)
    {
        // Get local count for this context
        var methodOOP = _heapManager.GetInstanceVariable(
            contextOOP,
            VMConstants.CONTEXT_METHOD_INDEX
        );
        var header = ParseMethodHeader(methodOOP);
        int localCount = header.NumArgs + header.NumTemps;

        if (remainingIndex < localCount)
        {
            // Found it!
            var value = _heapManager.GetIndexedOOP(contextOOP, remainingIndex);
            Push(value);
            return;
        }

        // Not here, traverse to parent
        remainingIndex -= localCount;
        contextOOP = _heapManager.GetInstanceVariable(
            contextOOP,
            VMConstants.CONTEXT_LEXICALPARENT_INDEX
        );

        if (contextOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException(
                $"Variable index {index} out of bounds (traversed all contexts)");
        }
    }
}

private void StoreTemporary(int index, SmalltalkOOP value)
{
    SmalltalkOOP contextOOP = _activeContextOOP;
    int remainingIndex = index;

    while (true)
    {
        var methodOOP = _heapManager.GetInstanceVariable(
            contextOOP,
            VMConstants.CONTEXT_METHOD_INDEX
        );
        var header = ParseMethodHeader(methodOOP);
        int localCount = header.NumArgs + header.NumTemps;

        if (remainingIndex < localCount)
        {
            // Found it!
            _heapManager.SetIndexedOOP(contextOOP, remainingIndex, value);
            return;
        }

        remainingIndex -= localCount;
        contextOOP = _heapManager.GetInstanceVariable(
            contextOOP,
            VMConstants.CONTEXT_LEXICALPARENT_INDEX
        );

        if (contextOOP == SpecialObjectsArray.Instance.Nil)
        {
            throw new InvalidOperationException(
                $"Variable index {index} out of bounds");
        }
    }
}
```

### 4.4 Correctness Proof

**Claim**: This algorithm correctly implements the compiler's index generation.

**Proof**:
1. Compiler generates: `index = localOffset + Σ(child counts)`
2. Interpreter subtracts: `remainingIndex -= localCount` for each child
3. After N subtractions: `remainingIndex = index - Σ(child counts) = localOffset`
4. Access at localOffset in parent context ✓

---

## 5. Block Invocation

### 5.1 Identifying Blocks

**Question**: How do we know a literal is a block?

**Answer**: A block literal is simply a CompiledMethod in the literal array:

```csharp
private bool IsBlockLiteral(SmalltalkOOP literalOOP)
{
    // A block literal is simply a CompiledMethod
    // (Methods store blocks as CompiledMethod literals in their literal array)
    var classOOP = _heapManager.GetClass(literalOOP);
    var className = GetClassName(classOOP);
    return className == "CompiledMethod";
}
```

**Note**: In practice, this check is rarely needed. The VM treats block literals and regular literals identically - both are just pushed onto the stack. The distinction only matters during block invocation (primitive 207).

### 5.2 Push Literal Handler

```csharp
case PUSH_LITERAL:
    {
        byte literalIndex = bytecodes[ip++];

        var methodOOP = _heapManager.GetInstanceVariable(
            _activeContextOOP,
            VMConstants.CONTEXT_METHOD_INDEX
        );

        var literalOOP = GetLiteral(methodOOP, literalIndex);

        // Push the literal (whether it's a block CompiledMethod or regular literal)
        // Blocks will be invoked later via primitive 207
        Push(literalOOP);

        return true;
    }
```

**Key Insight**: We don't create BlockContext objects! We just push literals - whether they're block CompiledMethods or regular values. The VM doesn't need to distinguish between them at push time.

### 5.3 Primitive 207 (value, value:, value:value:)

When Smalltalk code calls `aBlock value`, it invokes primitive 207:

```csharp
public static SmalltalkOOP BlockValue(
    IPrimitiveContext ctx,
    SmalltalkOOP receiverOOP,  // The block CompiledMethod
    SmalltalkOOP[] args)
{
    // Receiver is the block CompiledMethod
    var blockMethodOOP = receiverOOP;

    // Parse method header for metadata
    var header = ctx.ParseMethodHeader(blockMethodOOP);
    int expectedArgs = header.NumArgs;
    int tempCount = header.NumTemps;
    int stackSize = header.NeedsLargeFrame ? 48 : 16;

    // Validate argument count
    if (args.Length != expectedArgs)
    {
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    // Get current context (this is the lexical parent!)
    var currentContext = ctx.GetActiveContext();

    // Get receiver from current context (blocks inherit self)
    var receiverFromContext = ctx.GetInstanceVariable(
        currentContext,
        VMConstants.CONTEXT_RECEIVER_INDEX
    );

    // Create new MethodContext for block execution
    int totalIndexedSlots = expectedArgs + tempCount + stackSize;
    var blockContextOOP = ctx.AllocateMethodContext(
        blockMethodOOP,
        receiverFromContext,  // Inherit receiver
        currentContext,       // sender (return point)
        currentContext,       // lexicalParent (THIS IS THE KEY!)
        totalIndexedSlots
    );

    // Copy arguments to indexed slots
    for (int i = 0; i < args.Length; i++)
    {
        ctx.SetIndexedOOP(blockContextOOP, i, args[i]);
    }

    // Initialize temps to nil
    var nilOOP = SpecialObjectsArray.Instance.Nil;
    for (int i = expectedArgs; i < expectedArgs + tempCount; i++)
    {
        ctx.SetIndexedOOP(blockContextOOP, i, nilOOP);
    }

    // Switch context (trampoline)
    ctx.SwitchContext(blockContextOOP);

    return nilOOP; // Sentinel
}
```

**Critical Detail**: `lexicalParent = currentContext`

This creates the scope chain automatically!

---

## 6. Non-Local Returns

### 6.1 Semantics

When a block executes `^value`, control returns to the **defining method's caller**, not the block's caller.

**Example**:
```smalltalk
wrapper
    ^self helper: [^42]

helper: aBlock
    ^aBlock value + 100  "Never executes +"

test
    result := self wrapper.
    "result = 42, NOT 142"
```

### 6.2 Implementation Challenge

**Question**: How do we find the "defining method" to return from?

**Answer**: Walk the lexicalParent chain until we find a context with `lexicalParent = nil` (that's the method).

### 6.3 Return Bytecode Handler

```csharp
case 0x7E: // RETURN_TOP_POP (^)
    {
        var returnValue = Pop();

        // Check if we're in a block (has lexical parent)
        var lexicalParentOOP = _heapManager.GetInstanceVariable(
            _activeContextOOP,
            VMConstants.CONTEXT_LEXICALPARENT_INDEX
        );

        if (lexicalParentOOP != SpecialObjectsArray.Instance.Nil)
        {
            // We're in a block - find defining method
            var targetContextOOP = FindDefiningMethod(_activeContextOOP);

            // Validate target is in sender chain (safety check)
            if (!IsInSenderChain(targetContextOOP))
            {
                SignalError("cannotReturn:");
                return false;
            }

            // Initiate non-local return
            _nlrActive = true;
            _nlrTargetContext = targetContextOOP;
            _nlrReturnValue = returnValue;

            return false; // Exit bytecode loop
        }
        else
        {
            // Normal method return
            return HandleNormalReturn(returnValue);
        }
    }

private SmalltalkOOP FindDefiningMethod(SmalltalkOOP contextOOP)
{
    var currentOOP = contextOOP;

    while (true)
    {
        var lexicalParentOOP = _heapManager.GetInstanceVariable(
            currentOOP,
            VMConstants.CONTEXT_LEXICALPARENT_INDEX
        );

        if (lexicalParentOOP == SpecialObjectsArray.Instance.Nil)
        {
            // This context has no lexical parent - it's the defining method
            return currentOOP;
        }

        currentOOP = lexicalParentOOP;
    }
}

private bool IsInSenderChain(SmalltalkOOP targetOOP)
{
    var currentOOP = _activeContextOOP;
    int depth = 0;

    while (currentOOP != SpecialObjectsArray.Instance.Nil)
    {
        if (currentOOP == targetOOP)
            return true;

        if (depth++ > 5000)
            throw new InvalidOperationException("Sender chain too deep");

        currentOOP = _heapManager.GetInstanceVariable(
            currentOOP,
            VMConstants.CONTEXT_SENDER_INDEX
        );
    }

    return false;
}
```

### 6.4 Context Unwinding

```csharp
private InterpreterStatus HandleReturn(SmalltalkOOP returnValue)
{
    if (_nlrActive)
    {
        if (_activeContextOOP == _nlrTargetContext)
        {
            // Reached target - complete the NLR
            var senderOOP = _heapManager.GetInstanceVariable(
                _activeContextOOP,
                VMConstants.CONTEXT_SENDER_INDEX
            );

            PoolContext(_activeContextOOP);
            _activeContextOOP = senderOOP;
            Push(_nlrReturnValue);

            _nlrActive = false;
            _nlrTargetContext = default;
            _nlrReturnValue = default;

            return InterpreterStatus.Continue;
        }
        else
        {
            // Keep unwinding
            var senderOOP = _heapManager.GetInstanceVariable(
                _activeContextOOP,
                VMConstants.CONTEXT_SENDER_INDEX
            );

            PoolContext(_activeContextOOP);
            _activeContextOOP = senderOOP;

            return InterpreterStatus.Continue;
        }
    }
    else
    {
        // Normal return
        var senderOOP = _heapManager.GetInstanceVariable(
            _activeContextOOP,
            VMConstants.CONTEXT_SENDER_INDEX
        );

        PoolContext(_activeContextOOP);
        _activeContextOOP = senderOOP;
        Push(returnValue);

        return InterpreterStatus.Continue;
    }
}
```

---

## 7. Complete Algorithms

### 7.1 Fast-Path Temp Access (0x10-0x1F)

For indices 0-15, use fast-path bytecodes:

```csharp
if (BytecodeUtil.IsPushTemp(bytecode))
{
    int index = BytecodeUtil.PushTempIndex(bytecode);
    PushTemporary(index);
    return true;
}

if (BytecodeUtil.IsStoreTemp(bytecode))
{
    int index = BytecodeUtil.StoreTempIndex(bytecode);
    var value = Peek(); // Don't pop (STORE, not POP_STORE)
    StoreTemporary(index, value);
    return true;
}

if (BytecodeUtil.IsPopStoreTemp(bytecode))
{
    int index = BytecodeUtil.PopStoreTempIndex(bytecode);
    var value = Pop();
    StoreTemporary(index, value);
    return true;
}
```

### 7.2 Extended Temp Access (0x03, 0x04, 0x05)

For indices > 15, use extended bytecodes with explicit index:

```csharp
case 0x03: // PUSH_TEMP
    {
        byte index = bytecodes[ip++];
        PushTemporary(index);
        return true;
    }

case 0x04: // STORE_TEMP
    {
        byte index = bytecodes[ip++];
        var value = Peek();
        StoreTemporary(index, value);
        return true;
    }

case 0x05: // POP_STORE_TEMP
    {
        byte index = bytecodes[ip++];
        var value = Pop();
        StoreTemporary(index, value);
        return true;
    }
```

### 7.3 Method Context Creation

```csharp
private SmalltalkOOP AllocateMethodContext(
    SmalltalkOOP methodOOP,
    SmalltalkOOP receiverOOP,
    SmalltalkOOP senderOOP,
    SmalltalkOOP lexicalParentOOP,
    int indexedSlotCount)
{
    var methodContextClassOOP = RuntimeSpecialObjects.Instance.MethodContextClass;
    int instanceVarCount = 6; // sender, pc, stackp, method, lexicalParent, receiver

    var contextOOP = _heapManager.AllocateObject(
        ObjectFormat.PointerIndexed,
        instanceVarCount,
        indexedSlotCount
    );

    _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP);
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_SENDER_INDEX, senderOOP);
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_PC_INDEX, SmalltalkOOP.FromSmallInteger(0));
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_STACKP_INDEX, SmalltalkOOP.FromSmallInteger(0));
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_METHOD_INDEX, methodOOP);
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_LEXICALPARENT_INDEX, lexicalParentOOP);
    _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_RECEIVER_INDEX, receiverOOP);

    return contextOOP;
}
```

---

## 8. Implementation Plan

### Phase 1: Update Class Definition ✓ Ready

**File**: `Smalltalk-80 Source/Smalltalk-80.st`

```smalltalk
Object subclass: #MethodContext
    instanceVariableNames: 'sender pc stackp method lexicalParent receiver'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!
```

**Action**:
1. Locate MethodContext class definition (around line 27000-28000)
2. Change instanceVariableNames to include 'lexicalParent'
3. Remove BlockContext class entirely (if it exists as separate class)

### Phase 2: Update VMConstants.cs ✓ Ready

**File**: `Smalltalk80.Interpreter/Core/VM/VMConstants.cs`

```csharp
// MethodContext fields (unified for methods and blocks)
public const int CONTEXT_SENDER_INDEX = 1;
public const int CONTEXT_PC_INDEX = 2;
public const int CONTEXT_STACKP_INDEX = 3;
public const int CONTEXT_METHOD_INDEX = 4;
public const int CONTEXT_LEXICALPARENT_INDEX = 5;  // NEW!
public const int CONTEXT_RECEIVER_INDEX = 6;
public const int CONTEXT_TEMP_FRAME_START = 7;     // Updated offset

// Remove all METHODCONTEXT_* and BLOCKCONTEXT_* constants (obsolete)
```

### Phase 3: Implement Core Functions ✓ Ready

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

Implement these functions:

1. `PushTemporary(int index)` - Section 4.3
2. `StoreTemporary(int index, SmalltalkOOP value)` - Section 4.3
3. `AllocateMethodContext(...)` - Section 7.3
4. `FindDefiningMethod(SmalltalkOOP contextOOP)` - Section 6.3
5. `IsInSenderChain(SmalltalkOOP targetOOP)` - Section 6.3

### Phase 4: Update Bytecode Handlers ✓ Ready

**File**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

Replace existing handlers:

1. **Fast-path temps** (0x10-0x1F, 0x40-0x47, 0x60-0x67) - Section 7.1
2. **Extended temps** (0x03, 0x04, 0x05) - Section 7.2
3. **Push literal** (0x20-0x3F, 0x01 extended) - Section 5.2
4. **Return** (0x7E) - Section 6.3

### Phase 5: Update Primitive 207 ✓ Ready

**File**: `Smalltalk80.Interpreter/Core/VM/Primitives/BlockContextPrimitives.cs`

Replace with implementation from Section 5.3.

### Phase 6: Remove Obsolete Code ✓ Ready

Delete or comment out:

1. `BlockContext` class references
2. `BlockContext` class (if purely VM-internal)
3. `_blockContextMapping` dictionary
4. `_blockContextCache` dictionary
5. `FindNearestBlockExecutionContext()` function
6. `ResolveHomeContext()` function
7. `CreateBlockExecutionContext()` function
8. All special-case block handling

### Phase 7: Recompile Bundle ✓ Ready

```bash
cd Smalltalk80.Compiler
dotnet run -- \
  "../Smalltalk-80 Source/Smalltalk-80.st" \
  "../Smalltalk-80 Source/Tests/BlockContextTest.st" \
  "../bin/Debug/net9.0/coldstart_with_tests.json"
```

### Phase 8: Test ✓ Ready

```bash
cd bin/Debug/net9.0
./Smalltalk80.Interpreter.exe \
  --coldstart coldstart_with_tests.json \
  --skip-init-expressions \
  --execute "BlockContextTest testAllCombinations"
```

**Expected**: All 120 tests pass.

---

## 9. Migration Path

### 9.1 Compatibility Concerns

**Q**: What breaks when we remove BlockContext/BlockContext?

**A**:
- If Smalltalk code references BlockContext/BlockContext directly → Need compatibility stubs
- If only VM uses them → Can delete immediately

**Action**: Search Smalltalk-80.st for references to BlockContext and BlockContext.

### 9.2 Incremental Steps

**Option A**: Big bang replacement
- Implement entire architecture at once
- Test thoroughly
- Single commit

**Option B**: Incremental migration
1. Add lexicalParent field (alongside existing BlockContext support)
2. Implement new variable access (alongside old)
3. Switch flag to use new algorithm
4. Remove old code after validation

**Recommendation**: Option A (simpler, cleaner)

### 9.3 Rollback Plan

If implementation fails:
1. `git revert HEAD` - revert to previous commit
2. Review test failures
3. Add diagnostic logging
4. Fix issues
5. Retry

---

## 10. Validation Criteria

### 10.1 Correctness Tests

✅ **BlockContextTest testAllCombinations**: All 120 tests pass
✅ **Init expressions**: All execute without errors
✅ **No "generality called on nil"**: Variable access returns correct values
✅ **Nested blocks work**: 3-level nesting accesses grandparent variables
✅ **Non-local returns work**: Control returns to defining method's caller

### 10.2 Performance Metrics

✅ **Variable access**: O(nesting depth) - typically 1-3 levels
✅ **Context creation**: No cache lookups needed
✅ **Memory**: Fewer objects (no BlockContext, no BlockContext)

### 10.3 Code Quality

✅ **Lines of code**: ~50% reduction in block-related code
✅ **Complexity**: No type checking, no special cases
✅ **Maintainability**: Single execution model for everything

---

## 11. Summary

**Core Innovation**: Blocks are just methods with a lexicalParent link.

**Key Simplifications**:
- ❌ No BlockContext class
- ❌ No BlockContext class
- ❌ No block mappings
- ❌ No block caching
- ❌ No type checking (method vs block)
- ❌ No special cases

**What Remains**:
- ✅ MethodContext (unified for methods and blocks)
- ✅ CompiledMethod (used for methods and blocks)
- ✅ lexicalParent field (new)
- ✅ Hierarchical variable access (simple algorithm)

**Result**: Simpler, faster, more maintainable, and provably correct.
