# Smalltalk-80 Primitives Documentation

## Complete List of Primitives Used in Smalltalk-80.st

This document contains a comprehensive table of all primitives found in the Smalltalk-80.st source file. Primitives are low-level operations implemented directly in the virtual machine for performance-critical operations.

## Primitive Categories

The primitives are organized into the following categories:
- **Arithmetic (1-17, 21-37, 40-54)**: Integer and floating-point arithmetic operations
- **Array/Object Access (60-67)**: Accessing and modifying indexed fields
- **System Control (68-89)**: Object creation, method compilation, context control
- **File I/O (90-100, 150-158)**: Traditional Alto file system operations (many replaced by Windows file primitives)
- **BitBlt (96, 104)**: Bit block transfer operations
- **Special Objects (98-99, 101-116)**: System object access and manipulation
- **Windows File System (120-137)**: Windows-specific file operations
- **Windows Extended (150-158)**: Additional Windows file operations

## Primitives Table

| Primitive # | Class | Selector | Signature | Purpose | Return Value | Failure Behavior |
|------------|-------|----------|-----------|---------|--------------|------------------|
| **1** | SmallInteger | + | + aNumber | Add two SmallIntegers | SmallInteger sum | Calls super + aNumber |
| **2** | SmallInteger | - | - aNumber | Subtract two SmallIntegers | SmallInteger difference | Calls super - aNumber |
| **3** | SmallInteger | < | < aNumber | Compare less than | Boolean | Calls super < aNumber |
| **4** | SmallInteger | > | > aNumber | Compare greater than | Boolean | Calls super > aNumber |
| **5** | SmallInteger | <= | <= aNumber | Compare less than or equal | Boolean | Calls super <= aNumber |
| **6** | SmallInteger | >= | >= aNumber | Compare greater than or equal | Boolean | Calls super >= aNumber |
| **7** | SmallInteger | = | = aNumber | Compare equal | Boolean | Calls super = aNumber |
| **8** | SmallInteger | ~= | ~= aNumber | Compare not equal | Boolean | Calls super ~= aNumber |
| **9** | SmallInteger | * | * aNumber | Multiply two SmallIntegers | SmallInteger product | Calls super * aNumber |
| **10** | SmallInteger | / | / aNumber | Exact division | SmallInteger quotient | Creates Fraction if not exact |
| **11** | SmallInteger | \\\\ | \\\\ aNumber | Modulo operation | SmallInteger remainder | Calls super \\\\ aNumber |
| **12** | SmallInteger | // | // aNumber | Integer division (floor) | SmallInteger quotient | Calls super // aNumber |
| **13** | SmallInteger | quo: | quo: aNumber | Integer division (truncate) | SmallInteger quotient | Error if zero divisor |
| **14** | SmallInteger | bitAnd: | bitAnd: arg | Bitwise AND | SmallInteger | Calls arg bitAnd: self |
| **15** | SmallInteger | bitOr: | bitOr: arg | Bitwise OR | SmallInteger | Calls arg bitOr: self |
| **16** | SmallInteger | bitXor: | bitXor: arg | Bitwise XOR | SmallInteger | Calls arg bitXor: self |
| **17** | SmallInteger | bitShift: | bitShift: arg | Bit shift left/right | SmallInteger | Calls super bitShift: arg |
| **18** | Number | rounded | rounded | Round to nearest integer | Integer | Fallback implementation |
| **21** | LargePositiveInteger | + | + anInteger | Add large positive integers | Integer sum | Calls super + anInteger |
| **22** | LargePositiveInteger | - | - anInteger | Subtract large positive integers | Integer difference | Calls super - anInteger |
| **23** | LargePositiveInteger | < | < anInteger | Compare less than | Boolean | Calls super < anInteger |
| **24** | LargePositiveInteger | > | > anInteger | Compare greater than | Boolean | Calls super > anInteger |
| **25** | LargePositiveInteger | <= | <= anInteger | Compare less than or equal | Boolean | Calls super <= anInteger |
| **26** | LargePositiveInteger | >= | >= anInteger | Compare greater than or equal | Boolean | Calls super >= anInteger |
| **27** | LargePositiveInteger | = | = anInteger | Compare equal | Boolean | Calls super = anInteger |
| **28** | LargePositiveInteger | ~= | ~= anInteger | Compare not equal | Boolean | Calls super ~= anInteger |
| **29** | LargePositiveInteger | * | * anInteger | Multiply large positive integers | Integer product | Calls super * anInteger |
| **30** | LargePositiveInteger | / | / anInteger | Exact division | Integer quotient | Calls super / anInteger |
| **31** | LargePositiveInteger | \\\\ | \\\\ anInteger | Modulo operation | Integer remainder | Calls super \\\\ anInteger |
| **32** | LargePositiveInteger | // | // anInteger | Integer division (floor) | Integer quotient | Calls super // anInteger |
| **33** | LargePositiveInteger | quo: | quo: anInteger | Integer division (truncate) | Integer quotient | Calls super quo: anInteger |
| **34** | LargePositiveInteger | bitAnd: | bitAnd: anInteger | Bitwise AND | Integer | Calls super bitAnd: anInteger |
| **35** | LargePositiveInteger | bitOr: | bitOr: anInteger | Bitwise OR | Integer | Calls super bitOr: anInteger |
| **36** | LargePositiveInteger | bitXor: | bitXor: anInteger | Bitwise XOR | Integer | Calls super bitXor: anInteger |
| **37** | LargePositiveInteger | bitShift: | bitShift: anInteger | Bit shift | Integer | Calls super bitShift: anInteger |
| **40** | SmallInteger | asFloat | asFloat | Convert to Float | Float | primitiveFailed |
| **41** | Float | + | + aNumber | Add floats | Float sum | Retry with coercion |
| **42** | Float | - | - aNumber | Subtract floats | Float difference | Retry with coercion |
| **43** | Float | < | < aNumber | Compare less than | Boolean | Retry with coercion |
| **44** | Float | > | > aNumber | Compare greater than | Boolean | Retry with coercion |
| **45** | Float | <= | <= aNumber | Compare less than or equal | Boolean | Calls super <= aNumber |
| **46** | Float | >= | >= aNumber | Compare greater than or equal | Boolean | Calls super >= aNumber |
| **47** | Float | = | = aNumber | Compare equal | Boolean | Retry with coercion |
| **48** | Float | ~= | ~= aNumber | Compare not equal | Boolean | Retry with coercion |
| **49** | Float | * | * aNumber | Multiply floats | Float product | Retry with coercion |
| **50** | Float | / | / aNumber | Divide floats | Float quotient | Check for zero, retry with coercion |
| **51** | Float | truncated | truncated | Truncate to integer | Integer | Fallback implementation |
| **52** | Float | fractionPart | fractionPart | Get fractional part | Float | Fallback implementation |
| **53** | Float | exponent | exponent | Get exponent | Integer | Fallback implementation |
| **54** | Float | timesTwoPower: | timesTwoPower: anInteger | Scale by power of 2 | Float | Fallback implementation |
| **60** | LargePositiveInteger | digitAt: | digitAt: index | Get byte at index | SmallInteger (0-255) | Check bounds, use super at: |
| **60** | Array/String | at: | at: index | Get element at index | Object | Check bounds |
| **61** | LargePositiveInteger | digitAt:put: | digitAt: index put: value | Set byte at index | SmallInteger (0-255) | Use super at:put: |
| **61** | Array/String | at:put: | at: index put: value | Set element at index | Object stored | Check bounds |
| **62** | ArrayedCollection | size | size | Get size of indexed object | SmallInteger | Returns basicSize |
| **62** | LargePositiveInteger | digitLength | digitLength | Number of bytes | SmallInteger | Returns basicSize |
| **62** | String | size | size | String length | SmallInteger | Returns size |
| **63** | String | at: | at: index | Get character at index | Character | Bounds check |
| **64** | String | at:put: | at: index put: aCharacter | Set character at index | Character | Bounds check |
| **65** | FileStream | next | next | Read next byte | SmallInteger (0-255) | Handle position update |
| **66** | FileStream | nextPut: | nextPut: aByte | Write byte | SmallInteger (0-255) | Handle position update |
| **67** | FileDirectory | fileNames | fileNames | List files in directory | Array of Strings | Empty array on error |
| **68** | CompiledMethod | objectAt: | objectAt: index | Access method literal | Object | Bounds check |
| **69** | CompiledMethod | objectAt:put: | objectAt: index put: value | Set method literal | Object | Bounds check |
| **70** | Behavior | basicNew | basicNew | Create instance with no indexed vars | New instance | Check if indexable class |
| **70** | Behavior | new | new | Create instance | New instance | Handle indexable classes |
| **71** | Behavior | basicNew: | basicNew: anInteger | Create instance with indexed vars | New instance with size | primitiveFailed |
| **71** | Behavior | new: | new: anInteger | Create instance with size | New instance | primitiveFailed |
| **72** | Object | become: | become: otherObject | Swap object identities | otherObject | primitiveFailed |
| **73** | Object | instVarAt: | instVarAt: index | Get instance variable | Object | Access beyond fixed vars |
| **74** | Object | instVarAt:put: | instVarAt: anInteger put: anObject | Set instance variable | Object stored | Access beyond fixed fields |
| **75** | Object | hash | hash | Get identity hash | SmallInteger | primitiveFailed |
| **75** | Object | asOop | asOop | Get object pointer as integer | Integer | primitiveFailed |
| **76** | LargePositiveInteger | asObject | asObject | Convert integer to object pointer | Object | Return #NonExistentObject |
| **77** | Behavior | someInstance | someInstance | Get an instance of class | Instance or nil | Returns nil |
| **78** | Object | nextInstance | nextInstance | Get next instance in enumeration | Instance or nil | Returns nil |
| **79** | CompiledMethod | newMethod:header: | newMethod: numberOfBytes header: headerWord | Create new CompiledMethod | CompiledMethod | Fallback creation |
| **80** | ContextPart | blockCopy: | blockCopy: numArgs | Create block closure | BlockContext | Complex fallback |
| **81** | BlockContext | value/value:/value:value:/value:value:value: | value... | Evaluate block | Result of block | Use valueWithArguments: |
| **82** | BlockContext | valueWithArguments: | valueWithArguments: anArray | Evaluate with arguments | Result of block | Check argument count |
| **83** | Object | perform:/perform:with:/etc | perform:... | Perform selector | Method result | Use perform:withArguments: |
| **84** | Object | perform:withArguments: | perform: selector withArguments: anArray | Perform with arguments | Method result | primitiveFailed |
| **85** | Semaphore | signal | signal | Signal semaphore | self | Fallback implementation |
| **86** | Semaphore | wait | wait | Wait on semaphore | self | Fallback implementation |
| **87** | Process | resume | resume | Resume process | self | Fallback implementation |
| **88** | Process | suspend | suspend | Suspend process | List or nil | Fallback implementation |
| **89** | Behavior | flushCache | flushCache | Flush method cache | self | primitiveFailed |
| **90** | WindowsFile | nextPutByte: | nextPutByte: aByte | Write byte to file (was Alto primitive) | aByte | Fallback using primAt:position:put: |
| **90** | FileStream | nextPut: | nextPut: aByte | Write byte | aByte | Increment position |
| **91** | WindowsFile | nextByte | nextByte | Read byte from file (was Alto primitive) | SmallInteger (0-255) | Fallback using primAt:position: |
| **91** | FileStream | next | next | Read byte | SmallInteger or nil | Check atEnd |
| **92** | WindowsFile | size | size | Get file size (was Alto primitive) | Integer | Fallback using primSize: |
| **93** | WindowsFile | position | position | Get file position (was Alto primitive) | Integer | Return position ivar |
| **93** | FileStream | position | position | Get position | Integer | Return position |
| **94** | WindowsFile | position: | position: anInteger | Set file position (was Alto primitive) | anInteger | Fallback using primSetPosition:to: |
| **94** | FileStream | position: | position: anInteger | Set position | anInteger | Update position |
| **95** | WindowsFileDirectory | entries | entries | List directory entries (was Alto primitive) | OrderedCollection | Fallback using primLookup:pattern: |
| **95** | FileStream | atEnd | atEnd | Test end of file | Boolean | Check position vs size |
| **96** | BitBlt | copyBits | copyBits | Perform bit block transfer | self | Handle paint mode, retry |
| **97** | SystemDictionary | snapshotPrimitive | snapshotPrimitive | Save image snapshot | Boolean | Error handling |
| **98** | WindowsFile | openNamed:mode: | openNamed: aFileName mode: modeString | Open file (was Alto primitive) | self | Fallback initialization |
| **98** | FileStream | primOpen:mode: | primOpen: fileName mode: mode | Open file stream | FileStream | Error if fails |
| **99** | WindowsFile | close | close | Close file (was Alto primitive) | self | Fallback using primClose: |
| **99** | FileStream | close | close | Close stream | self | Set handle to nil |
| **100** | WindowsFile | delete | delete | Delete file (was Alto primitive) | self | Close and primDelete: |
| **100** | FileDirectory | deleteFileNamed: | deleteFileNamed: fileName | Delete file | Boolean | Error handling |
| **101** | DirectoryEntry | fileSize | fileSize | Get file size from directory | Integer | Fallback implementation |
| **102** | DirectoryEntry | name | name | Get file name | String | Fallback implementation |
| **103** | Cursor class | cursorLink: | cursorLink: aBoolean | Link hardware cursor | Boolean | Fallback implementation |
| **104** | BitBlt | drawFrom:to:withForm: | drawLoopX: xDelta Y: yDelta | Draw line primitive | self | Fallback line algorithm |
| **105** | ByteArray/String | replaceFrom:to:with:startingAt: | replaceFrom: start to: stop with: replacement startingAt: repStart | Fast byte/character copy | self | Use super implementation |
| **110** | Object/Character | == | == anObject | Identity comparison | Boolean | primitiveFailed |
| **111** | Object | class | class | Get object's class | Class | primitiveFailed |
| **112** | SystemDictionary | coreLeft | coreLeft | Get free memory | Integer | Fallback estimate |
| **113** | SystemDictionary | quit | quit | Quit system | Does not return | Fallback cleanup |
| **114** | SystemDictionary | exitToDebugger | exitToDebugger | Exit to debugger | Does not return | Fallback implementation |
| **115** | SystemDictionary | oopsLeft | oopsLeft | Get free object pointers | Integer | Fallback estimate |
| **116** | SystemDictionary | signal:atOops:wordsLeft: | signal: aSemaphore atOops: numOops wordsLeft: numWords | Low memory signal | self | Fallback implementation |
| **120** | WindowsPathName | pathNameFromString: | pathNameFromString: aString | Parse Windows path | self | Manual path parsing |
| **121** | WindowsFileReference | openRead/openWrite/openAppend | open... | Open file with Windows API | self | Error handling with fallback |
| **122** | WindowsFileReference | close | close | Close Windows file | self | Set handle to -1 |
| **123** | WindowsFileStream | basicRead:/basicWrite: | basicRead: count / basicWrite: buffer | Read/write bytes | ByteArray/Integer | Return bytes read/written |
| **124** | WindowsFileReference/Stream | seekToEnd/setPosition: | Various seek operations | Seek in file | self/position | Update position |
| **125** | WindowsFileReference | delete | delete | Delete file using Windows API | self | Close and delete |
| **126** | WindowsFileReference | rename: | rename: newName | Rename/move file | self | Close and rename |
| **127** | WindowsDirectoryReference | create | create | Create directory | self | Error on failure |
| **128** | WindowsDirectoryReference | delete | delete | Remove directory | self | Error on failure |
| **130** | WindowsFileReference/Attributes | size/exists/initializeFrom: | Various | Get file attributes | Dictionary/attrs | Return nil or default |
| **131** | WindowsFileAttributes | setReadOnly:/setHidden:/setSystem: | set... flags | Set file attributes | self | Return false on failure |
| **132** | WindowsDirectoryReference | primFindFirst: | primFindFirst: pattern | Start directory enumeration | Integer handle | Return -1 on failure |
| **133** | WindowsDirectoryReference | nextEntry | nextEntry | Get next directory entry | Dictionary/nil | Return nil at end |
| **134** | WindowsDirectoryReference | primFindClose: | primFindClose: handle | Close find handle | Boolean | Return false |
| **135** | WindowsFileSystem | drives | drives | Get drive list | String | Return default "C:D:E:" |
| **136** | WindowsDiskVolume | getDiskFreeSpaceEx | getDiskFreeSpaceEx | Get disk space info | Dictionary | Return default values |
| **137** | WindowsDiskVolume | refresh | refresh | Get volume information | Dictionary | Return default values |
| **150** | WindowsFile | nextPutByte: | nextPutByte: aByte | Write single byte | aByte | Use fallback with position |
| **151** | WindowsFile | nextByte | nextByte | Read single byte | SmallInteger (0-255) | Use fallback with position |
| **152** | WindowsFile | size | size | Get file size | Integer | Use primSize: fallback |
| **153** | WindowsFile | position | position | Get current position | Integer | Return position ivar |
| **154** | WindowsFile | position: | position: anInteger | Set file position | anInteger | Use primSetPosition:to: |
| **155** | WindowsFileDirectory | entries | entries | List directory contents | OrderedCollection | Use primLookup: fallback |
| **156** | WindowsFile | openNamed:mode: | openNamed: aFileName mode: modeString | Open file | self | Use primOpen: fallback |
| **157** | WindowsFile | close | close | Close file | self | Use primClose: fallback |
| **158** | WindowsFile | delete | delete | Delete file | self | Close and primDelete: |

## Notes on Primitive Numbers

### Standard Smalltalk-80 Primitives (1-127)
These are the original Blue Book primitives that are part of the standard Smalltalk-80 specification:
- **1-17**: SmallInteger arithmetic and bit operations
- **21-37**: LargePositiveInteger arithmetic and bit operations
- **40-54**: Float operations
- **60-67**: Object indexing and size
- **68-79**: System control (object creation, method compilation)
- **80-89**: Block and process control
- **90-100**: Original Alto file system (mostly replaced)
- **101-116**: System functions
- **110-111**: Object identity and class

### Extended Primitives (120-158)
These are Windows-specific primitives added for the Windows file system implementation:
- **120**: Path parsing
- **121-126**: File operations (open, close, read, write, delete, rename)
- **127-128**: Directory operations
- **130-131**: File attributes
- **132-134**: Directory enumeration
- **135-137**: Volume and disk information
- **150-158**: Additional file I/O (aliases/replacements for 90-100)

### Special Primitives
- **007**: Used for simulator primitive templates (tryPrimitive methods)
- **96, 104**: BitBlt graphics operations
- **105**: Optimized string/byte array copying

## Primitive Failure Handling

Most primitives have fallback Smalltalk code that executes when the primitive fails. Common fallback patterns include:
1. **Calling super**: Delegating to superclass implementation
2. **Type coercion**: Retrying with different argument types
3. **Error signaling**: Raising appropriate error messages
4. **Manual implementation**: Providing pure Smalltalk implementation
5. **primitiveFailed**: Calling the standard primitive failure handler

## Usage Patterns

Primitives are invoked using the `<primitive: N>` syntax at the beginning of a method. The virtual machine attempts to execute the primitive first. If it succeeds, the method returns immediately with the primitive's result. If it fails (due to type errors, bounds checks, etc.), the Smalltalk code following the primitive declaration is executed as a fallback.

This dual approach provides both performance (through VM-level implementation) and robustness (through Smalltalk fallbacks) for critical system operations.