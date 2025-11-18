# Smalltalk-80 Interpreter Diagnostic Support Design

**Date**: 2025-11-18
**Status**: Draft - Proposal (v1.1)
**Author**: Claude (Anthropic)

---

## Executive Summary

This document proposes a comprehensive diagnostic subsystem for the Smalltalk-80 interpreter covering **both cold start and runtime phases**. The current ad-hoc diagnostic code is scattered throughout the codebase, making it difficult to maintain and extend. This design introduces a clean event-based listener model that decouples diagnostic concerns from core interpreter logic.

**Key Features:**
- Unified diagnostic hub for cold start and runtime
- Event-based listener model with filtering
- Zero-cost when disabled
- Extensible event types and output formats
- Support for ad-hoc messages alongside typed events

---

## 1. Current State Analysis

### 1.1 Existing Diagnostic Code

Current diagnostics are implemented as:
- Boolean flags in `RuntimeDebugOptions` and `ColdStartDebugOptions`
- Inline `Console.WriteLine` statements scattered throughout
- Hard-coded format strings with no structured output
- No filtering capability
- No way to capture output programmatically

### 1.2 Problems

1. **Parasitic coupling** - Diagnostic code interleaved with interpreter logic
2. **No composability** - Can't combine multiple diagnostic modes effectively
3. **No filtering** - All-or-nothing tracing creates overwhelming output
4. **No structured output** - Hard to parse for tooling
5. **Performance impact** - Always pay cost of flag checks even when disabled
6. **No extensibility** - Adding new diagnostics requires modifying core code

---

## 2. Proposed Architecture

### 2.1 Core Concepts

```
                    ┌─────────────────┐
                    │  DiagnosticHub  │ (Created first, shared by all)
                    └────────┬────────┘
                             │
              ┌──────────────┼──────────────┐
              │              │              │
              ▼              ▼              ▼
    ┌─────────────────┐  ┌──────────┐  ┌────────────┐
    │ ColdStartLoader │  │   VM     │  │ HeapManager │
    │   (Phases 1-6)  │  │ Runtime  │  │    (GC)     │
    └────────┬────────┘  └────┬─────┘  └─────┬──────┘
             │                │              │
             └────────────────┼──────────────┘
                              │ Events
                              ▼
                       ┌──────────────┐
                       │  Listeners   │
                       ├──────────────┤
                       │ ConsoleTrace │
                       │ JsonLogger   │
                       │ Profiler     │
                       │ ColdStart    │
                       └──────────────┘
```

**Key Principle:** The DiagnosticHub is created **before** any other component and passed to both cold start and runtime systems. This ensures diagnostics are available from the very first operation.

### 2.2 Event-Based Listener Model

**DiagnosticHub** - Central event dispatcher
- Maintains list of registered listeners
- Filters events before dispatch (performance optimization)
- Thread-safe event delivery

**IDiagnosticListener** - Interface for diagnostic consumers
- Receives events matching their registered interests
- Can filter, format, aggregate, or forward events

**DiagnosticEvent** - Structured event data
- Immutable records with all relevant context
- Typed hierarchy for different event categories

---

## 3. Feature List

### 3.1 Method Tracing

| Feature | Description | Priority |
|---------|-------------|----------|
| Method Entry | Log receiver, selector, arguments on method activation | High |
| Method Exit | Log return value when method completes normally | High |
| Non-local Return | Log block returns that exit enclosing method | High |
| Primitive Call | Log primitive number, arguments, success/failure | High |
| Message Not Understood | Log failed lookups with receiver class and selector | High |
| Super Send | Distinguish super sends from regular sends | Medium |
| Perform | Log dynamic message sends via perform: | Medium |

**Example Output:**
```
→ Array(5)>>at: 3
  receiver: #(1 2 3 4 5)
  args: [3]
← Array>>at: returned 3

→ SmallInteger(42)>>+ 8
  primitive: 1
← SmallInteger>>+ returned 50
```

### 3.2 Bytecode Tracing

| Feature | Description | Priority |
|---------|-------------|----------|
| Instruction Execution | Log each bytecode with symbolic name | High |
| Stack Changes | Show stack before/after each instruction | High |
| Jump Targets | Show actual target for conditional/unconditional jumps | Medium |
| Literal Access | Show literal value being pushed | Medium |
| Variable Access | Show variable name and value | Medium |
| Block Creation | Log BlockCopy with argument count and extent | Medium |

**Example Output:**
```
[ip=0] pushReceiver
  stack: [] → [Array(5)]
[ip=1] pushLiteral 2   ; index 0
  stack: [Array(5)] → [Array(5), 3]
[ip=3] send #at:
  stack: [Array(5), 3] → [3]
[ip=5] returnTop
  stack: [3] → []
```

### 3.3 Context Tracing

| Feature | Description | Priority |
|---------|-------------|----------|
| Context Activation | Log when MethodContext/BlockContext becomes active | Medium |
| Context Switch | Log sender chain changes | Medium |
| Stack Overflow | Warn when stack approaches limit | High |
| Temp Frame Access | Log reads/writes to temporary variables | Low |

### 3.4 Memory Diagnostics

| Feature | Description | Priority |
|---------|-------------|----------|
| Object Allocation | Log class, format, size of new objects | Medium |
| GC Cycle | Log mark/sweep statistics | Medium |
| Become Operation | Log identity swaps | Low |
| Large Object | Warn when allocating unusually large objects | Low |

### 3.5 Filtering Capabilities

| Filter Type | Description | Priority |
|-------------|-------------|----------|
| Class Pattern | Match class name with glob/regex | High |
| Method Pattern | Match selector with glob/regex | High |
| Primitive Range | Filter by primitive number range | Medium |
| Instruction Range | Filter by bytecode offset range | Medium |
| Call Depth | Limit tracing to N levels deep | Medium |
| Sample Rate | Trace every Nth occurrence | Low |

**Example Filter Specifications:**
```csharp
// Trace all Collection methods
listener.AddFilter(DiagnosticFilter.ClassPattern("*Collection*"));

// Trace specific method
listener.AddFilter(DiagnosticFilter.Method("Dictionary", "at:put:"));

// Trace primitives 60-69 (arithmetic)
listener.AddFilter(DiagnosticFilter.PrimitiveRange(60, 69));

// Only first 3 call levels
listener.AddFilter(DiagnosticFilter.MaxDepth(3));
```

### 3.6 Output Formats

| Format | Use Case | Priority |
|--------|----------|----------|
| Console (Indented) | Interactive debugging | High |
| JSON Lines | Tool integration, log aggregation | High |
| Compact | Minimal overhead tracing | Medium |
| Statistics | Aggregate counts, timing | Medium |

---

## 4. Interface Design

### 4.1 Core Interfaces

```csharp
/// <summary>
/// Central hub for diagnostic event dispatch.
/// </summary>
public interface IDiagnosticHub
{
    /// <summary>Registers a listener for specific event types.</summary>
    void Register(IDiagnosticListener listener, DiagnosticEventTypes events);

    /// <summary>Unregisters a listener.</summary>
    void Unregister(IDiagnosticListener listener);

    /// <summary>Checks if any listener wants this event type (fast path).</summary>
    bool IsEnabled(DiagnosticEventTypes eventType);

    /// <summary>Dispatches an event to all interested listeners.</summary>
    void Emit(DiagnosticEvent evt);
}

/// <summary>
/// Consumer of diagnostic events.
/// </summary>
public interface IDiagnosticListener
{
    /// <summary>Unique identifier for this listener.</summary>
    string Name { get; }

    /// <summary>Handles an incoming diagnostic event.</summary>
    void OnEvent(DiagnosticEvent evt);

    /// <summary>Optional filter to reduce events at source.</summary>
    IDiagnosticFilter? Filter { get; }
}

/// <summary>
/// Filters events before delivery to listener.
/// </summary>
public interface IDiagnosticFilter
{
    /// <summary>Returns true if this event should be delivered.</summary>
    bool ShouldDeliver(DiagnosticEvent evt);
}
```

### 4.2 Event Types Enumeration

```csharp
[Flags]
public enum DiagnosticEventTypes : ulong
{
    None = 0,

    // Method events (runtime)
    MethodEntry = 1 << 0,
    MethodExit = 1 << 1,
    PrimitiveCall = 1 << 2,
    PrimitiveFailure = 1 << 3,
    MessageNotUnderstood = 1 << 4,
    NonLocalReturn = 1 << 5,

    // Bytecode events (runtime)
    InstructionExecute = 1 << 10,
    StackPush = 1 << 11,
    StackPop = 1 << 12,
    Jump = 1 << 13,

    // Context events (runtime)
    ContextActivate = 1 << 20,
    ContextDeactivate = 1 << 21,
    BlockCreate = 1 << 22,

    // Memory events (both phases)
    ObjectAllocate = 1 << 30,
    GarbageCollect = 1 << 31,
    Become = 1 << 32,

    // Cold start phase events
    PhaseStart = 1 << 40,
    PhaseEnd = 1 << 41,
    SymbolInterned = 1 << 42,
    ClassCreated = 1 << 43,
    MethodCompiled = 1 << 44,
    PatchApplied = 1 << 45,
    GlobalInstalled = 1 << 46,
    AssociationCreated = 1 << 47,

    // Generic message event (both phases)
    Message = 1 << 50,

    // Compound groups - Runtime
    AllMethod = MethodEntry | MethodExit | PrimitiveCall | PrimitiveFailure | MessageNotUnderstood | NonLocalReturn,
    AllBytecode = InstructionExecute | StackPush | StackPop | Jump,
    AllContext = ContextActivate | ContextDeactivate | BlockCreate,
    AllMemory = ObjectAllocate | GarbageCollect | Become,
    AllRuntime = AllMethod | AllBytecode | AllContext | AllMemory,

    // Compound groups - Cold Start
    AllColdStartPhase = PhaseStart | PhaseEnd,
    AllColdStartDetail = SymbolInterned | ClassCreated | MethodCompiled | PatchApplied | GlobalInstalled | AssociationCreated,
    AllColdStart = AllColdStartPhase | AllColdStartDetail | Message,

    // Everything
    All = AllRuntime | AllColdStart | Message
}
```

### 4.3 Event Records

```csharp
/// <summary>Base class for all diagnostic events.</summary>
public abstract record DiagnosticEvent
{
    public required long Timestamp { get; init; }
    public required long InstructionCount { get; init; }
}

/// <summary>Method entry event.</summary>
public record MethodEntryEvent : DiagnosticEvent
{
    public required SmalltalkOOP ReceiverOOP { get; init; }
    public required string ReceiverClass { get; init; }
    public required string Selector { get; init; }
    public required SmalltalkOOP[] Arguments { get; init; }
    public required int CallDepth { get; init; }
    public required bool IsSuper { get; init; }
}

/// <summary>Method exit event.</summary>
public record MethodExitEvent : DiagnosticEvent
{
    public required string Selector { get; init; }
    public required SmalltalkOOP ReturnValue { get; init; }
    public required long ElapsedInstructions { get; init; }
}

/// <summary>Bytecode instruction execution event.</summary>
public record InstructionEvent : DiagnosticEvent
{
    public required int IP { get; init; }
    public required byte Opcode { get; init; }
    public required string Mnemonic { get; init; }
    public required string? Operand { get; init; }
    public required SmalltalkOOP[] StackBefore { get; init; }
    public required SmalltalkOOP[] StackAfter { get; init; }
}

/// <summary>Primitive call event.</summary>
public record PrimitiveEvent : DiagnosticEvent
{
    public required int PrimitiveIndex { get; init; }
    public required string PrimitiveName { get; init; }
    public required SmalltalkOOP ReceiverOOP { get; init; }
    public required SmalltalkOOP[] Arguments { get; init; }
    public required bool Success { get; init; }
    public required SmalltalkOOP? Result { get; init; }
    public required string? FailureReason { get; init; }
}

/// <summary>Object allocation event.</summary>
public record AllocationEvent : DiagnosticEvent
{
    public required SmalltalkOOP ObjectOOP { get; init; }
    public required string ClassName { get; init; }
    public required ObjectFormat Format { get; init; }
    public required int SizeInWords { get; init; }
}

/// <summary>GC cycle event.</summary>
public record GCEvent : DiagnosticEvent
{
    public required int ObjectsBefore { get; init; }
    public required int ObjectsAfter { get; init; }
    public required int ObjectsCollected { get; init; }
    public required double MarkTimeMs { get; init; }
    public required double SweepTimeMs { get; init; }
}

// ============================================================
// Cold Start Events
// ============================================================

/// <summary>Cold start phase beginning.</summary>
public record PhaseStartEvent : DiagnosticEvent
{
    public required int PhaseNumber { get; init; }
    public required string PhaseName { get; init; }
}

/// <summary>Cold start phase completion.</summary>
public record PhaseEndEvent : DiagnosticEvent
{
    public required int PhaseNumber { get; init; }
    public required string PhaseName { get; init; }
    public required TimeSpan Duration { get; init; }
    public required bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>Symbol interned during Phase 1.</summary>
public record SymbolInternedEvent : DiagnosticEvent
{
    public required int SymbolIndex { get; init; }
    public required string SymbolText { get; init; }
    public required SmalltalkOOP SymbolOOP { get; init; }
}

/// <summary>Class created during Phase 2.</summary>
public record ClassCreatedEvent : DiagnosticEvent
{
    public required string ClassName { get; init; }
    public required SmalltalkOOP ClassOOP { get; init; }
    public required SmalltalkOOP MetaclassOOP { get; init; }
    public required int InstanceVariableCount { get; init; }
}

/// <summary>Method compiled during Phase 4.</summary>
public record MethodCompiledEvent : DiagnosticEvent
{
    public required string ClassName { get; init; }
    public required string Selector { get; init; }
    public required SmalltalkOOP MethodOOP { get; init; }
    public required int BytecodeLength { get; init; }
    public required int LiteralCount { get; init; }
}

/// <summary>Patch applied during Phase 5.</summary>
public record PatchAppliedEvent : DiagnosticEvent
{
    public required string PatchType { get; init; }
    public required string TargetLocalId { get; init; }
    public string? FieldName { get; init; }
    public int? LiteralIndex { get; init; }
}

/// <summary>Global installed in SystemDictionary.</summary>
public record GlobalInstalledEvent : DiagnosticEvent
{
    public required string GlobalName { get; init; }
    public required SmalltalkOOP AssociationOOP { get; init; }
    public required SmalltalkOOP ValueOOP { get; init; }
}

/// <summary>Association created.</summary>
public record AssociationCreatedEvent : DiagnosticEvent
{
    public required string Key { get; init; }
    public required SmalltalkOOP AssociationOOP { get; init; }
}

// ============================================================
// Generic Message Event (for ad-hoc output)
// ============================================================

/// <summary>Message severity levels.</summary>
public enum MessageSeverity { Debug, Info, Warning, Error }

/// <summary>Generic diagnostic message for informational output.</summary>
public record DiagnosticMessageEvent : DiagnosticEvent
{
    public required MessageSeverity Severity { get; init; }
    public required string Category { get; init; }
    public required string Message { get; init; }
    public object? Data { get; init; }
}
```

**Usage Notes:**

- **Typed events** (ClassCreatedEvent, MethodCompiledEvent, etc.) are for structured operations that benefit from machine-readable data
- **DiagnosticMessageEvent** is for informational/progress output that doesn't fit specific typed events
- Both can be used together - listeners choose what to consume

---

## 5. Built-in Listeners

### 5.1 ConsoleTraceListener

Default listener that outputs formatted text to console.

**Features:**
- Indented output based on call depth
- Colorized output (optional)
- Configurable verbosity levels
- Timestamp prefix option

**Configuration:**
```csharp
var listener = new ConsoleTraceListener
{
    IndentSize = 2,
    ShowTimestamps = false,
    ShowInstructionCounts = true,
    MaxStackDisplay = 5,
    TruncateStrings = 50
};
```

### 5.2 JsonLogListener

Outputs JSON Lines format for tool integration.

**Features:**
- One JSON object per line
- Full event data serialization
- Optional pretty-print mode
- File or stream output

**Example Output:**
```json
{"type":"MethodEntry","ts":1234567,"ic":5000,"receiver":"Array","selector":"at:","args":[3],"depth":2}
{"type":"MethodExit","ts":1234568,"ic":5010,"selector":"at:","result":42,"elapsed":10}
```

### 5.3 ProfilerListener

Aggregates statistics for performance analysis.

**Features:**
- Method call counts and timing
- Hotspot identification
- Primitive success/failure rates
- Memory allocation patterns

**Report Output:**
```
=== Method Profile ===
Calls    Total(ms)  Avg(ms)  Method
12543    1234.5     0.098    Dictionary>>at:ifAbsent:
8721     567.8      0.065    Array>>do:
...

=== Primitive Profile ===
Prim  Calls    Success%  Name
60    50000    100.0%    SmallInteger add
70    12000    99.8%     Object basicNew
...
```

### 5.4 BreakpointListener

Supports interactive debugging.

**Features:**
- Break on method entry
- Break on primitive failure
- Break on specific instruction
- Conditional breakpoints

### 5.5 ColdStartProgressListener

Outputs cold start phase progress (reproduces current console output).

**Features:**
- Phase start/end markers
- Timing information
- Success/failure indicators
- Message output with severity

**Example Output:**
```
Phase 1: Preflight Validation and Symbol Interning... ✓ (102ms)
Phase 2: Class Infrastructure... ✓ (20ms)
Phase 3: Object Allocation and Initialization...
  Created Undeclared dictionary (OOP=13922, capacity=634, undeclared count=317)
  ✓ Pre-created 317 canonical Associations
✓ (20ms)
...
```

**Implementation:**
```csharp
public class ColdStartProgressListener : IDiagnosticListener
{
    public string Name => "ColdStartProgress";

    public void OnEvent(DiagnosticEvent evt)
    {
        switch (evt)
        {
            case PhaseStartEvent e:
                Console.Write($"{e.PhaseName}...");
                break;

            case PhaseEndEvent e:
                Console.WriteLine($" ✓ ({e.Duration.TotalMilliseconds:F0}ms)");
                break;

            case DiagnosticMessageEvent e:
                var prefix = e.Severity switch
                {
                    MessageSeverity.Warning => "  ⚠️  ",
                    MessageSeverity.Error => "  ❌  ",
                    _ => "  "
                };
                Console.WriteLine($"{prefix}{e.Message}");
                break;
        }
    }
}
```

### 5.6 ColdStartDetailListener

Outputs detailed cold start operations for debugging.

**Features:**
- Symbol interning details
- Class creation details
- Patch application details
- Global installation details

**Configuration:**
```csharp
var listener = new ColdStartDetailListener
{
    ShowSymbols = false,      // Too verbose usually
    ShowClasses = true,
    ShowMethods = false,      // Very verbose
    ShowPatches = true,
    ShowGlobals = true
};
```

---

## 6. Integration Points

### 6.1 VirtualMachine Integration

```csharp
public class VirtualMachine
{
    private readonly IDiagnosticHub _diagnostics;

    // Fast-path check before event creation
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EmitMethodEntry(...)
    {
        if (_diagnostics.IsEnabled(DiagnosticEventTypes.MethodEntry))
        {
            _diagnostics.Emit(new MethodEntryEvent { ... });
        }
    }
}
```

### 6.2 BytecodeInterpreter Integration

```csharp
public class BytecodeInterpreter
{
    private void ExecuteInstruction(...)
    {
        if (_diagnostics.IsEnabled(DiagnosticEventTypes.InstructionExecute))
        {
            var stackBefore = CaptureStack();
            // ... execute instruction ...
            var stackAfter = CaptureStack();
            _diagnostics.Emit(new InstructionEvent { ... });
        }
        else
        {
            // ... execute instruction (no capture overhead) ...
        }
    }
}
```

### 6.3 Cold Start Integration

The diagnostic hub must be created **before** cold start begins and passed through all phases.

**Program.cs Setup:**
```csharp
// 1. Create hub FIRST (before anything else)
var diagnosticHub = new DiagnosticHub();

// 2. Configure listeners before cold start
diagnosticHub.Register(
    new ColdStartProgressListener(),
    DiagnosticEventTypes.AllColdStartPhase | DiagnosticEventTypes.Message
);

// 3. Pass hub to orchestrator
var loader = new ColdStartOrchestrator(bundle, heapManager, coldStartFile, diagnosticHub);
loader.Run();

// 4. Same hub continues into runtime
var vm = new VirtualMachine(heapManager, layoutResolver, vmMetadata, diagnosticHub);
```

**Phase Integration Example (Phase 3):**
```csharp
public class Phase3_ObjectAllocation : IPhase
{
    private readonly IDiagnosticHub? _diagnostics;

    public void Run()
    {
        _diagnostics?.Emit(new PhaseStartEvent
        {
            PhaseNumber = 3,
            PhaseName = Name,
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // Create Undeclared dictionary
        var undeclaredOOP = CreateUndeclaredDictionary();

        _diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase3",
            Message = $"Created Undeclared dictionary (OOP={undeclaredOOP}, capacity={capacity})",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });

        // ... more operations ...

        _diagnostics?.Emit(new PhaseEndEvent
        {
            PhaseNumber = 3,
            PhaseName = Name,
            Duration = stopwatch.Elapsed,
            Success = true,
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }
}
```

**Typed Event Usage (optional detail tracing):**
```csharp
// In Phase 2 - emit class creation event
if (_diagnostics?.IsEnabled(DiagnosticEventTypes.ClassCreated) ?? false)
{
    _diagnostics.Emit(new ClassCreatedEvent
    {
        ClassName = className,
        ClassOOP = classOOP,
        MetaclassOOP = metaclassOOP,
        InstanceVariableCount = instVarCount,
        Timestamp = Stopwatch.GetTimestamp(),
        InstructionCount = 0
    });
}
```

### 6.4 Command Line Integration

```bash
# Enable method tracing for all Dictionary methods
dotnet run -- --trace-methods "Dictionary*"

# Enable bytecode tracing for specific method
dotnet run -- --trace-bytecode "Array>>at:"

# Output to JSON file
dotnet run -- --trace-output trace.jsonl --trace-format json

# Multiple filters
dotnet run -- --trace-methods "Collection*" --trace-primitives 60-69

# Cold start diagnostics
dotnet run -- --trace-coldstart           # Phase progress only
dotnet run -- --trace-coldstart-detail    # All cold start events
```

---

## 7. Performance Considerations

### 7.1 Zero-Cost When Disabled

- Use `IsEnabled()` fast-path check before event creation
- Inline the check for hot paths
- No allocations when disabled

### 7.2 Minimal Impact When Enabled

- Event objects are pooled/cached where possible
- Lazy string formatting (defer until needed)
- Async dispatch option for non-blocking tracing

### 7.3 Filtering at Source

- Apply filters before creating event objects
- Hierarchical filter evaluation (most selective first)
- Bloom filter for class/method name patterns

### 7.4 Benchmarks (Target)

| Scenario | Overhead |
|----------|----------|
| Diagnostics disabled | < 1% |
| Method tracing (no filter) | 10-20% |
| Method tracing (filtered) | 5-10% |
| Bytecode tracing | 50-100% |

---

## 8. Implementation Plan

### Phase 1: Core Infrastructure (3-4 days)

1. **Create diagnostic namespace and interfaces**
   - `Core/Diagnostics/IDiagnosticHub.cs`
   - `Core/Diagnostics/IDiagnosticListener.cs`
   - `Core/Diagnostics/DiagnosticEventTypes.cs`
   - `Core/Diagnostics/DiagnosticEvents.cs` (all event records)

2. **Implement DiagnosticHub**
   - Event dispatch with filtering
   - Listener registration
   - Fast-path `IsEnabled()` check
   - Thread-safe operations

3. **Define all event records**
   - Runtime events (MethodEntry, Instruction, etc.)
   - Cold start events (PhaseStart, ClassCreated, etc.)
   - DiagnosticMessageEvent for ad-hoc output

4. **Add hub to Program.cs**
   - Create hub before anything else
   - Pass to ColdStartOrchestrator
   - Pass to VirtualMachine

### Phase 2: Method Tracing (2-3 days)

1. **Add integration points to VirtualMachine**
   - Method entry/exit
   - Primitive call
   - Message not understood

2. **Implement ConsoleTraceListener**
   - Formatted output
   - Indentation
   - Value formatting

3. **Add command line options**
   - `--trace-methods`
   - `--trace-primitives`

### Phase 3: Bytecode Tracing (2-3 days)

1. **Add integration points to BytecodeInterpreter**
   - Instruction execution
   - Stack capture

2. **Implement instruction mnemonic mapping**
   - Use existing BytecodeDecoder
   - Format operands symbolically

3. **Add command line options**
   - `--trace-bytecode`
   - `--trace-stack`

### Phase 4: Filtering (2 days)

1. **Implement filter types**
   - Class pattern
   - Method pattern
   - Primitive range
   - Call depth

2. **Add filter command line syntax**
   - Pattern matching
   - Range specification

### Phase 5: Cold Start Integration (3-4 days)

1. **Add hub parameter to ColdStartOrchestrator**
   - Constructor accepts optional IDiagnosticHub
   - Passes hub to all phase classes

2. **Implement ColdStartProgressListener**
   - Phase start/end output
   - Message handling
   - Reproduces current console output

3. **Add events to all 6 phases**
   - PhaseStart/PhaseEnd events
   - DiagnosticMessageEvent for informational output
   - Typed events for detail tracing (optional)

4. **Add command line options**
   - `--trace-coldstart`
   - `--trace-coldstart-detail`

### Phase 6: Additional Listeners (2-3 days)

1. **JsonLogListener**
   - JSON Lines output
   - File output

2. **ProfilerListener**
   - Statistics aggregation
   - Report generation

3. **ColdStartDetailListener**
   - Detailed cold start events
   - Configurable verbosity

### Phase 7: Cleanup (1-2 days)

1. **Remove old diagnostic code**
   - Delete ad-hoc Console.WriteLine calls from all phases
   - Verify output matches expected format
   - Update documentation

2. **Final testing and documentation**
   - Test cold start output
   - Test runtime tracing
   - Update CLAUDE.md

**Total Estimate**: 15-21 days

---

## 9. Migration Strategy

### 9.1 Incremental Adoption

1. Add new diagnostic system alongside existing code
2. Migrate one diagnostic type at a time
3. Validate output matches expectations
4. Remove old code after verification

### 9.2 Compatibility

- Keep existing command line flags working (map to new system)
- Maintain same output format options
- Document migration for users

---

## 10. Future Extensions

### 10.1 Remote Debugging

- WebSocket server for remote listeners
- Real-time trace streaming
- Interactive debugging protocol

### 10.2 Time-Travel Debugging

- Record full execution trace
- Replay and inspect any point
- Requires significant storage

### 10.3 Visualization

- Call graph visualization
- Memory allocation timeline
- Flame graphs for profiling

### 10.4 IDE Integration

- VS Code extension
- Breakpoint management
- Variable inspection

---

## 11. Appendix: Console Output to Replace

Files containing Console.WriteLine calls to be replaced with diagnostic events:

| File | Lines | Output Type |
|------|-------|-------------|
| Program.cs | ~240 | Init expression progress, GC stats, startup info |
| ColdStartOrchestrator.cs | ~10 | Phase progress markers |
| Phase2_ClassInfrastructure.cs | ~20 | Class infrastructure setup |
| Phase3_ObjectAllocation.cs | ~30 | Object allocation progress, Undeclared dict |
| Phase4_MethodCompilation.cs | ~20 | Method compilation stats |
| Phase5_GlobalsAndPatches.cs | ~40 | Globals, patches, Display creation |
| Phase6_ValidationAndGC.cs | ~45 | GC verification stats |
| StackManager.cs | ~15 | Stack overflow warnings |

**Total**: ~420 lines of Console.WriteLine calls to migrate

**Note**: Legacy debug options (RuntimeDebugOptions, ColdStartDebugOptions) have been removed.
The new system will replace all remaining Console.WriteLine calls with diagnostic events.

---

## 12. Success Criteria

1. **Functionality**: All existing diagnostic capabilities preserved
   - Cold start phase progress output matches current format
   - Init expression execution output matches current format
   - GC statistics output matches current format

2. **Performance**: < 1% overhead when disabled
   - Fast-path `IsEnabled()` check before event creation
   - No allocations when diagnostics disabled

3. **Usability**: Clear, filterable output
   - Class/method pattern filtering
   - Call depth limiting
   - Output format options (console, JSON, etc.)

4. **Maintainability**: Single integration point per subsystem
   - One hub passed through all components
   - Consistent event emission pattern
   - Clear separation of concerns

5. **Extensibility**: Easy to add new event types and listeners
   - Add new DiagnosticEventTypes flags
   - Add new event record types
   - Implement new IDiagnosticListener

6. **Cold Start Support**: Complete coverage of all phases
   - Phase lifecycle events (PhaseStart, PhaseEnd)
   - DiagnosticMessageEvent for informational output
   - Optional typed events for detail tracing

---

**Document Version**: 1.1
**Status**: Draft - Cold Start Support Added
**Last Updated**: 2025-11-18
