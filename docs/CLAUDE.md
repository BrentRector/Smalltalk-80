# Claude Development Guidelines

**Last Updated**: 2025-10-04
**Status**: Current Standing Guidance
**Current Phase**: Graphics & UI Implementation Complete (80% primitive coverage)

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
- Generated layouts (ColdStartData.cs, Layouts.cs)
- Bytecode constants
- Blue Book specifications
- Primitive implementations

❌ **Never use static for VM state** - prevents running multiple VMs.

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
├── ColdStart/                # Bootstrap system
│   ├── ColdStartBootstrap.cs # 4-pass + 2-phase orchestrator
│   ├── SymbolTableBuilder.cs # USTable creation
│   └── ...
├── Core/
│   ├── Objects/             # Object implementations
│   ├── Values/              # SObjectRef, SmallInteger
│   └── BlueBook/            # Blue Book compliance layer
├── Memory/
│   ├── ObjectMemory.cs      # Table-based storage
│   ├── GarbageCollectorNew.cs # Mark-and-sweep GC
│   └── SpecialObjectsArray.cs # 5-field struct
├── Execution/               # Bytecode execution
└── Primitives/              # VM primitives
```

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

## Grammar and Parser

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

Note: WS lexer rule drops whitespace - never add whitespace productions in parser.

## Bootstrap Process

### 4-Pass Class Creation

1. **Pass 1**: Bootstrap classes (Object, UndefinedObject, Behavior, ClassDescription, Class, Metaclass)
2. **Interlude**: Create nil singleton, fix Object.superclass
3. **Pass 2**: Bootstrap metaclasses (6 metaclasses for bootstrap classes)
4. **Pass 3**: Wire Metaclass.class = Metaclass (circular reference)
5. **Pass 4**: Remaining classes + metaclasses (40 classes + 40 metaclasses)

### 2-Phase Infrastructure

1. **Phase 2.1**: Create SystemDictionary, register all classes
2. **Phase 2.2**: Create singletons (true, false, ProcessorScheduler)
3. **Phase 2.3**: Install all methods (1,299 compiled methods)
4. **Phase 2.4**: Create USTable, migrate bootstrap symbols

### Critical Constraints

1. **Metaclass Instance Variables**: All metaclasses have exactly 7 instance variables (Behavior:4 + ClassDescription:2 + Metaclass:1)
2. **Symbol Pre-Interning**: Pre-intern symbols like "SymbolTable" before USTable creation
3. **Dictionary Sizing**: Pre-size dictionaries correctly (no growth during bootstrap)
4. **Deterministic OOPs**: Same objects get same OOPs on every run

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

## Summary Checklist

Before submitting any change, verify:

- [ ] Follows Blue Book specification
- [ ] Zero garbage during cold start
- [ ] All objects reachable from 5 SOA roots
- [ ] Per-VM architecture (no static VM state)
- [ ] Build succeeds (0 errors, 0 warnings)
- [ ] GC validation passes (0 objects collected)
- [ ] Documentation updated (if architecture changed)
- [ ] No modification to Smalltalk-80.st source

---

**Remember**: [Interpreter-Architecture.md](Interpreter-Architecture.md) is the canonical reference. When in doubt, check that document first.
