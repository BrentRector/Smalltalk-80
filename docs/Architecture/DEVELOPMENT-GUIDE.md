# Development Guide

**Last Updated**: 2025-11-18

This guide explains how to extend the Smalltalk-80 interpreter with new primitives, cold start phases, and VM components.

---

## Adding a New Primitive

### Step 1: Create the Primitive Class

Create a file in `Core/VM/Primitives/`:

```csharp
// Core/VM/Primitives/MyPrimitives.cs
namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// My custom primitive operations.
/// </summary>
public class MyNewPrimitive : IPrimitive
{
    public int Number => 200;  // Unique primitive number
    public string Name => "myOperation";

    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        // Get receiver and arguments
        var receiver = context.Receiver;
        var arg = context.Arguments[0];

        // Access heap
        var heap = context.HeapManager;

        // Validate types
        if (!receiver.IsSmallInteger || !arg.IsSmallInteger)
            return PrimitiveResult.Fail();

        // Perform operation
        int result = receiver.AsSmallInteger() + arg.AsSmallInteger();

        // Return success
        return PrimitiveResult.Succeed(SmalltalkOOP.FromSmallInteger(result));
    }
}
```

### Step 2: Register the Primitive

Add to `PrimitiveRegistry.RegisterAllPrimitives()`:

```csharp
// In PrimitiveRegistry.cs
private void RegisterAllPrimitives()
{
    // ... existing registrations ...

    // My new primitive
    Register(new MyNewPrimitive());
}
```

### Step 3: Add Tests

Create tests in `Tests/Primitives/`:

```csharp
// Tests/Primitives/MyPrimitivesTests.cs
public class MyPrimitivesTests
{
    [Fact]
    public void MyNewPrimitive_ValidInputs_ReturnsSum()
    {
        // Arrange
        var primitive = new MyNewPrimitive();
        var context = CreateContext(
            receiver: SmalltalkOOP.FromSmallInteger(5),
            arguments: [SmalltalkOOP.FromSmallInteger(3)]
        );

        // Act
        var result = primitive.Execute(context);

        // Assert
        Assert.True(result.Success);
        Assert.Equal(8, result.Value.AsSmallInteger());
    }

    [Fact]
    public void MyNewPrimitive_InvalidType_Fails()
    {
        // Test primitive failure case
    }
}
```

### Primitive Guidelines

1. **Return PrimitiveResult.Fail()** for type mismatches or invalid inputs
2. **Use context.HeapManager** for all heap access
3. **Avoid side effects** unless necessary (e.g., I/O primitives)
4. **Document primitive number** - must be unique
5. **Handle overflow** - return Fail() to fall through to Smalltalk code

---

## Adding a Cold Start Phase

### Step 1: Create the Phase Class

Create a file in `Engine/LoaderPhases/`:

```csharp
// Engine/LoaderPhases/Phase7_MyPhase.cs
namespace Smalltalk80.Interpreter.Engine.LoaderPhases;

/// <summary>
/// Phase 7: My custom initialization phase.
/// </summary>
public class Phase7_MyPhase : IPhase
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly IDiagnosticHub? _diagnostics;

    public string Name => "My Custom Phase";
    public int PhaseNumber => 7;

    public Phase7_MyPhase(
        ColdStartBundle bundle,
        ISmalltalkHeapManager heapManager,
        IDiagnosticHub? diagnostics = null)
    {
        _bundle = bundle;
        _heapManager = heapManager;
        _diagnostics = diagnostics;
    }

    public void ValidatePreconditions()
    {
        // Check that required state exists
        if (_bundle.Classes == null)
            throw new InvalidOperationException("Classes not loaded");
    }

    public void Run()
    {
        _diagnostics?.Emit(new PhaseStarted(PhaseNumber, Name));

        // Phase implementation here

        _diagnostics?.Emit(new PhaseCompleted(PhaseNumber, Name));
    }

    public void ValidatePostconditions()
    {
        // Verify phase completed correctly
    }
}
```

### Step 2: Add to Orchestrator

Update `ColdStartOrchestrator`:

```csharp
// In ColdStartOrchestrator.cs
private void BuildPhases()
{
    _phases = new IPhase[]
    {
        new Phase1_PreflightAndSymbols(...),
        new Phase2_ClassInfrastructure(...),
        new Phase3_ObjectAllocation(...),
        new Phase4_MethodCompilation(...),
        new Phase5Orchestrator(...),
        new Phase6_ValidationAndGC(...),
        new Phase7_MyPhase(_bundle, _heapManager, _diagnostics),  // Add here
    };
}
```

### Phase Guidelines

1. **Implement IPhase** - Name, PhaseNumber, Run, ValidatePreconditions, ValidatePostconditions
2. **Emit diagnostics** - Use IDiagnosticHub for progress events
3. **Validate preconditions** - Check required state before running
4. **Validate postconditions** - Verify phase completed correctly
5. **Document dependencies** - What must exist before this phase runs

---

## Adding a VM Component

### Step 1: Define the Interface

Create in `Core/VM/`:

```csharp
// Core/VM/IMyComponent.cs
namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides my custom VM functionality.
/// </summary>
public interface IMyComponent
{
    /// <summary>
    /// Performs the operation.
    /// </summary>
    /// <param name="input">Input value.</param>
    /// <returns>Result value.</returns>
    SmalltalkOOP DoSomething(SmalltalkOOP input);
}
```

### Step 2: Create the Implementation

```csharp
// Core/VM/MyComponent.cs
namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Implementation of my custom VM functionality.
/// </summary>
public class MyComponent : IMyComponent
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VMMetadata _metadata;

    public MyComponent(
        ISmalltalkHeapManager heapManager,
        VMMetadata metadata)
    {
        _heapManager = heapManager;
        _metadata = metadata;
    }

    public SmalltalkOOP DoSomething(SmalltalkOOP input)
    {
        // Implementation
        return input;
    }
}
```

### Step 3: Inject into VirtualMachine

Update `VirtualMachine` constructor:

```csharp
// In VirtualMachine.cs
private readonly IMyComponent _myComponent;

public VirtualMachine(
    ISmalltalkHeapManager heapManager,
    // ... other dependencies ...
    IMyComponent myComponent)
{
    // ... existing initialization ...
    _myComponent = myComponent;
}
```

### Step 4: Add Tests

```csharp
// Tests/MyComponentTests.cs
public class MyComponentTests
{
    [Fact]
    public void DoSomething_ValidInput_ReturnsExpected()
    {
        // Arrange
        var heapManager = new MockHeapManager();
        var metadata = CreateTestMetadata();
        var component = new MyComponent(heapManager, metadata);

        // Act
        var result = component.DoSomething(input);

        // Assert
        Assert.Equal(expected, result);
    }
}
```

---

## Naming Conventions

### Component Suffixes

| Suffix | Usage | Example |
|--------|-------|---------|
| Manager | Owns stateful resources | HeapManager |
| Orchestrator | Complex multi-step workflows | ColdStartOrchestrator |
| Executor | Executes operations | InitExpressionExecutor |
| Resolver | Looks up/resolves items | LayoutResolver |
| Registry | Registration tables | SymbolRegistry |
| Accessor | Provides data access | RuntimeHeapAccessor |
| Builder | Constructs objects | USTableBuilder |
| Handler | Handles events/operations | BecomeHandler |

### File Naming

- **One public type per file** (except nested helpers)
- **File name matches type name**: `StackManager.cs` contains `class StackManager`
- **Interfaces**: `IStackManager.cs` contains `interface IStackManager`

### Namespace Conventions

Namespaces match directory structure:

```csharp
// File location determines namespace
Core/VM/StackManager.cs → Smalltalk80.Interpreter.Core.VM
Engine/LoaderPhases/Phase5/USTableBuilder.cs → Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5
```

### Anti-Patterns to Avoid

- **Generic names**: Manager.cs, Helper.cs, Util.cs
- **Abbreviations**: Mgr, Svc, Exec
- **Verbs for classes**: Access, Load, Process (use Accessor, Loader, Processor)
- **Multiple public types per file**

---

## Testing Requirements

### Regression Test (MANDATORY)

After ANY change to compiler or interpreter, run:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected**: Returns 1191 (120 tests pass)

### Build Validation

```bash
dotnet build
# Must be: 0 errors, 0 warnings
```

### Init Expression Test

```bash
dotnet run
# Expected: 31/46 init expressions succeed
```

### Critical Files Requiring Test

Any changes to these files MUST pass the regression test:

- `BytecodeCompiler.cs` - bytecode generation
- `VariableResolver.cs` - variable resolution
- `TempFrameAllocator.cs` - variable allocation
- `VirtualMachine.cs` - execution
- `ControlPrimitives.cs` - BlockCopy, BlockValue

### Writing Good Tests

1. **Test happy path** - Normal operation
2. **Test failure cases** - Invalid inputs, edge cases
3. **Test boundary conditions** - Zero, max values, empty collections
4. **Use meaningful names** - `Method_Scenario_ExpectedResult`

---

## Cold Start vs Runtime

### Cold Start Phase (Phases 1-6)

- Uses registries (GlobalRegistry, SymbolRegistry)
- Builds heap from ColdStartBundle
- Can use `[ColdStartOnly]` attribute

```csharp
[ColdStartOnly]
public void BuildSymbolTable()
{
    // Uses SymbolRegistry
    SymbolRegistry.Add(name, oop);
}
```

### Runtime Phase (Init Expressions Onward)

- Uses only heap and LayoutResolver
- Registries are frozen (throw on modification)
- Can use `[RuntimeOnly]` attribute

```csharp
[RuntimeOnly]
public SmalltalkOOP LookupSymbol(string name)
{
    // Must use heap-based lookup, not registry
    return _runtimeHeapAccess.InternSymbol(name);
}
```

### Freeze Pattern

After cold start, registries are frozen:

```csharp
// This is done in Phase 5
GlobalRegistry.Freeze();
SymbolRegistry.Freeze();
AssociationRegistry.Freeze();

// Runtime access throws
GlobalRegistry.Add(...);  // InvalidOperationException!
```

---

## Diagnostic Events

### Emitting Events

```csharp
public void MyMethod()
{
    _diagnostics?.Emit(new MyCustomEvent(
        timestamp: DateTime.UtcNow,
        message: "Something happened"
    ));
}
```

### Creating Custom Events

```csharp
// Core/Diagnostics/DiagnosticEvents.cs
public record MyCustomEvent(
    DateTime Timestamp,
    string Message
) : DiagnosticEvent;
```

### Creating a Listener

```csharp
// Core/Diagnostics/Listeners/MyListener.cs
public class MyListener : IDiagnosticListener
{
    public bool IsEnabled(DiagnosticEventType eventType)
        => eventType.HasFlag(DiagnosticEventType.MyCategory);

    public void OnEvent<T>(T @event) where T : DiagnosticEvent
    {
        if (@event is MyCustomEvent e)
        {
            Console.WriteLine($"[{e.Timestamp}] {e.Message}");
        }
    }
}
```

---

## Common Patterns

### Dependency Injection

Components receive dependencies via constructor:

```csharp
public class MyComponent(
    ISmalltalkHeapManager heapManager,
    VMMetadata metadata,
    IMyDependency dependency)
{
    // Use primary constructor parameters
}
```

### Result Types

Use result types for fallible operations:

```csharp
public record OperationResult(
    bool Success,
    SmalltalkOOP Value,
    string? ErrorMessage = null
)
{
    public static OperationResult Succeed(SmalltalkOOP value)
        => new(true, value);

    public static OperationResult Fail(string message)
        => new(false, default, message);
}
```

### Nullable Dependencies

Optional dependencies use nullable types:

```csharp
public MyComponent(
    ISmalltalkHeapManager heapManager,
    IDiagnosticHub? diagnostics = null)  // Optional
{
    _diagnostics = diagnostics;
}

// Usage with null-conditional
_diagnostics?.Emit(new MyEvent());
```

---

## Related Documents

- [PHYSICAL-ARCHITECTURE.md](PHYSICAL-ARCHITECTURE.md) - File system organization
- [IMPLEMENTATION-ARCHITECTURE.md](IMPLEMENTATION-ARCHITECTURE.md) - How the interpreter works
