# BlockClosure Implementation Plan

**Date**: 2025-01-23
**Status**: Ready for VM Integration

## Current State Analysis

### ✅ What's Already Working

1. **Compiler** (BytecodeCompiler.cs):
   - ✅ Emits blocks as separate CompiledMethod objects
   - ✅ Pushes blocks as literal constants (0x20-0x3F bytecodes)
   - ✅ Tracks block arguments and temporaries correctly
   - ✅ Records scope depth for nested blocks
   - ✅ Copies parent scope variables into block environment

2. **Cold Start** (Phase8_MethodInstallation.cs):
   - ✅ Builds blockOwnerMap by scanning method literals
   - ✅ Stores blockOwnerMap in GlobalRegistry
   - ✅ Supports nested blocks with ownership chains

3. **VM Execution** (VirtualMachine.cs):
   - ✅ FindMethodContextByLocalId can find home context by owner
   - ✅ InvokeBlockInternal uses homeContext for lexical scoping
   - ✅ pushTemporary correctly reads from _activeContextOOP
   - ✅ pushReceiver uses home context in blocks

4. **Documentation**:
   - ✅ BlockClosure.st - Full Smalltalk-80 class definition
   - ✅ BlockValidatorTest.st - Comprehensive test suite
   - ✅ Phase0X_BlockOwnershipValidation.cs - Validation infrastructure
   - ✅ BlockClosureArchitecture.md - Complete specification

### ⏳ What Needs Implementation

1. **BlockClosure Class in Image** (BLOCKING):
   - ❌ BlockClosure.st not yet compiled into coldstart.json
   - ❌ Need to add BlockClosure to Smalltalk-80.st or compile separately
   - ❌ VM needs BlockClosure class OOP to create instances

2. **VM Runtime Wrapping** (pushLiteral):
   - ❌ Detect block literals (check if localId starts with "block:")
   - ❌ Wrap block CompiledMethod in BlockClosure instance
   - ❌ Capture current _activeContextOOP as homeContext
   - ❌ Push BlockClosure instead of raw CompiledMethod

3. **VM Block Invocation** (SendMessage):
   - ❌ Detect BlockClosure as receiver for #value messages
   - ❌ Extract homeContext from BlockClosure instead of searching
   - ❌ Remove dependency on FindMethodContextByLocalId

## Implementation Strategy

### Option A: Compile BlockClosure into Image (RECOMMENDED)

**Steps**:
1. Add BlockClosure.st to Smalltalk-80.st file
2. Recompile entire image with compiler
3. BlockClosure class becomes available in coldstart.json
4. VM can instantiate BlockClosure using GlobalRegistry

**Pros**:
- Clean, proper Smalltalk-80 way
- BlockClosure available for Smalltalk code to use
- Can call #value, #value:, etc. as real methods
- Full introspection support

**Cons**:
- Requires image recompilation
- Chicken-and-egg: need working blocks to compile BlockClosure?
- May break existing init expressions during transition

**Decision**: This is the correct long-term solution

### Option B: Create BlockClosure Programmatically (INTERIM)

**Steps**:
1. Define BlockClosure class structure in C# (hardcoded)
2. Create instances directly in pushLiteral without class lookup
3. Store as special tagged OOP or in separate data structure
4. VM recognizes BlockClosure by checking data structure membership

**Pros**:
- Can test immediately without image recompilation
- No dependency on BlockClosure.st being compiled
- Allows incremental testing

**Cons**:
- Hack/workaround, not proper Smalltalk
- Can't call BlockClosure methods from Smalltalk
- Need to remove later when proper implementation done

**Decision**: Use as stepping stone for testing

## Phased Implementation Plan

### Phase 1: Programmatic BlockClosure (Immediate Testing)

**Goal**: Test BlockClosure wrapping without recompiling image

**Changes**:

1. **Create BlockClosureInfo struct in VirtualMachine.cs**:
```csharp
private struct BlockClosureInfo
{
    public SmalltalkOOP BlockMethodOOP { get; set; }
    public SmalltalkOOP HomeContextOOP { get; set; }
    public SmalltalkOOP ReturnContextOOP { get; set; }
    public int NumArgs { get; set; }
}

private Dictionary<SmalltalkOOP, BlockClosureInfo> _blockClosures = new();
```

2. **Modify pushLiteral to wrap blocks** (VirtualMachine.cs:333):
```csharp
if (BytecodeUtil.IsPushLiteralConst(bytecode))
{
    int index = BytecodeUtil.PushLiteralConstIndex(bytecode);
    var literal = GetMethodLiteral(methodOOP, index + 1);

    // Check if this literal is a block
    if (IsBlockLiteral(literal))
    {
        // Create pseudo-BlockClosure by wrapping in dictionary
        var closureOOP = literal;  // Use block method OOP as closure ID
        _blockClosures[closureOOP] = new BlockClosureInfo
        {
            BlockMethodOOP = literal,
            HomeContextOOP = _activeContextOOP,
            ReturnContextOOP = _activeContextOOP,
            NumArgs = GetBlockArgCount(literal)
        };
        Push(closureOOP);
    }
    else
    {
        Push(literal);
    }
    return true;
}
```

3. **Add IsBlockLiteral helper**:
```csharp
private bool IsBlockLiteral(SmalltalkOOP oop)
{
    if (oop.IsSmallInteger)
        return false;

    string? localId = GetLocalId(oop);
    return localId != null && localId.StartsWith("block:");
}
```

4. **Modify InvokeBlockInternal to use BlockClosureInfo** (VirtualMachine.cs:1492):
```csharp
// Check if this is a wrapped BlockClosure
if (_blockClosures.TryGetValue(blockMethodOOP, out var closureInfo))
{
    homeContextOOP = closureInfo.HomeContextOOP;
    // No need to search - we have it!
}
else
{
    // Fallback: use existing FindMethodContextByLocalId
    homeContextOOP = FindMethodContextByLocalId(ownerLocalId);
}
```

**Testing**:
- Run init#1, init#2, init#3
- Verify blocks access correct lexical environment
- Check that homeContext is captured at creation time

### Phase 2: Proper BlockClosure Class (Image Integration)

**Goal**: Make BlockClosure a real Smalltalk-80 class

**Changes**:

1. **Add BlockClosure.st to main image**:
   - Merge BlockClosure.st content into Smalltalk-80.st
   - Place after Object, before Collection classes
   - Recompile entire image

2. **Modify VM to use real BlockClosure class**:
```csharp
// Get BlockClosure class from GlobalRegistry
if (!GlobalRegistry.TryGet("class:BlockClosure", out SmalltalkOOP blockClosureClassOOP))
{
    throw new InvalidOperationException("BlockClosure class not found");
}

// Allocate BlockClosure instance
int instVarCount = _layoutResolver.GetInstanceVariableCount("class:BlockClosure");
var closureOOP = _heapManager.AllocateObject(ObjectFormat.Pointer, instVarCount, 0);
_heapManager.SetSlot(closureOOP, 0, blockClosureClassOOP);

// Initialize instance variables
int methodIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "method");
int homeContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "homeContext");
int returnContextIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "returnContext");
int numArgsIndex = _layoutResolver.GetFieldIndex("class:BlockClosure", "numArgs");

_heapManager.SetInstanceVariable(closureOOP, methodIndex, blockMethodOOP);
_heapManager.SetInstanceVariable(closureOOP, homeContextIndex, _activeContextOOP);
_heapManager.SetInstanceVariable(closureOOP, returnContextIndex, _activeContextOOP);
_heapManager.SetInstanceVariable(closureOOP, numArgsIndex, SmalltalkOOP.FromSmallInteger(numArgs));
```

3. **Implement Primitive 207 (valueWithArguments:)**:
```csharp
public SmalltalkOOP Primitive207_ValueWithArguments(SmalltalkOOP receiverOOP, SmalltalkOOP[] args)
{
    // Extract BlockClosure components
    var methodOOP = _heapManager.GetInstanceVariable(receiverOOP, methodIndex);
    var argsArrayOOP = args[0];

    // Extract arguments from array
    var blockArgs = ExtractArrayElements(argsArrayOOP);

    // Invoke block
    return InvokeBlockInternal(methodOOP, blockArgs);
}
```

**Testing**:
- Run BlockValidatorTest new runAllTests
- Run all init expressions
- Verify BlockClosure methods work (#value, #value:, etc.)

### Phase 3: Non-Local Returns (Future)

**Goal**: Support ^ inside blocks returning to defining method

**Changes**:

1. **Add RETURN_NON_LOCAL bytecode detection**
2. **Implement context unwinding logic**
3. **Validate returnContext is on stack**
4. **Signal error for invalid returns**

**Testing**:
- Test non-local return with active context
- Test error on return to dead context
- Test unwinding through multiple contexts

## Critical Implementation Details

### Block Literal Detection

```csharp
private bool IsBlockLiteral(SmalltalkOOP oop)
{
    if (oop.IsSmallInteger)
        return false;

    // Check if this OOP has a localId starting with "block:"
    string? localId = GetLocalId(oop);
    return localId != null && localId.StartsWith("block:");
}

private string? GetLocalId(SmalltalkOOP oop)
{
    // Search GlobalRegistry for this OOP
    foreach (var kvp in GlobalRegistry.GetAllEntries())
    {
        if (kvp.Value.Equals(oop))
        {
            return kvp.Key;
        }
    }
    return null;
}
```

### Block Argument Count Extraction

```csharp
private int GetBlockArgCount(SmalltalkOOP blockMethodOOP)
{
    // Parse method header to get argument count
    var header = ParseMethodHeader(blockMethodOOP);
    return header.NumArgs;
}
```

### BlockClosure Instance Variables

```
method          → CompiledMethod for the block
homeContext     → MethodContext where block was defined
returnContext   → MethodContext to return to (for non-local returns)
numArgs         → SmallInteger, number of arguments
```

## Testing Strategy

### Unit Tests (Immediate)
1. Test block creation captures homeContext
2. Test block invocation uses correct homeContext
3. Test nested blocks have proper ownership chain
4. Test blocks access outer variables correctly

### Integration Tests (Phase 2)
1. Run BlockValidatorTest new runAllTests
2. Run all 280 init expressions
3. Test BlockClosure methods (#value, #fork, #whileTrue:, etc.)

### Regression Tests
1. Ensure init#1, init#2, init#3 still work
2. Verify no GC issues with homeContext retention
3. Check memory usage (contexts kept alive by blocks)

## Risk Mitigation

### Risk: Breaking Existing Init Expressions
**Mitigation**:
- Phase 1 uses fallback to FindMethodContextByLocalId
- Test each init expression after changes
- Keep git checkpoints for rollback

### Risk: Chicken-and-Egg with BlockClosure Compilation
**Mitigation**:
- Phase 1 doesn't require BlockClosure class
- Can test VM changes before image recompilation
- Bootstrap BlockClosure after basic blocks working

### Risk: GC Pressure from Retained Contexts
**Mitigation**:
- Document that homeContext keeps MethodContext alive
- Add GC metrics to track context retention
- Consider hybrid approach (copy only captured vars) later

## Next Actions

### Immediate (Today)
1. ✅ Document current state and plan
2. ⏳ Implement Phase 1: Programmatic BlockClosure
3. ⏳ Test with init#1, init#2, init#3

### Short Term (This Week)
4. Add BlockClosure to Smalltalk-80.st
5. Recompile image
6. Implement Phase 2: Real BlockClosure class

### Medium Term (Next Week)
7. Implement BlockValidatorTest execution
8. Run Phase0X validation
9. Add comprehensive debugging output

### Long Term (Future)
10. Implement Phase 3: Non-local returns
11. Optimize: copy captured vars instead of keeping context
12. JIT compilation for hot blocks

## Success Criteria

### Phase 1 Success
- [ ] All init expressions pass (1-280)
- [ ] Blocks access correct outer variables
- [ ] Blocks access correct receiver (self)
- [ ] No GC errors or crashes

### Phase 2 Success
- [ ] BlockClosure class in image
- [ ] BlockValidatorTest all tests pass
- [ ] Can call #value, #value:, #whileTrue: from Smalltalk
- [ ] Primitive 207 works correctly

### Phase 3 Success
- [ ] Non-local returns work correctly
- [ ] Invalid returns detected and raise errors
- [ ] Context unwinding works across multiple frames

## Conclusion

The implementation is straightforward because:
1. Compiler already emits correct bytecode
2. blockOwnerMap already populated
3. VM infrastructure mostly in place
4. Just need to wrap blocks at runtime

Phase 1 can be implemented immediately and tested without image recompilation. Phase 2 requires adding BlockClosure to the image but provides proper Smalltalk integration. Phase 3 adds advanced features.

Total estimated effort: 2-3 days for Phases 1-2, 1 week for Phase 3.
