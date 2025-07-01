The interpreter uses *contexts* to represent the state of its execution of `CompiledMethod`s and blocks. A context can be a `MethodContext` or a `BlockContext`. A `MethodContext` represents the execution of a `CompiledMethod` that was invoked by a message. Figure 27.5 shows a `MethodContext` and its `CompiledMethod`.

**Figure 27.5 – MethodContext and its CompiledMethod**

```
      +--------------------+
      | sender             |◀────────┐
      +--------------------+         │
      | instructionPointer |         │
      +--------------------+         │
      | stackPointer       |         │
      +--------------------+         │
      | method             |────────►│ +----------------+
      +--------------------+         │ | header         |
      | (unused)           |         │ +----------------+
      +--------------------+         │ | literal frame  |
      | receiver           |         │ +----------------+
      +--------------------+         │ | + bytecodes +  |
      | arguments          |         │ |   +   +       |
      | other temporaries  |         │ +----------------+
      | stack contents     |         │
      | ...                |         │
      +--------------------+         │
                                     └─ back arrow to stack contents
```

A `BlockContext` represents a block encountered in a `CompiledMethod`. A `BlockContext` refers to the `MethodContext` whose `CompiledMethod` contained the block it represents. This is called the `BlockContext`’s *home*. Figure 27.6 shows a `BlockContext` and its home.

**Figure 27.6 – BlockContext and its home**

```
      (MethodContext as above)
                  │
                  │
                arrow
                  │
      +--------------------+
      | caller             |
      +--------------------+
      | instructionPointer |
      +--------------------+
      | stackPointer       |
      +--------------------+
      | argumentCount      |
      +--------------------+
      | initialIP          |
      +--------------------+
      | home               |
      +--------------------+
      | stack contents     |
      | ...                |
      +--------------------+
                  │
                  │
                arrow back to MethodContext stack contents
```

The indices used to access the fields of contexts are initialized by the following routine.

```smalltalk
initializeContextIndices
    "Class MethodContext"
    SenderIndex ← 0.
    InstructionPointerIndex ← 1.
    StackPointerIndex ← 2.
    MethodIndex ← 3.
    ReceiverIndex ← 5.
    TempFrameStart ← 6.
    "Class BlockContext"
    CallerIndex ← 0.
    BlockArgumentCountIndex ← 3.
    InitialIPIndex ← 4.
    HomeIndex ← 5.
```

Both kinds of context have six fixed fields corresponding to six named instance variables. These fixed fields are followed by some indexable fields. The indexable fields are used to store the temporary frame (arguments and temporary variables) followed by the contents of the evaluation stack. The following routines are used to fetch and store the instruction pointer and stack pointer stored in a context.

```smalltalk
instructionPointerOfContext: contextPointer
    ↑self fetchInteger: InstructionPointerIndex
        ofObject: contextPointer

storeInstructionPointerValue: value inContext: contextPointer
    self storeInteger: InstructionPointerIndex
        ofObject: contextPointer
        withValue: value

stackPointerOfContext: contextPointer
    ↑self fetchInteger: StackPointerIndex
        ofObject: contextPointer

storeStackPointerValue: value inContext: contextPointer
    self storeInteger: StackPointerIndex
        ofObject: contextPointer
        withValue: value
```

A `BlockContext` stores the number of block arguments it expects in one of its fields.

```smalltalk
argumentCountOfBlock: blockPointer
    ↑self fetchInteger: BlockArgumentCountIndex
        ofObject: blockPointer
```

The context that represents the `CompiledMethod` or block currently being executed is called the *active context*. The interpreter caches in its registers the contents of the parts of the active context it uses most often. These registers are:

**Context-related Registers of the Interpreter**

| Register           | Description                                                                                                          |
|--------------------|----------------------------------------------------------------------------------------------------------------------|
| activeContext      | This is the active context itself. It is either a `MethodContext` or a `BlockContext`.                              |
| homeContext        | If the active context is a `MethodContext`, the home context is the same context. If it is a `BlockContext`, the home context is the contents of the home field of the active context (always a `MethodContext`). |
| method             | This is the `CompiledMethod` that contains the bytecodes the interpreter is executing.                              |
| receiver           | This is the object that received the message that invoked the home context’s method.                                |
| instructionPointer | This is the byte index of the next bytecode of the method to be executed.                                           |
| stackPointer       | This is the index of the field of the active context containing the top of the stack.                                |

Whenever the active context changes (when a new `CompiledMethod` is invoked, when a `CompiledMethod` returns or when a process switch occurs), all of these registers must be updated using the following routine.

```smalltalk
fetchContextRegisters
    (self isBlockContext: activeContext)
        ifTrue: [homeContext ← memory fetchPointer: HomeIndex ofObject: activeContext]
        ifFalse: [homeContext ← activeContext].
    receiver ← memory fetchPointer: ReceiverIndex ofObject: homeContext.
    method ← memory fetchPointer: MethodIndex ofObject: homeContext.
    instructionPointer ← (self instructionPointerOfContext: activeContext) - 1.
    stackPointer ← (self stackPointerOfContext: activeContext) + TempFrameStart - 1.
```

Note that the `receiver` and `method` are fetched from the `homeContext` and the `instructionPointer` and `stackPointer` are fetched from the `activeContext`. The interpreter tells the difference between `MethodContext`s and `BlockContext`s based on the fact that `MethodContext`s store the method pointer (an object pointer) and `BlockContext`s store the number of block arguments (an integer pointer) in the same field. If this location contains an integer pointer, the context is a `BlockContext`; otherwise, it is a `MethodContext`. The distinction could be made on the basis of the class of the context, but special provision would have to be made for subclasses of `MethodContext` and `BlockContext`.

```smalltalk
isBlockContext: contextPointer
    | methodOrArguments |
    methodOrArguments ← memory fetchPointer: MethodIndex ofObject: contextPointer.
    ↑memory isIntegerObject: methodOrArguments
```

Before a new context becomes the active context, the values of the `instructionPointer` and `stackPointer` must be stored into the active context with the following routine.

```smalltalk
storeContextRegisters
    self storeInstructionPointerValue: instructionPointer + 1 inContext: activeContext.
    self storeStackPointerValue: stackPointer - TempFrameStart + 1 inContext: activeContext.
```

The values of the other cached registers do not change so they do not need to be stored back into the context. The instruction pointer stored in a context is a one-relative index to the method’s fields because subscripting in Smalltalk (i.e., the `at:` message) takes one-relative indices. The memory, however, uses zero-relative indices; so the `fetchContextRegisters` routine subtracts one to convert it to a memory index and the `storeContextRegisters` routine adds the one back in. The stack pointer stored in a context tells how far the top of the evaluation stack is beyond the fixed fields of the context (i.e., how far after the start of the temporary frame) because subscripting in Smalltalk takes fixed fields into account and fetches from the indexable fields after them. The memory, however, wants an index relative to the start of the object; so the `fetchContextRegisters` routine adds in the offset of the start of the temporary frame (a constant) and the `storeContextRegisters` routine subtracts the offset.

The following routines perform various operations on the stack of the active context.

```smalltalk
push: object
    stackPointer ← stackPointer + 1.
    memory storePointer: stackPointer
        ofObject: activeContext
        withValue: object

popStack
    | stackTop |
    stackTop ← memory fetchPointer: stackPointer
        ofObject: activeContext.
    stackPointer ← stackPointer - 1.
    ↑stackTop

stackTop
    ↑memory fetchPointer: stackPointer
        ofObject: activeContext

stackValue: offset
    ↑memory fetchPointer: stackPointer - offset
        ofObject: activeContext

pop: number
    stackPointer ← stackPointer - number

unPop: number
    stackPointer ← stackPointer + number
```

The active context register must count as a reference to the part of the object memory that deallocates unreferenced objects. If the object memory maintains dynamic reference counts, the routine to change active contexts must perform the appropriate reference counting.

```smalltalk
newActiveContext: aContext
    self storeContextRegisters.
    memory decreaseReferencesTo: activeContext.
    activeContext ← aContext.
    memory increaseReferencesTo: activeContext.
    self fetchContextRegisters
```

The following routines fetch fields of contexts needed by the interpreter infrequently enough that they are not cached in registers. The sender is the context to be returned to when a `CompiledMethod` returns a value (either because of a “↑” or at the end of the method). Since an explicit return from within a block should return from the `CompiledMethod` enclosing the block, the sender is fetched from the home context.

```smalltalk
sender
    ↑memory fetchPointer: SenderIndex
        ofObject: homeContext
```

The caller is the context to be returned to when a `BlockContext` returns a value (at the end of the block).

```smalltalk
caller
    ↑memory fetchPointer: SenderIndex
        ofObject: activeContext
```

Since temporaries referenced in a block are the same as those referenced in the `CompiledMethod` enclosing the block, the temporaries are fetched from the home context.

```smalltalk
temporary: offset
    ↑memory fetchPointer: offset + TempFrameStart
        ofObject: homeContext
```

The following routine provides convenient access to the literals of the currently executing `CompiledMethod`.

```smalltalk
literal: offset
    ↑self literal: offset
        ofMethod: method
```
