# Implementation Plan: Single-Context Architecture

**Date**: 2025-11-06
**Status**: READY TO IMPLEMENT
**Architecture**: [Single-Context-Architecture.md](Single-Context-Architecture.md)

---

## Overview

This plan details the step-by-step implementation of the Single-Context Architecture, which simplifies block execution by using a unified MethodContext for both methods and blocks, with a `lexicalParent` field for scope chain traversal.

**Estimated Time**: 4-6 hours
**Risk Level**: Medium (architectural change, but well-specified)
**Rollback Strategy**: Git revert to pre-implementation commit

---

## Pre-Implementation Checklist

- [x] Architecture document complete ([Single-Context-Architecture.md](Single-Context-Architecture.md))
- [x] Variable indexing spec updated ([Variable-Indexing-Architecture.md](../Variable-Indexing-Architecture.md))
- [ ] Clean working directory (`git status`)
- [ ] All existing tests passing
- [ ] Create implementation branch: `git checkout -b feature/single-context-architecture`

---

## Phase 1: Update Smalltalk Class Definition

**Goal**: Add `lexicalParent` field to MethodContext in Smalltalk-80.st

**Files**: `Smalltalk-80 Source/Smalltalk-80.st`

### Steps

1. **Locate MethodContext definition**:
   ```bash
   grep -n "^Object subclass: #MethodContext" "Smalltalk-80 Source/Smalltalk-80.st"
   ```
   Expected around line 27000-28000

2. **Update instanceVariableNames**:
   ```smalltalk
   " BEFORE:
   Object subclass: #MethodContext
       instanceVariableNames: 'sender pc stackp method receiverMap receiver'
       ...
   "

   " AFTER:
   Object subclass: #MethodContext
       instanceVariableNames: 'sender pc stackp method lexicalParent receiver'
       ...
   "
   ```

   **Note**: Changed `receiverMap` → `lexicalParent`

3. **Remove BlockContext if it exists as separate class**:
   ```bash
   grep -n "^Object subclass: #BlockContext" "Smalltalk-80 Source/Smalltalk-80.st"
   ```
   If found, comment out the entire class definition.

4. **Verify changes**:
   ```bash
   grep -A 3 "^Object subclass: #MethodContext" "Smalltalk-80 Source/Smalltalk-80.st"
   ```

**Deliverable**: Updated Smalltalk-80.st with MethodContext having `lexicalParent` field

---

## Phase 2: Update VMConstants.cs

**Goal**: Update field index constants for new MethodContext layout

**Files**: `Smalltalk80.Interpreter/Core/VM/VMConstants.cs`

### Steps

1. **Update Context field indices**:
   ```csharp
   // BEFORE (lines 24-34):
   public const int CONTEXT_SENDER_INDEX = 0;
   public const int CONTEXT_PC_INDEX = 1;
   public const int CONTEXT_STACKP_INDEX = 2;

   public const int METHODCONTEXT_METHOD_INDEX = 3;
   public const int METHODCONTEXT_RECEIVERMAP_INDEX = 4;
   public const int METHODCONTEXT_RECEIVER_INDEX = 5;
   public const int METHODCONTEXT_TEMP_FRAME_START = 6;

   // AFTER:
   public const int CONTEXT_SENDER_INDEX = 1;           // Updated offset
   public const int CONTEXT_PC_INDEX = 2;               // Updated offset
   public const int CONTEXT_STACKP_INDEX = 3;           // Updated offset
   public const int CONTEXT_METHOD_INDEX = 4;           // Renamed
   public const int CONTEXT_LEXICALPARENT_INDEX = 5;    // NEW!
   public const int CONTEXT_RECEIVER_INDEX = 6;         // Updated offset
   public const int CONTEXT_TEMP_FRAME_START = 7;       // Updated offset
   ```

2. **Remove obsolete constants**:
   - Comment out or delete all `METHODCONTEXT_*` constants (now unified as `CONTEXT_*`)
   - Comment out or delete all `BLOCKCONTEXT_*` constants (no longer needed)
   - Comment out or delete all `BLOCKCONTEXT_*` constants (no longer needed)

3. **Verify no compilation errors**:
   ```bash
   cd Smalltalk80.Interpreter
   dotnet build
   ```

**Deliverable**: Updated VMConstants.cs with unified CONTEXT_* constants

---

## Phase 3: Implement Core Variable Access Functions

**Goal**: Implement the single-context variable access algorithm

**Files**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

### Steps

1. **Add PushTemporary() method** (new implementation):
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

           // Traverse to lexical parent
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

2. **Add StoreTemporary() method**:
   ```csharp
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

3. **Comment out old implementations**:
   - Comment out existing PushTemporary/StoreTemporary implementations
   - Add comment: `// OBSOLETE: Old BlockContext-based implementation`

**Deliverable**: New PushTemporary() and StoreTemporary() methods implemented

---

## Phase 4: Update Bytecode Handlers

**Goal**: Replace bytecode handlers to use new variable access functions

**Files**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

### Steps

1. **Update fast-path PUSH_TEMP (0x10-0x1F)**:
   ```csharp
   if (BytecodeUtil.IsPushTemp(bytecode))
   {
       int index = BytecodeUtil.PushTempIndex(bytecode);
       PushTemporary(index);  // Uses new implementation
       return true;
   }
   ```

2. **Update fast-path STORE_TEMP (0x40-0x47)**:
   ```csharp
   if (BytecodeUtil.IsStoreTemp(bytecode))
   {
       int index = BytecodeUtil.StoreTempIndex(bytecode);
       var value = Peek();
       StoreTemporary(index, value);  // Uses new implementation
       return true;
   }
   ```

3. **Update fast-path POP_STORE_TEMP (0x60-0x67)**:
   ```csharp
   if (BytecodeUtil.IsPopStoreTemp(bytecode))
   {
       int index = BytecodeUtil.PopStoreTempIndex(bytecode);
       var value = Pop();
       StoreTemporary(index, value);  // Uses new implementation
       return true;
   }
   ```

4. **Update extended PUSH_TEMP (0x03)**:
   ```csharp
   case 0x03: // PUSH_TEMP (extended)
       {
           byte index = bytecodes[ip++];
           _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex,
               SmalltalkOOP.FromSmallInteger(ip));
           PushTemporary(index);
           return true;
       }
   ```

5. **Update extended STORE_TEMP (0x04)**:
   ```csharp
   case 0x04: // STORE_TEMP (extended)
       {
           byte index = bytecodes[ip++];
           _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex,
               SmalltalkOOP.FromSmallInteger(ip));
           var value = Peek();
           StoreTemporary(index, value);
           return true;
       }
   ```

6. **Update extended POP_STORE_TEMP (0x05)**:
   ```csharp
   case 0x05: // POP_STORE_TEMP (extended)
       {
           byte index = bytecodes[ip++];
           _heapManager.SetInstanceVariable(_activeContextOOP, ipIndex,
               SmalltalkOOP.FromSmallInteger(ip));
           var value = Pop();
           StoreTemporary(index, value);
           return true;
       }
   ```

7. **Comment out old complex traversal code**:
   - Find old PUSH_TEMP/STORE_TEMP implementations (~100-200 lines each)
   - Comment out with: `// OBSOLETE: Complex BlockContext traversal`

**Deliverable**: All temp bytecode handlers use new simple algorithm

---

## Phase 5: Update Block Invocation

**Goal**: Simplify block invocation to create MethodContext with lexicalParent

**Files**:
- `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`
- `Smalltalk80.Interpreter/Core/VM/Primitives/BlockContextPrimitives.cs`

### Steps

1. **Update CreateMethodContext() helper**:
   ```csharp
   private SmalltalkOOP CreateMethodContext(
       SmalltalkOOP methodOOP,
       SmalltalkOOP receiverOOP,
       SmalltalkOOP senderOOP,
       SmalltalkOOP lexicalParentOOP,  // NEW parameter
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
       _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_PC_INDEX,
           SmalltalkOOP.FromSmallInteger(0));
       _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_STACKP_INDEX,
           SmalltalkOOP.FromSmallInteger(0));
       _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_METHOD_INDEX, methodOOP);
       _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_LEXICALPARENT_INDEX,
           lexicalParentOOP);  // NEW!
       _heapManager.SetInstanceVariable(contextOOP, VMConstants.CONTEXT_RECEIVER_INDEX, receiverOOP);

       return contextOOP;
   }
   ```

2. **Simplify PUSH_LITERAL handler**:
   ```csharp
   case 0x01: // PUSH_LITERAL (extended)
   case byte b when (b >= 0x20 && b <= 0x3F): // Fast-path literals
       {
           // ... get literal ...

           // Just push it directly - no BlockContext wrapping
           Push(literalOOP);
           return true;
       }
   ```

3. **Update Primitive 207** (BlockContextPrimitives.cs):
   ```csharp
   public static SmalltalkOOP Value(
       IPrimitiveContext ctx,
       SmalltalkOOP receiverOOP,  // Block CompiledMethod
       SmalltalkOOP[] args)
   {
       var blockMethodOOP = receiverOOP;
       var header = ctx.ParseMethodHeader(blockMethodOOP);

       // Validate args
       if (args.Length != header.NumArgs)
       {
           ctx.PrimitiveSuccess = false;
           return SpecialObjectsArray.Instance.Nil;
       }

       var currentContext = ctx.GetActiveContext();
       var receiverFromContext = ctx.GetInstanceVariable(
           currentContext,
           VMConstants.CONTEXT_RECEIVER_INDEX
       );

       int stackSize = header.NeedsLargeFrame ? 48 : 16;
       int totalSlots = header.NumArgs + header.NumTemps + stackSize;

       // Create MethodContext with lexicalParent = currentContext
       var blockContextOOP = ctx.CreateMethodContext(
           blockMethodOOP,
           receiverFromContext,
           currentContext,         // sender
           currentContext,         // lexicalParent (THIS IS THE KEY!)
           totalSlots
       );

       // Copy arguments
       for (int i = 0; i < args.Length; i++)
       {
           ctx.SetIndexedOOP(blockContextOOP, i, args[i]);
       }

       // Initialize temps to nil
       var nilOOP = SpecialObjectsArray.Instance.Nil;
       for (int i = args.Length; i < args.Length + header.NumTemps; i++)
       {
           ctx.SetIndexedOOP(blockContextOOP, i, nilOOP);
       }

       ctx.SwitchContext(blockContextOOP);
       return nilOOP;
   }
   ```

4. **Update method invocation** to pass `lexicalParent = nil`:
   ```csharp
   private void InvokeMethod(...)
   {
       var contextOOP = CreateMethodContext(
           methodOOP,
           receiverOOP,
           _activeContextOOP,  // sender
           SpecialObjectsArray.Instance.Nil,  // lexicalParent = nil for methods
           totalSlots
       );
       // ...
   }
   ```

5. **Comment out old block invocation code**:
   - `InvokeBlockInternal()`
   - `CreateBlockExecutionContext()`
   - All BlockContext/BlockContext creation logic

**Deliverable**: Simplified block invocation using unified MethodContext

---

## Phase 6: Update Non-Local Returns

**Goal**: NLR uses lexicalParent chain to find defining method

**Files**: `Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs`

### Steps

1. **Add FindDefiningMethod() helper**:
   ```csharp
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
               // No lexical parent - this is the defining method
               return currentOOP;
           }

           currentOOP = lexicalParentOOP;
       }
   }
   ```

2. **Update RETURN_TOP_POP (0x7E) handler**:
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
               // In block - find defining method
               var targetContextOOP = FindDefiningMethod(_activeContextOOP);

               // Validate target is in sender chain
               if (!IsInSenderChain(targetContextOOP))
               {
                   SignalError("cannotReturn:");
                   return false;
               }

               // Initiate NLR
               _nlrActive = true;
               _nlrTargetContext = targetContextOOP;
               _nlrReturnValue = returnValue;

               return false; // Exit bytecode loop
           }
           else
           {
               // Normal method return
               return HandleReturn(returnValue);
           }
       }
   ```

3. **Keep HandleReturn() unchanged** (already correct):
   - Unwinds sender chain
   - Pools contexts
   - Pushes return value

**Deliverable**: NLR correctly identifies defining method via lexicalParent chain

---

## Phase 7: Remove Obsolete Code

**Goal**: Clean up old BlockContext/BlockContext code

**Files**: Multiple

### Steps

1. **Remove dictionaries** (VirtualMachine.cs):
   ```csharp
   // REMOVE:
   private Dictionary<SmalltalkOOP, SmalltalkOOP> _blockContextMapping;
   private Dictionary<(SmalltalkOOP, int), SmalltalkOOP> _blockContextCache;
   ```

2. **Remove helper methods**:
   - `FindNearestBlockExecutionContext()`
   - `ResolveHomeContext()`
   - `CreateBlockExecutionContext()`
   - `InvokeBlockInternal()`
   - `GetBlockLocalCount()`
   - `CleanupBlockContextMappings()`

3. **Remove constants** (VMConstants.cs):
   - All `METHODCONTEXT_*` constants (replaced by `CONTEXT_*`)
   - All `BLOCKCONTEXT_*` constants
   - All `BLOCKCONTEXT_*` constants

4. **Search for uses**:
   ```bash
   grep -r "BlockContext" Smalltalk80.Interpreter/
   grep -r "BlockContext" Smalltalk80.Interpreter/
   grep -r "_blockContextMapping" Smalltalk80.Interpreter/
   ```

5. **Comment out or delete** all found references

**Deliverable**: Cleaned codebase with only MethodContext

---

## Phase 8: Recompile Bundle

**Goal**: Generate new cold start bundle with updated MethodContext

**Files**: Generated bundle

### Steps

1. **Clean build** interpreter:
   ```bash
   cd Smalltalk80.Interpreter
   dotnet clean
   dotnet build -c Debug
   ```

2. **Build** compiler:
   ```bash
   cd ../Smalltalk80.Compiler
   dotnet clean
   dotnet build -c Debug
   ```

3. **Generate new bundle**:
   ```bash
   dotnet run -- \
     "../Smalltalk-80 Source/Smalltalk-80.st" \
     "../Smalltalk-80 Source/Tests/BlockContextTest.st" \
     "../bin/Debug/net9.0/coldstart_with_tests.json"
   ```

4. **Verify bundle**:
   - Should see 476 classes
   - Should see 10,000+ methods
   - No errors during compilation

**Deliverable**: New coldstart_with_tests.json bundle

---

## Phase 9: Testing

**Goal**: Validate implementation with comprehensive tests

**Files**: Test output

### Steps

1. **Test 1: Simple execution**:
   ```bash
   cd bin/Debug/net9.0
   ./Smalltalk80.Interpreter.exe --coldstart coldstart_with_tests.json --skip-init-expressions
   ```
   Expected: Cold start succeeds, no crashes

2. **Test 2: BlockContextTest**:
   ```bash
   ./Smalltalk80.Interpreter.exe \
     --coldstart coldstart_with_tests.json \
     --skip-init-expressions \
     --execute "BlockContextTest testAllCombinations"
   ```
   Expected: "BlockContextTest>>testAllCombinations result = 1191" (120/120 tests pass)

3. **Test 3: Init expressions** (if time permits):
   ```bash
   ./Smalltalk80.Interpreter.exe --coldstart coldstart_with_tests.json
   ```
   Expected: All init expressions execute successfully

4. **Regression test**: Compare before/after test results

**Success Criteria**:
- ✅ Cold start completes
- ✅ BlockContextTest all 120 tests pass
- ✅ No "generality called on nil" errors
- ✅ No stack overflows or infinite loops

**Deliverable**: Test report showing all tests passing

---

## Phase 10: Documentation and Commit

**Goal**: Document changes and commit to repository

### Steps

1. **Update CLAUDE.md**:
   - Add section: "Single-Context Architecture Implementation (2025-11-06)"
   - Summarize changes
   - Update test status

2. **Create commit**:
   ```bash
   git add .
   git commit -m "Implement Single-Context Architecture for unified block/method execution

   - Add lexicalParent field to MethodContext
   - Simplify variable access to use hierarchical traversal via lexicalParent
   - Remove BlockContext/BlockContext classes and associated complexity
   - Update all bytecode handlers to use unified algorithm
   - Simplify block invocation (no more BlockContext wrapping)
   - Update NLR to use lexicalParent chain
   - Remove ~500 lines of obsolete code

   Benefits:
   - Simpler: One context type, one execution model
   - Faster: No type checking, no cache lookups
   - Correct: Direct mapping to compiler's hierarchical indices

   Testing:
   - BlockContextTest: 120/120 tests passing (result = 1191)
   - No variable access errors
   - All init expressions execute successfully

   Architecture: docs/Single-Context-Architecture.md"
   ```

3. **Merge to main**:
   ```bash
   git checkout main
   git merge feature/single-context-architecture
   git push origin main
   ```

**Deliverable**: Committed and merged implementation

---

## Rollback Plan

If implementation fails:

1. **Revert commit**:
   ```bash
   git revert HEAD
   ```

2. **Or reset to previous state**:
   ```bash
   git reset --hard <commit-before-implementation>
   ```

3. **Analyze failures**:
   - Review test output
   - Add diagnostic logging
   - Check variable access algorithm

4. **Fix and retry**

---

## Post-Implementation Validation

**Checklist**:
- [ ] All compilation warnings resolved
- [ ] No hardcoded indices (all use VMConstants)
- [ ] No null reference exceptions
- [ ] Memory usage reasonable (no leaks)
- [ ] Performance acceptable (no major slowdown)
- [ ] Code coverage: Variable access tested with nested blocks
- [ ] Edge cases: Empty blocks, blocks with no args/temps
- [ ] Error handling: Graceful failure on invalid indices

---

## Estimated Timeline

| Phase | Task | Time | Cumulative |
|-------|------|------|------------|
| 1 | Update Smalltalk-80.st | 15 min | 0:15 |
| 2 | Update VMConstants.cs | 15 min | 0:30 |
| 3 | Implement variable access | 45 min | 1:15 |
| 4 | Update bytecode handlers | 30 min | 1:45 |
| 5 | Update block invocation | 45 min | 2:30 |
| 6 | Update NLR | 30 min | 3:00 |
| 7 | Remove obsolete code | 30 min | 3:30 |
| 8 | Recompile bundle | 15 min | 3:45 |
| 9 | Testing | 1:00 | 4:45 |
| 10 | Documentation/commit | 15 min | 5:00 |

**Total**: ~5 hours (4-6 hour range with debugging)

---

## Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Bundle fails to compile | Low | High | Test compiler separately first |
| Tests fail | Medium | High | Incremental testing, add diagnostics |
| Performance regression | Low | Medium | Benchmark before/after |
| Edge case bugs | Medium | Medium | Comprehensive test coverage |
| Merge conflicts | Low | Low | Clean branch, frequent commits |

---

## Success Metrics

**Implementation Quality**:
- [ ] Code is simpler than before (~50% reduction in block-related code)
- [ ] No special cases or type checking
- [ ] Algorithm matches specification exactly

**Correctness**:
- [ ] All BlockContextTest tests pass (120/120)
- [ ] No variable access errors
- [ ] NLR works correctly

**Performance**:
- [ ] No significant slowdown
- [ ] Memory usage similar or better

**Maintainability**:
- [ ] Clear, well-documented code
- [ ] Easy to understand and modify
- [ ] Architecture documented

---

## Ready to Implement?

**Pre-flight checklist**:
- [x] Architecture document complete
- [x] Variable indexing spec updated
- [x] Implementation plan reviewed
- [ ] Clean working directory
- [ ] Tests passing before changes
- [ ] Implementation branch created

**When ready**: Proceed with Phase 1
