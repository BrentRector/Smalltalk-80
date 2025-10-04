# ProcessScheduler Cold Start Initialization Bug Fix

## Problem
The Process object was being created but never linked to ProcessorScheduler's activeProcess instance variable, causing it to be collected by the garbage collector on the first GC run after cold start.

## Root Cause
In `ColdStartBootstrap.cs`, the method `SpecialObjectsArray.SetActiveProcess()` was a no-op (does nothing) because ActiveProcess is not stored directly in the Special Objects Array. Instead, it must be accessed via ProcessorScheduler's instance variable at index 1.

## Solution Implemented

### Changes to `D:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80.Interpreter\ColdStart\ColdStartBootstrap.cs`

1. **Added using directive** for BlueBookFactory to access array creation utilities:
   ```csharp
   using Smalltalk80.Interpreter.Core.BlueBook;
   ```

2. **Fixed Phase2_2_CreateSingletons method** to properly initialize ProcessorScheduler:
   - **Linked activeProcess** to ProcessorScheduler using the typed STProcessorScheduler layout
   - **Created quiescentProcessLists** - an Array of 8 LinkedLists (one per priority level)
   - **Initialized each LinkedList** with nil firstLink and lastLink (empty lists)

### Key Code Changes
```csharp
// CRITICAL FIX: Link activeProcess to ProcessorScheduler to prevent GC collection
// The Process must be reachable via: SOA[4] -> ProcessorScheduler -> activeProcess
var scheduler = new STProcessorScheduler(processorScheduler);
scheduler.ActiveProcess = activeProcess;

// Create quiescentProcessLists - Array of 8 LinkedLists (one per priority level)
var quiescentLists = BlueBookFactory.CreateArray(arrayClass, 8);

// Initialize each priority list as an empty LinkedList
for (var i = 0; i < 8; i++)
{
    var linkedList = ObjectMemoryNew.CreateInstance(...);
    var list = new STLinkedList(linkedList);
    list.FirstLink = SObjectRef.Nil;
    list.LastLink = SObjectRef.Nil;
    quiescentLists.SetIndexedPointer(i, linkedList);
}

scheduler.QuiescentProcessLists = quiescentLists;
```

## Verification Results

After the fix, the GC validation shows:
- **Collection #1: 0 collected** (previously was collecting the orphaned Process)
- **3413 objects reachable from SOA**
- **Second GC also collected 0 objects**, confirming stability

## GC Reachability Chain

The fix ensures proper object reachability:
```
SpecialObjectsArray[4] → ProcessorScheduler
  ├─ Instance Variable 0: quiescentProcessLists → Array[8]
  │   └─ Each element → LinkedList (empty, for each priority level)
  └─ Instance Variable 1: activeProcess → Process object
```

## Blue Book Compliance

This implementation follows the Blue Book specification for ProcessorScheduler:
- Instance variable 0: `quiescentProcessLists` - Array of 8 LinkedLists
- Instance variable 1: `activeProcess` - The currently executing Process
- Each priority level (0-7) has its own LinkedList for queued processes

## Benefits of Using C# 13 Features

1. **Typed Layout Structs** (STProcessorScheduler, STLinkedList) provide compile-time safety
2. **Target-typed new expressions** reduce boilerplate
3. **Expression-bodied properties** in layout structs improve readability
4. **Primary constructors** in layout structs reduce ceremony

## Testing

A test utility `TestProcessorScheduler.cs` was created to validate the ProcessorScheduler structure after cold start initialization.