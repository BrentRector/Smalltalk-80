# Smalltalk-80 Interpreter Architecture Diagrams

**Last Updated**: 2025-11-18

## Overview

This document provides visual representations of the refactored Smalltalk-80 interpreter architecture.

---

## 1. High-Level System Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Application Layer                              │
│  Program.cs (Entry Point)                                            │
│  • Loads ColdStartBundle                                             │
│  • Runs ColdStartOrchestrator                                        │
│  • Initializes VirtualMachine                                        │
│  • Executes init expressions                                         │
└────────────────────────────┬────────────────────────────────────────┘
                             │
           ┌─────────────────┼─────────────────┐
           ▼                 ▼                 ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│   Cold Start    │ │    Runtime      │ │    Compiler     │
│     System      │ │     Engine      │ │                 │
│                 │ │                 │ │                 │
│ IColdStartLoader│ │ VirtualMachine  │ │ IBytecodeCompiler│
│ IPhase (x6)     │ │ IPrimitiveRegistry│ │                 │
└────────┬────────┘ └────────┬────────┘ └────────┬────────┘
         │                   │                   │
         └───────────────────┼───────────────────┘
                             │
                    ┌────────▼────────┐
                    │  Heap Manager   │
                    │                 │
                    │ ISmalltalkHeapManager│
                    │ ObjectTable     │
                    │ GC              │
                    └─────────────────┘
```

---

## 2. Cold Start System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              ColdStartOrchestrator : IColdStartLoader        │
│                                                              │
│  Run() → Executes phases 1-6 in sequence                    │
│  Phases property → List of IPhase                           │
│  Report property → Execution metrics                        │
└────────────────────────────┬─────────────────────────────────┘
                             │
    ┌────────────┬───────────┼───────────┬────────────┬────────────┐
    ▼            ▼           ▼           ▼            ▼            ▼
┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐ ┌────────┐
│Phase 1 │ │Phase 2 │ │Phase 3 │ │Phase 4 │ │Phase 5 │ │Phase 6 │
│Preflight│ │Classes │ │Objects │ │Methods │ │Globals │ │Validate│
│Symbols │ │Infrast │ │Alloc   │ │Compile │ │Patches │ │GC      │
│        │ │        │ │        │ │        │ │        │ │        │
│ :IPhase│ │ :IPhase│ │ :IPhase│ │ :IPhase│ │ :IPhase│ │ :IPhase│
└────────┘ └────────┘ └────────┘ └────────┘ └────────┘ └────────┘

Each IPhase has:
• Name: string
• PhaseNumber: int
• Run(): void
• ValidatePreconditions(): IReadOnlyList<string>
• ValidatePostconditions(): IReadOnlyList<string>
```

---

## 3. Primitive System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    VirtualMachine                             │
│                                                              │
│  Implements:                                                 │
│  • ICacheManager                                             │
│  • IPrimitiveMessageSender                                   │
│                                                              │
│  Creates PrimitiveExecutionContext for each primitive call   │
└────────────────────────────┬─────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│              PrimitiveRegistry : IPrimitiveRegistry          │
│                                                              │
│  Register(int, IPrimitive)                                   │
│  Execute(int, PrimitiveExecutionContext) → PrimitiveResult  │
│  IsRegistered(int) → bool                                   │
└────────────────────────────┬─────────────────────────────────┘
                             │
         ┌───────────────────┼───────────────────┐
         ▼                   ▼                   ▼
┌─────────────────┐ ┌─────────────────┐ ┌─────────────────┐
│ SmallInteger    │ │ Object          │ │ Control         │
│ Primitives      │ │ Primitives      │ │ Primitives      │
│ (1-17)          │ │ (60-79)         │ │ (80-89)         │
│                 │ │                 │ │                 │
│ : IPrimitive    │ │ : IPrimitive    │ │ : IPrimitive    │
└─────────────────┘ └─────────────────┘ └─────────────────┘
        ...                ...                ...

Total: 110 primitives implementing IPrimitive
```

---

## 4. Runtime Services Architecture

```
┌─────────────────────────────────────────────────────────────┐
│          RuntimeHeapAccessor : IRuntimeHeapAccessor          │
│                                                              │
│  GetClass(string) → SmalltalkOOP                            │
│  InternSymbol(string) → SmalltalkOOP                        │
│  LookupGlobalByName(dictOOP, name) → SmalltalkOOP           │
│  BuildFieldMap() → Dictionary<string, List<string>>         │
│  InvalidateAllCaches() → void                               │
└────────────────────────────┬─────────────────────────────────┘
                             │
              ┌──────────────┴──────────────┐
              ▼                             ▼
┌─────────────────────────┐   ┌─────────────────────────┐
│    RuntimeClassCache    │   │  RuntimeSpecialObjects  │
│                         │   │      (Singleton)        │
│  GetClass(name)         │   │                         │
│  InvalidateCache()      │   │  CompiledMethodClass    │
│                         │   │  ArrayClass             │
│  Uses hash-based        │   │  StringClass            │
│  lookup with caching    │   │  ...                    │
└─────────────────────────┘   │                         │
                              │  InvalidateCache()      │
                              └─────────────────────────┘
```

---

## 5. Cold Start vs Runtime Lifecycle

```
┌─────────────────────────────────────────────────────────────┐
│                    COLD START PHASE                          │
│                                                              │
│  Registries (mutable):                                       │
│  • GlobalRegistry                                            │
│  • SymbolRegistry                                            │
│  • AssociationRegistry                                       │
│                                                              │
│  Used by: Phases 1-6                                         │
└────────────────────────────┬─────────────────────────────────┘
                             │
                    Freeze() │ ← Registries become read-only
                             │
                             ▼
┌─────────────────────────────────────────────────────────────┐
│                    RUNTIME PHASE                             │
│                                                              │
│  Services (read-only heap access):                           │
│  • RuntimeHeapAccessor                                       │
│  • RuntimeClassCache (with invalidation)                     │
│  • RuntimeSpecialObjects (with invalidation)                 │
│                                                              │
│  Used by: VirtualMachine, Primitives                         │
│                                                              │
│  Cache Invalidation: Primitive 89 (flushCache)               │
└─────────────────────────────────────────────────────────────┘
```

---

## 6. Compiler Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              BytecodeCompiler : IBytecodeCompiler             │
│                                                              │
│  CompileMethodFromTree(methodDef, ...) → CompiledMethodData │
│  CompileExpressionFromTree(expr) → CompiledMethodData       │
└────────────────────────────┬─────────────────────────────────┘
                             │
    ┌────────────────────────┼────────────────────────┐
    ▼                        ▼                        ▼
┌────────────────┐ ┌─────────────────┐ ┌──────────────────┐
│ Compilation    │ │ BytecodeEmitter │ │ VariableResolver │
│ Environment    │ │                 │ │                  │
│                │ │ Emit()          │ │ Resolve variable │
│ Scope tracking │ │ AddLiteral()    │ │ to slot index    │
│ Variable maps  │ │ GetBytecodes()  │ │                  │
└────────────────┘ └─────────────────┘ └──────────────────┘
                             │
              ┌──────────────┼──────────────┐
              ▼              ▼              ▼
┌──────────────────┐ ┌────────────┐ ┌─────────────────┐
│TempFrameAllocator│ │TailRecursion│ │CollectingError │
│                  │ │  Analyzer   │ │   Listener     │
│ Allocate slots   │ │             │ │                │
│ with reuse       │ │ Optimization│ │ ANTLR errors   │
└──────────────────┘ └─────────────┘ └─────────────────┘
```

---

## 7. Interface Dependency Graph

```
                    ISmalltalkHeapManager
                           ▲
                           │
            ┌──────────────┼──────────────┐
            │              │              │
     IColdStartLoader  IRuntimeHeapAccessor  IPrimitiveRegistry
            │              │              │
            ▼              ▼              ▼
    ColdStartOrchestrator  RuntimeHeapAccessor  PrimitiveRegistry
            │                                       │
            ▼                                       ▼
         IPhase                                IPrimitive
            │                                       │
            ▼                                       ▼
    Phase1...Phase6                        110 Primitives
```

---

## 8. Data Flow: Init Expression Execution

```
1. Load Bundle     2. Cold Start      3. Runtime Init
   ─────────────      ───────────      ─────────────

┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│ coldstart.  │    │ Orchestrator│    │   Virtual   │
│   json      │───▶│   Run()     │───▶│   Machine   │
└─────────────┘    └──────┬──────┘    └──────┬──────┘
                          │                   │
                   Phases 1-6           Execute Init
                          │              Expressions
                          ▼                   │
                   ┌─────────────┐            │
                   │   Heap      │◀───────────┘
                   │  (populated)│
                   └─────────────┘
```

---

## Summary

The refactored architecture provides:

1. **Clear Boundaries**: Each subsystem has a well-defined interface
2. **Testability**: Interfaces enable unit testing and mocking
3. **Lifecycle Separation**: Cold start and runtime are clearly separated
4. **Cache Management**: Primitive 89 invalidates all caches correctly
5. **Extensibility**: New primitives implement IPrimitive pattern
