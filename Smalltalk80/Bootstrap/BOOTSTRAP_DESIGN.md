# Clean Multi-Pass Bootstrap Design for Smalltalk-80 Interpreter

## Overview
This document describes a clean, multi-pass bootstrap approach that creates all kernel classes before any Smalltalk code runs, ensuring no objects are created with null class pointers. The bootstrap progresses through distinct phases:

**VM Primitives → Stub Allocation → Wiring → Root Anchoring → File I/O → Compiler → Self-Hosting**

## Core Principles
1. **No objects without class pointers** - Every object created must have a valid class pointer
2. **Deferred operations** - Operations requiring types that don't exist yet are deferred
3. **Multi-pass construction** - Classes are built in phases to handle circular dependencies
4. **Clean separation** - C#/VM primitives handle structure, Smalltalk handles behavior
5. **Ordered phases** - Each phase enables the next, building toward self-hosting

## Phase 0: Kernel Class Specifications

Define all kernel classes with their metadata:

```csharp
public class KernelClassSpec
{
    public string Name { get; set; }
    public string SuperName { get; set; }  // null for Object
    public int FixedInstVars { get; set; }
    public bool IsIndexable { get; set; }
    public bool IsBytes { get; set; }  // true = bytes, false = pointers
    public List<string> InstVarNames { get; set; }
    public List<string> ClassVarNames { get; set; }
    public List<string> PoolNames { get; set; }
    public string Category { get; set; }
}
```

Kernel classes in dependency order:
1. **Object** (root)
2. **Behavior** → Object
3. **ClassDescription** → Behavior  
4. **Class** → ClassDescription
5. **Metaclass** → ClassDescription
6. **UndefinedObject** → Object
7. **Boolean** → Object
8. **True** → Boolean
9. **False** → Boolean
10. **Magnitude** → Object
11. **Number** → Magnitude
12. **Integer** → Number
13. **SmallInteger** → Integer
14. **LookupKey** → Magnitude
15. **Association** → LookupKey
16. **Link** → Object
17. **Process** → Link
18. **Collection** → Object
19. **SequenceableCollection** → Collection
20. **ArrayedCollection** → SequenceableCollection
21. **Array** → ArrayedCollection
22. **ByteArray** → ArrayedCollection
23. **String** → ArrayedCollection
24. **Symbol** → String
25. **Set** → Collection
26. **Dictionary** → Set
27. **IdentityDictionary** → Dictionary
28. **SystemDictionary** → Dictionary
29. **MethodDictionary** → IdentityDictionary
30. **CompiledMethod** → Object
31. **MethodContext** → Object
32. **BlockContext** → Object

## Phase 1: Stub Allocation

Create bare class and metaclass objects without any initialization:

```csharp
public void Phase1_StubAllocation(List<KernelClassSpec> kernelSpecs)
{
    foreach (var spec in kernelSpecs)
    {
        // Find superclass OOP (nil for Object)
        int superOop = spec.SuperName == null 
            ? vm.PrimitiveNil()
            : classRegistry[spec.SuperName];
        
        // Create bare class and metaclass
        int classOop = vm.PrimitiveNewClass();
        int metaclassOop = vm.PrimitiveNewMetaclass();
        
        // Set minimal relationships
        var classObj = ObjectMemory.FromOop(classOop);
        var metaclassObj = ObjectMemory.FromOop(metaclassOop);
        
        // Class is instance of its metaclass
        classObj.ClassPointer = metaclassObj;
        
        // Metaclass's thisClass points to class
        metaclassObj.Fields[5] = classObj;
        
        // Store in registry
        classRegistry[spec.Name] = classOop;
        metaclassRegistry[spec.Name] = metaclassOop;
    }
}
```

## Phase 2: Class Wiring

Now that all classes exist, we can reference them safely:

```csharp
public void Phase2_WireClasses(List<KernelClassSpec> kernelSpecs)
{
    // First pass: Set superclass relationships
    foreach (var spec in kernelSpecs)
    {
        var classOop = classRegistry[spec.Name];
        var classObj = ObjectMemory.FromOop(classOop);
        
        if (spec.SuperName != null)
        {
            var superOop = classRegistry[spec.SuperName];
            classObj.Fields[0] = ObjectMemory.FromOop(superOop); // superclass
        }
    }
    
    // Second pass: Set metaclass relationships
    foreach (var spec in kernelSpecs)
    {
        var metaclassOop = metaclassRegistry[spec.Name];
        var metaclassObj = ObjectMemory.FromOop(metaclassOop);
        
        // Metaclass superclass is superclass's metaclass
        if (spec.SuperName != null)
        {
            var superMetaOop = metaclassRegistry[spec.SuperName];
            metaclassObj.Fields[0] = ObjectMemory.FromOop(superMetaOop);
        }
        
        // All metaclasses are instances of Metaclass
        if (metaclassRegistry.ContainsKey("Metaclass"))
        {
            metaclassObj.ClassPointer = ObjectMemory.FromOop(
                metaclassRegistry["Metaclass"]);
        }
    }
}
```

## Phase 3: Deferred Symbol Operations

Now that Symbol class exists, create all symbols and set class names:

```csharp
public void Phase3_ResolveSymbols(List<KernelClassSpec> kernelSpecs)
{
    var symbolClass = ObjectMemory.FromOop(classRegistry["Symbol"]);
    
    // Fix any symbols created before Symbol class existed
    foreach (var symbolOop in earlySymbols)
    {
        var symbolObj = ObjectMemory.FromOop(symbolOop);
        symbolObj.ClassPointer = symbolClass;
    }
    
    // Set class names
    foreach (var spec in kernelSpecs)
    {
        var classObj = ObjectMemory.FromOop(classRegistry[spec.Name]);
        var nameSymbol = vm.PrimitiveInternSymbol(spec.Name);
        classObj.Fields[5] = ObjectMemory.FromOop(nameSymbol); // name field
    }
}
```

## Phase 4: Deferred Array Operations

Now that Array class exists, set instance variables:

```csharp
public void Phase4_SetInstanceVariables(List<KernelClassSpec> kernelSpecs)
{
    foreach (var spec in kernelSpecs)
    {
        if (spec.InstVarNames.Count == 0) continue;
        
        var classObj = ObjectMemory.FromOop(classRegistry[spec.Name]);
        
        // Create array of instance variable names
        var ivArray = vm.PrimitiveNewArray(spec.InstVarNames.Count);
        for (int i = 0; i < spec.InstVarNames.Count; i++)
        {
            var ivSymbol = vm.PrimitiveInternSymbol(spec.InstVarNames[i]);
            vm.PrimitiveArrayAtPut(ivArray, i, ivSymbol);
        }
        
        classObj.Fields[3] = ObjectMemory.FromOop(ivArray);
    }
}
```

## Phase 5: Method Dictionaries

Create empty method dictionaries for all classes:

```csharp
public void Phase5_CreateMethodDictionaries(List<KernelClassSpec> kernelSpecs)
{
    var methodDictClass = ObjectMemory.FromOop(
        classRegistry["MethodDictionary"]);
    
    foreach (var spec in kernelSpecs)
    {
        var classObj = ObjectMemory.FromOop(classRegistry[spec.Name]);
        
        // Create empty method dictionary
        var methodDict = new SmalltalkObject(methodDictClass, 8);
        methodDict.Fields[0] = 1; // tally = 0 as SmallInteger
        
        classObj.Fields[1] = methodDict;
    }
}
```

## Phase 6: Global Registration

Register all classes in SystemDictionary:

```csharp
public void Phase6_RegisterGlobals(List<KernelClassSpec> kernelSpecs)
{
    // Create SystemDictionary instance
    var systemDictOop = vm.PrimitiveNewInstance(
        classRegistry["SystemDictionary"]);
    vm.PrimitiveDictionaryInitialize(systemDictOop, 200);
    
    // Register all classes
    foreach (var spec in kernelSpecs)
    {
        var classOop = classRegistry[spec.Name];
        var metaclassOop = metaclassRegistry[spec.Name];
        
        // Register class
        vm.PrimitiveDictionaryAtPut(
            systemDictOop,
            vm.PrimitiveInternSymbol(spec.Name),
            classOop);
        
        // Register metaclass as "ClassName class"
        vm.PrimitiveDictionaryAtPut(
            systemDictOop,
            vm.PrimitiveInternSymbol(spec.Name + " class"),
            metaclassOop);
    }
    
    // Register SystemDictionary as "Smalltalk"
    vm.PrimitiveDictionaryAtPut(
        systemDictOop,
        vm.PrimitiveInternSymbol("Smalltalk"),
        systemDictOop);
}
```

## Phase 7: Special Objects

Set up VM special objects array:

```csharp
public void Phase7_SetupSpecialObjects()
{
    // Create singleton instances
    var nilOop = vm.PrimitiveNewInstance(classRegistry["UndefinedObject"]);
    var trueOop = vm.PrimitiveNewInstance(classRegistry["True"]);
    var falseOop = vm.PrimitiveNewInstance(classRegistry["False"]);
    
    // Set special objects
    vm.SetSpecialObject(SpecialObjectIndex.NilObject, nilOop);
    vm.SetSpecialObject(SpecialObjectIndex.TrueObject, trueOop);
    vm.SetSpecialObject(SpecialObjectIndex.FalseObject, falseOop);
    vm.SetSpecialObject(SpecialObjectIndex.BehaviorClass, 
        classRegistry["Behavior"]);
    vm.SetSpecialObject(SpecialObjectIndex.MetaclassClass,
        classRegistry["Metaclass"]);
    vm.SetSpecialObject(SpecialObjectIndex.SystemDictionaryInstance,
        systemDictOop);
    // etc...
}
```

## Key Advantages

1. **No null class pointers** - Every object has a valid class from creation
2. **Simple and clean** - Each phase has a single responsibility
3. **No complex dependencies** - Phases are clearly ordered
4. **Matches Smalltalk semantics** - Creates the same structure as a real image
5. **Easy to debug** - Each phase can be validated independently

## Implementation Notes

1. Early symbols created before Symbol class exists should be tracked and fixed in Phase 3
2. The SystemDictionary instance must be created before registering globals
3. Method dictionaries start empty and are populated later
4. All singleton instances (nil, true, false) are created in Phase 7
5. The VM's special objects array is populated last to ensure all objects exist

## Migration Path

To migrate our current implementation:
1. Extract kernel class specs from system image JSON
2. Replace current multi-pass bootstrap with this cleaner approach
3. Remove all code that creates objects with null class pointers
4. Validate that GC finds 0 objects to collect after bootstrap

## Adding Classes After Bootstrap

Once the kernel classes are bootstrapped, adding new classes follows the exact same pattern, just one class at a time.

### Step 1: Stub-Allocate Class and Metaclass

For each new class, create the bare class and metaclass objects:

```csharp
public int AddClass(SystemImageClassData classData)
{
    // Find superclass (must already exist)
    if (!classRegistry.TryGetValue(classData.Superclass, out int superOop))
    {
        throw new InvalidOperationException(
            $"Superclass {classData.Superclass} not found for {classData.Name}");
    }
    
    // Create bare class and metaclass
    int classOop = vm.PrimitiveNewClass();
    int metaclassOop = vm.PrimitiveNewMetaclass();
    
    // Set minimal relationships
    var classObj = ObjectMemory.FromOop(classOop);
    var metaclassObj = ObjectMemory.FromOop(metaclassOop);
    
    classObj.ClassPointer = metaclassObj;
    metaclassObj.Fields[5] = classObj; // thisClass
    
    // Store in registry
    classRegistry[classData.Name] = classOop;
    metaclassRegistry[classData.Name] = metaclassOop;
    
    return classOop;
}
```

### Step 2: Wire Up All Slots

Populate all fields of the class and metaclass:

```csharp
public void WireClass(SystemImageClassData classData, int classOop, int metaclassOop)
{
    var classObj = ObjectMemory.FromOop(classOop);
    var metaclassObj = ObjectMemory.FromOop(metaclassOop);
    
    // 2.1: Name and superclass
    classObj.Fields[5] = ObjectMemory.FromOop(
        vm.PrimitiveInternSymbol(classData.Name));
    classObj.Fields[0] = ObjectMemory.FromOop(
        classRegistry[classData.Superclass]);
    
    // 2.2: Format
    int format = CalculateFormat(classData);
    classObj.Fields[2] = SmallInteger.Encode(format);
    
    // 2.3: Instance variables
    if (classData.InstanceVariables.Count > 0)
    {
        var ivArray = vm.PrimitiveNewArray(classData.InstanceVariables.Count);
        for (int i = 0; i < classData.InstanceVariables.Count; i++)
        {
            var ivSymbol = vm.PrimitiveInternSymbol(classData.InstanceVariables[i]);
            vm.PrimitiveArrayAtPut(ivArray, i, ivSymbol);
        }
        classObj.Fields[3] = ObjectMemory.FromOop(ivArray);
    }
    
    // 2.4: Class variables
    if (classData.ClassVariables.Count > 0)
    {
        var cvArray = vm.PrimitiveNewArray(classData.ClassVariables.Count);
        for (int i = 0; i < classData.ClassVariables.Count; i++)
        {
            var cvSymbol = vm.PrimitiveInternSymbol(classData.ClassVariables[i]);
            vm.PrimitiveArrayAtPut(cvArray, i, cvSymbol);
        }
        classObj.Fields[4] = ObjectMemory.FromOop(cvArray);
    }
    
    // 2.5: Method dictionary (starts empty)
    var methodDict = vm.PrimitiveNewInstance(classRegistry["MethodDictionary"]);
    vm.PrimitiveDictionaryInitialize(methodDict, 32);
    classObj.Fields[1] = ObjectMemory.FromOop(methodDict);
    
    // 2.6: Wire metaclass
    metaclassObj.Fields[0] = ObjectMemory.FromOop(
        metaclassRegistry[classData.Superclass]); // superclass's metaclass
    metaclassObj.ClassPointer = ObjectMemory.FromOop(
        classRegistry["Metaclass"]);
    
    // 2.7: Metaclass method dictionary
    var metaclassMethodDict = vm.PrimitiveNewInstance(
        classRegistry["MethodDictionary"]);
    vm.PrimitiveDictionaryInitialize(metaclassMethodDict, 16);
    metaclassObj.Fields[1] = ObjectMemory.FromOop(metaclassMethodDict);
}
```

### Step 3: Register in SystemDictionary

Make the class globally accessible:

```csharp
public void RegisterClass(string className, int classOop, int metaclassOop)
{
    // Register class
    vm.PrimitiveDictionaryAtPut(
        systemDictOop,
        vm.PrimitiveInternSymbol(className),
        classOop);
    
    // Register metaclass
    vm.PrimitiveDictionaryAtPut(
        systemDictOop,
        vm.PrimitiveInternSymbol(className + " class"),
        metaclassOop);
}
```

### Step 4: Install Methods

With the class structure in place, compile and install methods:

```csharp
public void InstallMethods(SystemImageClassData classData, int classOop)
{
    var classObj = ObjectMemory.FromOop(classOop);
    var methodDict = classObj.Fields[1]; // method dictionary
    
    // Install instance methods
    foreach (var methodData in classData.InstanceMethods)
    {
        var compiledMethod = CompileMethod(methodData);
        var selector = vm.PrimitiveInternSymbol(methodData.Selector);
        
        vm.PrimitiveDictionaryAtPut(
            methodDict.Oop,
            selector,
            compiledMethod);
    }
    
    // Install class methods on metaclass
    var metaclassObj = classObj.ClassPointer;
    var metaclassMethodDict = metaclassObj.Fields[1];
    
    foreach (var methodData in classData.ClassMethods)
    {
        var compiledMethod = CompileMethod(methodData);
        var selector = vm.PrimitiveInternSymbol(methodData.Selector);
        
        vm.PrimitiveDictionaryAtPut(
            metaclassMethodDict.Oop,
            selector,
            compiledMethod);
    }
}
```

### Complete Flow for Non-Kernel Classes

```csharp
public void AddNonKernelClasses(List<SystemImageClassData> classes)
{
    // Sort by dependency order
    var sorted = TopologicalSort(classes);
    
    foreach (var classData in sorted)
    {
        // Step 1: Stub allocate
        int classOop = AddClass(classData);
        int metaclassOop = metaclassRegistry[classData.Name];
        
        // Step 2: Wire all slots
        WireClass(classData, classOop, metaclassOop);
        
        // Step 3: Register globally
        RegisterClass(classData.Name, classOop, metaclassOop);
        
        // Step 4: Install methods
        InstallMethods(classData, classOop);
    }
}
```

### Handling Circular Dependencies

For classes that reference each other:

```csharp
public void AddCircularClasses(List<SystemImageClassData> circularGroup)
{
    // Phase 1: Stub-allocate all classes in the group
    var classOops = new Dictionary<string, int>();
    foreach (var classData in circularGroup)
    {
        int classOop = AddClass(classData);
        classOops[classData.Name] = classOop;
    }
    
    // Phase 2: Wire all classes (now all exist)
    foreach (var classData in circularGroup)
    {
        WireClass(classData, 
                  classOops[classData.Name],
                  metaclassRegistry[classData.Name]);
    }
    
    // Phase 3: Register all globally
    foreach (var classData in circularGroup)
    {
        RegisterClass(classData.Name,
                     classOops[classData.Name],
                     metaclassRegistry[classData.Name]);
    }
    
    // Phase 4: Install methods (all classes now visible)
    foreach (var classData in circularGroup)
    {
        InstallMethods(classData, classOops[classData.Name]);
    }
}
```

### Key Advantages of Unified Approach

1. **Same pattern everywhere** - Kernel and non-kernel classes use identical creation process
2. **No special cases** - Every class follows: stub → wire → register → methods
3. **Dependency handling** - Topological sort ensures superclasses exist first
4. **Circular reference support** - Multi-phase approach handles mutual dependencies
5. **Clean separation** - Structure creation is separate from behavior installation

### Implementation Strategy

For our system using system image JSON:

1. **Kernel classes** (Phase 0-7): Create the ~32 essential classes
2. **Core library classes**: Add remaining bootstrap classes from JSON
3. **Extended classes**: Load additional classes from extended manifests
4. **User classes**: Support dynamic class creation at runtime

All use the same fundamental operations:
- `PrimitiveNewClass()` / `PrimitiveNewMetaclass()`
- Field setting via primitive operations
- `PrimitiveDictionaryAtPut()` for registration
- `CompileMethod()` for behavior

This unified approach ensures:
- Every object has a valid class pointer from creation
- No temporary objects or fixups needed
- Clean, predictable bootstrap process
- Easy debugging and validation

## From Bare Metal to Self-Hosting System

After bootstrapping kernel classes, the system still lacks file I/O, compilation, and source loading. This section describes how to bootstrap these capabilities to achieve a self-hosting system.

### Phase 8: Minimal File I/O and Streams

Create the infrastructure for reading source files:

#### VM Primitives for File Access

```csharp
public class FileStreamPrimitives
{
    // File handle management
    private Dictionary<int, FileStream> fileHandles = new();
    private int nextHandle = 1;
    
    public int PrimitiveFileOpen(string path, string mode)
    {
        FileStream stream;
        switch (mode)
        {
            case "read":
                stream = File.OpenRead(path);
                break;
            case "write":
                stream = File.OpenWrite(path);
                break;
            case "append":
                stream = File.Open(path, FileMode.Append);
                break;
            default:
                throw new ArgumentException($"Unknown mode: {mode}");
        }
        
        int handle = nextHandle++;
        fileHandles[handle] = stream;
        return handle;
    }
    
    public int PrimitiveFileRead(int handle, int count)
    {
        if (!fileHandles.TryGetValue(handle, out var stream))
            throw new InvalidOperationException($"Invalid file handle: {handle}");
        
        var buffer = new byte[count];
        int bytesRead = stream.Read(buffer, 0, count);
        
        // Create ByteArray in Smalltalk
        var byteArrayOop = vm.PrimitiveNewByteArray(bytesRead);
        for (int i = 0; i < bytesRead; i++)
        {
            vm.PrimitiveByteArrayAtPut(byteArrayOop, i, buffer[i]);
        }
        
        return byteArrayOop;
    }
    
    public void PrimitiveFileClose(int handle)
    {
        if (fileHandles.TryGetValue(handle, out var stream))
        {
            stream.Close();
            fileHandles.Remove(handle);
        }
    }
}
```

#### Stream Class Structure

```csharp
public void CreateStreamClasses()
{
    // Create Stream class hierarchy
    var streamSpecs = new[]
    {
        new KernelClassSpec
        {
            Name = "Stream",
            SuperName = "Object",
            InstVarNames = new[] { "position", "readLimit" }
        },
        new KernelClassSpec
        {
            Name = "PositionableStream",
            SuperName = "Stream",
            InstVarNames = new[] { "collection" }
        },
        new KernelClassSpec
        {
            Name = "ReadWriteStream",
            SuperName = "PositionableStream",
            InstVarNames = new string[0]
        },
        new KernelClassSpec
        {
            Name = "FileStream",
            SuperName = "ReadWriteStream",
            InstVarNames = new[] { "fileHandle", "name" }
        }
    };
    
    foreach (var spec in streamSpecs)
    {
        int classOop = AddClass(spec);
        WireClass(spec, classOop, metaclassRegistry[spec.Name]);
        RegisterClass(spec.Name, classOop, metaclassRegistry[spec.Name]);
    }
}
```

#### Bootstrap Stream Methods

```csharp
public void InstallStreamMethods()
{
    // Install FileStream class>>fileNamed:
    var method = CompileMethod(@"
        fileNamed: aString
            | handle |
            handle := self primitiveFileOpen: aString mode: 'read'.
            ^self new initializeWithHandle: handle name: aString
    ");
    InstallClassMethod("FileStream", "fileNamed:", method);
    
    // Install FileStream>>next
    var nextMethod = CompileMethod(@"
        next
            | byte |
            byte := self primitiveFileRead: fileHandle count: 1.
            ^byte isEmpty 
                ifTrue: [nil] 
                ifFalse: [Character value: (byte at: 1)]
    ");
    InstallInstanceMethod("FileStream", "next", nextMethod);
}
```

### Phase 9: Bootstrapping the Compiler

Create compiler infrastructure for parsing and compiling Smalltalk code:

#### Compiler Class Structure

```csharp
public void CreateCompilerClasses()
{
    var compilerSpecs = new[]
    {
        new KernelClassSpec
        {
            Name = "Scanner",
            SuperName = "Object",
            InstVarNames = new[] { "source", "position", "currentToken" }
        },
        new KernelClassSpec
        {
            Name = "Parser",
            SuperName = "Object",
            InstVarNames = new[] { "scanner", "currentClass", "methodNode" }
        },
        new KernelClassSpec
        {
            Name = "Compiler",
            SuperName = "Object",
            InstVarNames = new[] { "parser", "class", "category" },
            PoolNames = new[] { "Globals" }
        },
        new KernelClassSpec
        {
            Name = "MethodNode",
            SuperName = "Object",
            InstVarNames = new[] { "selector", "arguments", "temporaries", 
                                   "statements", "primitive", "literals" }
        },
        new KernelClassSpec
        {
            Name = "BytecodeGenerator",
            SuperName = "Object",
            InstVarNames = new[] { "methodNode", "bytecodes", "literals" }
        }
    };
    
    foreach (var spec in compilerSpecs)
    {
        int classOop = AddClass(spec);
        WireClass(spec, classOop, metaclassRegistry[spec.Name]);
        RegisterClass(spec.Name, classOop, metaclassRegistry[spec.Name]);
    }
}
```

#### VM Primitives for Compilation

```csharp
public class CompilerPrimitives
{
    public int PrimitiveScanTokens(string source)
    {
        // Basic tokenization - return array of tokens
        var tokens = new SmalltalkLexer(source).ScanAllTokens();
        
        var arrayOop = vm.PrimitiveNewArray(tokens.Count);
        for (int i = 0; i < tokens.Count; i++)
        {
            var tokenOop = CreateToken(tokens[i]);
            vm.PrimitiveArrayAtPut(arrayOop, i, tokenOop);
        }
        
        return arrayOop;
    }
    
    public int PrimitiveParseMethod(int tokensOop, int classOop)
    {
        // Parse tokens into method node
        var tokens = ExtractTokens(tokensOop);
        var parser = new SmalltalkParser(tokens);
        var methodNode = parser.ParseMethod();
        
        // Create MethodNode object in Smalltalk
        return CreateMethodNode(methodNode);
    }
    
    public int PrimitiveCompileMethod(int methodNodeOop, int classOop)
    {
        // Generate bytecode from method node
        var methodNode = ExtractMethodNode(methodNodeOop);
        var generator = new BytecodeGenerator(methodNode);
        var bytecode = generator.Generate();
        
        // Create CompiledMethod
        var compiledMethodOop = vm.PrimitiveNewCompiledMethod(
            bytecode.Length,
            methodNode.Literals.Count,
            methodNode.Temporaries.Count,
            methodNode.Arguments.Count);
        
        // Set bytecode
        for (int i = 0; i < bytecode.Length; i++)
        {
            vm.PrimitiveCompiledMethodAtPut(compiledMethodOop, i, bytecode[i]);
        }
        
        // Set literals
        for (int i = 0; i < methodNode.Literals.Count; i++)
        {
            vm.PrimitiveCompiledMethodLiteralAtPut(
                compiledMethodOop, i, methodNode.Literals[i]);
        }
        
        return compiledMethodOop;
    }
    
    public void PrimitiveInstallMethod(int methodOop, int selectorOop, int classOop)
    {
        // Install method in class's method dictionary
        var classObj = ObjectMemory.FromOop(classOop);
        var methodDict = classObj.Fields[1]; // method dictionary
        
        vm.PrimitiveDictionaryAtPut(methodDict.Oop, selectorOop, methodOop);
    }
}
```

### Phase 10: FileIn Capability

With streams and compiler in place, implement file-in:

```csharp
public void BootstrapFileIn()
{
    // Create Compiler>>fileIn: method
    var fileInMethod = CompileMethod(@"
        fileIn: aStream
            | chunk |
            [aStream atEnd] whileFalse: [
                chunk := aStream nextChunk.
                chunk isEmpty ifFalse: [
                    self 
                        compile: chunk
                        notifying: nil
                        ifFail: [:err | 
                            Transcript show: 'Compilation failed: ', err]
                ]
            ]
    ");
    InstallInstanceMethod("Compiler", "fileIn:", fileInMethod);
}
```

### Phase 11: Class Definition from Source (Pure Smalltalk)

The class creation logic lives entirely in Smalltalk, not in VM primitives. The VM provides only the minimal primitive to allocate the raw class/metaclass pair.

#### Minimal VM Primitive

```csharp
public class ClassCreationPrimitive
{
    public int PrimitiveNewClassSuperclassFormat(
        int nameSymbolOop, 
        int superclassOop, 
        int formatInt)
    {
        // This is the ONLY primitive for class creation
        // Just allocates raw class and metaclass objects
        
        // Create bare class and metaclass
        var classOop = vm.PrimitiveNewClass();
        var metaclassOop = vm.PrimitiveNewMetaclass();
        
        var classObj = ObjectMemory.FromOop(classOop);
        var metaclassObj = ObjectMemory.FromOop(metaclassOop);
        
        // Wire basic relationships
        classObj.ClassPointer = metaclassObj;
        metaclassObj.Fields[5] = classObj; // thisClass
        
        // Set superclass
        if (superclassOop != vm.PrimitiveNil())
        {
            var superObj = ObjectMemory.FromOop(superclassOop);
            classObj.Fields[0] = superObj;
            
            // Metaclass superclass is superclass's metaclass
            if (superObj.ClassPointer != null)
            {
                metaclassObj.Fields[0] = superObj.ClassPointer;
            }
        }
        
        // Set format
        classObj.Fields[2] = formatInt;
        
        // Everything else is done in Smalltalk!
        return classOop;
    }
}
```

#### Behavior Class Methods (Pure Smalltalk)

```smalltalk
Behavior class>>subclass: nameSymbol 
    instanceVariableNames: instVarString
    classVariableNames: classVarString
    poolDictionaries: poolString
    category: categoryString
    
    | newClass newMeta |
    
    "1. Allocate raw class + metaclass via the ONLY primitive"
    newClass := self primitiveNewClass: nameSymbol
                     superclass: self
                     format: (self calculateFormatFor: instVarString).
    newMeta := newClass class.
    
    "2. Initialize instance-side structure (pure Smalltalk)"
    newClass name: nameSymbol.
    newClass instVarNames: (instVarString subStrings: ' ').
    newClass classVarNames: (classVarString subStrings: ' ').
    newClass poolDictionaries: (poolString subStrings: ' ' 
        collect: [:name | Smalltalk at: name asSymbol]).
    newClass category: categoryString.
    
    "3. Initialize metaclass side (pure Smalltalk)"
    newMeta name: (nameSymbol asString , ' class') asSymbol.
    newMeta instVarNames: #().
    newMeta classVarNames: #().
    newMeta poolDictionaries: newClass poolDictionaries.
    newMeta category: categoryString.
    
    "4. Initialize method dictionaries"
    newClass methodDictionary: MethodDictionary new.
    newMeta methodDictionary: MethodDictionary new.
    
    "5. Register in global namespace (pure Smalltalk)"
    Smalltalk at: nameSymbol put: newClass.
    Smalltalk at: newMeta name put: newMeta.
    
    "6. Initialize class variables if any"
    classVarString isEmpty ifFalse: [
        newClass initializeClassVariables
    ].
    
    "7. Notify system of new class"
    SystemOrganization classify: nameSymbol under: categoryString.
    
    ^newClass
```

#### Supporting Methods (Pure Smalltalk)

```smalltalk
Behavior>>calculateFormatFor: instVarString
    "Calculate format word based on instance variables"
    | instVarCount inheritedCount |
    instVarCount := (instVarString subStrings: ' ') size.
    inheritedCount := self allInstVarNames size.
    ^self encodeFormat: inheritedCount + instVarCount
          isIndexable: false
          isBytes: false

Behavior>>initializeClassVariables
    "Create class variable bindings"
    | dict |
    dict := Dictionary new.
    self classVarNames do: [:name |
        dict at: name put: nil
    ].
    self classPool: dict

String>>subStrings: separator
    "Split string by separator"
    | result start |
    result := OrderedCollection new.
    start := 1.
    1 to: self size do: [:i |
        (self at: i) = separator ifTrue: [
            start < i ifTrue: [
                result add: (self copyFrom: start to: i - 1)
            ].
            start := i + 1
        ]
    ].
    start <= self size ifTrue: [
        result add: (self copyFrom: start to: self size)
    ].
    ^result asArray
```

#### Prerequisites for Pure Smalltalk Class Creation

Before the Smalltalk-level `subclass:` method can work, you need:

1. **Minimal VM Primitive**: Just `primitiveNewClass:superclass:format:`
2. **String Utilities**: `String>>subStrings:` for parsing
3. **Collection Classes**: OrderedCollection, Array for managing lists
4. **Dictionary Operations**: For class variables and pools
5. **Symbol Operations**: `asSymbol`, `asString` conversions
6. **SystemDictionary**: Global namespace registration
7. **SystemOrganization**: Category management

#### Bootstrap These Methods Early

```csharp
public void BootstrapClassBuilder()
{
    // First ensure prerequisite methods exist
    LoadPrerequisites();
    
    // Load the Smalltalk source for class creation
    var behaviorSubclassSource = File.ReadAllText(
        "Kernel-Classes-Behavior.st");
    
    // Compile and install the methods
    CompileAndInstall("Behavior class", 
        "subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:",
        behaviorSubclassSource);
    
    // Also load supporting methods
    CompileAndInstall("Behavior", "calculateFormatFor:", 
        File.ReadAllText("Kernel-Classes-Format.st"));
    
    CompileAndInstall("Behavior", "initializeClassVariables",
        File.ReadAllText("Kernel-Classes-Variables.st"));
    
    CompileAndInstall("String", "subStrings:",
        File.ReadAllText("Collections-String.st"));
}

private void LoadPrerequisites()
{
    // String methods
    FileInSource("Collections-String-Core.st");
    
    // Collection support
    FileInSource("Collections-Support.st");
    
    // Dictionary operations
    FileInSource("Collections-Dictionary-Core.st");
    
    // Symbol conversions
    FileInSource("Kernel-Objects-Symbol.st");
}
```

#### The Beauty of This Approach

1. **VM Simplicity**: The VM provides only ONE primitive for class creation
2. **Flexibility**: All class creation logic is in Smalltalk and can be modified
3. **Debuggability**: Class creation can be debugged in Smalltalk
4. **Extensibility**: Subclasses can override `subclass:...` for custom behavior
5. **Authenticity**: This matches how real Smalltalk-80 works

### Complete Bootstrap Sequence

```csharp
public class CompleteBootstrap
{
    public void Execute()
    {
        // Phase 0-7: Kernel classes (as described earlier)
        BootstrapKernelClasses();
        
        // Phase 8: File I/O and Streams
        CreateStreamClasses();
        InstallStreamMethods();
        RegisterFilePrimitives();
        
        // Phase 9: Compiler infrastructure
        CreateCompilerClasses();
        RegisterCompilerPrimitives();
        
        // Phase 10: FileIn capability
        BootstrapFileIn();
        
        // Phase 11: Class builder
        BootstrapClassBuilder();
        
        // Now we can load Smalltalk source files!
        LoadKernelSources();
    }
    
    private void LoadKernelSources()
    {
        // Load remaining kernel methods from source files
        FileInSource("Kernel-Objects.st");
        FileInSource("Kernel-Classes.st");
        FileInSource("Kernel-Methods.st");
        FileInSource("Collections-Abstract.st");
        FileInSource("Collections-Unordered.st");
        FileInSource("Collections-Sequenceable.st");
        FileInSource("Collections-Streams.st");
        FileInSource("Compiler-Lexical.st");
        FileInSource("Compiler-Parsing.st");
        FileInSource("Compiler-Bytecodes.st");
    }
    
    private void FileInSource(string filename)
    {
        // Use our bootstrapped file-in capability
        var code = $@"
            | stream |
            stream := FileStream fileNamed: '{filename}'.
            [Compiler new fileIn: stream] 
                ensure: [stream close]
        ";
        
        vm.EvaluateSource(code);
    }
}
```

### Validation Checkpoints

At each phase, validate the bootstrap:

1. **After Phase 7**: All kernel classes exist with proper structure
2. **After Phase 8**: Can read files and create streams
3. **After Phase 9**: Can tokenize and parse Smalltalk code
4. **After Phase 10**: Can compile methods from source
5. **After Phase 11**: Can define new classes from source

### Self-Hosting Achieved

At this point, the system is fully self-hosting:

- **File I/O**: Can read .st source files
- **Compilation**: Can parse and compile Smalltalk code
- **Class Creation**: Can define new classes from source
- **Method Installation**: Can add behavior to any class
- **Complete Smalltalk**: The rest of the system can be loaded from source

From here, the system grows entirely in Smalltalk:

```smalltalk
"Load the rest of the Smalltalk-80 system"
'System-Support.st' asFilename fileIn.
'System-Compiler.st' asFilename fileIn.
'Graphics-Primitives.st' asFilename fileIn.
'Graphics-Display.st' asFilename fileIn.
'Interface-Framework.st' asFilename fileIn.
"... and so on"
```

The bootstrap is complete - from bare metal C# primitives to a living Smalltalk system!

## Summary: The Complete Bootstrap Architecture

### Layered Design

The bootstrap follows a strict layered architecture:

1. **VM Layer (C#)**
   - Object allocation primitives
   - Field access primitives  
   - Method dispatch
   - Garbage collection
   - ONE primitive for class creation: `primitiveNewClass:superclass:format:`

2. **Kernel Bootstrap Layer (C#)**
   - Creates ~32 kernel classes using multi-pass approach
   - Sets up Special Objects Array
   - Initializes SystemDictionary
   - No null class pointers ever

3. **Bridge Layer (C# + Smalltalk)**
   - File I/O primitives
   - Minimal compiler primitives
   - Bootstrap Stream and Compiler classes
   - Load essential Smalltalk methods

4. **Smalltalk Layer (Pure Smalltalk)**
   - `Behavior class>>subclass:...` method
   - All class creation logic
   - Method compilation
   - FileIn capability

### Key Design Principles

1. **Minimal VM**: The VM knows nothing about Smalltalk semantics, just object structure
2. **No Null Pointers**: Every object has a valid class from creation
3. **Multi-Pass Bootstrap**: Handles circular dependencies cleanly
4. **Pure Smalltalk Logic**: Class creation and compilation logic lives in Smalltalk
5. **Progressive Enhancement**: Each phase enables the next

### What Makes This Design Correct

1. **Matches Blue Book**: This is how Smalltalk-80 actually works
2. **Clean Separation**: VM provides mechanism, Smalltalk provides policy
3. **Debuggable**: Once bridge layer works, everything else is debuggable in Smalltalk
4. **Extensible**: New class types can be added by overriding Smalltalk methods
5. **Self-Hosting**: System can rebuild itself from source

### The Bootstrap Journey

```
Bare Metal → Kernel Classes → File I/O → Compiler → Self-Hosting

Phase 0-7:   Create kernel classes (Object, Behavior, Collection, etc.)
Phase 8:     Add file I/O primitives and Stream classes
Phase 9:     Add compiler primitives and Compiler classes  
Phase 10:    Load FileIn capability
Phase 11:    Load class creation logic (in Smalltalk!)
Phase 12+:   Load everything else from .st files
```

### Final State

After bootstrap completes:

- **5000+ classes** can be loaded from source
- **50000+ methods** can be compiled and installed
- **Full Smalltalk-80** system running
- **Self-modifying**: Can change its own compiler and class builder
- **Reflective**: Can inspect and modify its own structure

The elegance is that after Phase 11, the VM never needs to know about any new classes or methods - everything is handled by Smalltalk code calling the same primitives used during bootstrap.

## Implementation Checklist

- [ ] Implement kernel class specifications
- [ ] Implement multi-pass bootstrap (Phase 0-7)
- [ ] Add file I/O primitives (Phase 8)
- [ ] Add compiler primitives (Phase 9)
- [ ] Load FileIn capability (Phase 10)
- [ ] Load Behavior>>subclass: from Smalltalk source (Phase 11)
- [ ] Validate no objects with null ClassPointer
- [ ] Validate GC collects 0 objects after bootstrap
- [ ] Load rest of Smalltalk-80 from source files
- [ ] Verify self-hosting by creating a new class from source