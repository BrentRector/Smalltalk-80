# Interpreter - Return Bytecodes

There are six bytecodes that return control and a value from a context; five return the value of a message (invoked explicitly by “↑” or implicitly at the end of a method) and the other one returns the value of a block (invoked implicitly at the end of a block). The distinction between the two types of return is that the former returns to the sender of the home context while the latter returns to the caller of the active context. The values returned from the five return bytecodes are: the receiver (self), true, false, nil, or the top of the stack. The last return bytecode returns the top of the stack as the value of a block.

```smalltalk
returnBytecode
    currentBytecode = 120 ifTrue: [↑self returnValue: receiver
                                       to: self sender].
    currentBytecode = 121 ifTrue: [↑self returnValue: TruePointer
                                       to: self sender].
    currentBytecode = 122 ifTrue: [↑self returnValue: FalsePointer
                                       to: self sender].
    currentBytecode = 123 ifTrue: [↑self returnValue: NilPointer
                                       to: self sender].
    currentBytecode = 124 ifTrue: [↑self returnValue: self popStack
                                       to: self sender].
    currentBytecode = 125 ifTrue: [↑self returnValue: self popStack
                                       to: self caller].
```

The simple way to return a value to a context would be to simply make it the active context and push the value on its stack.

```smalltalk
simpleReturnValue: resultPointer to: contextPointer
    self newActiveContext: contextPointer.
    self push: resultPointer
```

However, there are three situations in which this routine is too simple to work correctly. If the sender of the active context were nil, this routine would store a nil in the interpreter’s active context pointer, bringing the system to an unpleasant halt. In order to prevent this, the actual `returnValue:to:` routine first checks to see if the sender is nil. The interpreter also prevents returns to a context that has already been returned _from_. It does this by storing nil in the instruction pointer of the active context on return and checking for a nil instruction pointer of the context being returned to. Both of these situations can arise since contexts are objects and can be manipulated by user programs as well as by the interpreter. If either situation arises, the interpreter sends a message to the active context informing it of the problem. The third situation will arise in systems that automatically deallocate objects based on their reference counts. The active context may be deallocated as it is returning. It, in turn, may contain the only reference to the result being returned. In this case, the result will be deallocated before it can be pushed on the new context’s stack. Because of these considerations, the `returnValue:to:` routine must be somewhat more complicated.

```smalltalk
returnValue: resultPointer to: contextPointer
    | sendersIP |
    contextPointer = NilPointer
        ifTrue: [self push: activeContext.
                 self push: resultPointer.
                 ↑self sendSelector: CannotReturnSelector
                    argumentCount: 1].
    sendersIP ← memory fetchPointer: InstructionPointerIndex
                            ofObject: contextPointer.
    sendersIP = NilPointer
        ifTrue: [self push: activeContext.
                 self push: resultPointer.
                 ↑self sendSelector: CannotReturnSelector
                    argumentCount: 1].
    memory increaseReferencesTo: resultPointer.
    self returnToActiveContext: contextPointer.
    self push: resultPointer.
    memory decreaseReferencesTo: resultPointer.
```

This routine prevents the deallocation of the result being returned by raising the reference count until it is pushed on the new stack. It could also have pushed the result before switching active contexts. The `returnToActiveContext:` routine is basically the same as the `newActiveContext:` routine except that instead of restoring any cached fields of the context being returned _from_, it stores nil into the sender and instruction pointer fields.

```smalltalk
returnToActiveContext: aContext
    memory increaseReferencesTo: aContext.
    self nilContextFields.
    memory decreaseReferencesTo: activeContext.
    activeContext ← aContext.
    self fetchContextRegisters

nilContextFields
    memory storePointer: SenderIndex
        ofObject: activeContext
        withValue: NilPointer.
    memory storePointer: InstructionPointerIndex
        ofObject: activeContext
        withValue: NilPointer
```
