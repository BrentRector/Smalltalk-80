# Smalltalk-80 Interpreter Architecture

## Overview

The Smalltalk-80 interpreter is a clean, modern implementation in C# 13/.NET 9.0 that provides a faithful execution environment for the Blue Book specification. The architecture uses modern C# features while maintaining complete compatibility with Smalltalk-80 bytecode semantics.

## Core Design Principles

1. **Simplicity First**: Clean, understandable code over premature optimization
2. **Modern C#**: Leverage C# 13 features like primary constructors, pattern matching, and nullable reference types
3. **Bytecode Compatible**: 100% compatibility with Smalltalk-80 bytecode specification
4. **Extensible**: Easy to add new primitives and extend functionality
5. **Debuggable**: Clear execution flow with comprehensive tracing support

## Architecture Layers

### 1. Command-Line Interface

The interpreter provides a comprehensive CLI using System.CommandLine:

```csharp
// Program.cs - Entry point with command handling
Commands:
  run <file>         Execute a Smalltalk file or expression
  repl               Start interactive REPL
  compile <file>     Compile Smalltalk source to bytecode
  inspect <file>     Inspect a system image or compiled method
  cold-start         Bootstrap from minimal kernel
  eval <expression>  Evaluate a Smalltalk expression

Global Options:
  -v, --verbose      Enable verbose output
  --cold-start-log   Enable cold start logging messages
  --gc-log          Enable garbage collection logging
```

### 2. Virtual Machine Core

The `VirtualMachine` class orchestrates all interpreter components:

```csharp
namespace Smalltalk80.Interpreter;

public class VirtualMachine
{
    private readonly Dictionary<string, SClass> classes = [];
    private readonly Dictionary<string, SObject> globals = [];
    private readonly BytecodeInterpreter interpreter;
    private readonly Compiler compiler;
    
    public void ColdStart()
    {
        // Create core classes and metaclass hierarchy
        CreateCoreClasses();
        // Register primitives
        PrimitiveDispatcher.RegisterAll();
    }
    
    public SObject ExecuteExpression(string expression)
    {
        // Simple expression evaluation
        var result = compiler.CompileAndExecute(expression);
        return result;
    }
}
```

## Object Model

### 3.1 Base Object System

**FUNDAMENTAL REQUIREMENT**: All Smalltalk objects must be managed uniformly by the Smalltalk garbage collector. This includes:
- Regular instances (SInstance)
- Method contexts (SMethodContext) 
- Compiled methods (SCompiledMethod)
- Classes and metaclasses (SClass)

The object model uses a C# base class hierarchy but ALL objects are created through Smalltalk instantiation:

```csharp
namespace Smalltalk80.Interpreter.Core.Objects;

// Base class for all Smalltalk objects
public abstract class SObject
{
    public virtual SClass Class { get; set; }
    public virtual int Size => 0;
    public virtual bool IsIndexable => false;
    
    // Object identity and comparison
    public virtual int IdentityHash() => RuntimeHelpers.GetHashCode(this);
    public virtual bool IsIdenticalTo(SObject other) => ReferenceEquals(this, other);
}

// Regular Smalltalk instance
public class SInstance(SClass cls, int size = 0) : SObject
{
    private readonly object?[] fields = new object?[size];
    public override SClass Class { get; set; } = cls;
    
    public object? GetField(int index) => 
        index >= 0 && index < fields.Length ? fields[index] : null;
        
    public void SetField(int index, object? value)
    {
        if (index >= 0 && index < fields.Length)
            fields[index] = value;
    }
}
```

### 3.2 Class System

Classes and metaclasses form the foundation of the type system:

```csharp
public class SClass : SObject
{
    public string Name { get; set; }
    public SClass? Superclass { get; set; }
    public int InstanceSize { get; set; }
    public Dictionary<string, CompiledMethod> Methods { get; } = [];
    
    public SObject CreateInstance()
    {
        // Create the appropriate C# wrapper but ensure it's registered with GC
        SObject instance = Name switch
        {
            "Array" => new SArray(this, 0),
            "String" => new SString(this, ""),
            "Symbol" => new SSymbol(this, ""),
            "CompiledMethod" => new SCompiledMethod(this),
            "MethodContext" => new SMethodContext(this),
            _ => new SInstance(this, InstanceSize)
        };
        
        // Register with GC immediately
        if (VirtualMachine.Current?.GarbageCollector != null)
        {
            VirtualMachine.Current.GarbageCollector.RegisterObject(instance);
        }
        
        return instance;
    }
    
    public CompiledMethod? LookupMethod(string selector)
    {
        // Search this class
        if (Methods.TryGetValue(selector, out var method))
            return method;
            
        // Search superclass chain
        return Superclass?.LookupMethod(selector);
    }
}
```

### 3.3 Value Types (Immediates)

SmallInteger and Character are implemented as tagged immediate values:

```csharp
namespace Smalltalk80.Interpreter.Core.Values;

public class SmallInteger : SObject
{
    public int Value { get; }
    private static readonly SClass SmallIntegerClass = new("SmallInteger", null, 0);
    
    public SmallInteger(int value) => Value = value;
    public override SClass Class => SmallIntegerClass;
    
    // Arithmetic operations
    public static SmallInteger Add(SmallInteger a, SmallInteger b) => 
        new(a.Value + b.Value);
    
    public static SmallInteger Subtract(SmallInteger a, SmallInteger b) => 
        new(a.Value - b.Value);
        
    // Comparisons return Smalltalk booleans
    public static SObject LessThan(SmallInteger a, SmallInteger b) => 
        a.Value < b.Value ? STrue.Instance : SFalse.Instance;
}

public class Character : SObject
{
    public char Value { get; }
    private static readonly SClass CharacterClass = new("Character", null, 0);
    
    public Character(char value) => Value = value;
    public override SClass Class => CharacterClass;
}
```

### 3.4 Collection Types

Arrays and Strings provide indexed storage:

```csharp
public class SArray : SObject
{
    private readonly SObject?[] elements;
    
    public SArray(SClass cls, int size)
    {
        Class = cls;
        elements = new SObject?[size];
        // Initialize with nil
        for (int i = 0; i < size; i++)
            elements[i] = SNil.Instance;
    }
    
    public override int Size => elements.Length;
    public override bool IsIndexable => true;
    
    public SObject At(int index) => 
        index > 0 && index <= elements.Length 
            ? elements[index - 1] ?? SNil.Instance 
            : SNil.Instance;
            
    public void AtPut(int index, SObject value)
    {
        if (index > 0 && index <= elements.Length)
            elements[index - 1] = value;
    }
}

public class SString : SObject
{
    public string Value { get; set; }
    
    public SString(SClass cls, string value)
    {
        Class = cls;
        Value = value;
    }
    
    public override int Size => Value.Length;
    public override bool IsIndexable => true;
}
```

## Bytecode Execution

### 4.1 Bytecode Format

The interpreter implements the complete Blue Book bytecode specification:

| Range | Operation | Description |
|-------|-----------|-------------|
| 0-15 | Push Receiver Variable | Push instance variable 0-15 |
| 16-31 | Push Temporary Variable | Push temporary/argument 0-15 |
| 32-63 | Push Literal Constant | Push literal 0-31 |
| 64-95 | Push Literal Variable | Push value of Association 0-31 |
| 96-103 | Pop and Store Receiver Variable | Pop stack into instance variable 0-7 |
| 104-111 | Pop and Store Temporary Variable | Pop stack into temporary 0-7 |
| 112-119 | Push Special | receiver, true, false, nil, -1, 0, 1, 2 |
| 120-127 | Return | Return receiver, true, false, nil, stack top, block value |
| 128-175 | Extended Operations | Jump, conditional jump, send super |
| 176-255 | Send | Send literal selector with 0-2 arguments |

### 4.2 Bytecode Interpreter  

The bytecode interpreter executes compiled methods using proper Smalltalk objects:

```csharp
namespace Smalltalk80.Interpreter.Execution;

public class BytecodeInterpreter
{
    private readonly VirtualMachine vm;
    
    public SObject Execute(SCompiledMethod method, SObject receiver, SObject[] args)
    {
        // Create execution context as a proper Smalltalk object
        var contextClass = SystemDictionary.LookupClass("MethodContext");
        var contextInstance = contextClass.CreateInstance();
        
        // Initialize through proper Smalltalk object protocol
        var context = SMethodContext.InitializeFrom(contextInstance, method, receiver, args);
        
        // Copy arguments to temporaries
        for (int i = 0; i < args.Length; i++)
            context.Temporaries[i] = args[i];
            
        // Execute bytecodes
        while (context.PC < method.Bytecodes.Length)
        {
            var bytecode = method.Bytecodes[context.PC++];
            ExecuteBytecode(context, bytecode);
        }
        
        return context.Stack.Count > 0 ? context.Stack.Pop() : SNil.Instance;
    }
    
    private void ExecuteBytecode(MethodContext context, byte bytecode)
    {
        switch (bytecode)
        {
            // Push operations
            case >= 0 and <= 15:
                // Push receiver variable
                PushReceiverVariable(context, bytecode & 0x0F);
                break;
                
            case >= 16 and <= 31:
                // Push temporary variable
                PushTemporary(context, bytecode & 0x0F);
                break;
                
            case >= 32 and <= 63:
                // Push literal constant
                PushLiteral(context, bytecode & 0x1F);
                break;
                
            // Special constants
            case 112: context.Stack.Push(context.Receiver); break;
            case 113: context.Stack.Push(STrue.Instance); break;
            case 114: context.Stack.Push(SFalse.Instance); break;
            case 115: context.Stack.Push(SNil.Instance); break;
            case 116: context.Stack.Push(new SmallInteger(-1)); break;
            case 117: context.Stack.Push(new SmallInteger(0)); break;
            case 118: context.Stack.Push(new SmallInteger(1)); break;
            case 119: context.Stack.Push(new SmallInteger(2)); break;
            
            // Return operations
            case 120: Return(context, context.Receiver); break;
            case 121: Return(context, STrue.Instance); break;
            case 122: Return(context, SFalse.Instance); break;
            case 123: Return(context, SNil.Instance); break;
            case 124: Return(context, context.Stack.Pop()); break;
            
            // Send operations
            case >= 176 and <= 255:
                ExecuteSend(context, bytecode);
                break;
        }
    }
}
```

### 4.3 Method Context

**CRITICAL DESIGN REQUIREMENT**: Method contexts MUST be proper Smalltalk objects managed by the Smalltalk garbage collector, NOT C# objects. This ensures uniform object model and proper memory management.

Method contexts are Smalltalk objects created through the standard instantiation mechanism:

```csharp
// CORRECT: Create through Smalltalk class instantiation
public static SMethodContext CreateMethodContext(SCompiledMethod method, SObject receiver, SObject[] arguments)
{
    var contextClass = SystemDictionary.LookupClass("MethodContext");
    var context = (SMethodContext)contextClass.CreateInstance();
    context.Initialize(method, receiver, arguments);
    
    // NO GC registration - objects are reachable through process/context chains
    
    return context;
}

// INCORRECT: Direct C# instantiation (violates object model)
// var context = new SMethodContext(contextClass, method, receiver, arguments);
```

#### Blue Book Specification

From the Blue Book page 580, MethodContext has these instance variables:
- sender: The context that sent the message (or nil)
- instructionPointer: The current bytecode position
- stackPointer: The current stack position  
- method: The CompiledMethod being executed
- receiver: The receiver of the message
- temporaries and stack: Combined area for arguments, temps, and evaluation stack

#### Reachability and GC Integration

Method contexts form a chain reachable from the Process scheduler:
```
SpecialObjectsArray[3] -> ProcessorScheduler
    -> activeProcess -> suspendedContext
        -> sender -> sender -> ... (context chain)
```

This ensures contexts are properly traced during garbage collection.

### 4.4 Compiled Method

**CRITICAL DESIGN REQUIREMENT**: CompiledMethods MUST also be proper Smalltalk objects, not C# objects.

#### Blue Book Specification

CompiledMethod contains:
- header: Method flags and counts (literals, temps, args, stack size)
- literals: Array of literal objects and special bindings
- bytecodes: The bytecode instructions
- sourcePointer: Reference to source code (optional)

#### Proper Creation

```csharp
// CORRECT: Create through Smalltalk instantiation
public static SCompiledMethod CreateCompiledMethod(byte[] bytecodes, SArray literals, 
    int numArgs, int numTemps, int stackSize)
{
    var methodClass = SystemDictionary.LookupClass("CompiledMethod");
    var method = (SCompiledMethod)methodClass.CreateInstance();
    
    // Initialize through proper protocol
    method.InitializeBytecodes(bytecodes);
    method.InitializeLiterals(literals);
    method.InitializeHeader(numArgs, numTemps, stackSize);
    
    // NO GC registration - objects are reachable through class method dictionaries
    
    return method;
}

// INCORRECT: Direct C# instantiation
// var method = new SCompiledMethod(bytecodes, literals);
```

#### Method Dictionary Storage

Methods are stored in class method dictionaries and are reachable through:
```
Class -> methodDictionary -> CompiledMethod instances
Metaclass -> methodDictionary -> CompiledMethod instances (class methods)
```

## Primitive Operations

### 5.1 Primitive Dispatcher

Primitives provide low-level operations that cannot be implemented in Smalltalk:

```csharp
namespace Smalltalk80.Interpreter.Execution;

public static class PrimitiveDispatcher
{
    private static readonly Dictionary<int, PrimitiveHandler> primitives = [];
    
    public delegate bool PrimitiveHandler(SObject receiver, SObject[] args, out SObject? result);
    
    public static void RegisterAll()
    {
        // Arithmetic primitives
        Register(1, PrimitiveAdd);
        Register(2, PrimitiveSubtract);
        Register(3, PrimitiveLessThan);
        Register(4, PrimitiveGreaterThan);
        Register(5, PrimitiveLessOrEqual);
        Register(6, PrimitiveGreaterOrEqual);
        Register(7, PrimitiveEqual);
        Register(8, PrimitiveNotEqual);
        Register(9, PrimitiveMultiply);
        Register(10, PrimitiveDivide);
        
        // Object primitives
        Register(60, PrimitiveAt);
        Register(61, PrimitiveAtPut);
        Register(62, PrimitiveSize);
        Register(70, PrimitiveNew);
        Register(71, PrimitiveNewWithArg);
        Register(75, PrimitiveIdentityHash);
        Register(110, PrimitiveIdentical);
        Register(111, PrimitiveClass);
    }
    
    public static bool ExecutePrimitive(int index, SObject receiver, SObject[] args, out SObject? result)
    {
        if (primitives.TryGetValue(index, out var handler))
            return handler(receiver, args, out result);
            
        result = null;
        return false;
    }
}
```

### 5.2 Arithmetic Primitives

SmallInteger arithmetic with overflow handling:

```csharp
private static bool PrimitiveAdd(SObject receiver, SObject[] args, out SObject? result)
{
    if (receiver is SmallInteger a && 
        args.Length == 1 && 
        args[0] is SmallInteger b)
    {
        // Check for overflow
        long sum = (long)a.Value + b.Value;
        if (sum >= int.MinValue && sum <= int.MaxValue)
        {
            result = new SmallInteger((int)sum);
            return true;
        }
    }
    
    result = null;
    return false;  // Primitive failed, fall back to Smalltalk method
}
```

## Compiler Integration

### 6.1 Simple Expression Compiler

The compiler provides basic expression compilation:

```csharp
namespace Smalltalk80.Interpreter;

public class Compiler
{
    private readonly VirtualMachine vm;
    
    public SObject CompileAndExecute(string expression)
    {
        // Parse simple binary expressions (e.g., "3 + 4")
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 3 && int.TryParse(parts[0], out int left) && 
            int.TryParse(parts[2], out int right))
        {
            var leftObj = new SmallInteger(left);
            var rightObj = new SmallInteger(right);
            var selector = parts[1];
            
            // Execute primitive or method
            return vm.Send(leftObj, selector, [rightObj]);
        }
        
        throw new CompilationException($"Cannot compile: {expression}");
    }
}
```

## Memory Management

### 7.1 Object Memory

The interpreter uses a **table-based object memory** with explicit object pointers (OOPs) and structured object data. All Smalltalk data structures are maintained in the Smalltalk heap, not as C# proxy objects.

```csharp
namespace Smalltalk80.Interpreter.Memory;

public static class ObjectMemoryNew
{
    // Object table maps OOP to object data
    private static readonly Dictionary<nint, ObjectData> objectTable = [];

    // Symbol table is in HEAP (Symbol class -> classPool -> #USTable), not C#
    // During bootstrap only, we use a temporary C# table:
    private static readonly Dictionary<string, nint> bootstrapSymbolTable = new();

    public static SObjectRef InternSymbol(string str)
    {
        // Check if USTable exists in heap
        var usTableRef = SymbolTableBuilder.GetUSTable();

        if (usTableRef.HasValue && !usTableRef.Value.IsNil)
        {
            // Use heap-based symbol table (Smalltalk object)
            return InternSymbolInUSTable(str, usTableRef.Value);
        }
        else
        {
            // Bootstrap only - use temporary C# table
            return InternSymbolBootstrap(str);
        }
    }
}
```

### 7.2 Garbage Collection

**CRITICAL DESIGN RULE**: The GC traces all live objects from the **Special Objects Array (SOA)**. All Smalltalk objects must be reachable from one of the 5 GC roots.

#### Blue Book Minimal GC Root Specification

Per Goldberg & Robson (1983), only **5 roots** are needed for complete reachability:

```csharp
public static class SpecialObjectsArray
{
    // Singletons (required for hardwired bytecode references)
    public const int NilIndex = 0;              // nil
    public const int FalseIndex = 1;            // false
    public const int TrueIndex = 2;             // true

    // Main GC root for all classes, methods, symbols, and globals
    public const int SystemDictionaryIndex = 3; // Smalltalk global dictionary

    // Process scheduler and all live execution contexts
    public const int ProcessorSchedulerIndex = 4; // ProcessorScheduler singleton

    public const int Count = 5;
}
```

#### Complete Reachability Graph

From these 5 roots, **ALL objects** are reachable:

**1. nil, true, false** (indices 0-2)
   - Singletons required for bytecode operations (pushNil, pushTrue, pushFalse)

**2. SystemDictionary** (index 3) - Root of class hierarchy and symbols
```
SystemDictionary (Smalltalk global)
  ├─> Association(#Object -> Object class)
  │    └─> methodDict -> Associations -> CompiledMethod instances
  ├─> Association(#String -> String class)
  ├─> Association(#Symbol -> Symbol class)
  │    └─> classPool (Dictionary at slot 7)
  │         └─> Association(#USTable -> Symbol table Array)
  │              └─> Symbol buckets -> ALL Symbol instances
  ├─> Association(#Character -> Character class)
  │    └─> classPool
  │         └─> Association(#CharacterTable -> Array[256])
  │              └─> ALL Character instances (0-255)
  └─> ALL other classes, globals, and class-side methods
```

**3. ProcessorScheduler** (index 4) - Root of all live processes and contexts
```
ProcessorScheduler
  ├─> activeProcess -> Process
  │    └─> suspendedContext -> MethodContext
  │         └─> sender -> MethodContext chain
  │              ├─> receiver
  │              ├─> method (CompiledMethod)
  │              └─> temps/stack (live objects)
  └─> quiescentProcessLists -> Array[100] of LinkedLists
       └─> Process chains (runnable/waiting processes)
            └─> suspended contexts
```

#### Garbage Collector Implementation

```csharp
public class GarbageCollectorNew
{
    public GCStatistics Collect()
    {
        // Mark phase - trace from 5 GC roots ONLY
        var marked = new HashSet<nint>();

        for (int i = 0; i < SpecialObjectsArray.Count; i++)
        {
            var root = SpecialObjectsArray.Get(i);
            if (root != null && !root.IsNil)
            {
                MarkObject(root.AsObjectPointer(), marked);
            }
        }

        // Sweep phase - collect unmarked objects
        int collected = 0;
        var allOops = ObjectTable.Keys.ToList();

        foreach (var oop in allOops)
        {
            if (!marked.Contains(oop))
            {
                ObjectTable.Remove(oop);
                collected++;
            }
        }

        return new GCStatistics { ObjectsCollected = collected };
    }

    private void MarkObject(nint oop, HashSet<nint> marked)
    {
        if (marked.Contains(oop))
            return;

        marked.Add(oop);

        var data = ObjectMemoryNew.GetObjectData(oop);

        // Trace instance variables
        foreach (var fieldRef in data.InstanceVariables)
        {
            if (fieldRef.IsHeapObject)
                MarkObject(fieldRef.AsObjectPointer(), marked);
        }

        // Trace indexed pointers (arrays, method literals, etc.)
        foreach (var indexedRef in data.IndexedPointers)
        {
            if (indexedRef.IsHeapObject)
                MarkObject(indexedRef.AsObjectPointer(), marked);
        }
    }
}
```

### 7.3 Heap-Based Symbol Table

**CRITICAL ARCHITECTURE PRINCIPLE**: The symbol table (USTable) is a **Smalltalk heap object**, NOT a C# proxy structure.

#### Why Heap-Based Symbols Matter

When Smalltalk code runs (e.g., `Symbol intern: 'newSelector'`), it updates the USTable directly in the Smalltalk heap. If symbols were stored in a C# dictionary:
- ❌ Smalltalk code updates would be invisible to C#
- ❌ C# code updates would be invisible to Smalltalk
- ❌ Two separate symbol tables would diverge immediately

#### Implementation

```csharp
// Symbol class has classVariable 'USTable' per Blue Book
Symbol class
  -> classPool (Dictionary at slot 7)
      -> Association(#USTable -> Array[256] of symbol buckets)
           -> Each bucket is an Array of Symbol instances

// Symbol interning checks heap USTable
public static SObjectRef InternSymbol(string str)
{
    var usTableRef = SymbolTableBuilder.GetUSTable();

    if (usTableRef.HasValue && !usTableRef.Value.IsNil)
    {
        // Search heap-based USTable
        return InternSymbolInUSTable(str, usTableRef.Value);
    }
    else
    {
        // Bootstrap only - before USTable created
        return InternSymbolBootstrap(str);
    }
}

// Smalltalk code: Symbol intern: 'foo'
// This updates the SAME USTable object in heap that C# reads
```

#### Bootstrap Process

1. **Early Bootstrap**: Use temporary C# `bootstrapSymbolTable` to create initial symbols during class creation
2. **Create USTable**: After Symbol class exists, create USTable as Smalltalk Array in heap
3. **Migration**: Copy all bootstrap symbols into USTable buckets
4. **Switch Over**: All subsequent symbol interning uses heap-based USTable
5. **Clear Bootstrap Table**: Delete C# temporary table

```csharp
// In SystemDictionary.Initialize() after creating Symbol class:
var usTable = SymbolTableBuilder.CreateUSTable();          // Smalltalk Array[256]
SymbolTableBuilder.InstallUSTable(usTable);                // Put in Symbol.classPool
ObjectMemoryNew.MigrateBootstrapSymbolsToUSTable();        // Move existing symbols
bootstrapSymbolTable.Clear();                               // Delete C# temporary
```

### 7.4 Special Objects

Singleton objects with fixed identity:

```csharp
public sealed class SNil : SObject
{
    public static readonly SNil Instance = new();
    private SNil() { }
    public override string ToString() => "nil";
}

public sealed class STrue : SObject
{
    public static readonly STrue Instance = new();
    private STrue() { }
    public override string ToString() => "true";
}

public sealed class SFalse : SObject
{
    public static readonly SFalse Instance = new();
    private SFalse() { }
    public override string ToString() => "false";
}
```

## Bootstrap Process

### 8.1 Cold Start

The cold start creates the minimal kernel classes:

```csharp
private void CreateCoreClasses()
{
    // Create Object (the root of the hierarchy)
    var objectClass = new SClass("Object", null, 0);
    
    // Create Class and Metaclass (circular dependency)
    var classClass = new SClass("Class", objectClass, 5);
    var metaclassClass = new SClass("Metaclass", classClass, 1);
    
    // Wire up metaclass relationships
    objectClass.Class = metaclassClass;
    classClass.Class = metaclassClass;
    metaclassClass.Class = metaclassClass;
    
    // Create primitive types
    var smallIntClass = new SClass("SmallInteger", objectClass, 0);
    var charClass = new SClass("Character", objectClass, 0);
    
    // Create collections
    var arrayClass = new SClass("Array", objectClass, 0);
    var stringClass = new SClass("String", objectClass, 0);
    var symbolClass = new SClass("Symbol", stringClass, 0);
    
    // Create method infrastructure
    var compiledMethodClass = new SClass("CompiledMethod", objectClass, 0);
    var methodContextClass = new SClass("MethodContext", objectClass, 0);
    
    // Create boolean hierarchy
    var booleanClass = new SClass("Boolean", objectClass, 0);
    var trueClass = new SClass("True", booleanClass, 0);
    var falseClass = new SClass("False", booleanClass, 0);
    
    // Create nil's class
    var undefinedClass = new SClass("UndefinedObject", objectClass, 0);
    
    // Register all classes
    RegisterClass(objectClass);
    RegisterClass(classClass);
    RegisterClass(metaclassClass);
    // ... register remaining classes
}
```

### 8.2 Class Registration

Classes are registered in the global namespace:

```csharp
private void RegisterClass(SClass cls)
{
    classes[cls.Name] = cls;
    
    // Create global binding for the class
    globals[cls.Name] = cls;
    
    // Set up class-side methods
    if (cls.Name == "Object")
    {
        cls.Methods["new"] = CreatePrimitiveMethod(70);
        cls.Methods["class"] = CreatePrimitiveMethod(111);
        cls.Methods["=="] = CreatePrimitiveMethod(110);
    }
}
```

## Architectural Principles

### Uniform Object Model

1. **Everything is a Smalltalk Object**: All runtime entities including contexts and methods
2. **Single GC Management**: One garbage collector manages all objects uniformly
3. **Proper Reachability**: All live objects reachable from known roots
4. **No Hybrid Management**: Never mix C# and Smalltalk object lifecycle

### Object Creation Protocol

```csharp
// Universal pattern for creating ANY Smalltalk object:
public static T CreateSmalltalkObject<T>(string className, Action<T> initializer) where T : SObject
{
    var cls = SystemDictionary.LookupClass(className) 
        ?? throw new InvalidOperationException($"Class {className} not found");
    
    var instance = (T)cls.CreateInstance();
    initializer?.Invoke(instance);
    
    // NO GC registration - objects are reachable through references only
    
    return instance;
}
```

## Design Violations to Avoid

### ❌ Common Mistakes

1. **Creating Smalltalk objects with `new`**: 
   - WRONG: `new SMethodContext(class, method, receiver, args)`
   - RIGHT: `contextClass.CreateInstance()` then initialize

2. **Not registering objects with GC**:
   - WRONG: Create object and use immediately
   - RIGHT: Create, register with GC, then use

3. **Mixing C# and Smalltalk object management**:
   - WRONG: C# manages contexts, Smalltalk manages other objects
   - RIGHT: All objects uniformly managed by Smalltalk GC

4. **Context pooling outside of Smalltalk**:
   - WRONG: C# object pool for contexts
   - RIGHT: Smalltalk manages context lifecycle and reuse

5. **Forgetting GC registration**:
   - WRONG: Create object, forget to register
   - RIGHT: Registration happens in CreateInstance or immediately after

6. **Breaking reachability chains**:
   - WRONG: Contexts not reachable from Process
   - RIGHT: Process -> suspendedContext -> sender chain maintained

## Implementation Status

### ✅ Completed Features

**Core Object Model**
- [x] SObject base class with identity
- [x] SClass with method dictionaries
- [x] SmallInteger and Character immediates
- [x] Array, String, Symbol collections
- [x] Boolean hierarchy (true/false)
- [x] Nil singleton

**Bytecode Interpreter**
- [x] Basic bytecode execution
- [x] Push/pop operations
- [x] Special constants
- [x] Return operations
- [x] Simple send operations

**Primitives**
- [x] SmallInteger arithmetic (+, -, *, /)
- [x] SmallInteger comparison (<, >, <=, >=, =, ~=)
- [x] Object access (at:, at:put:, size)
- [x] Object creation (new, new:)
- [x] Identity operations (==, class, identityHash)

**Command Interface**
- [x] REPL with expression evaluation
- [x] Cold start bootstrap
- [x] File execution
- [x] Expression evaluation
- [x] Verbose/debug modes

### 🚧 In Progress

**Compiler**
- [ ] Full Smalltalk parser
- [ ] AST to bytecode compilation
- [ ] Method compilation
- [ ] Block compilation

**Advanced Bytecode**
- [ ] Extended bytecodes (128-175)
- [ ] Super sends
- [ ] Block operations
- [ ] Jump instructions

**Memory Management**
- [ ] Garbage collection
- [ ] Image persistence
- [ ] Snapshot/restore

### ❌ Not Implemented

**Advanced Features**
- [ ] Process scheduling (contexts must be reachable from Process)
- [ ] Semaphores
- [ ] File I/O primitives
- [ ] Display/graphics primitives
- [ ] Float arithmetic
- [ ] LargeInteger support

## Testing

### Running the Interpreter

```bash
# Build the project
dotnet build

# Run the REPL
dotnet run -- repl

# Evaluate an expression
dotnet run -- eval "3 + 4"

# Cold start with class listing
dotnet run -- cold-start --print-classes

# Run with verbose output
dotnet run -- -v eval "5 * 6"
```

### Example REPL Session

```
Smalltalk-80 REPL
Type 'exit' to quit

st> 3 + 4
=> 7

st> 5 - 2
=> 3

st> 10 * 3
=> 30

st> 15 / 3
=> 5

st> exit
```

## Performance Considerations

### Current Focus
- **Correctness over speed**: Ensure proper Smalltalk semantics
- **Clean code**: Maintainable and understandable implementation
- **Debuggability**: Clear execution flow with good error messages

### Future Optimizations
- **Inline caching**: Cache method lookups at call sites
- **Tagged pointers**: Use pointer tagging for immediates
- **JIT compilation**: Compile hot methods to native code
- **Arena allocation**: Use arena allocators for short-lived objects

## Architecture Benefits

### Simplicity
- Clean separation between VM, objects, and execution
- Standard C# objects with Smalltalk semantics
- No complex OOP indirection or handle tables

### Maintainability
- Modern C# 13 with nullable reference types
- Clear ownership and lifetime management
- Comprehensive XML documentation

### Extensibility
- Easy to add new primitive operations
- Simple to extend object types
- Clear plugin points for optimization

## Summary

The Smalltalk-80 interpreter provides a clean, modern implementation that balances simplicity with correctness. The architecture prioritizes understandable code and proper Smalltalk semantics while laying the groundwork for future optimizations. With its comprehensive command-line interface, working REPL, and extensible primitive system, it serves as both a learning tool and a foundation for a complete Smalltalk-80 system.