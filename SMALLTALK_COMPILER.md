# Smalltalk-80 Compiler Documentation

## Overview

The Smalltalk-80 Compiler is a complete compilation system that transforms Smalltalk source code into bytecode-based JSON manifests. It parses Smalltalk source files using an ANTLR4-based parser, extracts class definitions and method implementations, compiles methods to Blue Book bytecode, and outputs everything in a structured JSON format suitable for bootstrap phases.

## Features

- **Complete Smalltalk-80 syntax support** including all class definition types
- **Blue Book compliant bytecode generation** with all 256 bytecode instructions
- **Support for all class types**: `subclass:`, `variableSubclass:`, `variableByteSubclass:`, `variableWordSubclass:`
- **Comprehensive literal handling** for symbols, strings, numbers, arrays, and special objects
- **Full operator method support** including `+`, `-`, `*`, `/`, `<`, `=`, etc.
- **Bootstrap manifest generation** with complete class and method metadata
- **Accurate method compilation** with proper argument and temporary variable tracking

## Building the Compiler

```bash
# Build the standalone compiler project
dotnet build StandaloneCompiler

# The executable will be created at:
# Windows: bin\Debug\net9.0\Smalltalk80.Compiler.exe
# Linux/Mac: bin/Debug/net9.0/Smalltalk80.Compiler
```

## Usage

### Command Line Interface

```bash
Smalltalk80.Compiler <input.st> [output.json]
```

**Arguments:**
- `input.st` - Smalltalk source file to compile (required)
- `output.json` - Output JSON file (optional, defaults to input filename with .json extension)

### Examples

```bash
# Compile a single class file
dotnet run --project StandaloneCompiler -- Point.st
# Creates: Point.json

# Compile with specific output file
dotnet run --project StandaloneCompiler -- MyClass.st compiled.json

# Compile the entire Smalltalk-80 system image
dotnet run --project StandaloneCompiler -- "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" system.json
# Result: 223 classes, 4,493 methods

# Use the built executable directly
./bin/Debug/net9.0/Smalltalk80.Compiler.exe source.st output.json
```

### Integration with ExtractClass

The compiler is designed to work seamlessly with the ExtractClass tool. When extracting classes from the system image, ExtractClass:

1. Generates complete Smalltalk source code from system image data
2. Calls the Smalltalk80.Compiler to compile the source
3. Reads the resulting JSON and converts it to the ExtractClass format
4. Preserves all compilation artifacts while maintaining single-source compilation logic

## Generated JSON Format

The compiler outputs a complete bootstrap manifest in JSON format containing all information needed to reconstruct classes and methods at runtime.

### Top-Level Structure

```json
{
  "$schema": "./bootstrap-schema.json",
  "phase": "3",
  "description": "Compiled from source on 2025-01-13 15:30:45",
  "classes": [...],
  "methods": [...]
}
```

#### Manifest Properties

- **`$schema`**: Reference to JSON schema for validation
- **`phase`**: Bootstrap phase identifier ("2" or "3")
- **`description`**: Human-readable description with compilation timestamp
- **`classes`**: Array of class definitions
- **`methods`**: Array of compiled method definitions

### Class Definition Structure

Each class in the `classes` array contains complete metadata:

```json
{
  "name": "Point",
  "superclass": "Object",
  "instanceVariables": ["x", "y"],
  "classVariables": [],
  "poolDictionaries": [],
  "category": "Graphics-Primitives"
}
```

#### Class Properties

- **`name`**: Class name (e.g., "Point", "Collection", "Array")
- **`superclass`**: Parent class name (e.g., "Object", "Collection")
- **`instanceVariables`**: Array of instance variable names
- **`classVariables`**: Array of class variable names  
- **`poolDictionaries`**: Array of shared pool dictionary names
- **`category`**: Organization category for the class

### Method Definition Structure

Each method in the `methods` array contains complete executable information:

```json
{
  "class": "Point",
  "selector": "+",
  "bytecode": [112, 199, 16, 213, 17, 213, 240, 124],
  "literals": ["class", "x:", "y:", "+"],
  "primitiveIndex": 0,
  "numArgs": 1,
  "numTemps": 0,
  "isClassMethod": false
}
```

#### Method Properties

- **`class`**: Target class name (includes " class" suffix for class methods)
- **`selector`**: Method name/selector (e.g., "+", "at:", "new")
- **`bytecode`**: Array of Blue Book bytecode instructions
- **`literals`**: Array of literal objects referenced by the method
- **`primitiveIndex`**: Primitive number (0 for non-primitive methods)
- **`numArgs`**: Number of method arguments (not counting receiver)
- **`numTemps`**: Number of temporary variables
- **`isClassMethod`**: Boolean indicating class method vs instance method

### Bytecode Format

The bytecode array contains Blue Book format instructions as integers:

```json
"bytecode": [112, 199, 16, 213, 17, 213, 240, 124]
```

#### Bytecode Instruction Ranges

- **0-15**: Push receiver variables
- **16-31**: Push temporary variables
- **32-63**: Push literal constants
- **64-95**: Push literal variables
- **96-103**: Store and pop receiver variables
- **104-111**: Store and pop temporary variables
- **112**: Push receiver (`self`)
- **113-115**: Push `true`, `false`, `nil`
- **116-119**: Push small integers (-1, 0, 1, 2)
- **120-123**: Return receiver, `true`, `false`, `nil`
- **124**: Return top of stack
- **128-191**: Extended instructions
- **192-255**: Message sends with various argument counts

### Literals Array Format

The literals array contains all objects referenced by the bytecode:

```json
"literals": ["#class", "x:", "y:", "+", 42, "hello"]
```

#### Literal Types

- **Symbols**: Prefixed with `#` (e.g., `"#class"`, `"#+"``)
- **Strings**: Plain strings (e.g., `"hello"`, `"error message"`)
- **Numbers**: String representation (e.g., `"42"`, `"3.14"`)
- **Booleans**: `"true"` or `"false"`
- **Nil**: `"nil"`
- **Arrays**: Nested arrays for literal arrays
- **Blocks**: Block closures (for literal blocks)

### Example: Complete Point Class

```json
{
  "$schema": "./bootstrap-schema.json",
  "phase": "3",
  "description": "Compiled from source on 2025-01-13 15:30:45",
  "classes": [
    {
      "name": "Point",
      "superclass": "Object",
      "instanceVariables": ["x", "y"],
      "classVariables": [],
      "poolDictionaries": [],
      "category": "Graphics-Primitives"
    }
  ],
  "methods": [
    {
      "class": "Point",
      "selector": "x",
      "bytecode": [0, 124],
      "literals": [],
      "primitiveIndex": 0,
      "numArgs": 0,
      "numTemps": 0,
      "isClassMethod": false
    },
    {
      "class": "Point", 
      "selector": "y",
      "bytecode": [1, 124],
      "literals": [],
      "primitiveIndex": 0,
      "numArgs": 0,
      "numTemps": 0,
      "isClassMethod": false
    },
    {
      "class": "Point",
      "selector": "x:",
      "bytecode": [16, 96, 120],
      "literals": [],
      "primitiveIndex": 0,
      "numArgs": 1,
      "numTemps": 0,
      "isClassMethod": false
    },
    {
      "class": "Point",
      "selector": "+",
      "bytecode": [112, 199, 0, 16, 213, 235, 1, 16, 214, 235, 240, 124],
      "literals": ["#class", "#x", "#y", "#x:y:"],
      "primitiveIndex": 0,
      "numArgs": 1,
      "numTemps": 0,
      "isClassMethod": false
    },
    {
      "class": "Point class",
      "selector": "x:y:",
      "bytecode": [112, 204, 16, 17, 131, 1, 124],
      "literals": ["#new", "#x:y:"],
      "primitiveIndex": 0,
      "numArgs": 2,
      "numTemps": 0,
      "isClassMethod": true
    }
  ]
}
```

## Compilation Process

### 1. Source Parsing

The compiler uses ANTLR4 to parse Smalltalk source into an Abstract Syntax Tree (AST):

- **Lexical analysis**: Tokenizes Smalltalk syntax including keywords, identifiers, strings, numbers
- **Syntactic analysis**: Builds parse tree following Smalltalk-80 grammar rules
- **AST walking**: Extracts class definitions and method implementations

### 2. Class Extraction

For each class definition found:

- Extract class name and superclass
- Parse instance variable specifications
- Parse class variable specifications  
- Identify pool dictionaries
- Determine class category

### 3. Method Compilation

For each method found:

- Parse method signature to determine selector and arguments
- Build compilation environment with variable scopes
- Generate Blue Book bytecode using recursive descent
- Collect literal objects referenced by the method
- Calculate argument and temporary counts

### 4. Bytecode Generation

The bytecode compiler generates Blue Book compliant instructions:

- **Variable access**: Proper indexing for receiver, temporary, and literal variables
- **Message sending**: Correct selector encoding and argument passing
- **Control flow**: Blocks, conditionals, and loops
- **Stack management**: Push/pop operations and return statements
- **Literal references**: Symbol, string, and constant handling

### 5. JSON Serialization

Final output generation:

- Convert bytecode arrays to integer arrays
- Format literals for JSON serialization
- Apply proper JSON schema structure
- Include metadata and timestamps

## Runtime Integration

The generated JSON is designed for direct consumption by the Smalltalk-80 interpreter:

### Bootstrap Loading

```csharp
// Load and parse the manifest
var json = File.ReadAllText("compiled_classes.json");
var manifest = JsonSerializer.Deserialize<BootstrapManifest>(json);

// Install classes
foreach (var classDef in manifest.Classes)
{
    CreateClass(classDef.Name, classDef.Superclass, 
               classDef.InstanceVariables, classDef.ClassVariables);
}

// Install methods
foreach (var methodDef in manifest.Methods)
{
    InstallMethod(methodDef.Class, methodDef.Selector, 
                 methodDef.Bytecode, methodDef.Literals,
                 methodDef.NumArgs, methodDef.NumTemps);
}
```

### Method Execution

Compiled methods execute directly in the interpreter:

1. **Method lookup**: Find method by class and selector
2. **Context creation**: Allocate arguments and temporaries  
3. **Bytecode execution**: Process instructions sequentially
4. **Literal resolution**: Access objects from literal frame
5. **Message dispatch**: Handle sends to other methods

## Advanced Features

### Block Compilation

The compiler fully supports Smalltalk blocks with:

- **Block arguments**: `[:x :y | ...]` syntax
- **Block temporaries**: `[:x | | temp | ...]` local variables
- **Nested scoping**: Proper variable resolution across scopes
- **Closure creation**: Bytecode for block instantiation

### Array Literals

Support for both array types:

- **Literal arrays**: `#(1 2 3)` compiled to runtime objects
- **Dynamic arrays**: `{expr1. expr2. expr3}` with expression evaluation
- **Byte arrays**: `#[1 2 255]` for raw byte data

### Primitive Methods

Support for primitive method declarations:

```smalltalk
at: index
    <primitive: 60>
    ^self basicAt: index
```

The compiler recognizes primitive declarations and sets appropriate metadata.

## Error Handling

The compiler provides comprehensive error reporting:

- **Syntax errors**: Parse failures with line/column information
- **Semantic errors**: Undefined variables, invalid message sends
- **Compilation errors**: Bytecode generation failures
- **Graceful degradation**: Continue processing after recoverable errors

## Performance Considerations

- **Efficient parsing**: ANTLR4 provides fast, predictable parsing
- **Optimal bytecode**: Generates compact Blue Book instructions
- **Minimal allocations**: Reuses compilation environments
- **Streaming output**: Direct JSON serialization without intermediate formats

## Integration with Development Tools

The compiler integrates seamlessly with:

- **ExtractClass**: For system image class extraction
- **Bootstrap system**: For phase-based system construction  
- **Test framework**: For automated validation
- **Development environment**: For interactive compilation

This comprehensive compilation system ensures that all Smalltalk-80 source code can be accurately compiled to executable bytecode while preserving complete semantic information for runtime execution.