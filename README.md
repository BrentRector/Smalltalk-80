# Smalltalk-80

This rep## VS Code Setup

The repository contains a `.vscode` folder with build and debug tasks. After
installing the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit)
extension, open this folder in VS Code and press <kbd>F5</kbd> to build and
debug. 

### Integrated Build and Test Workflow

The VS Code build system is configured to automatically run all test cases after successful compilation:

- **Default Build** (Ctrl+Shift+B): Builds the solution and runs comprehensive test suite
- **Build Task**: Compiles `Smalltalk80.sln` with automatic ANTLR generation
- **Test Integration**: Automatically executes all 9 test categories after successful build
- **Results Display**: Shows test results with pass/fail status in the terminal

### Available VS Code Tasks

1. **`build`** (default) - Complete build and test workflow
2. **`build-and-test`** - Build only without tests  
3. **`run-tests`** - Run comprehensive test suite independently
4. **`generate-antlr`** - Generate ANTLR parser files only
5. **`run`** - Run the interpreter
6. **`repl`** - Start REPL mode

### Task Execution Flow
```
Ctrl+Shift+B (Build)
  ↓
ANTLR Generation → .NET Build → Comprehensive Tests
  ↓
Test Results: Pass/Fail Summary with Detailed Logging
```

### Manual Task Execution
Access tasks via Command Palette (`Ctrl+Shift+P` → "Tasks: Run Task") or use keyboard shortcuts:
- `Ctrl+Shift+B` - Default build with tests
- `Ctrl+Shift+P` → "Tasks: Run Task" → Select specific task contains a basic prototype of a Smalltalk-80 interpreter written in C#. The solution includes a console application demonstrating a minimal bytecode parser and execution engine.

## Building

Use the .NET SDK (`dotnet`) to build the solution:

The projects target **.NET&nbsp;9.0**, so be sure to install a matching SDK before building.

```bash
dotnet build Smalltalk80.sln
```

Running the console app executes a small bytecode sequence and prints the result.

## REPL

Launch the interactive Smalltalk-80 REPL with:

```bash
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --repl
```

You can also start it directly from VS Code using the **Smalltalk REPL** launch configuration or the **repl** task.

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
