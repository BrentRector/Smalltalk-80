# Snapshot Save and Restore Architecture

**Last Updated**: 2025-12-29

## Overview

The Smalltalk-80 snapshot mechanism provides persistent image saving and restoration. When a snapshot is saved, the entire state of the running system is captured to a file. When restored, the system resumes execution exactly where it left off, as if no interruption occurred.

This enables:
- Saving work in progress and resuming later
- Distributing pre-built images without cold start
- Future removal of all cold start logic (the image becomes self-contained)

## Smalltalk-Level API

The snapshot is initiated from Smalltalk code:

```smalltalk
SystemDictionary>>snapshotAs: fileName thenQuit: aBoolean
    "Save the current state to fileName. If aBoolean is true, quit after saving."

    | justSnapped |
    SourceFiles at: 1 put: nil.  "Close sources file"
    SourceFiles at: 2 put: nil.  "Close changes file"

    fileName asSnapshotFile beSnapshotFile.
    justSnapped := self snapshotPrimitive.  "primitive 97"

    "justSnapped is nil if we just saved, receiver if we just restored"
    justSnapped isNil & aBoolean ifTrue: [self quitPrimitive].

    "Reopen source files on restore..."
```

**Key insight**: Primitive 97 returns **nil** on save, **receiver** on restore. This single return value distinction allows Smalltalk code to detect whether it just saved or just resumed from a saved image.

## Image File Format

### Binary Encoding Conventions

| Aspect | Specification |
|--------|---------------|
| **Byte Order** | Little-endian (Intel/x86 native order) |
| **Integer Types** | Signed 32-bit for all integer fields |
| **String Encoding** | ASCII for magic number |
| **OOP Encoding** | Tagged 32-bit value (see below) |

### OOP (Object Pointer) Encoding

All OOP values use LSB tagging in a 32-bit signed integer:

| LSB | Interpretation | Value Extraction |
|-----|----------------|------------------|
| 0 | Object table pointer | `objectTableIndex = rawValue >> 1` |
| 1 | SmallInteger immediate | `integerValue = rawValue >> 1` (arithmetic shift) |

Example values:
- `0x00000004` → Object table index 2 (4 >> 1 = 2)
- `0x00000015` → SmallInteger 10 ((0x15 >> 1) = 10, LSB=1)
- `0xFFFFFFFD` → SmallInteger -2 ((-3 >> 1) = -2, LSB=1)

### ObjectFormat Enum

| Value | Name | Description |
|-------|------|-------------|
| 0 | Pointer | Named instance variables only, no indexed portion |
| 1 | PointerIndexed | Named instance variables + indexed OOP fields (Array, OrderedCollection) |
| 2 | ByteIndexed | Named instance variables + indexed byte fields (String, ByteArray, CompiledMethod) |
| 3 | WordIndexed | Named instance variables + indexed 16-bit word fields (WordArray) |

### Header (16 bytes)

| Offset | Size | Type | Field | Description |
|--------|------|------|-------|-------------|
| 0 | 4 | char[4] | Magic | "WOST" (ASCII, Wise Owl Smalltalk) |
| 4 | 4 | int32 | Version | Format version (currently 1) |
| 8 | 4 | int32 | ObjectCount | Number of objects in file |
| 12 | 4 | int32 | MaxObjectId | Highest object table index used |

### Per-Object Record

Each object is stored with its identity preserved. All OOP fields use the tagged raw value format for consistency.

| Offset | Size | Type | Field | Description |
|--------|------|------|-------|-------------|
| 0 | 4 | int32 | ObjectOOP | Raw tagged OOP value (objectTableIndex << 1) |
| 4 | 1 | uint8 | Format | ObjectFormat enum value (0-3) |
| 5 | 4 | int32 | InstanceVarCount | Number of named instance variables |
| 9 | 4 | int32 | IndexedSize | Number of indexed elements (0 if none) |
| 13 | 4 | int32 | ClassOOP | Raw tagged OOP value of class |
| 17 | 4 × N | int32[] | InstanceVars | Raw tagged OOP values for each instance variable |
| 17 + 4N | varies | varies | IndexedData | Format-specific indexed content |

**Per-object header size**: 17 bytes fixed + variable data

**Design decision**: All OOP fields (ObjectOOP, ClassOOP, InstanceVars, PointerIndexed data) store the raw tagged value rather than the object table index. This is self-describing and consistent - readers don't need to know which fields are OOPs vs integers.

### Indexed Data By Format

| Format | IndexedData Size | Element Type | Description |
|--------|------------------|--------------|-------------|
| Pointer (0) | 0 bytes | N/A | No indexed data |
| PointerIndexed (1) | 4 × IndexedSize | int32 (OOP) | Array of tagged OOP values |
| ByteIndexed (2) | IndexedSize | uint8 | Raw byte array |
| WordIndexed (3) | 2 × IndexedSize | uint16 | Array of 16-bit words (little-endian) |

**Note**: Any indexed object (PointerIndexed, ByteIndexed, or WordIndexed) may have named instance variables in addition to its indexed portion. Both are stored: first the instance variables as OOPs, then the indexed data in format-specific encoding. For example, CompiledMethod is ByteIndexed with instance variables for header, literals, etc.

### Complete File Structure

```
+---------------------------+
| Header (16 bytes)         |
|  - Magic: "WOST"          |
|  - Version: 1             |
|  - ObjectCount: N         |
|  - MaxObjectId: M         |
+---------------------------+
| Object 1                  |
|  - ObjectOOP              |
|  - Format                 |
|  - InstanceVarCount       |
|  - IndexedSize            |
|  - ClassOOP               |
|  - InstanceVars[...]      |
|  - IndexedData[...]       |
+---------------------------+
| Object 2                  |
|  - ...                    |
+---------------------------+
| ...                       |
+---------------------------+
| Object N                  |
|  - ...                    |
+---------------------------+
```

Objects appear in ascending object table index order (as enumerated from the object table). Gaps in the sequence indicate garbage-collected objects that are not stored.

## Save Operation

### Trigger

Save is triggered by primitive 97 (snapshotPrimitive).

### Pre-Save State Flush

Before writing the image, the VM must flush cached interpreter state back to heap objects.

#### Why Caching Exists

The VM caches IP (instruction pointer) and SP (stack pointer) in C# variables for performance. Every bytecode execution requires reading IP, incrementing it, and often modifying SP. Going through the heap for each operation would be prohibitively slow:

```csharp
// Slow: heap access per bytecode
var ipOOP = _heapManager.GetInstanceVariable(context, PC_INDEX);
int ip = ipOOP.AsSmallInteger();
ip++;
_heapManager.SetInstanceVariable(context, PC_INDEX, SmalltalkOOP.FromSmallInteger(ip));

// Fast: cached C# variables
byte bytecode = _bytecodes[_ip++];
```

#### What Must Be Flushed

Only IP and SP need explicit flushing:

1. **IP/SP**: Write current values back to the active MethodContext/BlockContext

What does NOT need explicit action:

2. **suspendedContext**: Already points to the active context. When a process is activated via `ActivateProcess()`, the context is read from `suspendedContext` but the field is not cleared. The reference remains valid while the process runs.

3. **activeProcess**: Already current in ProcessScheduler - maintained by normal process scheduling.

#### GC Reachability

The active context remains reachable for garbage collection through the chain:

```
Smalltalk (GC root) → Processor → activeProcess → suspendedContext → context
```

This is why `suspendedContext` is not cleared on activation - it provides the GC root for the running context.

### Write Sequence

1. **Collect live objects**: Enumerate object table (skips null/freed entries)
2. **Track max object ID**: Find highest index during enumeration
3. **Write header**: Magic, version, object count, max object ID
4. **Write each object**: Preserve object table index, format, class, all data

### Post-Save

Primitive 97 returns **nil** to Smalltalk, indicating "save completed successfully."

## Restore Operation

### Startup Path

On restore, the VM takes a completely different startup path:

```
Normal startup:     VM init → Cold Start (Phases 1-6) → Init Expressions → Interactive
Restore startup:    VM init → Load Image → Resume Execution
```

Cold start is **completely bypassed**. The image contains everything needed.

### Restore Sequence

#### 1. Read Header

```csharp
string magic = ReadString(4);      // Verify "WOST"
int version = ReadInt32();          // Verify supported version
int objectCount = ReadInt32();      // Number of objects to read
int maxObjectId = ReadInt32();      // Highest object table index
```

#### 2. Prepare Object Table

```csharp
heapManager.ObjectTable.EnsureCapacity(maxObjectId);
```

This pre-allocates enough blocks to accommodate all object indices without repeated growth.

#### 3. Load Objects

For each object in the file:

```csharp
SmalltalkOOP objectOOP = ReadOOP();           // Tagged OOP value
int objectId = objectOOP.ToObjectTableIndex(); // Extract index
ObjectFormat format = (ObjectFormat)ReadByte();
int instanceVarCount = ReadInt32();
int indexedSize = ReadInt32();
SmalltalkOOP classOOP = ReadOOP();

// Allocate storage based on format
var instanceVars = new SmalltalkOOP[instanceVarCount];
for (int i = 0; i < instanceVarCount; i++)
    instanceVars[i] = ReadOOP();

object indexedStorage = format switch {
    PointerIndexed => ReadOOPArray(indexedSize),
    ByteIndexed => ReadByteArray(indexedSize),
    WordIndexed => ReadWordArray(indexedSize),
    _ => null
};

// Create entry and place at original index
var entry = new ObjectTableEntry(format, classOOP, instanceVars, indexedStorage);
objectTable.SetObjectTableEntry(objectId, entry);
```

**Critical**: Objects are placed at their **original indices**. This preserves all OOP references - an object that was at index 42 during save is restored to index 42, so all pointers to it remain valid.

#### 4. Rebuild Free List

After all objects are loaded, scan for gaps (null entries) and add them to the free list:

```csharp
heapManager.ObjectTable.RebuildFreeList();
```

This ensures future allocations reuse freed indices before growing the table.

#### 5. Locate Active Process

Find the process that was running when the snapshot was taken:

```csharp
// Get Processor global (ProcessScheduler instance)
var processorOOP = heapManager.GetGlobal("Processor");

// Get activeProcess field from ProcessScheduler
var activeProcessOOP = heapManager.GetInstanceVariable(processorOOP,
    ProcessSchedulerInstanceVars.ActiveProcess);

// Get suspended context from Process
var activeContextOOP = heapManager.GetInstanceVariable(activeProcessOOP,
    ProcessInstanceVars.SuspendedContext);
```

#### 6. Initialize VM Interpreter State

Set up the VM's cached execution state from the restored context:

```csharp
vm.SetActiveProcess(activeProcessOOP);
vm.SetActiveContext(activeContextOOP);
vm.LoadContextState();  // Load IP, SP, temps from context object
```

#### 7. Signal Restore via Return Value

The context is positioned just after the primitive 97 send. Push the **receiver** (not nil) as the return value:

```csharp
// Push receiver as primitive 97's "return value"
// This signals to Smalltalk: "you just restored, not saved"
vm.Push(receiver);
```

#### 8. Resume Execution

Start the interpreter loop. From Smalltalk's perspective, primitive 97 just returned the receiver, indicating a restore occurred.

## Object Table Considerations

### Sparse Table

The object table after restore is sparse - it contains gaps where objects were garbage collected before the save. These gaps are:
- Left as null entries
- Added to the free list for reuse
- Never written to the snapshot file

### OOP Preservation

All OOP values (object references) are preserved exactly:
- Object at index N during save → Object at index N after restore
- All instance variable references remain valid
- All indexed OOP references remain valid
- Class references remain valid

### Free List Rebuild

The `RebuildFreeList()` method scans the object table after restore:

```csharp
public void RebuildFreeList()
{
    _freeObjectTableIndices.Clear();
    for (int index = 1; index < _nextObjectTableIndex; index++)
    {
        if (GetEntry(index) == null)
            _freeObjectTableIndices.Push(index);
    }
}
```

This ensures:
- Freed indices are reused before allocating new ones
- Object table doesn't grow unnecessarily
- Memory remains compact over save/restore cycles

## VM State Synchronization

### State That Lives in Heap Objects

Most VM state is stored in Smalltalk objects and automatically saved/restored:

| State | Location |
|-------|----------|
| Active process | ProcessScheduler.activeProcess |
| Ready queues | ProcessScheduler.quiescentProcessLists |
| Semaphores | Semaphore objects with waiting process lists |
| Context chain | MethodContext/BlockContext objects |
| Method temps | Context.tempFrame |
| Execution point | Context.instructionPointer, stackPointer |

### State That Lives in C# (Must Be Flushed/Restored)

The VM caches execution state in C# variables for performance. This state must be synchronized with heap objects for save/restore:

| C# Variable | Heap Location | Flush Before Save | Restore After Load |
|-------------|---------------|-------------------|-------------------|
| _instructionPointer | Context.instructionPointer | **Yes** | Yes |
| _stackPointer | Context.stackPointer | **Yes** | Yes |
| _activeContext | Process.suspendedContext | No (already set) | Yes |
| _activeProcess | Processor.activeProcess | No (already set) | Yes |

**Key insight**: Only IP and SP need explicit flushing. The `_activeContext` and `_activeProcess` C# variables are caches of heap state that's already correct - `suspendedContext` points to the active context, and `activeProcess` is maintained by process scheduling.

### Flush Protocol (Before Save)

```csharp
void FlushVMStateToHeap()
{
    // Write IP back to context
    heapManager.SetInstanceVariable(_activeContext,
        ContextInstanceVars.InstructionPointer,
        SmalltalkOOP.FromSmallInteger(_instructionPointer));

    // Write SP back to context
    heapManager.SetInstanceVariable(_activeContext,
        ContextInstanceVars.StackPointer,
        SmalltalkOOP.FromSmallInteger(_stackPointer));

    // No need to set suspendedContext - already points to active context
    // No need to set activeProcess - already current in ProcessScheduler
}
```

### Restore Protocol (After Load)

```csharp
void RestoreVMStateFromHeap()
{
    // Find active process from Processor global
    var processor = heapManager.GetGlobal("Processor");
    _activeProcess = heapManager.GetInstanceVariable(processor,
        ProcessSchedulerInstanceVars.ActiveProcess);

    // Get context from process
    _activeContext = heapManager.GetInstanceVariable(_activeProcess,
        ProcessInstanceVars.SuspendedContext);

    // Load IP and SP from context
    _instructionPointer = heapManager.GetInstanceVariable(_activeContext,
        ContextInstanceVars.InstructionPointer).ToSmallInteger();
    _stackPointer = heapManager.GetInstanceVariable(_activeContext,
        ContextInstanceVars.StackPointer).ToSmallInteger();
}
```

## Future: Eliminating Cold Start

Once snapshot restore is fully operational, cold start becomes optional:

1. **Bootstrap once**: Run cold start to create initial image
2. **Save bootstrap image**: Snapshot the fully initialized system
3. **Distribute image**: Ship the .im file instead of source + compiler
4. **Remove cold start**: Future VM versions can remove all Phase 1-6 code

The image becomes the canonical distribution format, just like the original Xerox Smalltalk-80 system.

## Error Handling

### Save Errors

- File I/O errors: Return failure from primitive 97, don't quit
- Disk full: Same as above
- Invalid path: Primitive fails

### Restore Errors

- Invalid magic: Reject file, fall back to cold start or error
- Version mismatch: Reject file
- Corrupted object data: Fatal error (cannot recover)
- Missing objects: Fatal error (dangling references)

## Testing Strategy

1. **Round-trip test**: Save image, restore, verify all globals present
2. **Execution test**: Save mid-computation, restore, verify correct result
3. **GC test**: Run GC, save, restore, verify free list rebuilt correctly
4. **Process test**: Save with multiple processes, restore, verify scheduling works

## Summary

| Aspect | Save | Restore |
|--------|------|---------|
| Trigger | Primitive 97 | VM startup with image file |
| Heap | Enumerate & write live objects | Read & place at original indices |
| VM State | Flush caches to context objects | Load caches from context objects |
| Free List | N/A (gaps not saved) | Rebuild by scanning for nulls |
| Return Value | nil (to Smalltalk) | receiver (to Smalltalk) |
| Cold Start | N/A | Completely bypassed |
