# Smalltalk-80 Compiler

A comprehensive, modern Smalltalk-80 compiler written in C# 13 with a clean, extensible architecture.

## Architecture Overview

The compiler uses a multi-phase architecture with clear separation of concerns:

### 1. Parsing Phase
- **ANTLR4 Grammar**: Uses the Smalltalk80.g4 grammar to parse source code
- **Parse Tree**: ANTLR generates a parse tree from the source

### 2. AST Construction Phase
- **AST Builder**: Converts ANTLR parse trees into a comprehensive Abstract Syntax Tree
- **Immutable Nodes**: All AST nodes are immutable records for thread safety
- **Source Locations**: Every node tracks its source location for error reporting

### 3. Processing Phase
- **Pluggable Processors**: Extensible architecture using the `IAstProcessor` interface
- **Visitor Pattern**: AST nodes accept visitors for traversal and transformation
- **Multiple Passes**: Supports multiple processing passes over the AST

### 4. Code Generation Phase
- **Bytecode Generator**: Unified bytecode generation and compilation
- **Single-Pass Compilation**: Efficient bytecode generation with inline literal collection
- **Dual Interface**: Works both as IAstVisitor for direct use and IAstProcessor for JSON output
- **JSON Output**: Generates bytecode in JSON format for interpreter consumption

## Project Structure

```
Smalltalk80.Compiler/
├── AST/                      # Abstract Syntax Tree
│   ├── AstNode.cs           # Base AST node class
│   ├── IAstVisitor.cs       # Visitor interfaces
│   ├── AstBuilder.cs        # Parse tree to AST converter
│   ├── Nodes/               # AST node implementations
│   │   ├── ProgramNodes.cs      # Program, Class, Method
│   │   ├── StatementNodes.cs    # Statements
│   │   ├── ExpressionNodes.cs   # Expressions
│   │   ├── LiteralNodes.cs      # Literals
│   │   ├── MessageNodes.cs      # Messages
│   │   └── DeclarationNodes.cs  # Declarations
│   └── Processors/          # AST processors
│       └── IAstProcessor.cs     # Processor interface
├── Compilation/             # Bytecode compilation
│   ├── BytecodeGenerator.cs     # Bytecode generator & compiler
│   ├── CompilationContext.cs    # Compilation context
│   ├── CompilationScope.cs      # Variable & literal tracking
│   └── Models/                   # Compilation data models
├── ANTLR4/                  # ANTLR tools
│   └── antlr-4.13.2-complete.jar
├── Generated/               # ANTLR-generated code
├── Program.cs               # Command-line interface
└── Smalltalk80.g4          # Grammar file
```

## Building the Compiler

### Prerequisites
- .NET 9.0 SDK
- Java runtime (for ANTLR)

### Windows
```powershell
.\build.ps1
```

### Linux/macOS
```bash
./build.sh
```

### Build Options
- `--clean`: Clean build artifacts
- `--rebuild`: Clean and rebuild
- `--release`: Build in Release configuration

## Usage

### Basic Usage
```bash
Smalltalk80.Compiler input.st
```

### Specify Output File
```bash
Smalltalk80.Compiler input.st output.json
```

### Options
- `--verbose`, `-v`: Show verbose output
- `--ast`: Dump the AST structure

### Examples

Compile a method:
```bash
Smalltalk80.Compiler method.st method.json
```

Compile with verbose output:
```bash
Smalltalk80.Compiler program.st --verbose
```

Debug AST construction:
```bash
Smalltalk80.Compiler test.st --ast
```

## AST Node Hierarchy

### Core Nodes
- **AstNode**: Base class for all nodes
- **ProgramNode**: Root of the AST
- **ClassDefinitionNode**: Class definitions
- **MethodDefinitionNode**: Method definitions

### Statement Nodes
- **StatementListNode**: List of statements
- **ReturnStatementNode**: Return statements (^expr)
- **ExpressionStatementNode**: Expression as statement

### Expression Nodes
- **AssignmentNode**: Variable assignments
- **CascadeNode**: Cascade expressions
- **MessageSendNode**: Message sends
- **BlockNode**: Block closures
- **VariableNode**: Variable references

### Literal Nodes
- **NumberLiteralNode**: Numeric literals
- **StringLiteralNode**: String literals
- **SymbolLiteralNode**: Symbol literals (#symbol)
- **ArrayLiteralNode**: Array literals #(...)
- **CharacterLiteralNode**: Character literals ($c)
- **BooleanLiteralNode**: true/false
- **NilLiteralNode**: nil
- **SelfLiteralNode**: self
- **SuperLiteralNode**: super

### Message Nodes
- **UnaryMessageNode**: No arguments
- **BinaryMessageNode**: Binary operators
- **KeywordMessageNode**: Keyword messages

## Extending the Compiler

### Adding a New AST Processor

1. Implement `IAstProcessor`:
```csharp
public class MyProcessor : IAstProcessor
{
    public string Name => "My Processor";
    
    public ProcessorResult Process(AstNode node, ProcessorContext context)
    {
        // Process the AST
        return new MyResult(...);
    }
}
```

2. Create a custom result type:
```csharp
public class MyResult : ProcessorResult
{
    public MyData Data { get; }
    
    public MyResult(MyData data) : base(true)
    {
        Data = data;
    }
}
```

3. Use the processor:
```csharp
var processor = new MyProcessor();
var result = processor.Process(ast, context);
```

### Adding New AST Nodes

1. Define the node class:
```csharp
public sealed record MyNode(
    string Property,
    SourceLocation Location) : AstNode(Location)
{
    public override T Accept<T>(IAstVisitor<T> visitor) 
        => visitor.VisitMyNode(this);
        
    public override void Accept(IAstVisitor visitor) 
        => visitor.VisitMyNode(this);
}
```

2. Add visitor methods to `IAstVisitor`:
```csharp
T VisitMyNode(MyNode node);
```

3. Update `AstBuilder` to construct the new node type

## Design Principles

### Immutability
- All AST nodes are immutable records
- Thread-safe by design
- Enables caching and parallel processing

### Separation of Concerns
- Parsing is separate from AST construction
- AST construction is separate from processing
- Each processor has a single responsibility

### Extensibility
- Visitor pattern for AST traversal
- Pluggable processor architecture
- Easy to add new optimization passes

### Modern C# 13
- Primary constructors
- Collection expressions
- Pattern matching
- Record types
- Nullable reference types
- File-scoped namespaces

## Testing

The compiler can be tested with various Smalltalk source files:

### Simple Expression
```smalltalk
3 + 4
```

### Method Definition
```smalltalk
factorial
    self = 0 ifTrue: [^1].
    ^self * (self - 1) factorial
```

### Class Definition
```smalltalk
Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!
```

## Output Format

The compiler generates JSON with the following structure:

```json
{
  "bytecode": [112, 118, 176, ...],
  "literals": ["#selector", "Association(key: #Var, value: null)", ...],
  "numArgs": 2,
  "numTemps": 4,
  "stackDepth": 3,
  "primitive": 0,
  "source": "original source code"
}
```

## Performance

- **AST Construction**: O(n) where n is parse tree size
- **Literal Collection**: O(n) single pass
- **Bytecode Generation**: O(n) single pass
- **Memory**: Immutable AST allows garbage collection of parse tree

## Future Enhancements

Potential improvements for the architecture:

1. **Type Inference**: Add type analysis pass
2. **Optimization Passes**: Dead code elimination, constant folding
3. **Error Recovery**: Better error messages with source locations
4. **Incremental Compilation**: Compile only changed methods
5. **Source Maps**: Map bytecode back to source for debugging
6. **Multiple Targets**: Generate JavaScript, WASM, or native code
7. **Language Server Protocol**: IDE integration support

## License

See LICENSE file in the repository root.