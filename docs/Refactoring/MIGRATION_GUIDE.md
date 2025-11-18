# Migration Guide for Future Changes

**Last Updated**: 2025-11-18

## Overview

This guide documents patterns and practices for making changes to the refactored Smalltalk-80 interpreter. Follow these guidelines to maintain architectural integrity.

---

## 1. Adding New Primitives

### Pattern: IPrimitive Interface

New primitives should implement the `IPrimitive` interface:

```csharp
public class MyNewPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext ctx)
    {
        // Get receiver and arguments
        var receiver = ctx.Receiver;
        var args = ctx.Arguments;

        // Access heap through context
        var heapManager = ctx.HeapManager;

        // Return result
        return PrimitiveResult.Success(resultOOP);
        // or
        return PrimitiveResult.Failure();
    }
}
```

### Registration

Register in `PrimitiveRegistry.CreateWithNewPrimitives()`:

```csharp
registry.Register(999, new MyNewPrimitive());
```

### Context Switch Detection

If your primitive changes the active context (like block primitives), the registry's `ExecuteLegacyExtended` will detect it automatically.

---

## 2. Adding New Cold Start Phases

### Pattern: IPhase Interface

New phases should implement `IPhase`:

```csharp
public class Phase7_NewFeature : IPhase
{
    public string Name => "New Feature";
    public int PhaseNumber => 7;

    public void Run()
    {
        // Phase implementation
    }

    public IReadOnlyList<string> ValidatePreconditions()
    {
        var failures = new List<string>();
        // Check prerequisites
        return failures;
    }

    public IReadOnlyList<string> ValidatePostconditions()
    {
        var failures = new List<string>();
        // Verify phase completed correctly
        return failures;
    }
}
```

### Integration

Add to `ColdStartOrchestrator.Run()`:

```csharp
var phase7 = new Phase7_NewFeature(...);
_phases.Add(phase7);
RunPhase(phase7.Name, () => { phase7.Run(); });
```

---

## 3. Extending Runtime Services

### Pattern: IRuntimeHeapAccessor

If you need new heap access methods, add to the interface and implementation:

```csharp
// In IRuntimeHeapAccessor.cs
public interface IRuntimeHeapAccessor
{
    // Existing methods...

    // New method
    SmalltalkOOP NewMethod(SmalltalkOOP arg);
}

// In RuntimeHeapAccessor.cs
public SmalltalkOOP NewMethod(SmalltalkOOP arg)
{
    // Implementation that accesses ONLY the heap
    // Never use GlobalRegistry or other cold-start structures
}
```

### Cache Considerations

If your method should be cached:
1. Add cache field to `RuntimeHeapAccessor` or `RuntimeClassCache`
2. Clear it in `InvalidateAllCaches()`

---

## 4. Cold Start vs Runtime Rules

### Critical Rule: Never Mix

- **Cold Start Code** uses: `GlobalRegistry`, `SymbolRegistry`, `AssociationRegistry`
- **Runtime Code** uses: `RuntimeHeapAccessor`, `SpecialObjectsArray`

### Enforcement

The Freeze() pattern prevents accidental mixing:

```csharp
// After cold start completes
GlobalRegistry.Freeze();
SymbolRegistry.Freeze();

// Any subsequent Register() call throws:
// InvalidOperationException: "GlobalRegistry.Register() called after freeze..."
```

### Attributes (Optional)

Consider marking methods with `[ColdStartOnly]` or `[RuntimeOnly]` attributes for documentation.

---

## 5. Modifying the Compiler

### Pattern: IBytecodeCompiler

The compiler interface is minimal - most changes are internal:

```csharp
public interface IBytecodeCompiler
{
    CompiledMethodData CompileExpressionFromTree(...);
    CompiledMethodData CompileMethodFromTree(...);
}
```

### Adding Compiler Features

1. Add new analysis pass in `Analysis/` directory
2. Integrate in `BytecodeCompiler.CompileMethodFromTree()`
3. Add tests to verify bytecode output

### Extracted Classes

Use the extracted helper classes:
- `CompilationEnvironment` - Scope and variable tracking
- `BytecodeEmitter` - Bytecode generation
- `VariableResolver` - Variable resolution

---

## 6. Testing New Code

### Unit Tests

Create tests in `Tests/Interfaces/` for new interfaces:

```csharp
public class MyNewInterfaceTests
{
    public static void TestFeature()
    {
        // Test implementation
    }

    public static int RunAllTests()
    {
        // Run all tests, return failure count
    }
}
```

### Integration Tests

For subsystem interactions, add to `Tests/Integration/SubsystemIntegrationTests.cs`.

### BlockContext Regression Test

**ALWAYS** run after changes to compiler or VM:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
# Expected: 1191
```

---

## 7. Performance Considerations

### Hot Paths

The following are performance-critical:
- `VirtualMachine.ExecuteBytecodeStep()` - Main interpreter loop
- `MessageDispatcher.LookupMethod()` - Method lookup
- `PrimitiveRegistry.Execute()` - Primitive dispatch

Avoid adding indirection to these paths.

### Caching

Use lazy caching with invalidation:

```csharp
private SmalltalkOOP? _cachedValue;

public SmalltalkOOP GetValue()
{
    return _cachedValue ??= ComputeExpensiveValue();
}

public void InvalidateCache()
{
    _cachedValue = null;
}
```

---

## 8. Documentation Updates

When making significant changes:

1. **Update CLAUDE.md** - Project status and recent work
2. **Update ARCHITECTURE_ANALYSIS_AND_REFACTORING_PLAN.md** - If adding new phases
3. **Update ARCHITECTURE_DIAGRAMS.md** - If changing system structure

---

## 9. Common Patterns

### Dependency Injection

Primitives receive dependencies via `PrimitiveExecutionContext`:

```csharp
public PrimitiveResult Execute(PrimitiveExecutionContext ctx)
{
    var heap = ctx.HeapManager;
    var runtime = ctx.RuntimeHeapAccessor;
    var cache = ctx.CacheManager;
    // ...
}
```

### Interface Segregation

Keep interfaces focused:
- `IRuntimeHeapAccessor` - Heap access only
- `ICacheManager` - Cache operations only
- `IPrimitiveMessageSender` - Message sending only

### Result Types

Use typed results instead of exceptions for expected failures:

```csharp
public PrimitiveResult Execute(...)
{
    if (condition)
        return PrimitiveResult.Success(value);
    else
        return PrimitiveResult.Failure();
}
```

---

## 10. Checklist for Changes

Before committing:

- [ ] New code follows established patterns
- [ ] Interfaces defined for testability
- [ ] Cold start/runtime separation maintained
- [ ] BlockContext tests pass (returns 1191)
- [ ] Build has 0 errors, 0 warnings
- [ ] Documentation updated
- [ ] Commit message describes "why" not "what"

---

## Summary

The refactored architecture emphasizes:

1. **Clear Interfaces** - Define contracts before implementation
2. **Separation of Concerns** - Cold start vs runtime, subsystem boundaries
3. **Testability** - Interfaces enable mocking and unit testing
4. **Performance** - Avoid indirection on hot paths, use caching
5. **Documentation** - Keep CLAUDE.md and docs current

Following these patterns ensures the codebase remains maintainable as it evolves.
