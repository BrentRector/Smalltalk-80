# Clean Slate Smalltalk-80 Architecture

## Core Design Principles

1. **Clear Separation of Concerns**: Each component has a single, well-defined responsibility
2. **No Circular Dependencies**: Clean dependency hierarchy from low-level to high-level
3. **Bootstrap-First Design**: System designed to bootstrap from bare metal without hacks
4. **Zero Null Pointers**: No objects ever exist with null class pointers
5. **VM/Image Boundary**: Clear separation between VM primitives and Smalltalk code

## Component Architecture

### Layer 0: Object Model (Pure Data Structures)
```
ObjectModel/
├── OOP.cs              // Object-Oriented Pointer encoding (even=object, odd=SmallInteger)
├── ObjectHeader.cs     // Object header with class pointer, size, hash, GC flags
├── ObjectLayout.cs     // Object memory layout (fields array)
└── SpecialObjects.cs   // Well-known object indices (nil, true, false, etc.)
```

### Layer 1: Memory Management
```
Memory/
├── ObjectMemory.cs     // Raw object allocation, no semantics
├── ObjectTable.cs      // OOP to object mapping
├── GarbageCollector.cs // Mark-and-sweep GC with Special Objects Array roots
└── MemoryStatistics.cs // Memory usage tracking
```

### Layer 2: VM Primitives (C# Implementation)
```
Primitives/
├── PrimitiveTable.cs           // Primitive method registry (0-255)
├── ArithmeticPrimitives.cs     // Integer/Float arithmetic
├── ObjectPrimitives.cs         // new, basicNew, become:, class, etc.
├── ControlPrimitives.cs        // perform:, value, blockCopy, etc.
├── SystemPrimitives.cs         // quit, garbageCollect, etc.
├── IOPrimitives.cs            // File I/O, console I/O
└── ClassPrimitives.cs         // THE primitive: primitiveNewClass:superclass:format:
```

### Layer 3: Execution Engine
```
Execution/
├── MethodContext.cs           // Execution context (sender, receiver, method, stack)
├── BlockContext.cs            // Block closure context
├── BytecodeDispatcher.cs      // Bytecode interpretation engine
├── MessageSend.cs             // Message lookup and dispatch
├── MethodCache.cs             // Method lookup cache for performance
└── Stack.cs                   // Execution stack management
```

### Layer 4: Bootstrap Infrastructure
```
Bootstrap/
├── BootstrapPhase.cs          // Phase 0-11 definitions from design doc
├── KernelSpecification.cs     // Minimal kernel class specifications
├── ObjectAllocator.cs         // Multi-pass object allocation
├── ClassBuilder.cs            // Multi-pass class construction
├── MethodInstaller.cs         // Method bytecode installation
└── ImageSerializer.cs         // Save/load image state
```

### Layer 5: Virtual Machine Interface
```
VM/
├── VirtualMachine.cs          // High-level VM interface
├── VMConfiguration.cs         // VM settings and options
├── Interpreter.cs             // Main interpreter loop
└── VMState.cs                 // VM state (Special Objects Array, etc.)
```

### Layer 6: Host Integration
```
Host/
├── ConsoleInterface.cs        // REPL and console I/O
├── FileSystem.cs             // Host file system access
├── CommandLineParser.cs      // CLI argument processing
└── Debugger.cs               // Debugging support
```

## Key Abstractions

### 1. Object-Oriented Pointer (OOP)
```csharp
public readonly struct OOP
{
    private readonly uint value;
    
    public bool IsSmallInteger => (value & 1) == 1;
    public bool IsObjectPointer => (value & 1) == 0;
    public int SmallIntegerValue => (int)(value >> 1);
    public uint ObjectIndex => value >> 1;
}
```

### 2. Object Memory Layout
```csharp
public sealed class ObjectLayout
{
    public ObjectHeader Header { get; init; }
    public OOP[] Fields { get; init; }
    
    // No direct class pointer - it's in the header
    // No null class pointers ever allowed
}

public sealed class ObjectHeader
{
    public OOP ClassOOP { get; init; }  // NEVER null/zero
    public int Size { get; init; }
    public int Hash { get; init; }
    public byte GCFlags { get; init; }
}
```

### 3. Clean Bootstrap Phases

**Phase 0: VM Primitives Only**
- Allocate raw memory blocks
- No class pointers yet

**Phase 1: Stub Allocation**
- Allocate ALL class objects as stubs
- Use temporary class indices

**Phase 2: Class Pointer Wiring**
- Wire ALL class pointers in single pass
- Object -> Object class -> Metaclass class
- Metaclass -> Metaclass class

**Phase 3: Superclass Chains**
- Wire superclass pointers
- Ensure metaclass parallel hierarchy

**Phase 4: Class Names**
- Symbol class now has valid class pointer
- Intern all class names

**Phase 5: Instance Variables**
- Array class now has valid class pointer
- Create instance variable arrays

**Phase 6: Method Dictionaries**
- MethodDictionary class now has valid class pointer
- Create method dictionaries

**Phase 7: Method Installation**
- CompiledMethod class now has valid class pointer
- Install all methods

**Phase 8: System Dictionary**
- Create Smalltalk global
- Register all classes

**Phase 9: Special Objects Array**
- Populate VM roots
- Enable GC

**Phase 10: Compiler Bootstrap**
- Load Scanner, Parser, Compiler classes
- Enable source code compilation

**Phase 11: Self-Hosting**
- Load remaining system via FileIn
- System fully self-hosting

## Memory Safety Guarantees

1. **No Null Class Pointers**: Every object allocation requires a valid class OOP
2. **Multi-Pass Bootstrap**: Classes created in phases to avoid forward references
3. **Special Objects Array**: VM-maintained GC roots, never in Smalltalk memory
4. **Deferred Operations**: Operations requiring unavailable types are queued

## Clean VM/Image Boundary

**VM Provides (C#)**:
- Memory allocation primitives
- Bytecode execution
- Special Objects Array
- ONE class creation primitive
- File I/O primitives

**Image Provides (Smalltalk)**:
- Class creation logic (Behavior>>subclass:...)
- Compiler
- System organization
- All application logic

## Implementation Order

1. Start with ObjectModel - pure data structures
2. Add Memory layer - allocation without semantics
3. Implement minimal Primitives for bootstrap
4. Build Bootstrap infrastructure
5. Create VM layer connecting components
6. Add Host integration last

This architecture ensures:
- No circular dependencies
- Clean bootstrap from bare metal
- Zero null class pointers
- Clear VM/image separation
- Maintainable and testable code