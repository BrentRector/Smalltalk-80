# Smalltalk-80 File System Design

**Version:** 1.4
**Date:** 2025-12-30
**Status:** Implemented - Legacy classes removed

> **Note**: This document has been updated to reflect the actual implementation.
> See `Smalltalk-80 Source/Smalltalk-80.st` for the authoritative source code.

## Table of Contents

1. [Overview](#overview)
2. [Design Goals](#design-goals)
3. [Design Rationale](#design-rationale)
4. [Microsoft Compliance](#microsoft-compliance)
5. [Class Hierarchy](#class-hierarchy)
6. [Class Specifications](#class-specifications)
7. [Initialization and Cold Start](#initialization-and-cold-start)
8. [Usage Examples](#usage-examples)
9. [Migration from Legacy System](#migration-from-legacy-system)

---

## Overview

This document describes a modernized file system architecture for Smalltalk-80 that:

- Removes the legacy Alto page-based abstractions (`FilePage`, page buffers, etc.)
- Provides clean abstract base classes that define file system protocols
- Implements concrete Windows-specific classes that inherit from the abstractions
- Enables platform-independent Smalltalk code through polymorphism

The architecture follows the principle: **"Program to abstractions, instantiate concrete classes."**

---

## Design Goals

### Primary Goals

1. **Abstraction-Based Programming**: Application code works with abstract protocols (`FileSystem`, `Directory`, `File`, `Path`, `FileStream`) without knowledge of the underlying platform.

2. **Platform Polymorphism**: Concrete implementations (`WindowsFileSystem`, `WindowsDirectory`, etc.) are instantiated at system startup. All file operations flow through these concrete instances transparently.

3. **Single Entry Point**: The `Disk` global provides the entry point to the file system. All concrete class knowledge flows from `Disk`.

4. **Clean Inheritance**: Concrete classes inherit from abstract base classes, gaining common behavior while implementing platform-specific operations.

5. **No Page Abstraction**: The legacy Alto `FilePage` concept is completely eliminated. Files are accessed as byte streams.

### Secondary Goals

- Support file system properties (case sensitivity, name constraints, etc.)
- Support file attributes (timestamps, hidden, read-only, etc.)
- Provide path manipulation with platform-aware semantics
- Enable future porting to other platforms (Unix, etc.)

---

## Design Rationale

### Why Remove the Page Abstraction?

The original Smalltalk-80 file system was designed for the Xerox Alto, which used a page-based disk architecture. Files were collections of fixed-size pages, and `FilePage` objects managed buffering between memory and disk.

Modern operating systems provide:
- Byte-addressable file access
- Efficient kernel-level buffering
- Memory-mapped file support
- Large file support (>2GB)

The page abstraction adds complexity without benefit on modern systems.

### Why Separate FileSystem from Directory?

In the legacy system, `Disk` was simply a `FileDirectory`. This conflates two concepts:

- **FileSystem**: Platform knowledge, naming rules, volumes, concrete class factories
- **Directory**: Operations on a specific directory (list, create, delete files)

Separating these allows:
- `FileSystem` to be the authority on platform-specific behavior
- `Directory` to be a pure container abstraction
- Multiple directories to share one `FileSystem`

### Why Instance-Based Paths (vs. Static Methods)?

Unlike .NET's `Path` static class, we use instance-based `Path` objects because:

1. **Polymorphism**: `WindowsPath` and `UnixPath` can have different parsing/formatting
2. **State**: Path objects hold parsed components, avoiding repeated parsing
3. **Smalltalk Style**: Objects with behavior, not utility functions

### Why a Common Base Class for File and Directory?

Both files and directories:
- Have a path/name
- Can be tested for existence
- Can be deleted, renamed, moved
- Have attributes (timestamps, hidden, read-only)

The `FileSystemNode` base class captures this shared protocol, following .NET's `FileSystemInfo` pattern.

### Comparison with .NET Framework

| Concept | .NET | This Design |
|---------|------|-------------|
| Platform file system | (implicit) | `FileSystem` → `WindowsFileSystem` |
| Directory | `DirectoryInfo` | `Directory` → `WindowsDirectory` |
| File metadata | `FileInfo` | `File` → `WindowsFile` |
| Common base | `FileSystemInfo` | `FileSystemNode` |
| Path handling | `Path` (static) | `Path` → `WindowsPath` (instances) |
| File streaming | `FileStream` | `FileStream` → `WindowsFileStream` |

---

## Microsoft Compliance

Our Windows implementation follows the [Microsoft Naming Files, Paths, and Namespaces](https://learn.microsoft.com/en-us/windows/win32/fileio/naming-a-file) documentation precisely.

### File Name Validation

**Illegal Characters:**
- Reserved characters: `< > : " / \ | ? *`
- Control characters: ASCII 0-31 (NUL through Unit Separator)

**Reserved Names (case-insensitive):**
- Device names: `CON`, `PRN`, `AUX`, `NUL`
- Serial ports: `COM1` through `COM9`
- Parallel ports: `LPT1` through `LPT9`
- These are reserved even with extensions (e.g., `NUL.txt` is invalid)

**Additional Restrictions:**
- No trailing dots (`.`) or spaces (` `)
- Maximum file name length: 255 characters (NTFS)
- Maximum path length: 260 characters (MAX_PATH)

### UNC Path Support

**Format:** `\\server\share\path\to\file`

Our `WindowsPath` class fully parses and reconstructs UNC paths:
- `uncServer`: The server name (e.g., "fileserver")
- `uncShare`: The share name (e.g., "documents")
- `components`: Additional path components after the share

UNC paths are considered absolute paths.

### Path Separator Handling

Windows accepts both:
- **Backslash (`\`)**: Primary/canonical separator
- **Forward slash (`/`)**: Alternate separator

Our implementation:
1. Accepts both during parsing
2. Normalizes to backslash internally
3. Outputs using canonical backslash

### Case Sensitivity

Windows file systems are **case-insensitive** but **case-preserving**:
- Reserved name validation uses `sameAs:` for case-insensitive comparison
- File names like `NUL`, `nul`, `Nul` are all rejected

### Parsing vs Canonicalization Strategy

Our `WindowsPath` implementation uses a **minimal parsing, explicit canonicalization** strategy:

**During Parsing (`fromString:`)** — Minimal normalization to preserve user intent:

| Transformation | Applied? | Rationale |
|----------------|----------|-----------|
| `/` → `\` | ✅ Yes | Essential for internal consistency; both are valid input |
| Remove empty components | ✅ Yes | Result of splitting on separator |
| Resolve `.` | ❌ No | Preserves user intent |
| Resolve `..` | ❌ No | Preserves user intent; symlink semantics may differ |
| Uppercase drive letter | ❌ No | Preserves original input |

**During Canonicalization (`canonicalize`)** — Full normalization on demand:

| Transformation | Applied? | Rationale |
|----------------|----------|-----------|
| Resolve `.` | ✅ Yes | Removes current-directory references |
| Resolve `..` | ✅ Yes | Collapses parent-directory references |
| Uppercase drive letter | ✅ Yes | Consistent canonical form |
| UNC server/share | Preserved | Case-preserving per Windows behavior |

**Why This Design?**

1. **Round-trip fidelity**: `(WindowsPath fromString: 'c:/foo/../bar') asString` returns `'c:\foo\..\bar'` — the structure is preserved.

2. **User control**: Callers decide when to canonicalize. Some use cases (logging, display, debugging) benefit from seeing the original path form.

3. **Symlink correctness**: On file systems with symbolic links, `foo/../bar` may not equal `bar` if `foo` is a symlink. Deferring resolution avoids incorrect assumptions.

4. **Comparison still works**: Path equality (`=`) uses `namesEqual:` which is case-insensitive on Windows, so `c:\foo` equals `C:\FOO` even without canonicalization.

**Usage Examples:**
```smalltalk
"Parsing preserves structure"
path := WindowsPath fromString: 'c:/users/../admin/./docs'.
path asString.  "→ 'c:\users\..\admin\.\docs'"

"Explicit canonicalization when needed"
path canonicalize asString.  "→ 'C:\admin\docs'"
path canonicalString.  "→ 'C:\admin\docs' (convenience)"
```

### Windows Link Support

Windows NTFS supports three types of file system links, all implemented in our architecture:

| Link Type | Target | Cross-Volume | Network | Relative | Class |
|-----------|--------|--------------|---------|----------|-------|
| **Hard Link** | Files only | ❌ No | ❌ No | N/A | `File` methods |
| **Junction** | Directories only | ✅ Yes (local) | ❌ No | ❌ No (absolute only) | `Directory` methods |
| **Symbolic Link** | Files or directories | ✅ Yes | ✅ Yes (UNC) | ✅ Yes | `SymbolicLink` / `WindowsSymbolicLink` |

**Hard Links** (`File>>createHardLinkAt:`)
- Multiple directory entries pointing to the same file data
- All links are equal; deleting one doesn't affect others
- Created with `CreateHardLinkW` API
- Query link count with `File>>hardLinkCount`

**Junctions** (`Directory>>createJunctionAt:`)
- Directory-only soft links using NTFS reparse points
- Target must be an absolute local path
- Identified by `IO_REPARSE_TAG_MOUNT_POINT` (0xA0000003)
- Created with `DeviceIoControl` + `FSCTL_SET_REPARSE_POINT`

**Symbolic Links** (`WindowsSymbolicLink`)
- Most flexible link type, supporting files and directories
- Can use relative or absolute paths, including UNC network paths
- Identified by `IO_REPARSE_TAG_SYMLINK` (0xA000000C)
- Created with `CreateSymbolicLinkW` API
- Requires `SeCreateSymbolicLinkPrivilege` or Developer Mode (Win10 1607+)

**Link Detection and Resolution:**
```smalltalk
"Check if a path is a link"
aFile isSymbolicLink.     "→ true/false"
aFile isJunction.         "→ true/false"
aFile isHardLink.         "→ true/false (hardLinkCount > 1)"
aFile isLink.             "→ true if any link type"

"Get link target"
aFile linkTarget.         "→ immediate target path"
aFile resolvedTarget.     "→ final target (follows chain)"

"Check if link is broken"
aFile isBrokenLink.       "→ true if target doesn't exist"
```

---

## Class Hierarchy

```
Object
│
├── FileSystemNode [abstract]
│   │   "Common base for files, directories, and links"
│   │
│   ├── Directory [abstract]
│   │   │   "Container for files and subdirectories"
│   │   │
│   │   └── WindowsDirectory [concrete]
│   │           "Windows implementation with junction support"
│   │
│   ├── File [abstract]
│   │   │   "Represents a file's metadata"
│   │   │
│   │   └── WindowsFile [concrete]
│   │           "Windows implementation with hard link support"
│   │
│   └── SymbolicLink [abstract]
│       │   "Represents a symbolic link"
│       │
│       └── WindowsSymbolicLink [concrete]
│               "Windows symbolic link (not junction)"
│
├── FileSystem [abstract]
│   │   "Platform file system knowledge and factories"
│   │
│   └── WindowsFileSystem [concrete]
│           "Windows NTFS implementation with link support"
│
├── Path [abstract]
│   │   "Platform-independent path manipulation"
│   │
│   └── WindowsPath [concrete]
│           "Windows path semantics (backslash, drive letters, UNC)"
│
└── Stream
    └── PositionableStream
        └── WriteStream
            └── ReadWriteStream
                └── ExternalStream
                    └── FileStream [abstract]
                        │   "Abstract file streaming"
                        │
                        └── WindowsFileStream [concrete]
                                "Windows handle-based I/O"

Global Variables:
    Disk    "→ WindowsFileSystem default (the entry point)"
```

---

## Class Specifications

### FileSystemNode (Abstract)

The common base class for both files and directories.

```smalltalk
Object subclass: #FileSystemNode
    instanceVariableNames: 'pathName'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Abstract'!

FileSystemNode comment:
'I am the abstract base class for file system entries (files and directories).
I define the common protocol for accessing paths, testing existence, and
manipulating attributes.

Instance Variables:
    pathName    <String> full path to this entry'!

"-------------------- accessing --------------------"

!FileSystemNode methodsFor: 'accessing'!

name
    "Answer the local name (last component of path)"
    ^self path fileName!

fullName
    "Answer the full path as a String"
    ^pathName!

pathName
    "Answer the full path (alias for fullName)"
    ^pathName!

pathName: aString
    "Set the full path"
    pathName := aString!

parent
    "Answer the parent Directory"
    | parentPath |
    parentPath := self path parent.
    parentPath isNil ifTrue: [^nil].
    ^self fileSystem directoryClass on: parentPath asString!

path
    "Answer a Path object for this entry"
    ^self fileSystem pathFromString: pathName!

fileSystem
    "Answer the FileSystem for this entry"
    ^Disk! !

"-------------------- testing --------------------"

!FileSystemNode methodsFor: 'testing'!

exists
    "Answer whether this entry exists in the file system"
    self subclassResponsibility!

isFile
    "Answer whether this entry is a file"
    ^false!

isDirectory
    "Answer whether this entry is a directory"
    ^false! !

"-------------------- validation --------------------"

!FileSystemNode methodsFor: 'validation'!

isValidName
    "Answer whether this entry's name is valid for the file system"
    ^self fileSystem isValidFileName: self name!

hasValidPath
    "Answer whether the path is valid (non-nil, non-empty)"
    pathName isNil ifTrue: [^false].
    pathName isEmpty ifTrue: [^false].
    ^true!

validatePath
    "Signal an error if the path is invalid"
    self hasValidPath ifFalse: [
        self error: 'Invalid path: path is nil or empty']!

validateName
    "Signal an error if the name component is invalid"
    self isValidName ifFalse: [
        self error: 'Invalid file name: ', self name]!

validateExists
    "Signal an error if this entry does not exist"
    self exists ifFalse: [
        self error: 'File or directory not found: ', pathName]! !

"-------------------- attributes --------------------"

!FileSystemNode methodsFor: 'attributes'!

isHidden
    "Answer whether this entry is hidden"
    self subclassResponsibility!

isHidden: aBoolean
    "Set whether this entry is hidden"
    self subclassResponsibility!

isReadOnly
    "Answer whether this entry is read-only"
    self subclassResponsibility!

isReadOnly: aBoolean
    "Set whether this entry is read-only"
    self subclassResponsibility!

creationTime
    "Answer the creation time as a DateAndTime, or nil if not supported"
    self subclassResponsibility!

creationTime: aDateAndTime
    "Set the creation time"
    self subclassResponsibility!

modificationTime
    "Answer the last modification time as a DateAndTime"
    self subclassResponsibility!

modificationTime: aDateAndTime
    "Set the modification time"
    self subclassResponsibility!

accessTime
    "Answer the last access time as a DateAndTime, or nil if not supported"
    self subclassResponsibility!

accessTime: aDateAndTime
    "Set the access time"
    self subclassResponsibility! !

"-------------------- operations --------------------"

!FileSystemNode methodsFor: 'operations'!

delete
    "Delete this entry from the file system"
    self subclassResponsibility!

renameTo: newName
    "Rename this entry to newName (in same directory)"
    self subclassResponsibility!

moveTo: aDirectory
    "Move this entry to another directory"
    | newPath |
    newPath := aDirectory fullName,
               (String with: self fileSystem pathSeparator),
               self name.
    self primRename: pathName to: newPath.
    pathName := newPath!

copyTo: destinationPath
    "Copy this entry to destinationPath"
    self subclassResponsibility! !

"-------------------- printing --------------------"

!FileSystemNode methodsFor: 'printing'!

printOn: aStream
    aStream nextPutAll: self class name.
    aStream nextPutAll: ' on '.
    pathName printOn: aStream! !

"-------------------- private --------------------"

!FileSystemNode methodsFor: 'private'!

initPathName: aString
    "Initialize the path. Validates that path is not nil."
    aString isNil ifTrue: [
        self error: 'Cannot create ', self class name, ' with nil path'].
    pathName := aString! !

"-------------------- instance creation --------------------"

!FileSystemNode class methodsFor: 'instance creation'!

on: aPathString
    "Answer a new instance for the given path"
    ^self new initPathName: aPathString! !
```

---

### Directory (Abstract)

Abstract base class for directory operations.

```smalltalk
FileSystemNode subclass: #Directory
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Abstract'!

Directory comment:
'I am the abstract base class for directories.
I define the protocol for listing, creating, and managing files and subdirectories.

Concrete subclasses implement platform-specific directory operations.'!

"-------------------- testing --------------------"

!Directory methodsFor: 'testing'!

isDirectory
    "Answer true - I am a directory"
    ^true!

isEmpty
    "Answer whether this directory contains no entries"
    ^self entries isEmpty!

includes: aFileSystemNode
    "Answer whether this directory contains the given entry"
    ^self includesKey: aFileSystemNode name!

includesKey: aName
    "Answer whether this directory contains an entry with the given name"
    | fs |
    fs := self fileSystem.
    ^self entries anySatisfy: [:entry |
        fs namesEqual: entry name and: aName]! !

"-------------------- enumeration --------------------"

!Directory methodsFor: 'enumeration'!

entries
    "Answer all entries (files and subdirectories) as a collection"
    self subclassResponsibility!

files
    "Answer all File objects in this directory"
    ^self entries select: [:entry | entry isFile]!

directories
    "Answer all subdirectory Directory objects"
    ^self entries select: [:entry | entry isDirectory]!

fileNames
    "Answer the names of all files"
    ^self files collect: [:file | file name]!

directoryNames
    "Answer the names of all subdirectories"
    ^self directories collect: [:dir | dir name]!

do: aBlock
    "Evaluate aBlock for each entry"
    self entries do: aBlock!

filesDo: aBlock
    "Evaluate aBlock for each file"
    self files do: aBlock!

directoriesDo: aBlock
    "Evaluate aBlock for each subdirectory"
    self directories do: aBlock!

entriesMatching: pattern
    "Answer entries whose names match the glob pattern"
    ^self entries select: [:entry | pattern match: entry name]!

filesMatching: pattern
    "Answer files whose names match the glob pattern"
    ^self files select: [:file | pattern match: file name]! !

"-------------------- file access --------------------"

!Directory methodsFor: 'file access'!

fileNamed: aName
    "Answer a File object for the named file"
    ^self fileSystem fileClass on: (self fullNameFor: aName)!

directoryNamed: aName
    "Answer a Directory object for the named subdirectory"
    ^self fileSystem directoryClass on: (self fullNameFor: aName)!

entryNamed: aName
    "Answer a File or Directory object for the named entry"
    | entry |
    entry := self fileNamed: aName.
    entry exists ifTrue: [^entry].
    entry := self directoryNamed: aName.
    entry exists ifTrue: [^entry].
    ^nil!

file: aName
    "Answer a read/write FileStream on the named file"
    ^self fileSystem streamClass readWriteOn: (self fullNameFor: aName)!

oldFile: aName
    "Answer a read-only FileStream on an existing file"
    ^self fileSystem streamClass readOn: (self fullNameFor: aName)!

newFile: aName
    "Answer a write FileStream on a new file (create/truncate)"
    ^self fileSystem streamClass writeOn: (self fullNameFor: aName)! !

"-------------------- modification --------------------"

!Directory methodsFor: 'modification'!

createFile: aName
    "Create a new empty file with the given name, answer the File"
    | file stream |
    file := self fileNamed: aName.
    stream := self fileSystem streamClass writeOn: (self fullNameFor: aName).
    stream close.
    ^file!

createDirectory: aName
    "Create a new subdirectory with the given name, answer the Directory"
    | dir |
    dir := self directoryNamed: aName.
    dir create.
    ^dir!

create
    "Create this directory if it doesn't exist"
    self subclassResponsibility!

remove: aFileSystemNode
    "Remove the given entry from this directory"
    aFileSystemNode delete!

removeKey: aName
    "Remove the entry with the given name"
    (self entryNamed: aName) delete!

rename: aFileSystemNode to: newName
    "Rename an entry in this directory"
    aFileSystemNode renameTo: newName! !

"-------------------- validation --------------------"

!Directory methodsFor: 'validation'!

checkName: aName fixErrors: aBoolean
    "Check if aName is valid. If fixErrors is true, return a corrected name.
     If fixErrors is false and name is invalid, signal an error."
    | fs |
    fs := self fileSystem.
    (fs isValidFileName: aName) ifTrue: [^aName].
    aBoolean
        ifTrue: [^fs sanitizeFileName: aName]
        ifFalse: [self error: 'Invalid file name: ', aName]!

sanitizeFileName: aName
    "Answer a legal file name based on aName"
    ^self fileSystem sanitizeFileName: aName! !

"-------------------- path utilities --------------------"

!Directory methodsFor: 'path utilities'!

fullNameFor: aName
    "Answer the full path for a file/directory name in this directory"
    | sep |
    sep := self fileSystem pathSeparator.
    (pathName endsWith: (String with: sep))
        ifTrue: [^pathName, aName]
        ifFalse: [^pathName, (String with: sep), aName]! !

"-------------------- validation --------------------"

!Directory methodsFor: 'validation'!

checkName: aName fixErrors: fixFlag
    "Validate aName as a file name. If fixFlag is true, return a corrected name.
     If fixFlag is false and name is invalid, signal an error."
    | fs |
    aName isNil ifTrue: [
        fixFlag ifTrue: [^'untitled'] ifFalse: [self error: 'File name is nil']].
    aName isEmpty ifTrue: [
        fixFlag ifTrue: [^'untitled'] ifFalse: [self error: 'File name is empty']].
    fs := self fileSystem.
    (fs isValidFileName: aName) ifTrue: [^aName].
    fixFlag
        ifTrue: [^fs sanitizeFileName: aName]
        ifFalse: [self error: 'Invalid file name: ', aName]!

checkNameOfFile: aFile
    "Check aFile's name, signal error if invalid"
    ^self checkName: aFile name fixErrors: false!

checkNameOfFile: aFile fixErrors: aBoolean
    "Check aFile's name, optionally fix errors"
    ^self checkName: aFile name fixErrors: aBoolean!

isValidOldFileName: aName
    "Answer whether aName is valid and exists in this directory"
    ^(self fileSystem isValidFileName: aName) and: [self includesKey: aName]!

validateFileName: aName
    "Signal an error if aName is not a valid file name"
    (self fileSystem isValidFileName: aName) ifFalse: [
        self error: 'Invalid file name: ', aName]! !
```

---

### WindowsDirectory (Concrete)

Windows implementation of Directory.

```smalltalk
Directory subclass: #WindowsDirectory
    instanceVariableNames: ''
    classVariableNames: 'Separator'
    poolDictionaries: ''
    category: 'Files-Windows'!

WindowsDirectory comment:
'I am the Windows implementation of Directory.
I use Windows API primitives to implement directory operations.

Class Variables:
    Separator   <Character> the path separator (backslash)'!

"-------------------- class initialization --------------------"

!WindowsDirectory class methodsFor: 'class initialization'!

initialize
    "Initialize class variables"
    "WindowsDirectory initialize"
    Separator := $\.! !

"-------------------- instance creation --------------------"

!WindowsDirectory class methodsFor: 'instance creation'!

on: aPathString
    "Answer a new instance for the given path"
    ^self new initPathName: aPathString! !

"-------------------- testing --------------------"

!WindowsDirectory methodsFor: 'testing'!

exists
    "Answer whether this directory exists"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 16) ~= 0  "FILE_ATTRIBUTE_DIRECTORY"! !

"-------------------- enumeration --------------------"

!WindowsDirectory methodsFor: 'enumeration'!

entries
    "Answer all entries as File and Directory objects"
    | names |
    names := self primLookup: pathName pattern: '*'.
    names isNil ifTrue: [^#()].
    ^names collect: [:name |
        | fullPath attrs |
        fullPath := self fullNameFor: name.
        attrs := self primGetAttributes: fullPath.
        (attrs notNil and: [(attrs bitAnd: 16) ~= 0])
            ifTrue: [WindowsDirectory on: fullPath]
            ifFalse: [WindowsFile on: fullPath]]! !

"-------------------- modification --------------------"

!WindowsDirectory methodsFor: 'modification'!

create
    "Create this directory"
    self primCreateDirectory: pathName! !

"-------------------- operations --------------------"

!WindowsDirectory methodsFor: 'operations'!

delete
    "Delete this directory (must be empty)"
    self primDeleteDirectory: pathName!

renameTo: newName
    "Rename this directory"
    | newPath |
    newPath := self parent fullNameFor: newName.
    self primRename: pathName to: newPath.
    pathName := newPath!

copyTo: destinationPath
    "Copy this directory to destinationPath (not implemented)"
    self error: 'Directory copy not yet implemented'! !

"-------------------- file copying --------------------"

!WindowsDirectory methodsFor: 'file copying'!

retrieveFrom: aFileName1 copyTo: aFileName2
    "Copy from one file to another. Uses platform primitive when available,
     falls back to stream copying when primitive fails."
    | srcPath destPath |
    srcPath := self fullNameFor: aFileName1.
    destPath := self fullNameFor: aFileName2.
    ^self primCopyFile: srcPath to: destPath!

primCopyFile: srcPath to: destPath
    "Platform primitive for file copy. Falls back to stream copy if primitive fails."
    <primitive: 157>
    | f1 f2 |
    "Fallback: character-by-character stream copy"
    f1 := (Disk fileAt: srcPath) readStream.
    f2 := (Disk fileAt: destPath) writeStream.
    [f1 atEnd] whileFalse: [f2 nextPut: f1 next].
    f2 close.
    f1 close! !

"-------------------- attributes --------------------"

!WindowsDirectory methodsFor: 'attributes'!

isHidden
    "Answer whether this directory is hidden"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 2) ~= 0  "FILE_ATTRIBUTE_HIDDEN"!

isHidden: aBoolean
    "Set whether this directory is hidden"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 2]
        ifFalse: [newAttrs := attrs bitAnd: 2 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

isReadOnly
    "Answer whether this directory is read-only"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 1) ~= 0  "FILE_ATTRIBUTE_READONLY"!

isReadOnly: aBoolean
    "Set whether this directory is read-only"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 1]
        ifFalse: [newAttrs := attrs bitAnd: 1 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

isSystem
    "Answer whether this directory has the system attribute"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 4) ~= 0  "FILE_ATTRIBUTE_SYSTEM"!

isSystem: aBoolean
    "Set whether this directory has the system attribute"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 4]
        ifFalse: [newAttrs := attrs bitAnd: 4 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

creationTime
    "Answer the creation time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^self fileTimeToDateAndTime: (times at: 1)!

creationTime: aDateAndTime
    "Set the creation time"
    self primSetTimes: pathName
         creation: (self dateAndTimeToFileTime: aDateAndTime)
         modification: nil
         access: nil!

modificationTime
    "Answer the last modification time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^self fileTimeToDateAndTime: (times at: 2)!

modificationTime: aDateAndTime
    "Set the modification time"
    self primSetTimes: pathName
         creation: nil
         modification: (self dateAndTimeToFileTime: aDateAndTime)
         access: nil!

accessTime
    "Answer the last access time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^self fileTimeToDateAndTime: (times at: 3)!

accessTime: aDateAndTime
    "Set the access time"
    self primSetTimes: pathName
         creation: nil
         modification: nil
         access: (self dateAndTimeToFileTime: aDateAndTime)! !

"-------------------- private --------------------"

!WindowsDirectory methodsFor: 'private'!

fileTimeToDateAndTime: fileTime
    "Convert Windows FILETIME to DateAndTime"
    fileTime isNil ifTrue: [^nil].
    "Implementation converts 100-nanosecond intervals since 1601"
    ^DateAndTime fromWindowsFileTime: fileTime!

dateAndTimeToFileTime: aDateAndTime
    "Convert DateAndTime to Windows FILETIME"
    aDateAndTime isNil ifTrue: [^nil].
    ^aDateAndTime asWindowsFileTime! !

"-------------------- primitives --------------------"

!WindowsDirectory methodsFor: 'primitives'!

primLookup: path pattern: pattern
    "Answer an array of entry names matching pattern"
    <primitive: 146>
    ^nil!

primCreateDirectory: path
    "Create a directory"
    <primitive: 127>
    self error: 'Cannot create directory: ', path!

primDeleteDirectory: path
    "Delete a directory"
    <primitive: 129>
    self error: 'Cannot delete directory: ', path!

primRename: oldPath to: newPath
    "Rename a file or directory"
    <primitive: 126>
    self error: 'Cannot rename: ', oldPath!

primGetAttributes: path
    "Answer file attributes as integer, or nil if not found"
    <primitive: 154>
    ^nil!

primSetAttributes: path to: attrs
    "Set file attributes"
    <primitive: 155>
    self error: 'Cannot set attributes'!

primGetTimes: path
    "Answer array of #(creationTime modificationTime accessTime)"
    <primitive: 157>
    ^nil!

primSetTimes: path creation: cTime modification: mTime access: aTime
    "Set file times (nil means don't change)"
    <primitive: 158>
    self error: 'Cannot set times'! !
```

---

### File (Abstract)

Abstract base class for file metadata.

```smalltalk
FileSystemNode subclass: #File
    instanceVariableNames: 'binary'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Abstract'!

File comment:
'I am the abstract base class for files.
I represent file metadata and provide methods to create streams for reading/writing.

Instance Variables:
    binary  <Boolean> true if file should be accessed in binary mode'!

"-------------------- testing --------------------"

!File methodsFor: 'testing'!

isFile
    "Answer true - I am a file"
    ^true!

isBinary
    "Answer whether this file is in binary mode"
    ^binary == true!

isText
    "Answer whether this file is in text mode"
    ^binary ~~ true! !

"-------------------- accessing --------------------"

!File methodsFor: 'accessing'!

size
    "Answer the file size in bytes"
    self subclassResponsibility!

extension
    "Answer the file extension (without dot), or empty string"
    | name dotIndex |
    name := self name.
    dotIndex := name findLast: [:ch | ch = $.].
    dotIndex = 0 ifTrue: [^''].
    ^name copyFrom: dotIndex + 1 to: name size!

baseName
    "Answer the file name without extension"
    | name dotIndex |
    name := self name.
    dotIndex := name findLast: [:ch | ch = $.].
    dotIndex = 0 ifTrue: [^name].
    ^name copyFrom: 1 to: dotIndex - 1!

directory
    "Answer the parent Directory"
    ^self parent! !

"-------------------- modes --------------------"

!File methodsFor: 'modes'!

binary
    "Set binary mode for subsequent stream operations"
    binary := true!

text
    "Set text mode for subsequent stream operations"
    binary := false! !

"-------------------- streaming --------------------"

!File methodsFor: 'streaming'!

readStream
    "Answer a FileStream for reading"
    | stream |
    stream := self fileSystem streamClass readOn: pathName.
    binary == true ifTrue: [stream binary].
    ^stream!

writeStream
    "Answer a FileStream for writing (create/truncate)"
    | stream |
    stream := self fileSystem streamClass writeOn: pathName.
    binary == true ifTrue: [stream binary].
    ^stream!

readWriteStream
    "Answer a FileStream for read/write"
    | stream |
    stream := self fileSystem streamClass readWriteOn: pathName.
    binary == true ifTrue: [stream binary].
    ^stream!

appendStream
    "Answer a FileStream for appending"
    | stream |
    stream := self fileSystem streamClass appendOn: pathName.
    binary == true ifTrue: [stream binary].
    ^stream!

asFileStream
    "Answer a default FileStream (read/write)"
    ^self readWriteStream! !

"-------------------- content access --------------------"

!File methodsFor: 'content access'!

contents
    "Read and answer the entire file contents as a String"
    | stream contents |
    stream := self readStream.
    contents := stream contents.
    stream close.
    ^contents!

contents: aString
    "Write aString as the entire file contents"
    | stream |
    stream := self writeStream.
    stream nextPutAll: aString.
    stream close!

binaryContents
    "Read and answer the entire file contents as a ByteArray"
    | stream contents |
    self binary.
    stream := self readStream.
    contents := stream contents.
    stream close.
    ^contents!

binaryContents: aByteArray
    "Write aByteArray as the entire file contents"
    | stream |
    self binary.
    stream := self writeStream.
    stream nextPutAll: aByteArray.
    stream close! !

"-------------------- snapshot --------------------"

!File methodsFor: 'snapshot'!

beSnapshotFile
    "Register this file as the snapshot file for primitive 97"
    self subclassResponsibility! !

"-------------------- initialization --------------------"

!File methodsFor: 'initialization'!

initialize
    "Initialize instance variables"
    binary := false! !
```

---

### WindowsFile (Concrete)

Windows implementation of File.

```smalltalk
File subclass: #WindowsFile
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Windows'!

WindowsFile comment:
'I am the Windows implementation of File.
I use Windows API primitives to access file metadata and attributes.'!

"-------------------- class initialization --------------------"

!WindowsFile class methodsFor: 'class initialization'!

initialize
    "Initialize class variables"
    "WindowsFile initialize"
    "No class variables to initialize"! !

"-------------------- instance creation --------------------"

!WindowsFile class methodsFor: 'instance creation'!

on: aPathString
    "Answer a new instance for the given path"
    ^self new initPathName: aPathString; initialize! !

"-------------------- testing --------------------"

!WindowsFile methodsFor: 'testing'!

exists
    "Answer whether this file exists"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 16) = 0  "Not FILE_ATTRIBUTE_DIRECTORY"! !

"-------------------- accessing --------------------"

!WindowsFile methodsFor: 'accessing'!

size
    "Answer the file size in bytes"
    ^self primGetSize: pathName! !

"-------------------- operations --------------------"

!WindowsFile methodsFor: 'operations'!

delete
    "Delete this file"
    self primDeleteFile: pathName!

renameTo: newName
    "Rename this file"
    | newPath |
    newPath := self parent fullNameFor: newName.
    self primRename: pathName to: newPath.
    pathName := newPath!

copyTo: destinationPath
    "Copy this file to destinationPath"
    | source dest buffer bytesRead |
    source := self readStream.
    source binary.
    dest := self fileSystem streamClass writeOn: destinationPath.
    dest binary.
    buffer := ByteArray new: 8192.
    [source atEnd] whileFalse: [
        bytesRead := source readInto: buffer startingAt: 1 count: 8192.
        dest next: bytesRead putAll: buffer startingAt: 1].
    source close.
    dest close! !

"-------------------- attributes --------------------"

!WindowsFile methodsFor: 'attributes'!

isHidden
    "Answer whether this file is hidden"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 2) ~= 0!

isHidden: aBoolean
    "Set whether this file is hidden"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 2]
        ifFalse: [newAttrs := attrs bitAnd: 2 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

isReadOnly
    "Answer whether this file is read-only"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 1) ~= 0!

isReadOnly: aBoolean
    "Set whether this file is read-only"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 1]
        ifFalse: [newAttrs := attrs bitAnd: 1 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

isSystem
    "Answer whether this file has the system attribute"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 4) ~= 0!

isSystem: aBoolean
    "Set whether this file has the system attribute"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 4]
        ifFalse: [newAttrs := attrs bitAnd: 4 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

isArchive
    "Answer whether this file has the archive attribute"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^false].
    ^(attrs bitAnd: 32) ~= 0!

isArchive: aBoolean
    "Set whether this file has the archive attribute"
    | attrs newAttrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^self].
    aBoolean
        ifTrue: [newAttrs := attrs bitOr: 32]
        ifFalse: [newAttrs := attrs bitAnd: 32 bitInvert].
    self primSetAttributes: pathName to: newAttrs!

creationTime
    "Answer the creation time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^DateAndTime fromWindowsFileTime: (times at: 1)!

creationTime: aDateAndTime
    "Set the creation time"
    self primSetTimes: pathName
         creation: aDateAndTime asWindowsFileTime
         modification: nil
         access: nil!

modificationTime
    "Answer the last modification time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^DateAndTime fromWindowsFileTime: (times at: 2)!

modificationTime: aDateAndTime
    "Set the modification time"
    self primSetTimes: pathName
         creation: nil
         modification: aDateAndTime asWindowsFileTime
         access: nil!

accessTime
    "Answer the last access time"
    | times |
    times := self primGetTimes: pathName.
    times isNil ifTrue: [^nil].
    ^DateAndTime fromWindowsFileTime: (times at: 3)!

accessTime: aDateAndTime
    "Set the access time"
    self primSetTimes: pathName
         creation: nil
         modification: nil
         access: aDateAndTime asWindowsFileTime! !

"-------------------- snapshot --------------------"

!WindowsFile methodsFor: 'snapshot'!

beSnapshotFile
    "Register as snapshot file for primitive 97"
    <primitive: 156>
    ^self! !

"-------------------- primitives --------------------"

!WindowsFile methodsFor: 'primitives'!

primGetAttributes: path
    "Answer file attributes as integer, or nil if not found"
    <primitive: 154>
    ^nil!

primSetAttributes: path to: attrs
    "Set file attributes"
    <primitive: 155>
    self error: 'Cannot set attributes'!

primGetSize: path
    "Answer file size in bytes"
    <primitive: 156>
    ^0!

primGetTimes: path
    "Answer array of #(creationTime modificationTime accessTime)"
    <primitive: 157>
    ^nil!

primSetTimes: path creation: cTime modification: mTime access: aTime
    "Set file times (nil means don't change)"
    <primitive: 158>
    self error: 'Cannot set times'!

primDeleteFile: path
    "Delete a file"
    <primitive: 128>
    self error: 'Cannot delete file: ', path!

primRename: oldPath to: newPath
    "Rename a file"
    <primitive: 126>
    self error: 'Cannot rename: ', oldPath! !
```

---

### FileSystem (Abstract)

Abstract base class for platform file system knowledge.

```smalltalk
Object subclass: #FileSystem
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Abstract'!

FileSystem comment:
'I am the abstract base class representing a platform file system.
I provide:
- Concrete class factories for platform-specific implementations
- File system characteristics (case sensitivity, name constraints, etc.)
- Volume/drive information
- Directory access (root, current, home, temp)

Concrete subclasses implement platform-specific behavior.'!

"-------------------- class methods --------------------"

!FileSystem class methodsFor: 'instance access'!

default
    "Answer the default FileSystem instance"
    self subclassResponsibility! !

"-------------------- concrete class factories --------------------"

!FileSystem methodsFor: 'concrete class factories'!

pathClass
    "Answer the concrete Path class for this file system"
    self subclassResponsibility!

fileClass
    "Answer the concrete File class for this file system"
    self subclassResponsibility!

directoryClass
    "Answer the concrete Directory class for this file system"
    self subclassResponsibility!

streamClass
    "Answer the concrete FileStream class for this file system"
    self subclassResponsibility! !

"-------------------- directory access --------------------"

!FileSystem methodsFor: 'directory access'!

currentDirectory
    "Answer the current working directory"
    self subclassResponsibility!

rootDirectory
    "Answer the root directory of the default volume"
    self subclassResponsibility!

rootDirectoryFor: volumeDesignator
    "Answer the root directory for the specified volume"
    self subclassResponsibility!

temporaryDirectory
    "Answer the system temporary directory"
    self subclassResponsibility!

homeDirectory
    "Answer the user's home directory"
    self subclassResponsibility! !

"-------------------- path factory --------------------"

!FileSystem methodsFor: 'path factory'!

pathFromString: aString
    "Answer a Path object parsed from aString"
    ^self pathClass fromString: aString! !

"-------------------- naming semantics --------------------"

!FileSystem methodsFor: 'naming semantics'!

isCaseSensitive
    "Answer whether file names are case-sensitive"
    self subclassResponsibility!

isCasePreserving
    "Answer whether file name case is preserved"
    self subclassResponsibility!

namesEqual: name1 and: name2
    "Answer whether the two names refer to the same file"
    self isCaseSensitive
        ifTrue: [^name1 = name2]
        ifFalse: [^name1 sameAs: name2]!

hashForName: aName
    "Answer a hash value for the name, consistent with namesEqual:and:"
    self isCaseSensitive
        ifTrue: [^aName hash]
        ifFalse: [^aName asLowercase hash]!

normalizeName: aName
    "Answer the canonical form of a name for storage/comparison"
    self isCasePreserving
        ifTrue: [^aName]
        ifFalse: [^aName asUppercase]! !

"-------------------- name constraints --------------------"

!FileSystem methodsFor: 'name constraints'!

maxFileNameLength
    "Answer the maximum length of a single file name component"
    self subclassResponsibility!

maxPathLength
    "Answer the maximum length of a full path"
    self subclassResponsibility!

illegalCharacters
    "Answer a collection of characters not allowed in file names"
    self subclassResponsibility!

reservedNames
    "Answer a collection of reserved file names"
    self subclassResponsibility!

isValidFileName: aName
    "Answer whether aName is a valid file name"
    aName isEmpty ifTrue: [^false].
    aName size > self maxFileNameLength ifTrue: [^false].
    (self reservedNames anySatisfy: [:reserved |
        self namesEqual: aName and: reserved]) ifTrue: [^false].
    aName do: [:ch |
        (self illegalCharacters includes: ch) ifTrue: [^false]].
    ^true!

sanitizeFileName: aName
    "Answer a valid file name based on aName"
    | result |
    aName isNil ifTrue: [^'untitled'].
    aName isEmpty ifTrue: [^'untitled'].
    result := aName collect: [:ch |
        (self illegalCharacters includes: ch)
            ifTrue: [$_]
            ifFalse: [ch]].
    result size > self maxFileNameLength
        ifTrue: [result := result copyFrom: 1 to: self maxFileNameLength].
    (self reservedNames anySatisfy: [:reserved |
        self namesEqual: result and: reserved])
            ifTrue: [result := '_', result].
    ^result!

checkName: aName fixErrors: fixFlag
    "Validate aName as a file name. If fixFlag is true, return a corrected name.
     If fixFlag is false and name is invalid, signal an error."
    aName isNil ifTrue: [
        fixFlag ifTrue: [^'untitled'] ifFalse: [self error: 'File name is nil']].
    aName isEmpty ifTrue: [
        fixFlag ifTrue: [^'untitled'] ifFalse: [self error: 'File name is empty']].
    (self isValidFileName: aName) ifTrue: [^aName].
    fixFlag
        ifTrue: [^self sanitizeFileName: aName]
        ifFalse: [self error: 'Invalid file name: ', aName]! !

"-------------------- path characteristics --------------------"

!FileSystem methodsFor: 'path characteristics'!

pathSeparator
    "Answer the primary path separator character"
    self subclassResponsibility!

altPathSeparator
    "Answer the alternate path separator, or nil if none"
    self subclassResponsibility!

volumeSeparator
    "Answer the volume/drive separator, or nil if none"
    self subclassResponsibility!

isAbsolutePath: aString
    "Answer whether aString represents an absolute path"
    self subclassResponsibility!

supportsRelativePaths
    "Answer whether relative paths are supported"
    ^true!

supportsUNCPaths
    "Answer whether UNC paths (\\server\share) are supported"
    self subclassResponsibility! !

"-------------------- feature support --------------------"

!FileSystem methodsFor: 'feature support'!

supportsLongNames
    "Answer whether long file names are supported"
    self subclassResponsibility!

supportsUnicode
    "Answer whether Unicode file names are supported"
    self subclassResponsibility!

supportsSymbolicLinks
    "Answer whether symbolic links are supported"
    self subclassResponsibility!

supportsHardLinks
    "Answer whether hard links are supported"
    self subclassResponsibility!

supportsSparseFiles
    "Answer whether sparse files are supported"
    self subclassResponsibility!

supportsCompression
    "Answer whether transparent compression is supported"
    self subclassResponsibility!

supportsEncryption
    "Answer whether transparent encryption is supported"
    self subclassResponsibility!

supportsExtendedAttributes
    "Answer whether extended attributes are supported"
    self subclassResponsibility!

supportsAccessControlLists
    "Answer whether ACLs are supported"
    self subclassResponsibility! !

"-------------------- file attributes support --------------------"

!FileSystem methodsFor: 'file attributes support'!

supportsHiddenAttribute
    "Answer whether the hidden attribute is supported"
    self subclassResponsibility!

supportsReadOnlyAttribute
    "Answer whether the read-only attribute is supported"
    self subclassResponsibility!

supportsSystemAttribute
    "Answer whether the system attribute is supported"
    self subclassResponsibility!

supportsCreationTime
    "Answer whether creation time is tracked"
    self subclassResponsibility!

supportsModificationTime
    "Answer whether modification time is tracked"
    self subclassResponsibility!

supportsAccessTime
    "Answer whether access time is tracked"
    self subclassResponsibility! !

"-------------------- volume information --------------------"

!FileSystem methodsFor: 'volume information'!

volumes
    "Answer a collection of available volume designators"
    self subclassResponsibility!

volumeLabel
    "Answer the label of the default volume"
    self subclassResponsibility!

volumeLabelFor: volumeDesignator
    "Answer the label of the specified volume"
    self subclassResponsibility!

totalSpace
    "Answer the total space in bytes on the default volume"
    self subclassResponsibility!

freeSpace
    "Answer the free space in bytes on the default volume"
    self subclassResponsibility!

totalSpaceFor: volumeDesignator
    "Answer the total space in bytes on the specified volume"
    self subclassResponsibility!

freeSpaceFor: volumeDesignator
    "Answer the free space in bytes on the specified volume"
    self subclassResponsibility! !
```

---

### WindowsFileSystem (Concrete)

Windows implementation of FileSystem.

```smalltalk
FileSystem subclass: #WindowsFileSystem
    instanceVariableNames: ''
    classVariableNames: 'Default'
    poolDictionaries: ''
    category: 'Files-Windows'!

WindowsFileSystem comment:
'I am the Windows implementation of FileSystem.
I provide Windows-specific file system behavior including NTFS characteristics.

Class Variables:
    Default     <WindowsFileSystem> the singleton instance'!

"-------------------- class initialization --------------------"

!WindowsFileSystem class methodsFor: 'class initialization'!

initialize
    "Initialize class variables"
    "WindowsFileSystem initialize"
    Default := self new! !

"-------------------- instance access --------------------"

!WindowsFileSystem class methodsFor: 'instance access'!

default
    "Answer the singleton instance"
    Default isNil ifTrue: [Default := self new].
    ^Default! !

"-------------------- concrete class factories --------------------"

!WindowsFileSystem methodsFor: 'concrete class factories'!

pathClass
    "Answer WindowsPath"
    ^WindowsPath!

fileClass
    "Answer WindowsFile"
    ^WindowsFile!

directoryClass
    "Answer WindowsDirectory"
    ^WindowsDirectory!

streamClass
    "Answer WindowsFileStream"
    ^WindowsFileStream! !

"-------------------- directory access --------------------"

!WindowsFileSystem methodsFor: 'directory access'!

currentDirectory
    "Answer the current working directory"
    ^WindowsDirectory on: self primGetCurrentDirectory!

rootDirectory
    "Answer the root directory of the default volume (C:\)"
    ^self rootDirectoryFor: 'C:'!

rootDirectoryFor: volumeDesignator
    "Answer the root directory for the specified volume"
    | path |
    path := volumeDesignator.
    (path endsWith: '\') ifFalse: [path := path, '\'].
    ^WindowsDirectory on: path!

temporaryDirectory
    "Answer the system temporary directory"
    ^WindowsDirectory on: self primGetTempDirectory!

homeDirectory
    "Answer the user's home directory"
    ^WindowsDirectory on: self primGetHomeDirectory! !

"-------------------- naming semantics --------------------"

!WindowsFileSystem methodsFor: 'naming semantics'!

isCaseSensitive
    "Windows is case-insensitive"
    ^false!

isCasePreserving
    "Windows preserves case"
    ^true! !

"-------------------- name constraints --------------------"

!WindowsFileSystem methodsFor: 'name constraints'!

maxFileNameLength
    "NTFS maximum file name length"
    ^255!

maxPathLength
    "Traditional Windows maximum path length"
    ^260!

illegalCharacters
    "Characters not allowed in Windows file names"
    ^#($\ $/ $: $* $? $" $< $> $|)!

reservedNames
    "Reserved file names in Windows"
    ^#('CON' 'PRN' 'AUX' 'NUL'
       'COM1' 'COM2' 'COM3' 'COM4' 'COM5' 'COM6' 'COM7' 'COM8' 'COM9'
       'LPT1' 'LPT2' 'LPT3' 'LPT4' 'LPT5' 'LPT6' 'LPT7' 'LPT8' 'LPT9')!

isValidFileName: aName
    "Windows-specific validation including reserved names, trailing dots/spaces,
     and control characters (ASCII 0-31) per Microsoft documentation."
    | baseName |
    (super isValidFileName: aName) ifFalse: [^false].
    "Windows doesn't allow control characters (ASCII 0-31)"
    aName do: [:ch | ch asciiValue < 32 ifTrue: [^false]].
    "Windows doesn't allow trailing dots or spaces"
    (aName last = $. or: [aName last = $ ]) ifTrue: [^false].
    "Check base name without extension against reserved names"
    baseName := (aName includes: $.)
        ifTrue: [aName copyFrom: 1 to: (aName indexOf: $.) - 1]
        ifFalse: [aName].
    (self reservedNames anySatisfy: [:reserved |
        baseName sameAs: reserved]) ifTrue: [^false].
    ^true! !

"-------------------- path characteristics --------------------"

!WindowsFileSystem methodsFor: 'path characteristics'!

pathSeparator
    "Primary path separator is backslash"
    ^$\!

altPathSeparator
    "Forward slash also works"
    ^$/!

volumeSeparator
    "Colon separates drive letter from path"
    ^$:!

isAbsolutePath: aString
    "Test for absolute path (C:\ or \\server\share)"
    aString isEmpty ifTrue: [^false].
    "UNC path"
    (aString beginsWith: '\\') ifTrue: [^true].
    "Drive letter path"
    (aString size >= 3 and: [
        (aString at: 2) = $: and: [
            (aString at: 3) = $\ or: [(aString at: 3) = $/]]]) ifTrue: [^true].
    ^false!

supportsUNCPaths
    "Windows supports UNC paths"
    ^true! !

"-------------------- feature support --------------------"

!WindowsFileSystem methodsFor: 'feature support'!

supportsLongNames
    ^true!

supportsUnicode
    ^true!

supportsSymbolicLinks
    "Vista and later"
    ^true!

supportsHardLinks
    "NTFS only"
    ^true!

supportsSparseFiles
    "NTFS only"
    ^true!

supportsCompression
    "NTFS only"
    ^true!

supportsEncryption
    "NTFS EFS"
    ^true!

supportsExtendedAttributes
    "NTFS alternate data streams"
    ^true!

supportsAccessControlLists
    "NTFS ACLs"
    ^true! !

"-------------------- file attributes support --------------------"

!WindowsFileSystem methodsFor: 'file attributes support'!

supportsHiddenAttribute
    ^true!

supportsReadOnlyAttribute
    ^true!

supportsSystemAttribute
    ^true!

supportsCreationTime
    ^true!

supportsModificationTime
    ^true!

supportsAccessTime
    ^true! !

"-------------------- volume information --------------------"

!WindowsFileSystem methodsFor: 'volume information'!

volumes
    "Answer available drive letters"
    ^self primGetVolumes!

volumeLabel
    "Answer the label of the default volume"
    ^self volumeLabelFor: 'C:'!

volumeLabelFor: volumeDesignator
    "Answer the label of the specified volume"
    | info |
    info := self primGetVolumeInfo: volumeDesignator.
    info isNil ifTrue: [^''].
    ^info at: 1!

totalSpace
    ^self totalSpaceFor: 'C:'!

freeSpace
    ^self freeSpaceFor: 'C:'!

totalSpaceFor: volumeDesignator
    "Answer total space in bytes"
    | info |
    info := self primGetDiskSpace: volumeDesignator.
    info isNil ifTrue: [^0].
    ^info at: 1!

freeSpaceFor: volumeDesignator
    "Answer free space in bytes"
    | info |
    info := self primGetDiskSpace: volumeDesignator.
    info isNil ifTrue: [^0].
    ^info at: 2! !

"-------------------- primitives --------------------"

!WindowsFileSystem methodsFor: 'primitives'!

primGetVolumes
    "Answer array of drive letters"
    <primitive: 147>
    ^#('C:')!

primGetVolumeInfo: volumeDesignator
    "Answer #(label fileSystem) or nil"
    <primitive: 148>
    ^nil!

primGetDiskSpace: volumeDesignator
    "Answer #(totalBytes freeBytes) or nil"
    <primitive: 159>
    ^nil!

primGetCurrentDirectory
    "Answer current directory path"
    <primitive: 149>
    ^'.'!

primGetTempDirectory
    "Answer temp directory path"
    <primitive: 150>
    ^'C:\Temp'!

primGetHomeDirectory
    "Answer home directory path"
    <primitive: 151>
    ^'C:\Users'! !
```

---

### Path (Abstract)

Abstract base class for path manipulation.

```smalltalk
Object subclass: #Path
    instanceVariableNames: 'components'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Files-Abstract'!

Path comment:
'I am the abstract base class for file system paths.
I provide platform-independent path manipulation.

Instance Variables:
    components  <Array of String> path components'!

"-------------------- class methods --------------------"

!Path class methodsFor: 'instance creation'!

fromString: aString
    "Parse a path string and answer a Path instance.
     Delegates to Disk for the concrete class."
    ^Disk pathClass fromString: aString!

root
    "Answer a path representing the root directory"
    self subclassResponsibility!

separator
    "Answer the path separator character"
    self subclassResponsibility! !

"-------------------- accessing --------------------"

!Path methodsFor: 'accessing'!

components
    "Answer the path components"
    ^components!

fileName
    "Answer the last component (file name)"
    components isEmpty ifTrue: [^''].
    ^components last!

directoryPath
    "Answer a Path for the parent directory"
    components size <= 1 ifTrue: [^nil].
    ^self class withComponents: (components copyFrom: 1 to: components size - 1)!

extension
    "Answer the file extension (without dot), or empty string"
    | name dotIndex |
    name := self fileName.
    name isEmpty ifTrue: [^''].
    dotIndex := name findLast: [:ch | ch = $.].
    dotIndex = 0 ifTrue: [^''].
    ^name copyFrom: dotIndex + 1 to: name size!

baseName
    "Answer the file name without extension"
    | name dotIndex |
    name := self fileName.
    name isEmpty ifTrue: [^''].
    dotIndex := name findLast: [:ch | ch = $.].
    dotIndex = 0 ifTrue: [^name].
    ^name copyFrom: 1 to: dotIndex - 1! !

"-------------------- testing --------------------"

!Path methodsFor: 'testing'!

isAbsolute
    "Answer whether this is an absolute path"
    self subclassResponsibility!

isRelative
    "Answer whether this is a relative path"
    ^self isAbsolute not!

isRoot
    "Answer whether this path represents a root directory"
    self subclassResponsibility!

isEmpty
    "Answer whether this path is empty"
    ^components isEmpty! !

"-------------------- navigation --------------------"

!Path methodsFor: 'navigation'!

parent
    "Answer the parent Path, or nil if at root"
    ^self directoryPath!

/ aString
    "Answer a new Path with aString appended as a component"
    ^self class withComponents: (components copyWith: aString)!

resolve: aRelativePath
    "Resolve a relative path against this path"
    aRelativePath isAbsolute ifTrue: [^aRelativePath].
    ^self class withComponents: (components, aRelativePath components)! !

"-------------------- conversion --------------------"

!Path methodsFor: 'conversion'!

asString
    "Answer the path as a string"
    self subclassResponsibility!

canonicalize
    "Answer a new Path in canonical form for this platform.
     Canonical form includes:
     - Using the platform's preferred separator
     - Resolving . (current) and .. (parent) components
     - Removing redundant separators
     - Platform-specific normalization (e.g., case on case-insensitive systems)"
    self subclassResponsibility!

printOn: aStream
    "Print the path"
    aStream nextPutAll: self class name.
    aStream nextPutAll: '('.
    aStream nextPutAll: self asString.
    aStream nextPutAll: ')'!

directory
    "Answer a Directory for this path"
    ^Disk directoryClass on: self asString!

file
    "Answer a File for this path"
    ^Disk fileClass on: self asString! !

"-------------------- comparing --------------------"

!Path methodsFor: 'comparing'!

= aPath
    "Answer whether two paths are equal"
    | fs |
    self species = aPath species ifFalse: [^false].
    self components size = aPath components size ifFalse: [^false].
    fs := Disk.
    self components with: aPath components do: [:a :b |
        (fs namesEqual: a and: b) ifFalse: [^false]].
    ^true!

hash
    "Answer a hash value consistent with ="
    | fs |
    fs := Disk.
    ^components inject: 0 into: [:h :comp |
        h bitXor: (fs hashForName: comp)]! !

"-------------------- private --------------------"

!Path methodsFor: 'private'!

setComponents: anArray
    "Set the path components"
    components := anArray! !

"-------------------- instance creation --------------------"

!Path class methodsFor: 'private'!

withComponents: anArray
    "Answer a new instance with the given components"
    ^self new setComponents: anArray! !
```

---

### WindowsPath (Concrete)

Windows implementation of Path. Handles both forward slash (/) and backslash (\) as path separators per Microsoft documentation. Fully supports UNC paths (`\\server\share\path`) as well as drive-letter paths (`C:\folder\file`).

```smalltalk
Path subclass: #WindowsPath
    instanceVariableNames: 'driveLetter uncServer uncShare'
    classVariableNames: 'Separator'
    poolDictionaries: ''
    category: 'Files-Windows'!

WindowsPath comment:
'I represent a Windows file system path. I handle path parsing and manipulation
without any file I/O operations.

Instance Variables:
    driveLetter   <Character|nil> drive letter (C, D, etc.) or nil for UNC/relative paths
    uncServer     <String|nil> UNC server name (e.g., "server" from "\\server\share")
    uncShare      <String|nil> UNC share name (e.g., "share" from "\\server\share")
    components    <Array> path components (directories and filename after root)

Class Variables:
    Separator     <Character> the path separator (backslash on Windows)

Path Representations:
- Drive paths: C:\folder\file.txt (driveLetter=C, uncServer=nil, uncShare=nil)
- UNC paths: \\server\share\folder\file.txt (driveLetter=nil, uncServer="server", uncShare="share")
- Relative paths: folder\file.txt (driveLetter=nil, uncServer=nil, uncShare=nil)

Parsing vs Canonicalization:
During parsing (fromString:), I perform MINIMAL normalization to preserve user intent:
- Forward slashes (/) are converted to backslashes (\) for internal consistency
- Empty path components are removed

I deliberately DO NOT canonicalize during parsing:
- "." and ".." components are preserved as-is
- Drive letter case is preserved (c: stays lowercase)
- This allows round-tripping and retains the original path form

To get a fully canonical path, explicitly call #canonicalize which:
- Resolves "." (current directory) by removing
- Resolves ".." (parent directory) by removing with parent
- Uppercases drive letters for consistency

This design gives callers control over when normalization occurs.'!

"-------------------- class initialization --------------------"

!WindowsPath class methodsFor: 'class initialization'!

initialize
    "Initialize class variables"
    "WindowsPath initialize"
    Separator := $\.! !

"-------------------- class methods --------------------"

!WindowsPath class methodsFor: 'instance creation'!

fromString: aString
    "Parse a Windows path string"
    ^self new initializeFromString: aString!

separator
    "Answer the path separator character"
    ^Separator!

isSeparator: aCharacter
    "Answer whether aCharacter is a path separator.
     Windows accepts both backslash and forward slash."
    ^aCharacter = $\ or: [aCharacter = $/]! !

"-------------------- initialization --------------------"

!WindowsPath methodsFor: 'initialization'!

initializeFromString: aString
    "Parse a Windows path string per Microsoft UNC conventions.
     Windows accepts both backslash and forward slash as separators.
     UNC format: \\server\share\path\to\file"
    | parts startIndex normalized |
    uncServer := nil.
    uncShare := nil.

    aString isEmpty ifTrue: [
        driveLetter := nil.
        components := #().
        ^self].

    "Normalize forward slashes to backslashes for consistent parsing"
    normalized := aString copyReplaceAll: '/' with: '\'.

    "Check for drive letter (C:)"
    (normalized size >= 2 and: [(normalized at: 2) = $:]) ifTrue: [
        driveLetter := normalized at: 1.
        startIndex := 3.
        "Skip leading separator after drive"
        (normalized size >= 3 and: [(self class isSeparator: (normalized at: 3))])
            ifTrue: [startIndex := 4]]
    ifFalse: [
        "Check for UNC path (\\server\share)"
        (normalized size >= 2 and: [(self class isSeparator: (normalized at: 1))
            and: [(self class isSeparator: (normalized at: 2))]])
            ifTrue: [
                "Parse UNC: \\server\share\rest"
                driveLetter := nil.
                parts := (normalized copyFrom: 3 to: normalized size)
                    findTokens: (String with: Separator).
                parts := parts select: [:p | p notEmpty].
                parts size >= 1 ifTrue: [uncServer := parts first].
                parts size >= 2 ifTrue: [uncShare := parts second].
                "Components are everything after server\share"
                parts size > 2
                    ifTrue: [components := parts copyFrom: 3 to: parts size]
                    ifFalse: [components := #()].
                ^self]
            ifFalse: [
                driveLetter := nil.
                startIndex := 1.
                "Skip leading separator for absolute paths"
                (self class isSeparator: (normalized at: 1)) ifTrue: [startIndex := 2]]].

    "Parse remaining components - split on backslash"
    startIndex > normalized size
        ifTrue: [components := #()]
        ifFalse: [
            parts := (normalized copyFrom: startIndex to: normalized size)
                findTokens: (String with: Separator).
            components := parts select: [:p | p notEmpty]].
    ^self! !

"-------------------- accessing --------------------"

!WindowsPath methodsFor: 'accessing'!

driveLetter
    "Answer the drive letter or nil."
    ^driveLetter!

uncServer
    "Answer the UNC server name or nil."
    ^uncServer!

uncShare
    "Answer the UNC share name or nil."
    ^uncShare!

components
    "Answer the path components."
    ^components!

fileName
    "Answer the last component (filename)."
    components isEmpty ifTrue: [^''].
    ^components last!

extension
    "Answer the file extension or empty string."
    | name dotIndex |
    name := self fileName.
    dotIndex := name findLast: [:c | c = $.].
    dotIndex = 0 ifTrue: [^''].
    ^name copyFrom: dotIndex + 1 to: name size!

directoryPath
    "Answer a new path without the filename."
    | newPath |
    components size <= 1 ifTrue: [
        self isUNC ifTrue: [
            ^self class fromString: '\\', uncServer, '\', uncShare].
        ^self class fromString: (driveLetter ifNil: [''] ifNotNil: [driveLetter asString, ':'])].
    newPath := self class new.
    newPath setDriveLetter: driveLetter uncServer: uncServer uncShare: uncShare components: components allButLast.
    ^newPath!

asString
    "Answer the path as a string per Microsoft UNC conventions."
    | stream |
    stream := WriteStream on: (String new: 100).
    self isUNC ifTrue: [
        "UNC path: \\server\share\path\..."
        stream nextPut: Separator; nextPut: Separator.
        stream nextPutAll: uncServer.
        stream nextPut: Separator; nextPutAll: uncShare]
    ifFalse: [
        driveLetter notNil ifTrue: [
            stream nextPut: driveLetter; nextPut: $:]].
    components do: [:comp |
        stream nextPut: Separator; nextPutAll: comp].
    stream position = 0 ifTrue: [^'.'].
    ^stream contents! !

"-------------------- testing --------------------"

!WindowsPath methodsFor: 'testing'!

isAbsolute
    "Answer whether this is an absolute path.
     Both drive paths (C:\) and UNC paths (\\server\share) are absolute."
    ^driveLetter notNil or: [self isUNC]!

isRelative
    "Answer whether this is a relative path."
    ^self isAbsolute not!

isEmpty
    "Answer whether this path is empty."
    ^driveLetter isNil and: [uncServer isNil and: [components isEmpty]]!

isRoot
    "Answer whether this path represents a root directory.
     For drive paths: C:\ is root. For UNC paths: \\server\share is root."
    ^components isEmpty and: [self isAbsolute]!

isUNC
    "Answer whether this is a UNC path (\\server\share)."
    ^uncServer notNil and: [uncShare notNil]! !

"-------------------- conversion --------------------"

!WindowsPath methodsFor: 'conversion'!

canonicalize
    "Answer a new WindowsPath in canonical Windows form.
     Canonical form:
     - Uses backslash separator (already normalized during parsing)
     - Resolves . (current directory) components by removing them
     - Resolves .. (parent directory) components by removing parent
     - Drive letter is uppercased
     - UNC server and share names are preserved as-is (case-preserving)"
    | newComponents result |
    newComponents := OrderedCollection new.

    "Process each component, resolving . and .."
    components do: [:comp |
        comp = '.'
            ifTrue: ["Skip current directory reference"]
            ifFalse: [
                comp = '..'
                    ifTrue: [
                        "Go up one level if possible"
                        newComponents isEmpty
                            ifFalse: [newComponents removeLast]]
                    ifFalse: [newComponents add: comp]]].

    result := self class new.
    result setDriveLetter: (driveLetter ifNotNil: [driveLetter asUppercase])
           uncServer: uncServer
           uncShare: uncShare
           components: newComponents asArray.
    ^result!

canonicalString
    "Answer the path as a canonical string.
     Convenience method combining canonicalize and asString."
    ^self canonicalize asString! !

"-------------------- navigation --------------------"

!WindowsPath methodsFor: 'operations'!

/ aString
    "Answer a new path with aString appended as a component."
    | newPath newComponents |
    newComponents := components copyWith: aString.
    newPath := self class new.
    newPath setDriveLetter: driveLetter uncServer: uncServer uncShare: uncShare components: newComponents.
    ^newPath!

withExtension: extString
    "Answer a new path with the given extension."
    | baseName newName |
    components isEmpty ifTrue: [^self].
    baseName := self fileName.
    (baseName includes: $.)
        ifTrue: [baseName := baseName copyFrom: 1 to: (baseName findLast: [:c | c = $.]) - 1].
    newName := baseName, '.', extString.
    ^self directoryPath / newName! !

"-------------------- private --------------------"

!WindowsPath methodsFor: 'private'!

setDriveLetter: drive components: comps
    "Set drive letter and components (for backward compatibility)."
    driveLetter := drive.
    uncServer := nil.
    uncShare := nil.
    components := comps.
    ^self!

setDriveLetter: drive uncServer: server uncShare: share components: comps
    "Set all path components including UNC server and share."
    driveLetter := drive.
    uncServer := server.
    uncShare := share.
    components := comps.
    ^self! !
```

---

### FileStream (Abstract)

Abstract base class for file streaming, inherits from ExternalStream.

```smalltalk
ExternalStream subclass: #FileStream
    instanceVariableNames: 'file rwmode'
    classVariableNames: ''
    poolDictionaries: 'FilePool'
    category: 'Files-Streams'!

FileStream comment:
'I am the abstract base class for file streams.
I provide the protocol for streaming file I/O.

Instance Variables:
    file    <File or String> the file being streamed
    rwmode  <Integer> read/write mode flags

Pool Variables (from FilePool):
    Read    = 1
    Write   = 2
    Shorten = 4'!

"-------------------- class methods --------------------"

!FileStream class methodsFor: 'instance creation'!

fileNamed: fileDesignator
    "Answer a read/write stream on the named file"
    ^Disk file: fileDesignator!

oldFileNamed: fileDesignator
    "Answer a read stream on an existing file"
    ^Disk oldFile: fileDesignator!

newFileNamed: fileDesignator
    "Answer a write stream on a new file"
    ^Disk newFile: fileDesignator!

on: aFileOrPath
    "Answer a stream on the given File or path"
    (aFileOrPath isKindOf: File)
        ifTrue: [^aFileOrPath asFileStream]
        ifFalse: [^self fileNamed: aFileOrPath]! !

"-------------------- accessing --------------------"

!FileStream methodsFor: 'accessing'!

file
    "Answer the File object"
    ^file!

name
    "Answer the file name"
    file isNil ifTrue: [^''].
    (file isKindOf: String) ifTrue: [^file].
    ^file name!

fullName
    "Answer the full path"
    file isNil ifTrue: [^''].
    (file isKindOf: String) ifTrue: [^file].
    ^file fullName! !

"-------------------- testing --------------------"

!FileStream methodsFor: 'testing'!

atEnd
    "Answer whether at end of file"
    self subclassResponsibility!

closed
    "Answer whether the stream is closed"
    self subclassResponsibility!

writing
    "Answer whether writing is allowed"
    rwmode isNil ifTrue: [^false].
    ^(rwmode bitAnd: Write) ~= 0! !

"-------------------- positioning --------------------"

!FileStream methodsFor: 'positioning'!

position
    "Answer the current position"
    self subclassResponsibility!

position: anInteger
    "Set the current position"
    self subclassResponsibility!

size
    "Answer the file size"
    self subclassResponsibility!

reset
    "Position to the beginning"
    self position: 0!

setToEnd
    "Position to the end"
    self position: self size!

skip: anInteger
    "Move position by anInteger"
    self position: self position + anInteger! !

"-------------------- reading --------------------"

!FileStream methodsFor: 'reading'!

next
    "Answer the next element"
    self subclassResponsibility!

next: anInteger
    "Answer the next anInteger elements"
    | result |
    result := String new: anInteger.
    1 to: anInteger do: [:i |
        result at: i put: self next].
    ^result!

peek
    "Answer the next element without advancing"
    self subclassResponsibility!

peekFor: anObject
    "Answer true and advance if next element equals anObject"
    self peek = anObject
        ifTrue: [self next. ^true]
        ifFalse: [^false]!

contents
    "Answer all remaining contents"
    | stream |
    stream := WriteStream on: (String new: 100).
    [self atEnd] whileFalse: [stream nextPut: self next].
    ^stream contents!

contentsOfEntireFile
    "Answer the entire file contents"
    | contents |
    self reset.
    contents := self contents.
    self close.
    ^contents! !

"-------------------- writing --------------------"

!FileStream methodsFor: 'writing'!

nextPut: anObject
    "Write anObject"
    self subclassResponsibility!

nextPutAll: aCollection
    "Write all elements of aCollection"
    aCollection do: [:each | self nextPut: each].
    ^aCollection!

cr
    "Write a carriage return"
    self nextPut: Character cr! !

"-------------------- file modes --------------------"

!FileStream methodsFor: 'file modes'!

readOnly
    "Set read-only mode"
    rwmode := Read!

readWrite
    "Set read/write mode"
    rwmode := Read bitOr: Write!

readWriteShorten
    "Set read/write mode with truncation on close"
    rwmode := Read bitOr: Write bitOr: Shorten!

binary
    "Set binary mode"
    self subclassResponsibility!

text
    "Set text mode"
    self subclassResponsibility! !

"-------------------- file operations --------------------"

!FileStream methodsFor: 'file operations'!

close
    "Close the stream"
    self subclassResponsibility!

flush
    "Flush any buffered data"
    self subclassResponsibility!

shorten
    "Truncate the file at current position"
    self subclassResponsibility! !

"-------------------- fileIn/Out --------------------"

!FileStream methodsFor: 'fileIn/Out'!

fileIn
    "File in the contents of this stream"
    | val |
    Transcript cr; show: 'Filing in from: ', self name; cr.
    self readOnly.
    Cursor read showWhile: [
        [self atEnd] whileFalse: [
            self skipSeparators.
            val := (self peekFor: $!)
                ifTrue: [(Compiler evaluate: self nextChunk logged: false)
                         scanFrom: self]
                ifFalse: [Compiler evaluate: self nextChunk logged: true]].
        self close].
    ^val!

nextChunk
    "Read a chunk delimited by !"
    | stream char terminator |
    terminator := $!.
    stream := WriteStream on: (String new: 200).
    self skipSeparators.
    [self atEnd] whileFalse: [
        char := self next.
        char = terminator
            ifTrue: [(self peekFor: terminator)
                ifTrue: [stream nextPut: char]
                ifFalse: [^stream contents]]
            ifFalse: [stream nextPut: char]].
    ^stream contents!

nextChunkPut: aString
    "Write a chunk with ! terminator"
    aString do: [:char |
        char = $! ifTrue: [self nextPut: $!].
        self nextPut: char].
    self nextPut: $!.
    self cr.
    ^aString!

skipSeparators
    "Skip whitespace"
    [self atEnd not and: [self peek isSeparator]]
        whileTrue: [self next]!

timeStamp
    "Write a timestamp"
    | stream |
    stream := WriteStream on: (String new: 50).
    Smalltalk timeStamp: stream.
    self nextChunkPut: stream contents.
    self cr!

emphasis: ignored
    "No-op for plain file streams"
    ^self! !
```

---

### WindowsFileStream (Concrete)

Windows implementation of FileStream.

```smalltalk
FileStream subclass: #WindowsFileStream
    instanceVariableNames: 'pathName handle position mode closed'
    classVariableNames: 'ReadMode WriteMode AppendMode ReadWriteMode'
    poolDictionaries: ''
    category: 'Files-Windows'!

WindowsFileStream comment:
'I am the Windows implementation of FileStream.
I use Windows file handles for direct file I/O.

Instance Variables:
    pathName    <String> full path to the file
    handle      <Integer> Windows file handle
    position    <Integer> current position in file
    mode        <Integer> access mode
    closed      <Boolean> true if stream is closed

Class Variables:
    ReadMode        = 1
    WriteMode       = 2
    AppendMode      = 3
    ReadWriteMode   = 4'!

"-------------------- class initialization --------------------"

!WindowsFileStream class methodsFor: 'class initialization'!

initialize
    "Initialize class variables"
    "WindowsFileStream initialize"
    ReadMode := 1.
    WriteMode := 2.
    AppendMode := 3.
    ReadWriteMode := 4! !

"-------------------- instance creation --------------------"

!WindowsFileStream class methodsFor: 'instance creation'!

readOn: aPathString
    "Open for reading"
    ^self new initPathName: aPathString; open: ReadMode!

writeOn: aPathString
    "Open for writing (create/truncate)"
    ^self new initPathName: aPathString; open: WriteMode!

appendOn: aPathString
    "Open for appending"
    ^self new initPathName: aPathString; open: AppendMode!

readWriteOn: aPathString
    "Open for read/write"
    ^self new initPathName: aPathString; open: ReadWriteMode! !

"-------------------- initialization --------------------"

!WindowsFileStream methodsFor: 'initialization'!

initPathName: aString
    "Initialize the path"
    pathName := aString.
    position := 0.
    closed := true!

open: modeValue
    "Open the file with specified mode"
    | accessMode shareMode createMode |
    mode := modeValue.

    modeValue = ReadMode ifTrue: [
        accessMode := 1.  "GENERIC_READ"
        shareMode := 1.   "FILE_SHARE_READ"
        createMode := 3]. "OPEN_EXISTING"

    modeValue = WriteMode ifTrue: [
        accessMode := 2.  "GENERIC_WRITE"
        shareMode := 0.
        createMode := 2]. "CREATE_ALWAYS"

    modeValue = AppendMode ifTrue: [
        accessMode := 2.
        shareMode := 0.
        createMode := 4]. "OPEN_ALWAYS"

    modeValue = ReadWriteMode ifTrue: [
        accessMode := 3.  "GENERIC_READ | GENERIC_WRITE"
        shareMode := 0.
        createMode := 4]. "OPEN_ALWAYS"

    handle := self primOpen: pathName
                   accessMode: accessMode
                   shareMode: shareMode
                   createMode: createMode.

    (handle isNil or: [handle < 0])
        ifTrue: [self error: 'Cannot open file: ', pathName].

    closed := false.
    position := 0.

    modeValue = AppendMode ifTrue: [self setToEnd].
    ^self! !

"-------------------- accessing --------------------"

!WindowsFileStream methodsFor: 'accessing'!

name
    "Answer the file name"
    | lastSep |
    lastSep := pathName findLast: [:c | c = $\ or: [c = $/]].
    lastSep = 0 ifTrue: [^pathName].
    ^pathName copyFrom: lastSep + 1 to: pathName size!

fullName
    "Answer the full path"
    ^pathName! !

"-------------------- testing --------------------"

!WindowsFileStream methodsFor: 'testing'!

atEnd
    "Answer whether at end of file"
    ^position >= self size!

closed
    "Answer whether the stream is closed"
    ^closed! !

"-------------------- positioning --------------------"

!WindowsFileStream methodsFor: 'positioning'!

position
    "Answer the current position"
    ^position!

position: anInteger
    "Set the current position"
    closed ifTrue: [self reopen].
    self primSeek: handle offset: anInteger origin: 0.
    position := anInteger!

size
    "Answer the file size"
    | attrs |
    attrs := self primGetAttributes: pathName.
    attrs isNil ifTrue: [^0].
    ^attrs at: 1! !

"-------------------- reading --------------------"

!WindowsFileStream methodsFor: 'reading'!

next
    "Read and return the next character"
    | byte |
    closed ifTrue: [self reopen].
    self atEnd ifTrue: [^nil].
    byte := self primReadByte: handle at: position.
    byte < 0 ifTrue: [^nil].
    position := position + 1.
    ^Character value: byte!

peek
    "Answer the next character without advancing"
    | byte |
    closed ifTrue: [self reopen].
    self atEnd ifTrue: [^nil].
    byte := self primReadByte: handle at: position.
    byte < 0 ifTrue: [^nil].
    ^Character value: byte! !

"-------------------- writing --------------------"

!WindowsFileStream methodsFor: 'writing'!

nextPut: aCharacter
    "Write a character"
    | byte |
    closed ifTrue: [self reopen].
    byte := aCharacter asciiValue.
    self primWriteByte: handle at: position value: byte.
    position := position + 1.
    ^aCharacter! !

"-------------------- file modes --------------------"

!WindowsFileStream methodsFor: 'file modes'!

binary
    "Set binary mode (no-op on Windows)"
    ^self!

text
    "Set text mode (no-op on Windows)"
    ^self! !

"-------------------- file operations --------------------"

!WindowsFileStream methodsFor: 'file operations'!

close
    "Close the file"
    closed ifTrue: [^self].
    handle notNil ifTrue: [
        self primClose: handle.
        handle := nil].
    closed := true!

flush
    "Flush buffered data (no-op for direct I/O)"
    ^self!

shorten
    "Truncate the file at current position"
    closed ifTrue: [^self].
    self primTruncate: handle!

reopen
    "Reopen a closed file"
    | savedPos |
    closed ifFalse: [^self].
    savedPos := position.
    mode isNil ifTrue: [mode := ReadWriteMode].
    self open: mode.
    self position: savedPos! !

"-------------------- primitives --------------------"

!WindowsFileStream methodsFor: 'primitives'!

primOpen: path accessMode: access shareMode: share createMode: create
    "Open a file and return the handle"
    <primitive: 121>
    ^nil!

primClose: aHandle
    "Close a file handle"
    <primitive: 122>
    ^nil!

primSeek: aHandle offset: offset origin: origin
    "Seek to position"
    <primitive: 124>
    ^-1!

primReadByte: aHandle at: pos
    "Read a byte at position"
    <primitive: 132>
    ^-1!

primWriteByte: aHandle at: pos value: byte
    "Write a byte at position"
    <primitive: 134>
    ^byte!

primTruncate: aHandle
    "Truncate file at current position"
    <primitive: 140>
    ^nil!

primGetAttributes: path
    "Get file attributes"
    <primitive: 154>
    ^nil! !
```

---

## Initialization and Cold Start

### Initialization Order

The classes must be initialized in dependency order:

```smalltalk
"1. Path class (no dependencies)"
WindowsPath initialize!

"2. FileSystem class (depends on Path)"
WindowsFileSystem initialize!

"3. Directory class (depends on FileSystem)"
WindowsDirectory initialize!

"4. File class (depends on FileSystem)"
WindowsFile initialize!

"5. FileStream class (depends on FileSystem)"
WindowsFileStream initialize!

"6. Set up Disk global (depends on WindowsFileSystem)"
Smalltalk at: #Disk put: WindowsFileSystem default!
```

### Adding to Cold Start Bundle

Add these init expressions to `Smalltalk-80.st` after the class definitions:

```smalltalk
"==================== File System Initialization ===================="

WindowsPath initialize!
WindowsFileSystem initialize!
WindowsDirectory initialize!
WindowsFile initialize!
WindowsFileStream initialize!

"Install Disk as the file system entry point"
Smalltalk at: #Disk put: WindowsFileSystem default!
```

---

## Usage Examples

### Basic File Operations

```smalltalk
"Create and write to a file"
| stream |
stream := Disk file: 'test.txt'.
stream nextPutAll: 'Hello, World!'.
stream close.

"Read from a file"
| stream contents |
stream := Disk oldFile: 'test.txt'.
contents := stream contentsOfEntireFile.
Transcript show: contents.

"Using File object"
| file |
file := Disk fileNamed: 'test.txt'.
file exists ifTrue: [
    Transcript show: 'Size: ', file size printString; cr.
    Transcript show: 'Modified: ', file modificationTime printString].
```

### Directory Operations

```smalltalk
"List files in current directory"
Disk currentDirectory filesDo: [:file |
    Transcript show: file name; cr].

"Create a subdirectory"
| dir |
dir := Disk currentDirectory createDirectory: 'newFolder'.

"Find files matching pattern"
Disk currentDirectory filesMatching: '*.txt' do: [:file |
    Transcript show: file name; cr].
```

### Path Manipulation

```smalltalk
"Parse and manipulate paths"
| path |
path := Path fromString: 'C:\Users\Public\Documents'.
Transcript show: path fileName; cr.        "Documents"
Transcript show: path parent asString; cr. "C:\Users\Public"

"Build paths"
| basePath fullPath |
basePath := Path fromString: 'C:\Projects'.
fullPath := basePath / 'MyProject' / 'src' / 'main.st'.
Transcript show: fullPath asString; cr.
```

### File Attributes

```smalltalk
"Check and modify attributes"
| file |
file := Disk fileNamed: 'secret.txt'.
file isHidden: true.
file isReadOnly: true.

"Get timestamps"
Transcript show: 'Created: ', file creationTime printString; cr.
Transcript show: 'Modified: ', file modificationTime printString; cr.
```

### File System Information

```smalltalk
"Query file system properties"
Transcript show: 'Case sensitive: ', Disk isCaseSensitive printString; cr.
Transcript show: 'Max name length: ', Disk maxFileNameLength printString; cr.
Transcript show: 'Free space: ', Disk freeSpace printString, ' bytes'; cr.

"List volumes"
Disk volumes do: [:vol |
    Transcript show: vol, ' - ', (Disk volumeLabelFor: vol); cr].
```

---

## Migration from Legacy System

### Removed Classes (Version 1.4)

The following legacy classes have been **completely removed** from the codebase:

| Class | Status | Replacement |
|-------|--------|-------------|
| `FilePage` | **REMOVED** | Use `WindowsFileStream` for byte-stream access |
| Legacy `File` (Object subclass) | **REMOVED** | Use `File` → `WindowsFile` (FileSystemNode subclass) |
| `FileDirectory` | **REMOVED** | Use `Disk` (WindowsFileSystem) or `WindowsDirectory` |

### Current Class Hierarchy

**Abstract Classes** (program to these):
- `FileSystem` → entry point, platform knowledge
- `FileSystemNode` → common file/directory protocol
  - `File` → file-specific operations
  - `Directory` → directory-specific operations
  - `SymbolicLink` → link operations
- `Path` → path manipulation

**Concrete Windows Classes** (instantiate these via Disk):
- `WindowsFileSystem` → Windows file system operations
- `WindowsFile` → Windows file implementation
- `WindowsDirectory` → Windows directory implementation
- `WindowsSymbolicLink` → Windows symbolic link implementation
- `WindowsPath` → Windows path handling
- `WindowsFileStream` → Windows file I/O streams

### Global Variables

| Variable | Value | Purpose |
|----------|-------|---------|
| `Disk` | `WindowsFileSystem default` | Entry point to file system |

### API Usage

**Modern API (recommended):**
```smalltalk
"Get files and directories through Disk"
file := Disk fileNamed: 'C:\path\to\file.txt'.
dir := Disk directoryAt: 'C:\path\to\dir'.

"Create paths"
path := Disk pathFromString: 'C:\path\to\file.txt'.

"Parse file designators"
dir := Disk directoryFromName: 'C:\path\file.txt' setFileName: [:name | localName := name].

"File streams (through FileStream class methods)"
stream := FileStream fileNamed: 'path'.    "read/write"
stream := FileStream newFileNamed: 'path'. "create/truncate"
stream := FileStream oldFileNamed: 'path'. "read only"
```

**Legacy FileStream class methods still work** (delegate to WindowsFileStream):
- `FileStream fileNamed:` → `WindowsFileStream readWriteOn:`
- `FileStream newFileNamed:` → `WindowsFileStream writeOn:`
- `FileStream oldFileNamed:` → `WindowsFileStream readOn:`

### Code Migration Notes

1. **FileDirectory references**: Replace with `Disk` or appropriate abstract class
2. **FilePage references**: Remove entirely; use stream-based I/O
3. **Direct WindowsFile/WindowsDirectory references**: Use `Disk fileNamed:`/`Disk directoryAt:` instead
4. **directoryFromName:setFileName:**: Now available on `FileSystem` (and thus `Disk`)

---

## Appendix: Primitive Numbers

| Primitive | Purpose |
|-----------|---------|
| 121 | File open |
| 122 | File close |
| 124 | File seek |
| 126 | Rename file/directory |
| 127 | Create directory |
| 128 | Delete file |
| 129 | Delete directory |
| 132 | Read byte |
| 134 | Write byte |
| 140 | Truncate file |
| 146 | Directory lookup |
| 147 | Get volumes |
| 148 | Get volume info |
| 149 | Get current directory |
| 150 | Get temp directory |
| 151 | Get home directory |
| 154 | Get file attributes |
| 155 | Set file attributes |
| 156 | Get file size / Register snapshot file |
| 157 | Get file times |
| 158 | Set file times |
| 159 | Get disk space |
