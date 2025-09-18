# Smalltalk-80 Interpreter Architecture

## Overview

This is a clean-slate implementation of a Smalltalk-80 virtual machine written in C#. The interpreter **automatically loads the complete Smalltalk-80 system image** with all 223 classes and 4,493 methods by default, providing the full Smalltalk-80 environment as specified in the Blue Book.

## Core Design Principles

1. **Clear Separation of Concerns**: Each component has a single, well-defined responsibility
2. **No Circular Dependencies**: Clean dependency hierarchy from low-level to high-level  
3. **Bootstrap-First Design**: System loads from JSON manifests without requiring self-hosting
4. **Zero Null Class Pointers**: Objects never exist with null/invalid class pointers
5. **VM/Image Boundary**: Clear separation between VM primitives and Smalltalk code

## Component Architecture

### Layer 0: Object Model (Pure Data Structures)
```
ObjectModel/
├── OOP.cs                 // 32-bit Object-Oriented Pointer encoding
├── ObjectHeader.cs        // Object metadata (class, size, format, GC flags)
├── ObjectLayout.cs        // Complete object memory layout
└── SpecialObjects.cs      // Well-known object indices (nil=0, true=2, false=1, etc.)
```

**OOP Encoding (32-bit)**:
- Bit 0: Tag (0=object pointer, 1=SmallInteger)
- Objects: Up to 2^31-1 indices (2 billion objects)
- SmallIntegers: -2^30 to 2^30-1 range (± 1 billion)
- Note: Smalltalk code assumes 15-bit SmallIntegers (-16384 to 16383)

### Layer 1: Memory Management
```
Memory/
├── ObjectMemory.cs        // Object allocation (fixed, word, byte arrays)
├── ObjectTable.cs         // OOP to ObjectLayout mapping with auto-resize
├── GarbageCollector.cs    // Mark-and-sweep from Special Objects Array
└── MemoryStatistics.cs    // Memory usage tracking
```

**Key Features**:
- Dynamic object table growth (no hard limits)
- Three allocation types: fixed objects, word arrays, byte arrays
- GC traces from 9 Special Objects Array roots
- Zero memory leaks in bootstrap (0 objects collected)

### Layer 2: VM Primitives (C# Implementation)
```
Primitives/
├── PrimitiveTable.cs           // Registry for primitives 0-255
├── ArithmeticPrimitives.cs     // Integer/Float operations (1-20)
├── ArrayPrimitives.cs          // at:, at:put:, size (60-69)
├── ClassPrimitives.cs          // class, new, basicNew (70-79)
├── ComparisonPrimitives.cs     // =, ==, <, >, <=, >= (1-7)
├── ControlPrimitives.cs        // blockCopy:, value, perform: (80-89)
├── ProcessPrimitives.cs        // Process scheduling (85-88)
├── IOPrimitives.cs             // File I/O, console I/O (90-94)
├── StringPrimitives.cs         // String operations (included in array)
├── ObjectPrimitives.cs         // become:, instVarAt:, hash (75-79)
└── SystemPrimitives.cs         // quit, garbageCollect (110-119)
```

**Implemented Process Primitives**:
- 85: semaphoreSignal
- 86: semaphoreWait  
- 87: processResume
- 88: processSuspend

### Layer 3: Execution Engine
```
Execution/
├── MethodContext.cs            // Method activation with stack
├── BlockContext.cs             // Block closure contexts
├── ProcessScheduler.cs         // Process scheduling and context switching
├── BytecodeDispatcher.cs       // Bytecode interpreter (256 bytecodes)
└── MessageDispatcher.cs        // Method lookup with caching (1024-entry cache)
```

**Bytecode Ranges**:
- 0-15: Push receiver variable
- 16-31: Push temporary variable  
- 32-63: Push literal constant
- 64-95: Push literal variable
- 96-103: Store and pop receiver variable
- 104-111: Store and pop temporary variable
- 112-119: Push special objects (receiver, true, false, nil, -1, 0, 1, 2)
- 120-123: Return operations
- 124-125: Stack operations
- 128-175: Send operations
- 176-191: Send literal selector with 0 args
- 192-207: Send literal selector with 1 arg
- 208-223: Send literal selector with 2 args
- 224-255: Extended operations

### Layer 4: Bootstrap Infrastructure
```
Bootstrap/
├── ClassBuilder.cs             // Multi-phase class construction
├── MethodInstaller.cs          // CompiledMethod creation and installation
├── ImageLoader.cs              // JSON manifest loading
├── BootstrapManifest.cs        // JSON data structures
└── KernelSpecification.cs      // Core class specifications
```

**Bootstrap Process**:
1. **Phase 1**: Allocate class/metaclass stubs
2. **Phase 2**: Wire class pointers (class -> metaclass -> Metaclass)
3. **Phase 3**: Wire superclass chains
4. **Phase 4**: Set class names (requires Symbol class)
5. **Phase 5**: Set instance variables (requires Array class)
6. **Create Special Objects**: nil, true, false singletons
7. **Install Methods**: Create CompiledMethod objects from JSON
8. **Create Symbol Table**: Initial symbol dictionary
9. **Create System Dictionary**: Register all classes

### Layer 5: Virtual Machine
```
VM/
├── VirtualMachine.cs           // Main VM coordinator
├── IExecutionContext.cs        // VM interface for execution
└── IPrimitiveContext.cs        // VM interface for primitives
```

**VM Capabilities**:
- Loads complete system (223 classes, 4,493 methods) by default
- Process scheduling with 100 priority levels
- Context switching for multi-process execution
- Semaphore-based synchronization
- 19,632+ objects managed
- ~0.12 second load time
- 256K object slots (expandable)

### Layer 6: Host Integration
```
Host/
├── ConsoleInterface.cs         // Interactive REPL
└── FileSystemInterface.cs      // File I/O support
```

## JSON Bootstrap Format

### Bootstrap Manifest Structure
```json
{
  "classes": [
    {
      "name": "Object",
      "superclass": null,
      "instanceVariables": [],
      "classVariables": [],
      "format": 0,
      "methods": [...],
      "classMethods": [...]
    }
  ],
  "methods": [
    {
      "class": "Object",
      "selector": "printString",
      "bytecode": [112, 209, 135, 124],
      "literals": ["class", "name"],
      "numArgs": 0,
      "numTemps": 0,
      "primitiveIndex": 0
    }
  ]
}
```

### Supported Class Format
- Bootstrap: 85 core classes
- Complete System: 223 classes
- Methods encoded as bytecode arrays
- Literals support: strings, symbols, numbers, arrays

## Memory Model

### Object Layout
```
ObjectLayout:
├── Header
│   ├── ClassOOP (32 bits)
│   ├── Size (32 bits)
│   ├── Format (8 bits)
│   ├── Hash (24 bits)
│   └── GCFlags (8 bits)
└── Fields[] (OOP array)
```

### Special Objects Array (9 entries)
```
0: NilObject
1: FalseObject  
2: TrueObject
3: BehaviorClass
4: MetaclassClass
5: SystemDictionaryInstance (Smalltalk global)
6: SymbolTable
7: ProcessList (reserved for active process list)
8: ProcessorScheduler (singleton instance)
```

## Performance Characteristics

### Method Cache
- 1024 entries with linear probing
- Caches method lookups by (class, selector) pair
- Typically 90%+ hit rate after warmup

### Garbage Collection
- Mark-and-sweep algorithm
- Traces from Special Objects Array
- Complete system GC in ~5ms
- Zero false positives (0 objects collected after bootstrap)

### Memory Usage
- Complete System: ~19,632 objects
- Default capacity: 262,144 objects (256K)
- Auto-grows as needed (up to 2^31-1 objects)
- Process contexts preserved during switches

## Compatibility Notes

### SmallInteger Range
- VM supports 31-bit SmallIntegers (±1 billion)
- Smalltalk code assumes 15-bit SmallIntegers (-16384 to 16383)
- Primitives should enforce 15-bit limits for compatibility

### Implemented Features
- ✅ Process scheduling with priorities
- ✅ Context switching
- ✅ Semaphore synchronization  
- ✅ Method caching (1024 entries)
- ✅ Complete bytecode set (256)
- ✅ Core primitives (85+)

### Not Yet Implemented
- Become: primitive (object swapping)
- Weak references
- Finalization
- Graphics primitives (BitBlt partially implemented)
- Network primitives
- Sound primitives

## File Organization

```
Smalltalk80.Interpreter/
├── Bootstrap/              # Bootstrap infrastructure
├── Docs/                   # Documentation
│   ├── ARCHITECTURE.md    # This file
│   ├── CHANGELOG.md        # Version history
│   └── PROCESS_SCHEDULING_REQUIREMENTS.md
├── Execution/             # Bytecode execution & scheduling
├── Host/                  # Host OS integration
├── Memory/                # Memory management
├── ObjectModel/           # Core data structures
├── Primitives/            # VM primitives
├── VM/                    # Virtual machine
├── Program.cs             # Entry point
└── README.md              # Quick start guide

../
├── Bootstrap/
│   └── bootstrap.json     # 85 core classes (fallback)
└── complete_system_final.json  # 223 full system classes (default)
```

## Building and Running

### Default Startup (Complete System)
```bash
dotnet run
# Automatically loads 223 classes from complete_system_final.json
# Falls back to bootstrap.json (85 classes) if not found
```

### Development Build
```bash
dotnet build Smalltalk80.Interpreter/Smalltalk80.Interpreter.csproj
```

### Running Tests
```bash
dotnet test
```

## Future Enhancements

1. **Graphics**: Full BitBlt implementation for display
2. **Become**: Object identity swapping primitive  
3. **File System**: Complete file stream primitives
4. **Compiler Integration**: Dynamic code compilation
5. **Image Persistence**: Save/load binary image format
6. **Debugger**: Step-through debugging support
7. **JIT Compilation**: Performance optimization
8. **Network**: TCP/IP socket primitives