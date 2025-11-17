# File Naming Quick Reference

**Last Updated**: 2025-11-17

Quick reference guide for file naming conventions during the interpreter refactoring.

---

## Immediate Action Items

### 🔴 CRITICAL: Must Fix Before Refactoring

1. **Duplicate DebugOptions**
   ```bash
   # Rename these files:
   Engine/DebugOptions.cs → ColdStart/Debug/ColdStartDebugOptions.cs
   Core/VM/DebugOptions.cs → Runtime/VM/RuntimeDebugOptions.cs
   ```

2. **Delete Legacy Stub Directory**
   ```bash
   # After confirming no runtime dependencies:
   rm -rf Core/Objects/
   # Move ObjectFormat.cs to Core/HeapManager/ first
   ```

3. **Move Misplaced Files**
   ```bash
   WinFormsDisplayProvider.cs → Platform/Windows/WinFormsDisplayProvider.cs
   TestColdStart.cs → Tests/Integration/ColdStartTests.cs
   ```

---

## Naming Pattern Cheat Sheet

### Component Type → Naming Pattern

| What | Pattern | Example |
|------|---------|---------|
| **Interface** | `I[VerbNoun]` | `ISmalltalkHeapManager` |
| **Implementation** | `[Impl][InterfaceName-I]` | `SmalltalkHeapManager` |
| **Orchestrator** | `[Domain]Orchestrator` | `ColdStartOrchestrator` |
| **Manager** | `[Domain]Manager` | `HeapManager` |
| **Executor** | `[Domain]Executor` | `InitExpressionExecutor` |
| **Resolver** | `[Domain]Resolver` | `LayoutResolver` |
| **Registry** | `[Domain]Registry` | `SymbolRegistry` |
| **Accessor** | `[Domain]Accessor` | `RuntimeHeapAccessor` |
| **Primitive** | `[Category]Primitives` | `ArithmeticPrimitives` |
| **Validator** | `[What]Validator` | `BytecodeValidator` |
| **Utilities** | `[Domain]Utilities` | `VMUtilities` |
| **Exception** | `[Condition]Exception` | `NonLocalReturnException` |
| **Test** | `[Target]Tests` | `HeapManagerTests` |

### Suffix Guide

**When to use each suffix:**

- **Manager** - Owns stateful resources (HeapManager)
- **Orchestrator** - Complex multi-step workflows (ColdStartOrchestrator)
- **Executor** - Executes operations (InitExpressionExecutor)
- **Resolver** - Looks up/resolves items (LayoutResolver)
- **Registry** - Registration/lookup tables (SymbolRegistry)
- **Accessor** - Provides data access (RuntimeHeapAccessor)
- **Service** - Stateless operations (ValidationService)
- **Utilities** - Static helper functions (VMUtilities)

---

## High Priority Renames

### Generic Names → Descriptive Names

```
Loader.cs → ColdStartOrchestrator.cs
SemanticAllocator.cs → ObjectGraphAllocator.cs
RuntimeHeapAccess.cs → RuntimeHeapAccessor.cs
```

### Inconsistent Suffixes

```
ClassHierarchyUtils.cs → ClassHierarchyUtilities.cs
(Standardize on "Utilities" not "Utils")
```

---

## Directory Structure (Target)

```
Smalltalk80.Interpreter/
├── ColdStart/                    # NEW - Cold start subsystem
│   ├── Orchestration/
│   │   └── ColdStartOrchestrator.cs
│   ├── Phases/
│   │   ├── Phase1_PreflightAndSymbols.cs
│   │   └── Phase2-6...
│   ├── Registries/
│   │   ├── SymbolRegistry.cs
│   │   ├── GlobalRegistry.cs
│   │   └── AssociationRegistry.cs
│   ├── Allocation/
│   │   └── ObjectGraphAllocator.cs
│   ├── Init/
│   │   ├── InitExpressionExecutor.cs
│   │   └── InitResult.cs
│   └── Debug/
│       └── ColdStartDebugOptions.cs
│
├── Runtime/                      # NEW - Runtime subsystem
│   ├── VM/
│   │   ├── VirtualMachine.cs
│   │   ├── RuntimeDebugOptions.cs
│   │   ├── RuntimeHeapAccessor.cs
│   │   └── VMUtilities.cs
│   ├── Primitives/
│   │   ├── ArithmeticPrimitives.cs
│   │   └── [All other primitives]
│   ├── Memory/
│   │   ├── LayoutResolver.cs
│   │   └── SpecialObjectsArray.cs
│   └── Exceptions/
│       └── NonLocalReturnException.cs
│
├── Core/                         # SHARED - Used by both subsystems
│   ├── Memory/                   # Was HeapManager/
│   │   ├── ISmalltalkHeapManager.cs
│   │   ├── SmalltalkHeapManager.cs
│   │   └── ObjectTableEntry.cs
│   ├── Schema/                   # JSON DTOs
│   │   ├── ColdStartBundle.cs
│   │   └── [All descriptors]
│   └── Bytecodes/                # Bytecode tools
│       ├── BytecodeDecompiler.cs
│       └── BytecodeValidator.cs
│
├── Platform/                     # NEW - Platform-specific
│   ├── IPlatformBridge.cs
│   ├── Headless/
│   │   └── HeadlessPlatformBridge.cs
│   └── Windows/
│       └── WinFormsDisplayProvider.cs
│
└── Tests/
    ├── Unit/
    ├── Integration/
    └── Primitives/
```

---

## Validation Rules

Before committing any rename:

✅ **File name matches primary type name**
✅ **Pattern matches category guidelines**
✅ **No naming conflicts**
✅ **Location matches namespace**
✅ **All references updated**
✅ **Tests still pass**

---

## Common Mistakes to Avoid

❌ **Generic names**: Manager.cs, Helper.cs, Util.cs
❌ **Inconsistent suffixes**: Utils vs Utilities, Access vs Accessor
❌ **Abbreviations**: Mgr, Svc, Exec (spell them out)
❌ **Multiple public types per file** (except nested helpers)
❌ **File name doesn't match type name**
❌ **Missing subsystem context** (ColdStart vs Runtime)

---

## Phase Execution Order

### Phase 1: Critical Fixes (Do First)
1. Rename both DebugOptions files
2. Delete Core/Objects/ directory
3. Move WinFormsDisplayProvider to Platform/
4. Move TestColdStart to Tests/Integration/

### Phase 2: ColdStart Subsystem
1. Create ColdStart/ directory structure
2. Rename Loader → ColdStartOrchestrator
3. Move Phase files to ColdStart/Phases/
4. Move registries to ColdStart/Registries/

### Phase 3: Runtime Subsystem
1. Create Runtime/ directory structure
2. Move VirtualMachine and primitives
3. Rename RuntimeHeapAccess → RuntimeHeapAccessor

### Phase 4: Polish
1. Rename Utils → Utilities
2. Move shared components to correct locations
3. Update all namespace references

---

## Quick Namespace Rules

Namespace MUST match directory structure:

```csharp
// File: ColdStart/Orchestration/ColdStartOrchestrator.cs
namespace Smalltalk80.Interpreter.ColdStart.Orchestration;

// File: Runtime/VM/VirtualMachine.cs
namespace Smalltalk80.Interpreter.Runtime.VM;

// File: Core/Memory/SmalltalkHeapManager.cs
namespace Smalltalk80.Interpreter.Core.Memory;
```

---

## Questions? Check Full Document

See `/docs/File-Naming-Strategy-And-Refactoring-Plan.md` for:
- Complete analysis of all 190 files
- Detailed rationale for each decision
- Migration maps for namespaces
- Full file inventory

---

**Remember**: Clear names make code self-documenting!
