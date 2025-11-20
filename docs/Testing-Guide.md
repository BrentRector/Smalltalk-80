# Testing Guide

## Test Organization

Tests are organized into functional categories using xUnit's `[Trait]` attribute. This allows you to run specific test groups independently.

### Test Categories

| Category | Test Count | Description |
|----------|-----------|-------------|
| **Primitives** | 258 | All primitive operation tests (arithmetic, comparison, control, strings, floats, etc.) |
| **HeapManager** | TBD | Heap management, object allocation, GC tests |
| **VM** | TBD | Virtual machine core: message dispatch, execution state |
| **Context** | TBD | Context management and execution context tests |
| **Compiler** | TBD | Bytecode compiler tests: emitter, variable resolution, temp frames |

## Running Tests by Category

### Run All Tests
```bash
cd Smalltalk80.Interpreter.Tests
dotnet test
```

### Run Only Primitive Tests
```bash
dotnet test --filter "Category=Primitives"
```

### Run Only HeapManager Tests
```bash
dotnet test --filter "Category=HeapManager"
```

### Run Only VM Tests
```bash
dotnet test --filter "Category=VM"
```

### Run Only Context Tests
```bash
dotnet test --filter "Category=Context"
```

### Run Only Compiler Tests
```bash
dotnet test --filter "Category=Compiler"
```

### Run Multiple Categories
```bash
# Run Primitives AND HeapManager tests
dotnet test --filter "Category=Primitives|Category=HeapManager"
```

### Exclude Categories
```bash
# Run everything EXCEPT Primitives
dotnet test --filter "Category!=Primitives"
```

### Run With Verbose Output
```bash
# See detailed test execution
dotnet test --filter "Category=Primitives" --logger "console;verbosity=detailed"
```

## Adding New Test Categories

To add a new test category, add the `[Trait]` attribute to your test class:

```csharp
namespace Smalltalk80.Interpreter.Tests.YourNamespace;

/// <summary>
/// Your test documentation
/// </summary>
[Trait("Category", "YourCategory")]
public class YourTestClass
{
    [Fact]
    public void YourTest()
    {
        // Test implementation
    }
}
```

## Test Development Workflow

### 1. Working on Primitives
```bash
# Run primitive tests frequently during development
dotnet test --filter "Category=Primitives"
```

### 2. Working on HeapManager
```bash
# Enable and run heap manager tests
dotnet test --filter "Category=HeapManager"
```

### 3. Before Committing
```bash
# Run all tests to ensure no regressions
dotnet test
```

## Current Test Status

### Primitives: ✅ 258/258 Passing (100%)
- SmallInteger arithmetic (Add, Subtract, Multiply, Divide, etc.)
- Comparison operations (LessThan, GreaterThan, Equal, etc.)
- Bitwise operations (BitAnd, BitOr, BitXor, BitShift)
- Float operations (Add, Subtract, Multiply, Divide, Truncate, etc.)
- String operations (At, AtPut, Size, Hash, AsSymbol, etc.)
- Large integer operations
- Object operations (Class, Size, Hash, Become, etc.)
- Control flow (BlockCopy, BlockValue, Signal, Wait, Resume, Suspend)
- System operations (Snapshot, Quit, etc.)

### HeapManager: ⏸️ Ready to Enable
Tests exist and are ready to be enabled and run independently.

### Other Categories: ⏸️ Ready to Enable
VM, Context, and Compiler tests are categorized and ready for selective execution.

## Continuous Integration

When setting up CI/CD, you can run test categories in parallel:

```yaml
jobs:
  test-primitives:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter "Category=Primitives"

  test-heapmanager:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter "Category=HeapManager"

  test-vm:
    runs-on: ubuntu-latest
    steps:
      - run: dotnet test --filter "Category=VM"
```

## Troubleshooting

### No tests found for filter
If you see "No tests matched the filter" ensure:
1. The test class has `[Trait("Category", "YourCategory")]` attribute
2. The category name is spelled correctly (case-sensitive)
3. You've rebuilt the test project: `dotnet build`

### Tests run but category filter ignored
Make sure you're using the correct filter syntax:
- Correct: `--filter "Category=Primitives"`
- Incorrect: `--filter Category=Primitives` (missing quotes)
