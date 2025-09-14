Crea# Smalltalk-80 Self-Hosting Roadmap

## Executive Summary

This document analyzes the current state of the Smalltalk-80 interpreter and outlines the remaining phases needed to achieve self-hosting capability - where the system can file in all additional classes to build the full Smalltalk-80 system.

## Current State Analysis

### Completed Components

#### 1. Bootstrap Infrastructure (✅ Complete)
- **Phase 1**: Core classes (Object, Behavior, Class, Metaclass, CompiledMethod, MethodDictionary, Symbol, String, Array, ByteArray, Collection, Dictionary, SystemDictionary)
- **Phase 2**: Method installation with bytecode for Phase 1 classes plus additional classes (UndefinedObject, Boolean, True, False, Magnitude, LookupKey, Association)
- **Phase 3 (Partial)**: Framework for adding remaining classes, currently includes minimal compiler classes (Scanner, Parser, Compiler)

#### 2. Bytecode Interpreter (✅ Complete)
- All 256 bytecodes implemented and tested
- Method dispatch and lookup
- Context management
- Stack operations
- Arithmetic and logical operations
- Jump and control flow

#### 3. VM Primitives (⚠️ Partial - 70% Complete)
- ✅ Integer primitives (1-18)
- ✅ Large integer primitives (21-37)
- ✅ Float primitives (40-54)
- ✅ Array/collection primitives (60-79)
- ✅ Control primitives (80-89, 152-154, 201)
- ✅ System primitives (110-116, 139)
- ✅ String primitives (158-159)
- ✅ Bootstrap primitives (174-183)
- ✅ Compiler primitives (170-173)
- ❌ File system primitives (142-151, 155-157) - **DISABLED**
- ⚠️ Input/output primitives (90-105) - Partially implemented

#### 4. Compiler Integration (⚠️ Partial)
- ✅ C# compiler (`SmalltalkCompiler.cs`) that can compile Smalltalk to bytecode
- ✅ Compiler primitives (170-173) for runtime compilation
- ✅ Parser and AST builder
- ⚠️ Not yet integrated with Smalltalk Scanner/Parser/Compiler classes

## Missing Components for Self-Hosting

### Phase 4: File System Support
**Priority: CRITICAL**
**Status: Blocked - Primitives disabled**

Required primitives (currently commented out):
- Primitive 142: OpenStream
- Primitive 143: CloseStream  
- Primitive 144: ReadNextChar
- Primitive 145: WriteChar
- Primitive 146: GetFileSize
- Primitive 147: SetPosition
- Primitive 148: GetFileSizeByName
- Primitive 149: FileExists
- Primitive 150: DeleteFile
- Primitive 151: RenameFile
- Primitive 155: DeleteFile (duplicate?)
- Primitive 156: IsDirectory
- Primitive 157: GetDirectoryEntries

**Implementation Tasks:**
1. Create `FileSystemPrimitives.cs` with proper file stream management
2. Implement FileStream class in Smalltalk with proper handle management
3. Add error handling for file operations
4. Test with simple file read/write operations

### Phase 5: Complete Compiler Integration
**Priority: HIGH**
**Status: Foundation exists, needs connection**

Required components:
1. **Connect C# compiler to Smalltalk compiler classes**
   - Scanner class needs to use C# lexer internally
   - Parser class needs to use C# parser internally
   - Compiler class needs to call primitive 170-173

2. **Method compilation workflow**
   - `Compiler compile: sourceCode in: class`
   - Parse source → Generate AST → Compile to bytecode → Create CompiledMethod → Install in class

3. **Source code management**
   - Method source storage
   - Source file references
   - Change log/versions

**Implementation Tasks:**
1. Implement Scanner>>scan to use C# lexer
2. Implement Parser>>parse: to use C# parser
3. Implement Compiler>>compile:in:notifying:ifFail:
4. Add source code storage to CompiledMethod
5. Test with simple method compilation

### Phase 6: File-In Mechanism
**Priority: HIGH**
**Status: Parser exists, needs integration**

Required components:
1. **Chunk format reader**
   - Read bang-delimited chunks
   - Handle class definitions
   - Handle method definitions
   - Handle doIt expressions

2. **Class creation from file-in**
   - Parse class definition: `ClassName subclass: #SubclassName instanceVariableNames: '...' ...`
   - Create class using primitives
   - Install in system dictionary

3. **Method installation from file-in**
   - Parse method source
   - Compile to bytecode
   - Install in appropriate class

**Implementation Tasks:**
1. Implement FileStream>>fileIn
2. Implement chunk reader (handling ! delimiters)
3. Implement class definition parser
4. Implement method installation workflow
5. Test with simple class file-in

### Phase 7: Stream Classes
**Priority: MEDIUM**
**Status: Not started**

Required classes:
- Stream (abstract)
- PositionableStream
- ReadStream
- WriteStream
- ReadWriteStream
- FileStream
- StandardFileStream

**Implementation Tasks:**
1. Define Stream hierarchy in Phase 3 manifest
2. Implement basic stream operations
3. Connect to file system primitives
4. Test with file operations

### Phase 8: System Classes
**Priority: MEDIUM**
**Status: Partially complete**

Required classes for full system:
- Behavior extensions (full method dictionary management)
- ClassDescription (full class structure)
- Metaclass (complete implementation)
- MethodContext (full debugger support)
- BlockContext (full closure support)
- Process and ProcessScheduler
- Semaphore
- SystemDictionary extensions

### Phase 9: Collection Classes
**Priority: LOW**
**Status: Basic collections exist**

Additional collections needed:
- Bag
- IdentityDictionary
- IdentitySet
- LinkedList
- Text
- RunArray

## Implementation Phases

### Immediate Phase (Phase 4a): Enable File System
**Timeline: 1-2 days**
1. Uncomment file system primitives in PrimitiveDispatcher.cs
2. Create FileSystemPrimitives.cs with basic operations
3. Test file read/write with simple examples
4. Add FileStream class to Phase 3 manifest

### Short Term (Phase 4b-5): Compiler Integration
**Timeline: 3-5 days**
1. Connect Smalltalk Compiler class to C# compiler
2. Implement source code compilation workflow
3. Test dynamic method compilation
4. Add source management

### Medium Term (Phase 6-7): File-In Support
**Timeline: 1 week**
1. Implement chunk format reader
2. Add class definition parsing
3. Implement full file-in mechanism
4. Test with Smalltalk-80 source files

### Long Term (Phase 8-9): Full System
**Timeline: 2-3 weeks**
1. Add remaining system classes
2. Implement process scheduling
3. Add remaining collection classes
4. Full system validation

## Success Criteria

The system will be considered self-hosting when it can:
1. ✅ Execute all 256 bytecodes (COMPLETE)
2. ✅ Create classes and methods programmatically (COMPLETE)
3. ❌ Read Smalltalk source files from disk
4. ❌ Parse and compile Smalltalk source code
5. ❌ Install compiled methods in classes
6. ❌ File in the complete Smalltalk-80 class library
7. ❌ Save and restore system images

## Current Blockers

1. **File System Primitives Disabled**: The most critical blocker. Without file I/O, the system cannot read source files.
2. **Compiler Integration**: The C# compiler exists but isn't connected to Smalltalk compiler classes.
3. **Stream Classes Missing**: No stream infrastructure for file operations.

## Recommended Next Steps

1. **Enable file system primitives** (1 day)
2. **Implement basic FileStream** (1 day)
3. **Connect Compiler class to C# compiler** (2 days)
4. **Implement chunk reader** (1 day)
5. **Test with simple file-in** (1 day)

## Testing Strategy

### Phase 4 Tests
```smalltalk
"Test file write and read"
| stream |
stream := FileStream newFileNamed: 'test.txt'.
stream nextPutAll: 'Hello World'.
stream close.

stream := FileStream oldFileNamed: 'test.txt'.
Transcript show: stream contents.
stream close.
```

### Phase 5 Tests
```smalltalk
"Test dynamic compilation"
Compiler compile: 'testMethod
    ^42' 
    in: Object.
    
Object new testMethod. "Should return 42"
```

### Phase 6 Tests
```smalltalk
"Test file-in"
FileStream fileIn: 'MyClass.st'.
"Where MyClass.st contains:
Object subclass: #MyClass
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''!

!MyClass methodsFor: 'accessing'!
x
    ^x!
x: aValue
    x := aValue! !
"
```

## Risk Assessment

- **High Risk**: File system primitive implementation may have platform dependencies
- **Medium Risk**: Compiler integration complexity with existing C# compiler
- **Low Risk**: Stream and collection classes are well-understood

## Conclusion

The Smalltalk-80 interpreter has made significant progress with a working bytecode interpreter and most VM primitives. The primary gap preventing self-hosting is the lack of file system support. With focused effort on enabling file I/O and connecting the compiler, the system could achieve self-hosting capability within 2-3 weeks of development time.

The recommended approach is to tackle the immediate blocker (file system primitives) first, then progressively build up the compiler integration and file-in mechanism. This incremental approach allows for testing at each stage and reduces implementation risk.