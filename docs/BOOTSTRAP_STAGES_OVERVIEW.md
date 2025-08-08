# Bootstrap Stages Overview

## The Three-Stage Bootstrap Process

The Smalltalk-80 bootstrap proceeds in three distinct stages, each building on the previous one's capabilities:

## 📦 Stage 1: VM Primitive Bootstrap (External)

**Goal**: Create foundational classes using VM primitives

**Mechanism**: C# orchestrates VM primitive calls

**Input**: `MinimalBootstrap.st` parsed by C#

**Key Operations**:
```csharp
// C# code orchestrating VM primitives
int metaclassId = vm.PrimitiveNewMetaclass();
vm.PrimitiveSetClass(metaclassId, metaclassId); // Self-referential
int protoObjectId = vm.PrimitiveNewClass();
vm.PrimitiveSetSlot(protoObjectId, SLOT_SUPERCLASS, vm.PrimitiveNil());
```

**Result**: 
- ProtoObject, Object, Behavior, Class, Metaclass exist
- Symbol, Dictionary, Array, Boolean hierarchy exist
- Basic primitives are wired up
- Methods are installed via VM compilation

## 🔄 Stage 2: Smalltalk Self-Expansion (Hybrid)

**Goal**: Use Smalltalk to expand the system

**Mechanism**: Mix of Smalltalk messages and VM primitives

**Input**: `Stage2Bootstrap.st` loaded via Smalltalk

**Key Operations**:
```smalltalk
"Now using Smalltalk itself!"
Object subclass: #Message
    instanceVariableNames: 'selector arguments'
    classVariableNames: ''
    category: 'Kernel-Methods'.

Message compile: 'sendTo: receiver
    ^receiver perform: selector withArguments: arguments'.

"Create system dictionary"
Smalltalk := SystemDictionary new.
Smalltalk at: #Object put: Object.
```

**Result**:
- Message objects for reflection
- Complete control structures
- Basic collections (OrderedCollection, String)
- Compiler stubs
- System dictionary populated
- Block contexts for closures

## 🚀 Stage 3: Full System Bootstrap (Internal)

**Goal**: Complete the Smalltalk system

**Mechanism**: Pure Smalltalk - system bootstraps itself

**Input**: Standard library files loaded via Smalltalk compiler

**Key Operations**:
```smalltalk
"System can now compile and load arbitrary Smalltalk code"
Compiler new
    compile: sourceString
    in: class
    classified: category.

"Load the rest of the system"
(FileStream fileNamed: 'Collections.st') fileIn.
(FileStream fileNamed: 'Graphics.st') fileIn.
(FileStream fileNamed: 'Tools.st') fileIn.
```

**Result**:
- Full compiler and parser
- Complete collection hierarchy
- Stream and file system
- Graphics and UI framework
- Development tools
- System is fully self-hosting

## 🔑 Key Transitions

### Stage 1 → Stage 2 Transition
- **Before**: Only VM primitives available
- **After**: Can send Smalltalk messages
- **Enabler**: `perform:` and method lookup work

### Stage 2 → Stage 3 Transition
- **Before**: Limited to pre-compiled methods
- **After**: Can compile arbitrary source
- **Enabler**: Compiler can parse and generate CompiledMethods

## 📊 Capability Progression

| Capability | Stage 1 | Stage 2 | Stage 3 |
|-----------|---------|---------|---------|
| Create classes | Via VM primitives | Via Smalltalk messages | Via class browser |
| Install methods | Via VM compilation | Via compile: | Via accept in browser |
| Object creation | primitiveNew | Class >> new | Multiple factories |
| Message dispatch | Primitive only | perform: available | Full dispatch |
| Compilation | External only | Stubs + primitive | Full compiler |
| System introspection | None | Basic | Full reflection |
| File I/O | None | None | Full file system |
| UI | None | None | Full graphics |

## 🎯 Implementation Files

### Stage 1 Files
- `ISmalltalkVM.cs` - VM primitive interface
- `VMPrimitiveBootstrapGenerator.cs` - Generates Stage 1 bootstrap
- `MinimalBootstrap.st` - Minimal class definitions
- `GeneratedVMBootstrap.cs` - Generated primitive orchestration

### Stage 2 Files
- `Stage2Bootstrap.cs` - C# code to initiate Stage 2
- `Stage2Bootstrap.st` - Smalltalk code for self-expansion
- `MessageBootstrap.st` - Message and reflection setup
- `CollectionsBootstrap.st` - Extended collections

### Stage 3 Files
- `StandardLibrary.st` - Full class library
- `Compiler.st` - Complete compiler implementation
- `Graphics.st` - Graphics and UI framework
- `Tools.st` - Development environment

## 🔍 Critical Insights

1. **Stage 1 is External**: C# drives everything via primitives
2. **Stage 2 is Hybrid**: Smalltalk takes over but still needs some primitive support
3. **Stage 3 is Internal**: Smalltalk is self-sufficient

4. **The Magic Moment**: When `perform:` starts working in Stage 2, Smalltalk comes alive

5. **Class Variables**: Must be at slot 6 throughout all stages

6. **Metaclass Loop**: ProtoObject class → Class creates essential circularity

## 📈 Bootstrap Metrics

### Stage 1
- ~15 core classes
- ~50 primitive methods
- ~500 lines of generated C# code
- ~200 lines of Smalltalk

### Stage 2
- ~50 classes
- ~200 methods
- ~1000 lines of Smalltalk
- Minimal C# involvement

### Stage 3
- ~300+ classes
- ~3000+ methods
- ~10,000+ lines of Smalltalk
- Zero C# involvement

## 🎉 Success Criteria

The bootstrap is complete when:

✅ Can create classes from within Smalltalk
✅ Can compile methods from source strings
✅ Can load .st files
✅ Can run a workspace
✅ Can browse and edit classes
✅ System can recreate itself from source

This three-stage process takes Smalltalk from VM primitives to a fully self-hosting development environment!