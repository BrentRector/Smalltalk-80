# StandardLibrary.st Missing Dependencies Report

## Executive Summary
The StandardLibrary.st file contains references to several classes, global variables, and primitives that are not defined within the library itself. These missing dependencies need to be addressed for the Smalltalk-80 interpreter to function correctly.

## Missing Global Variables

### Core System Globals
1. **Smalltalk** - System dictionary (used 17 times)
   - Referenced in class management, name lookups, and class renaming
   - Critical for: `Class>>rename:`, `Class>>subclass:*` methods

2. **Transcript** - System transcript for output (used 5 times)
   - Referenced in debugging and status messages
   - Used in: `Class>>recompile:`, `Class>>addSelector:withMethod:`

3. **SystemOrganization** - System organization registry (used 5 times)
   - Referenced for class categorization
   - Used in: `Class>>category`, `Class>>category:`, `Class>>subclassDefinerClass`

## Missing Classes

### UI/Graphics Classes
1. **Cursor** (used 5 times)
   - Referenced for UI feedback during operations
   - Methods: `Cursor execute`, `Cursor write`, `Cursor read`

2. **Point** - Not directly referenced but implied by primitive 18 (MakePoint)

3. **Rectangle**, **Form**, **Bitmap** - Not found in library but may be needed for graphics

### System Infrastructure Classes
1. **Compiler** (used 3 times)
   - Referenced for dynamic code compilation
   - Used in: `ReadWriteStream>>fileInAnnouncing:`

2. **ChangeSet** (referenced in comments)
   - System change tracking mechanism

3. **ByteArray** (used 2 times)
   - Referenced in `FileStream>>nextPutAll:`
   - Needed for binary data handling

4. **MethodContext** - Execution context (not explicitly referenced but implied)

5. **CompiledMethod** - Compiled method representation (not explicitly referenced)

6. **Process**, **ProcessScheduler** - Process management (not found)

7. **Semaphore**, **Mutex** - Synchronization primitives (not found)

## Primitive Dependencies

### Implemented Primitives (Found in PrimitiveDispatcher.cs)
The system has implementations for primitives 1-157 and 170, including:
- Integer arithmetic (1-17)
- Large integer operations (21-37)
- Float operations (40-54)
- Array/Stream operations (60-67)
- Storage management (68-79)
- Control flow (80-89)
- I/O operations (90-105)
- System operations (110-116)
- File system (142-157)
- Character creation (170)

### Primitives Used in StandardLibrary.st
The library references the following primitives:
- Storage: 70, 71 (new, new:)
- Object: 75, 77, 78, 89 (asOop, someInstance, nextInstance, flushCache)
- Array: 60, 61, 62, 72-74 (at:, at:put:, size, become:, instVarAt:, instVarAt:put:)
- Control: 83, 84 (perform:, perform:with:)
- System: 110, 111, 112 (==, class, coreLeft)
- Numeric: 1-17 (arithmetic)
- String: 105 (stringReplace)
- Object enumeration: 139 (nextObject)

All referenced primitives appear to be implemented.

## Recommendations

### Priority 1: Critical Missing Components
1. **Create Smalltalk global dictionary class**
   - Implement as a specialized Dictionary
   - Add methods: `at:`, `at:put:`, `includesKey:`, `keyAtValue:`, `removeClassNamed:`
   - Initialize during system bootstrap

2. **Create Transcript global**
   - Implement as TextCollector (already exists in library)
   - Initialize during system bootstrap
   - Wire to console output

3. **Create SystemOrganization**
   - Implement basic class categorization
   - Methods needed: `categoryOfElement:`, `classify:under:`

### Priority 2: System Infrastructure
1. **Add Compiler class stub**
   - Implement `evaluate:logged:` method
   - Can initially just parse and execute expressions

2. **Add ByteArray class**
   - Subclass of ArrayedCollection
   - Support binary data operations

3. **Add Cursor class stub**
   - Implement `execute`, `write`, `read` class methods
   - Can be no-ops initially

### Priority 3: Process Management (Future)
1. Process, ProcessScheduler
2. Semaphore, Mutex
3. Delay

### Priority 4: Graphics (Future)
1. Point, Rectangle
2. Form, Bitmap
3. DisplayScreen

## Implementation Notes

### Bootstrap Order
1. Initialize core globals (Smalltalk, SystemOrganization)
2. Create Transcript instance
3. Load StandardLibrary.st
4. Execute initialization statements
5. Run class initializers

### Global Variable Registration
The system needs a mechanism to register and access global variables beyond classes. Consider:
- Extending SystemDictionary to handle non-class globals
- Creating a separate GlobalDictionary
- Adding methods to access globals from compiled code

## Status Summary
- ✅ All core primitives are implemented
- ❌ Critical system globals missing (Smalltalk, Transcript, SystemOrganization)
- ❌ Several system classes referenced but not defined
- ⚠️ File system classes have been modernized (may have compatibility issues)
- ✅ Core class hierarchy is complete (Object, Behavior, Class, Metaclass)