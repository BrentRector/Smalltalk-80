# Testing Guide

## Test Organization

Tests are organized into functional categories using xUnit's `[Trait]` attribute. This allows you to run specific test groups independently.

### Test Categories

| Category | Test Count | Description |
|----------|-----------|-------------|
| **Primitives** | 258 | All primitive operation tests (arithmetic, comparison, control, strings, floats, etc.) |
| **HeapManager** | 122 | Heap management, object allocation, GC tests |
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

### HeapManager: ✅ 122/122 Passing (100%)
- Object allocation (Pointer, PointerIndexed, ByteIndexed, WordIndexed formats)
- Object table management (allocation, validation, enumeration)
- Garbage collection (mark, sweep, compaction)
- Object field access (get/set by index and name)
- Class operations (class retrieval, class name resolution)
- Become operations (object identity swapping)
- Memory statistics and monitoring
- SmalltalkOOP operations (tagging, conversion, equality)
- ObjectTableEntry typed accessors (byte/OOP/word arrays)
- Free list management and object table reuse

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

## Code Coverage Analysis

### Verifying Primitive Coverage

To confirm that all ~200 primitive functions are actually tested and return correct values, use these tools:

#### Option 1: Quick Primitive Coverage Analysis (Recommended)

Use the provided PowerShell script for quick analysis:

```bash
# Basic coverage summary
pwsh scripts/analyze-primitive-coverage.ps1

# Show detailed breakdown
pwsh scripts/analyze-primitive-coverage.ps1 -Detailed

# Show which primitives are untested
pwsh scripts/analyze-primitive-coverage.ps1 -ShowUntested
```

This will show:
- Number of primitives implemented vs tested per category
- Overall coverage percentage
- List of untested primitives (if any)

**For Linux/Mac users, use the Bash version:**

```bash
# Basic coverage summary
bash scripts/analyze-primitive-coverage.sh

# Show detailed breakdown
bash scripts/analyze-primitive-coverage.sh --detailed

# Show which primitives are untested
bash scripts/analyze-primitive-coverage.sh --show-untested
```

#### Quick HeapManager Coverage Analysis

Use the provided HeapManager coverage scripts for quick analysis:

```bash
# Basic coverage summary (PowerShell)
pwsh scripts/analyze-heapmanager-coverage.ps1

# Show detailed breakdown
pwsh scripts/analyze-heapmanager-coverage.ps1 -Detailed

# Show which methods are untested
pwsh scripts/analyze-heapmanager-coverage.ps1 -ShowUntested
```

**For Linux/Mac users:**

```bash
# Basic coverage summary (Bash)
bash scripts/analyze-heapmanager-coverage.sh

# Show detailed breakdown
bash scripts/analyze-heapmanager-coverage.sh --detailed

# Show which methods are untested
bash scripts/analyze-heapmanager-coverage.sh --show-untested
```

This will show:
- Number of methods implemented vs tested per component
- Overall method coverage percentage
- List of untested methods (if any)
- Component breakdown (SmalltalkHeapManager, ObjectTableManager, GarbageCollector, etc.)

#### Option 2: .NET Code Coverage (Detailed)

For line-by-line code coverage, install Coverlet:

```bash
# Install Coverlet (one-time setup)
cd Smalltalk80.Interpreter.Tests
dotnet add package coverlet.collector
```

Then run tests with coverage collection:

```bash
# Generate coverage for all tests
dotnet test --collect:"XPlat Code Coverage"

# Generate coverage for primitives only
dotnet test --filter "Category=Primitives" --collect:"XPlat Code Coverage"
```

Coverage reports are saved to:
```
Smalltalk80.Interpreter.Tests/TestResults/{guid}/coverage.cobertura.xml
```

#### Option 3: Generate HTML Coverage Report

For a visual HTML report, use ReportGenerator:

```bash
# Install ReportGenerator (one-time, global tool)
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html

# Open the report (Windows)
start coverage-report/index.html

# Open the report (macOS)
open coverage-report/index.html

# Open the report (Linux)
xdg-open coverage-report/index.html
```

The HTML report shows:
- ✅ Green: Lines that were executed by tests
- ❌ Red: Lines that were never executed
- Percentage coverage for each file, class, and method
- Branch coverage (which if/else paths were taken)

### Understanding Coverage Results

**Good Coverage Indicators:**
- ✅ All primitive `Execute()` methods should show green
- ✅ Both success and failure paths should be tested
- ✅ Edge cases (null, overflow, type errors) should be covered

**Coverage Goals:**
- **Primitives**: Aim for 100% (all 258 primitives fully tested)
- **VM Core**: Aim for 90%+ (critical execution paths)
- **HeapManager**: Aim for 95%+ (memory safety critical)
- **Compiler**: Aim for 85%+ (many edge cases)

### Verifying Specific Primitives

To verify a specific primitive is tested and returns correct values:

1. **Find the primitive implementation:**
   ```bash
   # Example: Find the Add primitive
   grep -r "class Add.*IPrimitive" Smalltalk80.Interpreter/Core/VM/Primitives/
   ```

2. **Find its tests:**
   ```bash
   # Example: Find Add primitive tests
   grep -r "Add_.*Returns" Smalltalk80.Interpreter.Tests/Primitives/
   ```

3. **Run the specific test:**
   ```bash
   # Run tests matching "Add"
   dotnet test --filter "FullyQualifiedName~Add"
   ```

### Example Coverage Analysis Output

```
===== Smalltalk-80 Primitive Coverage Analysis =====

Coverage by Category:

  ✓ Arithmetic: 34/34 primitives (100.0% coverage)
  ✓ Comparison: 8/8 primitives (100.0% coverage)
  ✓ Control: 12/12 primitives (100.0% coverage)
  ✓ Float: 42/42 primitives (100.0% coverage)
  ✓ LargeInteger: 15/15 primitives (100.0% coverage)
  ✓ Object: 28/28 primitives (100.0% coverage)
  ✓ String: 8/8 primitives (100.0% coverage)
  ✓ System: 6/6 primitives (100.0% coverage)

===== Summary =====

  Total Primitives Implemented: 153
  Total Test Methods: 258
  Overall Test Coverage: 100.0%
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

### Coverage shows untested lines in tested methods
This usually means:
- Some error paths aren't tested (e.g., null checks, type validation)
- Edge cases are missing (overflow, underflow, boundary conditions)
- Add tests for these scenarios to achieve 100% coverage
