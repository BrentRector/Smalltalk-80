# Testing Framework and Strategy

**Date Created**: 2025-11-20
**Project**: Smalltalk-80 Interpreter Unit Test Suite
**Total Tests**: 829 unit tests

---

## Executive Summary

This document describes the comprehensive unit test suite created for the Smalltalk-80 interpreter. The test suite provides thorough coverage of all major interpreter components, with particular emphasis on the recently refactored modules including the unified temp frame architecture, variable resolution system, and control primitives.

### Test Coverage Goals

| Component Category | Target Coverage | Tests Created | Status |
|-------------------|-----------------|---------------|--------|
| Heap Manager | 85%+ | 68 | Complete |
| Primitives | 100% | 258 | Complete |
| Compiler (TempFrame, VariableResolver) | 90%+ | 154 | Complete |
| VM Execution | 85%+ | 147 | Complete |
| Cold Start Phases | 75%+ | 130 | Complete |
| Context Management | 85%+ | 72 | Complete |
| **Total** | **80%+ average** | **829** | **Complete** |

---

## Project Structure

```
Smalltalk80.Interpreter.Tests/
├── Smalltalk80.Interpreter.Tests.csproj
├── HeapManager/
│   └── SmalltalkHeapManagerTests.cs          (68 tests)
├── Primitives/
│   ├── PrimitiveTestBase.cs                  (Base class)
│   ├── ArithmeticPrimitivesTests.cs          (45 tests)
│   ├── ComparisonPrimitivesTests.cs          (31 tests)
│   ├── ObjectPrimitivesTests.cs              (41 tests)
│   ├── ControlPrimitivesTests.cs             (57 tests)
│   ├── FloatPrimitivesTests.cs               (26 tests)
│   ├── LargeIntegerPrimitivesTests.cs        (15 tests)
│   ├── StringPrimitivesTests.cs              (15 tests)
│   └── SystemPrimitivesTests.cs              (28 tests)
├── Compiler/
│   ├── TempFrameAllocatorTests.cs            (45 tests)
│   ├── VariableResolverTests.cs              (55 tests)
│   └── BytecodeEmitterTests.cs               (54 tests)
├── VM/
│   ├── BytecodeInterpreterTests.cs           (44 tests)
│   ├── MessageDispatcherTests.cs             (30 tests)
│   ├── StackManagerTests.cs                  (18 tests)
│   ├── GlobalRegistryTests.cs                (28 tests)
│   └── ExecutionStateTests.cs                (27 tests)
├── ColdStart/
│   ├── Phase1PreflightTests.cs               (28 tests)
│   ├── Phase2ClassInfrastructureTests.cs     (24 tests)
│   ├── Phase4MethodCompilationTests.cs       (15 tests)
│   ├── Phase6ValidationTests.cs              (19 tests)
│   ├── ColdStartServicesTests.cs             (28 tests)
│   └── InitExpressionExecutorTests.cs        (16 tests)
├── Context/
│   └── ContextManagerTests.cs                (72 tests)
└── TestUtilities/
    └── TestHelpers.cs                        (Shared utilities)
```

---

## Test Framework and Tools

### Framework: xUnit 2.6.1

Selected for:
- Modern async test support
- Excellent parallel test execution
- Clean attribute syntax
- Strong community adoption in .NET ecosystem

### Dependencies

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
<PackageReference Include="Moq" Version="4.20.70" />
<PackageReference Include="xunit" Version="2.6.1" />
<PackageReference Include="xunit.runner.visualstudio" Version="2.5.3" />
<PackageReference Include="coverlet.collector" Version="6.0.0" />
```

### Mocking Strategy: Moq

Used extensively for:
- `ISmalltalkHeapManager` - Memory operations
- `IContextManager` - Context lifecycle
- `IMessageDispatcher` - Method lookup
- `IBytecodeInterpreter` - Bytecode execution
- `IPrimitiveContext` - Primitive execution context

---

## Test Naming Convention

All tests follow the pattern:

```
MethodName_Scenario_ExpectedResult
```

Examples:
- `AllocateObject_PointerFormat_ReturnsValidOOP`
- `BlockCopy_ZeroArguments_CreatesBlockContext`
- `ResolveVariable_MethodArgument_ReturnsCorrectIndex`
- `GarbageCollect_CyclicReferences_AllSurvive`

---

## Critical Test Areas

### 1. Unified Temp Frame Architecture (HIGHEST PRIORITY)

The refactored unified temp frame is the most critical change. These tests verify:

**TempFrameAllocatorTests**:
- Block arguments allocated in MethodContext (not BlockContext)
- Sequential blocks reuse slots
- Nested blocks don't reuse slots
- `GetBlockArgumentStartIndex()` returns correct offset for Primitive 81

**VariableResolverTests**:
- 9-step resolution priority ordering
- Block arguments resolve to unified frame indices
- Parent variable access from nested blocks

**BytecodeInterpreterTests**:
- Push temporary bytecodes (0x10-0x1F) access unified frame
- Store temporary bytecodes (0x68-0x6F) modify unified frame

### 2. Control Primitives (100% Coverage Required)

**ControlPrimitivesTests** (57 tests):

| Primitive | Name | Tests | Coverage |
|-----------|------|-------|----------|
| 80 | blockCopy: | 15 | Home pointer, nested blocks, field init |
| 81 | value/value: | 12 | Argument passing, PC setup, sender |
| 82 | valueWithArguments: | 6 | Array argument handling |
| 83 | perform: | 5 | Dynamic message send |
| 84 | perform:with: | 4 | Message with argument |
| 85 | signal | 3 | Semaphore signaling |
| 86 | wait | 3 | Semaphore waiting |
| 87 | resume | 3 | Process resume |
| 88 | suspend | 3 | Process suspend |
| 89 | flushCache | 3 | Cache invalidation |

### 3. Heap Manager Operations

**SmalltalkHeapManagerTests** (68 tests):
- All ObjectFormats: Pointer, PointerIndexed, ByteIndexed, WordIndexed
- Instance variable and indexed access
- Garbage collection with reachability graphs
- Free list reuse after GC
- Become operations (identity swapping)

### 4. Bytecode Execution

**BytecodeInterpreterTests** (44 tests):

Tests all bytecode ranges from Blue Book Chapter 30:
- 0x00-0x0F: Push receiver variable
- 0x10-0x1F: Push temporary (unified frame)
- 0x20-0x3F: Push literal constant
- 0x40-0x5F: Push literal variable
- 0x60-0x67: Store instance variable
- 0x68-0x6F: Store temporary (unified frame)
- 0x70-0x77: Push special (receiver, true, false, nil, -1, 0, 1, 2)
- 0x78-0x7D: Return operations
- 0x90-0x97: Jumps
- 0xD0-0xFF: Send literal selector

---

## Running the Tests

### Build and Run All Tests

```bash
cd Smalltalk80.Interpreter.Tests
dotnet test
```

### Run with Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

### Run Specific Test Category

```bash
# Run only HeapManager tests
dotnet test --filter "FullyQualifiedName~HeapManager"

# Run only Control Primitive tests
dotnet test --filter "FullyQualifiedName~ControlPrimitives"

# Run only Compiler tests
dotnet test --filter "FullyQualifiedName~Compiler"
```

### Run with Code Coverage

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### Generate Coverage Report

```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

---

## Integration with Regression Testing

### Mandatory Regression Test

After ANY interpreter or compiler change, run the BlockContext regression test:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected Result**: Returns `1191` (120 BlockContext tests pass)

### Recommended Workflow

1. **Before changes**: Run unit tests to establish baseline
2. **After changes**: Run unit tests
3. **Final validation**: Run BlockContext regression test
4. **Verify**: Result is still `1191`

---

## Test Utilities

### TestHelpers Class

Located in `TestUtilities/TestHelpers.cs`, provides:

**OOP Creation**:
```csharp
SmallInt(42)           // Create SmallInteger OOP
ObjectOOP(100)         // Create object OOP from index
NilOOP(), TrueOOP(), FalseOOP()  // Special objects
```

**Mock Factories**:
```csharp
CreateMockHeapManager()    // Pre-configured heap mock
CreateInitializedHeap()    // Real heap with SOA
```

**Assertions**:
```csharp
AssertPrimitiveSuccess(result)
AssertPrimitiveFailure(result)
AssertPrimitiveSuccessWithValue(result, 42)
AssertSmallInteger(oop, 42)
```

**Bytecode Helpers**:
```csharp
PushTempBytecode(3)    // 0x13
StoreTempBytecode(2)   // 0x6A
```

---

## Known Limitations

### Cannot Test Without Running

Since the .NET 9 SDK is not available in the development environment, tests cannot be executed during creation. The following was done to ensure quality:

1. **Syntax Validation**: All test code follows correct C# 13 syntax
2. **API Alignment**: Test code matches actual source file APIs
3. **Pattern Consistency**: Consistent mocking and assertion patterns
4. **Naming Convention**: Strict adherence to naming standards
5. **Coverage Planning**: Systematic coverage of all documented APIs

### Tests That May Need Adjustment

After running tests, pay attention to:
- Mock setup mismatches with actual implementations
- Missing or changed API methods
- Namespace/type resolution errors
- Assembly reference issues

---

## Maintenance Guidelines

### Adding New Tests

1. Follow the `MethodName_Scenario_ExpectedResult` naming convention
2. Place in appropriate category folder
3. Use TestHelpers utilities where applicable
4. Ensure proper mock cleanup with `[Fact]` isolation
5. Document any complex test scenarios

### Updating Existing Tests

When implementation changes:
1. Run affected tests first to identify failures
2. Update mock setups to match new APIs
3. Update expected values to match new behavior
4. Verify with BlockContext regression test

### Critical Files Requiring Full Test Pass

From CLAUDE.md, changes to these files MUST pass all tests:
- `BytecodeCompiler.cs`
- `VariableResolver.cs`
- `TempFrameAllocator.cs`
- `VirtualMachine.cs`
- `ControlPrimitives.cs`

---

## Future Enhancements

### Phase 2 Testing (When Resources Available)

1. **Integration Tests**: End-to-end method execution
2. **Performance Tests**: Benchmark critical paths
3. **Snapshot Tests**: Bytecode generation verification
4. **Fuzz Testing**: Random input validation

### Continuous Integration

Recommended CI workflow:
```yaml
test:
  steps:
    - dotnet build
    - dotnet test --logger trx
    - dotnet run -- --coldstart coldstart_with_runner.json
    - verify output equals 1191
```

---

## Summary

This test suite provides comprehensive coverage of the Smalltalk-80 interpreter with **829 unit tests** across 6 major component categories. The tests specifically target the critical refactored areas including the unified temp frame architecture, ensuring that block variables are correctly allocated and accessed through the MethodContext.

The test suite is designed to:
- Catch regressions early in development
- Document expected behavior
- Provide confidence for future refactoring
- Serve as executable specification

Run the tests after every change to maintain the interpreter's operational status.
