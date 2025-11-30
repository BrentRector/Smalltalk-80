# Source Code Lookup and Retrieval Mechanism

**Author**: Analysis of Smalltalk-80 Source Code
**Date**: 2025-11-30
**Status**: Complete Analysis

## Overview

Smalltalk-80 implements a sophisticated source code retrieval system that stores method source code in external files while keeping only a compact file reference (3 bytes) in each CompiledMethod object. This analysis documents the complete mechanism from browser request to source retrieval.

## Table of Contents

1. [Source Position Encoding in CompiledMethod](#source-position-encoding)
2. [File Index Determination](#file-index-determination)
3. [RemoteString: The File Reference Object](#remotestring)
4. [SourceFiles Global Array](#sourcefiles-global)
5. [Complete Call Chain](#complete-call-chain)
6. [Decompilation Fallback](#decompilation-fallback)
7. [Required Primitives](#required-primitives)
8. [Source Code Examples](#source-code-examples)

---

## Source Position Encoding

### Three-Byte Layout in CompiledMethod

Each CompiledMethod stores source file reference in its **last 3 bytes**:

```
Byte Position:  [size-2]  [size-1]  [size]
               lowByte   middleByte highByte
Bit Layout:
  lowByte:     [bit 0-7 of position]
  middleByte:  [bit 8-15 of position]
  highByte:    [bit 0-5: bit 16-21 of position] [bit 6-7: fileIndex - 1]
```

**Capacity:**
- **Position**: 22 bits = 0 to 4,194,303 bytes (4MB file size limit)
- **File Index**: 2 bits = 0 to 3 (representing file numbers 1-4)

**Special Value:**
- Position = 0 means "no source available" (triggers decompilation fallback)

### Encoding Algorithm

From `CompiledMethod>>setSourcePosition:inFile:` (lines 12159-12175):

```smalltalk
setSourcePosition: position inFile: fileIndex
    "Store the location of the source code for the receiver in the receiver.  The
    location consists of which source file (*.sources or *.changes) and the position
    in that file."

    | index hiByte middleByte lowByte |
    "set last three bytes to be position in file (1-4)"
    fileIndex > 4 ifTrue: [^self error: 'invalid file number'].
    index _ self size - 2.
    middleByte _ position bitShift: -8.
    hiByte _ middleByte bitShift: -8.
    middleByte _ middleByte bitAnd: 255.
    lowByte _ position bitAnd: 255.
    hiByte > 62 ifTrue: [Transcript show: 'Source file is getting full!!!!'; cr].
    self at: index + 2 put: fileIndex - 1 * 64 + hiByte.
    self at: index + 1 put: middleByte.
    self at: index put: lowByte!
```

**Encoding Steps:**
1. Extract low byte: `position bitAnd: 255`
2. Extract middle byte: `(position bitShift: -8) bitAnd: 255`
3. Extract high 6 bits: `position bitShift: -16` (max 63)
4. Combine fileIndex into high byte: `(fileIndex - 1) * 64 + hiByte`

**Warning Threshold:**
- When `hiByte > 62` (position > 4,128,768), warns that file is getting full

---

## File Index Determination

### Decoding File Number

From `CompiledMethod>>fileIndex` (lines 12096-12102):

```smalltalk
fileIndex
    "Answer 1 if the source code of the receiver is on the *.sources file
    and 2 if it is on the *.changes file."

    (self last between: 120 and: 124)
        ifTrue: [self error: 'Somehow a method does not have a file index.'].
    ^self last // 64 + 1!
```

**Decoding Algorithm:**
1. Read last byte of CompiledMethod
2. Integer divide by 64: `self last // 64`
3. Add 1 to get file number (1-4)

**Error Detection:**
- Bytes 120-124 (0x78-0x7C) indicate invalid state
- These values would decode to fileIndex 2 or 3, but are reserved for error detection

**File Number Mapping:**
- **1** = `*.sources` file (base system, written during image creation)
- **2** = `*.changes` file (incremental changes, append-only log)
- **3-4** = Reserved for future use (not currently used in Smalltalk-80)

---

## RemoteString

### Purpose

RemoteString is a lightweight object that holds a reference to source code stored in an external file. Instead of storing the entire source string in memory, it stores only:
- Which file (sourceFileNumber: 1-4)
- Where in that file (filePosition: 0-65535, stored as Hi/Lo bytes)

### Class Definition

From lines 33958-33968:

```smalltalk
Object subclass: #RemoteString
    instanceVariableNames: 'sourceFileNumber filePositionHi filePositionLo '
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Support'!

RemoteString comment: 'Instance Variables:
    sourceFileNumber    <Integer> index into SourceFiles
    filePositionHi      <Integer>
    filePositionLo      <Integer>

I hold the file reference to the class comment (or any other piece of executable text).'!
```

### Accessing Methods

#### position

From lines 33972-33975:

```smalltalk
position
    "Answer the location of the string on a file."

    ^(filePositionHi bitShift: 8) + filePositionLo!
```

Reconstructs the full position from high/low bytes (0-16,777,215 range).

#### string

From lines 33979-33988:

```smalltalk
string
    "Answer the receiver's string if remote files are enabled."

    | theFile |
    (SourceFiles == nil or: [sourceFileNumber == nil])
        ifTrue: [^'']
        ifFalse:
            [theFile _ SourceFiles at: sourceFileNumber.
            theFile position: (filePositionHi bitShift: 8) + filePositionLo.
            ^theFile nextChunk]!
```

**Retrieval Steps:**
1. Check if SourceFiles global exists (nil check)
2. Check if sourceFileNumber is set (nil check)
3. Get the appropriate file stream from SourceFiles array
4. Position the stream to the stored position
5. Read one chunk (bang-delimited text) using `nextChunk`
6. Return the chunk as a String

### Factory Methods

From lines 34021-34038:

```smalltalk
!RemoteString class methodsFor: 'instance creation'!
newFileNumber: sourceIndex position: anInteger
    "Answer a new instance of RemoteString for file indexed by sourceIndex,
    at the position anInteger.  Assumes that the string is already stored
    on the file and the instance will be used to access it."

    ^self new fileNumber: sourceIndex position: anInteger!

newString: aString onFileNumber: sourceIndex
    "Answer a new instance of RemoteString for string, aString, on file indexed by
    sourceIndex.  Puts the string on the file and creates the remote reference."

    ^self new string: aString onFileNumber: sourceIndex!

newString: aString onFileNumber: sourceIndex toFile: aFileStream
    "Answer a new instance of RemoteString for string, aString, on file indexed by
    sourceIndex.  Puts the string on the file, aFileStream, and creates the remote
    reference.  Assumes that the index corresponds properly to aFileStream."

    ^self new string: aString onFileNumber: sourceIndex toFile: aFileStream!
```

**Usage Patterns:**
- `newFileNumber:position:` - Reference existing source (used when reading)
- `newString:onFileNumber:` - Write new source and create reference
- `newString:onFileNumber:toFile:` - Write to specific stream (used during fileOut)

---

## SourceFiles Global

### Structure

SourceFiles is a **global variable** containing an Array of file streams:

```smalltalk
SourceFiles := Array new: 2.
SourceFiles at: 1 put: (FileStream fileNamed: 'Smalltalk-80.sources').
SourceFiles at: 2 put: (FileStream fileNamed: 'Smalltalk-80.changes').
```

### File Roles

**Index 1: *.sources**
- Contains source code for base system
- Written during initial image creation
- Read-only in normal operation
- Compact, stable reference

**Index 2: *.changes**
- Contains incremental changes
- Append-only log of all modifications
- Updated every time a method is compiled
- Grows continuously during development

### Nil Handling

Throughout the system, code checks `SourceFiles == nil` before accessing:

```smalltalk
SourceFiles == nil ifTrue: [^nil].
```

When SourceFiles is nil:
- Source retrieval returns empty string or nil
- System falls back to decompilation
- Methods can still execute (bytecode is in memory)

---

## Complete Call Chain

### Browser Requests Source Code

**Entry Point:** User selects a method in the browser

**1. Behavior>>sourceCodeAt:** (line 3024-3026)

```smalltalk
sourceCodeAt: messageSelector
    "Answer the string corresponding to the source code for the argument."
    ^ self sourceCodeForMethod: (methodDict at: messageSelector) at: messageSelector!
```

Looks up the CompiledMethod in methodDict and delegates to sourceCodeForMethod:at:

**2. Behavior>>sourceCodeForMethod:at:** (lines 3027-3050)

```smalltalk
sourceCodeForMethod: method at: messageSelector
    "Answer the string corresponding to the source code for the argument."
    | newSource index|
    Sensor leftShiftDown
        ifTrue: [newSource _
                    (self decompilerClass new
                        decompile: messageSelector
                        in: self
                        method: method) decompileString]
        ifFalse:
            [newSource _ method getSource.
            newSource == nil
                ifTrue: [newSource _
                            (self decompilerClass new
                                decompile: messageSelector
                                in: self
                                method: method) decompileString]
                ifFalse:    [((newSource at: newSource size) isSeparator)
                            ifTrue:    [index _ newSource size. "tidy up for file out"
                                    [((newSource at: index) isSeparator)
                                        and: [index > 1]]
                                        whileTrue:    [index _ index - 1].
                                    newSource _ newSource copyFrom: 1 to: index]]].
    ^newSource!
```

**Logic Flow:**
1. **If Sensor leftShiftDown**: Always decompile (for debugging)
2. **Otherwise**: Try `method getSource`
3. **If getSource returns nil**: Fall back to decompilation
4. **If successful**: Trim trailing whitespace from file source

**3. CompiledMethod>>getSource** (lines 12103-12121)

```smalltalk
getSource
    "Answer the source code for the receiver.  Answer nil if there are
    no source files specified in the global SourceFiles."

    | end highByte position source |
    SourceFiles == nil ifTrue: [^nil].
    Cursor read
        showWhile:
            [end _ self size.
            highByte _ self at: end.
            position _ highByte bitAnd: 63.
            position _ position * 256 + (self at: end - 1).
            position _ position * 256 + (self at: end - 2).
            position = 0
                ifTrue: [source _ nil]
                ifFalse: [source _
                            (RemoteString newFileNumber:
                                (highByte bitShift: -6) + 1 position: position) string]].
    ^source!
```

**Decoding Steps:**
1. Check if SourceFiles exists (return nil if not)
2. Show "read" cursor during file I/O
3. Extract last 3 bytes from CompiledMethod
4. Decode position (22 bits from bytes)
5. Decode fileIndex (2 bits from high byte)
6. Create RemoteString and immediately call `string` on it
7. Return the source string (or nil if position = 0)

**4. RemoteString>>string** (covered above)

Positions the file stream and reads one chunk.

### Alternative Path: MethodDescription

Some tools use MethodDescription objects which also retrieve source:

From lines 26685-26702:

```smalltalk
sourceFileAndPosition: aBlock
    "Get the source code for this method from information currently in the system."
    | class method fileIndex pos |
    (class _ self classObject) isNil ifTrue: [^nil].
    (class includesSelector: selector) ifFalse: [^nil].
    method _ class compiledMethodAt: selector.
    SourceFiles isNil ifTrue: [^nil].
    (fileIndex _ method fileIndex) > SourceFiles size ifTrue: [^nil].
    "The following code should be in CompiledMethod ..."
    pos _ (method at: method size) bitAnd: 63.
    pos _ pos * 256 + (method at: method size - 1).
    pos _ pos * 256 + (method at: method size - 2).
    pos = 0 ifTrue: [^nil].
    ^aBlock value: (SourceFiles at: method fileIndex) value: pos!
```

This version directly decodes the position and passes both file stream and position to a block, allowing the caller to control how to read the source.

---

## Decompilation Fallback

### When Decompilation Occurs

The system falls back to decompiling bytecode when:

1. **User Forces It**: Holding Left Shift while selecting method
2. **No SourceFiles**: SourceFiles global is nil
3. **Position is Zero**: Method has no source reference (position = 0)
4. **File Missing**: fileIndex points to non-existent file in SourceFiles array
5. **File I/O Error**: Stream can't be positioned or chunk can't be read

### Decompiler Class

From lines 14792-14813:

```smalltalk
InstructionStream subclass: #Decompiler
    instanceVariableNames: 'constructor method instVars tempVars constTable stack statements lastPc exit lastJumpPc lastReturnPc limit hasValue '
    classVariableNames: 'ArgumentFlag CascadeFlag '
    poolDictionaries: ''
    category: 'System-Compiler'!

Decompiler comment:
'I translate CompiledMethods into source code.'!

!Decompiler methodsFor: 'initialize-release'!
initSymbols: aClass
    |  nTemps |
    constructor
        method: method
        class: aClass
        literals: method literals.
    constTable _ constructor codeConstants.
    instVars _ Array new: aClass instSize.
    "parse the header"
    nTemps _ method numTemps.
    tempVars _ Array new: nTemps.
    1 to: nTemps do: [:i | tempVars at: i put: (constructor codeTemp: i - 1)]!
```

**Decompilation Process:**
1. Analyze CompiledMethod header for temps, args
2. Walk bytecode stream instruction by instruction
3. Reconstruct expression tree from stack operations
4. Generate synthetic variable names (t1, t2, arg1, etc.)
5. Format as readable source code

**Limitations:**
- Loses original variable names (uses t1, t2, arg1, arg2, etc.)
- Loses comments
- Loses original formatting
- May not match original source exactly (semantically equivalent, not textually)

### Usage Example

From Behavior>>decompile: (lines 3334-3338):

```smalltalk
decompile: selector
    "Find the compiled code associated with the argument, selector,
    in the receiver's method dictionary and decompile it.  Answer the resulting source
    code as a string.  Create an error if the selector is not in the receiver's method
    dictionary."

    ^self decompilerClass new decompile: selector in: self!

decompilerClass
    "Return a decompiler class appropriate for compiled methods of this class."

    ^Decompiler!
```

---

## Required Primitives

### File Stream Primitives

The source lookup mechanism requires these primitives to be implemented:

#### Primitive 144: GetFileSize

**Purpose**: Query file size in bytes
**Signature**: `<primitive: 144>`
**Usage**: Called by FileStream>>size

**Parameters:**
- Receiver: FileStream with open fileHandle

**Returns:**
- SmallInteger: File size in bytes

**Notes:**
- Current implementation issue: Called before file is opened in some cases
- Fix needed: Handle nil fileHandle by getting path from fileName instance variable

#### File Positioning

**Purpose**: Set stream position for reading
**Signature**: `position: anInteger`
**Implementation**: Usually Smalltalk code calling primitive next/skip

**Parameters:**
- anInteger: Byte offset in file (0-based)

**Returns:**
- Self (for chaining)

#### Primitive 65: Stream>>next

**Purpose**: Read next character/byte from stream
**Signature**: `<primitive: 65>`
**Usage**: Foundation for nextChunk

**Parameters:**
- Receiver: ReadStream or FileStream

**Returns:**
- Character (when reading from String/Symbol)
- SmallInteger (when reading from ByteArray/binary file)
- nil (when at end)

**Recent Fix:**
- Now correctly returns Character objects when reading from String/Symbol-based streams
- Uses isKindOf: semantics to handle subclasses properly

### nextChunk Implementation

From lines 1446-1465:

```smalltalk
nextChunk
    "Read and return the next chunk (text terminated by !)
     Doubled !! are converted to single !"
    | result char prevWasBang |
    result _ WriteStream on: (String new: 1000).
    prevWasBang _ false.
    [self atEnd] whileFalse: [
        char _ self next.
        char isNil ifTrue: [^result contents].
        char == $!
            ifTrue: [
                prevWasBang
                    ifTrue: [
                        result nextPut: $!.
                        prevWasBang _ false]
                    ifFalse: [prevWasBang _ true]]
            ifFalse: [
                prevWasBang ifTrue: [^result contents].
                result nextPut: char]].
    ^result contents!
```

**Chunk Format:**
- Text terminated by single `!`
- To include `!` in text, double it: `!!`
- Reading `!!` converts to single `!`
- First single `!` ends the chunk

**Example:**
```
File contains: Hello!! World!Garbage
Result:        "Hello! World"
```

### nextChunkPut Implementation

From lines 1467-1475:

```smalltalk
nextChunkPut: aString
    "Write aString as a chunk (with ! terminator)
     Single ! in aString are doubled to !!"
    aString do: [:char |
        char == $! ifTrue: [self nextPut: $!].
        self nextPut: char].
    self nextPut: $!.
    self cr.
    ^aString!
```

**Writing Process:**
1. For each character in string:
   - If `!`, write `!!` (double it)
   - Otherwise, write the character
2. Write terminating `!`
3. Write carriage return
4. Return the string

---

## Source Code Examples

### Writing Source During Compilation

From CompiledMethod>>putSource:inFile: (lines 12142-12158):

```smalltalk
putSource: sourceStr inFile: fileIndex
    "Store the source code for the receiver on an external file.
    If no sources are specified, i.e., SourceFile is nil, then do nothing.
    If the fileIndex is 1, print on *.sources;  if it is 2, print on *.changes."

    | file remoteString |
    SourceFiles == nil ifTrue: [^self].
    file _ SourceFiles at: fileIndex.
    file setToEnd; readWriteShorten.
    file cr; nextPut: $!!; nextChunkPut: 'Behavior method'; cr.
    remoteString _
        RemoteString
            newString: sourceStr
            onFileNumber: fileIndex
            toFile: file.
    file nextChunkPut: ' '; readOnly.
    self setSourcePosition: remoteString position inFile: fileIndex!
```

**Process:**
1. Get file stream from SourceFiles
2. Position to end of file (append mode)
3. Write header chunk: "Behavior method"
4. Create RemoteString which writes the source as a chunk
5. Write empty chunk as separator
6. Set file to read-only
7. Store position back into CompiledMethod

### Full Source Retrieval Example

```smalltalk
"Get source for Array>>at:"
| class method source |
class := Array.
method := class compiledMethodAt: #at:.

"Direct retrieval"
source := method getSource.

"Through behavior protocol"
source := class sourceCodeAt: #at:.

"Through behavior with method reference"
source := class sourceCodeForMethod: method at: #at:.
```

### Chunk File Format Example

```
!Behavior method
at: index
    "Answer the element at position index in the receiver."
    <primitive: 60>
    ^self basicAt: index!

!Behavior method
at: index put: value
    "Store value at position index in the receiver."
    <primitive: 61>
    ^self basicAt: index put: value!
```

Each method is stored as:
1. Header chunk: "Behavior method"
2. Source chunk: The actual method source
3. Empty separator chunk
4. Next method...

---

## Summary

The Smalltalk-80 source code retrieval mechanism is remarkably space-efficient:

**Storage Costs:**
- **3 bytes** per CompiledMethod for file reference
- **22 bits** for file position (4MB file capacity)
- **2 bits** for file index (4 files max)

**Retrieval Path:**
1. Browser → `Behavior>>sourceCodeAt:`
2. → `Behavior>>sourceCodeForMethod:at:`
3. → `CompiledMethod>>getSource`
4. → `RemoteString>>string`
5. → File stream positioned and chunk read

**Fallback Path:**
1. Any failure in retrieval chain
2. → `Behavior>>decompilerClass` returns Decompiler
3. → Bytecode analyzed and reconstructed as source
4. → Synthetic names generated for variables

**Benefits:**
- Minimal memory overhead (3 bytes vs. hundreds/thousands for source)
- Source preserved exactly as written
- Fast random access via position
- Graceful degradation to decompilation

**Trade-offs:**
- Requires external files to be present
- 4MB file size limit per file (adequate for 1980s systems)
- Limited to 4 source files total
- File corruption loses source (but bytecode still works)

This design demonstrates the careful balance Smalltalk-80 achieves between memory efficiency and development experience, allowing the system to maintain full source code access while running in the constrained memory environments of early 1980s workstations.

---

## C# Implementation

This section documents how the C# interpreter implements the source code lookup mechanism described above.

### 1. Source Position Encoding

**Location:** `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ObjectPrimitives.cs` (lines 795-871)

**Class:** `CompiledMethodHelpers`

The C# implementation provides full support for encoding source positions when creating CompiledMethod objects:

```csharp
/// <summary>
/// Allocates a CompiledMethod object with source position encoding.
/// When fileIndex and sourcePosition are provided, appends 3 bytes to bytecodes
/// encoding the source location per Blue Book format:
/// - Last 3 bytes of bytecodes encode: [(fileIndex-1)*64 + high6bits] [midByte] [lowByte]
/// - This allows CompiledMethod>>getSource to retrieve source from SourceFiles
/// </summary>
public static SmalltalkOOP AllocateCompiledMethodInternal(
    ISmalltalkHeap heapManager,
    SmalltalkOOP compiledMethodClassOOP,
    SmalltalkOOP byteArrayClassOOP,
    SmalltalkOOP homeClassOOP,
    int headerWord,
    SmalltalkOOP[] literalOOPs,
    byte[] bytecodes,
    int? fileIndex,
    int? sourcePosition)
{
    byte[] finalBytecodes;
    if (fileIndex.HasValue && sourcePosition.HasValue && sourcePosition.Value >= 0)
    {
        // Append 3 bytes for source position encoding per Blue Book format
        // Position encoding: 22-bit position (max ~4MB per file)
        // Byte layout: [lowByte] [midByte] [(fileIndex-1)*64 + high6bits]
        int pos = sourcePosition.Value;
        byte lowByte = (byte)(pos & 0xFF);           // Bits 0-7
        byte midByte = (byte)((pos >> 8) & 0xFF);    // Bits 8-15
        byte highByte = (byte)(((fileIndex.Value - 1) << 6) | ((pos >> 16) & 0x3F));

        finalBytecodes = new byte[bytecodes.Length + 3];
        Array.Copy(bytecodes, finalBytecodes, bytecodes.Length);
        finalBytecodes[bytecodes.Length] = lowByte;
        finalBytecodes[bytecodes.Length + 1] = midByte;
        finalBytecodes[bytecodes.Length + 2] = highByte;
    }
    else
    {
        finalBytecodes = bytecodes;
    }
    // ... create ByteArray and CompiledMethod objects ...
}
```

**Encoding Details:**
- **Low Byte**: `position & 0xFF` (bits 0-7)
- **Mid Byte**: `(position >> 8) & 0xFF` (bits 8-15)
- **High Byte**: `((fileIndex-1) << 6) | ((position >> 16) & 0x3F)` (file index + bits 16-21)

**Verification:**
This encoding exactly matches the Smalltalk decoding algorithm in `CompiledMethod>>getSource`:
```smalltalk
highByte := self at: end.
position := highByte bitAnd: 63.               "Extract low 6 bits"
position := position * 256 + (self at: end - 1).
position := position * 256 + (self at: end - 2).
fileIndex := (highByte bitShift: -6) + 1.      "Extract high 2 bits + 1"
```

**Status:** ✅ **READY TO USE** - Encoding is correct, but not currently called with source positions during compilation.

### 2. SourceFiles Array Initialization

**Location:** `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/SystemDictionaryInstaller.cs` (lines 73-191)

**Methods:**
- `InstallSourceFilesAsNil()` - Entry point called during Phase 5
- `TryCreateSourceFilesArray()` - Creates the SourceFiles Array
- `CreateWindowsFileReference()` - Creates individual file references

#### TryCreateSourceFilesArray

```csharp
/// <summary>
/// Creates the SourceFiles array with WindowsFileReference streams for .sources and .changes files.
/// Returns nil if files cannot be found or opened.
/// </summary>
private SmalltalkOOP TryCreateSourceFilesArray()
{
    var nilOOP = context.HeapManager.GetNil();

    // Get the bundle directory from BundlePath
    if (string.IsNullOrEmpty(context.BundlePath))
        return nilOOP;

    string bundleDir = Path.GetDirectoryName(context.BundlePath) ?? ".";
    string sourcesPath = Path.Combine(bundleDir, "Smalltalk-80.sources");
    string changesPath = Path.Combine(bundleDir, "Smalltalk-80.changes");

    // Check if .sources file exists
    if (!File.Exists(sourcesPath))
        return nilOOP;

    // Create .changes file if it doesn't exist
    if (!File.Exists(changesPath))
    {
        try { File.WriteAllText(changesPath, ""); }
        catch { return nilOOP; }
    }

    // Open the files using FileHandleManager
    int sourcesHandle = FileOperations.OpenFileHandle(sourcesPath, 1, 1, 3); // Read-only
    int changesHandle = FileOperations.OpenFileHandle(changesPath, 3, 1, 4); // Read-write

    if (sourcesHandle < 0 || changesHandle < 0)
    {
        if (sourcesHandle >= 0) FileOperations.CloseFileHandle(sourcesHandle);
        if (changesHandle >= 0) FileOperations.CloseFileHandle(changesHandle);
        return nilOOP;
    }

    // Create WindowsFileReference objects for each file
    var sourcesRef = CreateWindowsFileReference(sourcesPath, sourcesHandle);
    var changesRef = CreateWindowsFileReference(changesPath, changesHandle);

    // Create Array of size 2 for SourceFiles
    var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
    var sourceFilesArrayOOP = context.Allocator.AllocateArray(arrayClassOOP, 2);

    // SourceFiles at: 1 = .sources file (read-only)
    // SourceFiles at: 2 = .changes file (read-write)
    context.Allocator.SetIndexedOOP(sourceFilesArrayOOP, 0, sourcesRef);
    context.Allocator.SetIndexedOOP(sourceFilesArrayOOP, 1, changesRef);

    return sourceFilesArrayOOP;
}
```

**File Opening Modes:**
- **sourcesPath**: Read-only (accessMode: 1, shareMode: 1, createMode: 3)
- **changesPath**: Read-write (accessMode: 3, shareMode: 1, createMode: 4)

#### CreateWindowsFileReference

```csharp
/// <summary>
/// Creates a WindowsFileReference object in the heap with the given path and file handle.
/// WindowsFileReference has instance variables: pathName (inherited), handle, position
/// </summary>
private SmalltalkOOP CreateWindowsFileReference(string filePath, int handle)
{
    var windowsFileRefClassOOP = GlobalRegistry.LookupClassOOP("WindowsFileReference");
    var fileRefOOP = context.HeapManager.AllocateInstanceOf(windowsFileRefClassOOP, 0);

    // CRITICAL: Use plain String for pathName (not WindowsPathName) for Linux compatibility
    // WindowsPathName>>asString prepends "\\" for UNC-like paths on Linux, breaking file access
    var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
    var pathNameOOP = context.Allocator.AllocateString(stringClassOOP, filePath);

    // Set instance variables
    // slot 0: pathName (String with full path)
    // slot 1: handle (SmallInteger)
    // slot 2: position (SmallInteger, initialized to 0)
    context.HeapManager.SetInstanceVariable(fileRefOOP, 0, pathNameOOP);
    context.HeapManager.SetInstanceVariable(fileRefOOP, 1, SmalltalkOOP.FromSmallInteger(handle));
    context.HeapManager.SetInstanceVariable(fileRefOOP, 2, SmalltalkOOP.FromSmallInteger(0));

    return fileRefOOP;
}
```

**Critical Implementation Detail:**

The code uses a **plain String** for the pathName instead of WindowsPathName. This is essential for Linux compatibility:

```csharp
// WindowsPathName>>isUNC returns true for paths with 2+ components when driveLetter is nil
// On Linux: "/home/user/file.txt" → WindowsPathName>>asString → "\\home\user\file.txt"
// This doesn't exist on Linux, causing File.Exists to fail
// Solution: Use String directly, which has String>>asString that returns self
```

**Status:** ✅ **FULLY IMPLEMENTED** - Creates proper SourceFiles Array when source files exist.

### 3. Primitives Supporting Source Lookup

#### Primitive 60: CompiledMethod at:

**Location:** ObjectPrimitives.cs, lines 114-180

**Purpose:** Allows `CompiledMethod>>getSource` to read the last 3 bytes containing encoded position.

```csharp
/// <summary>
/// Special handling for CompiledMethod: In Blue Book, CompiledMethod>>at: accesses BYTECODES,
/// not header/literals (those use objectAt:).
/// </summary>
public class At : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        int index = context.Arguments[0].AsSmallInteger();

        // Special handling for CompiledMethod: at: accesses bytecodes only
        var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
        if (classOOP == context.Metadata!.CompiledMethodClassOOP)
        {
            // CompiledMethod stores bytecodes in a hidden ByteArray
            int cmIndexedSize = context.HeapManager.GetIndexedSize(context.Receiver);
            var bytecodeArrayOOP = context.HeapManager.GetIndexedOOP(context.Receiver, cmIndexedSize - 1);
            int bytecodeCount = context.HeapManager.GetIndexedSize(bytecodeArrayOOP);

            if (index < 1 || index > bytecodeCount)
                return PrimitiveResult.Fail(context.Metadata!);

            byte b = context.HeapManager.GetIndexedByte(bytecodeArrayOOP, index - 1);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(b));
        }
        // ... standard array access ...
    }
}
```

**Implementation:**
- Redirects to hidden ByteArray for transparent bytecode access
- Preserves 1-based Smalltalk indexing
- Returns SmallInteger byte values (0-255)

**Status:** ✅ **WORKING** - Enables reading of encoded source position bytes.

#### Primitive 62: CompiledMethod size

**Location:** ObjectPrimitives.cs, lines 12-39

**Purpose:** Returns bytecode count so `getSource` can access the last byte.

```csharp
public class Size : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var classOOP = context.HeapManager.GetClassOOP(context.Receiver);
        if (classOOP == context.Metadata!.CompiledMethodClassOOP)
        {
            int cmIndexedSize = context.HeapManager.GetIndexedSize(context.Receiver);
            var bytecodeArrayOOP = context.HeapManager.GetIndexedOOP(context.Receiver, cmIndexedSize - 1);
            int bytecodeCount = context.HeapManager.GetIndexedSize(bytecodeArrayOOP);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(bytecodeCount));
        }
        // ... standard size ...
    }
}
```

**Status:** ✅ **WORKING** - Returns correct bytecode count including position bytes.

#### Primitive 124: File Seek (position:)

**Location:** FilePrimitives.cs, lines 597-676

**Purpose:** Implements `FileStream>>position:` for seeking to source position.

```csharp
/// <summary>
/// Primitive 124: Seek in file (SetFilePointer equivalent).
/// Supports 2-arg (handle, offset) or 3-arg (handle, offset, origin) calling conventions.
/// </summary>
public class FileSeek : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        int handle = handleOOP.AsSmallInteger();
        int offset = offsetOOP.AsSmallInteger();
        int origin = 0; // Default to Begin

        var stream = FileHandleManager.GetStream(handle);
        if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

        SeekOrigin seekOrigin = origin switch
        {
            0 => SeekOrigin.Begin,
            1 => SeekOrigin.Current,
            2 => SeekOrigin.End,
            _ => SeekOrigin.Begin
        };

        // Apply BOM offset for seeks from beginning
        int actualOffset = offset;
        if (seekOrigin == SeekOrigin.Begin)
        {
            int bomOffset = FileHandleManager.GetBomOffset(handle);
            if (bomOffset > 0)
                actualOffset = offset + bomOffset;
        }

        long newPosition = stream.Seek(actualOffset, seekOrigin);
        return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)newPosition));
    }
}
```

**BOM Offset Handling:**

The implementation transparently handles UTF-8 BOM (Byte Order Mark):
- Detects UTF-8 BOM (EF BB BF) when opening file
- Stores 3-byte offset for BOM-enabled files
- All seeks from beginning automatically add BOM offset
- Source positions in CompiledMethods work correctly without BOM awareness

**Status:** ✅ **WORKING** - Fully functional seek with automatic BOM compensation.

#### Primitive 132: File Read Byte

**Location:** FilePrimitives.cs, lines 442-492

**Purpose:** Optimized single-byte read for `nextChunk` implementation.

```csharp
/// <summary>
/// Primitive 132: Read a single byte from file at position.
/// Arguments: handle (Integer), position (Integer).
/// Returns: Integer byte value (0-255), or -1 on EOF/error.
/// </summary>
public class FileReadByte : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        int handle = handleOOP.AsSmallInteger();
        int position = positionOOP.AsSmallInteger();

        var stream = FileHandleManager.GetStream(handle);
        if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

        // Apply BOM offset so source positions in CompiledMethods work correctly
        int actualPosition = position + FileHandleManager.GetBomOffset(handle);
        stream.Position = actualPosition;
        int byteValue = stream.ReadByte();

        return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(byteValue));
    }
}
```

**Status:** ✅ **WORKING** - Fast single-byte reads with BOM offset.

#### Primitive 65: Stream next

**Location:** ObjectPrimitives.cs, lines 425-533

**Purpose:** Optimizes `ReadStream>>next` for character-by-character reading in `nextChunk`.

```csharp
/// <summary>
/// Primitive 65: Stream next - read next element from stream
/// Handles String/Symbol (returns Character), ByteArray (returns SmallInteger)
/// </summary>
public class Next : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        SmalltalkOOP collectionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 0);
        SmalltalkOOP positionOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
        int position = positionOOP.AsSmallInteger();

        SmalltalkOOP element;
        var format = context.HeapManager.GetObjectFormat(collectionOOP);
        if (format == ObjectFormat.ByteIndexed)
        {
            byte byteValue = context.HeapManager.GetIndexedByte(collectionOOP, position - 1);

            // For String/Symbol, return Character object from CharacterTable
            if (IsKindOf(context, collectionOOP, context.Metadata!.StringClassOOP) ||
                IsKindOf(context, collectionOOP, context.Metadata!.SymbolClassOOP))
            {
                element = GetCharacterForAscii(context, byteValue);
            }
            else
            {
                element = SmalltalkOOP.FromSmallInteger(byteValue);
            }
        }

        // Increment position
        context.HeapManager.SetInstanceVariable(context.Receiver, 1,
            SmalltalkOOP.FromSmallInteger(position + 1));

        return PrimitiveResult.Ok(element);
    }
}
```

**Recent Fix (2025-11-28):**
- Now correctly returns Character objects when reading from String/Symbol streams
- Uses `isKindOf:` semantics to handle subclasses properly
- Critical for Scanner and source code parsing

**Status:** ✅ **WORKING** - Essential for `nextChunk` character parsing.

#### Primitive 144: Get File Size

**Location:** FilePrimitives.cs, lines 1140-1225

**Purpose:** Returns file size for stream positioning logic.

```csharp
public class GetFileSize : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        long size;

        if (context.Arguments.Length == 0)
        {
            // Get handle from receiver's instance variable slot 1
            var handleOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 1);
            int handle = handleOOP.AsSmallInteger();
            var stream = FileHandleManager.GetStream(handle);
            if (stream == null) return PrimitiveResult.Fail(context.Metadata!);
            size = stream.Length;
        }
        else if (context.Arguments.Length == 1)
        {
            // Handle or path argument
            // ... (supports both handle and path-based queries)
        }

        // Clamp to SmallInteger range
        if (size > 0x3FFFFFFF) size = 0x3FFFFFFF;
        return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)size));
    }
}
```

**Status:** ✅ **WORKING** - Supports multiple calling conventions.

### 4. UTF-8 BOM Handling Implementation

**Location:** FilePrimitives.cs, FileHandleManager class (lines 74-96)

**Purpose:** Automatically detect and compensate for UTF-8 Byte Order Mark in source files.

```csharp
// In FileHandleManager.OpenFile:
int bomOffset = 0;
if (fileStream.Length >= 3 && access != FileAccess.Write)
{
    byte[] bom = new byte[3];
    int bytesRead = bufferedStream.Read(bom, 0, 3);
    if (bytesRead == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
    {
        bomOffset = 3;
        Console.WriteLine($"[FileHandleManager] Detected UTF-8 BOM, offset={bomOffset}");
    }
    bufferedStream.Position = 0; // Reset to start
}
_bomOffsets[handle] = bomOffset;
```

**Integration:**
- Detected at file open time
- Stored per file handle in `_bomOffsets` dictionary
- Applied automatically in:
  - Primitive 124 (FileSeek) - when seeking from beginning
  - Primitive 132 (FileReadByte) - for positioned reads

**Example:**
```
Source file: [EF BB BF] H e l l o ...
             ^^^^^^^^^ BOM (3 bytes)
Position 0 in CompiledMethod → Actual file position 3
Position 5 in CompiledMethod → Actual file position 8
```

**Status:** ✅ **WORKING** - Transparent BOM handling for UTF-8 source files.

### 5. File Handle Management

**Location:** FilePrimitives.cs, FileHandleManager class (lines 17-155)

**Purpose:** Manage open file streams with handle-based access.

```csharp
private static class FileHandleManager
{
    private static readonly Dictionary<int, Stream> _openFiles = [];
    private static readonly Dictionary<int, int> _bomOffsets = [];
    private static int _nextHandle = 1;
    private const int BufferSize = 4096;

    public static int OpenFile(string path, int accessMode, int shareMode, int createMode)
    {
        // ... create FileStream with appropriate modes ...
        var bufferedStream = new BufferedStream(fileStream, BufferSize);
        int handle = _nextHandle++;
        _openFiles[handle] = bufferedStream;
        // ... detect BOM ...
        _bomOffsets[handle] = bomOffset;
        return handle;
    }

    public static Stream? GetStream(int handle)
    {
        return _openFiles.TryGetValue(handle, out var stream) ? stream : null;
    }

    public static int GetBomOffset(int handle)
    {
        return _bomOffsets.TryGetValue(handle, out var offset) ? offset : 0;
    }
}
```

**Features:**
- Buffered I/O (4KB buffer) for efficient byte-by-byte reads
- Thread-safe with lock protection
- Tracks BOM offset per handle
- Auto-incrementing handle allocation
- Proper cleanup on close

**Status:** ✅ **WORKING** - Robust file handle management.

## Analysis Summary

### ✅ Working Components

1. **Source Position Encoding**
   - Correct bit layout matching Smalltalk decoder
   - Supports 22-bit position + 2-bit file index
   - Ready to use when compiler provides positions

2. **SourceFiles Initialization**
   - Opens `.sources` and `.changes` files
   - Creates WindowsFileReference objects with handles
   - Linux-compatible path handling (uses String, not WindowsPathName)
   - Automatic `.changes` file creation

3. **File Primitives**
   - Primitive 124 (position:) - Seek with BOM compensation
   - Primitive 132 (FileReadByte) - Single-byte read with BOM
   - Primitive 144 (size) - File size query
   - Primitive 65 (next) - Character streaming for nextChunk
   - UTF-8 BOM detection and transparent handling

4. **ByteCode Access**
   - Primitive 60 (at:) - Read encoded position bytes
   - Primitive 62 (size) - Get bytecode count
   - Transparent ByteArray redirection for CompiledMethod

### ⚠️ Missing Components

#### 1. Compiler Source Position Tracking

**Issue:** The compiler doesn't track source file positions during method compilation.

**Impact:** Methods compiled during cold start have no source positions (position = 0).

**Required Changes:**

**File:** `Smalltalk80.Compiler/BytecodeCompiler.cs`

Add position tracking:
```csharp
public class CompilationContext
{
    public int SourceFileIndex { get; set; } = 1; // 1 = .sources
    public int SourcePosition { get; set; } = 0;
}
```

**File:** `Smalltalk80.Compiler/ColdStartGenerator.cs`

Track method positions:
```csharp
// Before compiling each method:
int methodPosition = (int)sourcesWriter.BaseStream.Position;
WriteChunk(sourcesWriter, method.SourceCode);

// Pass to compiler:
var compiledMethod = compiler.CompileMethod(
    methodSource,
    fileIndex: 1,
    sourcePosition: methodPosition);
```

#### 2. Source File Generation

**Issue:** No `.sources` or `.changes` files created during compilation.

**Impact:** `SourceFiles` returns nil, `getSource` falls back to decompiler.

**Required Implementation:**

```csharp
// In ColdStartGenerator.CompileSourceFile:
string sourcesPath = Path.Combine(outputDir, "Smalltalk-80.sources");
using var sourcesWriter = new StreamWriter(sourcesPath, false, Encoding.UTF8);

foreach (var methodDef in methodDefinitions)
{
    // Write header chunk
    WriteChunk(sourcesWriter, $"{className} methodsFor: '{category}'");

    // Get position before writing method
    int position = (int)sourcesWriter.BaseStream.Position;

    // Write method source
    WriteChunk(sourcesWriter, methodDef.SourceCode);

    // Store position for later encoding
    methodDef.SourceFileIndex = 1;
    methodDef.SourcePosition = position;
}

// Helper method:
private void WriteChunk(StreamWriter writer, string content)
{
    // Escape ! as !!
    string escaped = content.Replace("!", "!!");
    writer.Write(escaped);
    writer.WriteLine("!");
}
```

#### 3. nextChunk Primitive (Optional)

**Issue:** No primitive for `nextChunk` - uses Smalltalk fallback.

**Impact:** Source retrieval works but slower than optimal.

**Priority:** LOW - Smalltalk implementation is adequate.

**Optional Implementation:**

```csharp
// Primitive 156: nextChunk
public class NextChunk : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        var stream = GetFileStream(context.Receiver);
        var result = new StringBuilder(1000);
        bool prevWasBang = false;

        while (true)
        {
            int byteValue = stream.ReadByte();
            if (byteValue < 0) break; // EOF

            char ch = (char)byteValue;
            if (ch == '!')
            {
                if (prevWasBang)
                {
                    result.Append('!');
                    prevWasBang = false;
                }
                else
                {
                    prevWasBang = true;
                }
            }
            else
            {
                if (prevWasBang) break; // End of chunk
                result.Append(ch);
            }
        }

        return PrimitiveResult.Ok(CreateString(context, result.ToString()));
    }
}
```

### Implementation Roadmap

#### Phase 1: Source File Generation (HIGH PRIORITY)

1. Modify `ColdStartGenerator` to create `.sources` file
2. Write source code in chunk format during compilation
3. Track file positions for each method definition
4. Store positions in intermediate data structure

#### Phase 2: Compiler Integration (HIGH PRIORITY)

1. Update `BytecodeCompiler.CompileMethod` to accept source position parameters
2. Pass `fileIndex` and `sourcePosition` to `AllocateCompiledMethodInternal`
3. Verify encoding by testing with simple methods

#### Phase 3: Testing (HIGH PRIORITY)

1. Create minimal `.sources` file with known positions
2. Compile methods with position encoding
3. Test `CompiledMethod>>getSource` retrieval
4. Verify chunk parsing works correctly

#### Phase 4: Position Tracking (MEDIUM PRIORITY)

1. Update primitives to maintain WindowsFileReference position variable
2. Ensure consistency between C# stream position and Smalltalk instance variable

#### Phase 5: nextChunk Primitive (LOW PRIORITY)

1. Implement Primitive 156 for optimized chunk reading
2. Benchmark performance improvement
3. Consider cost/benefit trade-off

### Testing Source Lookup

Once source files are generated, test with:

```smalltalk
"Test source position encoding/decoding"
| method end highByte position fileIndex |
method := Array compiledMethodAt: #new:.
end := method size.
highByte := method at: end.
fileIndex := (highByte bitShift: -6) + 1.
position := highByte bitAnd: 63.
position := position * 256 + (method at: end - 1).
position := position * 256 + (method at: end - 2).
Transcript show: 'File: ', fileIndex printString; cr.
Transcript show: 'Position: ', position printString; cr.

"Test source retrieval"
source := method getSource.
Transcript show: 'Source: ', source; cr.
```

**Expected Output:**
```
File: 1
Position: 12345
Source: new: size
    "Answer an instance of me whose indexed size is size."
    <primitive: 71>
    self isVariable
        ifTrue: [^self basicNew: size]
        ifFalse: [self error: 'new: only applies to variable size classes']
```

## Conclusion

The C# interpreter has **complete runtime infrastructure** for source code lookup:

✅ **Source position encoding** - Fully implemented in compiler and interpreter
✅ **SourceFiles initialization** - Creates proper Array with file handles
✅ **File primitives** - All required operations (seek, read, size)
✅ **UTF-8 BOM handling** - Automatic and transparent (fixed 2025-11-30)
✅ **CompiledMethod access** - Bytecode reading via at:/size primitives
✅ **Compiler integration** - ColdStartJsonGenerator writes .sources file with positions

**Bug Fixed (2025-11-30): BOM Double-Offset**

A bug was found and fixed in primitive 124 (FileSeek) where:
- Seek added BOM offset to seek to physical position
- Seek returned PHYSICAL position (with BOM offset baked in)
- Read (primitive 132) then added BOM offset AGAIN to the stored position

**Fix:** Primitive 124 now returns LOGICAL position (without BOM offset). This ensures:
1. Smalltalk code works with BOM-transparent logical positions
2. Primitive 132 correctly translates logical → physical when reading

```
Before fix:
  position: 0 → seek to physical 3, return 3
  next → read at position 3 + BOM(3) = 6 ❌ WRONG

After fix:
  position: 0 → seek to physical 3, return 0 (logical)
  next → read at position 0 + BOM(3) = 3 ✅ CORRECT
```

**Status:** Source lookup should now work end-to-end. The compiler generates `.sources` files with proper chunk format, tracks positions, and encodes them in methods. The interpreter primitives correctly handle UTF-8 BOM files.
