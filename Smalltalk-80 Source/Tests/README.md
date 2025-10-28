# Smalltalk-80 Test Classes

This directory contains test classes that validate the Smalltalk-80 interpreter implementation. These test classes are **separate from the core Smalltalk-80 system image** and should be filed in after the main system is bootstrapped.

## Test Files

### BlockValidatorTest.st
Comprehensive validation of block closure semantics, including:
- Simple blocks
- Nested blocks
- Lexical scoping
- Block passing through methods
- Instance variable access in blocks
- Non-local returns

### SimpleBlockTest.st
Basic block functionality tests:
- Zero-argument blocks (`value`)
- One-argument blocks (`value:`)
- Two-argument blocks (`value:value:`)

### ConditionalBlockTest.st
Control flow tests using blocks:
- `ifTrue:`
- `ifFalse:`
- `ifTrue:ifFalse:`
- `ifFalse:ifTrue:`

## Usage

### During Development
Test files can be compiled and executed individually during interpreter development:

```bash
# Compile main system
dotnet run --project Smalltalk80.Compiler "Smalltalk-80 Source/Smalltalk-80.st" coldstart.json

# Compile and run specific test
dotnet run --project Smalltalk80.Compiler "Smalltalk-80 Source/Tests/SimpleBlockTest.st" test-bundle.json
dotnet run --project Smalltalk80.Interpreter --coldstart test-bundle.json
```

### Future: Multi-File Support
When the compiler supports multiple input files, tests will be loaded alongside the main system:

```bash
dotnet run --project Smalltalk80.Compiler \
  "Smalltalk-80 Source/Smalltalk-80.st" \
  "Smalltalk-80 Source/Tests/BlockValidatorTest.st" \
  "Smalltalk-80 Source/Tests/SimpleBlockTest.st" \
  "Smalltalk-80 Source/Tests/ConditionalBlockTest.st" \
  coldstart-with-tests.json
```

## Execution Order

Test classes should be executed **AFTER** all system initialization:

1. **Phase 1**: Load and compile main system (Smalltalk-80.st)
2. **Phase 2**: Execute all `ClassName initialize!` expressions
3. **Phase 3**: Execute all `ClassName comment:'...'!` expressions
4. **Phase 4**: Load and compile test classes
5. **Phase 5**: Execute test class `run!` expressions

This ensures all dependencies (e.g., `Digitbuffer` from `SmallInteger initialize`) are satisfied before tests run.

## Why Separate Files?

Test classes were originally included in the main Smalltalk-80.st file but have been extracted because:

1. **Clean separation**: Core system vs. validation code
2. **Optional testing**: Bootstrap system without running tests
3. **Faster iteration**: Modify/add tests without recompiling entire system
4. **Future extensibility**: Easy to add new test suites

## Contributing Tests

When adding new test classes:

1. Create a new `.st` file in this directory
2. Follow the existing pattern:
   - Class definition
   - Test methods
   - Class-side `run` method
   - Global `ClassName run!` expression at end
3. Document what is being tested in the class comment
4. Update this README with the new test file

## Notes

- Test classes use the `'Kernel-Tests'` category
- All tests inherit from `Object` (no test framework required)
- Tests report results via `Transcript` or return values
- Each test class is standalone and can run independently
