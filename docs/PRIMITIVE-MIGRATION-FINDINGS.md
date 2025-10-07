# Primitive Migration Findings from Git History

## Summary
Found working implementations of control flow primitives in older commits. The implementations use different architectures that need to be adapted to our current VirtualMachine-based design.

## Git History Analysis

### Key Commits with Implementations

1. **Commit cd05556** - "Phase 3 is working, or very close to it"
   - Location: `Smalltalk80/Primitives/Control/ControlPrimitives.cs`
   - Has working implementations of:
     - BlockCopy (Primitive 80)
     - Value (Primitive 81)
     - Perform (Primitive 83)
     - Signal (Primitive 85)
     - Wait (Primitive 86)
     - Resume (Primitive 87)
     - Suspend (Primitive 88)
   - Architecture: Uses `Context`, `BlockContext`, `Process`, `Semaphore`, `ProcessorScheduler`

2. **Commit 26e5f0f** - "feat: complete graphics and UI implementation"
   - Location: `Smalltalk80.Interpreter/Primitives/ControlPrimitives.cs`
   - Has partial implementations using `InterpreterPrimitiveContext`
   - Uses: `primitiveContext.Interpreter.CreateBlockContext()` and `ActivateBlockContext()`

## Architecture Comparison

### Old Architecture (cd05556)
```csharp
public static bool BlockCopy(Context ctx)
{
    // Pops argCount and source context from stack
    var home = source is BlockContext blk ? blk.HomeContext : source;
    var block = BlockContext.Create(home, startPc, argCount, args, ctx);
    ctx.Stack.Push(block);
    return true;
}
```

**Key Components:**
- `Context` class: Sender, Method, PC, Stack, Receiver, Arguments, Temporaries
- `BlockContext`: HomeContext, StartPC, ArgumentCount
- `Interpreter` class with `Evaluate(context)` method
- `ProcessorScheduler` singleton for process management
- `Semaphore` with ExcessSignals and Waiting queue

### Current Architecture
```csharp
public class VirtualMachine : IPrimitiveContext
{
    private int _activeContextId;  // Current MethodContext OOP

    private int CreateMethodContext(int methodId, int receiverId, int[] arguments)
    {
        // Allocates MethodContext object in heap
        // Fields: sender, instructionPointer, stackPointer, method, receiver
        // Indexed: args, temps, stack
    }

    private int ExecuteBytecodeLoop()
    {
        // Fetches and executes bytecodes from current context
    }
}
```

**Key Differences:**
- Contexts are heap objects (OOPs), not C# objects
- Stack is in MethodContext's indexed fields, not C# Stack<>
- No separate Interpreter class - VM executes directly
- No BlockContext/Process/Semaphore classes yet

## Required Migrations

### Immediate (Can implement with current architecture)

#### Primitive 83-84: Perform/Perform:with:
**Requirements:**
- Need `IPrimitiveContext.SendMessage(receiver, selector, args)` method
- VirtualMachine already has `SendMessage(receiverId, selectorId, args)` at line 452
- Just need to expose through interface

**Implementation:**
```csharp
// Add to IPrimitiveContext
SObjectRef SendMessage(SObjectRef receiver, int selectorId, SObjectRef[] args);

// In VirtualMachine
SObjectRef IPrimitiveContext.SendMessage(SObjectRef receiver, int selectorId, SObjectRef[] args)
{
    int receiverId = receiver.IsSmallInteger ? (int)receiver.AsSmallInteger() : (int)receiver.AsObjectPointer();
    int[] argIds = args.Select(a => a.IsSmallInteger ? (int)a.AsSmallInteger() : (int)a.AsObjectPointer()).ToArray();
    int resultId = SendMessage(receiverId, selectorId, argIds);
    return SObjectRef.FromObjectPointer(resultId);
}
```

### Medium (Requires BlockContext infrastructure)

#### Primitive 80: BlockCopy
**Requirements:**
- BlockContext class in heap (with layout definition)
- Fields: homeContext, initialIP, argumentCount
- Capture current PC from active MethodContext
- Store reference to home context

**Implementation approach:**
```csharp
public static SObjectRef BlockCopy(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
{
    int argumentCount = args[0].AsSmallInteger();

    // Get active MethodContext
    int homeContextId = ctx.ActiveContext; // Need to add to IPrimitiveContext

    // Get current PC from homeContext
    int pc = ctx.GetField(homeContextId, "instructionPointer");

    // Allocate BlockContext
    int blockContextClass = ctx.Heap.GetClassByName("BlockContext");
    int blockId = ctx.Heap.AllocateRawObject(blockContextClass, ...);

    // Set fields
    ctx.Heap.SetField(blockId, "homeContext", homeContextId);
    ctx.Heap.SetField(blockId, "initialIP", pc);
    ctx.Heap.SetField(blockId, "argumentCount", argumentCount);

    return SObjectRef.FromObjectPointer(blockId);
}
```

#### Primitive 81-82: Value/Value:
**Requirements:**
- Extract homeContext, initialIP from BlockContext
- Create new BlockContext with arguments
- Switch execution to block's bytecodes
- Return result when block returns

**Challenge:** Current VM executes one method at a time. Need to support:
- Context switching
- Block execution within primitive
- Proper return handling

**Possible approach:**
1. Extract block info
2. Use `ctx.VM.Execute(homeMethod, blockContextId)`
3. Or: Set active context to block and continue execution

### Complex (Requires Process/Semaphore infrastructure)

#### Primitives 85-88: Process/Semaphore
**Requirements:**
- Process class: suspendedContext, priority
- Semaphore class: excessSignals, firstLink (waiting processes)
- ProcessorScheduler: activeProcess, process queues by priority
- Context switching mechanism
- Process suspension/resumption

**Not feasible with current architecture** - would require:
- Multi-context execution
- Process scheduler integration
- Semaphore wait queues

## Graphics & Input Primitive Implementations

### Graphics Primitives (Primitives 90-95, 96-104)

**Commit 26e5f0f** - "feat: complete graphics and UI implementation with Windows Forms (80% primitive coverage)"

Includes complete implementations of:

#### Input Primitives (90-95)
- Location: `git show 26e5f0f:Smalltalk80.Interpreter/Primitives/InputPrimitives.cs`
- **Primitive 90**: mousePoint - Returns Point object with current mouse coordinates
- **Primitive 91**: cursorLocPut: - Sets cursor position (stub for Windows Forms limitation)
- **Primitive 92**: cursorLink: - Links cursor to Form object
- **Primitive 93**: inputSemaphore: - Registers semaphore for input events
- **Primitive 94**: sampleInterval: - Sets input sampling interval
- **Primitive 95**: inputWord - Returns encoded input event word

**Dependencies:**
- `IDisplayProvider` interface for display/input abstraction
- Methods: `GetMousePosition()`, `GetInputWord()`, `SetCursor()`

#### Graphics Primitives (96-104)
- Location: `git show 26e5f0f:Smalltalk80.Interpreter/Primitives/GraphicsPrimitives.cs`
- **Primitive 96**: copyBits - Complete BitBlt implementation with all 16 combination rules
- **Primitive 97**: snapshot - Image save (stub)
- **Primitive 98**: oopsLeft - Returns available object pointers
- **Primitive 99**: coreLeft - Returns available memory
- **Primitive 100**: timeWords - Returns Unix epoch seconds
- **Primitive 101**: beDisplay - Makes Form the active display
- **Primitive 102**: beCursor - Sets hardware cursor
- **Primitive 103**: beep - System beep (Console.Beep)
- **Primitive 104**: clipboardText - Clipboard operations (stub)

**Supporting Files (all in commit 26e5f0f):**
1. `Smalltalk80.Interpreter/Graphics/IDisplayProvider.cs` - Display/input abstraction interface
2. `Smalltalk80.Interpreter/Graphics/BitBltEngine.cs` - Complete BitBlt implementation (200 lines)
3. `Smalltalk80.Interpreter/Graphics/BitBltOperation.cs` - BitBlt operation descriptor
4. `Smalltalk80.Interpreter/Graphics/CombinationRule.cs` - 16 BitBlt combination rules enum
5. `Smalltalk80.Interpreter/Graphics/WinFormsDisplayProvider.cs` - Windows Forms implementation

#### IDisplayProvider Interface

```csharp
public interface IDisplayProvider
{
    Memory<byte> GetDisplayBuffer();  // Direct access to display bits
    int Width { get; }
    int Height { get; }
    int BitsPerPixel { get; }

    void InvalidateRect(int x, int y, int width, int height);
    void InvalidateAll();
    void Show();
    void Hide();
    bool ProcessEvents();

    // Input support
    (int X, int Y) GetMousePosition();
    int GetMouseButtons();
    int GetInputWord();
    void SetCursor(ReadOnlySpan<byte> cursorBits, int hotspotX, int hotspotY);
}
```

#### BitBlt Architecture

**BitBltEngine** features:
- Pure C# implementation with all 16 combination rules
- Optimized for 1-bit monochrome bitmaps
- Handles clipping, source/dest overlap
- Halftone pattern support
- Pixel-by-pixel processing (could be optimized to byte-level)

**BitBlt Combination Rules:**
0. Clear, 1. And, 2. AndReverse, 3. Copy, 4. AndInverted, 5. Noop
6. Xor, 7. Or, 8. Nor, 9. Equiv, 10. Invert, 11. OrReverse
12. CopyInverted, 13. OrInverted, 14. Nand, 15. Set

### Migration Path for Graphics/Input

**Architecture Compatibility:**
- Uses `PrimitiveResult` return type (needs conversion to our pattern)
- Uses `IPrimitiveContext` methods that we don't have:
  - `context.GetSpecialObject(index)` - Get special objects (Point class, etc.)
  - `context.NewInstance(classRef, indexedSize)` - Allocate new instance
  - `context.GetInstanceVariable(oop, index)` - Get instance variable
  - `context.SetInstanceVariable(oop, index, value)` - Set instance variable
  - `context.IsHeapObject(ref)` - Check if heap object
  - `context.GetIndexedBytes(oop)` - Get byte array data
  - `context.LookupGlobal(name)` - Look up global (e.g., "Display")

**Required IPrimitiveContext Extensions:**

```csharp
public interface IPrimitiveContext
{
    SmalltalkHeap Heap { get; }
    LayoutResolver LayoutResolver { get; }
    bool PrimitiveSuccess { get; set; }

    // ADD THESE:
    SObjectRef GetSpecialObject(int index);
    SObjectRef NewInstance(SObjectRef classRef, int indexedSize);
    SObjectRef GetInstanceVariable(int oop, int index);
    void SetInstanceVariable(int oop, int index, SObjectRef value);
    bool IsHeapObject(SObjectRef obj);
    Memory<byte>? GetIndexedBytes(int oop);
    SObjectRef? LookupGlobal(string name);
}
```

**Infrastructure Files to Copy:**

1. Copy entire `Graphics/` folder from commit 26e5f0f:
   - IDisplayProvider.cs
   - BitBltEngine.cs
   - BitBltOperation.cs
   - CombinationRule.cs
   - WinFormsDisplayProvider.cs (optional - for testing)

2. Update primitive signatures from:
   ```csharp
   public static PrimitiveResult PrimitiveMousePoint(SObjectRef receiver, SObjectRef[] arguments, IPrimitiveContext context)
   ```
   To:
   ```csharp
   public static SObjectRef MousePoint(IPrimitiveContext ctx, SObjectRef receiver, SObjectRef[] args)
   ```

3. Convert return pattern from:
   ```csharp
   return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
   return PrimitiveResult.CreateSuccess(result);
   ```
   To:
   ```csharp
   ctx.PrimitiveSuccess = false;
   return SpecialObjectsArray.Instance.Nil;
   return result;  // Success
   ```

## Recommendations

### Phase 0: Graphics Infrastructure (High Value)
1. Copy Graphics folder from commit 26e5f0f
2. Extend IPrimitiveContext with required methods
3. Implement extended methods in VirtualMachine
4. Update InputPrimitives.cs with working implementations
5. Update GraphicsPrimitives.cs with BitBlt implementation
6. **Effort: 2-4 hours**
7. **Value: Enables 10 primitives (90-95, 96-104)** - Display output capability!

### Phase 1: Enable Perform (Primitives 83-84)
1. Add `SendMessage` to `IPrimitiveContext`
2. Implement in VirtualMachine
3. Update ControlPrimitives.cs with working implementation
4. **Effort: 1-2 hours**

### Phase 2: BlockContext Support (Primitives 80-82)
1. Define BlockContext class layout in JSON schemas
2. Add `ActiveContext` property to `IPrimitiveContext`
3. Implement BlockCopy to capture current PC
4. Implement Value/Value: with context switching
5. **Effort: 4-8 hours** (complex due to context switching)

### Phase 3: Process/Semaphore (Primitives 85-88)
1. Define Process and Semaphore class layouts
2. Implement ProcessorScheduler
3. Add process switching to VM
4. Implement primitives
5. **Effort: 8-16 hours** (very complex)

## Files to Reference

### Working Implementations
- `git show cd05556:Smalltalk80/Primitives/Control/ControlPrimitives.cs` - Full working code
- `git show 26e5f0f:Smalltalk80.Interpreter/Primitives/ControlPrimitives.cs` - InterpreterPrimitiveContext version

### Current Files to Modify
- `Smalltalk80.Interpreter/VM/Primitives/IPrimitiveContext.cs` - Add SendMessage, ActiveContext
- `Smalltalk80.Interpreter/VM/Primitives/ControlPrimitives.cs` - Implement primitives
- `Smalltalk80.Interpreter/VM/VirtualMachine.cs` - Expose SendMessage, add BlockContext support
