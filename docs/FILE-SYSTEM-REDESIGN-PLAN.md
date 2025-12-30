# File System Redesign Plan

## Executive Summary

The current file system implementation carries legacy baggage from the Alto file system, particularly the `FilePage` abstraction. This document proposes eliminating the page-based model in favor of a modern stream-based architecture that better matches Windows (and modern OS) semantics.

## Current Architecture Problems

### 1. The FilePage Legacy

The Alto computer used a **page-based file system** where files were collections of fixed-size pages (typically 512 bytes). This led to:

- `FilePage` class representing individual pages
- `File.lastPageNumber` tracking page count
- Multi-page enumeration for large files
- Complex position calculations involving page numbers and offsets
- `dataBeginning`, `dataEnd`, `dataSize`, `headerSize`, `trailerSize` concepts

**Windows (and modern OSes) don't work this way.** Files are contiguous byte streams with:
- A single file handle
- A position pointer
- Direct read/write at any offset
- No page boundaries

### 2. Redundant Class Hierarchy

The current implementation has THREE parallel hierarchies:

```
Legacy Abstract          Windows Concrete         Modern Reference
--------------          ----------------         ----------------
File                    WindowsFile              FileReference
FileDirectory           WindowsFileDirectory     WindowsFileReference
FilePage                WindowsFilePage          DirectoryReference
FileStream              WindowsFileStream        WindowsDirectoryReference
```

This creates:
- Code duplication
- Confusion about which classes to use
- Maintenance burden
- Unnecessary indirection

### 3. The `asFileStream:` Problem

The snapshot bug we just fixed illustrates a fundamental problem:

```smalltalk
WindowsFilePage>>asFileStream: aStream
    "Open file and read contents"
    self openForRead.
    sz _ self size.
    sourceData _ self dataString.   "Reads ENTIRE file into memory!"
    bufferSize _ self dataSize.

    "Initialize the stream with file contents"
    destData _ String new: bufferSize.  "Creates ANOTHER buffer!"
    destData replaceFrom: 1 to: sz withByteArray: sourceData startingAt: 1.
    aStream on: destData from: 1 to: sz.
```

This approach:
- Reads entire files into memory (breaks for large files)
- Creates unnecessary String buffers
- Doesn't support true streaming
- Hides the actual file handle from FileStream

### 4. Confusion Between Modes and Operations

Current code mixes:
- File object creation
- File opening
- Read/write mode setting
- Page creation

Example:
```smalltalk
file := WindowsFile named: 'foo.txt'.
file open.                          "Does what?"
page := file readPageNumber: 1.     "Opens file again?"
stream := page asFileStream.        "Reads entire file!"
stream readWrite.                   "Changes mode after reading?"
```

## Proposed New Architecture

### Design Principles

1. **Stream-First**: Files are byte streams, not page collections
2. **Lazy I/O**: Don't read until needed, don't buffer entire files
3. **Handle-Based**: Expose file handle for direct primitive access
4. **Single Hierarchy**: One set of classes, not three
5. **Mode at Open**: Specify read/write mode when opening, not after
6. **No Pages**: Eliminate FilePage entirely

### New Class Structure

```
WindowsPath           - Path manipulation (parsing, joining, etc.)
WindowsFileSystem     - Drive/volume operations
WindowsDirectory      - Directory operations (list, create, delete)
WindowsFile           - File metadata (exists, size, attributes, delete, rename)
WindowsFileStream     - File I/O stream (read, write, seek, position)
```

### Class Specifications

#### WindowsPath (Replaces WindowsPathName)

```smalltalk
Object subclass: #WindowsPath
    instanceVariableNames: 'driveLetter components'
    classVariableNames: 'Separator'
    poolDictionaries: ''
    category: 'Files-Windows'

"Path manipulation without any I/O"

Class methods:
    separator               "Answer $\"
    fromString: aString     "Parse path string"

Instance methods:
    asString                "Answer path as string"
    driveLetter             "Answer drive letter or nil for UNC"
    components              "Answer path components array"
    fileName                "Answer last component"
    directoryPath           "Answer path without filename"
    extension               "Answer file extension"
    withExtension:          "Answer new path with extension"
    / aString               "Answer new path with component added"
    isAbsolute              "Answer if path is absolute"
    isUNC                   "Answer if UNC path"
```

#### WindowsFileSystem (Simplified)

```smalltalk
Object subclass: #WindowsFileSystem
    instanceVariableNames: ''
    classVariableNames: 'Current'
    poolDictionaries: ''
    category: 'Files-Windows'

Class methods:
    current                 "Answer singleton instance"
    drives                  "Answer array of drive letters"

Instance methods:
    currentDirectory        "Answer current working directory path"
    currentDirectory:       "Set current working directory"
    tempDirectory           "Answer temp directory path"
```

#### WindowsDirectory (Replaces WindowsFileDirectory)

```smalltalk
Object subclass: #WindowsDirectory
    instanceVariableNames: 'path'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Windows'

"Directory operations - no file handle, no page concepts"

Class methods:
    on: pathString          "Answer directory at path"
    current                 "Answer current directory"

Instance methods:
    path                    "Answer WindowsPath"
    pathString              "Answer path as string"

    "Testing"
    exists                  "Answer if directory exists"

    "Contents"
    entries                 "Answer array of entry names"
    fileNames               "Answer array of file names"
    directoryNames          "Answer array of subdirectory names"

    "File/Directory access"
    fileNamed: name         "Answer WindowsFile for name"
    directoryNamed: name    "Answer WindowsDirectory for subdirectory"
    / name                  "Synonym for fileNamed: or directoryNamed:"

    "Modification"
    create                  "Create directory if not exists"
    delete                  "Delete empty directory"

    "Streams (convenience)"
    readStream: name        "Answer read stream on file"
    writeStream: name       "Answer write stream on file"
    appendStream: name      "Answer append stream on file"
```

#### WindowsFile (Simplified - Metadata Only)

```smalltalk
Object subclass: #WindowsFile
    instanceVariableNames: 'path'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Windows'

"File metadata and operations - does NOT hold a file handle"

Class methods:
    on: pathString          "Answer file reference at path"
    named: pathString       "Synonym for on:"

Instance methods:
    path                    "Answer WindowsPath"
    pathString              "Answer path as string"
    fileName                "Answer file name only"
    directory               "Answer WindowsDirectory containing this file"

    "Testing"
    exists                  "Answer if file exists"
    isReadOnly              "Answer if file is read-only"

    "Attributes"
    size                    "Answer file size in bytes"
    creationTime            "Answer creation timestamp"
    modificationTime        "Answer modification timestamp"

    "Modification"
    delete                  "Delete file"
    renameTo: newPath       "Rename/move file"
    copyTo: newPath         "Copy file"

    "Streams"
    readStream              "Answer read-only stream"
    writeStream             "Answer write stream (creates/truncates)"
    appendStream            "Answer append stream"
    readWriteStream         "Answer read-write stream"

    "Snapshot support"
    beSnapshotFile          "Register as snapshot file for VM"
```

#### WindowsFileStream (Complete Rewrite)

```smalltalk
Object subclass: #WindowsFileStream
    instanceVariableNames: 'path handle position mode'
    classVariableNames: 'ReadMode WriteMode AppendMode ReadWriteMode'
    poolDictionaries: ''
    category: 'Files-Windows'

"Actual file I/O stream - holds file handle, supports streaming"

Class methods:
    initialize
        ReadMode := 1.
        WriteMode := 2.
        AppendMode := 3.
        ReadWriteMode := 4.

    on: pathString mode: modeSymbol
        "modeSymbol is #read, #write, #append, or #readWrite"

    readOn: pathString      "Open for reading"
    writeOn: pathString     "Open for writing (create/truncate)"
    appendOn: pathString    "Open for appending"
    readWriteOn: pathString "Open for read/write"

Instance methods:
    "Accessing"
    path                    "Answer WindowsPath"
    pathString              "Answer path string"
    name                    "Answer file name (for compatibility)"
    file                    "Answer self (for compatibility)"

    "Status"
    isOpen                  "Answer if file is open"
    atEnd                   "Answer if at end of file"
    position                "Answer current position"
    position: anInteger     "Set position"
    size                    "Answer file size"

    "Reading"
    next                    "Read next byte as Character"
    next: count             "Read count bytes as String"
    nextByte                "Read next byte as Integer"
    nextBytes: count        "Read count bytes as ByteArray"
    peek                    "Read without advancing"
    upToEnd                 "Read from position to end"
    contents                "Read entire file"

    "Writing"
    nextPut: aCharacter     "Write character"
    nextPutAll: aString     "Write string"
    nextBytePut: anInteger  "Write byte"
    nextBytesPutAll: aByteArray  "Write bytes"
    cr                      "Write line ending"
    flush                   "Flush buffers to disk"
    truncate                "Truncate at current position"

    "Positioning"
    reset                   "Position at beginning"
    setToEnd                "Position at end"
    skip: count             "Move position by count"

    "Chunk I/O (for source files)"
    nextChunk               "Read ! terminated chunk"
    nextChunkPut: aString   "Write ! terminated chunk"

    "Lifecycle"
    close                   "Close file handle"

    "Private - Primitives"
    primOpen: path mode: mode       "Primitive 121"
    primClose: handle               "Primitive 122"
    primRead: handle count: count   "Primitive 123"
    primWrite: handle bytes: bytes  "Primitive 136"
    primSeek: handle offset: offset origin: origin  "Primitive 124"
    primSize: handle                "Primitive 144"
```

## Migration Strategy

### Phase 1: Create New Classes (Non-Breaking)

1. Create new classes with new names:
   - `WindowsPath` (new)
   - `WindowsDirectory` (rename from WindowsFileDirectory)
   - `WindowsFile` (simplify existing)
   - `WindowsFileStream` (rewrite existing)

2. Keep old classes working during transition

### Phase 2: Update Callers

#### SourceFiles System

**Current:**
```smalltalk
"In Phase 5 cold start"
sourcesStream := WindowsFileStream new.
sourcesStream pathName: sourcesPath; handle: sourcesHandle.
```

**New:**
```smalltalk
sourcesStream := WindowsFileStream readOn: sourcesPath.
```

#### SystemDictionary>>snapshotAs:thenQuit:

**Current (after our fix):**
```smalltalk
(WindowsFile named: imageFilePrefix, '.im') beSnapshotFile
```

**New:**
```smalltalk
(WindowsFile on: imageFilePrefix, '.im') beSnapshotFile
```

No change needed - already uses the simple pattern.

#### FileStream fileNamed:

**Current:**
```smalltalk
FileStream class>>fileNamed: fileDesignator
    | dir fNameHolder |
    fNameHolder := Array new: 1.
    dir := FileDirectory directoryFromName: fileDesignator
                         setFileName: [:name | fNameHolder at: 1 put: name].
    ^(dir findOrAddKey: (fNameHolder at: 1)) asFileStream
```

**New:**
```smalltalk
FileStream class>>fileNamed: pathString
    ^WindowsFileStream readWriteOn: pathString
```

#### Compiler Source Output

**Current:**
```smalltalk
Behavior>>compile:notifying:trailer:ifFail:
    "...trailer is three bytes pointing to source location..."
```

This needs to continue working - source positions are stored as byte offsets. No change needed since we're keeping byte-based positioning.

#### Form>>writeOn:

**Current:**
```smalltalk
Form>>writeOn: fileName
    file := FileStream fileNamed: fileName.
    file binary.
    file nextWordPut: fileCode.
    ...
```

**New:**
```smalltalk
Form>>writeOn: fileName
    | stream |
    stream := WindowsFileStream writeOn: fileName.
    stream nextWordPut: fileCode.  "Stream handles binary automatically"
    ...
```

### Phase 3: Remove Legacy Classes

Once all callers are updated:

1. Remove `FilePage` class entirely
2. Remove `WindowsFilePage` class entirely
3. Remove page-related methods from `File` and `WindowsFile`
4. Remove `FileDirectory` if not needed for compatibility
5. Simplify `FileStream` to delegate to `WindowsFileStream`

## Detailed Change List

### Classes to Create

| Class | Purpose |
|-------|---------|
| `WindowsPath` | Path parsing and manipulation |

### Classes to Simplify

| Class | Changes |
|-------|---------|
| `WindowsFile` | Remove page-related methods, keep only metadata |
| `WindowsFileStream` | Rewrite for direct handle-based I/O |
| `WindowsDirectory` | Remove page allocation, simplify API |
| `WindowsFileSystem` | Remove unnecessary state |

### Classes to Remove

| Class | Reason |
|-------|--------|
| `FilePage` | Page abstraction not needed |
| `WindowsFilePage` | Page abstraction not needed |
| `FileReference` | Redundant with WindowsFile |
| `WindowsFileReference` | Redundant with WindowsFile |
| `DirectoryReference` | Redundant with WindowsDirectory |
| `WindowsDirectoryReference` | Redundant with WindowsDirectory |
| `WindowsPathName` | Replaced by WindowsPath |

### Methods to Remove from WindowsFile

```smalltalk
"Page-related - remove all"
pageClass
readPageNumber:
readOrAddPageNumber:
initPageNumber:
initPage:
read:
write:
endFile:
lastPageNumber
currentPage

"Mode setting after creation - remove"
binary
text
readOnly
readWrite
```

### Methods to Remove from WindowsFileDirectory

```smalltalk
"Page-related - remove all"
allocate:after:
deallocate:
freePages
newPage
pageClass
```

### Methods to Update in FileStream

The `FileStream` class can become a thin wrapper:

```smalltalk
FileStream class>>fileNamed: pathString
    ^WindowsFileStream readWriteOn: pathString

FileStream class>>oldFileNamed: pathString
    ^WindowsFileStream readOn: pathString

FileStream class>>newFileNamed: pathString
    ^WindowsFileStream writeOn: pathString

FileStream class>>on: aFilePage
    "Compatibility - convert old page-based code"
    ^WindowsFileStream readWriteOn: aFilePage file fullName
```

### Callers to Update

| Location | Current | New |
|----------|---------|-----|
| `File initialize` | Creates FilePool with page modes | Remove page modes |
| `SystemDictionary>>copyChangesTo:` | Uses FileStream fileNamed: | Use WindowsFileStream |
| `SystemDictionary>>snapshotAs:thenQuit:` | Already fixed | No change |
| `Behavior>>fileOutOn:` | Uses file nextChunkPut: | No change (method exists) |
| `Class>>fileOut` | Uses FileStream fileNamed: | Update to WindowsFileStream |
| `FileModel>>acceptText:from:` | Uses FileStream fileNamed: | Update |
| `Form>>writeOn:` | Uses FileStream fileNamed: | Update |
| `Form>>readFrom:` | Uses FileStream oldFileNamed: | Update |
| `MessageTally>>spyOn:to:` | Uses FileStream fileNamed: | Update |
| `RemoteString>>string:onFileNumber:toFile:` | Uses SourceFiles | No change |
| `Benchmark>>testList:toFile:` | Uses FileStream fileNamed: | Update |

## API Compatibility Layer

For gradual migration, provide compatibility methods:

```smalltalk
WindowsFileStream>>page
    "Compatibility - answer self as pseudo-page"
    ^self

WindowsFileStream>>dataSize
    "Compatibility - answer file size"
    ^self size

WindowsFileStream>>dataBeginning
    "Compatibility - answer 0"
    ^0

WindowsFileStream>>asFileStream: aStream
    "Compatibility - copy state to aStream"
    aStream become: self copy
```

## Primitives

No new primitives needed. Existing primitives:

| Primitive | Purpose | Keep/Modify |
|-----------|---------|-------------|
| 121 | File open | Keep |
| 122 | File close | Keep |
| 123 | File read (bulk) | Keep |
| 124 | File seek | Keep |
| 125 | File delete | Keep |
| 126 | File rename | Keep |
| 127 | Directory create | Keep |
| 128 | Directory delete | Keep |
| 132 | Byte read | Keep |
| 134 | Byte write | Keep |
| 136 | File write (bulk) | Keep |
| 140 | File truncate | Keep |
| 144 | Get file size | Keep |
| 146 | Directory listing | Keep |
| 154 | Get file attributes | Keep |
| 156 | Mark snapshot file | Keep |

## Testing Strategy

1. **Unit tests for new classes**
   - WindowsPath parsing
   - WindowsDirectory operations
   - WindowsFile operations
   - WindowsFileStream read/write/seek

2. **Integration tests**
   - Source file reading (critical for browser)
   - Changes file writing
   - Snapshot save/restore
   - Form file I/O

3. **Regression tests**
   - All existing file operations continue working
   - SourceFiles access still works
   - Compiler source positions still work

## Risk Assessment

| Risk | Mitigation |
|------|------------|
| Break source code retrieval | Test browser extensively |
| Break snapshot save | Test save/restore cycle |
| Break changes file | Test method compilation |
| Performance regression | Benchmark file operations |
| Memory issues with large files | Test with large files |

## Timeline Estimate

| Phase | Work Items | Complexity |
|-------|------------|------------|
| 1 | Create WindowsPath, rewrite WindowsFileStream | Medium |
| 2 | Simplify WindowsFile, WindowsDirectory | Medium |
| 3 | Update all callers | High (many locations) |
| 4 | Remove legacy classes | Low |
| 5 | Testing and bug fixes | Medium |

## Summary

The proposed redesign:

1. **Eliminates FilePage** - No more artificial page boundaries
2. **Simplifies class hierarchy** - One set of classes instead of three
3. **Enables true streaming** - Don't read entire files into memory
4. **Clarifies responsibilities** - Path, File (metadata), Directory, Stream
5. **Maintains compatibility** - Gradual migration with compatibility layer

The key insight is that **Windows files are streams, not page collections**. The new design embraces this reality while maintaining compatibility with existing Smalltalk source code patterns.
