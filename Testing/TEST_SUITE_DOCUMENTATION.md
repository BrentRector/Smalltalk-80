# Standard Library Test Suite Documentation

## Overview

This comprehensive test suite validates every method in every class of the Smalltalk-80 standard library. The tests are organized by class hierarchy and cover all public methods with expected behavior verification.

## Test Organization

### Core Test Files

1. **ObjectTests.st** - Tests for the root Object class
2. **BooleanTests.st** - Tests for Boolean, True, and False classes
3. **NumberTests.st** - Tests for Number, Integer, and SmallInteger classes
4. **CharacterTests.st** - Tests for Character class
5. **UndefinedObjectTests.st** - Tests for UndefinedObject (nil) class
6. **CollectionTests.st** - Tests for Collection and Array classes
7. **StringTests.st** - Tests for String class
8. **BlockContextTests.st** - Tests for BlockContext class
9. **StandardLibraryTestSuite.st** - Master test suite runner

## Test Coverage by Class

### Object Class
- **Accessing**: `yourself`
- **Testing**: `isInteger`, `isNil`, `notNil`
- **Comparing**: `=`, `~=`, `==`, `~~`, `hash`
- **Class Membership**: `class`, `isMemberOf:`, `isKindOf:`
- **Accessing**: `size`, `basicSize`
- **Copying**: `copy`, `shallowCopy`
- **Printing**: `printString`

### Boolean Hierarchy
- **Boolean**: Logical operations (`&`, `|`, `not`, `and:`, `or:`, `eqv:`, `xor:`)
- **Boolean**: Control flow (`ifTrue:`, `ifFalse:`, `ifTrue:ifFalse:`, `ifFalse:ifTrue:`)
- **True/False**: Specific implementations and copying behavior
- **Printing**: `printString` for both True and False

### Number Hierarchy
- **Number**: `negated`
- **Integer**: `isInteger`, `even`
- **SmallInteger**: Complete arithmetic operations (`+`, `-`, `*`, `/`, `//`, `\\`)
- **SmallInteger**: Comparison operations (`<`, `<=`, `>`, `>=`, `=`, `~=`)
- **SmallInteger**: Bit manipulation (`bitAnd:`, `bitOr:`, `bitXor:`, `bitShift:`)
- **SmallInteger**: Testing (`even`, `odd`, `positive`, `negative`, `strictlyPositive`, `sign`)
- **SmallInteger**: Converting (`asString`, `printString`)
- **SmallInteger**: System (`hash`)

### Character Class
- **Accessing**: `asciiValue`, `digitValue`, `value`
- **Comparing**: `=`, `<`, `<=`, `>`, `>=`, `hash`
- **Testing**: `isAlphaNumeric`, `isDigit`, `isLetter`, `isLowercase`, `isUppercase`, `isSeparator`, `isVowel`, `tokenish`
- **Copying**: `copy`, `deepCopy`
- **Converting**: `asCharacter`, `asInteger`, `asLowercase`, `asUppercase`, `asSymbol`
- **Class Methods**: `value:`, `cr`, `lf`, `tab`, `space`

### UndefinedObject (nil)
- **Testing**: `isNil`, `notNil` (overrides)
- **Printing**: `printString`
- **Identity**: Basic identity operations

### Collection Hierarchy
- **Collection**: `size`, `isEmpty`, `notEmpty`
- **Collection**: `includes:`, `occurrencesOf:`
- **Collection**: `do:`, `collect:`, `select:`, `reject:`, `detect:`, `detect:ifNone:`, `inject:into:`
- **Array**: `at:`, `at:put:`, `size`
- **Array**: `do:` (enumeration)
- **Array**: `copyWith:`, `,` (concatenation)
- **Array**: `hash`, `asArray`
- **Array Class**: `new:`, `with:`, `with:with:`, `with:with:with:`

### String Class
- **Accessing**: `at:`, `at:put:`, `size`
- **Enumeration**: `do:`
- **Converting**: `asSymbol`, `asUppercase`, `asLowercase`
- **Comparing**: `=`, `<`, `<=`, `>`, `>=`
- **Copying**: `,` (concatenation)
- **Collection Behavior**: Inherited methods like `includes:`, `isEmpty`, `notEmpty`

### BlockContext Class
- **Evaluating**: `value`, `value:`, `value:value:`
- **Control Structures**: `whileTrue:`, `whileFalse:`, `whileTrue`, `whileFalse`
- **Nested Blocks**: Complex block structures
- **Closures**: Variable capture and modification
- **Conditional Evaluation**: Usage in conditional contexts

## Test Methodology

### Test Structure
Each test class follows the pattern:
1. **Setup**: Initialize test fixtures where needed
2. **Test Methods**: Individual methods testing specific functionality
3. **Assertions**: Use TestCase assertion methods (`assertEqual:to:`, `assertTrue:`, `assertFalse:`)
4. **Runner**: `performTest` method that executes all tests in sequence

### Assertion Methods Used
- `assertEqual:to:` - Verify two values are equal
- `assertTrue:` - Verify a condition is true
- `assertFalse:` - Verify a condition is false

### Test Coverage Principles
1. **Positive Cases**: Test normal, expected behavior
2. **Edge Cases**: Test boundary conditions (empty collections, zero values, etc.)
3. **Type Verification**: Ensure correct types are returned
4. **State Changes**: Verify mutable operations work correctly
5. **Error Conditions**: Where appropriate, test error handling

## Running the Tests

### Complete Test Suite
```smalltalk
StandardLibraryTestSuite new performTest
```

### Individual Test Categories
```smalltalk
ObjectTests new performTest
BooleanTests new performTest
NumberTests new performTest
SmallIntegerTests new performTest
CharacterTests new performTest
UndefinedObjectTests new performTest
CollectionTests new performTest
ArrayTests new performTest
StringTests new performTest
BlockContextTests new performTest
```

### Quick Test Run
```smalltalk
StandardLibraryTestSuite runQuick
```

## Test Execution from Command Line

Load and run tests using the Smalltalk-80 interpreter with proper dependency order:

### Dependency Loading Order
1. **Standard Library** (automatically loaded by interpreter)
2. **Test Framework** (TestLibrary.st - provides TestCase base class)
3. **Test Cases** (individual test files)

### Manual Test Execution

```bash
# The standard library is automatically loaded by the interpreter

# Load the testing framework first (required dependency)
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st"

# Then load individual test files (each requires TestLibrary.st)
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/ObjectTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/BooleanTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/NumberTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/CharacterTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/UndefinedObjectTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/CollectionTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/StringTests.st"
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/BlockContextTests.st"

# Load the master test suite with all dependencies
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/StandardLibraryTestSuite.st"
```

### Automated Test Execution

Use the provided PowerShell script for comprehensive testing:

```powershell
# Run all tests with proper dependency loading
.\scripts\Run-ComprehensiveTests.ps1

# Run specific test category
.\scripts\Run-ComprehensiveTests.ps1 -TestCategory "ObjectTests"

# Run with verbose output
.\scripts\Run-ComprehensiveTests.ps1 -Verbose
```

### VS Code Integration

The project is configured to automatically run all test cases after a successful compilation:

#### Build Task Integration
- **Default Build** (Ctrl+Shift+P → "Tasks: Run Build Task" or Ctrl+Shift+B):
  1. Generates ANTLR parser files
  2. Compiles the solution
  3. Automatically runs comprehensive test suite
  4. Reports test results in the terminal

#### Available Tasks
- **`build`** (default): Complete build and test workflow
- **`build-and-test`**: Build only (without tests)
- **`run-tests`**: Run tests independently
- **`generate-antlr`**: Generate ANTLR parser files only
- **`run`**: Run the interpreter
- **`repl`**: Start REPL mode

#### Task Execution Flow
```
Default Build (Ctrl+Shift+B)
  ↓
build-and-test-all
  ↓
build-and-test → generate-antlr → dotnet build
  ↓
run-tests → Run-ComprehensiveTests.ps1
  ↓
Test Results & Summary
```

#### Manual Task Execution
You can run individual tasks using VS Code's Command Palette:
1. Press `Ctrl+Shift+P`
2. Type "Tasks: Run Task"
3. Select the desired task from the list

#### Test Results in VS Code
- Test output appears in the Terminal panel
- Colored output shows pass/fail status
- Comprehensive logging with timestamps
- Build problems appear in the Problems panel

## Expected Outcomes

### Successful Test Execution
All tests should pass, demonstrating that:
1. All standard library methods work as specified
2. Edge cases are handled correctly
3. Type safety is maintained
4. Object behavior is consistent

### Test Validation
The tests validate:
- **Correctness**: Methods return expected values
- **Consistency**: Similar operations behave similarly across classes
- **Completeness**: All public methods are tested
- **Robustness**: Edge cases don't cause failures

## Maintenance and Extension

### Adding New Tests
1. Follow existing test patterns
2. Include setup/teardown as needed
3. Test both positive and negative cases
4. Add new test class to StandardLibraryTestSuite

### Test Categories
- **Unit Tests**: Individual method testing
- **Integration Tests**: Cross-class interaction
- **Regression Tests**: Prevent breaking changes
- **Performance Tests**: (Future consideration)

This comprehensive test suite ensures the reliability and correctness of the Smalltalk-80 standard library implementation.
