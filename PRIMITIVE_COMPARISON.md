# Smalltalk-80 Primitive Comparison Report

## Overview
This document compares the primitive implementations between:
1. **SavedInterpreter**: The archived implementation
2. **New Interpreter**: The current implementation
3. **Blue Book Specification**: The Smalltalk-80 standard

## Primitive Categories

### SmallInteger Arithmetic (1-17)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 1 | primitiveAdd | SmallInteger + | ✓ | ✓ | Complete |
| 2 | primitiveSubtract | SmallInteger - | ✓ | ✓ | Complete |
| 3 | primitiveLessThan | SmallInteger < | ✓ | ✓ | Complete |
| 4 | primitiveGreaterThan | SmallInteger > | ✓ | ✓ | Complete |
| 5 | primitiveLessOrEqual | SmallInteger <= | ✓ | ✓ | Complete |
| 6 | primitiveGreaterOrEqual | SmallInteger >= | ✓ | ✓ | Complete |
| 7 | primitiveEqual | SmallInteger = | ✓ | ✓ | Complete |
| 8 | primitiveNotEqual | SmallInteger ~= | ✓ | ✓ | Complete |
| 9 | primitiveMultiply | SmallInteger * | ✓ | ✓ | Complete |
| 10 | primitiveDivide | SmallInteger / (exact) | ✓ | ✓ | Complete |
| 11 | primitiveModulo | SmallInteger \\\\ | ✓ | ✓ | Complete |
| 12 | primitiveDiv | SmallInteger // | ✓ | ✓ | Complete |
| 13 | primitiveQuo | SmallInteger quo: | ✓ | ✓ | Complete |
| 14 | primitiveBitAnd | SmallInteger bitAnd: | ✓ | ✓ | Complete |
| 15 | primitiveBitOr | SmallInteger bitOr: | ✓ | ✓ | Complete |
| 16 | primitiveBitXor | SmallInteger bitXor: | ✓ | ✓ | Complete |
| 17 | primitiveBitShift | SmallInteger bitShift: | ✓ | ✓ | Complete |

### Numeric Type Testing (18-20)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 18 | primitiveIsInteger | Test if Integer | ❌ | ❌ | **MISSING** |
| 19 | primitiveIsFloat | Test if Float | ❌ | ❌ | **MISSING** |
| 20 | primitiveIsSmallInteger | Test if SmallInteger | ❌ | ✓ | Partial |

### LargeInteger Arithmetic (21-37)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 21-37 | LargeInteger ops | Same as 1-17 for LargeInteger | ❌ | ❌ | **MISSING** |

### Float Arithmetic (38-59)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 38-59 | Float operations | Float arithmetic & comparison | ❌ | ❌ | **MISSING** |

### Array and Stream Access (60-67)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 60 | primitiveAt | Object at: | ✓ | ✓ | Complete |
| 61 | primitiveAtPut | Object at:put: | ✓ | ✓ | Complete |
| 62 | primitiveSize | Object size | ✓ | ✓ | Complete |
| 63 | primitiveStringAt | String at: | ✓ | ✓ | Complete |
| 64 | primitiveStringAtPut | String at:put: | ✓ | ✓ | Complete |
| 65 | primitiveNext | Stream next | ✓ | ❌ | **MISSING** |
| 66 | primitiveNextPut | Stream nextPut: | ✓ | ❌ | **MISSING** |
| 67 | primitiveAtEnd | Stream atEnd | ✓ | ❌ | **MISSING** |

### Storage Management (68-69)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 68 | primitiveObjectAt | Object instVarAt: | ✓ (as become) | ❌ | **MISSING** |
| 69 | primitiveObjectAtPut | Object instVarAt:put: | ❌ | ❌ | **MISSING** |

### Class and Instance Creation (70-79)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 70 | primitiveNew | Class new | ✓ | ✓ | Complete |
| 71 | primitiveNewWithArg | Class new: | ✓ | ✓ | Complete |
| 72 | primitiveBecome | Object become: | ✓ (as basicNew) | ✓ | Different |
| 73 | primitiveInstVarAt | Object instVarAt: | ✓ (as basicNewWithArg) | ✓ | Different |
| 74 | primitiveInstVarAtPut | Object instVarAt:put: | ✓ (instanceSize) | ✓ | Different |
| 75 | primitiveHash | Object hash | ✓ | ✓ | Complete |
| 76 | primitiveSmallIntegerHash | SmallInteger hash | ✓ | ✓ | Complete |
| 77 | primitiveAsOop | Object asOop | ✓ (someInstance) | ✓ | Different |
| 78 | primitiveAsObject | Integer asObject | ✓ (nextInstance) | ✓ | Different |
| 79 | primitiveNewMethod | CompiledMethod new | ✓ (newClass) | ✓ (class) | Different |

### Control Primitives (80-89)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 80 | primitiveBlockCopy | BlockContext blockCopy: | ✓ | ✓ | Complete |
| 81 | primitiveValue | BlockContext value | ✓ | ✓ | Complete |
| 82 | primitiveValueWithArgs | BlockContext value:value:... | ✓ | ✓ | Complete |
| 83 | primitivePerform | Object perform: | ✓ | ❌ | **MISSING** |
| 84 | primitivePerformWithArgs | Object perform:with: | ✓ | ❌ | **MISSING** |
| 85 | primitiveSignal | Semaphore signal | ✓ | ❌ | **MISSING** |
| 86 | primitiveWait | Semaphore wait | ✓ | ❌ | **MISSING** |
| 87 | primitiveResume | Process resume | ✓ | ❌ | **MISSING** |
| 88 | primitiveSuspend | Process suspend | ✓ | ❌ | **MISSING** |
| 89 | primitiveFlushCache | Behavior flushCache | ❌ | ❌ | **MISSING** |

### Input/Output (90-109)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 90 | primitiveMousePoint | Sensor mousePoint | ✓ (fileAtPut) | ❌ | **MISSING** |
| 91 | primitiveCursorLocPut | Sensor cursorPoint: | ✓ (fileAt/terminate) | ❌ | **MISSING** |
| 92 | primitiveCursorLink | Cursor link | ✓ (fileSize) | ❌ | **MISSING** |
| 93 | primitiveInputSemaphore | InputState signalSemaphore | ✓ (filePosition) | ❌ | **MISSING** |
| 94 | primitiveSampleInterval | InputState sampleInterval | ✓ (fileSetPosition) | ❌ | **MISSING** |
| 95 | primitiveInputWord | InputState primInputWord | ✓ (directoryLookup) | ❌ | **MISSING** |
| 96 | primitiveCopyBits | BitBlt copyBits | ✓ (consoleOutput) | ❌ | **MISSING** |
| 97 | primitiveSnapshot | SystemDictionary snapshot | ✓ (consoleInput) | ❌ | **MISSING** |
| 98 | primitiveTimeWordsInto | Time timeWordsInto: | ✓ (fileOpen) | ❌ | **MISSING** |
| 99 | primitiveTickWordsInto | Time tickWordsInto: | ✓ (fileClose) | ❌ | **MISSING** |
| 100 | primitiveSignalAtTick | Delay signalAtTick: | ✓ (fileDelete) | ❌ | **MISSING** |
| 101-109 | Reserved I/O | Reserved for I/O | ✓ (101-109 reserved) | ❌ | **MISSING** |

### System Primitives (110-119)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 110 | primitiveIdentical | Object == | ✓ | ✓ | Complete |
| 111 | primitiveClass | Object class | ✓ | ✓ (as notIdentical) | Different |
| 112 | primitiveCoreLeft | SystemDictionary coreLeft | ✓ | ❌ | **MISSING** |
| 113 | primitiveQuit | SystemDictionary quit | ✓ | ❌ | **MISSING** |
| 114 | primitiveExitToDebugger | SystemDictionary exitToDebugger | ✓ | ❌ | **MISSING** |
| 115 | primitiveOopChanged | Object oopChanged | ✓ | ❌ | **MISSING** |
| 116 | primitiveFlushCache | CompiledMethod flushCache | ✓ | ❌ | **MISSING** |
| 117-119 | Reserved | Reserved system primitives | ✓ (117-127 reserved) | ❌ | **MISSING** |

### Private Primitives (128-255)
| # | Name | Blue Book Purpose | SavedInterpreter | New Interpreter | Status |
|---|------|------------------|------------------|-----------------|--------|
| 128 | primitiveSomeInstance | Behavior someInstance | ✓ | ❌ | **MISSING** |
| 129 | primitiveNextInstance | Object nextInstance | ✓ | ❌ | **MISSING** |
| 130 | primitiveBecomeForward | Object becomeForward: | ✓ | ❌ | **MISSING** |
| 131 | primitiveInstanceCount | Behavior instanceCount | ✓ | ❌ | **MISSING** |
| 132-249 | Reserved/Private | Reserved for private use | ❌ | ❌ | N/A |
| 250 | VM specific | Implementation specific | ✓ (writeChar/evaluate) | ❌ | **MISSING** |
| 251 | VM specific | Implementation specific | ✓ (flushOutput/compile) | ❌ | **MISSING** |
| 252 | VM specific | Implementation specific | ✓ (clearConsole) | ❌ | **MISSING** |
| 253 | VM specific | Implementation specific | ✓ (writeString) | ❌ | **MISSING** |
| 254-255 | Reserved | Reserved for VM | ❌ | ❌ | N/A |

## Summary

### Implementation Status

**SavedInterpreter**:
- Implements **69 primitives** total
- Has most essential primitives for basic operation
- Includes I/O, process, and system primitives
- Some primitives are mapped differently than Blue Book spec

**New Interpreter**:
- Implements **38 primitives** total
- Focuses on core arithmetic and object manipulation
- Missing critical categories:
  - Process and semaphore primitives (85-88)
  - I/O primitives (90-100)
  - System primitives (112-116)
  - Stream primitives (65-67)
  - String manipulation (103-106)
  - Control flow (83-84)

### Essential Missing Primitives for Working System

The following primitives are **critical** for a functioning Smalltalk-80 system and are missing from the new interpreter:

1. **Process Control** (Priority: CRITICAL)
   - 85: primitiveSignal (Semaphore signal)
   - 86: primitiveWait (Semaphore wait)
   - 87: primitiveResume (Process resume)
   - 88: primitiveSuspend (Process suspend)

2. **Method Execution** (Priority: HIGH)
   - 83: primitivePerform (Object perform:)
   - 84: primitivePerformWithArgs (Object perform:with:)

3. **Stream Operations** (Priority: HIGH)
   - 65: primitiveNext (Stream next)
   - 66: primitiveNextPut (Stream nextPut:)
   - 67: primitiveAtEnd (Stream atEnd)

4. **String Operations** (Priority: HIGH)
   - 103: primitiveStringReplace
   - 104: primitiveStringHash
   - 105: primitiveStringCompare
   - 106: primitiveAsSymbol

5. **System Management** (Priority: MEDIUM)
   - 112: primitiveCoreLeft (memory left)
   - 113: primitiveQuit (system quit)
   - 114: primitiveExitToDebugger
   - 128: primitiveSomeInstance
   - 129: primitiveNextInstance

6. **I/O Operations** (Priority: MEDIUM)
   - 96: Console/Display output
   - 97: Console input
   - 98-100: File operations

### Recommendations

1. **Immediate Priority**: Implement process and semaphore primitives (85-88) as these are essential for the Smalltalk-80 process scheduler and concurrency model.

2. **High Priority**: Add perform primitives (83-84) for dynamic method invocation, which is fundamental to Smalltalk's reflective capabilities.

3. **Important**: Implement stream primitives (65-67) and string operations (103-106) as these are heavily used by the standard library.

4. **System Integration**: Add basic I/O primitives for console interaction and file operations to enable practical programs.

5. **Compatibility Note**: Several primitives in the new interpreter are mapped to different numbers than the Blue Book spec (e.g., 111 is notIdentical instead of class). This should be corrected for Blue Book compliance.

## File Comparison

### SavedInterpreter Primitive Files:
- ArithmeticPrimitives.cs (1-17)
- ArrayPrimitives.cs (60-68)
- ClassPrimitives.cs (70-79)
- ComparisonPrimitives.cs (3-8, duplicates arithmetic)
- CompilerPrimitives.cs (250-251, custom)
- ControlPrimitives.cs (80-84)
- IOPrimitives.cs (90-109)
- ObjectPrimitives.cs (60-62, 70-71, 75, 110-111)
- ProcessPrimitives.cs (81, 85-88, 91)
- StringPrimitives.cs (103-106)
- SystemPrimitives.cs (75-76, 110-116, 128-131)
- TranscriptPrimitives.cs (250-253, custom)

### New Interpreter Primitive Files:
- ArithmeticPrimitives.cs (1-17)
- BlockPrimitives.cs (80-82)
- ComparisonPrimitives.cs (20, 110-111)
- ObjectPrimitives.cs (60-64, 70-79)
- PrimitiveTable.cs (registration only)

The new interpreter needs to add:
- ProcessPrimitives.cs
- ControlPrimitives.cs (for perform)
- StreamPrimitives.cs
- StringPrimitives.cs
- SystemPrimitives.cs
- IOPrimitives.cs