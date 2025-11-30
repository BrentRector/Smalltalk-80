# Resume Prompt: Smalltalk-80 Interpreter

**Session Date**: 2025-11-30
**Status**: ✅ System Browser FULLY FUNCTIONAL

## Current State

### What's Working
- ✅ **System Browser**: Fully functional with source code retrieval
- ✅ **MVC Controller Loop**: Running, mouse/keyboard input working
- ✅ **Process Primitives 85-88**: suspend, resume, terminate, yield
- ✅ **Init Expressions**: 51/51 passing (100%)
- ✅ **BlockContext Tests**: 120/120 passing (returns 1191)
- ✅ **Source File Retrieval**: `CompiledMethod>>getSource` working via `.sources` file

### Recent Fixes (This Session)

1. **BOM Double-Offset Bug** (`FilePrimitives.cs`)
   - Primitive 124 (FileSeek) returned physical position instead of logical
   - Primitive 132 (FileReadByte) then added BOM offset again
   - Fix: Seek now returns logical position (physical - BOM offset)

2. **BlockContext Stack Underflow** (`StackManager.cs`)
   - Blue Book block arg handling pushes args onto stack for pop/store bytecodes
   - StackManager used old model: `initialSP = nargs` (assumed fixed arg slots)
   - Fix: Changed `initialSP` to 0 for BlockContexts (args are on stack, not reserved)

3. **Outdated Build Warning** (`Smalltalk80.Interpreter.csproj`)
   - Warning looked for `coldstart.json` in wrong location
   - Fix: Updated path to `Data\coldstart.json`, removed misleading warning

---

## Architecture Notes

### Source Code Retrieval Flow
```
Browser → Behavior>>sourceCodeForMethod:at:
       → CompiledMethod>>getSource
       → RemoteString>>string
       → WindowsFileReference>>position: + nextChunk
       → Source text from .sources file
```

**Key Components**:
- `.sources` file: Chunk-formatted source (! delimiters, !! escaping)
- Source positions: Last 3 bytes of CompiledMethod bytecodes
- `SourceFiles` global: Array of 2 WindowsFileReference objects
- UTF-8 BOM: Automatically compensated in file primitives

### Block Argument Handling (Blue Book Model)
```
1. value: primitive pushes args onto BlockContext stack
2. Block's first bytecodes are POP_STORE_TEMP
3. These pop args from stack into temp slots in home MethodContext
4. initialSP = 0 for BlockContexts (no reserved slots)
```

---

## Known Issues

### 🟡 Control Structure Optimization Disabled
`BytecodeCompiler.cs` has `TryInlineConditional()` disabled. Blue Book says `ifTrue:`/`ifFalse:` should compile to conditional jumps (no BlockContext). Current workaround: `BlockContext>>removeSelf` delegates to home MethodContext.

### 🟡 --execute Feature
Parser error on number literals. Scanner produces `tokenType: #xDigit` instead of `#number`.

---

## Key Files

### Recently Modified
- `FilePrimitives.cs` - BOM offset handling (primitive 124 returns logical position)
- `StackManager.cs` - BlockContext initialSP = 0 for Blue Book arg handling
- `Smalltalk80.Interpreter.csproj` - Removed outdated coldstart.json warning

### Source Lookup
- `SystemDictionaryInstaller.cs` - Creates SourceFiles array with file handles
- `ObjectPrimitives.cs` - `AllocateCompiledMethodInternal` encodes source positions
- `docs/SOURCE-LOOKUP-ANALYSIS.md` - Complete documentation of source retrieval

### Block Handling
- `ControlPrimitives.cs` - Primitive 81 (value*) pushes args to BlockContext stack
- `BytecodeCompiler.cs` - Emits POP_STORE_TEMP at block start for args

---

## Test Commands

```bash
# Standard run
cd Smalltalk80.Interpreter
dotnet run

# BlockContext regression test (MUST PASS)
dotnet run -- --coldstart ../Smalltalk80.Interpreter/bin/Debug/net9.0-windows/Data/coldstart_with_runner.json
# Expected: Returns 1191 (120 tests pass)

# C# unit tests
cd Smalltalk80.Interpreter.Tests
dotnet test
```

---

## Model Selection

Always use model: **claude-sonnet-4-5**
