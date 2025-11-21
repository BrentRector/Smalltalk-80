# HeapManager Separation - Quick Reference

## TL;DR

**Can HeapManager be separated?** ✅ Yes, but challenging

**Effort Required:** 3-5 weeks

**Main Blocker:** Circular dependency between Memory and HeapManager modules

**Recommendation:** Pursue only if you need:
- Independent module reuse
- Architectural boundaries enforcement
- Clear separation of concerns

---

## Key Findings

### Current State
- **Module Size**: 12 files, 2,169 lines of code
- **Consumer Base**: 88 files across VM, Primitives, Diagnostics
- **Dependencies**: Only 2 external modules (Memory, Shared)
- **Tests**: 1 comprehensive test file (61KB)

### Critical Issue: Circular Dependency

```
Memory.SpecialObjectsArray → needs → ISmalltalkHeapManager
                                              ↓
GarbageCollector → needs → ObjectFormat enum (from Memory)
```

**Cannot separate without resolving this first.**

### Shared Types (Must Move)
```
SmalltalkOOP           - Used by 87 files
ObjectTableEntry       - Used internally + by GC
ObjectTableBlock       - Used by GC
GCStatistics          - Used by diagnostics
```

---

## Three Options

### Option A: Full Separation (RECOMMENDED)
```
Create 3 assemblies:
1. Smalltalk80.Interpreter.Shared.Types     (NEW - ~350 lines)
   ├─ SmalltalkOOP
   ├─ GCStatistics
   ├─ ObjectTableEntry
   └─ ObjectTableBlock

2. Smalltalk80.Interpreter.Core             (MODIFIED - removes HeapManager)
   ├─ Memory, VM, Diagnostics, etc
   └─ References Shared.Types + HeapManager

3. Smalltalk80.Interpreter.HeapManager      (NEW - ~2,170 lines)
   ├─ All HeapManager files
   └─ References Shared.Types + Core
```

**Pros**: Cleanest, no circular deps, independently testable
**Cons**: 3 weeks effort, 88 files need import updates

### Option B: Facade Assembly (LIGHTWEIGHT)
```
Create 1 thin assembly:
Smalltalk80.Interpreter.HeapManager.dll
├─ Re-exports ISmalltalkHeapManager from Core
├─ Re-exports SmalltalkOOP from Core
└─ Actual code stays in Core

(Just an API boundary, not actual separation)
```

**Pros**: 3-4 days effort, minimal disruption
**Cons**: Doesn't actually reduce monolithic size

### Option C: Keep as-is (SAFEST)
```
Leave everything in Core.csproj
└─ Current stable configuration
```

**Pros**: Zero work, proven stability
**Cons**: Tight coupling remains

---

## Files Affected by Separation

### Must Change (88 files)
```
VM Module (42 files)
├─ BytecodeInterpreter.cs
├─ All 30+ primitive implementations
├─ StackManager.cs
├─ ContextManager.cs
└─ Various utilities

Memory Module (1 file - REQUIRES REFACTORING)
└─ SpecialObjectsArray.cs

Core Components (15 files)
├─ ObjectGraphAllocator.cs
├─ SnapshotWriter.cs
└─ Diagnostics utilities

Tests & App (2 files)
├─ SmalltalkHeapManagerTests.cs
└─ Program.cs
```

### New Files to Create (Option A)
```
Smalltalk80.Interpreter/
├─ Shared.Types/
│  ├─ Smalltalk80.Interpreter.Shared.Types.csproj
│  ├─ SmalltalkOOP.cs
│  ├─ ObjectTableEntry.cs
│  ├─ ObjectTableBlock.cs
│  └─ GCStatistics.cs
│
└─ HeapManager/
   ├─ Smalltalk80.Interpreter.HeapManager.csproj
   ├─ SmalltalkHeapManager.cs
   ├─ GarbageCollector.cs
   ├─ BecomeHandler.cs
   ├─ ObjectTableManager.cs
   ├─ ISmalltalkHeapManager.cs
   └─ [all other files]
```

---

## Import Statement Changes

### Before
```csharp
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

// Use ISmalltalkHeapManager, SmalltalkOOP
```

### After (Option A)
```csharp
using Smalltalk80.Interpreter.Shared.Types;      // NEW
using Smalltalk80.Interpreter.HeapManager;        // NEW (was Core.HeapManager)
using Smalltalk80.Interpreter.Core.Memory;

// Use ISmalltalkHeapManager from HeapManager assembly
// Use SmalltalkOOP from Shared.Types assembly
```

---

## Critical Circular Dependency Fix

**Problem Code** (Memory/SpecialObjectsArray.cs):
```csharp
public SmalltalkOOP GetActiveProcess(ISmalltalkHeapManager heapManager)
{
    // Memory module depends on HeapManager interface!
    return heapManager.GetInstanceVariable(ProcessorScheduler, ...);
}
```

**Solution Options**:
1. Move ISmalltalkHeapManager to Shared.Types (recommended with Option A)
2. Inject heap manager as constructor parameter instead
3. Create utility function outside SpecialObjectsArray
4. Accept stateless SpecialObjectsArray that doesn't access heap

---

## Impact Assessment

### Build Time
- Current: ~5 seconds (monolithic)
- After: ~7-8 seconds (3 assemblies)
- Impact: +2-3 seconds (+40%) - acceptable

### Runtime Performance
- **No change** - same code, just split across assemblies
- No new indirection layers
- GC behavior identical

### Deployment Size
- Current: 1 DLL (Core assembly)
- After: 3 DLLs (Core, HeapManager, Shared.Types)
- Total size: Identical
- Impact: Negligible

### Testing
- All 120 BlockContext tests still pass
- 61KB HeapManager test file still works
- New structure enables independent HeapManager testing

---

## Decision Flowchart

```
START
  │
  ├─ Do you need to reuse HeapManager elsewhere?
  │  NO  ─→ Keep as-is (Option C) ✓
  │  YES ─→ Continue...
  │
  ├─ Do you want true architectural boundaries?
  │  NO  ─→ Use Facade (Option B)
  │  YES ─→ Continue...
  │
  ├─ Do you have 3-5 weeks available?
  │  NO  ─→ Use Facade (Option B)
  │  YES ─→ Proceed with Option A
  │
  ├─ Can you handle 88 files needing import updates?
  │  NO  ─→ Use Facade (Option B)
  │  YES ─→ Proceed with Option A ✓
  │
  └─ Ready to implement Option A
     ✓ Solves circular dependencies
     ✓ Enables independent testing
     ✓ Clear module boundaries
     ✓ Future-proof architecture
```

---

## Risk Summary

| Risk | Severity | Mitigation |
|------|----------|-----------|
| Circular dependency | HIGH | Shared.Types assembly |
| 88 files to update | HIGH | Search-replace automation |
| Test breakage | MEDIUM | InternalsVisibleTo + regression test |
| Build complexity | MEDIUM | CI/CD template updates |
| Performance regression | LOW | No new indirection |
| Documentation rot | MEDIUM | Update architecture docs |

---

## What to Do Next

### If choosing Option A (Full Separation):
1. [ ] Review both detailed reports
2. [ ] Run feasibility spike (1-2 days)
3. [ ] Get team alignment on circular dependency solution
4. [ ] Plan 5-week timeline
5. [ ] Set up parallel branch for refactoring

### If choosing Option B (Facade):
1. [ ] Create thin wrapper assembly
2. [ ] Update project references
3. [ ] Re-export public types
4. [ ] Test with existing code

### If choosing Option C (As-is):
1. [ ] Document decision
2. [ ] Add architectural boundary comments
3. [ ] Consider interface-based access points
4. [ ] Plan for future if needs change

---

## Key Metrics

- **Coupling**: 88 files depend on HeapManager
- **Cohesion**: HeapManager module has 12 internal files (good)
- **Lines of Code**: 2,169 (manageable size)
- **Test Coverage**: Comprehensive (61KB tests)
- **External Dependencies**: Only 2 modules (Memory, Shared)
- **Circular Deps**: 1 critical (blocker for separation)

---

## Architecture Layers (After Separation - Option A)

```
LAYER 0: Shared Foundation (No deps)
└─ Smalltalk80.Interpreter.Shared.Types.dll
   ├─ SmalltalkOOP (core value type)
   ├─ ObjectTableEntry (metadata)
   └─ GCStatistics (results)

LAYER 1: Modules (Depend on Layer 0)
├─ Memory module
├─ HeapManager module
└─ Other core components

LAYER 2: VM & Consumers (Depend on Layers 0+1)
├─ BytecodeInterpreter
├─ All Primitives
├─ StackManager
└─ Diagnostics
```

---

## Recommended Approach Summary

**Choose Option A if:**
- Long-term project (>6 months)
- Want clean architectural boundaries
- Plan to maintain separate versions
- Have capacity for careful refactoring

**Choose Option B if:**
- Short-term solution needed
- Want to preserve current code
- Just need API clarity
- Limited time/resources

**Choose Option C if:**
- Current architecture working well
- Stability is highest priority
- Plans may change
- Team capacity limited

---

## Resources

See full analysis in:
- **HeapManager_Separation_Analysis.md** - 16KB detailed report
- **HeapManager_Dependency_Diagrams.md** - 17KB visual architecture

For questions or implementation guidance, reference:
- Circular dependency discussion (Section 2.2 of analysis)
- Implementation roadmap (Section 7 of analysis)
- Migration timeline (Section 11 of diagrams)

