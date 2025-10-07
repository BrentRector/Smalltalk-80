# Smalltalk-80 Implementation - Resume Prompt

**Last Updated**: 2025-10-07
**Status**: Bytecode interpreter complete (256/256 opcodes), 94/111 primitives working, 1 critical compiler bug blocking execution
**Build Status**: ✅ Both projects compile successfully (0 errors)
**Cold Start**: ✅ Loads successfully, zero garbage collection
**Execution**: ❌ **BLOCKED** by instance variable layout bug in compiler

---

## 🚀 Quick Start for New Session

### First Commands to Run

```bash
# Set UTF-8 for Unicode display (box-drawing characters in output)
pwsh -Command "chcp 65001"

# Verify both projects build
cd Smalltalk80.Compiler && dotnet build --no-restore
cd ../Smalltalk80.Interpreter && dotnet build --no-restore

# Test cold start bootstrap
cd Smalltalk80.Interpreter
dotnet run --no-build -- repl --gc-log --validate-gc
```

### Essential Files to Read First

1. **[CLAUDE.md](CLAUDE.md)** - Complete development guidelines, principles, pitfalls to avoid
2. **[Interpreter-Architecture.md](docs/Interpreter-Architecture.md)** - Canonical architectural reference, bytecode specification (lines 1990-2189)
3. **This file** - Current status, blocking issues, next steps

### Critical Context

- **Blue Book Compliance**: Every design decision MUST follow Smalltalk-80 specification (Goldberg & Robson, 1983)
- **Zero Garbage Requirement**: Cold start creates exactly 0 garbage (all 3,413 objects properly rooted)
- **Shared BytecodeDefinitions.cs**: Single source of truth in `Shared/Core/Bytecodes/` linked by both projects
- **JSON Cold Start Format**: Compiler generates `Smalltalk-80.json` (11MB), interpreter loads it in 10 phases

---

## 📊 Current System State

### ✅ What's Working

**Compiler (Smalltalk80.Compiler)**:
- ✅ ANTLR4 grammar parses complete Smalltalk-80.st source (474 classes, 4,640 methods)
- ✅ Bytecode compiler with all 256 opcodes correctly mapped to Blue Book spec
- ✅ JSON generator creates schema-validated output (coldstart-v1.0.0.schema.json)
- ✅ Stack analysis validates all method bytecode sequences
- ✅ Generates deterministic LocalIds for stable cross-references
- ✅ All literal accesses use correct +1 offset (header at index 0)

**Interpreter (Smalltalk80.Interpreter)**:
- ✅ 10-phase JSON bootstrap loader (zero garbage collection)
- ✅ Two-phase symbol interning (temp storage → USTable migration)
- ✅ Table-based ObjectTable with OOP indirection for become: support
- ✅ 5-element Special Objects Array (nil, false, true, Smalltalk, ProcessorScheduler)
- ✅ Complete VirtualMachine.cs bytecode interpreter (all 256 opcodes 0x00-0xFF)
- ✅ 94/111 primitives fully implemented (85% complete)
- ✅ SmalltalkHeap with dynamic field access (GetField/SetField)
- ✅ LayoutResolver for runtime field lookups by name
- ✅ Mark-and-sweep garbage collector
- ✅ Bytecode validation system (stack analysis + operand validation)

**Graphics & Advanced Features**:
- ✅ Complete BitBlt engine (all 16 combination rules for 1-bit monochrome graphics)
- ✅ IDisplayProvider interface for display/input abstraction
- ✅ BlockContext infrastructure (block closures, value, value:)
- ✅ Dynamic message sending (perform:, perform:with:)

### ❌ What's Broken

**CRITICAL BUG - Instance Variable Layout Compilation** (BLOCKING EXECUTION):

**Problem**: Compiler generates invalid bytecode for instance variable access in 846 methods across all Windows* extension classes.

**Root Cause**: `FileInFormatAstBuilder.ComputeInheritedInstanceVariables()` computes instance variable indices incorrectly:
- Uses `InsertRange(0)` which reverses inheritance order
- Places subclass variables before superclass variables
- Violates Smalltalk-80 requirement: superclass inst vars MUST come first

**Example**:
```smalltalk
Object subclass: #WindowsDisplay
    instanceVariableNames: 'width height bits'
    ...

" Correct layout:
  width  = index 0
  height = index 1
  bits   = index 2

" Compiler generates bytecode accessing:
  width  = index 2  ← WRONG!
  height = index 1
  bits   = index 0  ← WRONG!
"
```

**Impact**:
- All standard Smalltalk-80 classes work (Object has no inst vars, so offset doesn't matter)
- All Windows* extension classes have invalid bytecode
- Cannot execute any method in affected classes
- 846/4640 methods fail bytecode operand validation (18% of codebase)

**Status**:
- ✅ Bug identified in FileInFormatAstBuilder.cs:920-930
- ✅ Fix attempted (reversed iteration order in ComputeInheritedInstanceVariables)
- ❌ Fix didn't work - errors persist after clean rebuild
- ❌ Debug output shows correct layout being computed, but bytecode still has wrong indices
- 🔍 **Investigation needed**: Why does correct layout computation not translate to correct bytecode emission?

**Files Involved**:
- `Smalltalk80.Compiler/AST/FileInFormatAstBuilder.cs` - Instance variable layout computation
- `Smalltalk80.Compiler/BytecodeCompiler.cs` - Bytecode generation (EmitStoreInstanceVariable, EmitPushInstanceVariable)
- `Smalltalk80.Interpreter/LoaderPhases/Phase12_BytecodeValidation.cs` - Validation runner
- `Smalltalk80.Interpreter/Core/Bytecodes/BytecodeOperandValidator.cs` - Validation logic

**Validation Errors** (sample):
```
WindowsDisplay>>initialize: Instance variable index 2 out of range (max 1)
WindowsDisplay>>width: Instance variable index 0 out of range (max -1)
WindowsController>>controlActivity: Temporary variable index 2 out of range (max 0)
```

**Next Steps to Fix**:
1. Add extensive logging to BytecodeCompiler.EmitStoreInstanceVariable() and EmitPushInstanceVariable()
2. Trace exact indices being emitted for a failing method (e.g., WindowsDisplay>>initialize)
3. Compare indices being emitted vs. indices from ComputeInheritedInstanceVariables()
4. Determine if bug is in layout computation or bytecode emission
5. Verify ClassMetadata.InstanceVariableNames has correct order during AST building

---

## 🏗️ Architecture Overview

### Project Structure

```
Smalltalk-80/
├── Shared/                            # Shared source (linked by projects)
│   └── Core/Bytecodes/
│       └── BytecodeDefinitions.cs     # SINGLE SOURCE OF TRUTH for all 256 bytecodes
│
├── Smalltalk80.Compiler/              # Compiler (Smalltalk → JSON)
│   ├── AST/FileInFormatAstBuilder.cs  # ANTLR4 visitor, builds AST
│   ├── BytecodeCompiler.cs            # Emits bytecode for methods
│   ├── CodeGen/ColdStartJsonGenerator.cs  # Generates Smalltalk-80.json
│   └── Program.cs                     # CLI entry point
│
├── Smalltalk80.Interpreter/           # VM runtime
│   ├── VM/
│   │   ├── VirtualMachine.cs          # Complete bytecode interpreter (256 opcodes)
│   │   ├── SmalltalkHeap.cs           # Object storage + field access
│   │   ├── LayoutResolver.cs          # Dynamic field lookup by name
│   │   └── Primitives/                # 111 primitives (94 implemented)
│   ├── ColdStart/                     # 10-phase bootstrap system
│   │   ├── JsonColdStartBootstrap.cs  # Orchestrator
│   │   ├── SymbolInterning.cs         # Two-phase symbol creation
│   │   └── Phase12_BytecodeValidation.cs  # Validates all method bytecode
│   ├── Memory/
│   │   ├── ObjectTable.cs             # Table-based storage
│   │   ├── GarbageCollector.cs        # Mark-and-sweep GC
│   │   └── SpecialObjectsArray.cs     # 5 GC roots
│   └── Core/Bytecodes/                # Bytecode utilities (decompiler, validators)
│
└── Smalltalk-80 Source/
    └── Smalltalk-80.st                # Master source (NEVER MODIFY!)
```

### Data Flow

```
Smalltalk-80.st
    ↓ (Compiler parses)
ANTLR4 AST
    ↓ (BytecodeCompiler emits bytecode)
    ↓ (ColdStartJsonGenerator serializes)
Smalltalk-80.json (11MB, schema-validated)
    ↓ (Interpreter loads)
    ↓ (10-phase bootstrap)
ObjectTable (3,413 objects, 0 garbage)
    ↓ (VirtualMachine executes)
Running Smalltalk-80 System
```

### Key Design Principles

1. **Blue Book Compliance**: Every decision follows Smalltalk-80 specification
2. **Zero Garbage**: Cold start creates no temporary objects (validates via GC after bootstrap)
3. **Per-VM Architecture**: All VM state is instance-based (supports multiple independent VMs)
4. **Table-Based Memory**: ObjectTable with OOP indirection (enables become:)
5. **5 GC Roots**: Minimal SOA (nil, false, true, Smalltalk, ProcessorScheduler)
6. **Deterministic Bootstrap**: Same objects get same OOPs every run
7. **Shared Bytecode Definitions**: Compiler and interpreter use identical enum mappings

---

## 🔧 Completed Work

### October 2025 Session Highlights

**Bytecode System Audit & Fixes**:
- ✅ Fixed critical return bytecode bug (0x78-0x7C were completely wrong!)
  - Before: RETURN_TOP=0x78, RETURN_SELF=0x79, etc. (WRONG!)
  - After: RETURN_SELF=0x78, RETURN_TRUE=0x79, etc. (Blue Book compliant)
  - Impact: Would have caused every `^ self` to return top of stack instead
- ✅ Fixed conditional jump bytecodes (were using reserved opcodes 152-175)
  - Now use extended bytecodes 0x80 0x14-0x19 (Blue Book compliant)
  - Eliminated 130 validation errors
- ✅ Added missing return emission methods (ReturnTrue, ReturnFalse, ReturnNil, etc.)
- ✅ Fixed stack analysis return range (120-127 instead of 120-125)
- ✅ Fixed IsReturnBytecode range check

**Shared Source Infrastructure**:
- ✅ Created `Shared/Core/Bytecodes/BytecodeDefinitions.cs`
- ✅ Removed duplicate copies from Compiler and Interpreter projects
- ✅ Added MSBuild links in both .csproj files
- ✅ Changed namespace to neutral `Smalltalk80.Core.Bytecodes`
- ✅ Updated all using statements across both projects
- ✅ Documented in CLAUDE.md and Interpreter-Architecture.md
- ✅ Result: Zero duplication, impossible to have version mismatch

**Bytecode Validation System**:
- ✅ BytecodeStackAnalyzer validates all 4,640 methods (all pass!)
- ✅ BytecodeOperandValidator validates operand indices
  - 3,794/4,640 methods pass (82%)
  - 846 errors in Windows* extension classes (instance var layout bug)

---

## 🎯 Remaining Work

### 1. FIX CRITICAL BUG - Instance Variable Layout (HIGH PRIORITY)

**Goal**: Fix compiler to generate correct instance variable indices

**Tasks**:
- [ ] Add debug logging to BytecodeCompiler emission methods
- [ ] Trace bytecode emission for one failing method (e.g., WindowsDisplay>>initialize)
- [ ] Verify ClassMetadata.InstanceVariableNames order during AST building
- [ ] Determine if bug is in ComputeInheritedInstanceVariables or bytecode emission
- [ ] Fix the root cause
- [ ] Recompile Smalltalk-80.json
- [ ] Verify all 4,640 methods pass bytecode validation

**Success Criteria**: All 4,640 methods pass bytecode operand validation

### 2. Complete Primitive Implementations (MEDIUM PRIORITY)

**Remaining**: 17 primitives (15% of total)

**Process Management** (HIGH - needed for system to run):
- [ ] Primitive 85 - Signal semaphore
- [ ] Primitive 86 - Wait on semaphore
- [ ] Primitive 87 - Resume process
- [ ] Primitive 88 - Suspend process

**Requirements**: Implement ProcessorScheduler infrastructure, Semaphore class support, Process management

**Symbol Interning** (MEDIUM - needed for runtime symbol creation):
- [ ] Primitive 106 - String asSymbol (currently stubs)
- [ ] Add runtime symbol table access API
- [ ] Integrate with existing USTable from cold start

**Advanced Object Operations** (LOW - rarely used):
- [ ] Primitive 68, 130 - become: / becomeForward:
- [ ] Primitives 128-129, 139 - Object enumeration (someInstance, nextInstance, nextObject)
- [ ] Primitives 114-116 - Advanced system operations

### 3. Execute First Smalltalk Code (HIGH PRIORITY)

**Goal**: Successfully execute "1 + 2" and return 3

**Prerequisites**:
- [x] All bytecodes implemented
- [x] Arithmetic primitives working
- [x] Message sending working
- [ ] Instance variable layout bug fixed
- [ ] Basic process scheduler (may not need full semaphore support for simple arithmetic)

**Test Plan**:
```smalltalk
" Start with simplest possible expressions "
1.                    " Push literal SmallInteger "
true.                 " Push literal true "
nil.                  " Push literal nil "
1 + 2.                " Arithmetic message send → SmallInteger primitive "
3 factorial.          " Recursive method calls "
#(1 2 3) at: 2.       " Array access "
[ 42 ] value.         " Block closure "
```

**Execution Modes**:
- [ ] Implement REPL (read-eval-print loop)
- [ ] Add expression evaluation API to VirtualMachine
- [ ] Handle Smalltalk syntax vs. doIt expressions

### 4. Build Development Tools (MEDIUM PRIORITY)

**Debugging Tools**:
- [ ] Bytecode disassembler (partially done in BytecodeDisassembler.cs)
- [ ] Stack frame inspector
- [ ] Object inspector (dump object fields)
- [ ] Method decompiler (bytecode → source approximation)
- [ ] Execution tracer (log every bytecode executed)

**Testing Infrastructure**:
- [ ] Unit test framework integration
- [ ] Automated test suite for primitives
- [ ] Regression tests for bytecode interpreter
- [ ] Performance benchmarks

### 5. Graphics & UI (LOW PRIORITY - Future Work)

**Display System**:
- [ ] Implement IDisplayProvider for Windows/Linux/macOS
- [ ] Connect BitBlt engine to actual framebuffer
- [ ] Implement Form class properly
- [ ] Display update loop

**Input Handling**:
- [ ] Mouse event queue
- [ ] Keyboard event queue
- [ ] Cursor management

**Smalltalk-80 UI**:
- [ ] Test BitBlt primitives with real display
- [ ] MVC framework execution
- [ ] Browser, Workspace, Debugger windows

---

## 📚 Essential Documentation

### Bytecode Specification Reference

**Location**: [Interpreter-Architecture.md](docs/Interpreter-Architecture.md), lines 1990-2189

**All 256 opcodes documented**:
- 0x00-0x0F: Push receiver instance variables 0-15
- 0x10-0x1F: Push temporary/argument variables 0-15
- 0x20-0x3F: Push literal constants 0-31
- 0x40-0x47: Push literal variables 0-7
- 0x48-0x4F: Pop and store into receiver instance variables 0-7
- 0x50-0x5F: Pop and store into temporary variables 0-7
- 0x60-0x67: Push receiver
- 0x68-0x6F: Pop and store into receiver instance variables 8-15
- 0x70-0x77: Push special constants (self, true, false, nil, -1, 0, 1, 2)
- 0x78-0x7F: Return bytecodes (self, true, false, nil, top, block returns, NOP)
- 0x80: Extended bytecode prefix (26 sub-operations)
- 0x81-0x8F: Reserved
- 0x90-0xA7: Jump bytecodes (short/medium/long forward)
- 0xA8-0xAF: Reserved
- 0xB0-0xBF: Arithmetic sends (+, -, <, >, *, /, \\, @, bitShift:, //, bitAnd:, bitOr:, =, ~=, >=, <=)
- 0xC0-0xCF: Special sends (at:, at:put:, size, next, class, blockCopy:, value, etc.)
- 0xD0-0xFF: Literal message sends (0/1/2 arguments)

**Extended Bytecodes** (0x80 + sub-op):
- 0x00-0x01: Extended push instance variable (any index)
- 0x02-0x03: Extended store instance variable (any index)
- 0x04: Extended push literal constant (any index)
- 0x05: Extended push literal variable (any index)
- 0x06-0x07: Extended store/pop into literal variable
- 0x08-0x09: Extended push receiver/temporary variable
- 0x0A-0x0B: Extended store/pop into temporary variable
- 0x0C-0x0D: Push/store into active context
- 0x0E-0x13: Single/double/triple extended sends (normal + super)
- 0x14-0x19: Conditional jumps (jumpIfTrue/False, short/medium/long)

### Primitive Numbers

**Arithmetic** (1-54):
- 1-17: SmallInteger operations (+, -, <, >, *, /, \\, etc.)
- 21-37: LargeInteger operations (same as SmallInteger)
- 40-54: Float operations (IEEE 754)

**Object/Array** (60-75, 110-111):
- 60: at: (array/string indexing)
- 61: at:put: (array/string modification)
- 62: size (array/string/object size)
- 63: stringAt: (byte access)
- 64: stringAt:put: (byte modification)
- 70: new (create new instance)
- 71: new: (create instance with indexed size)
- 73: objectAt: (raw object field access)
- 74: objectAt:put: (raw object field modification)
- 110: == (identity comparison)
- 111: class (object class)

**Control** (80-88):
- 80: blockCopy: (create block closure)
- 81: value (evaluate 0-arg block)
- 82: value: (evaluate 1-arg block)
- 83: perform: (dynamic message send)
- 84: perform:with: (dynamic send with 1 arg)
- 85-88: signal/wait/resume/suspend (process management - STUBS)

**Graphics/Input** (90-104):
- 90: mousePoint (get mouse position)
- 91: cursorLocPut: (set cursor position)
- 92: cursorLink (cursor display)
- 93: inputSemaphore: (set input semaphore)
- 94: sampleInterval: (set sampling interval)
- 95: inputWord (get input word)
- 96: bitBlt (perform BitBlt operation)
- 97: snapshot (save image)
- 100: performWithArgs: (dynamic send with arg array)
- 101: signal (signal semaphore - partial stub)
- 102: wait (wait on semaphore - partial stub)
- 103: resume (resume process - partial stub)
- 104: beep (make sound)

**Strings** (105-106):
- 105: stringReplace (string copy/compare)
- 106: asSymbol (intern string as symbol - STUB)

**System** (112-139):
- 112: oopsLeft (free memory)
- 113: coreLeft (free memory in words)
- 128-129, 139: Object enumeration (STUBS)
- 130: become: (swap object identities - STUB)
- 133: timeWordsInto: (get system time)

---

## 🐛 Known Bugs & Issues

### CRITICAL Issues

1. **Instance Variable Layout Bug** (BLOCKING)
   - Affects: 846 methods in Windows* extension classes
   - Symptom: Invalid bytecode operand indices
   - Status: Root cause identified, fix attempted but didn't work
   - Priority: **MUST FIX BEFORE ANY EXECUTION**

### MEDIUM Issues

2. **Process Scheduler Not Implemented**
   - Affects: Primitives 85-88, full Smalltalk execution model
   - Symptom: Process management primitives are stubs
   - Status: Not started
   - Priority: High for full system, not needed for simple arithmetic

3. **Symbol Interning Runtime API Missing**
   - Affects: Primitive 106 (String asSymbol)
   - Symptom: Cannot create new symbols at runtime
   - Status: Cold start has interning, needs runtime API
   - Priority: Medium - rarely used in simple code

### LOW Issues

4. **become: Not Implemented**
   - Affects: Primitives 68, 130
   - Symptom: Cannot swap object identities
   - Status: Not started (requires deep memory manipulation)
   - Priority: Low - advanced feature, rarely used

5. **Object Enumeration Not Implemented**
   - Affects: Primitives 128-129, 139
   - Symptom: Cannot iterate all objects or instances of a class
   - Status: Not started
   - Priority: Low - debugging feature, not needed for execution

---

## 🔍 Debugging Strategies

### When Investigating the Instance Variable Bug

1. **Enable Debug Logging**:
   ```csharp
   // In BytecodeCompiler.cs
   public void EmitPushInstanceVariable(int index)
   {
       Console.WriteLine($"[DEBUG] EmitPushInstanceVariable: index={index}");
       // ... rest of method
   }
   ```

2. **Pick a Simple Failing Method**:
   - WindowsDisplay>>initialize is a good candidate
   - Check what instance variables it accesses
   - Trace indices being emitted

3. **Compare Layout Computation vs. Emission**:
   ```csharp
   // In FileInFormatAstBuilder.cs
   var layout = ComputeInheritedInstanceVariables(className);
   Console.WriteLine($"[DEBUG] {className} layout: {string.Join(", ", layout)}");
   ```

4. **Check ClassMetadata Order**:
   - Verify InstanceVariableNames list has correct order
   - Compare against inheritance chain

5. **Verify Bytecode Compiler Lookup**:
   - When emitting instance variable access, does it look up index correctly?
   - Does it account for inherited variables?

### When Bytecode Validation Fails

**Location**: `Smalltalk80.Interpreter/LoaderPhases/Phase12_BytecodeValidation.cs`

The validation runs after method installation and checks:
1. Stack analysis (all methods must have balanced stacks) ✅ PASSING
2. Operand validation (all indices must reference valid entries) ❌ 846 ERRORS

**To Debug**:
```bash
# Run interpreter with validation
dotnet run --project Smalltalk80.Interpreter -- repl --gc-log

# Look for Phase 12 output:
# "Phase 12: Bytecode Validation"
# "Stack validation: 4640 methods checked, 0 errors"
# "Operand validation: 3794 methods validated, 846 errors"
```

**Error Format**:
```
WindowsDisplay>>initialize: Instance variable index 2 out of range (max 1)
  Class: WindowsDisplay
  Inherited inst vars: width, height
  Max valid index: 1 (0-based)
  Bytecode tried to access: index 2
```

---

## 💡 Tips for Future Sessions

### Before Making Changes

1. **Read CLAUDE.md** - Understand principles, pitfalls, checklist
2. **Read Interpreter-Architecture.md** - Canonical reference for all decisions
3. **Check Blue Book** - Verify compliance for any behavior changes
4. **Run builds** - Ensure both projects compile before starting work

### When Adding Features

1. **Update Documentation First** - Write what you're going to do
2. **Implement** - Make the changes
3. **Test** - Verify with builds and validation
4. **Update CLAUDE.md** - Mark feature as complete
5. **Update Interpreter-Architecture.md** - Document architectural changes
6. **Update RESUME-PROMPT.md** - Update status for next session

### When Debugging

1. **Add Logging** - Console.WriteLine is your friend
2. **Start Simple** - Pick the simplest failing case
3. **Trace Data Flow** - Follow values from input to output
4. **Verify Assumptions** - Check what you think is true
5. **Compare vs. Spec** - Blue Book is always right

### Common Pitfalls to Avoid

1. ❌ **Don't modify Smalltalk-80.st** - This is the master source
2. ❌ **Don't create duplicate BytecodeDefinitions.cs** - Use shared version
3. ❌ **Don't add 6th SOA element** - Blue Book has exactly 5
4. ❌ **Don't use static VM state** - Per-VM architecture required
5. ❌ **Don't create garbage during bootstrap** - Zero garbage requirement
6. ❌ **Don't forget +1 literal offset** - Header at index 0
7. ❌ **Don't modify bytecode values without Blue Book reference**

---

## 📖 Reference Material

### Essential Files

- **CLAUDE.md** - Development guidelines, complete checklist
- **docs/Interpreter-Architecture.md** - Architectural reference
- **docs/schemas/coldstart-v1.0.0.schema.json** - JSON schema
- **Shared/Core/Bytecodes/BytecodeDefinitions.cs** - All 256 bytecodes
- **Smalltalk-80 Source/Smalltalk-80.st** - Master Smalltalk source

### Key Classes

**Compiler**:
- `FileInFormatAstBuilder` - Parses Smalltalk source into AST
- `BytecodeCompiler` - Emits bytecode from AST
- `ColdStartJsonGenerator` - Generates JSON output

**Interpreter**:
- `JsonColdStartBootstrap` - 10-phase bootstrap orchestrator
- `VirtualMachine` - Bytecode interpreter (all 256 opcodes)
- `SmalltalkHeap` - Object storage and field access
- `LayoutResolver` - Dynamic field lookup by name
- `GarbageCollector` - Mark-and-sweep GC
- `BytecodeOperandValidator` - Validates bytecode operands

### Build Commands

```bash
# Full clean rebuild
dotnet clean
dotnet restore
dotnet build

# Compiler only
cd Smalltalk80.Compiler && dotnet build --no-restore

# Interpreter only
cd Smalltalk80.Interpreter && dotnet build --no-restore

# Generate Smalltalk-80.json from source
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler/Smalltalk80.Compiler.csproj -- Smalltalk-80.st

# Run interpreter with validation
cd Smalltalk80.Interpreter
dotnet run -- repl --gc-log --validate-gc --cold-start-log
```

---

## 🎯 Success Metrics

### Milestone 1: Fix Compiler Bug ✅ When Complete
- [ ] All 4,640 methods pass bytecode operand validation
- [ ] Clean rebuild generates valid Smalltalk-80.json
- [ ] Zero validation errors in Phase 12

### Milestone 2: First Execution ✅ When Complete
- [ ] Successfully execute "1 + 2" and return 3
- [ ] Execute "3 factorial" and return 6
- [ ] Execute "#(1 2 3) at: 2" and return 2
- [ ] Execute "[ 42 ] value" and return 42

### Milestone 3: Full Primitives ✅ When Complete
- [ ] All 111 primitives implemented (no stubs)
- [ ] Process scheduler working
- [ ] Symbol interning runtime API working
- [ ] All primitive tests passing

### Milestone 4: Interactive System ✅ When Complete
- [ ] REPL working
- [ ] Can define new classes at runtime
- [ ] Can define new methods at runtime
- [ ] Can inspect objects

### Milestone 5: Graphics (Long-term Goal)
- [ ] Display provider implemented for host OS
- [ ] BitBlt rendering to screen
- [ ] Mouse and keyboard input working
- [ ] Smalltalk-80 UI running (Browser, Workspace, etc.)

---

## 🚦 Current Priority Queue

### IMMEDIATE (Do This First)
1. Fix instance variable layout bug in compiler
2. Verify all 4,640 methods pass validation
3. Attempt first execution ("1 + 2")

### HIGH (Do This Soon)
4. Implement basic ProcessorScheduler (enough to run simple code)
5. Implement REPL for interactive testing
6. Complete remaining arithmetic primitives

### MEDIUM (Do This Eventually)
7. Implement symbol interning runtime API
8. Complete all 111 primitives
9. Build debugging tools (inspector, tracer, etc.)

### LOW (Nice to Have)
10. Implement become: operation
11. Implement object enumeration
12. Graphics system integration
13. Full Smalltalk-80 UI

---

## 📝 Notes from Previous Sessions

### October 2025 - Bytecode Audit Session

- Discovered return bytecode mappings were completely wrong
- Fixed by auditing against Blue Book specification (Interpreter-Architecture.md lines 1990-2189)
- Created shared BytecodeDefinitions.cs to prevent future mismatches
- Documented shared source approach in both CLAUDE.md and Interpreter-Architecture.md

### Key Insight

The compiler and interpreter must use **identical** bytecode definitions. Any mismatch causes catastrophic failures. Example: if compiler emits 0x78 meaning RETURN_SELF but interpreter thinks 0x78 means RETURN_TOP, every method returning `self` will return top of stack instead.

**Solution**: Single source of truth in `Shared/Core/Bytecodes/BytecodeDefinitions.cs`, linked by both projects via MSBuild.

---

## 🔮 Future Considerations

### Performance Optimizations (Post-MVP)
- JIT compilation for hot methods
- Inline caching for message sends
- Primitive inlining for common operations
- Optimized SmallInteger arithmetic paths

### Advanced Features (Post-MVP)
- Image snapshots (serialize running system to disk)
- Remote debugging protocol
- Multi-VM support (run multiple VMs in same process)
- Concurrent GC (parallel mark phase)

### Platform Support (Post-MVP)
- Cross-platform display providers (Windows, Linux, macOS)
- WebAssembly compilation target
- Mobile platforms (iOS, Android)

---

## ✅ Session Checklist

Before ending a session, ensure:

- [ ] Both projects build successfully (0 errors)
- [ ] No compiler warnings introduced
- [ ] CLAUDE.md updated with completed work
- [ ] Interpreter-Architecture.md updated if architecture changed
- [ ] RESUME-PROMPT.md updated with current status
- [ ] Git commit with detailed message
- [ ] All temporary debug code removed

---

## 🆘 If You Get Stuck

### Compiler Issues
- Read bytecode specification in Interpreter-Architecture.md (lines 1990-2189)
- Verify AST structure matches expected Smalltalk syntax
- Check BytecodeCompiler emission methods for correct opcodes
- Validate JSON against schema (docs/schemas/coldstart-v1.0.0.schema.json)

### Interpreter Issues
- Check SpecialObjectsArray initialization (5 roots only!)
- Verify object reachability from SOA roots
- Enable --gc-log to see garbage collection statistics
- Enable --cold-start-log to see bootstrap phases
- Check Phase 12 validation output for bytecode errors

### Bytecode Validation Errors
- Stack underflow: Missing push before pop
- Stack overflow: Too many pushes without pops
- Invalid temp index: Accessing temp/arg out of range
- Invalid inst var index: Accessing instance variable out of range (CURRENT BUG!)
- Invalid literal index: Accessing literal out of range

### Runtime Errors
- NotImplementedException: Bytecode/primitive not implemented
- NullReferenceException: Likely missing SOA initialization
- IndexOutOfRangeException: Array/object access bug
- InvalidOperationException: Stack manipulation bug

---

**Remember**: The Blue Book (Smalltalk-80 specification) is always right. When in doubt, check the spec. When really in doubt, check CLAUDE.md and Interpreter-Architecture.md.

**Good luck!** 🚀
