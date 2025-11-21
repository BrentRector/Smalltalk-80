# Unit Test Suite - Quick Reference Guide

**Last Updated**: 2025-11-20  
**Full Documentation**: See `UNIT-TEST-SUITE-PLAN.md` (1776 lines)

---

## 📊 Component Overview

### 3 Major Subsystems

| Subsystem | Files | Lines | Priority | Status |
|-----------|-------|-------|----------|--------|
| **Compiler** | 6 | 5,200 | CRITICAL | Active |
| **Interpreter** | 45+ | 10,000+ | CRITICAL | Refactoring complete (phases 1-4) |
| **Cold Start** | 16 | 4,500+ | CRITICAL | Active (Phase 5 needs service extraction) |

---

## 🎯 Critical Components to Test

### Top 5 Risk Items (Test First)

1. **BytecodeCompiler** (3,300 lines)
   - Generates bytecode from AST
   - Risk: Wrong bytecode breaks entire execution
   - Test Count: 100+

2. **VariableResolver** (800 lines)
   - Resolves variables in 9-step priority order
   - Risk: Wrong index → wrong variable access
   - Test Count: 50+

3. **TempFrameAllocator** (300 lines)
   - Allocates unified temp frame slots
   - Risk: New architecture (block args now in MethodContext)
   - Test Count: 40+

4. **BytecodeInterpreter** (1,100 lines)
   - Executes individual bytecode instructions
   - Risk: Core execution engine
   - Test Count: 80+

5. **Control Primitives (80-84)** (Critical)
   - Block execution, value sending
   - Risk: All block operations depend on these
   - Test Count: 50+

---

## 🗂️ Test Organization

### Suggested Folder Structure

```
Tests/
├── Compiler/
│   ├── BytecodeCompilerTests.cs
│   ├── VariableResolverTests.cs
│   ├── TempFrameAllocatorTests.cs
│   └── [3 supporting test files]
├── Interpreter/
│   ├── VirtualMachineTests.cs
│   ├── BytecodeInterpreterTests.cs
│   ├── ContextManagerTests.cs
│   ├── MessageDispatcherTests.cs
│   └── [Primitives subdirectory]
├── Memory/
│   ├── SmalltalkHeapManagerTests.cs
│   ├── GarbageCollectorTests.cs
│   └── LayoutResolverTests.cs
└── ColdStart/
    ├── Phase1Tests.cs
    ├── Phase2Tests.cs
    ├── [... Phases 3-6]
    └── ColdStartIntegrationTests.cs
```

---

## 📈 Test Metrics

### Estimated Test Cases by Phase

| Phase | Target | Complexity | Effort |
|-------|--------|-----------|--------|
| **Phase 1: Compiler** | 200 tests | High | 2 weeks |
| **Phase 2: Interpreter Core** | 300 tests | Very High | 2 weeks |
| **Phase 3: Memory & Cold Start** | 300 tests | Very High | 2 weeks |
| **Phase 4: Integration** | 100 tests | Medium | 1 week |
| **Phase 5: Services (When Extracted)** | 100 tests | Medium | 1 week |

**Total**: ~1000 unit tests across all phases

### Coverage Goals

- **BytecodeCompiler**: 85%+
- **VariableResolver**: 90%+
- **TempFrameAllocator**: 90%+
- **BytecodeInterpreter**: 85%+
- **Primitives 1-84**: 80%+
- **Cold Start Phases**: 75%+

---

## 🔗 Critical Dependencies

### Compiler → Interpreter → Memory → Cold Start

```
BytecodeCompiler
    ↓ (depends on)
VariableResolver + TempFrameAllocator
    ↓ (bytecode generated for)
BytecodeInterpreter
    ↓ (executes on)
SmalltalkHeapManager + Primitives
    ↓ (heap built by)
Cold Start Phases 1-6
```

**Key Invariants**:
- VariableResolver and TempFrameAllocator must be synchronized
- Bytecode indices must match allocated slots
- All phases must execute in order
- No data corruption after GC

---

## ✅ Regression Test

**Mandatory after EVERY test addition**:

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
```

**Expected Output**: `Returns 1191` (120 BlockContext tests pass)

---

## 🔄 Refactoring Components

These were recently extracted and need testing:

1. **ExecutionState.cs** (~100 lines)
   - Immutable trampoline state records
   - Tests: State transitions, context switches

2. **ExecutionTracer.cs** (~200 lines)
   - Debug/trace state extracted from VirtualMachine
   - Tests: Trace output, entry/exit tracking

3. **NonLocalReturnHandler.cs** (~150 lines)
   - NLR state machine encapsulation
   - Tests: NLR unwinding, context skipping

4. **SelectorCache.cs** (~100 lines)
   - Arithmetic/special selector caching
   - Tests: Cache correctness, invalidation

---

## 📋 Key Test Scenarios

### 1. Unified Temp Frame Tests (Critical)

```smalltalk
1. Simple block: [5] value
2. Block with arguments: [:x | x + 1] value: 5
3. Block capturing temp: | temp | temp := 10. [:x | x + temp] value: 5
4. Nested blocks: | temp | [:a | [:b | a + b + temp] value: 3] value: 2
5. Non-local return: [^42] value
6. Escaping block: | block | block := [:x | x + 1]. block value: 5
```

### 2. Variable Resolution Tests (9 Steps)

```
Priority 1: Pseudo-variables (self, true, false, nil, super, thisContext)
Priority 2: Method arguments (in unified frame)
Priority 3: Method temporaries (in unified frame)
Priority 4: Block arguments (in unified frame - NEW!)
Priority 5: Block temporaries (in unified frame)
Priority 6: Instance variables (class layout)
Priority 7: Class variables (literal index)
Priority 8: Pool variables (literal index + pool name)
Priority 9: Global variables (Undeclared if not found)
```

### 3. Non-Local Return Tests

```
1. Simple: [^42]
2. From nested block: [[^42] value] value
3. With active temporaries
4. In exception handler
```

### 4. Primitive Tests (Control = CRITICAL)

```
Primitive 80: blockCopy: - Create BlockContext
Primitive 81: value (no args)
Primitive 82: value: (1 arg)
Primitive 83: value:value: (2 args)
```

---

## 🛠️ Test Infrastructure Required

### Mocks/Fakes Needed

- `MockHeapManager` - In-memory allocation
- `MockContextManager` - Track contexts
- `MockMessageDispatcher` - Track sends
- `FakeCompilationEnvironment` - Variable resolution testing

### Test Helpers Needed

- `MethodBuilder` - Construct test methods
- `BlockBuilder` - Construct test blocks
- `AssertBytecodeSequence()` - Verify bytecode patterns
- `AssertVariableResolution()` - Verify resolver
- `AssertHeapIntegrity()` - Post-execution validation

### Dependencies

```xml
<ItemGroup>
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.x" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
    <PackageReference Include="Moq" Version="4.x" />
</ItemGroup>
```

---

## 📍 File Locations (Absolute Paths)

### Source Files

```
/home/user/Smalltalk-80/Smalltalk80.Compiler/
  ├── BytecodeCompiler.cs
  ├── VariableResolver.cs
  └── Analysis/TempFrameAllocator.cs

/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/
  ├── VirtualMachine.cs
  ├── BytecodeInterpreter.cs
  ├── ContextManager.cs
  ├── MessageDispatcher.cs
  └── Primitives/[15 primitive files]

/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/HeapManager/
  ├── SmalltalkHeapManager.cs
  └── GarbageCollector.cs

/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/
  ├── Phase1_PreflightAndSymbols.cs
  ├── Phase2_ClassInfrastructure.cs
  ├── Phase3_ObjectAllocation.cs
  ├── Phase4_MethodCompilation.cs
  ├── Phase5/Phase5Orchestrator.cs
  └── Phase6_ValidationAndGC.cs
```

### Documentation Files

```
/home/user/Smalltalk-80/docs/
  ├── UNIT-TEST-SUITE-PLAN.md (this plan - 1776 lines)
  ├── TEST-SUITE-QUICK-REFERENCE.md (this file)
  ├── Architecture/PHYSICAL-ARCHITECTURE.md
  ├── Architecture/IMPLEMENTATION-ARCHITECTURE.md
  ├── Architecture/DEVELOPMENT-GUIDE.md
  ├── Block-Context-And-Variable-Access-COMPREHENSIVE.md
  └── Refactoring/Cold-Start-Refactoring.md
```

---

## 🎓 Learning Path

1. **Read** CLAUDE.md (project status)
2. **Read** Architecture docs (3 files)
3. **Read** UNIT-TEST-SUITE-PLAN.md (full details)
4. **Examine** critical source files
5. **Start** with Compiler tests (highest ROI)
6. **Validate** with BlockContextTest regression
7. **Proceed** to Interpreter, Memory, Cold Start

---

## ✨ Success Criteria

### Per Component

| Component | Metric |
|-----------|--------|
| Build | 0 errors, 0 warnings |
| BlockContextTest | Returns 1191 (120 tests pass) |
| Coverage | Target percentages met |
| Tests | Complete per phase plan |

### Overall

- [ ] 1000+ unit tests created
- [ ] All tests passing
- [ ] All coverage targets met
- [ ] BlockContextTest regression passes
- [ ] No build warnings
- [ ] Documentation complete

---

## 🚀 Quick Start

```bash
# 1. Review the full plan
cat /home/user/Smalltalk-80/docs/UNIT-TEST-SUITE-PLAN.md

# 2. Create test project structure
cd /home/user/Smalltalk-80
mkdir -p Smalltalk80.Tests/{Compiler,Interpreter,Memory,ColdStart}

# 3. Begin with compiler tests
# See UNIT-TEST-SUITE-PLAN.md Section 8.1 for detailed breakdown

# 4. After each test addition
cd Smalltalk80.Interpreter
dotnet build
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
# Expected: Returns 1191
```

---

## 📞 Key Contacts

- **Full Plan**: `/home/user/Smalltalk-80/docs/UNIT-TEST-SUITE-PLAN.md`
- **Architecture**: `/home/user/Smalltalk-80/docs/Architecture/`
- **Refactoring**: `/home/user/Smalltalk-80/docs/Refactoring/`
- **Project Status**: `/home/user/Smalltalk-80/CLAUDE.md`

---

**Status**: ✅ Ready for Test Suite Implementation  
**Created**: 2025-11-20  
**Next Step**: Begin Phase 1 (Compiler Tests)
