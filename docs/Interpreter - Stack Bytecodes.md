# Interpreter - Stack Bytecodes

The stack bytecodes all perform simple operations on the active context’s evaluation stack.

- **107** bytecodes *push* an object pointer on the stack  
  - **99** push an object pointer found in the object memory  
  - **7** push a constant object pointer  
  - **1** pushes the interpreter’s active context register (`activeContext`)  
- **18** bytecodes *store* an object pointer found on the stack into the object memory  
  - **17** of these also remove it from the stack  
  - **1** leaves it on the stack  
- **1** bytecode *removes* an object pointer from the stack without storing it anywhere.  

The routines used to manipulate the stack were described in the section of the previous chapter on contexts (`push:`, `popStack`, `pop:`). The `stackBytecode` routine dispatches to the appropriate routine for the current bytecode.

```smalltalk
stackBytecode
  (currentBytecode between: 0 and: 15)
    ifTrue: [↑self pushReceiverVariableBytecode].
  (currentBytecode between: 16 and: 31)
    ifTrue: [↑self pushTemporaryVariableBytecode].
  (currentBytecode between: 32 and: 63)
    ifTrue: [↑self pushLiteralConstantBytecode].
  (currentBytecode between: 64 and: 95)
    ifTrue: [↑self pushLiteralVariableBytecode].
  (currentBytecode between: 96 and: 103)
    ifTrue: [↑self storeAndPopReceiverVariableBytecode].
  (currentBytecode between: 104 and: 111)
    ifTrue: [↑self storeAndPopTemporaryVariableBytecode].
  currentBytecode = 112
    ifTrue: [↑self pushReceiverBytecode].
  (currentBytecode between: 113 and: 119)
    ifTrue: [↑self pushConstantBytecode].
  currentBytecode = 128
    ifTrue: [↑self extendedPushBytecode].
  currentBytecode = 129
    ifTrue: [↑self extendedStoreBytecode].
  currentBytecode = 130
    ifTrue: [↑self extendedStoreAndPopBytecode].
  currentBytecode = 135
    ifTrue: [↑self popStackBytecode].
  currentBytecode = 136
    ifTrue: [↑self duplicateTopBytecode].
  currentBytecode = 137
    ifTrue: [↑self pushActiveContextBytecode].
```

---

There are single byte instructions that push the first 16 instance variables of the receiver and the first 16 temporary frame locations. Recall that the temporary frame includes the arguments and the temporary variables.

```smalltalk
pushReceiverVariableBytecode
  | fieldIndex |
  fieldIndex ← self extractBits: 12 to: 15
               of: currentBytecode.
  self pushReceiverVariable: fieldIndex

pushReceiverVariable: fieldIndex
  self push: (memory fetchPointer: fieldIndex
                   ofObject: receiver)

pushTemporaryVariableBytecode
  | fieldIndex |
  fieldIndex ← self extractBits: 12 to: 15
               of: currentBytecode.
  self pushTemporaryVariable: fieldIndex

pushTemporaryVariable: temporaryIndex
  self push: (self temporary: temporaryIndex)
```

There are also single byte instructions that reference the first 32 locations in the literal frame of the active context’s method. The contents of one of these locations can be pushed with `pushLiteralConstantBytecode`. The contents of the value field of an `Association` stored in one of these locations can be pushed with `pushLiteralVariableBytecode`.

```smalltalk
pushLiteralConstantBytecode
  | fieldIndex |
  fieldIndex ← self extractBits: 11 to: 15
               of: currentBytecode.
  self pushLiteralConstant: fieldIndex

pushLiteralConstant: literalIndex
  self push: (self literal: literalIndex)

pushLiteralVariableBytecode
  | fieldIndex |
  fieldIndex ← self extractBits: 11 to: 15
               of: currentBytecode.
  self pushLiteralVariable: fieldIndex

pushLiteralVariable: literalIndex
  | association |
  association ← self literal: literalIndex.
  self push: (memory fetchPointer: ValueIndex
                         ofObject: association)
```

Associations are objects with two fields, one for a name and one for a value. They are used to implement shared variables (global variables, class variables, and pool variables). The following routine initializes the index used to fetch the value field of Associations.

```smalltalk
initializeAssociationIndex
  ValueIndex ← 1
```

---

The extended push bytecode can perform any of the four operations described above (receiver variable, temporary frame location, literal constant, or literal variable). However, instead of a limit of 16 or 32 variables accessible, it can access up to 64 instance variables, temporary locations, literal constants, or literal variables. The extended push bytecode is followed by a byte whose high order two bits determine which type of push is being done and whose low order six bits determine the offset to use.

```smalltalk
extendedPushBytecode
  | descriptor variableType variableIndex |
  descriptor ← self fetchByte.
  variableType ← self extractBits: 8 to: 9
                   of: descriptor.
  variableIndex ← self extractBits: 10 to: 15
                     of: descriptor.
  variableType = 0 ifTrue: [↑self pushReceiverVariable: variableIndex].
  variableType = 1 ifTrue: [↑self pushTemporaryVariable: variableIndex].
  variableType = 2 ifTrue: [↑self pushLiteralConstant: variableIndex].
  variableType = 3 ifTrue: [↑self pushLiteralVariable: variableIndex]
```

The `pushReceiverBytecode` routine pushes a pointer to the active context’s receiver. This corresponds to the use of `self` or `super` in a Smalltalk method.

```smalltalk
pushReceiverBytecode
  self push: receiver
```

The `duplicateTopBytecode` routine pushes another copy of the object pointer on the top of the stack.

```smalltalk
duplicateTopBytecode
  ↑self push: self stackTop
```

The `pushConstantBytecode` routine pushes one of seven constant object pointers (`true`, `false`, `nil`, `-1`, `0`, `1`, or `2`).

```smalltalk
pushConstantBytecode
  currentBytecode = 113 ifTrue: [↑self push: TruePointer].
  currentBytecode = 114 ifTrue: [↑self push: FalsePointer].
  currentBytecode = 115 ifTrue: [↑self push: NilPointer].
  currentBytecode = 116 ifTrue: [↑self push: MinusOnePointer].
  currentBytecode = 117 ifTrue: [↑self push: ZeroPointer].
  currentBytecode = 118 ifTrue: [↑self push: OnePointer].
  currentBytecode = 119 ifTrue: [↑self push: TwoPointer]
```

The `pushActiveContextBytecode` routine pushes a pointer to the active context itself. This corresponds to the use of `thisContext` in a Smalltalk method.

```smalltalk
pushActiveContextBytecode
  self push: activeContext
```

---

The store bytecodes transfer references in the opposite direction from the push bytecodes; from the top of the stack to the receiver’s instance variables, the temporary frame, or the literal frame. There are single byte versions for storing into the first eight variables of the receiver or the temporary frame and then popping the stack.

```smalltalk
storeAndPopReceiverVariableBytecode
  | variableIndex |
  variableIndex ← self extractBits: 13 to: 15
                   of: currentBytecode.
  memory storePointer: variableIndex
       ofObject: receiver
      withValue: self popStack

storeAndPopTemporaryVariableBytecode
  | variableIndex |
  variableIndex ← self extractBits: 13 to: 15
                   of: currentBytecode.
  memory storePointer: variableIndex + TempFrameStart
       ofObject: homeContext
      withValue: self popStack
```

Stores into variables other than those accessible by the single byte versions are accomplished by two extended store bytecodes. One pops the stack after storing and the other does not. Both extended stores take a following byte of the same form as the extended push. It is illegal, however, to follow an extended store with a byte of the form `10xxxxxx` since this would mean changing the value of a literal constant.

```smalltalk
extendedStoreAndPopBytecode
  self extendedStoreBytecode.
  self popStackBytecode

extendedStoreBytecode
  | descriptor variableType variableIndex association |
  descriptor ← self fetchByte.
  variableType ← self extractBits: 8 to: 9
                   of: descriptor.
  variableIndex ← self extractBits: 10 to: 15
                     of: descriptor.
  variableType = 0 ifTrue:
    [↑memory storePointer: variableIndex
       ofObject: receiver
      withValue: self stackTop].
  variableType = 1 ifTrue:
    [↑memory storePointer: variableIndex + TempFrameStart
       ofObject: homeContext
      withValue: self stackTop].
  variableType = 2 ifTrue:
    [↑self error: 'illegal store'].
  variableType = 3 ifTrue:
    [association ← self literal: variableIndex.
     ↑memory storePointer: ValueIndex
       ofObject: association
      withValue: self stackTop]
```

---

The last stack bytecode removes the top object pointer from the stack without doing anything else with it.

```smalltalk
popStackBytecode
  self popStack
```
