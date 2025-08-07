# Smalltalk-80 Solution Restructure Plan

## New Project Structure

```
Smalltalk-80.sln
├── Core/
│   ├── Smalltalk80.Core/              # Core interfaces and abstractions
│   │   ├── IVirtualMachine.cs
│   │   ├── IObjectMemory.cs
│   │   ├── IClassRegistry.cs
│   │   ├── IPrimitiveDispatcher.cs
│   │   └── IBootstrapProvider.cs
│   │
│   ├── Smalltalk80.ObjectModel/       # Object model implementation
│   │   ├── SmalltalkObject.cs
│   │   ├── SmalltalkClass.cs
│   │   ├── CompiledMethod.cs
│   │   ├── Symbol.cs
│   │   ├── BlockContext.cs
│   │   └── Association.cs
│   │
│   └── Smalltalk80.VM/                # Virtual machine implementation
│       ├── VirtualMachine.cs
│       ├── ObjectMemory.cs
│       ├── ClassRegistry.cs
│       └── MethodCache.cs
│
├── Execution/
│   ├── Smalltalk80.Interpreter/       # Bytecode interpreter
│   │   ├── Interpreter.cs
│   │   ├── ExecutionContext.cs
│   │   ├── BytecodeDispatcher.cs
│   │   └── SpecialSelectors.cs
│   │
│   ├── Smalltalk80.Bytecodes/         # Bytecode operations
│   │   ├── StackOperations.cs
│   │   ├── JumpOperations.cs
│   │   ├── SendOperations.cs
│   │   └── ReturnOperations.cs
│   │
│   └── Smalltalk80.Primitives/        # Primitive operations
│       ├── PrimitiveDispatcher.cs
│       ├── ArithmeticPrimitives.cs
│       ├── ObjectPrimitives.cs
│       ├── ControlPrimitives.cs
│       ├── IOPrimitives.cs
│       └── FileSystemPrimitives.cs
│
├── Bootstrap/
│   ├── Smalltalk80.Bootstrap/         # Bootstrap infrastructure
│   │   ├── JsonBootstrapProvider.cs
│   │   ├── BootstrapExecutor.cs
│   │   ├── Phase1Executor.cs         # VM primitive operations
│   │   ├── Phase2Executor.cs         # Messaging operations
│   │   └── Phase3Executor.cs         # File loading
│   │
│   └── Smalltalk80.Bootstrap.Data/    # Bootstrap data files
│       ├── bootstrap_phase1.json
│       ├── bootstrap_phase2.json
│       └── bootstrap_phase3.json
│
├── Compilation/
│   ├── Smalltalk80.Parser/            # ANTLR4 parser
│   │   ├── Smalltalk80.g4
│   │   ├── ParserService.cs
│   │   └── Generated/
│   │
│   └── Smalltalk80.Compiler/          # AST to bytecode compiler
│       ├── AstMethodCompiler.cs
│       ├── BytecodeGenerator.cs
│       └── LiteralEncoder.cs
│
├── IO/
│   └── Smalltalk80.FileSystem/        # File I/O
│       ├── FileStream.cs
│       ├── FileLoader.cs
│       └── SystemImageLoader.cs
│
├── CLI/
│   └── Smalltalk80.CLI/               # Command-line interface
│       ├── Program.cs
│       ├── Commands/
│       └── Configuration/
│
└── Tests/
    ├── Smalltalk80.Core.Tests/
    ├── Smalltalk80.Interpreter.Tests/
    ├── Smalltalk80.Bootstrap.Tests/
    └── Smalltalk80.Integration.Tests/
```

## Key Design Principles

### 1. **Clean Architecture Layers**
- **Core**: Interfaces and abstractions only
- **Implementation**: Concrete implementations separated by concern
- **Bootstrap**: Isolated bootstrap logic with JSON-driven approach
- **No circular dependencies** between projects

### 2. **Interface-Driven Design**
All major components defined by interfaces in Core project:
- `IVirtualMachine`: Top-level VM interface
- `IObjectMemory`: Object storage and management
- `IClassRegistry`: Class and metaclass management
- `IPrimitiveDispatcher`: Primitive operation dispatch
- `IBootstrapProvider`: Bootstrap data provider

### 3. **JSON-Driven Bootstrap**
Replace hardcoded bootstrap with data-driven approach:
- Phase 1: VM primitives from `bootstrap_phase1.json`
- Phase 2: Messaging from `bootstrap_phase2.json`
- Phase 3: File loading from `bootstrap_phase3.json`

### 4. **Modern C# Features**
- File-scoped namespaces (C# 10+)
- Record types for immutable data
- Pattern matching enhancements
- Nullable reference types
- Init-only properties

## Migration Steps

### Step 1: Create New Project Structure
1. Create solution with new project structure
2. Set up project dependencies
3. Configure NuGet packages (ANTLR4, System.Text.Json)

### Step 2: Define Core Interfaces
1. Create interface definitions in Core project
2. Define data contracts for bootstrap
3. Set up dependency injection infrastructure

### Step 3: Migrate Object Model
1. Move object model classes to ObjectModel project
2. Remove C# shadow objects
3. Implement proper VM object creation

### Step 4: Implement JSON Bootstrap
1. Create JSON bootstrap provider
2. Implement Phase 1 executor (VM primitives)
3. Implement Phase 2 executor (messaging)
4. Remove legacy bootstrap code

### Step 5: Refactor VM and Interpreter
1. Separate VM from interpreter concerns
2. Clean up execution context management
3. Consolidate primitive dispatch

### Step 6: Update Tests
1. Create unit tests for each component
2. Add integration tests for bootstrap
3. Ensure backwards compatibility where needed

## Benefits of Restructuring

1. **Maintainability**: Clear separation of concerns
2. **Testability**: Interface-driven design enables mocking
3. **Flexibility**: JSON bootstrap can be modified without recompilation
4. **Performance**: Cleaner architecture enables optimization
5. **Documentation**: Project structure reflects system architecture
6. **Extensibility**: Easy to add new primitives or bootstrap phases