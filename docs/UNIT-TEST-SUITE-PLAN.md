# Comprehensive Unit Test Suite Plan for Smalltalk-80 Interpreter

**Date**: 2025-11-20  
**Status**: Complete Analysis  
**Regression Test Baseline**: 120 BlockContext tests (expected: 1191 return value)

---

## Executive Summary

The Smalltalk-80 interpreter is organized around three major subsystems:

1. **Compiler Subsystem** (Smalltalk80.Compiler) - Converts Smalltalk source to bytecode
2. **Interpreter Subsystem** (Smalltalk80.Interpreter) - Executes bytecode on heap
3. **Cold Start System** (6 sequential phases) - Builds heap from JSON bundle

This plan provides:
- **140+ source files** organized into logical testable components
- **5 major refactoring initiatives** that created new testable units
- **Critical file requirements** from CLAUDE.md
- **Dependency mapping** between components
- **Test coverage priorities** based on operational risk

---

## 1. COMPILER SUBSYSTEM (Smalltalk80.Compiler)

### 1.1 Critical Components

#### BytecodeCompiler.cs (~3,300 lines)
**Purpose**: Main compiler that traverses AST and emits bytecode

**Key APIs**:
- `CompileMethod()` - Main entry point for method compilation
- `CompileExpressionFromTree()` - Compile file-scope expressions
- `CompileBlockExpression()` - Compile block literals
- `CompileStatement()` - Compile statement expressions
- `GetUndeclaredVariables()` - Retrieve tracking data

**Public State**:
- `GetUndeclaredVariables()` - Dictionary of variable name → methods referencing it
- `ClearUndeclaredVariables()` - Reset tracking for new compilation

**Internal Critical Methods**:
- `CompileExpression()` - Core expression compilation (100+ lines)
- `CompileAssignment()` - Assignment with undeclared tracking
- `CompileLiteral()` - Literal value compilation
- `CompileBlockLiteral()` - Block literal with closure support
- `CompileMessageSend()` - Message sending (dynamic dispatch)
- `CompileReturn()` - Return statement handling

**Dependencies**:
- `VariableResolver` - Variable resolution
- `TempFrameAllocator` - Temporary variable allocation
- `BytecodeEmitter` - Bytecode generation
- `CompilationEnvironment` - Compilation context

**Test Requirements** (MANDATORY per CLAUDE.md):
- Block argument compilation (unified temp frame)
- Nested block compilation
- Non-local return compilation
- Message send bytecode generation
- Literal resolution
- Undeclared variable tracking

---

#### VariableResolver.cs (~800 lines)
**Purpose**: Unified variable resolution - single source of truth for all variable types

**Key Data Types**:
```csharp
public enum VariableType
{
    BlockTemporary,      // Block temp (absolute index in unified frame)
    ParentVariable,      // Parent context var (absolute index)
    MethodArgument,      // Method arg (absolute index)
    MethodTemporary,     // Method temp (absolute index)
    InstanceVariable,    // Instance var (class layout index)
    ClassVariable,       // Class var (literal index)
    PoolVariable,        // Pool var (literal index + pool name)
    Global,              // Global (literal index)
    PseudoVariable       // self, true, false, nil, super, thisContext
}

public struct ResolutionResult
{
    public VariableType Type { get; set; }
    public int Index { get; set; }
    public bool IsAssignable { get; set; }
    public string Name { get; set; }
    public string? PoolDictionaryName { get; set; }
}
```

**Key APIs**:
- `ResolveVariable()` - Main entry point (9-step resolution priority)
  - Pseudo-variables (self, true, false, nil, super, thisContext)
  - Temp variables via allocator (method args/temps, block args/temps)
  - Instance variables
  - Class variables
  - Pool variables
  - Global variables
  - Undeclared
- `TryResolvePseudoVariable()` - Resolve special variables
- `GetPoolVariable()` - Lookup pool dictionary variable

**Dependencies**:
- `CompilationEnvironment` - Scope context
- `TempFrameAllocator` - Temp frame allocation

**Test Requirements** (MANDATORY per CLAUDE.md):
- Pseudo-variable resolution (9 types)
- Method argument resolution (unified frame)
- Method temporary resolution
- Block argument resolution (UNIFIED FRAME - critical)
- Block temporary resolution
- Instance variable resolution
- Class variable resolution
- Pool variable resolution
- Global variable resolution
- Undeclared variable handling
- Priority ordering (9 steps)
- Assignability rules

**Architecture Note**: This is the unified implementation - previously there were duplicated resolution paths. Must test that resolution is consistent across all contexts.

---

#### TempFrameAllocator.cs (~300 lines)
**Purpose**: Allocates temp slots in unified temp frame with intelligent slot reuse

**Key Concept - Unified Temp Frame**:
```
Method: example: arg1
  | temp1 |
  ^[:blockArg | blockArg + temp1] value: 5

Unified Temp Frame Allocation:
  Slot 0: arg1 (method argument)
  Slot 1: temp1 (method temporary)
  Slot 2: blockArg (block argument - NOW IN UNIFIED FRAME)

All stored in MethodContext indexed slots, not BlockContext
```

**Key APIs**:
- `EnterMethodScope()` - Enter method and allocate arg/temp slots
  - Parameters: `List<string> argNames, List<string> tempNames`
  - Creates root scope (non-reusable)
  
- `EnterBlockScope()` - Enter block and allocate arg/temp slots
  - Parameters: `List<string> argNames, List<string> tempNames, bool isNested`
  - **CRITICAL**: Block arguments now allocated in unified frame (major refactoring)
  - Sequential blocks can reuse slots; nested blocks cannot
  
- `ExitBlockScope()` - Exit block scope
  - Sequential blocks release slots for reuse
  - Nested blocks keep slots (parent might access them)
  
- `TryGetSlotIndex()` - Lookup slot for variable
  - Returns absolute index (0, 1, 2...)
  - Used by VariableResolver
  
- `GetMethodArgumentStartIndex()` - Start of method args in frame
- `GetBlockArgumentStartIndex()` - Start of block args (for Primitive 81)
- `GetMaxSimultaneousSlots()` - Highest slot ever needed
- `GetHighestSlotIndex()` - For frame size calculation

**Internal State Tracking**:
- `_nextSlot` - Next available slot index
- `_maxSimultaneous` - Peak simultaneous slot need (not sum!)
- `_allocatedSlots` - Name → absolute index mapping
- `_scopeStack` - Nested scope tracking

**Test Requirements** (MANDATORY per CLAUDE.md):
- Method scope allocation (sequential)
- Block scope allocation with nesting detection
- Slot reuse for sequential blocks
- No slot reuse for nested blocks
- Slot index lookup
- Boundary conditions (zero args, zero temps)
- MaxSimultaneousSlots calculation
- HighestSlotIndex tracking
- Escape analysis (detect if block escapes method)
- BlockArgumentStartIndex for Primitive 81

**Architecture Note - CRITICAL CHANGE**:
- **OLD**: Block arguments stored in BlockContext indexed slots
- **NEW**: Block arguments stored in MethodContext unified frame (same as method args/temps)
- This change affects bytecode generation, runtime variable access, and block primitives

---

### 1.2 Supporting Compiler Components

#### BytecodeEmitter.cs (~800 lines)
**Purpose**: Bytecode instruction generation

**Key APIs**:
- `Emit()` - Emit single bytecode instruction
- `EmitPush()` - Emit push operations
- `EmitStore()` - Emit store operations
- `EmitSend()` - Emit message send
- `EmitReturn()` - Emit return
- `EmitJump()` - Emit jump
- `GetBytecodes()` - Get final bytecode array
- `MarkLabel()`, `PatchJump()` - Control flow patching

**Responsibilities**:
- Bytecode stream management
- Offset tracking
- Jump/label resolution
- Literal array building

**Test Requirements**:
- Bytecode stream correctness
- Offset calculations
- Jump patching
- Literal resolution

---

#### CompilationEnvironment.cs (~200 lines)
**Purpose**: Compilation context (scope, variables, literals)

**Key State**:
- `Type` - Scope type (Method, Block)
- `LocalArgumentIndices` - Method argument name → index
- `LocalTemporaryIndices` - Method temporary name → index
- `InstanceVariableIndices` - Instance var name → class layout index
- `PoolVariables` - Pool dictionaries and their variables
- `ClassVariable` - Class variables
- `PoolVariables` - Pool dictionaries

**Responsibilities**:
- Track variable scopes
- Store class layout information
- Manage pool variable context

**Test Requirements**:
- Scope initialization
- Variable index tracking
- Pool variable management

---

### 1.3 Compiler Dependency Graph

```
BytecodeCompiler
├── VariableResolver (unified variable resolution)
│   └── CompilationEnvironment (scope tracking)
├── TempFrameAllocator (unified frame allocation)
│   └── (pure computation, no dependencies)
├── BytecodeEmitter (bytecode generation)
│   └── (no dependencies)
└── CompilationEnvironment (scope context)
    └── (no dependencies)
```

**Critical Invariant**: VariableResolver and TempFrameAllocator must be synchronized:
- TempFrameAllocator allocates slots
- VariableResolver returns indices from allocator
- If out of sync: wrong bytecode indices for temp access

---

## 2. INTERPRETER SUBSYSTEM (Smalltalk80.Interpreter)

### 2.1 Core VM Components

#### VirtualMachine.cs (~1,500 lines)
**Purpose**: Main execution orchestrator and component coordinator

**Public API**:
- `Execute()` - Main entry point: compile and execute expression
- `ExecuteExistingContext()` - Execute in pre-existing context
- `InterpreterLoop()` - Main trampoline loop (architecture: avoid deep stacks)
- `ExecuteBytecodeStep()` - Execute single bytecode instruction

**Component Injection**:
```csharp
public VirtualMachine(
    ISmalltalkHeapManager heapManager,
    IStackManager stackManager,
    IContextManager contextManager,
    IMessageDispatcher messageDispatcher,
    IBytecodeInterpreter bytecodeInterpreter,
    IVMDebugger debugger,
    IPrimitiveRegistry primitiveRegistry,
    // ... others
)
```

**Interfaces Implemented**:
1. `IPrimitiveContext` - Primitives access heap/stack
2. `IPrimitiveMessageSender` - Primitives can send messages

**Critical State** (Extracted in Recent Refactoring):
- `ExecutionState` - Trampoline state (initial context, return value, context switch flag)
- `NonLocalReturnState` - NLR tracking (pending flag, value, target context)
- `ExecutionTracer` - Debug/trace state (selectors, instruction counts, stack traces)
- `SelectorCache` - Cached arithmetic/special selector OOPs

**Test Requirements**:
- Basic method execution
- Context switching
- Return handling
- Non-local returns (^return in blocks)
- Exception handling (doesNotUnderstand:)
- Primitive invocation
- Block execution

**Refactoring Status** (Per VirtualMachine-Rewrite-Plan.md):
- ✅ Phase 1-4 COMPLETED: State extraction, ExecutionTracer, SelectorCache, NonLocalReturnHandler
- ⏭️ Phase 5 SKIPPED: InterpreterEngine extraction (high coupling risk)

---

#### BytecodeInterpreter.cs (~1,100 lines)
**Purpose**: Single-bytecode instruction execution

**Key Methods**:
- `ExecuteBytecode()` - Main dispatch function
- `DoBytecode_{operation}()` - Individual instruction handlers (40+ methods)
  - Push operations (receiver var, temp var, literal, special)
  - Store operations (receiver var, temp var)
  - Message sending
  - Jumps and returns
  - Block copy

**Bytecode Range Coverage** (Blue Book Chapter 30):
```
0-15:    Push receiver/instance variable
16-31:   Push temporary variable
32-63:   Push literal constant
64-95:   Push literal variable
96-103:  Store instance variable
104-111: Store temporary variable
112-119: Push special (receiver, true, false, nil, -1, 0, 1, 2)
120-125: Return operations
128-134: Extended operations
144-175: Jumps (unconditional, if true, if false)
176-191: Send arithmetic message
192-207: Send special message
208-255: Send literal selector
```

**Interfaces Implemented**:
- `IBytecodeInterpreter` - Component interface
- **PLUS** implicit interface with VirtualMachine (IBytecodeContext)

**Critical Methods**:
- `HandleMessageSend()` - Message dispatch
- `HandleReturn()` - Return/NLR processing
- `HandleNonLocalReturn()` - NLR unwinding

**Test Requirements** (HIGH PRIORITY):
- Each bytecode range (0-15, 16-31, etc.)
- Variable access bytecodes with unified frame indices
- Message sending (special, arithmetic, literal selector)
- Return operations
- Non-local return (^return in blocks)
- Block copy (bytecode 32x - creates BlockContext)
- Jump operations (forward, backward, conditional)
- Special object access (receiver, true, false, nil)

**Architecture Note**: Bytecode interpreter uses trampoline pattern - returns control to VirtualMachine.InterpreterLoop() rather than recursing.

---

#### ContextManager.cs (~500 lines)
**Purpose**: Context lifecycle (MethodContext and BlockContext creation/pooling)

**Key APIs**:
- `CreateMethodContext()` - Create new MethodContext
  ```
  1. Allocate context object
  2. Set sender to current context
  3. Set method, receiver
  4. Copy arguments to indexed slots [0..argCount-1]
  5. Initialize temps to nil [argCount..tempCount-1]
  6. Set stackp after temps
  ```
- `CreateBlockContext()` - Create new BlockContext
  ```
  1. Allocate context object
  2. Set sender to current context
  3. Set home (MethodContext)
  4. Set argumentCount, initialIP
  5. Set blockArgumentStartIndex (for arg access)
  ```
- `ActivateContext()` - Make context current
- `DeactivateContext()` - Return from context
- `ReturnFromContext()` - Return value and switch sender

**Context Pooling**:
- `ReclaimDeadContext()` - Mark context for reuse
- `GetPooledContext()` - Retrieve from pool

**Test Requirements**:
- MethodContext creation with arguments
- MethodContext creation with temporaries
- BlockContext creation
- Home pointer setup (for closures)
- Context pooling
- Argument/temp initialization
- Stack pointer setup
- Sender chain integrity

---

#### MessageDispatcher.cs (~700 lines)
**Purpose**: Method lookup and message sending

**Key APIs**:
- `SendMessage()` - Send message to receiver
  ```
  1. Look up method in class hierarchy
  2. Check for primitive (try execute, fail → bytecodes)
  3. Create new MethodContext
  4. Activate context
  5. Return ContextSwitch signal
  ```
- `LookupMethod()` - Find method in hierarchy
  ```
  1. Get receiver's class
  2. Search method dictionary for selector
  3. Walk superclass chain
  4. If not found: send doesNotUnderstand:
  ```
- `SendSuperMessage()` - Super send (start lookup at superclass)

**Cache Management**:
- `InvalidateMethodCache()` - Cache invalidation (Primitive 89)

**Test Requirements**:
- Method lookup in receiver's class
- Method lookup in superclass chain
- Primitive execution with fallback
- Super sends
- doesNotUnderstand: invocation
- Method cache invalidation

---

#### StackManager.cs (~200 lines)
**Purpose**: Simple stack (expression evaluation stack)

**Key APIs**:
- `Push()` - Push value
- `Pop()` - Pop value
- `Peek()` - Read top without removing
- `Clear()` - Reset stack
- `Dump()` - Debug output

**Test Requirements**:
- Basic push/pop/peek
- Stack underflow detection
- Boundary conditions

---

### 2.2 Memory Management Components

#### SmalltalkHeapManager.cs (~800 lines)
**Purpose**: Object allocation, identity, and lifecycle

**Key APIs**:
- **Object Allocation**:
  - `Allocate()` - Allocate object with class and format
  - `AllocateArray()` - Allocate indexed OOP array
  - `AllocateByteArray()` - Allocate indexed byte array
  - `AllocateString()` - Allocate string (byte-indexed)
  - `AllocateLargeInteger()` - Allocate large int
  - `AllocateFloat()` - Allocate float
  - `AllocateSymbol()` - Allocate symbol (interned)
  - `AllocateMethodObject()` - Allocate CompiledMethod

- **Field Access**:
  - `GetInstanceVariable()` - Get field value
  - `SetInstanceVariable()` - Set field value
  - `GetIndexedValue()` - Get indexed element
  - `SetIndexedValue()` - Set indexed element

- **Object Inspection**:
  - `GetObjectClass()` - Get object's class
  - `GetObjectFormat()` - Get storage format
  - `GetInstanceVariableCount()` - Field count
  - `GetIndexedDataSize()` - Indexed element count

- **Object Identity**:
  - `GetIdentityHash()` - Hash for identity-based collections
  - `Become()` - Swap two objects' identities

- **GC and Memory**:
  - `RequestGarbageCollection()` - Trigger GC
  - `GetStatistics()` - Heap statistics

**Object Formats** (Key Abstraction):
```csharp
public enum ObjectFormat
{
    NoIndexed = 0,      // No indexed slots (Point, Association)
    IndexableOOP = 1,   // Array of objects (Array, Dictionary)
    IndexableWord = 2,  // Array of words (Bitmap, SmallInteger array)
    IndexableByteArray = 3  // Byte array (String, ByteArray)
}
```

**Test Requirements** (HIGH PRIORITY):
- Allocation of each object format
- Field access for all formats
- Indexed access for all formats
- Object class reference
- SmallInteger encoding/decoding
- Symbol interning
- Large integer allocation
- Float allocation
- Identity hash
- Become operations

---

#### GarbageCollector.cs (~300 lines)
**Purpose**: Mark-sweep garbage collection

**Key APIs**:
- `Collect()` - Trigger GC
  ```
  1. Mark phase: start from roots, recursively mark reachable
  2. Sweep phase: free unmarked objects
  3. Clear marks for next GC
  ```

**GC Roots**:
- Active context chain (sender links)
- SpecialObjectsArray
- SystemDictionary
- Symbol table (USTable)

**Test Requirements**:
- Mark phase correctness
- Reachability from all roots
- Sweep phase object reclamation
- Mark clearing
- GC with active contexts

---

### 2.3 Component Dependency Graph - Interpreter

```
VirtualMachine (orchestrator)
├── StackManager (simple)
├── ContextManager
│   ├── SmalltalkHeapManager (allocation)
│   └── LayoutResolver (field indices)
├── MessageDispatcher
│   ├── SmalltalkHeapManager
│   ├── RuntimeHeapAccessor (safe access)
│   └── RuntimeClassCache
├── BytecodeInterpreter
│   ├── All of the above
│   └── PrimitiveRegistry (primitive dispatch)
└── Primitive System (15 primitive files)
```

---

### 2.4 Primitive System (15 files)

**Architecture**: All primitives implement `IPrimitive` interface

```csharp
public interface IPrimitive
{
    int Number { get; }
    string Name { get; }
    PrimitiveResult Execute(PrimitiveExecutionContext context);
}

public record PrimitiveResult(
    bool Success,
    SmalltalkOOP Value = default,
    string? ErrorMessage = null
)
{
    public static PrimitiveResult Succeed(SmalltalkOOP value) => new(true, value);
    public static PrimitiveResult Fail(string message) => new(false, message: message);
}
```

#### Primitive Categories

| Range | File | Examples | Test Priority |
|-------|------|----------|----------------|
| 1-20 | ArithmeticPrimitives.cs | `+`, `-`, `*`, `/`, `bitAnd:`, `bitOr:`, `mod:` | HIGH |
| 21-39 | LargeIntegerPrimitives.cs | `digitAt:`, `digitLength`, large integer ops | MEDIUM |
| 40-58 | FloatPrimitives.cs | Float arithmetic, transcendental functions | MEDIUM |
| 60-67 | ObjectPrimitives.cs | `at:`, `at:put:`, `size`, `instVarAt:`, `instVarAt:put:` | HIGH |
| 68-79 | ObjectPrimitives.cs (cont) | `==`, `class`, `instVarAt:`, identity operations | HIGH |
| 80-84 | ControlPrimitives.cs | `blockCopy:`, `value`, `value:`, `perform:` | **CRITICAL** |
| 85-89 | ControlPrimitives.cs (cont) | `flushCache`, signal, wait | MEDIUM |
| 90-99 | InputPrimitives.cs | Mouse, keyboard input | LOW (GUI) |
| 100-109 | StringPrimitives.cs | String operations, character replacement | MEDIUM |
| 110-127 | GraphicsPrimitives.cs | BitBlt, graphics operations | LOW (GUI) |
| 128+ | SystemPrimitives.cs | System, file operations | MEDIUM |

**Test Requirements by Primitive**:

1. **Arithmetic (1-20)** - HIGH PRIORITY:
   - SmallInteger arithmetic (+, -, *, /, //, mod:, etc.)
   - Bitwise operations
   - Overflow handling
   - Comparison (>, <, >=, <=)
   
2. **Object Access (60-67, 68-79)** - HIGH PRIORITY:
   - `at:` for arrays and strings
   - `at:put:` for arrays
   - `size` for collections
   - `instVarAt:` for instance variables
   - Identity comparison (==)
   - `class` method

3. **Control (80-84)** - **CRITICAL**:
   - `blockCopy:` - Create BlockContext from block literal
   - `value` - Execute block with no arguments
   - `value:` - Execute block with 1 argument
   - `value:value:` - Execute block with 2 arguments
   - Primitive 81 (GetBlockArgumentStartIndex) for block arg access
   
4. **Large Integers (21-39)** - MEDIUM:
   - `digitAt:` - Get digit
   - `digitLength` - Number of digits
   - Large integer arithmetic

5. **Floats (40-58)** - MEDIUM:
   - Float arithmetic
   - sin, cos, exp, log, sqrt

6. **Strings (100-109)** - MEDIUM:
   - `replaceFrom:to:with:startingAt:`
   - String comparisons

---

### 2.5 Recently Refactored Components (Per VirtualMachine-Rewrite-Plan.md)

#### ExecutionState.cs (~100 lines)
**Purpose**: Immutable record of execution state (trampoline control)

```csharp
public record CallOutcome(
    InterpreterStatus Status,
    SmalltalkOOP ReturnValue = default
);

public enum InterpreterStatus
{
    Continue,           // Continue to next bytecode
    Return,             // Method returned
    ContextSwitch       // New context activated
}

public record NonLocalReturnState(
    bool IsPending,
    SmalltalkOOP Value,
    SmalltalkOOP TargetContext
);
```

**Test Requirements**:
- State transitions
- Context switch signal propagation
- Return value capture

---

#### ExecutionTracer.cs (~200 lines)
**Purpose**: Encapsulated debug/trace functionality

**Key APIs**:
- `TraceSelectorEntry()` - Log method entry
- `TraceSelectorExit()` - Log method exit
- `DumpStack()` - Debug stack contents
- Trace enabling/disabling

**Test Requirements**:
- Trace output accuracy
- Selector entry/exit tracking

---

#### NonLocalReturnHandler.cs (~150 lines)
**Purpose**: NLR (non-local return) state machine

**Key Concept**:
```smalltalk
example
    | block |
    block := [^42].      "Returns from example, not block"
    block value.
    ^0                  "Never reached"
```

**Implementation**:
- When bytecode 124 (return stack top) executes in BlockContext
- Find home MethodContext
- Throw NonLocalReturnException with home's sender as target
- Unwind call stack to target context

**Key APIs**:
- `InitiateNonLocalReturn()` - Start NLR
- `IsNonLocalReturnPending()` - Check NLR state
- `CompleteNonLocalReturn()` - Finish unwinding

**Test Requirements** (CRITICAL):
- Non-local return from simple block
- Non-local return from nested block
- Non-local return skipping intermediate contexts
- Exception unwinding

---

### 2.6 Supporting Infrastructure

#### RuntimeHeapAccessor.cs (~200 lines)
**Purpose**: Safe runtime access to heap (read-only after cold start)

**Key APIs**:
- `InternSymbol()` - Intern string as symbol (runtime)
- `LookupGlobal()` - Look up global variable
- `LookupClass()` - Look up class by name
- `GetMethodDictionary()` - Get class method dictionary

**Invariant**: Uses only heap, no registries (which are frozen after cold start)

---

#### LayoutResolver.cs (~200 lines)
**Purpose**: Class layout caching for fast field index lookup

**Key APIs**:
- `GetInstanceVariableIndex()` - Get field index in class
- `GetInstanceVariableCount()` - Total fields
- `GetClassVariableIndex()` - Get class var index

**Invariant**: Populated in Phase 2, read-only from Phase 3+

---

#### SpecialObjectsArray.cs (~100 lines)
**Purpose**: Well-known object cache (nil, true, false, etc.)

**Key Objects**:
- Index 0: nil
- Index 1: false
- Index 2: true
- Index 3: (reserved)
- Index 4: ProcessorScheduler singleton
- Index 5: Semaphore class
- Index 6: Character class
- Index 7: SmallInteger class
- Index 8: String class
- Index 9: Array class
- Index 10: Float class
- Index 11: MethodContext class
- Index 12: BlockContext class
- Index 13: Point class
- Index 14: LargePositiveInteger class
- Index 15: Display instance
- Index 16: SystemDictionary instance

**Test Requirements**:
- All indices accessible
- Correct class references
- Immutability after setup

---

## 3. COLD START SUBSYSTEM (6 Phases)

**Purpose**: Build heap from JSON bundle in 6 sequential phases

**Key Invariant**: Phases must execute in order; each phase has well-defined preconditions/postconditions

### 3.1 Phase Organization

```
ColdStartOrchestrator (orchestrates 6 phases)
├── Phase 1: Preflight & Symbols
│   └── Validates bundle, creates all symbols
├── Phase 2: Class Infrastructure
│   └── Creates classes, metaclasses, wires hierarchy
├── Phase 3: Object Allocation
│   ├── DictionaryInstaller (hash-based dictionary ops)
│   ├── FieldValueResolver (resolve field values)
│   └── LiteralResolver (resolve literal references)
├── Phase 4: Method Compilation
│   ├── LiteralResolver (resolve method literals)
│   └── Bytecode generation for each method
├── Phase 5: Globals & Patches (9 helper files)
│   ├── RuntimeDictionaryService (hash operations) ← Shared with Phases 3, 4
│   ├── FieldValueResolver (value resolution)
│   ├── PatchApplier (apply forward reference patches)
│   ├── SystemDictionaryInstaller (install globals)
│   ├── USTableBuilder (build symbol table)
│   └── RuntimeObjectCreator (create Display, etc.)
└── Phase 6: Validation & GC
    └── Validate all objects, run GC verification
```

### 3.2 Phase Details

#### Phase 1: Preflight & Symbols (~350 lines)
**Purpose**: Validate bundle and create all symbols

**Responsibilities**:
1. Validate bundle version >= 1.0.6
2. Validate symbol indices (all unique)
3. Validate object references (all valid)
4. Validate precomputed layouts
5. Validate metadata (HomeClass, etc.)
6. Create all symbols as ByteIndexed objects
7. Populate and FREEZE SymbolRegistry

**Output State**:
- All symbols allocated and available
- SymbolRegistry frozen (no additions allowed)

**Test Requirements**:
- Bundle validation
- Symbol creation
- Symbol registry immutability
- Metadata validation

---

#### Phase 2: Class Infrastructure (~750 lines)
**Purpose**: Create class/metaclass hierarchy

**Responsibilities**:
1. Allocate SystemDictionary
2. Allocate class and metaclass shells
3. Wire metaclass.thisClass links
4. Wire metaclass superclass chain
5. Populate ClassDescription.instanceVariables
6. Load precomputed layouts into LayoutResolver

**Output State**:
- All class OOPs allocated
- Metaclass chain complete
- LayoutResolver populated
- Ready for object allocation

**Test Requirements**:
- Class shell allocation
- Metaclass wiring
- Superclass chain
- Layout loading
- SystemDictionary creation

---

#### Phase 3: Object Allocation (~1,050 lines)
**Purpose**: Allocate all objects from bundle

**Responsibilities**:
1. Allocate sentinel objects (nil, true, false)
2. Allocate ProcessorScheduler singleton
3. Allocate all bundle objects
4. Set class references
5. Populate Behavior.subclasses field
6. Initialize instance variables to nil
7. Pre-create canonical Associations

**Helper Classes**:
- `DictionaryInstaller` - Hash-based dictionary ops (hash, install, probe)
- `FieldValueResolver` - Resolve field values (nil, booleans, integers, strings, refs)
- `LiteralResolver` - Resolve literal references

**Output State**:
- All objects allocated
- AssociationRegistry pre-populated
- Ready for method compilation

**Test Requirements**:
- Object allocation for all formats
- Class reference setup
- Field initialization
- Subclass list building
- Sentinel objects

---

#### Phase 4: Method Compilation (~750 lines)
**Purpose**: Compile methods to bytecode

**Responsibilities**:
1. Allocate method dictionaries for all classes
2. Build blockOwnerMap for scoping
3. Two-pass method compilation:
   - Pass 1: Create shells with placeholder literals
   - Pass 2: Resolve and patch literals
4. Install methods in class dictionaries
5. Create CompiledMethod for init expressions

**Helper Classes**:
- `LiteralResolver` - Resolve method literal references

**Output State**:
- All methods compiled
- Method dictionaries populated
- Init expressions array ready

**Test Requirements**:
- Method dictionary creation
- Bytecode generation
- Literal resolution
- Two-pass consistency
- Method installation

---

#### Phase 5: Globals & Patches (~100 lines orchestrator + 10 helpers)
**Purpose**: Install globals and apply patches

**Responsibilities** (Orchestrated by Phase5Orchestrator):
1. Apply patches (SetInstanceVariable, InstallMethodDictionary, ReplaceLiteral)
2. Build and install USTable (Universal Symbol Table)
3. Install global variables in SystemDictionary
4. Create runtime objects (DefaultTextStyle, Display)
5. FREEZE AssociationRegistry, GlobalRegistry, SymbolRegistry

**Helper Classes**:
- `PatchApplier` - Apply forward reference patches
- `RuntimeDictionaryService` - Unified hash-based dict ops (shared with Phases 3, 4)
- `SystemDictionaryInstaller` - Install globals
- `USTableBuilder` - Build symbol table
- `RuntimeObjectCreator` - Create Display, etc.
- `FieldValueResolver` - Value resolution
- `AssociationManager` - Association tracking

**Deleted** (2025-12-27): ~~DictionaryOperations~~ - Replaced by RuntimeDictionaryService

**Output State**:
- All globals installed
- All registries FROZEN (read-only)
- Ready for validation

**Test Requirements** (CHALLENGE):
- Patch application correctness
- Global installation
- Registry freezing
- USTable integrity
- Runtime object creation

---

#### Phase 6: Validation & GC (~1,000 lines)
**Purpose**: Validate heap integrity and verify GC reachability

**Responsibilities**:
1. Validate all classes (format, fields, subclasses)
2. Validate all methods (structure, bytecodes, literals)
3. Validate special objects (nil, true, false, etc.)
4. Validate init expressions
5. Validate cross-references (all OOPs point to valid objects)
6. Validate SpecialObjectsArray
7. Validate method dictionaries
8. Validate Association canonicalization
9. Set up GC roots
10. Run GC verification (nothing should be collected)

**Validation Functions**:
- `ValidateClassObject()` - Class structure
- `ValidateMethodObject()` - Method structure
- `ValidateCrossReferences()` - All OOPs valid
- `ValidateGC()` - GC reachability

**Output State**:
- Heap validated
- GC ready
- System ready for execution

**Test Requirements** (COMPLEX):
- Class validation
- Method validation
- Cross-reference checking
- GC verification
- SpecialObjectsArray integrity

---

### 3.3 Cold Start Dependencies

```
Phase 1: Symbols
    ↓ (symbols needed)
Phase 2: Classes (needs symbols for class names)
    ↓ (classes needed)
Phase 3: Objects (needs classes + symbols + layouts)
    ├─ DictionaryInstaller (uses SymbolRegistry)
    ├─ FieldValueResolver (uses symbols)
    └─ LiteralResolver (uses symbols)
    ↓ (objects needed)
Phase 4: Methods (needs objects + symbols)
    ├─ LiteralResolver (uses symbols)
    └─ Method dictionaries allocated in Phase 3
    ↓ (methods compiled)
Phase 5: Globals (needs methods + objects)
    ├─ PatchApplier (patches methods/objects)
    ├─ RuntimeDictionaryService (uses symbols) ← Shared service
    ├─ SystemDictionaryInstaller (uses classes/objects)
    └─ USTableBuilder (uses symbols)
    ↓ (globals installed, registries frozen)
Phase 6: Validation (validates everything above)
    └─ GC verification
```

---

### 3.4 Code Duplication in Cold Start (Per Cold-Start-Refactoring.md)

**Major Duplications** (High-Priority Test Coverage):

1. **FieldValueResolver** (233 lines in Phase 3, 36 lines in Phase 5)
   - Both implement value resolution
   - Phase 3 is more complete
   - Should be unified into single service
   - **TEST IMPACT**: Must test both implementations match

2. **DictionaryOperations** ✅ RESOLVED (2025-12-27)
   - ~~Phase 3: DictionaryInstaller~~ → Uses RuntimeDictionaryService
   - ~~Phase 5: DictionaryOperations~~ → **Deleted** (uses RuntimeDictionaryService)
   - Now unified: `Core/VM/RuntimeDictionaryService.cs`
   - **TEST IMPACT**: Test RuntimeDictionaryService hash correctness

3. **Symbol Interning** (4 separate implementations)
   - Phase 1 creates new symbols
   - Phase 2, 3, 4 lookup in SymbolRegistry
   - **TEST IMPACT**: Must ensure consistent lookups

---

### 3.5 Recommended Service Extraction (Per Cold-Start-Refactoring.md)

**Hybrid Approach**: Keep phases, extract shared services

```csharp
// Shared services (RuntimeDictionaryService implemented 2025-12-27)
ISymbolService              // Intern/lookup symbols
RuntimeDictionaryService    // ✅ Hash-based dictionary ops (IMPLEMENTED)
IFieldValueService          // Resolve all value types
IObjectTrackingService      // SetAndTrackInstanceVariable
IValidationService          // Common validation patterns

// REFACTORED: Phases use services
Phase1_PreflightAndSymbols  (uses SymbolService)
Phase2_ClassInfrastructure  (uses SymbolService, ObjectTrackingService)
Phase3_ObjectAllocation     (uses all services)
Phase4_MethodCompilation    (uses SymbolService, FieldValueService)
Phase5Orchestrator          (uses all services)
Phase6_ValidationAndGC      (uses ValidationService)
```

**Test Benefits**:
- Test services independently
- Test phase integration separately
- Reduce duplication testing

---

## 4. COMPREHENSIVE TEST COVERAGE PRIORITIES

### 4.1 CRITICAL (Do First - Blocking Issues)

| Component | Rationale | Risk Level |
|-----------|-----------|-----------|
| **BytecodeCompiler** | Bytecode errors break execution | CRITICAL |
| **VariableResolver** | Wrong indices → wrong variable access | CRITICAL |
| **TempFrameAllocator** | Unified frame is new architecture | CRITICAL |
| **BytecodeInterpreter** | Core execution engine | CRITICAL |
| **Primitive 80-84 (Control)** | Block execution depends on these | CRITICAL |
| **ContextManager** | Context creation/lifecycle | CRITICAL |
| **SmalltalkHeapManager** | Memory allocation | CRITICAL |
| **MessageDispatcher** | Method lookup and sending | CRITICAL |
| **Phase 1-6** | Cold start must complete | CRITICAL |

**Estimated Tests**: 500+ test cases

**Key Invariants to Test**:
1. Bytecode indices match allocated slots
2. Unified temp frame has correct max size
3. Non-local returns unwind correctly
4. Block closures capture variables correctly
5. Cold start phases execute in order
6. No data corruption after GC

---

### 4.2 HIGH PRIORITY (Second - Core Functionality)

| Component | Test Cases |
|-----------|-----------|
| **Arithmetic Primitives (1-20)** | Addition, subtraction, multiplication, division, bitwise |
| **Object Primitives (60-79)** | at:, at:put:, size, instVarAt:, class, == |
| **String Primitives (100-109)** | replaceFrom:to:with:startingAt: |
| **Float Primitives (40-58)** | Arithmetic, transcendental |
| **Large Integer Primitives (21-39)** | digitAt:, digitLength, arithmetic |
| **NonLocalReturnHandler** | NLR state machine transitions |
| **ExecutionTracer** | Debug tracing (if enabled) |
| **SelectorCache** | Arithmetic/special selector caching |

**Estimated Tests**: 200+ test cases

---

### 4.3 MEDIUM PRIORITY (Third - Supporting Functionality)

| Component | Test Cases |
|-----------|-----------|
| **GarbageCollector** | Mark-sweep, reachability, cleanup |
| **RuntimeHeapAccessor** | Safe runtime access patterns |
| **LayoutResolver** | Field index caching |
| **SpecialObjectsArray** | Well-known object access |
| **Input Primitives (90-99)** | If not GUI-dependent |
| **System Primitives** | File operations, if applicable |
| **Cold Start Services** | If extracted (SymbolService, etc.) |

**Estimated Tests**: 150+ test cases

---

### 4.4 LOW PRIORITY (Optional - GUI/Platform)

| Component | Reason |
|-----------|--------|
| **Graphics Primitives (110-127)** | GUI-dependent, headless mode OK |
| **Input Primitives (90-99)** | GUI-dependent |
| **Font Primitives** | Platform-specific |
| **Platform Bridges** | Platform-specific |

---

## 5. CRITICAL TEST SCENARIOS

### 5.1 Block and Closure Tests

**Unified Temp Frame Scenarios**:
```smalltalk
1. Simple block: [5] value
2. Block with arguments: [:x | x + 1] value: 5
3. Block with method temp capture: 
   | temp | temp := 10. [:x | x + temp] value: 5
4. Nested blocks:
   | temp | [:a | [:b | a + b + temp] value: 3] value: 2
5. Non-local return in block: [^42] value
6. Escaping block: | block | block := [:x | x + 1]. block value: 5
7. Multiple blocks in sequence:
   [:x | x + 1] value: 5.
   [:y | y * 2] value: 3
8. Block with no args: [] value
```

**Index Verification**:
- Allocator must assign correct slots
- Bytecode must use those indices
- Runtime must access correct positions

---

### 5.2 Variable Resolution Tests

**Nine-Step Priority** (from VariableResolver):
```smalltalk
1. self, true, false, nil, super, thisContext
2. Method argument (highest priority temporary)
3. Method temporary
4. Block argument (nested blocks)
5. Block temporary
6. Instance variable
7. Class variable
8. Pool dictionary variable
9. Global variable (Undeclared if not found)
```

---

### 5.3 Non-Local Return Tests

```smalltalk
1. Simple NLR: [^42]
2. NLR skipping contexts: [[^42] value] value
3. NLR with active temporaries
4. NLR in exception handler
5. Nested NLR in multiple blocks
```

---

### 5.4 Cold Start Sequence Tests

```
Phase 1 Pass → Symbols interned
Phase 2 Pass → Classes created, layout loaded
Phase 3 Pass → Objects allocated, fields initialized
Phase 4 Pass → Methods compiled, installed
Phase 5 Pass → Globals installed, registries frozen
Phase 6 Pass → Heap validated, GC verified
```

---

### 5.5 Primitive Execution Tests

**Control Primitives (80-84)**:
```smalltalk
Primitive 80 (blockCopy:):
  - Create BlockContext from block literal
  - Set home to current MethodContext
  - Set initialIP to block start
  
Primitive 81 (value - no args):
  - Activate BlockContext
  - Jump to initialIP
  - Return to sender

Primitive 82 (value: - 1 arg):
  - Create BlockContext
  - Set argument in unified frame
  - Activate and execute

Primitive 83 (value:value: - 2 args):
  - Create BlockContext
  - Set 2 arguments
  - Activate and execute
```

**Test Variants**:
- Block with 0 args
- Block with 1 arg
- Block with 2 args
- Block with temporaries
- Escaping block (stored in variable)

---

### 5.6 Message Sending Tests

```smalltalk
1. Receiver message: 5 + 3
2. Receiver with arguments: 'hello' replaceFrom:1 to:5 with:'goodbye' startingAt:1
3. Message to self: self + 1
4. Super sends: super message
5. Messages on nil: nil + 1 (should send doesNotUnderstand:)
6. Method lookup in superclass
7. Method cache invalidation (Primitive 89)
8. Block as argument: [1, 2, 3] collect: [:x | x * 2]
```

---

## 6. TEST INFRASTRUCTURE REQUIREMENTS

### 6.1 Unit Test Framework

**Recommended**: xUnit (Microsoft.NET.Test.Sdk + xunit)

```csharp
public class BytecodeCompilerTests
{
    [Fact]
    public void CompileMethod_SimpleExpression_GeneratesBytecodes()
    {
        // Arrange
        var compiler = new BytecodeCompiler();
        var source = "example: x | temp | temp := 1. ^x + temp";
        var tree = ParseSmalltalk(source);

        // Act
        var result = compiler.CompileMethod(tree);

        // Assert
        Assert.NotNull(result.Bytecodes);
        Assert.True(result.Bytecodes.Length > 0);
    }
}
```

---

### 6.2 Helper Infrastructure

**1. Mock/Fake Implementations**:
- `MockHeapManager` - In-memory object allocation
- `MockContextManager` - Track context creation
- `MockMessageDispatcher` - Track method sends
- `FakeCompilationEnvironment` - Test variable resolution

**2. Test Data Builders**:
- `MethodBuilder` - Construct test methods
- `BlockBuilder` - Construct test blocks
- `CompilationEnvironmentBuilder` - Setup scopes

**3. Assertion Helpers**:
- `AssertBytecodeSequence()` - Verify bytecode pattern
- `AssertVariableResolution()` - Verify resolver output
- `AssertSlotAllocation()` - Verify temp frame layout
- `AssertHeapIntegrity()` - Post-execution validation

---

### 6.3 Test Organization

```
Tests/
├── Compiler/
│   ├── BytecodeCompilerTests.cs (100+ test methods)
│   ├── VariableResolverTests.cs (50+ test methods)
│   ├── TempFrameAllocatorTests.cs (40+ test methods)
│   ├── BytecodeEmitterTests.cs (30+ test methods)
│   └── CompilationEnvironmentTests.cs (20+ test methods)
├── Interpreter/
│   ├── VirtualMachineTests.cs (50+ test methods)
│   ├── BytecodeInterpreterTests.cs (80+ test methods)
│   ├── ContextManagerTests.cs (40+ test methods)
│   ├── MessageDispatcherTests.cs (50+ test methods)
│   ├── StackManagerTests.cs (10+ test methods)
│   └── Primitives/
│       ├── ArithmeticPrimitivesTests.cs (40+ test methods)
│       ├── ObjectPrimitivesTests.cs (40+ test methods)
│       ├── ControlPrimitivesTests.cs (50+ test methods)
│       ├── StringPrimitivesTests.cs (20+ test methods)
│       └── ...
├── Memory/
│   ├── SmalltalkHeapManagerTests.cs (60+ test methods)
│   ├── GarbageCollectorTests.cs (30+ test methods)
│   └── LayoutResolverTests.cs (20+ test methods)
├── ColdStart/
│   ├── Phase1Tests.cs (30+ test methods)
│   ├── Phase2Tests.cs (30+ test methods)
│   ├── Phase3Tests.cs (50+ test methods)
│   ├── Phase4Tests.cs (40+ test methods)
│   ├── Phase5Tests.cs (40+ test methods)
│   ├── Phase6Tests.cs (40+ test methods)
│   └── ColdStartIntegrationTests.cs (Integration: all phases)
└── Integration/
    ├── EndToEndTests.cs (Full cold start + execution)
    └── RegressionTests.cs (BlockContextTest verification)
```

---

## 7. REFACTORING-SPECIFIC TEST CONSIDERATIONS

### 7.1 Unified Temp Frame (TempFrameAllocator)

**What Changed**:
- Block arguments now allocated in MethodContext (not BlockContext)
- Compiler assigns unique absolute indices to all variables
- Bytecode uses absolute indices directly

**Tests Must Verify**:
- Slot allocation order (method args, method temps, block args, block temps)
- Sequential block slot reuse
- Nested block no reuse
- Absolute indices in bytecode
- Frame size calculation (max simultaneous, not sum)

**Risk**: If slot allocation is wrong, all variable access is wrong

---

### 7.2 NonLocalReturnHandler (New Component)

**What Changed**:
- NLR state extracted from VirtualMachine
- Dedicated state machine component

**Tests Must Verify**:
- NLR state transitions (not pending → pending → complete)
- Exception raising with correct target
- Context unwinding
- Integration with VirtualMachine

---

### 7.3 ExecutionTracer (Extracted Component)

**What Changed**:
- Debug/trace state moved from VirtualMachine
- Dedicated tracing component

**Tests Must Verify**:
- Trace output accuracy
- Trace stack management
- Enable/disable flags
- Performance impact (minimal)

---

### 7.4 SelectorCache (Extracted Component)

**What Changed**:
- Arithmetic and special selector caches extracted
- Dedicated caching component

**Tests Must Verify**:
- Cache correctness
- Cache invalidation (Primitive 89)
- Performance (OOP lookups eliminated)

---

### 7.5 Cold Start Service Extraction (Partially Complete)

**Status** (2025-12-27):
- ✅ RuntimeDictionaryService - **IMPLEMENTED** (replaces DictionaryService plan)
- SymbolService (when extracted)
- FieldValueService (when extracted)
- ObjectTrackingService (when extracted)
- ValidationService (when extracted)

**Test Pattern**:
```csharp
[Fact]
public void SymbolService_InternNewSymbol_CreatesObject()
{
    var service = new SymbolService(heapManager);
    var sym = service.InternNew("mySymbol");
    Assert.True(sym.IsSmallInteger == false);  // Symbol is object
}

[Fact]
public void SymbolService_LookupExistingSymbol_ReturnsSameOOP()
{
    var service = new SymbolService(heapManager);
    var sym1 = service.InternNew("mySymbol");
    var sym2 = service.LookupByName("mySymbol");
    Assert.Equal(sym1, sym2);
}
```

---

## 8. TEST EXECUTION STRATEGY

### 8.1 Phase 1: Compiler Tests (Weeks 1-2)

```
BytecodeCompiler (100 tests)
├── Method compilation variations
├── Expression compilation
├── Block literal compilation
├── Undeclared variable tracking
└── Edge cases (no args, no temps, etc.)

VariableResolver (50 tests)
├── Nine-step priority ordering
├── All variable types
├── Pseudo-variables
├── Pool variables
└── Undeclared handling

TempFrameAllocator (40 tests)
├── Method scope allocation
├── Block scope allocation
├── Slot reuse (sequential blocks)
├── No reuse (nested blocks)
├── Frame size calculation
└── Edge cases

Supporting Components (20 tests)
├── BytecodeEmitter
├── CompilationEnvironment
└── Utilities
```

**Validation**: Compile BlockContextTest, verify 120 tests still pass

---

### 8.2 Phase 2: Interpreter Core Tests (Weeks 3-4)

```
VirtualMachine (50 tests)
├── Basic execution
├── Method calls
├── Returns
├── Non-local returns
└── Exceptions

BytecodeInterpreter (80 tests)
├── All bytecode ranges
├── Push operations
├── Store operations
├── Message sends
├── Jumps
├── Returns
└── Block copy (bytecode 32x)

ContextManager (40 tests)
├── MethodContext creation
├── BlockContext creation
├── Context activation
├── Context pooling
└── Argument/temp initialization

MessageDispatcher (50 tests)
├── Method lookup
├── Superclass walking
├── Primitive execution
├── doesNotUnderstand:
└── Cache invalidation

Primitives (80 tests)
├── Arithmetic (40 tests)
├── Object access (30 tests)
├── Control (50 tests: critical!)
└── Others (20 tests)
```

**Validation**: Run with BlockContextTest, verify all pass

---

### 8.3 Phase 3: Memory & Cold Start Tests (Weeks 5-6)

```
SmalltalkHeapManager (60 tests)
├── All allocation types
├── Field access
├── Indexed access
├── SmallInteger encoding
├── Symbol interning
└── Become operations

GarbageCollector (30 tests)
├── Mark phase
├── Sweep phase
├── Reachability
└── Completeness

Cold Start Phases (250+ tests)
├── Phase 1: 30 tests
├── Phase 2: 30 tests
├── Phase 3: 50 tests
├── Phase 4: 40 tests
├── Phase 5: 40 tests
├── Phase 6: 40 tests
└── Integration: 20 tests
```

**Validation**: Full cold start, all phases succeed, GC passes

---

### 8.4 Phase 4: Integration & Refactoring Tests (Weeks 7-8)

```
End-to-End Tests (50 tests)
├── Full cold start + execution
├── Init expressions
├── Block execution
└── Complex scenarios

Refactored Components (40 tests)
├── ExecutionState
├── NonLocalReturnHandler
├── ExecutionTracer
├── SelectorCache
└── Integration with VM

Regression Tests (Automated)
└── BlockContextTest (120 tests, expect 1191)
```

---

### 8.5 Phase 5: Service Extraction Tests (Weeks 9-10, As Refactoring Proceeds)

```csharp
[When SymbolService Extracted]
public class SymbolServiceTests
{
    [Fact]
    public void InternNew_CreatesSymbol() { }
    
    [Fact]
    public void LookupByName_ReturnsSameOOP() { }
    
    [Fact]
    public void InternMultipleSymbols_EachUnique() { }
}

// RuntimeDictionaryService IMPLEMENTED (2025-12-27)
public class RuntimeDictionaryServiceTests
{
    [Fact]
    public void InstallAssociation_CorrectHashProbing() { }

    [Fact]
    public void RemoveAssociation_FixesCollisions() { }

    [Fact]
    public void GetSymbolHash_ConsistentAcrossPhases() { }

    [Fact]
    public void LookupGlobalAssociation_SystemDictionaryThenUndeclared() { }
}
```

---

## 9. SUCCESS METRICS

### 9.1 Coverage Goals

| Component | Current | Target | Rationale |
|-----------|---------|--------|-----------|
| BytecodeCompiler | 0% | 85%+ | Core functionality |
| VariableResolver | 0% | 90%+ | Critical for correctness |
| TempFrameAllocator | 0% | 90%+ | New architecture |
| BytecodeInterpreter | 0% | 85%+ | Core execution |
| Primitives 1-84 | 0% | 80%+ | Must work |
| Cold Start Phases | 0% | 75%+ | Must complete |

### 9.2 Regression Test

```bash
cd Smalltalk80.Interpreter
dotnet run -- --coldstart ../bin/Debug/net9.0/coldstart_with_runner.json
Expected: Returns 1191 (120 BlockContext tests pass)
```

This test must pass before and after each test suite addition.

### 9.3 Build Quality

```bash
dotnet build
Expected: 0 errors, 0 warnings
```

### 9.4 Test Execution Time

- Individual unit tests: < 100ms
- Test suite total: < 5 minutes
- Regression test: < 30 seconds

---

## 10. DEPENDENCIES AND PREREQUISITES

### 10.1 External Test Dependencies

```xml
<ItemGroup>
    <PackageReference Include="xunit" Version="2.4.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.x" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.4.5" />
    <PackageReference Include="Moq" Version="4.x" /> <!-- For mocking -->
</ItemGroup>
```

### 10.2 Internal Test Dependencies

- Access to BytecodeCompiler assembly
- Access to Interpreter assembly
- Ability to instantiate heap manager
- Ability to load cold start bundle

### 10.3 Known Challenges

1. **Cold Start Complexity**: Full cold start takes ~2 seconds
   - Solution: Unit test phases separately, integration test full flow

2. **Circular Dependencies**: Tests need VM but VM needs heap/primitives
   - Solution: Create test doubles for non-critical components

3. **State Mutation**: Cold start modifies registries
   - Solution: Each test resets state or uses isolated instances

4. **Platform Dependencies**: Graphics, input, files
   - Solution: Mock platform bridge, use headless mode

---

## 11. QUICK START CHECKLIST

### Immediate Actions

```
[ ] 1. Read this document completely
[ ] 2. Review CLAUDE.md project status
[ ] 3. Read Architecture documentation (3 files)
[ ] 4. Read Refactoring documentation (Cold-Start-Refactoring.md)
[ ] 5. Examine critical source files:
      - Smalltalk80.Compiler/BytecodeCompiler.cs
      - Smalltalk80.Compiler/VariableResolver.cs
      - Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs
      - Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs
      - Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs

[ ] 6. Create test project structure
[ ] 7. Implement test infrastructure (mocks, builders)
[ ] 8. Begin with Compiler tests (highest ROI)
[ ] 9. Validate with BlockContextTest regression
[ ] 10. Proceed through phases as per schedule
```

### File Locations (All Absolute Paths)

```
Compiler:
  /home/user/Smalltalk-80/Smalltalk80.Compiler/BytecodeCompiler.cs
  /home/user/Smalltalk-80/Smalltalk80.Compiler/VariableResolver.cs
  /home/user/Smalltalk-80/Smalltalk80.Compiler/Analysis/TempFrameAllocator.cs
  
Interpreter (VM):
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/VirtualMachine.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/BytecodeInterpreter.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/ContextManager.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/MessageDispatcher.cs
  
Interpreter (Memory):
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/HeapManager/SmalltalkHeapManager.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/HeapManager/GarbageCollector.cs
  
Primitives:
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ArithmeticPrimitives.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ObjectPrimitives.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/StringPrimitives.cs
  
Cold Start:
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase1_PreflightAndSymbols.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase2_ClassInfrastructure.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_ObjectAllocation.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase4_MethodCompilation.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/Phase5Orchestrator.cs
  /home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase6_ValidationAndGC.cs

Documentation:
  /home/user/Smalltalk-80/docs/Architecture/PHYSICAL-ARCHITECTURE.md
  /home/user/Smalltalk-80/docs/Architecture/IMPLEMENTATION-ARCHITECTURE.md
  /home/user/Smalltalk-80/docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md
  /home/user/Smalltalk-80/docs/Refactoring/Cold-Start-Refactoring.md
```

---

## 12. APPENDIX: Component Summary Table

| Component | Files | Lines | Test Priority | Status |
|-----------|-------|-------|----------------|--------|
| **BytecodeCompiler** | 1 | 3,300 | CRITICAL | Active |
| **VariableResolver** | 1 | 800 | CRITICAL | Active |
| **TempFrameAllocator** | 1 | 300 | CRITICAL | Active |
| **BytecodeEmitter** | 1 | 800 | HIGH | Active |
| **VirtualMachine** | 3 | 1,500+ | CRITICAL | Refactored (Phases 1-4) |
| **BytecodeInterpreter** | 1 | 1,100 | CRITICAL | Active |
| **ContextManager** | 1 | 500 | CRITICAL | Active |
| **MessageDispatcher** | 1 | 700 | CRITICAL | Active |
| **StackManager** | 1 | 200 | HIGH | Stable |
| **Primitives (15 files)** | 15 | 2,000+ | CRITICAL | Active |
| **SmalltalkHeapManager** | 1 | 800 | CRITICAL | Active |
| **GarbageCollector** | 1 | 300 | HIGH | Stable |
| **Cold Start (6 phases)** | 6+10 | 4,500+ | CRITICAL | Active (5 needs service extraction) |
| **Refactored Components** | 4 | 700 | HIGH | Complete (ExecutionState, Tracer, etc.) |

**Total**: 140+ files, ~28,000 lines of code

---

**Document Status**: ✅ Complete  
**Last Updated**: 2025-11-20  
**Next Action**: Begin Phase 1 (Compiler Tests)
