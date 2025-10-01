# Blue Book Compliance Status

## Overview

This document tracks the compliance of our C# interpreter implementation with the Smalltalk-80 Blue Book specification, specifically regarding object layouts and structure.

## Core Principle

**Smalltalk object layouts are defined in Smalltalk source code, not C# code.**

The C# interpreter should:
1. Operate on generic `SInstance` objects with format-aware access
2. Use `BlueBookLayouts` constants for any required layout knowledge
3. Minimize coupling between C# code and Smalltalk class definitions
4. Isolate layout knowledge to cold start code and critical primitives

## BlueBookLayouts.cs - Single Source of Truth

**File**: `Core/BlueBook/BlueBookLayouts.cs`

**Status**: ✅ VERIFIED CORRECT against Smalltalk-80.st source

This file contains layout constants for all critical Blue Book classes. Each layout has been verified against the actual Smalltalk-80.st source file.

### Verified Layouts

| Class | Status | Fixed Fields | Variable Part | Source Line |
|-------|--------|--------------|---------------|-------------|
| InstructionStream | ✅ | 2 (sender, pc) | none | 22372-22376 |
| ContextPart | ✅ | 3 (sender, pc, stackp) | none | 11979-11983 |
| MethodContext | ✅ | 6 (sender, pc, stackp, method, receiverMap, receiver) | temps+stack | 25946-25950 |
| BlockContext | ✅ | 6 (sender, pc, stackp, nargs, startpc, home) | copied+stack | 4976-4980 |
| CompiledMethod | ✅ | 0 | bytes (header+literals+bytecodes) | 11156-11160 |
| Process | ✅ | 4 (nextLink, suspendedContext, priority, myList) | none | 32060-32064 |
| Semaphore | ✅ | 3 (firstLink, lastLink, excessSignals) | none | 34419-34423 |
| Dictionary | ✅ | 1 (tally) | associations | 14778-14782 |
| IdentityDictionary | ✅ | 2 (tally, valueArray) | keys | 21402-21407 |
| Association | ✅ | 2 (key, value) | none | 2007-2011 |
| Symbol | ✅ | 0 | bytes | 37518-37522 |
| Behavior | ✅ | 4 (superclass, methodDict, format, subclasses) | none | 2121-2125 |
| ClassDescription | ✅ | 6 (superclass, methodDict, format, subclasses, instanceVariables, organization) | none | 9743-9747 |
| Class | ✅ | 9 (inherited 6 + name, classPool, sharedPools) | none | 9055-9059 |
| Metaclass | ✅ | 7 (inherited 6 + thisClass) | none | 25572-25576 |
| Array | ✅ | 0 | pointers | ~1964 |
| Character | ✅ | 1 (value) | none | ~7741 |
| String | ✅ | 0 | bytes | ~37374 |
| ByteArray | ✅ | 0 | bytes | ~4880 |
| Float | ✅ | 0 | words | ~17982 |
| Link | ✅ | 1 (nextLink) | none | 24161-24165 |
| LinkedList | ✅ | 2 (firstLink, lastLink) | none | 24185-24189 |
| LookupKey | ✅ | 1 (key) | none | 24672-24676 |
| Set | ✅ | 1 (tally) | elements | 34793-34797 |

## C# Classes Requiring Blue Book Compliance

### ✅ Compliant

| C# Class | Status | Notes |
|----------|--------|-------|
| CharacterTableBuilder | ✅ | Uses BlueBookLayouts.Character constants |
| SInstance | ✅ | Generic object, format-agnostic |
| ObjectFormat enum | ✅ | Matches Blue Book format types |

### ⚠️ Non-Compliant (Needs Fixing)

| C# Class | Issue | Correct Layout | Current Layout |
|----------|-------|----------------|----------------|
| SMethodContext | Wrong field count | 6 fields | 5 fields (missing receiverMap) |
| SMethodContext | Wrong indices | method@3, receiverMap@4, receiver@5 | method@3, receiver@4 |
| SBlockContext | Not verified | 6 fields per Blue Book | Need to verify |
| SCompiledMethod | Complex structure | Need to verify header encoding | Need to verify |
| SProcess | Not verified | 4 fields per Blue Book | Need to verify |
| SSemaphore | Not verified | 3 fields per Blue Book | Need to verify |
| SDictionary | Not verified | 1 field + variable | Need to verify |

## Action Items

### High Priority

1. ⚠️ **Fix SMethodContext to match Blue Book layout**
   - Add receiverMap field at index 4
   - Move receiver from index 4 to index 5
   - Update all code that creates/accesses MethodContext

2. ⚠️ **Verify and fix SBlockContext**
   - Check against BlueBookLayouts.BlockContext
   - Ensure 6 fixed fields (not 5)

3. ⚠️ **Verify SCompiledMethod structure**
   - Check header encoding format
   - Verify literals vs bytecodes layout

### Medium Priority

4. 📝 **Document which primitives need layout knowledge**
   - List all primitives that access specific fields
   - Ensure they use BlueBookLayouts constants

5. 📝 **Update cold start code**
   - Use BlueBookLayouts everywhere
   - Document which layouts cold start must know

6. 📝 **Consider removing specialized C# classes**
   - Evaluate if SMethodContext, etc. should be removed
   - Replace with generic SInstance + BlueBookLayouts

### Low Priority

7. 🔍 **Verify remaining classes**
   - SProcess, SSemaphore, SDictionary
   - Check field counts match Blue Book

## Testing Strategy

1. Create unit tests that verify:
   - Each SInstance created during cold start has correct field count
   - Field indices match BlueBookLayouts constants
   - No hardcoded indices exist outside BlueBookLayouts

2. Create compliance tests:
   - Parse Smalltalk-80.st to extract class definitions
   - Compare against BlueBookLayouts.cs
   - Fail if any mismatch

## References

- **Smalltalk-80 Source**: `Smalltalk-80 Source/Smalltalk-80.st`
- **Blue Book**: "Smalltalk-80: The Language and its Implementation" by Adele Goldberg and David Robson
- **BlueBookLayouts**: `Smalltalk80.Interpreter/Core/BlueBook/BlueBookLayouts.cs`