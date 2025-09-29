# Standalone Smalltalk-80 Compiler

This is a self-contained Smalltalk-80 compiler that converts Smalltalk source code to bytecode in JSON format.

## Files

- `Smalltalk80.g4` - ANTLR4 grammar file for Smalltalk-80
- `Generated/` - Parser files generated from ANTLR4 grammar
- `ANTLR4/` - Contains antlr-4.13.2-complete.jar for parser generation
- `BytecodeCompiler.cs` - Main bytecode compilation logic
- `Program.cs` - Command-line interface
- `CompilerDataTypes.cs` - Data structures for compilation
- `GenerateIfNewer.ps1` - Automatically regenerates parser when grammar changes
- `Invoke-Antlr4CSharp.ps1` - ANTLR invocation script
- `build.ps1` - Complete build script with automatic parser regeneration

## Building

The project is self-contained and automatically handles parser generation:

### Standard .NET Build Commands
```bash
dotnet clean  # Removes build outputs AND generated parser files
dotnet build  # Automatically regenerates parser if needed, then builds
```

This works at both solution and project level. The build system:
1. Detects if Generated parser files are missing or outdated
2. Automatically regenerates them from the grammar using included ANTLR jar
3. Compiles the project with the generated parser

### Alternative Build Options

#### PowerShell build script (includes extra validation)
```powershell
./build.ps1
```

#### Manual parser regeneration
```powershell
./GenerateIfNewer.ps1  # Manually regenerate parser if grammar changed
```

## Parser Generation Details

The Generated/ folder contains ANTLR-generated parser files from `Smalltalk80.g4`:
- Files are automatically regenerated when missing or when grammar changes
- `dotnet clean` removes these files for a complete clean rebuild
- `dotnet build` regenerates them automatically as needed
- No manual steps required - everything is integrated into MSBuild

## Running

```bash
dotnet run <input.st> [output.json]
# or
./bin/Debug/net9.0/StandaloneCompiler.exe <input.st> [output.json]
```

## Requirements

- .NET 9.0 SDK or later
- Java Runtime (for ANTLR parser generation only)
- PowerShell (for automatic build scripts)

Note: The project uses `Antlr4.Runtime.Standard` version 4.13.1 and includes `antlr-4.13.2-complete.jar` for parser generation.