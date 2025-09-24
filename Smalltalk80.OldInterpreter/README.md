# Smalltalk-80 Interpreter - Clean Slate Edition

A clean architecture implementation of a Smalltalk-80 virtual machine and interpreter, designed from the ground up to avoid common pitfalls like null class pointers and circular dependencies.

## Quick Start

The interpreter now loads the **complete Smalltalk-80 system image** by default, containing all 223 classes and 4,493 methods. This provides the full Smalltalk-80 environment as specified in the Blue Book.

```bash
# Build and run the interpreter
dotnet build
dotnet run --project Smalltalk80.Interpreter

# The interpreter will automatically:
# 1. Search for complete_system_final.json (full system)
# 2. Fall back to bootstrap.json if not found
# 3. Use built-in bootstrap as last resort
```

## Architecture Overview

The interpreter is organized in 6 clean layers with no circular dependencies:

### Layer 0: Object Model (`ObjectModel/`)
- **OOP.cs**: Object-Oriented Pointer encoding (SmallIntegers vs object references)
- **ObjectHeader.cs**: Object metadata (class, size, format, hash, GC flags)  
- **ObjectLayout.cs**: Complete object memory layout
- **SpecialObjects.cs**: Indices for the 9 special objects (nil, true, false, etc.)

### Layer 1: Memory Management (`Memory/`)
- **ObjectMemory.cs**: Raw object allocation without semantic knowledge
- **ObjectTable.cs**: Maps OOP indices to object layouts
- **GarbageCollector.cs**: Mark-and-sweep GC tracing from Special Objects Array
- **MemoryStatistics.cs**: Memory usage tracking

### Layer 2: VM Primitives (`Primitives/`)
- **PrimitiveTable.cs**: Registry for primitives 0-255
- **PrimitiveResult.cs**: Success/failure result type for primitives
- **ObjectPrimitives.cs**: Core object manipulation (at:, at:put:, new, class, etc.)

### Layer 3: Execution Engine (`Execution/`)
- **MethodContext.cs**: Method activation records and execution stack
- **BytecodeDispatcher.cs**: Interprets Smalltalk-80 bytecodes per Blue Book

### Layer 4: Bootstrap Infrastructure (`Bootstrap/`)
- **BootstrapPhase.cs**: Defines the 12-phase bootstrap process
- **KernelSpecification.cs**: Specifications for the 27 kernel classes
- **ClassBuilder.cs**: Multi-pass class construction avoiding null pointers

### Layer 5: VM & Host (`VM/` and `Host/`)
- **VirtualMachine.cs**: Coordinates all components
- **ConsoleInterface.cs**: REPL and debugging interface

## Key Design Features

### Zero Null Class Pointers
The bootstrap process uses multiple passes to ensure objects never have null class pointers:
1. Phase 1: Allocate ALL class stubs with temporary placeholders
2. Phase 2: Wire ALL class pointers atomically
3. Phase 3-5: Set names, variables, and methods only after classes exist

### Special Objects Array
The VM maintains exactly 9 special objects for garbage collection and system operation:
- 0: nil object
- 1: false object  
- 2: true object
- 3: Behavior class
- 4: Metaclass class
- 5: SystemDictionary instance (Smalltalk global)
- 6: Symbol table
- 7: Process list (reserved for active process list)
- 8: ProcessorScheduler (singleton instance)

### Clean Bootstrap Process
The system bootstraps from bare metal through 12 well-defined phases:
- Phases 0-7: Kernel class creation
- Phase 8: File I/O infrastructure
- Phase 9: Compiler bootstrap
- Phase 10: FileIn capability
- Phase 11: Self-hosting with Behavior>>subclass:

## Running the Interpreter

```bash
# Build the interpreter
dotnet build Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj

# Run the interactive REPL
dotnet run --project Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj
```

## REPL Commands

- `help` - Show available commands
- `quit`/`exit` - Exit the interpreter
- `gc` - Run garbage collection
- `memory` - Show memory statistics
- `classes` - List all registered classes
- `inspect <name>` - Inspect a class or object
- `eval <expr>` - Evaluate a Smalltalk expression (requires compiler)

## Current Status

### System Specifications
- **Classes**: 223 (complete Smalltalk-80 system)
- **Methods**: 4,493 
- **Memory**: 256K object slots (configurable)
- **Load Time**: ~0.12 seconds on modern hardware

✅ **Implemented:**
- Complete Smalltalk-80 system image loading
- Complete object model with OOP encoding
- Memory management with mark-and-sweep GC
- Bootstrap infrastructure with multi-pass class creation
- Process scheduling system with priorities
- Full bytecode dispatcher (all 256 bytecodes)
- Message sending and method lookup
- Interactive console interface
- 85 core primitives

🚧 **In Progress:**
- Remaining primitives (graphics, file I/O)
- Compiler integration for dynamic code evaluation
- Debugger support

📋 **Future Work:**
- Graphics subsystem (BitBlt, Forms)
- Network support
- Image snapshot and resume
- JIT compilation

## Design Documentation

See `Docs/ARCHITECTURE.md` for detailed design documentation and `../BOOTSTRAP_DESIGN.md` for the complete bootstrap specification.

## Benefits of Clean Architecture

1. **Maintainability**: Clear separation of concerns makes the code easy to understand
2. **Correctness**: No null pointers or circular dependencies by design
3. **Testability**: Each layer can be tested independently
4. **Extensibility**: New features can be added without breaking existing code
5. **Documentation**: Architecture is self-documenting through clean structure

The clean slate approach ensures a solid foundation for a correct, maintainable Smalltalk-80 implementation.