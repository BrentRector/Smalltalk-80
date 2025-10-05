# Claude Development Guidelines

**Last Updated**: 2025-10-05
**Status**: Current Standing Guidance
**Current Phase**: JSON Cold Start Migration - ST* Struct Removal In Progress

## Primary Documentation Reference

**📖 [Interpreter-Architecture.md](Interpreter-Architecture.md) is the canonical source of truth for all architectural decisions, design principles, and implementation details.**

All development work must align with the architecture documented in that file. Before making any significant changes:

1. Read Interpreter-Architecture.md to understand the current system state
2. Verify your changes align with the documented design principles
3. Update Interpreter-Architecture.md if your changes modify the architecture

## Core Development Principles

### 1. Blue Book Compliance

**Every design decision MUST follow the Smalltalk-80 Blue Book specification (Goldberg & Robson, 1983):**

✅ **Correct:**
- 5-element Special Objects Array (nil, false, true, Smalltalk, ProcessorScheduler)
- ActiveProcess accessed via `ProcessorScheduler.activeProcess` (instance variable)
- Table-based object memory with OOP indirection
- Tagged pointers for SmallInteger only (Character, Symbol, etc. are heap objects)
- Mark-and-sweep garbage collection

❌ **Incorrect:**
- Adding a 6th SOA element for ActiveProcess (use instance variable instead)
- Using tagged pointers for Character or other objects
- Implementing generational or copying GC (Blue Book uses mark-and-sweep)

### 2. Zero Garbage Requirement

**Cold start bootstrap MUST create exactly zero temporary objects:**

✅ **Correct:**
- Pre-intern ALL symbols before they are used
- Pre-size ALL dictionaries to correct capacity
- Deterministic OOP allocation (same objects get same OOPs every run)
- Every object reachable from the 5 SOA roots

❌ **Incorrect:**
- Creating temporary arrays, strings, or symbols during bootstrap
- Undersizing dictionaries (causes growth, creates garbage)
- Using symbols before pre-interning them
- Creating objects not reachable from SOA

**Validation:** Run GC after cold start - must collect 0 objects on all runs.

### 3. Object Reachability

**Every object MUST be reachable from one of the 5 SOA roots:**

```
1. nil (root 0) - UndefinedObject singleton
2. false (root 1) - False singleton
3. true (root 2) - True singleton
4. Smalltalk (root 3) - SystemDictionary → all classes → all methods → all symbols
5. ProcessorScheduler (root 4) - all processes → all contexts → all execution state
```

**Reachability chains:**
- Classes → `Smalltalk at: #ClassName`
- Methods → `Class.methodDict`
- Symbols → `(Smalltalk at: #Symbol) classPool at: #USTable`
- Contexts → `ProcessorScheduler.activeProcess.suspendedContext.sender...`

### 4. Per-VM Architecture

**All VM state MUST be instance-based to support multiple independent VMs:**

✅ **Per-VM (instance-based):**
- ObjectTable (heap storage)
- SpecialObjectsArray (GC roots)
- GarbageCollector
- Interpreter
- SystemDictionary reference

✅ **Shared (static):**
- JSON cold-start schema (coldstart-v1.0.0.schema.json)
- Bytecode constants
- Blue Book specifications
- Primitive implementations

❌ **Never use static for VM state** - prevents running multiple VMs.

### 6. JSON Cold Start Format

**All cold-start data is now in JSON format:**

✅ **Correct:**
- Compiler generates `Smalltalk-80.json` from source (11MB, compresses to 1.2MB)
- Schema-validated JSON conforming to `docs/schemas/coldstart-v1.0.0.schema.json`
- LocalId-based references resolved by interpreter loader
- Deterministic install order phases

❌ **Incorrect:**
- Using generated C# code (ColdStartData.cs, Layouts.cs) - these are deprecated
- Hardcoding object IDs in compiler - use stable localIds instead
- Skipping schema validation

### 5. Source File Immutability

**NEVER modify the Smalltalk-80.st source file:**

- This file is the master, canonical reference for Smalltalk-80
- All cold start data comes from compiler analysis of this file
- No hand-written method installers or hardcoded symbol lists
- The compiler extracts all information (classes, methods, literals) from the AST

## Code Quality Standards

### Language and Style

- **Target C# 13** with modern features (pattern matching, primary constructors, etc.)
- **XML documentation** required for all public members
- **Null safety** - use nullable reference types correctly
- **Expression-bodied members** when they improve readability

### File Organization

```
Smalltalk80.Interpreter/
├── ColdStart/                # JSON-based bootstrap system
│   ├── JsonColdStartBootstrap.cs # 10-phase orchestrator
│   ├── ColdStartLoader.cs        # JSON deserialization + LocalId registry
│   ├── SymbolInterning.cs        # Two-phase symbol creation
│   ├── ClassAllocator.cs         # Bootstrap/core class allocation
│   ├── ClassPopulator.cs         # Populate class fields
│   ├── ObjectAllocator.cs        # Pool dictionaries, arrays, etc.
│   ├── MethodInstaller.cs        # CompiledMethod creation
│   ├── SystemDictionaryCreator.cs # Smalltalk global dictionary
│   └── PatchApplicator.cs        # Forward reference resolution
├── Core/
│   ├── Objects/             # Object implementations
│   ├── Values/              # SObjectRef, SmallInteger
│   └── BlueBook/            # Blue Book compliance layer (LayoutManagement)
├── Memory/
│   ├── ObjectMemory.cs      # Table-based storage
│   ├── GarbageCollectorNew.cs # Mark-and-sweep GC
│   └── SpecialObjectsArray.cs # 5-field struct
├── Execution/               # Bytecode execution (MIGRATING)
│   └── Interpreter.cs       # 268 ST* references remain
└── Primitives/              # VM primitives (MIGRATING)
    └── ControlPrimitives.cs # 54 Generated namespace references remain
```

**Migration Status**:
- ✅ ColdStart/ - Fully migrated to JSON + LayoutManagement
- ✅ Memory/SpecialObjectsArray.cs - Fixed to use LayoutManagement
- ✅ Core/BlueBook/SObjectRefClassExtensions.cs - All 6 ST* references converted
- ⏳ Execution/Interpreter.cs - 68 ST* references (STMethodContext, STBlockContext)
- ⏳ Primitives/ControlPrimitives.cs - 54 Generated namespace references
- ⏳ Core/BlueBook/BlueBookFactory.cs - Various ST* type references

### Testing and Validation

**Before every commit:**

```bash
# 1. Build must succeed with no errors
dotnet build --no-restore

# 2. Run with GC validation
dotnet run --project Smalltalk80.Interpreter -- repl --gc-log --validate-gc

# 3. Verify zero garbage
# Output must show: "0 objects collected" on all GC runs
```

## Common Pitfalls to Avoid

### ❌ Mistake 1: Adding 6th SOA Element

**Wrong:**
```csharp
public readonly struct SpecialObjectsArray
{
    public readonly SObjectRef ActiveProcess; // NO!
}
```

**Right:**
```csharp
public readonly struct SpecialObjectsArray
{
    public SObjectRef ActiveProcess =>
        ProcessorScheduler.GetInstanceVariable(
            ProcessorSchedulerLayout.ActiveProcess);
}
```

### ❌ Mistake 2: Creating Temporary Objects

**Wrong:**
```csharp
// During bootstrap - creates garbage!
var tempSymbols = new List<string>();
tempSymbols.Add("someSymbol");
```

**Right:**
```csharp
// Pre-intern before use
ObjectMemoryNew.InternSymbolBootstrap("someSymbol");
```

### ❌ Mistake 3: Static VM State

**Wrong:**
```csharp
public static class ObjectMemory
{
    private static ObjectTable _table; // NO!
}
```

**Right:**
```csharp
public class SmalltalkVM
{
    public ObjectTable ObjectTable { get; } // Per-VM
}
```

### ❌ Mistake 4: Breaking Reachability

**Wrong:**
```csharp
// Creating object not reachable from SOA
var orphan = ObjectMemoryNew.CreateInstance(...);
// Not stored in SystemDictionary or any SOA-reachable structure
```

**Right:**
```csharp
var instance = ObjectMemoryNew.CreateInstance(...);
// Store in class pool, method dict, or other SOA-reachable structure
SystemDictionary.StoreGlobal("MyGlobal", instance);
```

### ❌ Mistake 5: Using ST* Structs After Migration

**Wrong:**
```csharp
// ST* structs are DELETED - do not use them!
STClass classStruct = classRef;
var superclassRef = classStruct.Superclass;
```

**Right:**
```csharp
// Use LayoutManagement pattern for field access
var classOop = classRef.AsObjectPointer();
var superclassIndex = LayoutManagement.Layout.Class_Superclass;
var superclassRef = ObjectMemory.GetInstanceVariable(classOop, superclassIndex);
```

### ❌ Mistake 6: Creating Symbols Before Symbol Class Exists

**Wrong:**
```csharp
// Phase 2 bootstrap - Symbol class doesn't exist yet!
public void InternAll()
{
    foreach (var symbolText in symbols)
    {
        var symbolRef = ObjectMemory.CreateSymbol(symbolText); // CRASH!
        _symbolMap[symbolText] = symbolRef;
    }
}
```

**Right:**
```csharp
// Phase 2: Use temporary C# storage
public void CreateTemporaryStorage()
{
    foreach (var symbolText in symbols)
    {
        var placeholderOop = AllocateTemporaryPlaceholder(symbolText);
        _tempSymbolMap[symbolText] = placeholderOop;
    }
}

// Phase 7: Migrate to real Symbol objects after Symbol class exists
public void MigrateToUSTable()
{
    var symbolClassRef = _loader.ResolveLocalId("class:Symbol");
    foreach (var kvp in _tempSymbolMap)
    {
        var symbolRef = CreateRealSymbol(kvp.Key, symbolClassRef);
        AddToUSTable(symbolRef);
    }
}
```

## Compiler

### Generating Cold Start JSON

The compiler reads Smalltalk-80 file-out format and generates JSON cold-start images:

```bash
# Generate JSON from Smalltalk source
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- Smalltalk-80.st

# Output
Compiling Smalltalk-80.st...
Generated Smalltalk-80.json
  Symbols: 3914
  Strings: 6532
  Classes: 474
  Methods: 4640
  Init expressions: 285
```

**Compiler output:**
- Input: `Smalltalk-80.st` (file-out format)
- Output: `Smalltalk-80.json` (11MB uncompressed, 1.2MB gzipped)
- Schema: Conforms to `docs/schemas/coldstart-v1.0.0.schema.json`

**Key files:**
- `Smalltalk80.Compiler/CodeGen/ColdStartJsonGenerator.cs` - Main JSON generator
- `Smalltalk80.Compiler/AST/FileInFormatAstBuilder.cs` - ANTLR4 AST builder
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Bytecode generation
- `Smalltalk80.Compiler/Program.cs` - CLI entry point

### ANTLR4 Generation

```powershell
# Generate parser (from scripts directory)
cd scripts
pwsh GenerateIfNewer.ps1

# Force regeneration
pwsh Invoke-Antlr4CSharp.ps1
```

**Requirements:**
- NO warnings during ANTLR4 generation (fix grammar if warnings appear)
- Grammar must support full Smalltalk-80 language
- Never sacrifice language completeness for convenience

### Validation

Use `Smalltalk-80 Source/Smalltalk-80.st` (thousands of lines) to validate:
- Grammar changes
- Parser correctness
- Syntax assumptions
- Compiler JSON generation

Note: WS lexer rule drops whitespace - never add whitespace productions in parser.

## Bootstrap Process

### JSON-Based Cold Start

The interpreter loads cold-start data from `Smalltalk-80.json` (or `coldstart.json`) following a deterministic 10-phase bootstrap process:

**Critical Constraint**: Symbol and String classes don't exist until Phase 3-5, so symbols must be stored in temporary C# storage first, then migrated to USTable after Symbol class is created.

**Bootstrap Algorithm (10 Phases):**

1. **Parse & Validate**: Load JSON, validate against schema (docs/schemas/coldstart-v1.0.0.schema.json)
2. **Create Temporary Storage**: Allocate placeholder OOPs for symbols/strings in C# Dictionary (Symbol class doesn't exist yet!)
3. **Allocate Objects**: Process install order phases sequentially
   - Bootstrap classes (Object, Behavior, Class, Metaclass, etc.)
   - Core classes (all remaining classes + metaclasses)
   - Pool dictionaries
   - Methods (grouped by class for locality)
4. **Create SystemDictionary**: Allocate Smalltalk global dictionary and populate with all classes, pools, system globals
5. **Migrate Symbols to USTable**: Now Symbol class exists - create real Symbol objects, migrate from temp storage to USTable using Smalltalk hash algorithm
6. **Apply Patches**: Resolve forward references (Association values, metaclass wiring)
7. **Execute Init Expressions**: Run global expressions (class initialize, comments, etc.)
8. **Create Special Objects Array**: Build 5-element SOA structure (nil, false, true, Smalltalk, ProcessorScheduler)
9. **Verify Reachability**: Ensure all objects reachable from SOA roots
10. **Finalize**: Initialize GC and complete bootstrap

### Install Order Phases

The JSON file specifies exact installation sequence:

```json
{
  "phases": [
    { "name": "Bootstrap Classes", "kind": "classes", "localIds": [...] },
    { "name": "Core Classes", "kind": "classes", "localIds": [...] },
    { "name": "Pool Dictionaries", "kind": "objects", "localIds": [...] },
    { "name": "Methods", "kind": "methods", "localIds": [...] },
    { "name": "Init Expressions", "kind": "initExpressions", "localIds": [...] }
  ]
}
```

### Critical Constraints

1. **LocalId Resolution**: Compiler emits stable localIds (e.g., `class:Object`), loader maps to runtime OOPs
2. **Symbol Pre-Interning**: All symbols interned before use (from JSON symbols array)
3. **Dictionary Sizing**: Dictionaries pre-sized correctly based on JSON metadata
4. **Deterministic OOPs**: Same objects get same OOPs on every run (from install order)
5. **Zero Garbage**: No temporary objects created during bootstrap

## Debugging and Diagnostics

### GC Logging

```bash
# Enable GC logging
dotnet run -- repl --gc-log

# Enable GC validation
dotnet run -- repl --validate-gc

# Enable cold start logging
dotnet run -- repl --cold-start-log

# All logging
dotnet run -- repl --gc-log --validate-gc --cold-start-log
```

### Class Hierarchy Display

```bash
# Show hierarchical class tree
dotnet run -- repl --list-cold-start-classes
```

Output shows inheritance with box-drawing characters (├─, └─).

### Common Issues

**If GC collects objects during cold start:**
1. Check which OOP was collected
2. Trace when that OOP was allocated
3. Verify it's reachable from SOA
4. Check if symbol was pre-interned

**If class hierarchy is incomplete:**
1. Verify all superclasses are in ColdStartClasses list
2. Check GetAllClassNames() iterates SystemDictionary correctly
3. Ensure class was registered in SystemDictionary

## Git Workflow

### Before Committing

**CRITICAL: Both compiler and interpreter MUST compile and run without errors before committing.**

1. **Build verification** (REQUIRED - must pass):
   ```bash
   # Build compiler
   cd Smalltalk80.Compiler
   dotnet build --no-restore
   # Must show: 0 Error(s)

   # Build interpreter
   cd ../Smalltalk80.Interpreter
   dotnet build --no-restore
   # Must show: 0 Error(s)
   ```

2. **Runtime verification** (IDEAL - should pass):
   ```bash
   # Test interpreter cold start with GC validation
   cd Smalltalk80.Interpreter
   dotnet run --no-build -- repl --gc-log --validate-gc
   # Ideally: Should complete without exceptions
   # Ideally: Should show "0 objects collected" on all GC runs
   ```

3. **Update documentation**: Update Interpreter-Architecture.md if architecture changed

4. **Clean build required**: Build must succeed with 0 errors and 0 warnings

**DO NOT commit if either project has compilation errors or warnings.**

### Commit Message Format

```
<type>: <short summary>

<detailed description of what changed and why>

- Specific change 1
- Specific change 2

<any breaking changes or important notes>
```

**Types**: feat, fix, refactor, docs, test, perf

## Key Resources

- **[Interpreter-Architecture.md](Interpreter-Architecture.md)** - Complete architectural documentation
- **Smalltalk-80 Blue Book** - Goldberg & Robson (1983) - The specification
- **Smalltalk-80.st** - Reference Smalltalk-80 source code

## Current Migration Status

### Completed Work (Previous Session)

✅ **JSON Schema & Compiler**:
- Created `docs/schemas/coldstart-v1.0.0.schema.json` with complete specification
- Implemented `ColdStartJsonGenerator.cs` to emit schema-compliant JSON
- Compiler successfully generates `Smalltalk-80.json` (11MB, compresses to 1.2MB)

✅ **Cold Start Bootstrap System**:
- Implemented 10-phase `JsonColdStartBootstrap.cs` with correct sequencing
- Fixed bootstrap chicken-and-egg: temporary C# storage → class creation → USTable migration
- Created `SymbolInterning.cs` with two-phase approach (CreateTemporaryStorage + MigrateToUSTable)
- Implemented `SystemDictionaryCreator.cs` for Smalltalk global dictionary population
- Fixed all JSON schema property mismatches (Format, Bytecodes, Literals, HomeClassLocalId, etc.)

✅ **Legacy Code Removal**:
- Deleted 8 legacy ST* struct-based cold start files (~50KB removed)
- Fixed `SpecialObjectsArray.cs` to use LayoutManagement pattern
- Converted `SObjectRefClassExtensions.cs` (6 ST* references → LayoutManagement)
- Added `ObjectMemory.SetClass()` method for metaclass wiring
- Updated `VirtualMachine.cs` to use `JsonColdStartBootstrap`

### Remaining Work (Current Session)

⏳ **Fix Compilation Errors** (268 total across 3 files):

1. **Interpreter.cs** (68 errors):
   - 36 `STMethodContext` references
   - 32 `STBlockContext` references
   - Pattern: Convert to `LayoutManagement.Layout.MethodContext_*` field access

2. **ControlPrimitives.cs** (54 errors):
   - Generated namespace references (old ST* structs)
   - Convert to LayoutManagement pattern

3. **BlueBookFactory.cs** (various errors):
   - ST* type references (Process, Semaphore, Dictionary, etc.)
   - Convert to LayoutManagement pattern

4. **SystemDictionary.cs** (references to deleted ColdStartBootstrap):
   - Remove old bootstrap references
   - Update to use JsonColdStartBootstrap

### Next Steps

1. **Systematic ST* Conversion**:
   ```csharp
   // Pattern to apply throughout codebase:

   // BEFORE (ST* struct):
   STMethodContext ctx = contextRef;
   var senderRef = ctx.Sender;

   // AFTER (LayoutManagement):
   var ctxOop = contextRef.AsObjectPointer();
   var senderIndex = LayoutManagement.Layout.MethodContext_Sender;
   var senderRef = ObjectMemory.GetInstanceVariable(ctxOop, senderIndex);
   ```

2. **Build Verification**:
   - Achieve 0 compilation errors
   - Verify 0 warnings
   - Test cold start bootstrap with GC validation

3. **Runtime Testing**:
   - Run: `dotnet run -- repl --gc-log --validate-gc`
   - Verify: "0 objects collected" on all GC runs
   - Test: Basic REPL functionality

4. **Documentation Update**:
   - Update Interpreter-Architecture.md with final migration details
   - Document LayoutManagement conversion patterns
   - Update file organization diagrams

## Summary Checklist

Before submitting any change, verify:

- [ ] Follows Blue Book specification
- [ ] Zero garbage during cold start
- [ ] All objects reachable from 5 SOA roots
- [ ] Per-VM architecture (no static VM state)
- [ ] JSON conforms to schema (docs/schemas/coldstart-v1.0.0.schema.json)
- [ ] Compiler builds and generates valid JSON
- [ ] Interpreter builds and loads JSON successfully
- [ ] Build succeeds (0 errors, 0 warnings)
- [ ] GC validation passes (0 objects collected)
- [ ] Documentation updated (if architecture changed)
- [ ] No modification to Smalltalk-80.st source

---

**Remember**: [Interpreter-Architecture.md](Interpreter-Architecture.md) is the canonical reference. When in doubt, check that document first.
