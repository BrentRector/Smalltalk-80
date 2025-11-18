# File Naming Strategy and Refactoring Plan

**Created**: 2025-11-17
**Status**: PLANNING PHASE - Research Complete

---

## Executive Summary

This document provides a comprehensive analysis of current file naming in the Smalltalk-80 interpreter and proposes a standardized naming strategy for the upcoming refactoring into formal subsystems with well-defined APIs.

**Current State**: 190 C# files with mixed naming patterns, duplicates, and legacy code
**Goal**: Clear, consistent naming that communicates purpose, subsystem, and responsibility

---

## Table of Contents

1. [Current State Analysis](#current-state-analysis)
2. [Critical Issues Found](#critical-issues-found)
3. [Naming Convention Rules](#naming-convention-rules)
4. [Subsystem Organization](#subsystem-organization)
5. [Renaming Plan](#renaming-plan)
6. [Integration with Refactoring Phases](#integration-with-refactoring-phases)
7. [Quick Reference](#quick-reference)

---

## Current State Analysis

### File Count by Category

| Category | Count | Examples |
|----------|-------|----------|
| **Core Components** | 25 | VirtualMachine.cs, SmalltalkHeapManager.cs, LayoutResolver.cs |
| **Schema/DTOs** | 19 | ClassDescriptor.cs, MethodDescriptor.cs, ColdStartBundle.cs |
| **Primitives** | 11 | ArithmeticPrimitives.cs, ControlPrimitives.cs, ObjectPrimitives.cs |
| **Phase Loaders** | 6 | Phase1_PreflightAndSymbols.cs → Phase6_ValidationAndGC.cs |
| **Interfaces** | 7 | ISmalltalkHeapManager.cs, IDisplayProvider.cs, IPrimitiveContext.cs |
| **Bytecode Tools** | 5 | BytecodeDecompiler.cs, BytecodeStackAnalyzer.cs |
| **Utilities** | 8 | VMUtilities.cs, ClassHierarchyUtils.cs, SymbolHasher.cs |
| **Tests** | 18 | HeapManagerTests.cs, ArithmeticPrimitivesTests.cs |
| **Compiler** | 13 | BytecodeCompiler.cs, VariableResolver.cs, TempFrameAllocator.cs |
| **Validator** | 20+ | BytecodeValidator.cs, MetadataCountsValidator.cs |
| **Legacy/Stubs** | 4 | SObject.cs, ObjectMemory.cs (NotImplementedException stubs) |
| **Other** | 50+ | Program.cs files, display providers, test harnesses |

### Current Naming Patterns

#### ✅ Good Patterns (Keep)

1. **Phase Files**: `Phase[N]_DescriptiveName.cs`
   - Phase1_PreflightAndSymbols.cs
   - Phase2_ClassInfrastructure.cs
   - Clear ordering and purpose

2. **Interfaces**: `I[VerbNoun].cs` (Standard C# convention)
   - ISmalltalkHeapManager.cs
   - IDisplayProvider.cs
   - IColdStartPrimitives.cs

3. **Primitives**: `[Category]Primitives.cs`
   - ArithmeticPrimitives.cs
   - ControlPrimitives.cs
   - ProcessPrimitives.cs

4. **Schema/DTOs**: `[Entity]Descriptor.cs` or `[Entity].cs`
   - ClassDescriptor.cs
   - MethodDescriptor.cs
   - ColdStartBundle.cs

5. **Test Files**: `[TypeUnderTest]Tests.cs`
   - HeapManagerTests.cs
   - ArithmeticPrimitivesTests.cs

#### ❌ Problem Patterns (Fix)

1. **Generic/Vague Names**
   - Loader.cs → ColdStartOrchestrator.cs
   - SemanticAllocator.cs → ObjectGraphAllocator.cs
   - Consumer.cs, Producer.cs → GraphNode.cs types

2. **Inconsistent Suffixes**
   - VMUtilities.cs vs ClassHierarchyUtils.cs
   - Should standardize on "Utilities" or "Utils" (prefer "Utilities")

3. **Ambiguous Purpose**
   - RuntimeHeapAccess.cs → Could be RuntimeHeapAccessor or RuntimeHeapFacade
   - SymbolHasher.cs vs SymbolRegistry.cs → Both deal with symbols, unclear distinction

4. **Location Issues**
   - WinFormsDisplayProvider.cs at root → should be in Platform/ or UI/
   - TestColdStart.cs at root → should be in Tests/

---

## Critical Issues Found

### 🔴 Issue #1: Duplicate DebugOptions Classes

**Location**:
- `/Engine/DebugOptions.cs` - Cold start debugging (9 flags)
- `/Core/VM/DebugOptions.cs` - Runtime VM debugging (3 flags)

**Problem**: Two completely different classes with same name in different namespaces

**Solution**: Rename to reflect purpose
- Engine/DebugOptions.cs → **Engine/ColdStartDebugOptions.cs**
- Core/VM/DebugOptions.cs → **Core/VM/RuntimeDebugOptions.cs**

### 🔴 Issue #2: Duplicate ObjectTableEntry Classes

**Location**:
- `/Core/Objects/ObjectTableEntry.cs` - Legacy stub (old format)
- `/Core/HeapManager/ObjectTableEntry.cs` - Active implementation (new format)

**Problem**: Duplication with different structure, confusing which to use

**Solution**:
- Remove Core/Objects/ObjectTableEntry.cs (legacy stub)
- Keep Core/HeapManager/ObjectTableEntry.cs (active)
- Entire Core/Objects/ namespace appears to be legacy stubs (ObjectMemory.cs, SObject.cs throw NotImplementedException)

**Action**: Audit Core/Objects/ namespace - likely entire directory can be deleted

### 🔴 Issue #3: Legacy Stub Code

**Location**: `/Core/Objects/` directory
- ObjectMemory.cs - All methods throw NotImplementedException
- SObject.cs - Empty base class stub
- ObjectTableEntry.cs - Old format

**Problem**: Dead code confusing architecture

**Solution**: DELETE entire `/Core/Objects/` directory after confirming no runtime dependencies

### 🔴 Issue #4: Schema Naming Ambiguity

**Location**: `/Core/Schema/` directory (19 files)

**Problem**: Files look like domain models but are actually JSON serialization DTOs

**Consideration**: Should these be named with "Dto" suffix?
- ClassDescriptor.cs → ClassDescriptorDto.cs?
- MethodDescriptor.cs → MethodDescriptorDto.cs?

**Decision Needed**:
- **Option A**: Keep current names (they're already in Schema namespace which implies serialization)
- **Option B**: Add "Dto" suffix for explicitness
- **Recommendation**: **Option A** - namespace already provides context, "Descriptor" suffix is clear

---

## Naming Convention Rules

### General Principles

1. **File name MUST match primary type name** (Standard C# rule)
2. **One primary public type per file** (except nested helper classes)
3. **Name must communicate purpose without reading code**
4. **Consistent patterns within each category**
5. **Namespace structure mirrors directory structure**

### Category-Specific Rules

#### 1. Interfaces

**Pattern**: `I[VerbNoun].cs`

**Examples**:
- `ISmalltalkHeapManager.cs` - Manages the Smalltalk heap
- `IDisplayProvider.cs` - Provides display output
- `IPrimitiveContext.cs` - Provides context for primitive execution

**Rules**:
- Always start with 'I' prefix (C# standard)
- Use verb + noun for capability interfaces (IDisposable, IComparable)
- Use adjective + noun for trait interfaces (IReadOnlyList)

#### 2. Abstract Classes

**Pattern**: `[AbstractConcept].cs` or `Abstract[Concept].cs`

**Examples**:
- `BytecodeValidator.cs` (if abstract base)
- `AbstractValidator.cs` (if generic base)

**Rules**:
- No special prefix unless "Abstract" adds clarity
- Use when interface is insufficient (protected members, partial implementation)

#### 3. Concrete Implementations

**Pattern**: `[Implementation][Interface].cs` where Interface drops 'I' prefix

**Examples**:
- Interface: `ISmalltalkHeapManager.cs`
- Implementation: `SmalltalkHeapManager.cs`
- Alternative implementation: `TestHeapManager.cs`

**Rules**:
- Default implementation drops any qualifier
- Alternative implementations use descriptive prefix

#### 4. Services and Managers

**Pattern**: `[Domain][Service/Manager/Orchestrator].cs`

**Examples**:
- `ColdStartOrchestrator.cs` - Orchestrates cold start phases (current: Loader.cs)
- `ObjectGraphAllocator.cs` - Allocates object graph (current: SemanticAllocator.cs)
- `InitExpressionExecutor.cs` ✅ (already good)

**Suffixes**:
- **Service**: Stateless operations (e.g., ValidationService)
- **Manager**: Stateful coordination (e.g., HeapManager, SymbolManager)
- **Orchestrator**: Complex multi-step workflows (e.g., ColdStartOrchestrator)
- **Executor**: Executes specific operations (e.g., InitExpressionExecutor)
- **Resolver**: Resolves/looks up items (e.g., LayoutResolver, VariableResolver)
- **Registry**: Maintains registration/lookup (e.g., SymbolRegistry, GlobalRegistry)

#### 5. Data Models and Records

**Pattern**: `[Entity].cs` or `[Entity][Qualifier].cs`

**Examples**:
- `ClassDescriptor.cs` - Describes a class (DTO)
- `ClassInfo.cs` - Runtime class information (domain model)
- `MethodHeader.cs` - Method header structure
- `InitResult.cs` - Result record

**Rules**:
- Use descriptive suffixes when needed: Descriptor, Info, Header, Result, Statistics
- Records should use record types (C# 13)
- DTOs in Schema namespace don't need "Dto" suffix (namespace provides context)

#### 6. Primitives

**Pattern**: `[Category]Primitives.cs`

**Examples**:
- `ArithmeticPrimitives.cs`
- `ControlPrimitives.cs`
- `ObjectPrimitives.cs`

**Rules**:
- Always use "Primitives" suffix
- Category should be noun describing primitive group
- Partial classes allowed for large primitive sets

#### 7. Utilities and Helpers

**Pattern**: `[Domain]Utilities.cs` or `[Domain]Helper.cs`

**Examples**:
- `VMUtilities.cs` ✅
- `ClassHierarchyUtilities.cs` (current: ClassHierarchyUtils.cs)
- `SmalltalkMath.cs` ✅ (static math operations)

**Rules**:
- Prefer "Utilities" over "Utils" (more professional, consistent)
- Use "Helper" for specific assistance classes
- Static utility classes should be clearly named

#### 8. Validators

**Pattern**: `[WhatIsValidated]Validator.cs`

**Examples**:
- `BytecodeValidator.cs` ✅
- `MetadataCountsValidator.cs` ✅
- `LocalIdUniquenessValidator.cs` ✅

**Rules**:
- Always end with "Validator"
- Name describes what is validated, not how
- Should implement common validator interface

#### 9. Phase Loaders

**Pattern**: `Phase[N]_[PurposeDescription].cs`

**Examples**:
- `Phase1_PreflightAndSymbols.cs` ✅
- `Phase5_GlobalsAndPatches.cs` ✅

**Rules**:
- Numeric prefix for ordering
- Underscore separator
- Brief descriptive name of phase purpose
- These may be refactored into ColdStart subsystem

#### 10. Test Files

**Pattern**: `[TypeUnderTest]Tests.cs` or `[Feature]Tests.cs`

**Examples**:
- `HeapManagerTests.cs` ✅
- `ArithmeticPrimitivesTests.cs` ✅
- `ComprehensiveArithmeticTests.cs` ✅

**Rules**:
- Always end with "Tests"
- Match the class being tested when testing single class
- Use feature name for integration/comprehensive tests

#### 11. Exceptions

**Pattern**: `[Condition]Exception.cs`

**Examples**:
- `NonLocalReturnException.cs` ✅
- `PrimitiveFailureException.cs` ✅

**Rules**:
- Always end with "Exception"
- Describe the exceptional condition
- Should inherit from Exception or specific exception base

#### 12. Attributes

**Pattern**: `[Purpose]Attribute.cs`

**Examples**:
- `ColdStartOnlyAttribute.cs` ✅
- `RuntimeOnlyAttribute.cs` ✅
- `IHeavyCheckMarker.cs` → `HeavyCheckAttribute.cs`?

**Rules**:
- Always end with "Attribute" (can be omitted in usage)
- Describe the purpose/constraint being marked
- Marker interfaces should be attributes instead (C# best practice)

#### 13. Enums

**Pattern**: `[ConceptName].cs` (singular)

**Examples**:
- `ObjectFormat.cs` ✅
- `CombinationRule.cs` ✅

**Rules**:
- Use singular form for enum name
- Enum members use PascalCase
- Avoid "Enum" suffix

#### 14. Constants

**Pattern**: `[Domain]Constants.cs`

**Examples**:
- `VMConstants.cs` ✅

**Rules**:
- Use "Constants" suffix
- Should be static class with const or readonly fields

---

## Subsystem Organization

Based on the upcoming refactoring, files will be organized into these subsystems:

### Subsystem Directory Structure

```
Smalltalk80.Interpreter/
├── Core/
│   ├── HeapManager/           # Memory management subsystem
│   ├── Bytecodes/             # Bytecode analysis tools
│   ├── Schema/                # JSON serialization DTOs
│   └── [to be deleted] Objects/  # Legacy stubs - DELETE
│
├── ColdStart/                 # NEW - Cold start subsystem
│   ├── Orchestration/
│   │   └── ColdStartOrchestrator.cs (was Loader.cs)
│   ├── Phases/
│   │   ├── Phase1_PreflightAndSymbols.cs
│   │   ├── Phase2_ClassInfrastructure.cs
│   │   └── ...
│   ├── Allocation/
│   │   └── ObjectGraphAllocator.cs (was SemanticAllocator.cs)
│   └── Debug/
│       └── ColdStartDebugOptions.cs (was Engine/DebugOptions.cs)
│
├── Runtime/                   # NEW - Runtime subsystem
│   ├── VM/
│   │   ├── VirtualMachine.cs
│   │   ├── RuntimeDebugOptions.cs (was Core/VM/DebugOptions.cs)
│   │   └── RuntimeHeapAccessor.cs (was RuntimeHeapAccess.cs)
│   ├── Primitives/
│   │   ├── ArithmeticPrimitives.cs
│   │   ├── ControlPrimitives.cs
│   │   └── ...
│   └── Memory/
│       ├── LayoutResolver.cs
│       └── SpecialObjectsArray.cs
│
├── Compiler/                  # Existing - stays in separate project
│   ├── Analysis/
│   ├── CodeGen/
│   └── AST/
│
└── Tests/                     # Test organization
    ├── Unit/
    ├── Integration/
    └── Primitives/
```

---

## Renaming Plan

### Priority 1: Critical Duplicates (IMMEDIATE)

| Current Path | New Path | Reason |
|--------------|----------|--------|
| `Engine/DebugOptions.cs` | `ColdStart/Debug/ColdStartDebugOptions.cs` | Duplicate name conflict |
| `Core/VM/DebugOptions.cs` | `Runtime/VM/RuntimeDebugOptions.cs` | Duplicate name conflict |
| **DELETE** `Core/Objects/` | N/A | Legacy stub code (entire directory) |

**Action**: These must be resolved before any refactoring work

### Priority 2: Misleading/Generic Names (HIGH)

| Current Path | New Path | Reason |
|--------------|----------|--------|
| `Engine/Loader.cs` | `ColdStart/Orchestration/ColdStartOrchestrator.cs` | "Loader" too generic |
| `Core/SemanticAllocator.cs` | `ColdStart/Allocation/ObjectGraphAllocator.cs` | "Semantic" unclear |
| `Core/VM/RuntimeHeapAccess.cs` | `Runtime/VM/RuntimeHeapAccessor.cs` | "Access" → "Accessor" (noun) |
| `WinFormsDisplayProvider.cs` | `Platform/Windows/WinFormsDisplayProvider.cs` | Wrong location |
| `TestColdStart.cs` | `Tests/Integration/ColdStartTests.cs` | Wrong location |

### Priority 3: Consistency Improvements (MEDIUM)

| Current Path | New Path | Reason |
|--------------|----------|--------|
| `Core/ClassHierarchyUtils.cs` | `Runtime/Utilities/ClassHierarchyUtilities.cs` | "Utils" → "Utilities" |
| `Core/VM/VMUtilities.cs` | `Runtime/VM/VMUtilities.cs` | Move to Runtime subsystem |
| `Validator/Core/Interfaces/IHeavyCheckMarker.cs` | `Validator/Core/Attributes/HeavyCheckAttribute.cs` | Marker interface → Attribute |

### Priority 4: Subsystem Reorganization (During Refactoring)

This will happen as part of the subsystem refactoring work:

**Move to ColdStart/**:
- All Phase[N]_*.cs files → `ColdStart/Phases/`
- ColdStartPrimitives.cs → `ColdStart/Primitives/`
- InitExpressionExecutor.cs → `ColdStart/Init/`
- AssociationRegistry.cs → `ColdStart/Registries/`
- GlobalRegistry.cs → `ColdStart/Registries/`
- SymbolRegistry.cs → `ColdStart/Registries/`

**Move to Runtime/**:
- VirtualMachine.cs → `Runtime/VM/`
- All *Primitives.cs → `Runtime/Primitives/`
- LayoutResolver.cs → `Runtime/Memory/`
- NonLocalReturnException.cs → `Runtime/Exceptions/`

**Keep in Core/**:
- HeapManager/ - stays (shared by ColdStart and Runtime)
- Schema/ - stays (shared DTOs)
- Bytecodes/ - stays (shared tools)

### Priority 5: Test Organization (LOW)

Reorganize tests into clearer structure:

```
Tests/
├── Unit/
│   ├── HeapManagerTests.cs
│   └── BytecodeDecompilerTests.cs
├── Integration/
│   └── ColdStartTests.cs (was TestColdStart.cs)
└── Primitives/
    ├── ArithmeticPrimitivesTests.cs
    └── ObjectArrayPrimitivesTests.cs
```

---

## Integration with Refactoring Phases

### Phase 1: Critical Fixes (Week 1)
- Resolve duplicate DebugOptions
- Delete Core/Objects/ directory
- Move misplaced files (WinFormsDisplayProvider, TestColdStart)

### Phase 2: ColdStart Subsystem Extraction (Week 2-3)
- Create ColdStart/ directory structure
- Move and rename Loader.cs → ColdStartOrchestrator.cs
- Move Phase files to ColdStart/Phases/
- Move registries to ColdStart/Registries/
- Define IColdStartOrchestrator interface

### Phase 3: Runtime Subsystem Extraction (Week 3-4)
- Create Runtime/ directory structure
- Move VM and primitives
- Define IRuntimeExecutor interface
- Separate runtime from cold start concerns

### Phase 4: Polish and Consistency (Week 4)
- Rename Utils → Utilities
- Organize test directories
- Update all namespace references
- Update documentation

---

## Quick Reference

### Naming Pattern Cheat Sheet

| Component Type | Naming Pattern | Example |
|----------------|----------------|---------|
| **Interface** | `I[VerbNoun]` | `ISmalltalkHeapManager` |
| **Implementation** | `[Impl][Interface-I]` | `SmalltalkHeapManager` |
| **Service** | `[Domain]Service` | `ValidationService` |
| **Manager** | `[Domain]Manager` | `HeapManager` |
| **Orchestrator** | `[Domain]Orchestrator` | `ColdStartOrchestrator` |
| **Executor** | `[Domain]Executor` | `InitExpressionExecutor` |
| **Resolver** | `[Domain]Resolver` | `LayoutResolver` |
| **Registry** | `[Domain]Registry` | `SymbolRegistry` |
| **Primitive** | `[Category]Primitives` | `ArithmeticPrimitives` |
| **Validator** | `[What]Validator` | `BytecodeValidator` |
| **Utilities** | `[Domain]Utilities` | `VMUtilities` |
| **Exception** | `[Condition]Exception` | `NonLocalReturnException` |
| **Attribute** | `[Purpose]Attribute` | `ColdStartOnlyAttribute` |
| **DTO/Schema** | `[Entity]Descriptor` | `ClassDescriptor` |
| **Record** | `[Entity][Qualifier]` | `InitResult`, `ClassInfo` |
| **Test** | `[Target]Tests` | `HeapManagerTests` |
| **Phase** | `Phase[N]_[Purpose]` | `Phase1_PreflightAndSymbols` |

### Suffix Decision Guide

**When to use each suffix:**

- **Manager**: Owns and coordinates stateful resources (HeapManager, SymbolManager)
- **Service**: Provides stateless operations, often injected (ValidationService)
- **Orchestrator**: Coordinates complex multi-step workflows (ColdStartOrchestrator)
- **Executor**: Executes specific operations or commands (InitExpressionExecutor)
- **Resolver**: Resolves or looks up items by key/name (LayoutResolver, VariableResolver)
- **Registry**: Maintains registration and lookup tables (SymbolRegistry)
- **Accessor**: Provides access to data (RuntimeHeapAccessor)
- **Provider**: Provides implementation of capability (DisplayProvider, PlatformProvider)
- **Factory**: Creates instances (ObjectFactory)
- **Builder**: Builds complex objects step-by-step (AstBuilder)
- **Analyzer**: Analyzes structures (BytecodeStackAnalyzer, TailRecursionAnalyzer)
- **Validator**: Validates correctness (BytecodeValidator)
- **Utilities**: Collection of related utility functions (VMUtilities)
- **Helper**: Assists specific operations (JsonHelper)

---

## Validation Checklist

Before finalizing any rename:

- [ ] New name clearly communicates purpose
- [ ] Pattern matches category guidelines
- [ ] No naming conflicts with existing types
- [ ] File location matches namespace
- [ ] All references updated
- [ ] Tests still pass
- [ ] Documentation updated

---

## Future Considerations

### 1. Namespace Alignment

After refactoring, namespaces should align with directory structure:

```csharp
// Cold start subsystem
namespace Smalltalk80.Interpreter.ColdStart.Orchestration;
namespace Smalltalk80.Interpreter.ColdStart.Phases;

// Runtime subsystem
namespace Smalltalk80.Interpreter.Runtime.VM;
namespace Smalltalk80.Interpreter.Runtime.Primitives;

// Shared core
namespace Smalltalk80.Interpreter.Core.HeapManager;
namespace Smalltalk80.Interpreter.Core.Schema;
```

### 2. Partial Classes

VirtualMachine is currently split across multiple partial class files. Consider:
- VirtualMachine.cs - Core execution
- VirtualMachine.MessageSend.cs - Message sending
- VirtualMachine.ContextManagement.cs - Context operations
- VirtualMachine.Primitives.cs - Primitive dispatch

**Pattern**: `[ClassName].[Aspect].cs` for partial classes

### 3. Platform-Specific Code

Platform bridges should be clearly organized:

```
Platform/
├── IPlatformBridge.cs (interface)
├── Headless/
│   └── HeadlessPlatformBridge.cs
└── Windows/
    └── WinFormsDisplayProvider.cs
```

---

## Appendix A: Current File Inventory

### Interpreter Project Files (100 files)

**Core/HeapManager/ (5 files)**
- GarbageCollectionResult.cs ✅
- HeapStatistics.cs ✅
- ISmalltalkHeapManager.cs ✅
- ObjectTableEntry.cs ✅
- SmalltalkHeapManager.cs ✅

**Core/Objects/ (5 files) - RECOMMEND DELETE**
- ObjectFormat.cs → move to Core/HeapManager/
- ObjectMemory.cs → DELETE (stubs)
- ObjectTableEntry.cs → DELETE (duplicate)
- SObject.cs → DELETE (stub)
- SystemDictionary.cs → move to Core/HeapManager/ or DELETE

**Core/Schema/ (19 files) - ✅ All good**
- AssociationLiteral.cs
- BlockContextStructure.cs
- ClassDescriptor.cs
- ColdStartBundle.cs
- Consumer.cs (graph node)
- Diagnostics.cs
- Edge.cs (graph edge)
- FieldLayoutEntry.cs
- FieldValue.cs
- InitExpression.cs
- InstallOrder.cs
- InstallPhase.cs
- LiteralValue.cs
- Metadata.cs
- MethodDescriptor.cs
- MethodHeader.cs
- ObjectDescriptor.cs
- ObjectRef.cs
- PatchDescriptor.cs
- PatchTarget.cs
- PatchValue.cs
- PoolDependencyGraph.cs
- PoolReference.cs
- Producer.cs (graph node)
- SourceFile.cs
- UndeclaredVariable.cs

**Core/VM/ (15 files + Primitives/ + Graphics/)**
- AssociationRegistry.cs → move to ColdStart/Registries/
- BytecodeDisassembler.cs ✅
- ClassInfo.cs ✅
- ClassPrettyPrinter.cs ✅
- DebugOptions.cs → **RuntimeDebugOptions.cs**
- GlobalRegistry.cs → move to ColdStart/Registries/
- LayoutResolver.cs → move to Runtime/Memory/
- NonLocalReturnException.cs → move to Runtime/Exceptions/
- RuntimeHeapAccess.cs → **RuntimeHeapAccessor.cs**
- SmalltalkMath.cs ✅
- SymbolHasher.cs ✅
- SymbolRegistry.cs → move to ColdStart/Registries/
- VMConstants.cs ✅
- VMMetadata.cs ✅
- VMUtilities.cs → move to Runtime/VM/
- VirtualMachine.cs → move to Runtime/VM/

**Core/VM/Primitives/ (11 files)**
- ArithmeticPrimitives.cs ✅
- CompiledMethodPrimitives.cs ✅
- ControlPrimitives.cs ✅
- FloatPrimitives.cs ✅
- GraphicsPrimitives.cs ✅
- HeadlessPlatformBridge.cs → move to Platform/Headless/
- InputPrimitives.cs ✅
- IPlatformBridge.cs → move to Platform/
- IPrimitiveContext.cs ✅
- LargeIntegerPrimitives.cs ✅
- ObjectPrimitives.cs ✅
- PointPrimitives.cs ✅
- PrimitiveTable.cs ✅
- ProcessPrimitives.cs ✅
- StringPrimitives.cs ✅
- SystemPrimitives.cs ✅

**Core/VM/Graphics/ (3 files)**
- BitBltEngine.cs ✅
- CombinationRule.cs ✅
- IDisplayProvider.cs ✅

**Core/Bytecodes/ (6 files) - ✅ All good**
- BytecodeDecompiler.cs
- BytecodeOperandValidator.cs
- BytecodeStackAnalyzer.cs
- BytecodeStackAnalyzerCFG.cs
- BytecodeStackValidator.cs
- BytecodeDisassembler.cs (also in Core/VM/)

**Core/Memory/ (3 files)**
- ObjectTable.cs ✅
- ObjectTableStatistics.cs ✅
- SpecialObjectsArray.cs ✅

**Core/Attributes/ (2 files) - ✅ Good**
- ColdStartOnlyAttribute.cs
- RuntimeOnlyAttribute.cs

**Core/ (root - 3 files)**
- ClassHierarchyUtils.cs → **ClassHierarchyUtilities.cs**
- ColdstartReport.cs ✅
- SemanticAllocator.cs → **ObjectGraphAllocator.cs**
- SnapshotWriter.cs ✅

**Engine/ (4 files + subdirs)**
- ColdStartPrimitives.cs → move to ColdStart/Primitives/
- DebugOptions.cs → **ColdStartDebugOptions.cs**
- IColdStartPrimitives.cs → move to ColdStart/Primitives/
- Loader.cs → **ColdStartOrchestrator.cs**

**Engine/LoaderPhases/ (6 files)**
- Phase1_PreflightAndSymbols.cs → move to ColdStart/Phases/
- Phase2_ClassInfrastructure.cs → move to ColdStart/Phases/
- Phase3_ObjectAllocation.cs → move to ColdStart/Phases/
- Phase4_MethodCompilation.cs → move to ColdStart/Phases/
- Phase5_GlobalsAndPatches.cs → move to ColdStart/Phases/
- Phase6_ValidationAndGC.cs → move to ColdStart/Phases/

**Engine/InitExecution/ (2 files)**
- InitExpressionExecutor.cs → move to ColdStart/Init/
- InitResult.cs → move to ColdStart/Init/

**Root Level/ (3 files)**
- Program.cs ✅
- TestColdStart.cs → **Tests/Integration/ColdStartTests.cs**
- WinFormsDisplayProvider.cs → **Platform/Windows/WinFormsDisplayProvider.cs**

**Tests/ (4 files + subdirs)**
- HeapManagerTests.cs → Tests/Unit/

**Tests/Primitives/ (3 files)**
- ArithmeticPrimitivesTests.cs ✅
- ComprehensiveArithmeticTests.cs ✅
- ObjectArrayPrimitivesTests.cs ✅

---

## Appendix B: Namespace Migration Map

### Current → Future Namespace Mapping

```
Smalltalk80.Interpreter.Engine
  → Smalltalk80.Interpreter.ColdStart.Orchestration (Loader)
  → Smalltalk80.Interpreter.ColdStart.Phases (Phase files)

Smalltalk80.Interpreter.Core.VM
  → Smalltalk80.Interpreter.Runtime.VM (VirtualMachine, RuntimeHeapAccessor)
  → Smalltalk80.Interpreter.ColdStart.Registries (Registries)

Smalltalk80.Interpreter.Core.VM.Primitives
  → Smalltalk80.Interpreter.Runtime.Primitives

Smalltalk80.Interpreter.Core.HeapManager
  → Smalltalk80.Interpreter.Core.Memory (keep shared)

Smalltalk80.Interpreter.Core.Objects
  → DELETE (legacy stubs)

Smalltalk80.Interpreter.Core.Schema
  → Smalltalk80.Interpreter.Core.Schema (keep shared)

[Root]
  → Smalltalk80.Interpreter.Platform.Windows (WinFormsDisplayProvider)
  → Smalltalk80.Interpreter.Tests.Integration (TestColdStart)
```

---

## Document Revision History

| Date | Version | Changes |
|------|---------|---------|
| 2025-11-17 | 1.0 | Initial analysis and naming strategy |

---

**Next Steps:**
1. Review this document with team
2. Resolve Priority 1 duplicates (DebugOptions, ObjectTableEntry, Core/Objects/)
3. Begin ColdStart subsystem extraction
4. Incrementally apply naming conventions during refactoring
