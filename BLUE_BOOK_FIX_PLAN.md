# Blue Book Compliance Fix Plan

## Critical Issues Found

### 1. SMethodContext - WRONG FIELD COUNT
**Blue Book**: 6 fields (sender, pc, stackp, method, receiverMap, receiver)
**Current**: 5 fields (sender@0, ip@1, sp@2, method@3, receiver@4)
**Missing**: receiverMap@4

**Fix**:
- Add receiverMap field at index 4
- Move receiver from index 4 to index 5
- Update all references to use BlueBookLayouts.MethodContext constants

### 2. SBlockContext - WRONG FIELD NAMES
**Blue Book**: sender, pc, stackp, nargs, startpc, home
**Current**: caller, instructionPointer, stackPointer, argumentCount, initialIP, home

**Fix**:
- Rename fields to match Blue Book exactly
- Use BlueBookLayouts.BlockContext constants

### 3. SCompiledMethod - WRONG STRUCTURE
**Blue Book**: ByteArray variableByteSubclass with 0 instance variables
**Current**: Stores data as C# properties (selector, bytecodes, literals, etc.)

**Issue**: CompiledMethod should store everything in indexed bytes, not as C# properties!

**Fix**:
- CompiledMethod is a variableByteSubclass - should use ByteIndexed format
- Header encoding + literals + bytecodes all in indexed data
- No instance variables, no C# properties

### 4. SProcess - MISSING SUPERCLASS FIELDS
**Blue Book**: Link subclass with nextLink@0, then suspendedContext@1, priority@2, myList@3
**Current**: 3 fields starting at index 0

**Fix**:
- Add nextLink field from Link superclass at index 0
- Shift all fields: suspendedContext@1, priority@2, myList@3
- Use BlueBookLayouts.Process constants

### 5. SSemaphore - MISSING SUPERCLASS FIELDS
**Blue Book**: LinkedList subclass with firstLink@0, lastLink@1, then excessSignals@2
**Current**: 2 fields (excessSignals@0, waitingProcesses@1)

**Fix**:
- Add firstLink@0 and lastLink@1 from LinkedList superclass
- Move excessSignals to index 2
- Remove C# Queue<SProcess> - use Smalltalk LinkedList
- Use BlueBookLayouts.Semaphore constants

### 6. SDictionary - WRONG STRUCTURE
**Blue Book**: Set variableSubclass with tally@0 + variable part for associations
**Current**: Has tally@0 and array@1, plus C# Dictionary<SObject, SObject>

**Issue**: Dictionary should store Association objects in variable part, not use C# Dictionary!

**Fix**:
- Remove C# Dictionary<SObject, SObject>
- Use SInstance with PointerIndexed format
- tally@0, variable part holds Association objects
- Use BlueBookLayouts.Dictionary constants

## Implementation Strategy

**Option A**: Fix all specialized classes to match Blue Book exactly
**Option B**: Remove specialized classes entirely, use generic SInstance everywhere

**Recommendation**: Option A initially (less breaking), then migrate to Option B later

## Action Plan

1. Create helper methods in BlueBookLayouts for creating correctly-sized instances
2. Fix each specialized class one at a time
3. Update all cold start code to use correct field counts
4. Update all code that creates/accesses these classes
5. Add unit tests to verify field counts match Blue Book

## Files to Update

### Core Classes
- `Core/Objects/SMethodContext.cs` - Add receiverMap field
- `Core/Objects/SBlockContext.cs` - Fix field names
- `Core/Objects/SCompiledMethod.cs` - Major restructure needed
- `Core/Objects/SProcess.cs` - Add nextLink field
- `Core/Objects/SSemaphore.cs` - Add firstLink/lastLink fields
- `Core/Objects/SDictionary.cs` - Remove C# Dictionary, use associations

### Cold Start
- All files in `ColdStart/` that create these objects
- Verify instance variable counts match BlueBookLayouts

### Execution
- All bytecode interpreters that access context fields
- All primitives that access these object fields

## Testing
- Verify each object created has correct field count
- Verify field access uses BlueBookLayouts constants
- Verify no hardcoded indices exist