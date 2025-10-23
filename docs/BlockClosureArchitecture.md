# BlockClosure Architecture

**Date**: 2025-01-23
**Status**: Defined & Ready for Implementation

## Overview

BlockClosure is the runtime representation of block literals in Smalltalk-80. It wraps a CompiledMethod and captures the lexical environment where the block was defined, enabling proper lexical scoping, non-local returns, and closure semantics.

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
