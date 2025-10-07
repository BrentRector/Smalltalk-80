# Scripts Directory

**Status**: Most scripts in this directory are **OBSOLETE** and kept for historical reference only.

## Why Obsolete?

The project has migrated from multi-phase C# code generation to a JSON-based cold start format. The compiler now generates a single `Smalltalk-80.json` file, and the interpreter loads it directly.

**Old approach** (deprecated):
- Compiler generated C# code (`ColdStartData.cs`, `Layouts.cs`)
- Complex multi-phase bootstrap scripts
- Manual dictionary sizing, literal extraction, etc.

**New approach** (current):
- Compiler generates `Smalltalk-80.json` (schema-validated)
- Interpreter loads JSON in 10 deterministic phases
- Zero manual intervention required

## Still Used

**ANTLR4 Generation** (in `Smalltalk80.Compiler/` directory):
- `GenerateIfNewer.ps1` - Regenerates parser if grammar changed
- `Invoke-Antlr4CSharp.ps1` - Calls ANTLR4 Java tool
- `build.ps1` - Build script with ANTLR generation

These are the only scripts actively maintained.

## Obsolete Scripts

All scripts in this directory are from the old multi-phase bootstrap system:
- `*Phase*.ps1` - Phase management (obsolete)
- `*Bootstrap*.ps1` - Bootstrap generation (obsolete)
- `*Validate*.ps1` - Validation scripts (obsolete - now built into interpreter)
- `*Analyze*.ps1` - Analysis scripts (obsolete)
- `Extract*.ps1` - Class extraction (obsolete)
- `Fix*.ps1` - Manual fixes (obsolete)

**Do not use these scripts.** They reference old data structures and file formats that no longer exist.

## What to Use Instead

**Generate cold start data**:
```bash
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- Smalltalk-80.st
# Generates Smalltalk-80.json
```

**Run interpreter**:
```bash
cd Smalltalk80.Interpreter
dotnet run -- repl --gc-log --validate-gc
```

**Regenerate ANTLR parser** (if grammar changes):
```bash
cd Smalltalk80.Compiler
pwsh GenerateIfNewer.ps1
```

See [CLAUDE.md](../CLAUDE.md) and [Interpreter-Architecture.md](../docs/Interpreter-Architecture.md) for complete documentation.
