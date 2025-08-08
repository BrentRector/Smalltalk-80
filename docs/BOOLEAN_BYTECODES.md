# Boolean Class Hierarchy - Bytecodes

## Boolean (abstract class)
Instance variables: none
Superclass: Object

### Methods that call subclassResponsibility:
- `& aBoolean` - subclassResponsibility
- `not` - subclassResponsibility  
- `| aBoolean` - subclassResponsibility
- `and: alternativeBlock` - subclassResponsibility
- `ifFalse: alternativeBlock` - subclassResponsibility
- `ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock` - subclassResponsibility
- `ifTrue: alternativeBlock` - subclassResponsibility
- `ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock` - subclassResponsibility
- `or: alternativeBlock` - subclassResponsibility

### Methods with concrete implementations:
- `eqv: aBoolean` - `^self == aBoolean`
  - Bytecode: [176, 16, 192, 180] (push self, push arg0, send ==, return top)
  
- `xor: aBoolean` - `^(self == aBoolean) not`
  - Bytecode: [176, 16, 192, 193, 180] (push self, push arg0, send ==, send not, return top)
  
- `deepCopy` - Returns self (comment: only one instance of True/False should exist)
  - Bytecode: [176] (ReturnReceiver)
  
- `shallowCopy` - Returns self  
  - Bytecode: [176] (ReturnReceiver)
  
- `storeOn: aStream` - `self printOn: aStream`
  - Bytecode: [176, 16, 192, 180] (push self, push arg0, send printOn:, return top)

### Boolean class methods:
- `new` - `self error: 'You may not create any more Booleans - this is two-valued logic'`

## True class
Instance variables: none
Superclass: Boolean

### Logical operations:
- `& alternativeObject` - `^alternativeObject` 
  - Bytecode: [16, 180] (push arg0, return top)
  
- `not` - `^false`
  - Bytecode: [178] (ReturnFalse)
  
- `| aBoolean` - `^self`
  - Bytecode: [176] (ReturnReceiver)

### Controlling:
- `and: alternativeBlock` - `^alternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)
  
- `ifFalse: alternativeBlock` - `^nil`
  - Bytecode: [179] (ReturnNil)
  
- `ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock` - `^trueAlternativeBlock value`
  - Bytecode: [17, 192, 180] (push arg1, send value, return top)
  
- `ifTrue: alternativeBlock` - `^alternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)
  
- `ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock` - `^trueAlternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)
  
- `or: alternativeBlock` - `^self`
  - Bytecode: [176] (ReturnReceiver)

### Printing:
- `printOn: aStream` - `aStream nextPutAll: 'true'`
  - Bytecode: [16, 32, 193, 180] (push arg0, push literal 'true', send nextPutAll:, return top)

## False class
Instance variables: none
Superclass: Boolean

### Logical operations:
- `& alternativeObject` - `^self`
  - Bytecode: [176] (ReturnReceiver)
  
- `not` - `^true`
  - Bytecode: [177] (ReturnTrue)
  
- `| aBoolean` - `^aBoolean`
  - Bytecode: [16, 180] (push arg0, return top)

### Controlling:
- `and: alternativeBlock` - `^self`
  - Bytecode: [176] (ReturnReceiver)
  
- `ifFalse: alternativeBlock` - `^alternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)
  
- `ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock` - `^falseAlternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)
  
- `ifTrue: alternativeBlock` - `^nil`
  - Bytecode: [179] (ReturnNil)
  
- `ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock` - `^falseAlternativeBlock value`
  - Bytecode: [17, 192, 180] (push arg1, send value, return top)
  
- `or: alternativeBlock` - `^alternativeBlock value`
  - Bytecode: [16, 192, 180] (push arg0, send value, return top)

### Printing:
- `printOn: aStream` - `aStream nextPutAll: 'false'`
  - Bytecode: [16, 32, 193, 180] (push arg0, push literal 'false', send nextPutAll:, return top)

## Required Symbols/Selectors:
- `&`
- `not`
- `|`
- `and:`
- `ifFalse:`
- `ifFalse:ifTrue:`
- `ifTrue:`
- `ifTrue:ifFalse:`
- `or:`
- `eqv:`
- `xor:`
- `deepCopy`
- `shallowCopy`
- `storeOn:`
- `printOn:`
- `==`
- `value`
- `nextPutAll:`
- `error:`
- `subclassResponsibility`

## Required Literals:
- String 'true'
- String 'false'
- String 'You may not create any more Booleans - this is two-valued logic'

## Notes:
1. Many control flow methods are normally compiled inline and never actually executed
2. The `value` message needs to be sent to block arguments
3. The concrete True and False classes provide the actual boolean behavior
4. Boolean is abstract and most methods call subclassResponsibility