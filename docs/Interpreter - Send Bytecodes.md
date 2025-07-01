# Interpreter - Send Bytecodes

The send bytecodes cause a message to be sent. The object pointers for the receiver and the arguments of the message are found on the active context’s evaluation stack. The send bytecode determines the selector of the message and how many arguments to take from the stack. The number of arguments is also indicated in the CompiledMethod invoked by the message. The compiler guarantees that this information is redundant except when a CompiledMethod is reached by a perform: message, in which case it is checked to make sure the CompiledMethod takes the right number of arguments. The perform: messages will be discussed in the next chapter in a section on control primitives.

```smalltalk
sendBytecode
    (currentBytecode between: 131 and: 134) ifTrue: [↑self extendedSendBytecode].
    (currentBytecode between: 176 and: 207) ifTrue: [↑self sendSpecialSelectorBytecode].
    (currentBytecode between: 208 and: 255) ifTrue: [↑self sendLiteralSelectorBytecode].
```

The literal-selector bytecodes are single bytes that can specify 0, 1, or 2 arguments and a selector in any one of the first 16 locations of the literal frame. Both the selector index and the argument count are encoded in the bits of the bytecode.

```smalltalk
sendLiteralSelectorBytecode
    | selector |
    selector ← self literal: (self extractBits: 12 to: 15 of: currentBytecode).
    self sendSelector: selector
        argumentCount: (self extractBits: 10 to: 11 of: currentBytecode) - 1
```

Most of the send bytecodes call the `sendSelector:argumentCount:` routine after determining the appropriate selector and argument count. This routine sets up the variables `messageSelector` and `argumentCount`, which are available to the other routines in the interpreter that will lookup the message and perhaps activate a method.

```smalltalk
sendSelector: selector argumentCount: count
    | newReceiver |
    messageSelector ← selector.
    argumentCount ← count.
    newReceiver ← self stackValue: argumentCount.
    self sendSelectorToClass: (memory fetchClassOf: newReceiver)
```

```smalltalk
sendSelectorToClass: classPointer
    self findNewMethodInClass: classPointer.
    self executeNewMethod
```

The interpreter uses a method cache to reduce the number of dictionary lookups necessary to find CompiledMethods associated with selectors. The method cache may be omitted by substituting a call on `lookupMethodInClass:` for the call on `findNewMethodInClass:` in `sendSelectorToClass:` above. The `lookupMethodInClass:` routine is described in the previous chapter in a section on classes. The cache may be implemented in various ways. The following routine uses four sequential locations in an Array for each entry. The four locations store the selector, class, CompiledMethod, and primitive index for the entry. This routine does not allow for reprobes.

```smalltalk
findNewMethodInClass: class
    | hash |
    hash ← (((messageSelector bitAnd: class) bitAnd: 16rFF) bitShift: 2) + 1.
    ((methodCache at: hash) = messageSelector
        and: [(methodCache at: hash + 1) = class])
        ifTrue: [
            newMethod ← methodCache at: hash + 2.
            primitiveIndex ← methodCache at: hash + 3]
        ifFalse: [
            self lookupMethodInClass: class.
            methodCache at: hash put: messageSelector.
            methodCache at: hash + 1 put: class.
            methodCache at: hash + 2 put: newMethod.
            methodCache at: hash + 3 put: primitiveIndex]
```

The method cache is initialized with the following routine.

```smalltalk
initializeMethodCache
    methodCacheSize ← 1024.
    methodCache ← Array new: methodCacheSize
```

The `executeNewMethod` routine calls a primitive routine if one is associated with the CompiledMethod. The `primitiveResponse` routine returns false if no primitive is indicated or the primitive routine is unable to produce a result. In that case, the CompiledMethod is activated. Primitive routines and the `primitiveResponse` routine will be described in the next chapter.

```smalltalk
executeNewMethod
    self primitiveResponse
        ifFalse: [self activateNewMethod]
```

The routine that activates a method creates a MethodContext and transfers the receiver and arguments from the currently active context’s stack to the new context’s stack. It then makes this new context be the interpreter’s active context.

```smalltalk
activateNewMethod
    | contextSize newContext newReceiver |
    (self largeContextFlagOf: newMethod) = 1
        ifTrue: [contextSize ← 32 + TempFrameStart]
        ifFalse: [contextSize ← 12 + TempFrameStart].
    newContext ← memory instantiateClass: ClassMethodContextPointer
        withPointers: contextSize.
    memory storePointer: SenderIndex
        ofObject: newContext
        withValue: activeContext.
    self storeInstructionPointerValue:
        (self initialInstructionPointerOfMethod: newMethod)
        inContext: newContext.
    self storeStackPointerValue: (self temporaryCountOf: newMethod)
        inContext: newContext.
    memory storePointer: MethodIndex
        ofObject: newContext
        withValue: newMethod.
    self transfer: argumentCount + 1
        fromIndex: stackPointer - argumentCount
        ofObject: activeContext
        toIndex: ReceiverIndex
        ofObject: newContext.
    self pop: argumentCount + 1.
    self newActiveContext: newContext
```

There are four extended-send bytecodes. The first two have the same effect as the literal-selector bytecodes except that the selector index and argument count are found in one or two following bytes instead of in the bytecode itself. The other two extended-send bytecodes are used for superclass messages.

```smalltalk
extendedSendBytecode
    currentBytecode = 131 ifTrue: [↑self singleExtendedSendBytecode].
    currentBytecode = 132 ifTrue: [↑self doubleExtendedSendBytecode].
    currentBytecode = 133 ifTrue: [↑self singleExtendedSuperBytecode].
    currentBytecode = 134 ifTrue: [↑self doubleExtendedSuperBytecode]
```

The first form of extended send is followed by a single byte specifying the number of arguments in its high order three bits and selector index in the low order five bits.

```smalltalk
singleExtendedSendBytecode
    | descriptor selectorIndex |
    descriptor ← self fetchByte.
    selectorIndex ← self extractBits: 11 to: 15 of: descriptor.
    self sendSelector: (self literal: selectorIndex)
        argumentCount: (self extractBits: 8 to: 10 of: descriptor)
```

The second form of extended send bytecode is followed by two bytes; the first is the number of arguments and the second is the index of the selector in the literal frame.

```smalltalk
doubleExtendedSendBytecode
    | count selector |
    count ← self fetchByte.
    selector ← self literal: self fetchByte.
    self sendSelector: selector
        argumentCount: count
```

When the compiler encounters a message to super in a symbolic method, it uses the bytecode that pushes self for the receiver, but it uses an extended-super bytecode to indicate the selector instead of a regular send bytecode. The two extended-super bytecodes are similar to the two extended-send bytecodes. The first is followed by a single byte and the second by two bytes that are interpreted exactly as for the extended-send bytecodes. The only difference in what these bytecodes do is that they start the message lookup in the superclass of the class in which the current CompiledMethod was found. Note that this is not necessarily the immediate superclass of self. Specifically, it will not be the immediate superclass of self if the CompiledMethod containing the extended-super bytecode was found in a superclass of self originally. All CompiledMethods that contain extended-super bytecodes have the class in which they are found as their last literal variable.

```smalltalk
singleExtendedSuperBytecode
    | descriptor selectorIndex methodClass |
    descriptor ← self fetchByte.
    argumentCount ← self extractBits: 8 to: 10 of: descriptor.
    selectorIndex ← self extractBits: 11 to: 15 of: descriptor.
    messageSelector ← self literal: selectorIndex.
    methodClass ← self methodClassOf: method.
    self sendSelectorToClass: (self superclassOf: methodClass)
```

```smalltalk
doubleExtendedSuperBytecode
    | methodClass |
    argumentCount ← self fetchByte.
    messageSelector ← self literal: self fetchByte.
    methodClass ← self methodClassOf: method.
    self sendSelectorToClass: (self superclassOf: methodClass)
```

The set of special selectors can be used in a message without being included in the literal frame. An Array in the object memory contains the object pointers of the selectors in alternating locations. The argument count for each selector is stored in the location following the selector’s object pointer. The `specialSelectorPrimitiveResponse` routine will be described in the next chapter.

```smalltalk
sendSpecialSelectorBytecode
    | selectorIndex selector count |
    self specialSelectorPrimitiveResponse
        ifFalse: [
            selectorIndex ← (currentBytecode - 176) * 2.
            selector ← memory fetchPointer: selectorIndex
                ofObject: SpecialSelectorsPointer.
            count ← self fetchInteger: selectorIndex + 1
                ofObject: SpecialSelectorsPointer.
            self sendSelector: selector
                argumentCount: count]
```
