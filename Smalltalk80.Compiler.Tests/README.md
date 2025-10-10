# Smalltalk80.Compiler.Tests

Comprehensive test suite for the Smalltalk-80 compiler, verifying bytecode generation for all language features.

## Test Structure

The test suite is organized into the following categories:

### 1. **LiteralTests.cs**
Tests for literal value compilation:
- Numbers (SmallInteger, LargeInteger, Float)
- Strings and empty strings
- Symbols (regular and keyword)
- Characters (regular and special)
- Arrays (literal, empty, nested)
- Special constants (true, false, nil)
- Special integers (-1, 0, 1, 2)

### 2. **VariableTests.cs**
Tests for variable access patterns:
- Instance variables (read/write)
- Class variables
- Temporary variables
- Method parameters
- Block parameters and temporaries
- Special variables (self, super, thisContext)
- Variable shadowing

### 3. **MessageTests.cs**
Tests for message sending:
- Unary messages
- Binary messages
- Keyword messages (single and multiple)
- Message precedence rules
- Cascaded messages
- Special selectors (+, -, <, =, etc.)
- Self and super sends

### 4. **ControlStructureTests.cs**
Tests for control flow:
- Conditionals (ifTrue:, ifFalse:, ifTrue:ifFalse:)
- Loops (whileTrue:, whileFalse:, timesRepeat:)
- Iteration (to:do:, to:by:do:)
- Boolean operations (and:, or:, not)
- Early returns
- Guard clauses

### 5. **BlockTests.cs**
Tests for block literals:
- Empty blocks
- Blocks with parameters
- Blocks with temporaries
- Nested blocks
- Block closures
- Non-local returns
- Recursive blocks
- Block value messages

### 6. **MethodTests.cs**
Tests for method compilation:
- Simple returns
- Implicit returns (self)
- Optimized returns (true, false, nil)
- Method temporaries
- Method parameters
- Primitive methods
- Complex expressions
- Accessors and mutators

### 7. **EdgeCaseTests.cs**
Tests for special cases and edge conditions:
- Empty methods
- Very long selectors
- Many arguments
- Deep nesting
- Large literal pools
- Large temp counts
- Special characters
- Chained assignments
- Unicode support

### 8. **IntegrationTests.cs**
End-to-end compilation tests:
- Complete class compilation
- Complex expressions
- Control structures
- Method categories
- Bytecode optimizations
- Primitive methods
- Large methods

## Test Sources

The `TestSources` directory contains complete Smalltalk source files used for integration testing:

- **SimpleClass.st** - Basic class with instance variables and methods
- **ComplexExpressions.st** - Complex arithmetic, conditionals, blocks, and cascades
- **ControlStructures.st** - All loop and control flow constructs

## Expected Bytecode Ranges

The tests verify against the Smalltalk-80 bytecode specification:

| Range | Description |
|-------|-------------|
| 0-15 | Push receiver variable |
| 16-31 | Push temporary variable |
| 32-63 | Push literal constant |
| 64-95 | Push literal variable |
| 96-103 | Pop and store receiver variable |
| 104-111 | Pop and store temporary variable |
| 112-119 | Push special constants |
| 120-123 | Return operations |
| 124-125 | Stack operations |
| 128-175 | Send operations |
| 176-191 | Send literal selector (0 args) |
| 192-207 | Send literal selector (1 arg) |
| 208-223 | Send literal selector (2 args) |
| 224-255 | Special sends and primitives |

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test category
dotnet test --filter TestCategory=Literals

# Run with verbose output
dotnet test -v detailed
```

## Test Base Class

All test classes inherit from `CompilerTestBase` which provides:
- `CompileSource(string)` - Compile Smalltalk source to JSON
- `CompileFile(string)` - Compile a test source file
- `GetFirstMethod(JObject)` - Extract first method from JSON
- `AssertBytecode(...)` - Verify bytecode sequence
- `AssertLiterals(...)` - Verify literal values
- `WrapInTestClass(string)` - Create test class wrapper

## Contributing

When adding new tests:
1. Create focused tests for specific language features
2. Use descriptive test names
3. Include expected bytecode sequences
4. Document any special cases or assumptions
5. Add integration tests for complex scenarios