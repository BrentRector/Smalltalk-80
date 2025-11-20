# HeapManager Tests - Correct Build Sequence

## Issue: Clean Build Removes ANTLR Generated Files

When running `dotnet clean`, it removes the ANTLR-generated parser files (`Smalltalk80Parser.cs`, `Smalltalk80Lexer.cs`, etc.) causing build errors:
```
error CS0246: The type or namespace name 'Smalltalk80Parser' could not be found
```

## Solution: Regenerate ANTLR Files Before Building

### Option 1: Build Without Clean (Recommended)

Since the trait is a simple attribute, you can just rebuild without cleaning:

```bash
cd Smalltalk80.Interpreter.Tests

# Just rebuild (faster, preserves ANTLR files)
dotnet build

# Run HeapManager tests
dotnet test --filter "Category=HeapManager"
```

This should work because:
- The `[Trait]` attribute is source code, not a build artifact
- Rebuilding will recompile the source with the trait
- ANTLR files stay intact

### Option 2: Full Clean + Regenerate ANTLR (If Needed)

If you really need a clean build:

```bash
cd Smalltalk-80

# Step 1: Clean
dotnet clean

# Step 2: Regenerate ANTLR parser files (Windows)
pwsh ./Invoke-Antlr4CSharp.ps1

# OR (Linux/Mac)
bash ./invoke-antlr4-csharp.sh

# Step 3: Build everything
dotnet build

# Step 4: Run tests
cd Smalltalk80.Interpreter.Tests
dotnet test --filter "Category=HeapManager"
```

### Option 3: Selective Clean (Best of Both)

Clean only the test project, not the compiler:

```bash
# Only clean test project (preserves ANTLR files in compiler)
cd Smalltalk80.Interpreter.Tests
dotnet clean
dotnet build

# Run tests
dotnet test --filter "Category=HeapManager"
```

## Quick Reference

### Fastest Path to Run HeapManager Tests

```bash
# From project root
cd Smalltalk80.Interpreter.Tests

# Simple rebuild (no clean needed)
dotnet build

# Run HeapManager tests
dotnet test --filter "Category=HeapManager"
```

**Expected Result:**
```
Test summary: total: 85, failed: 0, succeeded: 85, skipped: 0
```

## Why This Happens

1. ANTLR generates parser C# files from grammar (`.g4` files)
2. These generated files are in `bin/` or `obj/` directories
3. `dotnet clean` removes all build outputs, including generated files
4. Build fails because source code references the generated parser
5. Must regenerate ANTLR files before building

## When You Actually Need Clean Build

You typically **don't need** `dotnet clean` just to pick up a trait attribute. A simple `dotnet build` will:
- Detect source code changes (like adding `[Trait]`)
- Recompile those files
- Produce a new .dll with the trait metadata

Clean build is only needed when:
- Build artifacts are corrupted
- Switching branches with major changes
- Troubleshooting mysterious build issues
- Build scripts get confused about dependencies

For adding a trait attribute, **simple rebuild is sufficient**.
