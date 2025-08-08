# Headless Standard Library for Smalltalk-80

## Overview

The HeadlessStandardLibrary.st is a curated subset of the Smalltalk-80 system image designed for headless (non-graphical) environments. It contains all the essential computational classes while excluding graphics, UI, and platform-specific code.

## Current Status

✅ **Implemented**: The system now defaults to loading `HeadlessStandardLibrary.st` instead of `StandardLibrary.st`
✅ **Fallback**: If HeadlessStandardLibrary.st is not found, the system falls back to StandardLibrary.st
✅ **Bootstrap**: Uses the three-phase bootstrap architecture:
   1. GeneratedBootstrap.cs - 30 core classes baked into the interpreter
   2. SecondStageBootstrap.st - Class creation infrastructure
   3. HeadlessStandardLibrary.st - Extended standard library

## Library Contents

### Core Classes (From System Image)
The HeadlessStandardLibrary.st currently contains:

**Object System:**
- `Object` - Root class with full method protocol including:
  - initialize-release
  - accessing (at:, at:put:, basicAt:, basicAt:put:, size)
  - testing (isNil, notNil, isInteger, isNumber, etc.)
  - comparing (=, ==, ~=, ~~, hash)
  - copying (copy, shallowCopy, deepCopy)
  - dependents access (addDependent:, removeDependent:, etc.)
  - updating (changed, changed:, update:)
  - printing (printOn:, printString, storeOn:)
  - class membership (class, isKindOf:, isMemberOf:, respondsTo:)
  - message handling (perform:, perform:with:, etc.)
  - error handling (error:, doesNotUnderstand:, halt, etc.)
  - system primitives

- `UndefinedObject` - The nil object

**Boolean Hierarchy:**
- `Boolean` - Abstract boolean class
- `True` - The true singleton
- `False` - The false singleton

**Magnitude Hierarchy:**
- `Magnitude` - Abstract class for comparable objects
  - Comparison methods (<, <=, >, >=, =, between:and:)
  - min:, max: methods

## How the System Works

### VM Initialization
```csharp
// The VirtualMachine class now:
1. Loads GeneratedBootstrap (instant 30 classes)
2. Loads SecondStageBootstrap (class creation methods)
3. Attempts to load HeadlessStandardLibrary.st
4. Falls back to StandardLibrary.st if needed
```

### Loading Priority
1. **HeadlessStandardLibrary.st** - Preferred for headless systems
2. **StandardLibrary.st** - Fallback for compatibility

### File Locations
All library files are expected in the root project directory:
- `HeadlessStandardLibrary.st` - Headless library
- `StandardLibrary.st` - Original library (fallback)
- `SecondStageBootstrap.st` - Class creation infrastructure
- `Bootstrap.st` - Source for GeneratedBootstrap.cs

## Classes to be Added

The following classes from the system image should be added to make a complete headless system:

### Essential Missing Classes
- **Numbers**: Integer, SmallInteger, Float, Fraction, LargePositiveInteger, LargeNegativeInteger
- **Strings**: Character, String, Symbol
- **Collections**: Collection, Array, OrderedCollection, Set, Dictionary
- **Streams**: Stream, ReadStream, WriteStream, FileStream
- **Date/Time**: Date, Time
- **Execution**: CompiledMethod, BlockContext, MethodContext
- **Processes**: Process, Semaphore, Delay

### Optional but Useful
- **Compiler**: Scanner, Parser, Compiler (for runtime code compilation)
- **Files**: File, FileDirectory (modern file system)
- **Changes**: ChangeSet (for tracking code changes)
- **Random**: Random number generation

## Usage

### Running with Headless Library
```bash
# The system automatically uses HeadlessStandardLibrary.st
dotnet run --project Smalltalk80/Smalltalk80.csproj

# Force no standard library (bootstrap only)
dotnet run --project Smalltalk80/Smalltalk80.csproj -- --no-stdlib
```

### Testing
```smalltalk
"Basic tests that work with current headless library"
3 + 4.
true ifTrue: ['yes'] ifFalse: ['no'].
nil isNil.
Object new class name.
```

## Benefits of Headless Library

1. **Smaller footprint** - No graphics/UI code
2. **Server-ready** - Perfect for headless environments
3. **Faster loading** - Fewer classes to initialize
4. **Cleaner dependencies** - No platform-specific code
5. **Maintainable** - Easier to understand and modify

## Extending the Library

To add more classes from the system image:

1. Identify non-graphical classes in `Smalltalk80/Smalltalk-80 Source/Smalltalk-80.st`
2. Extract class definition and all methods
3. Append to HeadlessStandardLibrary.st
4. Ensure dependencies are satisfied

## Technical Notes

- The library uses underscore (_) for assignment, not :=
- All methods must be in bang-delimited format
- Class definitions use the standard Smalltalk fileout format
- Graphics-dependent methods have been adapted or removed

## Future Work

1. Complete extraction of all computational classes from system image
2. Add comprehensive Number hierarchy
3. Add complete Collection hierarchy  
4. Add Stream classes for I/O
5. Add basic Compiler classes for runtime compilation
6. Create automated extraction tool for system image classes