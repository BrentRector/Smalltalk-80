# Smalltalk-80 System - Complete Build and Run Guide

## Table of Contents
- [Prerequisites](#prerequisites)
- [Building the System](#building-the-system)
- [Running Tests](#running-tests)
- [Using the Standalone Compiler](#using-the-standalone-compiler)
- [Running the Interpreter](#running-the-interpreter)
- [Utility Programs](#utility-programs)
- [Development Workflow](#development-workflow)
- [Troubleshooting](#troubleshooting)

## Prerequisites

### Required Software
- **.NET 9.0 SDK** or later
- **PowerShell** (Windows) or **Bash** (Linux/Mac) for scripts
- **Git** for version control
- **VS Code** with C# Dev Kit (optional, for IDE support)

### Verify Installation
```bash
# Check .NET version (should be 9.0 or later)
dotnet --version

# Check PowerShell (Windows)
pwsh --version

# List installed SDKs
dotnet --list-sdks
```

## Building the System

### Complete Build Process

```bash
# 1. Clean previous build artifacts
dotnet clean

# 2. Build all projects (generates ANTLR parser, compiles C#, copies bootstrap files)
dotnet build

# Expected output: 
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)
```

### Build Options

```bash
# Build without restore (faster for subsequent builds)
dotnet build --no-restore

# Build in Release mode
dotnet build -c Release

# Build with test execution
dotnet build -p:RunTests=true

# Build specific project
dotnet build Smalltalk80/Smalltalk80.csproj
```

### Project Structure
```
Smalltalk-80/
├── Smalltalk80/              # Core interpreter and VM
├── Smalltalk80.Interpreter/  # Interactive interpreter executable
├── StandaloneCompiler/       # Smalltalk to bytecode compiler
├── Smalltalk80.Tests/        # Comprehensive test suite
├── Smalltalk80.CompilerTest/ # Compiler validation tests
├── Smalltalk80.ExtractClass/ # Class extraction utility
├── Bootstrap/                # Bootstrap manifests
├── complete_system_final.json # Compiled system image
└── scripts/                  # Build and utility scripts
└── docs/                     # Documentation
```

### Build Process Details

1. **ANTLR Parser Generation**: Automatically generates the Smalltalk parser from grammar
   ```bash
   cd Smalltalk80/scripts
   pwsh -ExecutionPolicy Bypass -File GenerateIfNewer.ps1
   ```

2. **Main Library Build**: Compiles the core interpreter library
   ```bash
   dotnet build Smalltalk80/Smalltalk80.csproj
   ```

3. **Bootstrap Files**: Copies essential bootstrap files to output directory

4. **Automatic Testing**: Runs bytecode tests after successful build (can be disabled)

## Running the Interpreter

### Basic Usage
```bash
# Navigate to interpreter directory
cd Smalltalk80.Interpreter

# Run with default bootstrap
dotnet run

# Run with specific bootstrap file
dotnet run -- --bootstrap ../Bootstrap/bootstrap.json

# Run with extended library
dotnet run -- --extended-library ../Bootstrap/test_counter.json
```

### Command Line Options
```bash
# Display help
dotnet run -- --help

# Enable method tracing
dotnet run -- --trace-methods

# Enable bytecode tracing  
dotnet run -- --bytecode-trace

# Debug bootstrap process
dotnet run -- --debug-bootstrap

# Print a specific class
dotnet run -- --print-class Dictionary

# Enhanced printing with more details
dotnet run -- --enhanced-print

# List all bootstrap classes
dotnet run -- --list-bootstrap-classes

# Test critical methods
dotnet run -- --test-critical-methods

# Skip Phase 3 initialization
dotnet run -- --skip-phase3

# Skip extended library loading
dotnet run -- --skip-extended
```

### Running Smalltalk Code
```bash
# Execute a Smalltalk source file
dotnet run -- my_program.st

# Interactive mode (if implemented)
dotnet run -- --interactive
```

## Running Tests

### Test Suite Overview
The Smalltalk-80 system includes comprehensive test coverage:
- **Bytecode Tests**: Validates all 256 bytecode instructions
- **Compiler Tests**: Verifies source-to-bytecode compilation
- **Phase 3 Tests**: Bootstrap and system initialization tests

### Running the Test Suite

```bash
# Run bytecode test suite (primary test method)
dotnet run --project Smalltalk80.Tests

# Use test scripts for convenience
./run-tests.ps1    # Windows PowerShell
./run-tests.sh      # Linux/Mac

# Run tests during build
dotnet build -p:RunTests=true

# Run compiler tests
dotnet run --project Smalltalk80.CompilerTest
```

### Test Output Example
```
=== Smalltalk-80 Bytecode Test Runner ===

LEVEL 1: Push Operations
========================
  ✓ 00: PushReceiverVariable0
  ✓ 01: PushReceiverVariable1
  ... (tests all 256 bytecodes)

=== Test Results ===
Time: 523ms
Tests: 256/256 passed
Bytecodes: 256/256 tested (100.0%)
```

## Using the Standalone Compiler

### Overview
The standalone compiler transforms Smalltalk source code into bytecode JSON manifests.

### Basic Usage

```bash
# Compile a Smalltalk source file
dotnet run --project StandaloneCompiler -- input.st [output.json]

# Examples
dotnet run --project StandaloneCompiler -- Point.st
# Creates: Point.json

dotnet run --project StandaloneCompiler -- MyClass.st compiled.json
# Creates: compiled.json
```

### Compiling the System Image

```bash
# Compile the entire Smalltalk-80 system (223 classes, 4,493 methods)
dotnet run --project StandaloneCompiler -- "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st" system.json
```

### Using the Built Executable

```bash
# After building, use the executable directly
./bin/Debug/net9.0/Smalltalk80.Compiler.exe source.st output.json
```

### Compiler Output
The compiler generates JSON with:
- **classes**: Array of class definitions (name, superclass, variables, category)
- **methods**: Array of compiled methods (selector, bytecode, literals, metadata)
## Development Workflow

### Typical Development Cycle

1. **Make Code Changes**
   ```bash
   # Edit source files in your preferred editor
   code .  # Open VS Code in project root
   ```

2. **Clean and Build**
   ```bash
   dotnet clean
   dotnet build
   ```

3. **Run Tests**
   ```bash
   dotnet run --project Smalltalk80.Tests
   ```

4. **Test Your Changes**
   ```bash
   # Compile Smalltalk code
   dotnet run --project StandaloneCompiler -- test.st
   
   # Run interpreter with your compiled code
   dotnet run --project Smalltalk80.Interpreter -- --bootstrap test.json
   ```

### Continuous Integration

```bash
# Full CI pipeline simulation
dotnet clean
dotnet restore
dotnet build --no-restore
dotnet run --project Smalltalk80.Tests
dotnet run --project Smalltalk80.CompilerTest
```

## Utility Programs

### 1. Class Extractor (Smalltalk80.ExtractClass)
Extracts class definitions from the Smalltalk-80 system image.

```bash
cd Smalltalk80.ExtractClass

# Extract a specific class
dotnet run -- --class Dictionary

# Extract to specific output directory
dotnet run -- --class Array --output extracted/

# Extract with all methods
dotnet run -- --class String --include-methods

# Show help
dotnet run -- --help
```

### 3. Bootstrap Utilities

#### Update Phase 2 Methods
```powershell
cd scripts
pwsh -File UpdatePhase2Methods.ps1
```

#### Analyze Dictionary Sizes
```powershell
cd scripts
pwsh -File AnalyzeDictionarySizes.ps1
```

#### Validate Phase 2 Bootstrap
```powershell
cd scripts
pwsh -File ValidatePhase2Simple.ps1
```

## Bootstrap Process

The Smalltalk-80 system bootstraps in three phases:

### Phase 1: Minimal Core Classes
Creates essential classes: Object, Behavior, Class, Metaclass, CompiledMethod, MethodDictionary, Symbol, String, Array, ByteArray, Collection, Dictionary, SystemDictionary.

### Phase 2: Method Installation
Installs compiled methods with bytecode into Phase 1 classes and creates additional classes (UndefinedObject, Boolean, True, False, Magnitude, LookupKey, Association).

### Phase 3: Full System Classes
Adds remaining Smalltalk-80 classes with complete method implementations.

## Environment Variables

### Tracing Control
```bash
# Enable method tracing
export TRACE_ENABLED=true

# Enable bytecode tracing
export BYTECODE_TRACE_ENABLED=true

# Disable all tracing (for performance)
export TRACE_ENABLED=false
```

## Build Artifacts

Build outputs are located in:
```
bin/Debug/net9.0/
├── Smalltalk80.dll              # Core library
├── Smalltalk80.Interpreter.exe  # Interpreter executable
├── Smalltalk80.Compiler.exe     # Compiler
├── Smalltalk80.ExtractClass.exe # Class extractor
├── Smalltalk80.Tests.exe        # Test suite
└── Bootstrap/                   # Bootstrap files
    ├── bootstrap.json
    ├── bootstrap_phase2.json
    └── test_*.json
```

## Troubleshooting

### Common Issues and Solutions

1. **Build Warnings After Clean**
   ```bash
   # Solution: Build was already fixed to have 0 warnings
   dotnet clean
   dotnet build
   # Expected: 0 Warning(s), 0 Error(s)
   ```

2. **Tests Not Running with `dotnet test`**
   ```bash
   # Tests are executable, not xUnit/NUnit tests
   # Run as executable instead:
   dotnet run --project Smalltalk80.Tests
   ```

3. **Compiler Not Found**
   ```bash
   # Ensure StandaloneCompiler is built
   dotnet build StandaloneCompiler
   # Executable at: bin/Debug/net9.0/Smalltalk80.Compiler.exe
   ```

4. **Method Parsing Errors in Compiler**
   ```bash
   # Compiler supports all operator methods
   # Ensure source file uses proper Smalltalk syntax
   # Test with known-good file:
   dotnet run --project StandaloneCompiler -- "Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st"
   ```

5. **Missing Classes in JSON Output**
   ```bash
   # Compiler now handles all class types:
   # - subclass:
   # - variableSubclass:
   # - variableByteSubclass:
   # - variableWordSubclass:
   ```

### Debug Mode

For detailed debugging:
```bash
# Run with all debug options
dotnet run -- --debug-bootstrap --trace-methods --bytecode-trace

# Attach debugger in VS Code
# Launch configuration in .vscode/launch.json
```

## Performance Tips

1. **Disable Tracing**: Tracing significantly impacts performance
   ```bash
   dotnet run -- --no-trace
   ```

2. **Skip Extended Library**: For faster startup
   ```bash
   dotnet run -- --skip-extended
   ```

3. **Use Release Build**: For production use
   ```bash
   dotnet build -c Release
   dotnet run -c Release
   ```

## Contributing

When making changes:

1. **Run Tests**: Ensure all tests pass
   ```bash
   dotnet test
   ```

2. **Update Bootstrap**: If modifying core classes
   ```bash
   cd scripts
   pwsh -File UpdatePhase2Methods.ps1
   ```

3. **Validate Changes**: Check bootstrap integrity
   ```bash
   pwsh -File ValidatePhase2Simple.ps1
   ```

4. **Document**: Update relevant documentation in `docs/`

## Additional Resources

- [CLAUDE.md](../CLAUDE.md) - Contribution guidelines and conventions
- [Smalltalk-80 Blue Book](http://stephane.ducasse.free.fr/FreeBooks/BlueBook/Bluebook.pdf) - Language specification
- [ANTLR Documentation](https://www.antlr.org/) - Parser generator documentation

## Support

For issues or questions:
1. Check existing issues in the repository
2. Review test output for specific bytecode failures
3. Enable debug tracing to diagnose runtime issues
4. Consult the Blue Book for language specification details