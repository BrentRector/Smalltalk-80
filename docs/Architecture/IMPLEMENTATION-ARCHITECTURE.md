# Implementation Architecture

**Last Updated**: 2025-11-18

This document describes how the Smalltalk-80 interpreter executes bytecodes, manages contexts, dispatches messages, and handles primitives.

---

## Overview

The interpreter implements the Blue Book (Smalltalk-80: The Language and Its Implementation) specification. Key concepts:

- **Objects**: Everything is an object with an OOP (Object Pointer)
- **Messages**: All computation is message sending
- **Contexts**: Execution state (MethodContext or BlockContext)
- **Bytecodes**: Compiled method instructions
- **Primitives**: Built-in operations in C#

---

## Execution Model

### Interpreter Loop

The VM uses a **trampoline** pattern for the main execution loop:

```
InterpreterLoop()
    │
    ├─► ExecuteBytecodeStep()
    │       │
    │       ├─ Fetch bytecode from method
    │       ├─ Dispatch to handler
    │       └─ Return status (Continue, Return, ContextSwitch)
    │
    ├─ If ContextSwitch → continue loop
    ├─ If Return → process return value
    └─ If Continue → fetch next bytecode
```

This avoids deep call stacks when executing Smalltalk method calls.

### Bytecode Dispatch

Bytecodes are dispatched using a switch statement in `BytecodeInterpreter.ExecuteBytecode()`:

| Range | Operation |
|-------|-----------|
| 0-15 | Push receiver variable |
| 16-31 | Push temporary variable |
| 32-63 | Push literal constant |
| 64-95 | Push literal variable |
| 96-103 | Store receiver variable |
| 104-111 | Store temporary variable |
| 112-119 | Push special (receiver, true, false, nil, -1..2) |
| 120-125 | Return (receiver, true, false, nil, stack top, block) |
| 128-134 | Extended operations |
| 144-175 | Jumps |
| 176-191 | Send arithmetic message |
| 192-207 | Send special message |
| 208-255 | Send literal selector |

---

## Context Management

### Context Types

**MethodContext** - Execution state for a method:
```
Fixed slots [0-4]:
  [0] sender      - Calling context
  [1] pc          - Program counter
  [2] stackp      - Stack pointer
  [3] method      - CompiledMethod
  [4] receiver    - Message receiver

Indexed slots [0..n]:
  [0..argCount-1]       - Arguments
  [argCount..tempCount] - Temporaries
  [tempCount+1..]       - Expression stack
```

**BlockContext** - Execution state for a block:
```
Fixed slots [0-5]:
  [0] sender          - Calling context
  [1] pc              - Program counter
  [2] stackp          - Stack pointer
  [3] argumentCount   - Number of block arguments
  [4] initialIP       - Starting instruction pointer
  [5] home            - Home MethodContext

Extension slot [6]:
  argStartIndex       - VM extension for Primitive 81

Indexed slots:
  Expression stack only (NO variable storage)
```

### Unified Temp Frame Architecture

**Key Principle**: All method and block variables are stored in the **MethodContext's unified temp frame**.

```
Method: example: arg1
    | temp1 |
    ^[:blockArg | blockArg + temp1] value: 5

MethodContext indexed slots:
  [0] arg1      - Method argument
  [1] temp1     - Method temporary
  [2] blockArg  - Block argument (stored in MethodContext!)
  [3+] stack    - Expression stack
```

**Variable Access**:
1. Compiler assigns absolute index to each variable
2. Get/SetTemporaryVariable follows `home` chain to MethodContext
3. Uses absolute index directly (no relative offsets)

See `docs/Block-Context-And-Variable-Access-COMPREHENSIVE.md` for complete details.

### Context Creation

```csharp
// ContextManager.CreateMethodContext()
1. Allocate MethodContext object
2. Set sender to current context
3. Set method, receiver
4. Copy arguments to indexed slots
5. Initialize temporaries to nil
6. Set pc to method start
7. Set stackp after temps
```

### Context Switching

When a message is sent:
1. Create new MethodContext
2. Set new context as active
3. Return ContextSwitch status
4. Interpreter loop continues in new context

When returning:
1. Get sender from current context
2. Push return value on sender's stack
3. Set sender as active context
4. Pool dead context for reuse

---

## Message Sending

### Method Lookup

```csharp
// MessageDispatcher.LookupMethod()
1. Get receiver's class
2. Search method dictionary for selector
3. If not found, walk superclass chain
4. If still not found, send doesNotUnderstand:
```

### Send Process

```csharp
// MessageDispatcher.SendMessage()
1. Look up method in class hierarchy
2. Check for primitive
   - If primitive succeeds → return result
   - If primitive fails → execute bytecodes
3. Create new MethodContext
4. Activate context (make it current)
5. Return ContextSwitch to interpreter loop
```

### Super Sends

Super sends start lookup at superclass of method's defining class (not receiver's class):

```csharp
// Get methodClass from literal frame
var methodClass = GetMethodLiteral(method, literalCount - 1);
var superclass = GetSuperclass(methodClass);
LookupMethodStartingAt(superclass, selector);
```

---

## Primitive System

### IPrimitive Pattern

All 110 primitives implement a common interface:

```csharp
public interface IPrimitive
{
    int Number { get; }
    string Name { get; }
    PrimitiveResult Execute(PrimitiveExecutionContext context);
}
```

### Primitive Execution Context

Primitives receive dependencies via injection:

```csharp
public class PrimitiveExecutionContext
{
    public SmalltalkOOP Receiver { get; }
    public SmalltalkOOP[] Arguments { get; }
    public ISmalltalkHeapManager HeapManager { get; }
    public IStackManager StackManager { get; }
    public IContextManager ContextManager { get; }
    public IRuntimeHeapAccessor RuntimeAccess { get; }
    public IPlatformBridge? Platform { get; }
}
```

### Primitive Categories

| Range | Category | Examples |
|-------|----------|----------|
| 1-20 | Integer arithmetic | +, -, *, /, bitAnd: |
| 21-39 | Large integer | digitAt:, digitLength |
| 40-58 | Float arithmetic | +, -, *, /, sin, cos |
| 60-67 | Object access | at:, at:put:, size |
| 68-79 | Object identity | ==, class, instVarAt: |
| 80-84 | Control | blockCopy:, value:, perform: |
| 85-89 | System | signal, wait, flushCache |
| 90-99 | Input | mousePoint, cursorLink: |
| 100-109 | String | replaceFrom:to:with:startingAt: |
| 110-127 | Graphics | bitBlt, copyBits |

### Primitive Failure

If a primitive fails, execution falls through to bytecodes:

```csharp
var result = primitive.Execute(context);
if (result.Success)
    return result.Value;
else
    // Execute method bytecodes as fallback
```

---

## Non-Local Returns

### Block Return Semantics

A return statement in a block (`^value`) returns from the **home method**, not just the block:

```smalltalk
example
    | block |
    block := [^42].  "Returns from example, not just block"
    block value.
    ^0  "Never reached"
```

### Implementation

```csharp
// In BytecodeInterpreter, bytecode 124 (return stack top)
if (IsBlockContext(activeContext))
{
    // Find home MethodContext
    var home = GetHomeContext(activeContext);

    // Unwind to home's sender
    throw new NonLocalReturnException(returnValue, home.Sender);
}
```

### NLR State

The VM tracks non-local return state:
- `_nonLocalReturnPending` - NLR in progress
- `_nlrValue` - Value being returned
- `_nlrTargetContext` - Target context to unwind to

---

## Garbage Collection

### Mark-Sweep Algorithm

The GC uses mark-sweep with root tracing:

```csharp
// GarbageCollector.Collect()
1. Mark phase:
   - Start from roots (active context, special objects)
   - Recursively mark all reachable objects

2. Sweep phase:
   - Walk object table
   - Free unmarked objects
   - Clear marks for next GC
```

### GC Roots

- Active context chain (sender links)
- Special objects array
- Smalltalk global dictionary
- Symbol table (USTable)

### When GC Runs

- After cold start (Phase 6)
- When object table is exhausted
- Explicitly via Primitive 130

---

## Memory Layout

### Object Format

Objects have different storage formats:

| Format | Storage | Examples |
|--------|---------|----------|
| 0 | No indexed | Point, Association |
| 1 | Indexable OOP | Array, OrderedCollection |
| 2 | Indexable word | Bitmap |
| 3 | Indexable byte | String, ByteArray |

### Object Structure

```
ObjectData:
  ClassRef         - Class OOP
  Format           - Storage format
  InstanceVariables[] - Named instance variables
  IndexedData      - Format-specific indexed data
  Marked           - GC mark bit
```

### SmallInteger Encoding

SmallIntegers are encoded in the OOP itself (no heap allocation):

```csharp
// Bit 0 = 1 indicates SmallInteger
public bool IsSmallInteger => (RawValue & 1) == 1;
public int AsSmallInteger() => RawValue >> 1;
public static SmalltalkOOP FromSmallInteger(int value)
    => new((value << 1) | 1);
```

---

## Cold Start Process

### Six Phases

| Phase | Purpose | Key Actions |
|-------|---------|-------------|
| 1 | Preflight & Symbols | Validate bundle, create symbols |
| 2 | Class Infrastructure | Create class hierarchy |
| 3 | Object Allocation | Allocate all objects |
| 4 | Method Compilation | Compile methods to bytecode |
| 5 | Globals & Patches | Install globals, apply patches |
| 6 | Validation & GC | Validate heap, run GC |

### Registry Freeze

After Phase 5, registries are frozen:

```csharp
// Prevent runtime modification of cold-start data
GlobalRegistry.Freeze();
SymbolRegistry.Freeze();
AssociationRegistry.Freeze();
```

Runtime code uses only the Smalltalk heap via `IRuntimeHeapAccessor`.

---

## Diagnostics System

### Event-Based Architecture

The diagnostic system uses an event hub with multiple listeners:

```csharp
public interface IDiagnosticHub
{
    void Emit<T>(T @event) where T : DiagnosticEvent;
    void RegisterListener(IDiagnosticListener listener);
}
```

### Event Categories

| Category | Events | Use Case |
|----------|--------|----------|
| detail | SymbolInterned, ClassCreated, MethodCompiled | Debug cold start |
| method | MethodEntry, MethodExit, PrimitiveCall | Runtime tracing |
| gc | GCEvent, AllocationEvent | Memory debugging |

### Command-Line Control

```bash
# Enable verbose cold start
dotnet run -- --diagnostics detail

# Enable method tracing
dotnet run -- --diagnostics method

# Multiple categories
dotnet run -- --diagnostics detail,method,gc
```

---

## Performance Characteristics

### Hot Paths

These are optimized for performance:

1. **Bytecode fetch/decode** - Tight loop, no allocations
2. **Stack operations** - Direct indexed slot access
3. **SmallInteger arithmetic** - No heap allocation
4. **Method cache** - O(1) lookup after first call

### Caching

- **RuntimeClassCache** - Caches class name → OOP lookups
- **RuntimeSpecialObjects** - Caches well-known objects
- **Method cache** - Invalidated by Primitive 89

### Avoiding Overhead

- Interface calls only at component boundaries
- No virtual dispatch in bytecode loop
- Context pooling reduces GC pressure
- SmallInteger encoding avoids allocation

---

## Blue Book Alignment

### Chapter References

- **Chapter 26**: CompiledMethod format
- **Chapter 27**: Context structure (MethodContext, BlockContext)
- **Chapter 28**: Primitive methods
- **Chapter 30**: Bytecode instruction set

### View Types

Type-safe views align with Blue Book concepts:

```csharp
// MethodContextView properties match Chapter 27
public SmalltalkOOP Sender => ...
public int PC => ...
public int StackPointer => ...
public SmalltalkOOP Method => ...
public SmalltalkOOP Receiver => ...
```

---

## Related Documents

- [PHYSICAL-ARCHITECTURE.md](PHYSICAL-ARCHITECTURE.md) - File system and subsystem organization
- [../Block-Context-And-Variable-Access-COMPREHENSIVE.md](../Block-Context-And-Variable-Access-COMPREHENSIVE.md) - Detailed BlockContext design
