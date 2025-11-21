# Physical Architecture

**Last Updated**: 2025-11-20

This document describes the file system organization, subsystem structure, and dependencies of the Smalltalk-80 interpreter.

---

## Overview

The interpreter consists of ~140 C# files organized into logical subsystems. The architecture separates **cold start** (building the heap from JSON) from **runtime** (executing Smalltalk bytecodes).

---

## Project Structure

```
Smalltalk80.Interpreter/
├── Program.cs                    # Entry point
├── CommandLineParser.cs          # CLI argument parsing
├── DiagnosticInspector.cs        # Heap inspection utilities
│
├── Core/                         # Shared infrastructure
│   ├── Attributes/               # Lifecycle markers
│   ├── Bytecodes/                # Bytecode analysis tools
│   ├── Diagnostics/              # Event-based diagnostics
│   ├── HeapManager/              # Memory management
│   ├── Memory/                   # Object data structures
│   ├── Schema/                   # JSON DTOs
│   ├── VM/                       # Virtual machine
│   └── *.cs                      # Shared utilities
│
├── Engine/                       # Cold start system
│   ├── LoaderPhases/             # 6 cold start phases
│   └── InitExecution/            # Init expression runner
│
├── Platform/                     # Platform-specific
│   └── Windows/                  # Windows Forms display
│
└── Tests/                        # Test infrastructure
    ├── Integration/              # Integration tests
    ├── Interfaces/               # Interface tests
    └── Primitives/               # Primitive tests
```

---

## Subsystem Details

### Smalltalk80.Interpreter.Shared.Types Assembly (2 files)

**SEPARATE ASSEMBLY** - Core value types with zero dependencies. Layer 0 of the architecture.

| File | Purpose |
|------|---------|
| SmalltalkOOP.cs | Tagged object reference (SmallInteger or object pointer) |
| ObjectFormat.cs | Object storage format enum (Pointer, PointerIndexed, ByteIndexed, WordIndexed) |

**Key Type**: `SmalltalkOOP` - readonly struct implementing LSB tagging (LSB=1: SmallInteger, LSB=0: object pointer)

**Nil Encapsulation**: OOP(0) is implementation detail. Use `heap.GetNil()` to access Nil singleton.

---

### Smalltalk80.Interpreter.HeapManager Assembly (11 files)

**SEPARATE ASSEMBLY** - Memory management and object allocation with zero dependencies on VM semantics.

**Dependencies**: Only `Smalltalk80.Interpreter.Shared.Types` (SmalltalkOOP, ObjectFormat, FormatWord)

**Location**: `/Smalltalk80.Interpreter.HeapManager/`

#### Three-Interface Architecture

The heap manager is split into three focused interfaces:

1. **ISmalltalkHeap** - Core heap operations (250 lines)
   - Nil singleton: `GetNil()` - Returns placeholder/real nil OOP
   - Object allocation: `AllocateObject()`, `AllocateInstanceOf()` (reads format from heap)
   - Instance variable access with write barriers: `SetInstanceVariableWithBarrier()`, `SetInstanceVariableNoBarrier()`
   - Indexed access (OOP, byte, word): `GetIndexedOOP()`, `SetIndexedOOP()`, `GetIndexedByte()`, `SetIndexedByte()`, `GetIndexedWord()`, `SetIndexedWord()`
   - Class pointer access: `GetClassOOP()`, `SetClassOOP()`
   - Object metadata: `GetObjectFormat()`, `GetObjectSizeInWords()`, `GetInstanceVariableCount()`, `GetIndexedSize()`
   - Validation: `IsValidObjectOOP()`, `IsValidOOP()`, `EnumerateObjectTable()`
   - Instance finding (primitives 77-78): `FindInstanceOf()`, `FindNextInstanceOf()`
   - Diagnostics: `GetClassName()`, `ValidateAllObjectClasses()`, `VerboseDebugMode`
   - Subsystem access via `GC` and `Become` properties

2. **IGarbageCollector** - Generic GC with zero semantic knowledge (72 lines)
   - Root management: `AddGCRoot()`, `RemoveGCRoot()`, `GetGCRoots()`, `ClearGCRoots()`
   - Collection: `GarbageCollect()` (4 phases: clear marks → mark reachable → find unreachable → collect)
   - Statistics: `GetStatistics()` - Current GC statistics without collection
   - Debug/Testing: `GetUnreachableObjects()` - Marking only, no actual collection

3. **IBecomeHandler** - Object identity swapping (20 lines)
   - `BecomeTwoWay(oop1, oop2)` - Primitive 72 support (swaps identities of two objects)
   - Used for placeholder nil swap during cold start Phase 3

#### Files

| File | Purpose |
|------|---------|
| ISmalltalkHeap.cs | Core heap interface (250 lines) |
| ISmalltalkHeapManager.cs | Facade interface (backwards compatibility) |
| SmalltalkHeapManager.cs | Heap implementation (825+ lines) |
| IGarbageCollector.cs | GC interface (72 lines) |
| GarbageCollector.cs | Mark-sweep GC implementation |
| IBecomeHandler.cs | Identity swap interface (20 lines) |
| BecomeHandler.cs | Become operations implementation |
| IObjectTableManager.cs | Table management interface + ObjectTableBlock |
| ObjectTableManager.cs | Object table block allocation and index management |
| ObjectTableEntry.cs | Object metadata structure (200 lines) |
| HeapStatistics.cs | GC statistics structure |

**Key Architectural Principle - Placeholder Nil Pattern**:
1. Heap initialization creates placeholder nil at index 1 (OOP 0x0002)
2. Cold start uses `heapManager.GetNil()` throughout Phases 1-5
3. Phase 3 creates real UndefinedObject and calls `heapManager.Become.BecomeTwoWay(placeholderNil, realNil)`
4. Phase 6 GC collects exactly 1 object (unreachable placeholder nil)

**Usage**:
```csharp
// Nil access (runtime and cold start)
var nil = heap.GetNil();

// GC operations
heap.GC.AddGCRoot(oop);
heap.GC.GarbageCollect();

// Become operations (cold start only)
heap.Become.BecomeTwoWay(placeholderNil, realNil);
```

**See**: [HEAP-MANAGER-ARCHITECTURE.md](HEAP-MANAGER-ARCHITECTURE.md) for complete heap manager design details

---

### Core/Memory (1 file)

Special objects management (well-known OOPs).

| File | Purpose |
|------|---------|
| SpecialObjectsArray.cs | Well-known objects cache (ProcessorScheduler, Nil, True, False, etc.) |

**Note**: Object storage structures (ObjectData, ObjectTable, ObjectFormat) are now in the `Smalltalk80.Interpreter.HeapManager` assembly and `Smalltalk80.Interpreter.Shared.Types` assembly.

---

### Core/Schema (24 files)

JSON DTOs for the cold start bundle. These define the structure of `coldstart.json`.

**Key Files**:
- `ColdStartBundle.cs` - Root bundle structure
- `ClassDescriptor.cs` - Class definitions
- `MethodDescriptor.cs` - Compiled methods
- `ObjectDescriptor.cs` - Object instances
- `PatchDescriptor.cs` - Forward reference patches
- `InitExpression.cs` - Initialization expressions

---

### Core/VM (45 files)

Virtual machine runtime execution.

#### Main Components

| File | Lines | Purpose |
|------|-------|---------|
| VirtualMachine.cs | ~1,500 | Main orchestrator |
| VirtualMachine.MessageSending.cs | - | Partial: message dispatch |
| VirtualMachine.VariableAccess.cs | - | Partial: variable access |
| BytecodeInterpreter.cs | ~1,100 | Bytecode dispatch |
| MessageDispatcher.cs | ~700 | Method lookup and sending |
| ContextManager.cs | ~500 | Context lifecycle |
| StackManager.cs | ~200 | Stack operations |
| VMDebugger.cs | ~400 | Tracing and diagnostics |

#### Interfaces (Component Boundaries)

| Interface | Implementation | Purpose |
|-----------|----------------|---------|
| IStackManager | StackManager | Push/Pop/Peek |
| IContextManager | ContextManager | Context lifecycle |
| IMessageDispatcher | MessageDispatcher | Method lookup |
| IBytecodeInterpreter | BytecodeInterpreter | Instruction dispatch |
| IVMDebugger | VMDebugger | Diagnostics |
| IRuntimeHeapAccessor | RuntimeHeapAccessor | Runtime heap access |

#### Runtime Services

| File | Purpose |
|------|---------|
| RuntimeHeapAccessor.cs | Safe runtime heap access |
| RuntimeClassCache.cs | Cached class lookups |
| RuntimeSpecialObjects.cs | Cached special objects |
| LayoutResolver.cs | Field index resolution |
| VMMetadata.cs | VM configuration record |
| VMConstants.cs | Context slot indices |
| VMUtilities.cs | Helper functions |
| SmalltalkMath.cs | Smalltalk arithmetic |

#### Registries (Cold Start Only)

| File | Purpose |
|------|---------|
| GlobalRegistry.cs | Global variable storage |
| SymbolRegistry.cs | Symbol interning |
| AssociationRegistry.cs | Association tracking |
| SymbolHasher.cs | Symbol hash computation |

**Note**: Registries are frozen after cold start and read-only during runtime.

---

### Core/VM/Primitives (15 files)

Primitive operation implementations using the `IPrimitive` pattern.

| File | Primitives | Purpose |
|------|------------|---------|
| ArithmeticPrimitives.cs | 1-20 | Integer arithmetic |
| FloatPrimitives.cs | 40-58 | Float arithmetic |
| LargeIntegerPrimitives.cs | 21-39 | Large integer ops |
| ObjectPrimitives.cs | 60-79 | Object operations |
| ControlPrimitives.cs | 80-89 | Control flow |
| StringPrimitives.cs | 100-109 | String operations |
| InputPrimitives.cs | 90-99 | Input handling |
| GraphicsPrimitives.cs | 110-127 | BitBlt and graphics |
| PointPrimitives.cs | 130-138 | Point operations |
| SystemPrimitives.cs | 128, 200+ | System operations |

**Interfaces**:
- `IPrimitive` - Individual primitive contract
- `IPrimitiveRegistry` - Registry and dispatch
- `IPrimitiveContext` - Execution context
- `PrimitiveExecutionContext` - Dependency injection
- `PrimitiveResult` - Success/failure result

---

### Core/VM/Views (5 files)

Type-safe Blue Book view types for semantic clarity.

| File | Purpose |
|------|---------|
| MethodContextView.cs | Method context fields |
| BlockContextView.cs | Block context fields |
| CompiledMethodView.cs | Compiled method structure |
| ClassView.cs | Class structure |
| IContextAccessor.cs | Unified context access |

---

### Core/VM/BytecodeTypes (2 files)

Typed instruction records for pattern matching.

| File | Purpose |
|------|---------|
| Instructions.cs | 40+ instruction record types |
| BytecodeDecoder.cs | Decode bytes to instructions |

---

### Core/Bytecodes (5 files)

Bytecode analysis and validation tools.

| File | Purpose |
|------|---------|
| BytecodeDecompiler.cs | Disassembly |
| BytecodeStackValidator.cs | Stack balance checking |
| BytecodeStackAnalyzer.cs | Stack effect analysis |
| BytecodeStackAnalyzerCFG.cs | Control flow analysis |
| BytecodeOperandValidator.cs | Operand validation |

---

### Core/Diagnostics (7 files)

Event-based diagnostic system with multiple listeners.

| File | Purpose |
|------|---------|
| DiagnosticEventTypes.cs | Event type flags |
| DiagnosticEvents.cs | Typed event records |
| IDiagnosticHub.cs | Hub interface |
| DiagnosticHub.cs | Event dispatch |
| Listeners/ColdStartProgressListener.cs | Phase progress |
| Listeners/ColdStartDetailListener.cs | Verbose cold start |
| Listeners/MethodTraceListener.cs | Method entry/exit |

---

### Engine (4 files)

Cold start orchestration.

| File | Purpose |
|------|---------|
| ColdStartOrchestrator.cs | Orchestrates 6 phases |
| ColdStartPrimitives.cs | Cold start primitive helpers |
| IColdStartInterfaces.cs | Cold start interfaces |
| IColdStartPrimitives.cs | Primitive interface |

---

### Engine/LoaderPhases (15 files)

Six cold start phases that build the heap from JSON.

| Phase | File | Purpose |
|-------|------|---------|
| 1 | Phase1_PreflightAndSymbols.cs | Validate bundle, create symbols |
| 2 | Phase2_ClassInfrastructure.cs | Create class hierarchy |
| 3 | Phase3_ObjectAllocation.cs | Allocate all objects |
| 4 | Phase4_MethodCompilation.cs | Compile methods to bytecode |
| 5 | Phase5/*.cs (10 files) | Install globals, apply patches |
| 6 | Phase6_ValidationAndGC.cs | Validate heap, run GC |

**Phase 5 Components**:
- Interfaces.cs - Component interfaces
- DataTypes.cs - Phase5Context record
- FieldValueResolver.cs - Resolve field values
- DictionaryOperations.cs - Hash-based operations
- AssociationManager.cs - Association identity
- PatchApplier.cs - Apply forward references
- USTableBuilder.cs - Build symbol table
- SystemDictionaryInstaller.cs - Install globals
- RuntimeObjectCreator.cs - Create Display, etc.
- Phase5Orchestrator.cs - Coordinate components

---

### Engine/InitExecution (3 files)

Initialize expression execution after cold start.

| File | Purpose |
|------|---------|
| InitExpressionExecutor.cs | Execute init expressions |
| InitExpressionOrdering.cs | Dependency ordering |
| InitResult.cs | Execution result |

---

## Dependency Graph

### Component Flow

```
                    ┌─────────────┐
                    │   Program   │
                    └──────┬──────┘
                           │
              ┌────────────┼────────────┐
              ▼            ▼            ▼
     ┌────────────┐  ┌──────────┐  ┌──────────┐
     │ ColdStart  │  │ Init     │  │ Diagnostic│
     │Orchestrator│  │ Executor │  │ Inspector │
     └──────┬─────┘  └────┬─────┘  └──────────┘
            │              │
            ▼              ▼
     ┌─────────────────────────┐
     │    VirtualMachine       │
     │  ┌─────┐ ┌──────────┐  │
     │  │Stack│ │Bytecode  │  │
     │  │Mgr  │ │Interpreter│ │
     │  └─────┘ └──────────┘  │
     │  ┌───────┐ ┌────────┐  │
     │  │Context│ │Message │  │
     │  │Manager│ │Dispatch│  │
     │  └───────┘ └────────┘  │
     └───────────┬────────────┘
                 │
     ┌───────────┼───────────┐
     ▼           ▼           ▼
┌──────────┐ ┌─────────┐ ┌─────────┐
│  Heap    │ │Primitive│ │ Runtime │
│ Manager  │ │Registry │ │ Access  │
│          │ └─────────┘ └─────────┘
│ ┌──────┐ │
│ │  GC  │ │ (Three-Interface Architecture)
│ └──────┘ │
│ ┌──────┐ │
│ │Become│ │
│ └──────┘ │
└──────────┘
```

### Layered Architecture

```
┌────────────────────────────────────────────────────┐
│ Layer 2: Smalltalk80.Interpreter (Core + Engine)  │
│                                                    │
│  ┌─────────────┐     ┌──────────────┐            │
│  │   Program   │────▶│ ColdStart    │            │
│  └─────────────┘     │ Orchestrator │            │
│         │            └──────┬───────┘            │
│         │                   │                     │
│         ▼                   ▼                     │
│  ┌─────────────────────────────────┐             │
│  │     VirtualMachine              │             │
│  │  ┌─────┐ ┌──────────┐ ┌──────┐ │             │
│  │  │Stack│ │Bytecode  │ │Msg   │ │             │
│  │  │Mgr  │ │Interpeter│ │Disp  │ │             │
│  │  └─────┘ └──────────┘ └──────┘ │             │
│  └─────────────────────────────────┘             │
│         │                                         │
└─────────┼─────────────────────────────────────────┘
          │
┌─────────┼─────────────────────────────────────────┐
│ Layer 1: Smalltalk80.Interpreter.HeapManager      │
│         ▼                                         │
│  ┌──────────────────────────────────┐            │
│  │  SmalltalkHeapManager            │            │
│  │  (ISmalltalkHeap)                │            │
│  │  ┌───────────┐  ┌─────────────┐ │            │
│  │  │    GC     │  │   Become    │ │            │
│  │  │ Collector │  │   Handler   │ │            │
│  │  └───────────┘  └─────────────┘ │            │
│  └──────────────────────────────────┘            │
│         │                                         │
└─────────┼─────────────────────────────────────────┘
          │
┌─────────┼─────────────────────────────────────────┐
│ Layer 0: Smalltalk80.Interpreter.Shared.Types     │
│         ▼                                         │
│  ┌─────────────┐  ┌──────────────┐              │
│  │SmalltalkOOP │  │ ObjectFormat │              │
│  └─────────────┘  └──────────────┘              │
│                                                   │
└───────────────────────────────────────────────────┘
```

### Dependency Rules

- **Layer 0** (Shared.Types): Zero dependencies, pure value types
- **Layer 1** (HeapManager): Depends only on Shared.Types
- **Layer 2** (Core): Depends on HeapManager and Shared.Types
- **No circular dependencies** between layers

---

## Key Design Patterns

### 1. Interface-Based Components

All major components are behind interfaces for testability:

```csharp
public interface IStackManager
{
    void Push(SmalltalkOOP value);
    SmalltalkOOP Pop();
    SmalltalkOOP Peek();
}
```

### 2. Cold Start vs Runtime Separation

**Cold Start Phase** (Phases 1-6):
- Uses registries (GlobalRegistry, SymbolRegistry)
- Builds heap from ColdStartBundle
- Marked with `[ColdStartOnly]` attribute

**Runtime Phase** (Init expressions onward):
- Uses only heap and LayoutResolver
- Registries frozen (throw on modification)
- Marked with `[RuntimeOnly]` attribute

### 3. Freeze Pattern

Registries are frozen after cold start:

```csharp
// In Phase 5
AssociationRegistry.Freeze();

// Runtime access throws
GlobalRegistry.Add(...);  // InvalidOperationException
```

### 4. IPrimitive Pattern

All primitives implement a common interface:

```csharp
public interface IPrimitive
{
    int Number { get; }
    string Name { get; }
    PrimitiveResult Execute(PrimitiveExecutionContext context);
}
```

### 5. Three-Interface Architecture (HeapManager)

The heap manager separates concerns into three focused interfaces:

```csharp
// Main heap operations
public interface ISmalltalkHeap
{
    SmalltalkOOP GetNil();
    SmalltalkOOP AllocateObject(ObjectFormat format, int fixedSlots, int indexedSize);
    SmalltalkOOP GetInstanceVariable(SmalltalkOOP objectOOP, int index);
    void SetInstanceVariableWithBarrier(SmalltalkOOP objectOOP, int index, SmalltalkOOP value);

    // Subsystem access
    IGarbageCollector GC { get; }
    IBecomeHandler Become { get; }
}

// Generic GC with zero semantic knowledge
public interface IGarbageCollector
{
    void AddGCRoot(SmalltalkOOP oop);
    void RemoveGCRoot(SmalltalkOOP oop);
    GCStatistics GarbageCollect();
    SmalltalkOOP[] GetUnreachableObjects(); // Testing only
}

// Object identity swapping (bootstrap-only)
public interface IBecomeHandler
{
    void BecomeTwoWay(SmalltalkOOP oop1, SmalltalkOOP oop2);
}
```

**Benefits**:
- Clear separation of concerns (Heap, GC, Become)
- Generic GC interface with no Smalltalk semantics
- Cleaner public API (no confusing GetSlot/SetSlot)
- Testable in isolation

**Usage**:
```csharp
// New API
heap.GC.AddGCRoot(soa.Nil);
heap.Become.BecomeTwoWay(placeholderNil, realNil);

// Legacy API (compatibility)
heap.AddGCRoots(new[] { soa.Nil });
heap.BecomeTwoWay(placeholderNil, realNil);
```

---

## File Size Guidelines

| Category | Target | Rationale |
|----------|--------|-----------|
| Orchestrators | < 500 lines | Coordination only |
| Components | < 800 lines | Single responsibility |
| Large components | < 1,500 lines | VM, Interpreter |
| Generators | No limit | Cohesive output |

**Current Largest Files**:
- VirtualMachine.cs: ~1,500 lines
- BytecodeInterpreter.cs: ~1,100 lines
- SmalltalkHeapManager.cs: 774 lines

---

## Namespace Conventions

Namespaces match directory structure:

```csharp
// File: Core/VM/StackManager.cs
namespace Smalltalk80.Interpreter.Core.VM;

// File: Engine/LoaderPhases/Phase5/USTableBuilder.cs
namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

// File: Core/HeapManager/GarbageCollector.cs
namespace Smalltalk80.Interpreter.Core.HeapManager;
```

---

## Adding New Components

### Adding a New Primitive

1. Create class implementing `IPrimitive` in `Core/VM/Primitives/`
2. Register in `PrimitiveRegistry.RegisterAllPrimitives()`
3. Add tests in `Tests/Primitives/`

### Adding a New Cold Start Phase

1. Create class implementing `IPhase` in `Engine/LoaderPhases/`
2. Add to phase list in `ColdStartOrchestrator`
3. Document preconditions and postconditions

### Adding a New VM Component

1. Define interface in `Core/VM/I{Component}.cs`
2. Create implementation in `Core/VM/{Component}.cs`
3. Inject into VirtualMachine constructor
4. Add tests in `Tests/`

---

## Build and Test

```bash
# Build
cd Smalltalk80.Interpreter
dotnet build

# Run with default diagnostics
dotnet run

# Run with verbose diagnostics
dotnet run -- --diagnostics detail,method

# Run BlockContext regression test
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected Results**:
- Build: 0 errors, 0 warnings
- Init expressions: 31/46 succeed
- BlockContext tests: Returns 1191 (120 tests pass)

---

## Related Documents

- [IMPLEMENTATION-ARCHITECTURE.md](IMPLEMENTATION-ARCHITECTURE.md) - How bytecode execution, contexts, and primitives work
- [HEAP-MANAGER-ARCHITECTURE.md](HEAP-MANAGER-ARCHITECTURE.md) - Heap manager design and three-interface architecture
- [DEVELOPMENT-GUIDE.md](DEVELOPMENT-GUIDE.md) - How to extend the interpreter with new primitives, phases, and components
- [../Block-Context-And-Variable-Access-COMPREHENSIVE.md](../Block-Context-And-Variable-Access-COMPREHENSIVE.md) - BlockContext design details
