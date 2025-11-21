# HeapManager Separation - Dependency Diagrams

## Current Architecture (Monolithic)

```
┌─────────────────────────────────────────────────────────────────────┐
│                  Smalltalk80.Interpreter.Core.csproj                │
│                                                                       │
│  ┌─────────────────┐   ┌──────────────────┐   ┌──────────────────┐ │
│  │  Memory Module  │   │ HeapManager      │   │    VM Module     │ │
│  │                 │   │ (2,169 lines)    │   │                  │ │
│  │ - ObjectFormat  │◄──│                  │──►│ - Interpreter    │ │
│  │ - SpecialObj... │   │ - SmalltalkOOP   │   │ - Primitives     │ │
│  │                 │   │ - ISmalltalk...  │   │ - StackManager   │ │
│  │   (CIRCULAR!)   │   │ - GarbageCollect.│   │ - ContextMgr.    │ │
│  │                 │   │ - BecomeHandler  │   │ - (88 consumers) │ │
│  └─────────────────┘   │ - ObjectTable*   │   │                  │ │
│                        │                  │   └──────────────────┘ │
│                        └──────────────────┘                         │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────────┐│
│  │  Diagnostics, ObjectGraphAllocator, SnapshotWriter             ││
│  │  (All depend on ISmalltalkHeapManager and SmalltalkOOP)         ││
│  └─────────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────────┘
                          ↓ references
                    ┌──────────────┐
                    │ Shared Core  │
                    │ - FormatWord │
                    └──────────────┘
```

**Problem**: Circular dependency between Memory ↔ HeapManager

---

## Proposed Architecture (Option A - Recommended)

```
┌──────────────────────────────────┐
│  Smalltalk80.Interpreter.Core    │
│  (Modified - without HeapManager)│
│                                   │
│ ┌───────────────────────────────┐│
│ │ Memory Module                 ││
│ │ - ObjectFormat (enum)         ││
│ │ - SpecialObjectsArray (no HM) ││
│ │ References:                   ││
│ │   → Shared.Types              ││
│ └───────────────────────────────┘│
│                                   │
│ ┌───────────────────────────────┐│
│ │ VM Module (88+ files)         ││
│ │ - BytecodeInterpreter         ││
│ │ - All Primitives              ││
│ │ - StackManager, ContextMgr    ││
│ │ References:                   ││
│ │   → HeapManager               ││
│ │   → Shared.Types              ││
│ └───────────────────────────────┘│
│                                   │
│ ┌───────────────────────────────┐│
│ │ Other Modules (Diagnostics,  ││
│ │  ObjectGraphAllocator, etc)   ││
│ │ References:                   ││
│ │   → HeapManager               ││
│ │   → Shared.Types              ││
│ └───────────────────────────────┘│
└──────────────────────────────────┘
  ↓ depends                  ↓ depends
  ▼                          ▼
┌──────────────────┐    ┌──────────────────────┐
│ Shared.Types     │◄───│ HeapManager Assembly │
│                  │    │ (2,169 lines)        │
│ - SmalltalkOOP   │    │                      │
│ - GCStatistics   │    │ - SmalltalkHeapMgr   │
│ - ObjectTableEnt │    │ - GarbageCollector   │
│ - ObjectTableBlk │    │ - BecomeHandler      │
│ - FormatWord*    │    │ - ObjectTableManager │
│   (*in Shared)   │    │ - Interfaces         │
└──────────────────┘    └──────────────────────┘
  ↑                       ↓
  └───────────────────────┘ depends
        (NO CYCLE!)
```

**Key Point**: No circular dependencies. Shared.Types is the lowest layer.

---

## Dependency Flow Analysis

### Current (Monolithic)

```
LAYER 0 (Foundation)
├─ FormatWord (Shared)
└─ ObjectFormat (Memory)

LAYER 1 (Circular - PROBLEMATIC)
├─ Memory Module
│  ├─ SpecialObjectsArray → ISmalltalkHeapManager [UPWARD DEPENDENCY]
│  └─ Uses ObjectFormat
│
└─ HeapManager Module
   ├─ SmalltalkOOP
   ├─ ISmalltalkHeapManager
   ├─ GarbageCollector
   └─ Uses ObjectFormat [UPWARD DEPENDENCY]

LAYER 2 (VM, Consumers)
├─ BytecodeInterpreter
├─ StackManager
├─ All Primitives (50+ files)
├─ Diagnostics
└─ All use ISmalltalkHeapManager + SmalltalkOOP
```

### Proposed (Layered)

```
LAYER 0 (Foundation - Standalone)
└─ Shared.Types Assembly
   ├─ FormatWord
   ├─ SmalltalkOOP
   ├─ GCStatistics
   ├─ ObjectTableEntry
   └─ ObjectTableBlock
   
   ✓ No external dependencies
   ✓ Used by all layers

LAYER 1 (Module - Independent)
├─ Memory Module
│  ├─ ObjectFormat enum
│  ├─ SpecialObjectsArray (stateless)
│  └─ Depends on: Shared.Types
│
└─ HeapManager Assembly
   ├─ SmalltalkHeapManager
   ├─ GarbageCollector
   ├─ BecomeHandler
   ├─ ObjectTableManager
   └─ Depends on: Shared.Types, Memory (for ObjectFormat)
   
   ✓ No circular dependencies
   ✓ Memory only uses types, not heap manager
   ✓ HeapManager can be independently versioned

LAYER 2 (VM & Consumers)
├─ BytecodeInterpreter
├─ StackManager
├─ All Primitives
├─ Diagnostics
├─ ObjectGraphAllocator
└─ SnapshotWriter

   Depends on:
   ├─ Core (VM infrastructure)
   ├─ HeapManager (for allocation/access)
   ├─ Shared.Types (for OOP, statistics)
   └─ Memory (for format info)
```

---

## Critical Circular Dependency (Current Problem)

```
┌─────────────────────────┐
│  Memory Module          │
│  SpecialObjectsArray.cs │
│  Line 104-113:          │
│                         │
│  public SmalltalkOOP    │
│  GetActiveProcess(      │
│    ISmalltalkHeapManager◄───── UPWARD REFERENCE!
│    heapManager)         │      (Problem: Memory depends
│  {                      │       on HeapManager API)
│    return heapManager   │
│      .GetInstanceVar...│
│  }                      │
└─────────────────────────┘
           ↑
           │ Would import this interface
           │
┌─────────────────────────┐
│  HeapManager Module     │
│  ISmalltalkHeapManager  │
│  (Interface definition) │
└─────────────────────────┘
           ↓
           │ Uses this to allocate objects
           │
┌─────────────────────────┐
│  Memory Module          │
│  ObjectFormat enum      │
└─────────────────────────┘

⚠️ CIRCULAR: Memory → HeapManager → Memory
```

### Resolution

```
BEFORE (Circular):
Memory.SpecialObjectsArray → ISmalltalkHeapManager
                    ↑
GarbageCollector → ObjectFormat (from Memory)

AFTER (Linear):
┌─────────────────┐
│  Shared.Types   │  (New - lowest layer)
│  - SmalltalkOOP │
│  - ObjectFormat │  (moved from Memory)
│  - GCStatistics │
└────────┬────────┘
         │
         ├─────────────────────┬─────────────────────┐
         ↓                     ↓                     ↓
    ┌────────────┐      ┌────────────┐      ┌──────────────┐
    │  Memory    │      │HeapManager │      │ VM/Core      │
    │            │      │            │      │              │
    │(uses OOP,  │      │(uses OOP,  │      │(uses both)   │
    │ Format)    │      │ Format)    │      │              │
    └────────────┘      └────────────┘      └──────────────┘

✓ Linear hierarchy
✓ No circular dependencies
✓ Each module can be independently compiled
```

---

## Consumer Dependency Map

### Files Directly Affected by Separation

```
88 files depend on HeapManager types:
│
├─ VM Module (42 files)
│  ├─ BytecodeInterpreter.cs
│  ├─ StackManager.cs
│  ├─ ContextManager.cs
│  ├─ RuntimeHeapAccessor.cs
│  ├─ 30+ Primitive implementations
│  │  ├─ ArithmeticPrimitives.cs
│  │  ├─ ObjectPrimitives.cs
│  │  ├─ ControlPrimitives.cs
│  │  ├─ FloatPrimitives.cs
│  │  ├─ SystemPrimitives.cs
│  │  └─ ...
│  └─ Views (MethodContextView, BlockContextView, etc.)
│
├─ Memory Module (1 file)
│  └─ SpecialObjectsArray.cs (REQUIRES REFACTORING)
│
├─ Core Components (15 files)
│  ├─ ObjectGraphAllocator.cs
│  ├─ SnapshotWriter.cs
│  ├─ ClassHierarchyTraversal.cs
│  └─ Various utilities
│
├─ Diagnostics (8 files)
│  ├─ DiagnosticInspector.cs
│  ├─ DiagnosticEvents.cs
│  └─ ...
│
├─ Tests (1 file, 61KB)
│  └─ SmalltalkHeapManagerTests.cs
│
└─ Application Entry (1 file)
   └─ Program.cs

Impact: Using statements to update ~120+
```

---

## Type Import Changes Required

### Before Separation

```csharp
// 88 files use this pattern:
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

class BytecodeInterpreter
{
    private readonly ISmalltalkHeapManager _heap;
    private readonly SmalltalkOOP _something;
}
```

### After Separation (Option A)

```csharp
// All 88 files need to add:
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

class BytecodeInterpreter
{
    private readonly ISmalltalkHeapManager _heap;  // From HeapManager assembly
    private readonly SmalltalkOOP _something;       // From Shared.Types
}
```

**Change Impact**: Massive, but mechanical (search-replace feasible)

---

## Assembly References After Separation

### Project File Changes

**Smalltalk80.Interpreter.Core.csproj** (modified):
```xml
<ItemGroup>
  <ProjectReference Include="../Shared/Smalltalk80.Interpreter.Shared.Types.csproj" />
  <ProjectReference Include="../HeapManager/Smalltalk80.Interpreter.HeapManager.csproj" />
</ItemGroup>
```

**Smalltalk80.Interpreter.HeapManager.csproj** (new):
```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <AssemblyName>Smalltalk80.Interpreter.HeapManager</AssemblyName>
</PropertyGroup>

<ItemGroup>
  <ProjectReference Include="../Shared/Smalltalk80.Interpreter.Shared.Types.csproj" />
  <ProjectReference Include="../Core/Smalltalk80.Interpreter.Core.csproj" />
  <!-- Only for ObjectFormat enum -->
</ItemGroup>
```

**Smalltalk80.Interpreter.Shared.Types.csproj** (new):
```xml
<PropertyGroup>
  <TargetFramework>net9.0</TargetFramework>
  <AssemblyName>Smalltalk80.Interpreter.Shared.Types</AssemblyName>
</PropertyGroup>

<ItemGroup>
  <!-- No project dependencies! -->
  <PackageReference Include="..." />
</ItemGroup>
```

---

## Test Assembly Integration

### Current Test Structure

```
Smalltalk80.Interpreter.Tests/
├─ HeapManager/
│  └─ SmalltalkHeapManagerTests.cs (61KB)
│     └─ Accesses internal HeapManager types
│
└─ (Other test files)
```

### After Separation

```
Smalltalk80.Interpreter.Tests/
├─ HeapManager/
│  ├─ SmalltalkHeapManagerTests.cs
│  └─ [Requires InternalsVisibleTo]
│
Smalltalk80.Interpreter.HeapManager.Tests/  [NEW - Optional]
├─ HeapManagerTests.cs
├─ GarbageCollectorTests.cs
└─ ObjectTableManagerTests.cs
```

**InternalsVisibleTo Declaration** (in HeapManager assembly):
```csharp
// In Smalltalk80.Interpreter.HeapManager.csproj:
<ItemGroup>
  <InternalsVisibleTo Include="Smalltalk80.Interpreter.Tests" />
  <InternalsVisibleTo Include="Smalltalk80.Interpreter.HeapManager.Tests" />
</ItemGroup>
```

---

## Build Order & Dependencies

### Current Single-Assembly Build
```
1. Core.csproj
   └─ Everything builds together
   └─ Build time: ~5 seconds

2. Engine.csproj → Core.csproj
3. Interpreter.csproj → Engine.csproj
4. Tests.csproj → Interpreter.csproj
```

### After Separation (Recommended)

```
1. Shared.Types.csproj
   └─ Builds independently (~1 second)
   └─ No dependencies
   └─ Output: Smalltalk80.Interpreter.Shared.Types.dll

2. Core.csproj (excluding HeapManager)
   └─ Depends on: Shared.Types
   └─ Build time: ~4 seconds

3. HeapManager.csproj
   └─ Depends on: Shared.Types, Core (for ObjectFormat)
   └─ Build time: ~1 second

4. Engine.csproj
   └─ Depends on: Core, HeapManager, Shared.Types
   └─ Build time: ~2 seconds

5. Interpreter.csproj → Engine.csproj
6. Tests.csproj → All above
7. HeapManager.Tests.csproj (optional) → HeapManager.csproj

Total incremental build: +~2-3 seconds (acceptable)
```

---

## Risk Assessment Matrix

```
Risk Factor          | Current | After Sep. | Mitigation
─────────────────────┼─────────┼────────────┼─────────────────
Circular Deps        | HIGH    | NONE       | Shared.Types
Consumer Impact      | N/A     | HIGH       | Mechanical refactoring
Type Visibility      | LOW     | MEDIUM     | Public APIs clear
Test Coverage        | OK      | OK         | InternalsVisibleTo
Build Complexity     | LOW     | MEDIUM     | CI/CD updates
Performance          | N/A     | NONE       | No new indirection
Documentation        | OK      | NEEDS UPD. | Architecture docs
Deployment Size      | 1 DLL   | 3 DLLs     | Minimal impact
```

---

## Migration Timeline Estimate

```
Week 1: Preparation Phase
├─ Days 1-2: Analyze and plan
├─ Day 3: Create Shared.Types project skeleton
├─ Days 4-5: Move types, update imports
└─ Validation: Verify no breakage

Week 2: Extract Phase
├─ Days 1-2: Create HeapManager.csproj
├─ Day 3: Move all HeapManager files
├─ Days 4-5: Update project references
└─ Validation: Test compilation

Week 3: Refactor Phase
├─ Days 1-2: Fix circular dependencies
├─ Days 3-4: Update SpecialObjectsArray
├─ Day 5: Resolve import statements
└─ Validation: All files compile

Week 4: Testing Phase
├─ Days 1-2: Unit test verification
├─ Days 3-4: Integration testing
├─ Day 5: Regression testing (BlockContext)
└─ Validation: All 120 tests pass

Week 5: Polish & Docs
├─ Days 1-2: Performance verification
├─ Day 3: Update architecture documentation
├─ Day 4: Code review and polish
├─ Day 5: Buffer/final adjustments
└─ Release ready
```

