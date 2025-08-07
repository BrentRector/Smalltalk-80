# Smalltalk-80 Bootstrap Design

## Overview

A proper Smalltalk-80 bootstrap involves multiple layers, progressing from C# host code to a fully self-hosting Smalltalk environment.

## Current State vs Target Architecture

### Current Implementation (Suboptimal)
- `MinimalBootstrap.cs` manually creates classes in C#
- `BootstrapMethods.st` is parsed and executed at runtime
- Infrastructure classes loaded via hybrid C#/Smalltalk approach
- Fragile and requires manual maintenance

### Target Architecture (Your Proposal)
1. **Compile-time Bootstrap Generation**
   - Parse `Bootstrap.st` containing minimal class definitions
   - Generate `GeneratedBootstrap.cs` that creates these classes directly
   - Core classes are "baked into" the interpreter binary
   - No runtime parsing of bootstrap code

2. **Runtime Class Loading Sequence**
   ```
   Interpreter Start
        ↓
   GeneratedBootstrap.Initialize() [C#, instant]
        ↓
   Load StandardLibrary via Smalltalk class creation
        ↓
   Load System Image with full file in/out capabilities
        ↓
   Fully self-hosting system
   ```

## Bootstrap Layers

### Layer 0: C# Host (GeneratedBootstrap.cs)
**Purpose**: Minimal object memory and class structure
**Contains**: ~30 classes absolutely required for ANY Smalltalk code execution

```csharp
// Generated from Bootstrap.st at compile time
public static class GeneratedBootstrap 
{
    public static void Initialize()
    {
        // Create Object
        var objectClass = new SmalltalkClass("Object", null);
        objectClass.MethodDictionary[new Symbol("class")] = /* compiled bytecode */;
        
        // Create Metaclass hierarchy
        var behaviorClass = new SmalltalkClass("Behavior", objectClass);
        // ... etc
    }
}
```

**Classes Required**:
- Object, UndefinedObject
- Behavior, ClassDescription, Class, Metaclass
- Boolean, True, False
- SmallInteger, Symbol, String, Array
- BlockContext, CompiledMethod
- Collection, Set, Dictionary, SystemDictionary
- Association
- MethodDictionary, ClassOrganizer

### Layer 1: Infrastructure Classes (via Smalltalk)
**Purpose**: Enable class creation and method compilation
**Loaded via**: Smalltalk `subclass:` messages

Classes:
- Character, OrderedCollection
- Stream hierarchy (for parsing)
- Scanner, Parser basics
- SystemOrganizer

### Layer 2: Standard Library
**Purpose**: Core functionality
**Loaded via**: Smalltalk file-in mechanism once available

Classes:
- Number hierarchy
- Collection hierarchy
- Stream hierarchy (complete)
- Exception handling

### Layer 3: System Image
**Purpose**: Full development environment
**Loaded via**: Binary image or file-in

Contains:
- Browser, Debugger, Inspector
- ChangeSet, FileStream, FileDirectory
- Compiler, Decompiler
- SystemTracer (for snapshots)

## Bootstrap.st Format

```smalltalk
"Bootstrap.st - Minimal classes for cold start"
"This file is NEVER executed - only parsed to generate C# code"

Object subclass: #Object
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''!

!Object methodsFor: 'system primitives'!
== anObject
    <primitive: 110>
    self primitiveFailed!

class
    <primitive: 111>
    self primitiveFailed! !

"... minimal methods only ..."

Object subclass: #Behavior
    instanceVariableNames: 'superclass methodDict format'
    classVariableNames: ''
    poolDictionaries: ''!

"... continue with absolute minimum ..."
```

## Bootstrap Generator Tool

```csharp
// BootstrapGenerator.cs - Build-time tool
public class BootstrapGenerator
{
    public static void Main(string[] args)
    {
        var bootstrapAst = ParseSmalltalk("Bootstrap.st");
        var csharpCode = GenerateCSharpBootstrap(bootstrapAst);
        File.WriteAllText("GeneratedBootstrap.cs", csharpCode);
    }
    
    static string GenerateCSharpBootstrap(FileNode ast)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// Auto-generated from Bootstrap.st");
        sb.AppendLine("public static class GeneratedBootstrap {");
        
        foreach (var classDef in ast.Classes)
        {
            sb.AppendLine($"    // Create {classDef.Name}");
            sb.AppendLine($"    var {classDef.Name.ToLower()} = new SmalltalkClass(\"{classDef.Name}\", {classDef.Superclass?.ToLower() ?? "null"});");
            
            foreach (var method in classDef.Methods)
            {
                var bytecode = CompileMethod(method);
                sb.AppendLine($"    {classDef.Name.ToLower()}.InstallMethod(\"{method.Selector}\", new byte[] {{ {bytecode} }});");
            }
        }
        
        sb.AppendLine("}");
        return sb.ToString();
    }
}
```

## Binary Image Support

### Snapshot (Save)
```csharp
public class ImageWriter
{
    public void SaveSnapshot(string filename)
    {
        using (var stream = File.Create(filename))
        using (var writer = new BinaryWriter(stream))
        {
            // Write header
            writer.Write("ST80IMG1"); // Magic number
            writer.Write(1); // Version
            
            // Write all objects with relocatable references
            var objects = CollectAllObjects();
            var objectTable = BuildObjectTable(objects);
            
            writer.Write(objectTable.Count);
            foreach (var obj in objectTable)
            {
                WriteObject(writer, obj);
            }
            
            // Write root pointers
            writer.Write(SystemDictionary.GetSmalltalk());
        }
    }
}
```

### Resume (Load)
```csharp
public class ImageReader  
{
    public void LoadSnapshot(string filename)
    {
        using (var stream = File.Open(filename, FileMode.Open))
        using (var reader = new BinaryReader(stream))
        {
            // Verify header
            var magic = reader.ReadString();
            if (magic != "ST80IMG1") throw new InvalidOperationException();
            
            // Read object table
            var count = reader.ReadInt32();
            var objects = new List<SmalltalkObject>(count);
            
            // First pass: create all objects
            for (int i = 0; i < count; i++)
            {
                objects.Add(ReadObjectStub(reader));
            }
            
            // Second pass: fix up references
            for (int i = 0; i < count; i++)
            {
                FixupReferences(objects[i], reader);
            }
            
            // Restore roots
            SystemDictionary.SetSmalltalk(reader.ReadObject());
        }
    }
}
```

## Cold Start Sequence

### First Time (No Image)
1. `GeneratedBootstrap.Initialize()` - Instant, creates core classes
2. Load `StandardLibrary.st` via Smalltalk class creation
3. Load `SystemImage.st` for development tools
4. Save snapshot for next time

### Subsequent Starts (With Image)
1. Load binary image - Instant restoration
2. System ready with full environment

### Development Mode (Bootstrap Development)
1. Edit `Bootstrap.st`
2. Run `BootstrapGenerator` at build time
3. Recompile interpreter with new `GeneratedBootstrap.cs`
4. Test cold start sequence

## Benefits of This Approach

1. **Fast Startup**: Core classes created instantly in C#
2. **Self-Hosting**: Most classes created via Smalltalk
3. **Maintainable**: Bootstrap.st is readable Smalltalk code
4. **Reproducible**: Same bootstrap every time
5. **Efficient**: Binary images for instant restore
6. **Debuggable**: Can always cold start if image corrupted

## Implementation Priority

1. **Phase 1**: Bootstrap Generator
   - Parse Bootstrap.st
   - Generate C# code
   - Integrate into build

2. **Phase 2**: Refactor Loading
   - Use generated bootstrap
   - Load StandardLibrary via Smalltalk
   - Verify file in/out works

3. **Phase 3**: Binary Images
   - Implement snapshot/resume
   - Add image management commands
   - Support image versioning

4. **Phase 4**: Full Self-Hosting
   - Load compiler from Smalltalk
   - Support runtime class modifications
   - Implement SystemTracer equivalent

## Comparison with Other Smalltalks

### Squeak/Pharo
- Uses binary image always
- Bootstrap from "seed" image
- Complicated bootstrap process

### GNU Smalltalk  
- File-based like ours
- Bootstrap from C code
- Less emphasis on images

### VisualWorks
- Commercial, binary image
- Sophisticated parcels system
- Complex bootstrap hidden

Our approach combines the best:
- Simple file-based bootstrap (GNU-like)
- Binary images for speed (Squeak-like)
- Clear layering (unique advantage)