# Bug Fix: Super Send from Within BlockClosure

**Date**: 2025-01-27
**Status**: FIXED
**Component**: VirtualMachine.cs - SendSuperMessage method

## Executive Summary

Fixed critical bug where super sends executed from within BlockClosure contexts would find the wrong method, causing incorrect method lookup and argument count mismatches.

## The Bug

### Symptoms
- Super sends from within blocks failed with "wrong method found" errors
- Example: `replaceFrom:to:with:startingAt:` super send would incorrectly find `False>>ifTrue:ifFalse:`
- Argument count mismatches (4 args sent, 2 expected)
- "Instance variable index out of range" errors

### Root Cause

When executing a super send, the VM needs to find the home class of the currently executing method to start superclass lookup. The original code did:

```csharp
// BUG: Assumes _activeContextOOP is always a MethodContext
var currentMethodOOP = _heapManager.GetInstanceVariable(_activeContextOOP, methodIndex);
```

However, when executing inside a BlockClosure:
- `_activeContextOOP` points to a **BlockContext**, not a MethodContext
- BlockContext has a "method" field that points to the **block's compiled method**
- The block's method has the wrong homeClass (usually CompiledMethod or similar)
- This causes super lookup to start from the wrong class hierarchy

### Example Failure Scenario

```
Executing: collection replaceFrom:to:with:startingAt: (inside a block)
  ↓
Super send tries to find method starting from superclass
  ↓
Gets currentMethodOOP from _activeContextOOP (BlockContext)
  ↓
Gets wrong method (block's method, not enclosing method)
  ↓
Gets wrong homeClass → wrong superclass → wrong method found
  ↓
FAILURE: False>>ifTrue:ifFalse: found instead of replaceFrom:to:with:startingAt:
```

## The Fix

Use the existing `FindMethodContext(_activeContextOOP)` method to skip BlockContext and find the actual enclosing MethodContext:

```csharp
// FIXED: Find the actual MethodContext, skipping any BlockContext
var methodContextOOP = FindMethodContext(_activeContextOOP);
if (methodContextOOP == default)
{
    throw new InvalidOperationException($"Cannot find MethodContext for super send - active context may be invalid");
}

// Get the currently executing method from the method context
int methodIndex = _layoutResolver.GetFieldIndex("class:MethodContext", "method");
var currentMethodOOP = _heapManager.GetInstanceVariable(methodContextOOP, methodIndex);
```

### How FindMethodContext Works

The `FindMethodContext` method (VirtualMachine.cs:2482):
1. Starts from the current context (may be BlockContext or MethodContext)
2. Checks if context is in `_blockContextMapping` (tracks all BlockContexts)
3. If it's a BlockContext, follows the sender chain to the next context
4. Repeats until it finds a MethodContext (not in _blockContextMapping)
5. Returns the MethodContext

This ensures super sends always start lookup from the correct enclosing method's homeClass, regardless of whether the super send is executed directly in a method or inside a nested block.

## Files Modified

- **Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs**
  - `SendSuperMessage` method (lines 1647-1663)
  - Added call to `FindMethodContext(_activeContextOOP)` before getting method
  - Added null check with descriptive error message
  - Added explanatory comment about BlockContext issue

## Testing

This fix enables:
- ✅ Super sends from within blocks execute correctly
- ✅ Correct method lookup using proper homeClass/superclass chain
- ✅ Argument counts match expected method signatures
- ✅ Init expressions that use blocks with super sends complete successfully

## Impact

This fix is critical for:
- Collection iteration methods that use blocks with super sends
- Any Smalltalk code that performs super sends inside block closures
- Init expressions during cold start that rely on this pattern
- Future Smalltalk code execution that uses this common idiom

## Related Code

The `FindMethodContext` method is already used correctly in several other places:
- GetReceiverForCurrentContext (line 2419)
- CreateBlockClosure for returnContext (line 3317)

This fix brings `SendSuperMessage` into alignment with these other methods that correctly handle BlockContext vs MethodContext distinction.

## Key Lessons

1. **Context Type Matters**: Always check whether _activeContextOOP is a BlockContext or MethodContext
2. **Use Existing Helpers**: FindMethodContext exists specifically for this purpose
3. **Super Sends Need Enclosing Method**: Super lookup requires the actual method that contains the super send source code, not the block's method
4. **Block Method ≠ Enclosing Method**: Blocks have their own compiled methods, distinct from the method they're defined in
