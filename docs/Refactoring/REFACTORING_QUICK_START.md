# Refactoring Quick Start Guide

**Goal**: Begin the architectural refactoring with Phase 0 (Foundation)
**Time**: Week 1 (20 hours)
**Prerequisites**: Read ARCHITECTURE_ANALYSIS_AND_REFACTORING_PLAN.md

---

## Phase 0: Foundation (Week 1)

### Objectives

1. ✅ Define all formal interfaces
2. ✅ Create test infrastructure
3. ✅ Establish architectural validation
4. ✅ Document design decisions

### Deliverables Checklist

- [ ] `/Smalltalk80.Abstractions/` project created
- [ ] All interface definitions written with XML docs
- [ ] Unit test projects for each subsystem
- [ ] Integration test harness
- [ ] CLAUDE.md updated with new architecture
- [ ] All existing tests still pass (no behavior changes)

---

## Step-by-Step Instructions

### Step 1: Create Abstractions Assembly (1 hour)

```bash
cd /home/user/Smalltalk-80

# Create new class library for interfaces
dotnet new classlib -n Smalltalk80.Abstractions -f net9.0
cd Smalltalk80.Abstractions

# Create directory structure
mkdir -p Runtime/VM
mkdir -p Runtime/Primitives
mkdir -p Runtime/Services
mkdir -p ColdStart
mkdir -p Compiler
mkdir -p Foundation

# Add to solution
cd ..
dotnet sln add Smalltalk80.Abstractions/Smalltalk80.Abstractions.csproj
```

**Result**: Separate assembly for interfaces (enforces contract-based design)

---

### Step 2: Define Runtime VM Interfaces (3 hours)

#### `/Runtime/VM/IVirtualMachine.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.VM;

/// <summary>
/// Public API for the Smalltalk-80 Virtual Machine.
/// Executes bytecode, manages execution contexts, and coordinates primitives.
/// </summary>
public interface IVirtualMachine
{
    /// <summary>Executes a compiled method with given receiver and arguments.</summary>
    /// <param name="methodOOP">CompiledMethod object pointer.</param>
    /// <param name="receiver">Receiver object pointer.</param>
    /// <param name="args">Arguments array (OOPs).</param>
    /// <returns>Return value OOP.</returns>
    SmalltalkOOP Execute(SmalltalkOOP methodOOP, SmalltalkOOP receiver, SmalltalkOOP[] args);

    /// <summary>Sends a message to a receiver.</summary>
    /// <param name="receiver">Receiver object pointer.</param>
    /// <param name="selector">Selector Symbol object pointer.</param>
    /// <param name="args">Arguments array (OOPs).</param>
    /// <returns>Result of message send.</returns>
    SmalltalkOOP SendMessage(SmalltalkOOP receiver, SmalltalkOOP selector, SmalltalkOOP[] args);

    /// <summary>Gets the currently active context.</summary>
    /// <returns>Active context OOP (MethodContext or BlockContext).</returns>
    SmalltalkOOP GetActiveContext();

    /// <summary>Gets execution statistics.</summary>
    VMStatistics GetStatistics();

    /// <summary>Resets instruction counter and statistics.</summary>
    void Reset();
}

/// <summary>
/// Execution statistics for VM diagnostics and profiling.
/// </summary>
public readonly struct VMStatistics
{
    public long InstructionCount { get; init; }
    public long MessageSendCount { get; init; }
    public long ContextSwitchCount { get; init; }
    public long PrimitiveInvocationCount { get; init; }
    public long GarbageCollectionCount { get; init; }
}
```

#### `/Runtime/VM/IBytecodeInterpreter.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.VM;

/// <summary>
/// Low-level bytecode interpretation.
/// Internal to runtime engine, not exposed publicly.
/// </summary>
public interface IBytecodeInterpreter
{
    /// <summary>Executes bytecodes in a context until return or context switch.</summary>
    /// <param name="contextOOP">Context to execute (MethodContext or BlockContext).</param>
    /// <returns>Call outcome (Normal, NonLocalReturn, ContextSwitch, Exception).</returns>
    CallOutcome ExecuteBytecodeLoop(SmalltalkOOP contextOOP);

    /// <summary>Fetches next bytecode from instruction stream.</summary>
    /// <returns>Bytecode value (0-255).</returns>
    byte FetchByte();

    /// <summary>Decodes and dispatches single bytecode.</summary>
    /// <param name="bytecode">Bytecode to execute.</param>
    void ExecuteInstruction(byte bytecode);
}

/// <summary>
/// Result of bytecode execution (used for trampoline control flow).
/// </summary>
public readonly struct CallOutcome
{
    public CallOutcomeKind Kind { get; init; }
    public SmalltalkOOP Value { get; init; }
    public SmalltalkOOP TargetContext { get; init; }
    public Exception? Exception { get; init; }

    public static CallOutcome Normal(SmalltalkOOP value)
        => new() { Kind = CallOutcomeKind.Normal, Value = value };

    public static CallOutcome NonLocalReturn(SmalltalkOOP target, SmalltalkOOP value)
        => new() { Kind = CallOutcomeKind.NonLocalReturn, Value = value, TargetContext = target };

    public static CallOutcome ContextSwitch()
        => new() { Kind = CallOutcomeKind.ContextSwitch };

    public static CallOutcome Exception(Exception ex)
        => new() { Kind = CallOutcomeKind.Exception, Exception = ex };
}

/// <summary>Kind of call outcome.</summary>
public enum CallOutcomeKind
{
    Normal,           // Normal return
    NonLocalReturn,   // Non-local return (e.g., from block)
    ContextSwitch,    // Context switched (continue in new context)
    Exception         // VM exception occurred
}
```

#### `/Runtime/VM/IMessageDispatcher.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.VM;

/// <summary>
/// Message sending and method lookup.
/// Internal to runtime engine.
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>Looks up method for selector in receiver's class hierarchy.</summary>
    /// <param name="receiver">Receiver object pointer.</param>
    /// <param name="selector">Selector Symbol object pointer.</param>
    /// <returns>CompiledMethod OOP, or nil if not found.</returns>
    SmalltalkOOP LookupMethod(SmalltalkOOP receiver, SmalltalkOOP selector);

    /// <summary>Creates and activates a new method context.</summary>
    /// <param name="methodOOP">CompiledMethod object pointer.</param>
    /// <param name="receiver">Receiver object pointer.</param>
    /// <param name="args">Arguments array (OOPs).</param>
    /// <returns>New MethodContext OOP.</returns>
    SmalltalkOOP ActivateMethod(SmalltalkOOP methodOOP, SmalltalkOOP receiver, SmalltalkOOP[] args);

    /// <summary>Clears method lookup cache (primitive 89).</summary>
    void InvalidateMethodCache();
}
```

#### `/Runtime/VM/IContextManager.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.VM;

/// <summary>
/// Context (MethodContext/BlockContext) lifecycle management.
/// Internal to runtime engine.
/// </summary>
public interface IContextManager
{
    /// <summary>Creates a new method context.</summary>
    /// <param name="methodOOP">CompiledMethod object pointer.</param>
    /// <param name="receiver">Receiver object pointer.</param>
    /// <param name="stackSize">Initial stack size.</param>
    /// <returns>New MethodContext OOP.</returns>
    SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiver, int stackSize);

    /// <summary>Creates a new block context.</summary>
    /// <param name="homeContext">Home MethodContext OOP.</param>
    /// <param name="startPC">Starting program counter.</param>
    /// <param name="argCount">Number of block arguments.</param>
    /// <returns>New BlockContext OOP.</returns>
    SmalltalkOOP CreateBlockContext(SmalltalkOOP homeContext, int startPC, int argCount);

    /// <summary>Activates a context (makes it the active context).</summary>
    /// <param name="contextOOP">Context to activate.</param>
    void ActivateContext(SmalltalkOOP contextOOP);

    /// <summary>Returns from current context to sender.</summary>
    /// <param name="returnValue">Value to return.</param>
    void ReturnToSender(SmalltalkOOP returnValue);

    /// <summary>Performs non-local return to target context.</summary>
    /// <param name="targetContext">Context to unwind to.</param>
    /// <param name="returnValue">Value to return.</param>
    void NonLocalReturn(SmalltalkOOP targetContext, SmalltalkOOP returnValue);
}
```

#### `/Runtime/VM/IStackManager.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.VM;

/// <summary>
/// Context stack operations.
/// Provided to primitives via dependency injection.
/// </summary>
public interface IStackManager
{
    /// <summary>Pushes value onto active context's stack.</summary>
    /// <param name="value">Value to push.</param>
    void Push(SmalltalkOOP value);

    /// <summary>Pops value from active context's stack.</summary>
    /// <returns>Popped value.</returns>
    SmalltalkOOP Pop();

    /// <summary>Peeks at top stack value without removing.</summary>
    /// <returns>Top stack value.</returns>
    SmalltalkOOP Peek();

    /// <summary>Gets stack depth of active context.</summary>
    /// <returns>Number of items on stack.</returns>
    int GetStackDepth();

    /// <summary>Clears the stack (resets to empty).</summary>
    void Clear();
}
```

---

### Step 3: Define Primitive Interfaces (2 hours)

#### `/Runtime/Primitives/IPrimitive.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.Primitives;

/// <summary>
/// Interface for individual primitive implementations.
/// Primitives receive dependencies via constructor injection.
/// </summary>
public interface IPrimitive
{
    /// <summary>Primitive number (1-255).</summary>
    int Number { get; }

    /// <summary>Primitive name (for diagnostics).</summary>
    string Name { get; }

    /// <summary>Executes the primitive operation.</summary>
    /// <param name="context">Primitive execution context (receiver, args, dependencies).</param>
    /// <returns>Result with success flag and return value.</returns>
    PrimitiveResult Execute(PrimitiveContext context);
}
```

#### `/Runtime/Primitives/IPrimitiveRegistry.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.Primitives;

/// <summary>
/// Registry and dispatch for primitive operations.
/// Replaces static PrimitiveTable with dependency-injectable design.
/// </summary>
public interface IPrimitiveRegistry
{
    /// <summary>Registers a primitive implementation.</summary>
    /// <param name="primitive">Primitive to register.</param>
    void Register(IPrimitive primitive);

    /// <summary>Executes a primitive by number.</summary>
    /// <param name="primitiveNumber">Primitive number (1-255).</param>
    /// <param name="context">Execution context.</param>
    /// <returns>Primitive result (success + return value).</returns>
    PrimitiveResult Execute(int primitiveNumber, PrimitiveContext context);

    /// <summary>Checks if primitive is registered.</summary>
    /// <param name="primitiveNumber">Primitive number.</param>
    /// <returns>True if registered.</returns>
    bool IsRegistered(int primitiveNumber);

    /// <summary>Gets total number of registered primitives.</summary>
    int Count { get; }
}
```

#### `/Runtime/Primitives/PrimitiveContext.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.Primitives;

/// <summary>
/// Context provided to primitives via dependency injection.
/// REPLACES IPrimitiveContext (which caused circular dependency).
/// Primitives receive only what they need, not entire VM.
/// </summary>
public readonly struct PrimitiveContext
{
    /// <summary>Receiver object pointer.</summary>
    public SmalltalkOOP Receiver { get; init; }

    /// <summary>Arguments array (OOPs).</summary>
    public SmalltalkOOP[] Arguments { get; init; }

    // Injected dependencies (interfaces, not VM!)

    /// <summary>Heap manager for object allocation and access.</summary>
    public ISmalltalkHeapManager HeapManager { get; init; }

    /// <summary>Stack manager for stack operations.</summary>
    public IStackManager StackManager { get; init; }

    /// <summary>Context manager for context creation/activation.</summary>
    public IContextManager ContextManager { get; init; }

    /// <summary>Runtime heap access for class/symbol lookup.</summary>
    public IRuntimeHeapAccess RuntimeAccess { get; init; }

    /// <summary>Platform bridge for platform-specific operations (optional).</summary>
    public IPlatformBridge? Platform { get; init; }
}

/// <summary>
/// Result of primitive execution.
/// </summary>
public readonly struct PrimitiveResult
{
    /// <summary>Whether primitive succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Return value (ignored if Success=false).</summary>
    public SmalltalkOOP Value { get; init; }

    public static PrimitiveResult Succeed(SmalltalkOOP value)
        => new() { Success = true, Value = value };

    public static PrimitiveResult Fail()
        => new() { Success = false };
}
```

---

### Step 4: Define Service Interfaces (1 hour)

#### `/Runtime/Services/IRuntimeHeapAccess.cs`

```csharp
namespace Smalltalk80.Abstractions.Runtime.Services;

/// <summary>
/// High-level runtime access to Smalltalk heap data structures.
/// Provides caching, class lookup, symbol interning.
/// All methods access ONLY the Smalltalk heap, never cold-start proxy data.
/// </summary>
public interface IRuntimeHeapAccess
{
    /// <summary>Gets a class OOP by name, using cache if valid.</summary>
    /// <param name="className">Class name (e.g., "Array", "String").</param>
    /// <returns>Class OOP, or nil if not found.</returns>
    SmalltalkOOP GetClass(string className);

    /// <summary>Interns a string as a Symbol in the Smalltalk heap.</summary>
    /// <param name="symbolName">String to intern.</param>
    /// <returns>Symbol OOP if found, nil if not found.</returns>
    SmalltalkOOP InternSymbol(string symbolName);

    /// <summary>Looks up a global variable by name.</summary>
    /// <param name="dictOOP">Dictionary to search (typically Smalltalk global dict).</param>
    /// <param name="globalName">Global name to look up.</param>
    /// <returns>Global value OOP, or nil if not found.</returns>
    SmalltalkOOP LookupGlobalByName(SmalltalkOOP dictOOP, string globalName);

    /// <summary>Invalidates all runtime caches (called by primitive 89).</summary>
    void InvalidateAllCaches();

    /// <summary>Builds complete field layout map for all classes by walking heap.</summary>
    /// <returns>Dictionary of "class:ClassName" → field names.</returns>
    Dictionary<string, List<string>> BuildFieldMap();
}
```

---

### Step 5: Define Cold Start Interfaces (1 hour)

#### `/ColdStart/IColdStartLoader.cs`

```csharp
namespace Smalltalk80.Abstractions.ColdStart;

/// <summary>
/// Public API for cold start loader.
/// Builds heap from ColdStartBundle.
/// </summary>
public interface IColdStartLoader
{
    /// <summary>Executes all cold start phases (1-6).</summary>
    void Run();

    /// <summary>Builds VM metadata for runtime (call after Run).</summary>
    /// <returns>VM metadata structure.</returns>
    VMMetadata BuildVMMetadata();

    /// <summary>Gets the layout resolver (available after Run).</summary>
    ILayoutResolver LayoutResolver { get; }

    /// <summary>Gets execution report with metrics and timing.</summary>
    ColdStartReport GetReport();

    /// <summary>Gets localId to OOP mapping (available after Run).</summary>
    IReadOnlyDictionary<string, SmalltalkOOP> LocalIdToObjectId { get; }
}
```

#### `/ColdStart/IPhase.cs`

```csharp
namespace Smalltalk80.Abstractions.ColdStart;

/// <summary>
/// Common interface for cold start phases.
/// Enables testability, composability, and validation.
/// </summary>
public interface IPhase
{
    /// <summary>Phase name for diagnostics.</summary>
    string Name { get; }

    /// <summary>Phase number (1-6).</summary>
    int Number { get; }

    /// <summary>Executes the phase.</summary>
    void Run();

    /// <summary>Validates phase preconditions (call before Run).</summary>
    /// <exception cref="InvalidOperationException">Preconditions not met.</exception>
    void ValidatePreconditions();

    /// <summary>Validates phase postconditions (call after Run).</summary>
    /// <exception cref="InvalidOperationException">Postconditions not met.</exception>
    void ValidatePostconditions();
}
```

---

### Step 6: Create Test Infrastructure (2 hours)

#### Create Test Projects

```bash
cd /home/user/Smalltalk-80

# Unit test projects (one per subsystem)
dotnet new xunit -n Smalltalk80.Runtime.Tests -f net9.0
dotnet new xunit -n Smalltalk80.Primitives.Tests -f net9.0
dotnet new xunit -n Smalltalk80.ColdStart.Tests -f net9.0
dotnet new xunit -n Smalltalk80.Compiler.Tests.New -f net9.0

# Add to solution
dotnet sln add Smalltalk80.Runtime.Tests/Smalltalk80.Runtime.Tests.csproj
dotnet sln add Smalltalk80.Primitives.Tests/Smalltalk80.Primitives.Tests.csproj
dotnet sln add Smalltalk80.ColdStart.Tests/Smalltalk80.ColdStart.Tests.csproj
dotnet sln add Smalltalk80.Compiler.Tests.New/Smalltalk80.Compiler.Tests.New.csproj

# Add project references
cd Smalltalk80.Runtime.Tests
dotnet add reference ../Smalltalk80.Abstractions/Smalltalk80.Abstractions.csproj
dotnet add reference ../Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj
```

#### Example Unit Test (Primitive)

```csharp
// Smalltalk80.Primitives.Tests/ArithmeticPrimitiveTests.cs

using Xunit;
using Smalltalk80.Abstractions.Runtime.Primitives;

namespace Smalltalk80.Primitives.Tests;

public class ArithmeticPrimitiveTests
{
    [Fact]
    public void AddPrimitive_TwoSmallIntegers_ReturnsSum()
    {
        // Arrange
        var primitive = new AddPrimitive();
        var context = CreateContext(
            receiver: SmallInteger(5),
            arguments: [SmallInteger(3)]
        );

        // Act
        var result = primitive.Execute(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(SmallInteger(8), result.Value);
    }

    [Fact]
    public void AddPrimitive_Overflow_Fails()
    {
        // Arrange
        var primitive = new AddPrimitive();
        var context = CreateContext(
            receiver: SmallInteger(MaxSmallInteger),
            arguments: [SmallInteger(1)]
        );

        // Act
        var result = primitive.Execute(context);

        // Assert
        Assert.False(result.Success); // Should fail, fallback to LargeInteger
    }

    private PrimitiveContext CreateContext(SmalltalkOOP receiver, SmalltalkOOP[] arguments)
    {
        // Mock dependencies
        var heapManager = new MockHeapManager();
        var stackManager = new MockStackManager();
        var contextManager = new MockContextManager();
        var runtimeAccess = new MockRuntimeHeapAccess();

        return new PrimitiveContext
        {
            Receiver = receiver,
            Arguments = arguments,
            HeapManager = heapManager,
            StackManager = stackManager,
            ContextManager = contextManager,
            RuntimeAccess = runtimeAccess
        };
    }
}
```

---

### Step 7: Update CLAUDE.md (1 hour)

Add new section to `/CLAUDE.md`:

```markdown
## Architectural Refactoring (In Progress)

**Status**: Phase 0 - Foundation (Week 1)
**Goal**: Transform monolithic architecture into clean, testable components

### Current Phase: Foundation

**Deliverables**:
- ✅ `/Smalltalk80.Abstractions/` assembly created
- ✅ All interface definitions with XML documentation
- ✅ Unit test projects for each subsystem
- ✅ Integration test harness

**Validation**:
- ✅ All existing tests pass (194 BlockContext tests)
- ✅ No behavior changes

### Architecture Overview

See `/docs/ARCHITECTURE_ANALYSIS_AND_REFACTORING_PLAN.md` for complete plan.

**Key Interfaces**:
- `IVirtualMachine` - Public VM API
- `IPrimitive` / `IPrimitiveRegistry` - Primitive system
- `IColdStartLoader` / `IPhase` - Cold start system
- `IRuntimeHeapAccess` - Runtime heap services

**Breaking Changes**:
1. VM ↔ Primitives circular dependency eliminated
2. VirtualMachine.cs decomposed into 7 components
3. All subsystems behind formal interfaces

**Next Phase**: Phase 1 - Primitive System Refactoring (Week 2-3)
```

---

### Step 8: Validation (30 minutes)

```bash
# Build everything
cd /home/user/Smalltalk-80
dotnet build

# Run existing tests (MUST PASS)
cd Smalltalk80.Interpreter.Tests
dotnet test

# Run BlockContext tests (MUST PASS 194/194)
cd ../Smalltalk80.Compiler.Tests
dotnet test --filter "BlockContext"

# Run interpreter with init expressions (MUST SUCCEED 31/46)
cd ../Smalltalk80.Interpreter
dotnet run
```

**Success Criteria**:
- ✅ Solution builds with no errors
- ✅ All existing tests pass
- ✅ BlockContext tests: 194/194 pass
- ✅ Init expressions: 31/46 succeed (baseline)
- ✅ No behavior changes

---

## Completion Checklist

### Foundation Phase Complete When:

- [ ] All interfaces defined in `/Smalltalk80.Abstractions/`
- [ ] XML documentation on all public APIs
- [ ] Test projects created and configured
- [ ] Example unit tests written (at least 1 per subsystem)
- [ ] CLAUDE.md updated with refactoring status
- [ ] All existing tests pass
- [ ] No behavior changes (interpreter works as before)

### Ready for Phase 1 When:

- [ ] Foundation phase validated
- [ ] Team reviewed and approved interface definitions
- [ ] Test infrastructure proven to work
- [ ] Baseline metrics captured (performance, test coverage)

---

## Quick Reference

### Key Files Created

```
/Smalltalk80.Abstractions/
├─ Runtime/
│  ├─ VM/
│  │  ├─ IVirtualMachine.cs
│  │  ├─ IBytecodeInterpreter.cs
│  │  ├─ IMessageDispatcher.cs
│  │  ├─ IContextManager.cs
│  │  └─ IStackManager.cs
│  ├─ Primitives/
│  │  ├─ IPrimitive.cs
│  │  ├─ IPrimitiveRegistry.cs
│  │  └─ PrimitiveContext.cs
│  └─ Services/
│     └─ IRuntimeHeapAccess.cs
├─ ColdStart/
│  ├─ IColdStartLoader.cs
│  └─ IPhase.cs
└─ Foundation/
   └─ (ISmalltalkHeapManager already exists)

/Smalltalk80.Runtime.Tests/
/Smalltalk80.Primitives.Tests/
/Smalltalk80.ColdStart.Tests/
/Smalltalk80.Compiler.Tests.New/

/docs/
├─ ARCHITECTURE_ANALYSIS_AND_REFACTORING_PLAN.md ✅
├─ ARCHITECTURE_VISUAL_SUMMARY.md ✅
└─ REFACTORING_QUICK_START.md ✅ (this file)
```

### Commands Cheat Sheet

```bash
# Build everything
dotnet build

# Run all tests
dotnet test

# Run specific test project
cd Smalltalk80.Runtime.Tests && dotnet test

# Run interpreter
cd Smalltalk80.Interpreter && dotnet run

# Run BlockContext regression test
cd Smalltalk80.Interpreter && dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

---

## Next Steps

After Phase 0 completion:

1. **Review interfaces** with team/stakeholders
2. **Adjust based on feedback** (expect 1-2 iterations)
3. **Approve Phase 0 deliverables**
4. **Begin Phase 1**: Primitive System Refactoring
   - Break VM ↔ Primitives circular dependency
   - Migrate all 100+ primitives to `IPrimitive`
   - Implement `PrimitiveRegistry`

---

**Status**: ✅ Ready to Begin
**Estimated Time**: 1 week (20 hours)
**Risk**: Low 🟢 (no behavior changes, interface definitions only)
