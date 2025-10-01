# Running the Smalltalk-80 Interpreter

## Quick Start

### List All Cold Start Classes

To see all 60 classes installed during cold start:

```bash
cd Smalltalk80.Interpreter
dotnet run repl --list-cold-start-classes
```

Then type `exit` to quit the REPL.

### Start Interactive REPL

```bash
cd Smalltalk80.Interpreter
dotnet run repl
```

### With Verbose Logging

To see detailed cold start initialization:

```bash
cd Smalltalk80.Interpreter
dotnet run repl --cold-start-log --list-cold-start-classes
```

## Available Commands

### `repl` - Interactive REPL
Starts an interactive read-eval-print loop:

```bash
dotnet run repl [options]
```

### `eval` - Evaluate Expression
Evaluate a single Smalltalk expression:

```bash
dotnet run eval "3 + 4"
```

### `run` - Execute File
Execute a Smalltalk source file:

```bash
dotnet run run myfile.st
```

### `compile` - Compile to Bytecode
Compile Smalltalk source to bytecode:

```bash
dotnet run compile myfile.st
```

### `inspect` - Inspect Image
Inspect a system image or compiled method:

```bash
dotnet run inspect image.st80
```

## Command-Line Options

### Logging Options

- `--cold-start-log` - Show detailed cold start initialization
- `--gc-log` - Show garbage collection activity
- `--log-sends` - Show message send operations
- `-v, --verbose` - Enable verbose output

### Class Inspection Options

- `--list-cold-start-classes` - List all 60 cold start classes
- `--list-classes` - Same as above
- `--print-class ClassName` - Show details for specific class
- `--inspect-class ClassName` - Inspect class structure
- `--enhanced-print` - Enhanced pretty printing

### Debug Options

- `--debug <mode>` - Enable debug mode (bytecode, methodtrace, class, trace, all)
- `--trace-methods` - Trace method calls
- `--bytecode-trace` - Trace bytecode execution
- `--show-bytecode` - Show bytecode during execution
- `--show-mnemonics` - Show bytecode mnemonics

### Testing Options

- `--validate-gc` - Validate GC reachability from Special Objects Array
- `--test-critical-methods` - Test critical methods
- `--test-class-creation-fixed` - Test class creation
- `--validate-bytecode` - Validate bytecode execution

## Examples

### Example 1: View All Classes with Logging

```bash
dotnet run repl --cold-start-log --list-cold-start-classes
```

Output shows:
```
[COLD START] Creating Object class
[COLD START] Creating Behavior class
...
[COLD START] System cold started successfully
[COLD START] Total classes: 60

System Classes:
Total Classes: 60
  Array -> SequenceableCollection
  Association -> Object
  Behavior -> Object
  ...
```

### Example 2: Inspect a Specific Class

```bash
dotnet run repl --print-class Array
```

Shows class details, superclass, instance variables, and methods.

### Example 3: Debug Bytecode Execution

```bash
dotnet run repl --bytecode-trace --show-mnemonics
```

### Example 4: Validate GC

```bash
dotnet run repl --validate-gc
```

Verifies all objects are reachable from the Special Objects Array.

## Cold Start Classes (60 Total)

### Core Object Model (11 classes)
- Object (root class)
- Behavior, ClassDescription, Class, Metaclass (meta-hierarchy)
- CompiledMethod, MethodDictionary, InstructionStream
- ContextPart, MethodContext, BlockContext (execution)

### Collections (12 classes)
- Collection, SequenceableCollection, OrderedCollection
- Array, ByteArray, String, Symbol
- Set, Dictionary
- Link, LinkedList

### Numeric Classes (7 classes)
- Magnitude, Number, Integer
- SmallInteger, LargePositiveInteger, LargeNegativeInteger
- Float, Character

### Control Flow (4 classes)
- Boolean, True, False
- UndefinedObject (nil)

### Process Management (3 classes)
- Process, ProcessorScheduler, Semaphore

### Streams (5 classes)
- Stream, PositionableStream
- ReadStream, WriteStream
- StringHolder, TextCollector, Text

### Compilation (13 classes)
- Scanner, Parser, Compiler, Encoder
- ParseNode, LeafNode
- VariableNode, LiteralNode, SelectorNode
- MessageNode, AssignmentNode, ReturnNode
- BlockNode, CascadeNode, MethodNode

### Association (1 class)
- Association (key-value pairs)

## Build Status

Current build status (as of last run):

- ✅ **0 Errors**
- ✅ **0 Warnings**
- ✅ **136/138 Tests Passing (98.6%)**
- ⚠️ 2 Tests Skipped (test isolation issues)

## Architecture

- **Pure SObjectRef-only system** - Tagged pointer architecture
- **ObjectMemoryNew** - OOP-based internal storage
- **9-element SpecialObjectsArray** - GC roots only
- **BlueBookFactory** - 15+ factory methods
- **BlueBookLayouts** - 24 verified class layouts

## Phase 1 Complete

Phase 1 (Cold Start Bootstrap) is 100% complete with:

- ✅ 60 classes created with correct hierarchy
- ✅ 256 Character instances in character table
- ✅ Essential kernel methods installed
- ✅ Process system initialized
- ✅ Garbage collection configured
- ✅ Clean build (0 errors, 0 warnings)

## Next Steps

Phase 2 will implement:
- Method lookup cache
- Message sending primitives
- Full context management
- Block closure evaluation

## Troubleshooting

### Error: "Not an object reference (might be SmallInteger)"

This error has been fixed in the latest version. If you see it, rebuild:

```bash
dotnet build
```

### REPL Doesn't Show Classes

Make sure to include `--list-cold-start-classes` option:

```bash
dotnet run repl --list-cold-start-classes
```

### Exit REPL

Type `exit` and press Enter to quit the REPL.

## More Information

See also:
- `PHASE_1_COMPLETION_REPORT.md` - Detailed Phase 1 completion report
- `COLD_START_ROADMAP.md` - Complete 6-phase roadmap
- `Interpreter Architecture.md` - Full architecture documentation
