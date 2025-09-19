# Smalltalk-80

A complete implementation of the Smalltalk-80 system in C#, including an interpreter, standalone bytecode compiler, and comprehensive test suite.

## Prerequisites

- **.NET 9.0 SDK** or later
- PowerShell (for Windows scripts) or Bash (for Unix scripts)
- VS Code with C# Dev Kit extension (optional, for IDE support)

## Quick Start

### Building the System

```bash
# Clean build
dotnet clean
dotnet build

# Build without running tests
dotnet build --no-restore
```

### Running Tests

```bash
# Run the bytecode test suite (tests all 256 bytecodes)
dotnet run --project Smalltalk80.Tests

# Or use the test scripts
./run-tests.ps1    # Windows PowerShell
./run-tests.sh      # Linux/Mac

# Run with test flag during build
dotnet build -p:RunTests=true
```

## Project Structure

### Core Projects

- **Smalltalk80/** - Core interpreter and VM implementation
- **Smalltalk80.Interpreter/** - Interactive interpreter executable
- **StandaloneCompiler/** - Smalltalk source to bytecode compiler
- **Smalltalk80.ExtractClass/** - Class extraction utility
- **Smalltalk80.Tests/** - Comprehensive bytecode test suite
- **Smalltalk80.CompilerTest/** - Compiler validation tests

### Key Files

- **complete_system_final.json** - Compiled Smalltalk-80 system image (223 classes, 4,493 methods)
- **Bootstrap/** - Bootstrap manifests for system initialization
- **StandardLibrary.st** - Core Smalltalk-80 standard library

## Building the System

### Complete Build Process

```bash
# 1. Clean previous build artifacts
dotnet clean

# 2. Build all projects
dotnet build

# 3. Verify no warnings or errors
# Build should complete with 0 warnings, 0 errors
```

The build process automatically:
- Generates ANTLR4 parser from Smalltalk grammar
- Compiles all C# projects
- Copies bootstrap files to output directory
- Optionally runs Phase 3 comprehensive tests

## Standalone Compiler

The standalone compiler converts Smalltalk source code to bytecode JSON manifests.

### Usage

```bash
# Compile a Smalltalk source file
dotnet run --project StandaloneCompiler -- input.st [output.json]

# Or use the built executable
./bin/Debug/net9.0/Smalltalk80.Compiler.exe input.st [output.json]

# If output file is omitted, defaults to input.json
dotnet run --project StandaloneCompiler -- MyClass.st  # Creates MyClass.json
```

### Command-Line Arguments

1. **input.st** (required) - Smalltalk source file to compile
2. **output.json** (optional) - Output JSON file path (defaults to input filename with .json extension)

### Example: Compiling the System Image

```bash
# Compile the entire Smalltalk-80 system
dotnet run --project StandaloneCompiler -- "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" system.json

# Result: Creates system.json with all 223 classes and 4,493 methods
```

### Output Format

The compiler generates a JSON manifest with:
- **classes** - Array of class definitions with instance variables, class variables, etc.
- **methods** - Array of compiled methods with bytecode, literals, and metadata

See [SMALLTALK_COMPILER.md](SMALLTALK_COMPILER.md) for detailed JSON schema.

## Interpreter

### REPL Mode

Launch the interactive Smalltalk-80 REPL:

```bash
dotnet run --project Smalltalk80.Interpreter -- --repl
```

### Command-Line Arguments

```bash
# Load and execute a Smalltalk source file
dotnet run --project Smalltalk80.Interpreter -- --filein "source.st"

# Load a single method
dotnet run --project Smalltalk80.Interpreter -- --load "method.st"

# Load bootstrap manifest
dotnet run --project Smalltalk80.Interpreter -- --bootstrap manifest.json

# Enable method tracing
dotnet run --project Smalltalk80.Interpreter -- --trace-methods

# Enable bytecode tracing
dotnet run --project Smalltalk80.Interpreter -- --bytecode-trace

# Print a specific class
dotnet run --project Smalltalk80.Interpreter -- --print-class Dictionary
```

## VS Code Setup

The repository contains a `.vscode` folder with build and debug tasks. After
installing the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
extension, open this folder in VS Code and press <kbd>F5</kbd> to build and
debug. The *Build* task compiles `Smalltalk80.sln`, and the default launch
configuration runs the console application.

## Loading Source Files

Use the `--load` option to compile a single method file where the first line takes the form `ClassName>>selector` followed by the method body. For classic Smalltalk file-in sources that contain multiple class and method definitions, use the `--filein` option instead:

```bash
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --filein "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
```

## Project Structure

### Core Components
- **Smalltalk80/** - Main interpreter implementation in C#
- **StandardLibrary.st** - Core Smalltalk-80 standard library classes
- **docs/** - Documentation and implementation notes

### Testing
- **Testing/** - Complete test suite and testing framework
  - `TestLibrary.st` - Core testing framework with TestCase base class
  - `BasicTestMethods.st` - Comprehensive functionality tests
  - `README.md` - Detailed testing documentation
  - Various specific test files for different features

### Testing Framework Usage

The project includes a comprehensive test suite covering all standard library classes.

#### Quick Start - Run All Tests
```bash
# Windows PowerShell
.\scripts\Run-ComprehensiveTests.ps1

# Windows Command Prompt
.\scripts\run-tests.bat

# Manual execution with proper dependencies
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/StandardLibraryTestSuite.st"
```

#### Test Categories Available
- **ObjectTests** - Root Object class methods
- **BooleanTests** - Boolean logic and control flow
- **NumberTests** - Arithmetic and numeric operations
- **CharacterTests** - Character operations and conversions
- **UndefinedObjectTests** - nil object behavior
- **CollectionTests** - Collection enumeration methods
- **StringTests** - String operations and comparisons
- **BlockContextTests** - Block evaluation and closures

#### Advanced Test Execution
```powershell
# Run specific test category
.\scripts\Run-ComprehensiveTests.ps1 -TestCategory "ObjectTests"

# Run with verbose output
.\scripts\Run-ComprehensiveTests.ps1 -Verbose

# Run individual test quickly
.\scripts\Run-QuickTests.ps1 -TestName "BooleanTests"

# Stop on first failure
.\scripts\Run-ComprehensiveTests.ps1 -StopOnFirstFailure
```

#### Dependency Loading Order
Tests require proper loading sequence:
1. **Standard Library** (automatically loaded)
2. **Test Framework** (`Testing/TestLibrary.st`)
3. **Test Cases** (specific test files)

#### Manual Test Loading
```bash
# Load testing framework first
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st"

# Then load specific tests with dependencies
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --source "Testing/TestLibrary.st" --source "Testing/ObjectTests.st"

# Interactive REPL testing
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --repl
Smalltalk> LoadFile Testing/TestLibrary.st
Smalltalk> LoadFile Testing/ObjectTests.st
```
