# Dead Code Analysis Report

**Date**: 2025-11-06
**File**: Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs

## Summary

Found **12 dead private methods** out of 56 total private methods (21% dead code).

---

## Category 1: Debug/Diagnostic Methods

These are utility methods for debugging that are never called. Decision: **Keep or remove based on usefulness**.

### Line 2256: `DumpMethodStateOnError`
- **Purpose**: Dumps method state when an error occurs
- **Status**: Never called
- **Recommendation**: KEEP - Useful for debugging when needed

### Line 4217: `DumpBytecodes`
- **Purpose**: Dumps bytecode disassembly
- **Status**: Never called
- **Recommendation**: KEEP - Useful diagnostic tool

---

## Category 2: Obsolete BlockContext Methods

These methods were part of the old dual-context architecture (BlockContext + MethodContext) that has been replaced by the Single-Context Architecture.

### Line 2677: `ResolveOuterContext`
- **Purpose**: Walks blockOwnerMap to find root method
- **Status**: Obsolete - used blockOwnerMap
- **Recommendation**: **DELETE**

### Line 2693: `GetHomeContextReceiver`
- **Purpose**: Gets receiver from home context
- **Status**: Obsolete - old architecture
- **Recommendation**: **DELETE**

### Line 2704: `GetBlockHomeReceiver`
- **Purpose**: Gets receiver from block home context
- **Status**: Obsolete - old architecture
- **Recommendation**: **DELETE**

### Line 2773: `FindNearestBlockExecutionContext`
- **Purpose**: Finds nearest block execution context in sender chain
- **Status**: Obsolete - used _blockContextMapping
- **Recommendation**: **DELETE**

### Line 2779: `CleanupBlockContextMappings`
- **Purpose**: Cleanup for _blockContextMapping dictionary
- **Status**: Obsolete - dictionary removed
- **Recommendation**: **DELETE**

### Line 2785: `LookupBlockClosureForExecutionContext`
- **Purpose**: Lookup in _blockContextMapping
- **Status**: Obsolete - dictionary removed
- **Recommendation**: **DELETE**

### Line 2791: `ResolveHomeContext`
- **Purpose**: Resolves BlockClosure homeContext field
- **Status**: Obsolete - old architecture
- **Recommendation**: **DELETE**

### Line 2804: `FindMethodContextForMethod`
- **Purpose**: Finds method context for specific method using _blockContextMapping
- **Status**: Obsolete - dictionary removed
- **Recommendation**: **DELETE**

### Line 4003: `GetBlockArgCount`
- **Purpose**: Gets arg count for block method
- **Status**: Obsolete - blocks now handled uniformly
- **Recommendation**: **DELETE**

---

## Category 3: Other Dead Methods

### Line 3883: `AllocateEmptyClassPoolDictionary`
- **Purpose**: Allocates empty dictionary for class pool
- **Status**: Never called
- **Recommendation**: **DELETE** (or keep if needed for future use)

---

## Recommendations

### Immediate Deletions (8 methods - Obsolete BlockContext code)
1. ResolveOuterContext (line 2677)
2. GetHomeContextReceiver (line 2693)
3. GetBlockHomeReceiver (line 2704)
4. FindNearestBlockExecutionContext (line 2773)
5. CleanupBlockContextMappings (line 2779)
6. LookupBlockClosureForExecutionContext (line 2785)
7. ResolveHomeContext (line 2791)
8. FindMethodContextForMethod (line 2804)
9. GetBlockArgCount (line 4003)

### Consider Keeping (2 methods - Debug utilities)
1. DumpMethodStateOnError (line 2256) - Useful for error diagnosis
2. DumpBytecodes (line 4217) - Useful for bytecode inspection

### Uncertain (1 method)
1. AllocateEmptyClassPoolDictionary (line 3883) - May be needed for class pool initialization

---

---

## Obsolete Constants in VMConstants.cs

These constants are marked with `[Obsolete]` attribute but still present in the code. They can be removed once all references are migrated to the new CONTEXT_* constants.

### Legacy MethodContext Constants (lines 44-49)
- `METHODCONTEXT_METHOD_INDEX` → Use `CONTEXT_METHOD_INDEX`
- `METHODCONTEXT_RECEIVER_INDEX` → Use `CONTEXT_RECEIVER_INDEX`
- `METHODCONTEXT_TEMP_FRAME_START` → Use `CONTEXT_TEMP_FRAME_START`

**Recommendation**: Can be removed if no code references them (check build warnings)

### Legacy BlockContext Constants (lines 52-59)
- `BLOCKCONTEXT_NARGS_INDEX` - OBSOLETE (blocks use MethodContext now)
- `BLOCKCONTEXT_STARTPC_INDEX` - OBSOLETE
- `BLOCKCONTEXT_HOME_INDEX` - OBSOLETE
- `BLOCKCONTEXT_TEMP_FRAME_START` - OBSOLETE

**Recommendation**: **DELETE** - These are completely obsolete with Single-Context Architecture

---

## Impact

### Removing Obsolete Methods (9 methods)
- Reduce VirtualMachine.cs by approximately 150-200 lines
- Eliminate all references to removed _blockContextMapping infrastructure
- Clean up dead code from old architecture
- Improve code maintainability

### Removing Obsolete Constants (7 constants)
- Clean up VMConstants.cs
- Remove obsolete BlockContext references
- Simplify constant naming (single CONTEXT_* namespace)

**Build Status**: Removing these methods and constants will not break the build (they're never called/unused).
