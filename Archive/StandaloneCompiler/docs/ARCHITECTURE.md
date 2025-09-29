# StandaloneCompiler Architecture

## Overview
The StandaloneCompiler is a completely self-contained Smalltalk-80 to bytecode compiler that requires no external dependencies beyond the .NET runtime and the ANTLR4 runtime NuGet package.

## Self-Contained Design

### All Required Files in Folder
- **Smalltalk80.g4** - Complete ANTLR4 grammar for Smalltalk-80
- **Generated/** - Pre-generated parser files (Lexer, Parser, Visitor)
- **BytecodeCompiler.cs** - Complete bytecode generation logic
- **Program.cs** - CLI interface and compilation orchestration
- **CompilerDataTypes.cs** - All required data structures

### No External Dependencies
- Does NOT depend on any files outside this folder
- Does NOT require the solution file to build
- Does NOT share code with other projects
- Only external dependency is ANTLR4.Runtime.Standard NuGet package

### Building Independently
Three ways to build:
1. `dotnet build` in this folder
2. `./build.ps1` (PowerShell)
3. `./build.sh` (Bash)

### Parser Generation
The Generated folder contains pre-generated ANTLR4 parser files that are checked into source control. These were generated from Smalltalk80.g4 using ANTLR 4.13.1.

To regenerate (only needed if grammar changes):
```bash
java -jar antlr-4.13.1-complete.jar -Dlanguage=CSharp -visitor -no-listener Smalltalk80.g4
```

## Bytecode Generation
The compiler generates Smalltalk-80 Blue Book compatible bytecode:
- Ranges 32-63: Push Literal Constant
- Ranges 64-95: Push Literal Variable (Association value)
- Special constants: 113-119 (true, false, nil, -1, 0, 1, 2)
- Arithmetic selectors: 176-191
- Special message sends: 192-223
- Return bytecodes: 120-127

## Output Format
Generates JSON with:
- Class definitions with metadata
- Method bytecode arrays
- Literal frames
- Initialization expressions

## Usage
```bash
StandaloneCompiler <input.st> [output.json]
```

The compiler is designed to be portable and can be copied to any system with .NET runtime installed.