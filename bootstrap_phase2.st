"================================================================================================"
"  bootstrap_phase2.st - Phase 2 Bootstrap Definitions"
"  Loaded after Phase 1 primitive bootstrap, executed via bytecode/messaging"
"================================================================================================"

"================================================================================================"
"  Boolean Hierarchy - Control Structures Foundation"
"================================================================================================"

Object subclass: #Boolean
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!Boolean methodsFor: 'logical operations'!
& aBoolean
    "Evaluating conjunction -- AND"
    self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
| aBoolean
    "Evaluating disjunction -- OR"
    self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
not
    "Negation"
    self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: aBlock
    "Evaluating conjunction with a block"
    self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
or: aBlock
    "Evaluating disjunction with a block"
    self subclassResponsibility! !

Boolean subclass: #True
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!True methodsFor: 'controlling'!
ifTrue: trueBlock
    "Execute trueBlock since receiver is true"
    ^trueBlock value! !

!True methodsFor: 'controlling'!
ifFalse: falseBlock
    "Answer nil since receiver is true"
    ^nil! !

!True methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
    "Execute trueBlock since receiver is true"
    ^trueBlock value! !

!True methodsFor: 'logical operations'!
& aBoolean
    "Answer aBoolean since receiver is true"
    ^aBoolean! !

!True methodsFor: 'logical operations'!
| aBoolean
    "Answer true since receiver is true"
    ^true! !

!True methodsFor: 'logical operations'!
not
    "Answer false since receiver is true"
    ^false! !

!True methodsFor: 'logical operations'!
and: aBlock
    "Evaluate aBlock since receiver is true"
    ^aBlock value! !

!True methodsFor: 'logical operations'!
or: aBlock
    "Answer true without evaluating aBlock"
    ^true! !

Boolean subclass: #False
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!False methodsFor: 'controlling'!
ifTrue: trueBlock
    "Answer nil since receiver is false"
    ^nil! !

!False methodsFor: 'controlling'!
ifFalse: falseBlock
    "Execute falseBlock since receiver is false"
    ^falseBlock value! !

!False methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
    "Execute falseBlock since receiver is false"
    ^falseBlock value! !

!False methodsFor: 'logical operations'!
& aBoolean
    "Answer false since receiver is false"
    ^false! !

!False methodsFor: 'logical operations'!
| aBoolean
    "Answer aBoolean since receiver is false"
    ^aBoolean! !

!False methodsFor: 'logical operations'!
not
    "Answer true since receiver is false"
    ^true! !

!False methodsFor: 'logical operations'!
and: aBlock
    "Answer false without evaluating aBlock"
    ^false! !

!False methodsFor: 'logical operations'!
or: aBlock
    "Evaluate aBlock since receiver is false"
    ^aBlock value! !

"================================================================================================"
"  UndefinedObject - The nil class"
"================================================================================================"

Object subclass: #UndefinedObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!UndefinedObject methodsFor: 'testing'!
isNil
    "Answer true since receiver is nil"
    ^true! !

!UndefinedObject methodsFor: 'testing'!
notNil
    "Answer false since receiver is nil"
    ^false! !

!UndefinedObject methodsFor: 'printing'!
printString
    "Answer a string representation of nil"
    ^'nil'! !

!UndefinedObject methodsFor: 'controlling'!
ifNil: aBlock
    "Execute aBlock since receiver is nil"
    ^aBlock value! !

!UndefinedObject methodsFor: 'controlling'!
ifNotNil: aBlock
    "Answer nil since receiver is nil"
    ^nil! !

!UndefinedObject methodsFor: 'controlling'!
ifNil: nilBlock ifNotNil: notNilBlock
    "Execute nilBlock since receiver is nil"
    ^nilBlock value! !

"Register nil as global"
Smalltalk at: #nil put: UndefinedObject new!

"================================================================================================"
"  Message - Reified messages for reflection"
"================================================================================================"

Object subclass: #Message
    instanceVariableNames: 'selector arguments'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!

!Message methodsFor: 'accessing'!
selector
    "Answer the message selector"
    ^selector! !

!Message methodsFor: 'accessing'!
selector: aSymbol
    "Set the message selector"
    selector := aSymbol! !

!Message methodsFor: 'accessing'!
arguments
    "Answer the message arguments"
    ^arguments! !

!Message methodsFor: 'accessing'!
arguments: anArray
    "Set the message arguments"
    arguments := anArray! !

!Message methodsFor: 'sending'!
sendTo: receiver
    "Send this message to the receiver"
    ^receiver perform: selector withArguments: arguments! !

!Message class methodsFor: 'instance creation'!
selector: aSymbol arguments: argArray
    "Create a new message with given selector and arguments"
    ^self new
        selector: aSymbol;
        arguments: argArray;
        yourself! !

"================================================================================================"
"  Symbol Enhancement"
"================================================================================================"

!Symbol methodsFor: 'comparing'!
= aSymbol
    "Test equality with another symbol"
    ^self == aSymbol! !

!Symbol methodsFor: 'comparing'!
hash
    "Answer hash value for symbol"
    ^self asString hash! !

!Symbol methodsFor: 'converting'!
asString
    "Convert symbol to string"
    <primitive: 118>
    self primitiveFailed! !

!Symbol methodsFor: 'converting'!
asSymbol
    "Answer self since already a symbol"
    ^self! !

!Symbol class methodsFor: 'instance creation'!
intern: aString
    "Answer the unique symbol with aString's characters"
    <primitive: 119>
    self primitiveFailed! !

"================================================================================================"
"  Dictionary Enhancement"
"================================================================================================"

!Dictionary methodsFor: 'accessing'!
at: key
    "Answer the value at key"
    | index |
    index := keys indexOf: key.
    index = 0 ifTrue: [^nil].
    ^values at: index! !

!Dictionary methodsFor: 'accessing'!
at: key put: value
    "Store value at key"
    | index |
    index := keys indexOf: key.
    index = 0
        ifTrue: [
            keys := keys copyWith: key.
            values := values copyWith: value]
        ifFalse: [
            values at: index put: value].
    ^value! !

!Dictionary methodsFor: 'testing'!
includesKey: key
    "Answer whether dictionary includes key"
    ^(keys indexOf: key) > 0! !

!Dictionary methodsFor: 'removing'!
removeKey: key
    "Remove key and its value"
    | index |
    index := keys indexOf: key.
    index = 0 ifTrue: [^self error: 'Key not found'].
    keys := keys copyWithoutIndex: index.
    values := values copyWithoutIndex: index! !

"================================================================================================"
"  BlockContext - Closures and Control Flow"
"================================================================================================"

Object subclass: #BlockContext
    instanceVariableNames: 'nargs startpc home'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!

!BlockContext methodsFor: 'evaluating'!
value
    "Evaluate block with no arguments"
    <primitive: 81>
    self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1
    "Evaluate block with one argument"
    <primitive: 81>
    self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1 value: arg2
    "Evaluate block with two arguments"
    <primitive: 81>
    self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1 value: arg2 value: arg3
    "Evaluate block with three arguments"
    <primitive: 81>
    self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
valueWithArguments: argArray
    "Evaluate block with arguments from array"
    <primitive: 82>
    self primitiveFailed! !

!BlockContext methodsFor: 'controlling'!
whileTrue: aBlock
    "Evaluate aBlock while receiver evaluates to true"
    self value ifTrue: [
        aBlock value.
        self whileTrue: aBlock]! !

!BlockContext methodsFor: 'controlling'!
whileFalse: aBlock
    "Evaluate aBlock while receiver evaluates to false"
    self value ifFalse: [
        aBlock value.
        self whileFalse: aBlock]! !

!BlockContext methodsFor: 'controlling'!
whileTrue
    "Evaluate receiver while it evaluates to true"
    self value ifTrue: [self whileTrue]! !

!BlockContext methodsFor: 'controlling'!
whileFalse
    "Evaluate receiver while it evaluates to false"
    self value ifFalse: [self whileFalse]! !

!BlockContext methodsFor: 'controlling'!
repeat
    "Evaluate receiver repeatedly"
    self value.
    self repeat! !

"================================================================================================"
"  Collection Hierarchy"
"================================================================================================"

Object subclass: #Collection
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Abstract'!

!Collection methodsFor: 'testing'!
isEmpty
    "Answer whether receiver is empty"
    ^self size = 0! !

!Collection methodsFor: 'testing'!
notEmpty
    "Answer whether receiver contains elements"
    ^self isEmpty not! !

!Collection methodsFor: 'accessing'!
size
    "Answer the number of elements"
    self subclassResponsibility! !

!Collection methodsFor: 'enumerating'!
do: aBlock
    "Evaluate aBlock with each element"
    self subclassResponsibility! !

!Collection methodsFor: 'enumerating'!
collect: aBlock
    "Answer new collection with results of aBlock for each element"
    | result |
    result := self species new.
    self do: [:each | result add: (aBlock value: each)].
    ^result! !

!Collection methodsFor: 'enumerating'!
select: aBlock
    "Answer elements for which aBlock evaluates to true"
    | result |
    result := self species new.
    self do: [:each | 
        (aBlock value: each) ifTrue: [result add: each]].
    ^result! !

!Collection methodsFor: 'enumerating'!
reject: aBlock
    "Answer elements for which aBlock evaluates to false"
    ^self select: [:each | (aBlock value: each) not]! !

!Collection methodsFor: 'enumerating'!
detect: aBlock
    "Answer first element for which aBlock evaluates to true"
    self do: [:each |
        (aBlock value: each) ifTrue: [^each]].
    ^nil! !

!Collection methodsFor: 'enumerating'!
inject: initialValue into: binaryBlock
    "Accumulate a value"
    | result |
    result := initialValue.
    self do: [:each |
        result := binaryBlock value: result value: each].
    ^result! !

!Collection methodsFor: 'adding'!
add: anObject
    "Add anObject to receiver"
    self subclassResponsibility! !

!Collection methodsFor: 'private'!
species
    "Answer the class to use for creating new collections"
    ^self class! !

Collection subclass: #SequenceableCollection
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Abstract'!

!SequenceableCollection methodsFor: 'accessing'!
at: index
    "Answer element at index"
    self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
at: index put: anObject
    "Store anObject at index"
    self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
first
    "Answer first element"
    ^self at: 1! !

!SequenceableCollection methodsFor: 'accessing'!
last
    "Answer last element"
    ^self at: self size! !

!SequenceableCollection methodsFor: 'accessing'!
indexOf: anObject
    "Answer index of first occurrence of anObject, 0 if none"
    1 to: self size do: [:i |
        (self at: i) = anObject ifTrue: [^i]].
    ^0! !

!SequenceableCollection methodsFor: 'enumerating'!
do: aBlock
    "Evaluate aBlock with each element in sequence"
    1 to: self size do: [:index |
        aBlock value: (self at: index)]! !

!SequenceableCollection methodsFor: 'copying'!
copyWith: anObject
    "Answer a copy with anObject added at end"
    | newColl |
    newColl := self species new: self size + 1.
    1 to: self size do: [:i |
        newColl at: i put: (self at: i)].
    newColl at: self size + 1 put: anObject.
    ^newColl! !

!SequenceableCollection methodsFor: 'copying'!
copyWithoutIndex: index
    "Answer a copy without element at index"
    | newColl j |
    newColl := self species new: self size - 1.
    j := 1.
    1 to: self size do: [:i |
        i = index ifFalse: [
            newColl at: j put: (self at: i).
            j := j + 1]].
    ^newColl! !

SequenceableCollection subclass: #OrderedCollection
    instanceVariableNames: 'array firstIndex lastIndex'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Sequenceable'!

!OrderedCollection methodsFor: 'initialization'!
initialize
    "Initialize with capacity 10"
    array := Array new: 10.
    firstIndex := 5.
    lastIndex := 4! !

!OrderedCollection methodsFor: 'accessing'!
size
    "Answer number of elements"
    ^lastIndex - firstIndex + 1! !

!OrderedCollection methodsFor: 'accessing'!
at: index
    "Answer element at index"
    (index < 1 or: [index > self size])
        ifTrue: [self error: 'Index out of bounds'].
    ^array at: firstIndex + index - 1! !

!OrderedCollection methodsFor: 'accessing'!
at: index put: anObject
    "Store anObject at index"
    (index < 1 or: [index > self size])
        ifTrue: [self error: 'Index out of bounds'].
    ^array at: firstIndex + index - 1 put: anObject! !

!OrderedCollection methodsFor: 'adding'!
add: anObject
    "Add anObject at end"
    lastIndex := lastIndex + 1.
    lastIndex > array size ifTrue: [self grow].
    array at: lastIndex put: anObject.
    ^anObject! !

!OrderedCollection methodsFor: 'adding'!
addFirst: anObject
    "Add anObject at beginning"
    firstIndex := firstIndex - 1.
    firstIndex < 1 ifTrue: [self grow].
    array at: firstIndex put: anObject.
    ^anObject! !

!OrderedCollection methodsFor: 'private'!
grow
    "Double the capacity"
    | newArray newFirst |
    newArray := Array new: array size * 2.
    newFirst := newArray size // 3.
    1 to: self size do: [:i |
        newArray at: newFirst + i - 1 put: (array at: firstIndex + i - 1)].
    array := newArray.
    lastIndex := newFirst + self size - 1.
    firstIndex := newFirst! !

!OrderedCollection class methodsFor: 'instance creation'!
new
    "Create new empty collection"
    ^super new initialize! !

!OrderedCollection class methodsFor: 'instance creation'!
new: size
    "Create collection with given capacity"
    ^self new! !

"================================================================================================"
"  String - Text manipulation"
"================================================================================================"

Object subclass: #String
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Text'!

!String methodsFor: 'accessing'!
at: index
    "Answer character at index"
    <primitive: 63>
    self primitiveFailed! !

!String methodsFor: 'accessing'!
at: index put: aCharacter
    "Store character at index"
    <primitive: 64>
    self primitiveFailed! !

!String methodsFor: 'accessing'!
size
    "Answer number of characters"
    <primitive: 62>
    self primitiveFailed! !

!String methodsFor: 'concatenation'!
, aString
    "Concatenate with aString"
    | newString |
    newString := String new: self size + aString size.
    1 to: self size do: [:i | 
        newString at: i put: (self at: i)].
    1 to: aString size do: [:i | 
        newString at: self size + i put: (aString at: i)].
    ^newString! !

!String methodsFor: 'converting'!
asSymbol
    "Answer the unique symbol with my characters"
    ^Symbol intern: self! !

!String methodsFor: 'converting'!
asString
    "Answer self since already a string"
    ^self! !

!String methodsFor: 'printing'!
printString
    "Answer a string representation"
    ^'''' , self , ''''! !

!String methodsFor: 'printing'!
printOn: aStream
    "Print quoted string on stream"
    aStream nextPut: $'.
    self do: [:char | aStream nextPut: char].
    aStream nextPut: $'! !

!String class methodsFor: 'instance creation'!
new: size
    "Create string of given size"
    <primitive: 71>
    self primitiveFailed! !

!String class methodsFor: 'instance creation'!
with: aCharacter
    "Create string with single character"
    ^(self new: 1) at: 1 put: aCharacter; yourself! !

!String class methodsFor: 'constants'!
cr
    "Answer carriage return string"
    ^self with: Character cr! !

!String class methodsFor: 'constants'!
lf
    "Answer line feed string"
    ^self with: Character lf! !

!String class methodsFor: 'constants'!
tab
    "Answer tab string"
    ^self with: Character tab! !

"================================================================================================"
"  Number Hierarchy"
"================================================================================================"

Object subclass: #Magnitude
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Numbers'!

!Magnitude methodsFor: 'comparing'!
< aMagnitude
    "Answer whether receiver is less than aMagnitude"
    self subclassResponsibility! !

!Magnitude methodsFor: 'comparing'!
<= aMagnitude
    "Answer whether receiver is less than or equal to aMagnitude"
    ^(self > aMagnitude) not! !

!Magnitude methodsFor: 'comparing'!
> aMagnitude
    "Answer whether receiver is greater than aMagnitude"
    ^aMagnitude < self! !

!Magnitude methodsFor: 'comparing'!
>= aMagnitude
    "Answer whether receiver is greater than or equal to aMagnitude"
    ^(self < aMagnitude) not! !

!Magnitude methodsFor: 'comparing'!
= aMagnitude
    "Answer whether receiver equals aMagnitude"
    self subclassResponsibility! !

!Magnitude methodsFor: 'comparing'!
between: min and: max
    "Answer whether receiver is between min and max"
    ^self >= min and: [self <= max]! !

!Magnitude methodsFor: 'comparing'!
min: aMagnitude
    "Answer the minimum of receiver and aMagnitude"
    self < aMagnitude ifTrue: [^self] ifFalse: [^aMagnitude]! !

!Magnitude methodsFor: 'comparing'!
max: aMagnitude
    "Answer the maximum of receiver and aMagnitude"
    self > aMagnitude ifTrue: [^self] ifFalse: [^aMagnitude]! !

Magnitude subclass: #Number
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Numbers'!

!Number methodsFor: 'arithmetic'!
+ aNumber
    "Add aNumber to receiver"
    self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
- aNumber
    "Subtract aNumber from receiver"
    self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
* aNumber
    "Multiply receiver by aNumber"
    self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
/ aNumber
    "Divide receiver by aNumber"
    self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
negated
    "Answer the negation"
    ^0 - self! !

!Number methodsFor: 'arithmetic'!
abs
    "Answer the absolute value"
    self < 0 ifTrue: [^self negated].
    ^self! !

!Number methodsFor: 'testing'!
negative
    "Answer whether receiver is negative"
    ^self < 0! !

!Number methodsFor: 'testing'!
positive
    "Answer whether receiver is positive"
    ^self > 0! !

!Number methodsFor: 'testing'!
isZero
    "Answer whether receiver is zero"
    ^self = 0! !

!Number methodsFor: 'testing'!
even
    "Answer whether receiver is even"
    ^(self \\ 2) = 0! !

!Number methodsFor: 'testing'!
odd
    "Answer whether receiver is odd"
    ^(self \\ 2) = 1! !

"================================================================================================"
"  SmallInteger - Machine integers"
"================================================================================================"

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber
    "Primitive integer addition"
    <primitive: 1>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
    "Primitive integer subtraction"
    <primitive: 2>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
* aNumber
    "Primitive integer multiplication"
    <primitive: 9>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
/ aNumber
    "Primitive integer division"
    <primitive: 10>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
// aNumber
    "Integer division"
    <primitive: 12>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
\\ aNumber
    "Modulo operation"
    <primitive: 11>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
    "Primitive less than"
    <primitive: 3>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
> aNumber
    "Primitive greater than"
    <primitive: 4>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
<= aNumber
    "Primitive less than or equal"
    <primitive: 5>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
>= aNumber
    "Primitive greater than or equal"
    <primitive: 6>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
= aNumber
    "Primitive equality"
    <primitive: 7>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
~= aNumber
    "Primitive inequality"
    <primitive: 8>
    self primitiveFailed! !

!SmallInteger methodsFor: 'bit manipulation'!
bitAnd: anInteger
    "Bitwise AND"
    <primitive: 14>
    self primitiveFailed! !

!SmallInteger methodsFor: 'bit manipulation'!
bitOr: anInteger
    "Bitwise OR"
    <primitive: 15>
    self primitiveFailed! !

!SmallInteger methodsFor: 'bit manipulation'!
bitXor: anInteger
    "Bitwise XOR"
    <primitive: 16>
    self primitiveFailed! !

!SmallInteger methodsFor: 'bit manipulation'!
bitShift: anInteger
    "Shift bits by anInteger"
    <primitive: 17>
    self primitiveFailed! !

!SmallInteger methodsFor: 'enumerating'!
to: stop do: aBlock
    "Evaluate aBlock with each integer from self to stop"
    | i |
    i := self.
    [i <= stop] whileTrue: [
        aBlock value: i.
        i := i + 1]! !

!SmallInteger methodsFor: 'enumerating'!
to: stop by: step do: aBlock
    "Evaluate aBlock with integers from self to stop by step"
    | i |
    i := self.
    step > 0
        ifTrue: [[i <= stop] whileTrue: [
            aBlock value: i.
            i := i + step]]
        ifFalse: [[i >= stop] whileTrue: [
            aBlock value: i.
            i := i + step]]! !

!SmallInteger methodsFor: 'enumerating'!
timesRepeat: aBlock
    "Evaluate aBlock self times"
    1 to: self do: [:i | aBlock value]! !

"================================================================================================"
"  SystemDictionary - Global namespace"
"================================================================================================"

Dictionary subclass: #SystemDictionary
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Support'!

!SystemDictionary methodsFor: 'accessing'!
at: key put: value
    "Register a global - special handling for classes"
    super at: key put: value.
    (value isKindOf: Class) ifTrue: [
        value instVarAt: 6 put: key].  "Set class name at slot 6"
    ^value! !

!SystemDictionary methodsFor: 'class registration'!
registerClass: aClass as: aSymbol
    "Register a class with its name"
    self at: aSymbol put: aClass.
    aClass instVarAt: 6 put: aSymbol! !

!SystemDictionary methodsFor: 'printing'!
printOn: aStream
    "Print as Smalltalk"
    aStream nextPutAll: 'Smalltalk'! !

!SystemDictionary class methodsFor: 'initialization'!
initialize
    "Create and populate Smalltalk globals"
    | smalltalk |
    smalltalk := self new.
    
    "Register core classes"
    smalltalk at: #ProtoObject put: ProtoObject.
    smalltalk at: #Object put: Object.
    smalltalk at: #Behavior put: Behavior.
    smalltalk at: #ClassDescription put: ClassDescription.
    smalltalk at: #Class put: Class.
    smalltalk at: #Metaclass put: Metaclass.
    smalltalk at: #UndefinedObject put: UndefinedObject.
    smalltalk at: #Boolean put: Boolean.
    smalltalk at: #True put: True.
    smalltalk at: #False put: False.
    smalltalk at: #Symbol put: Symbol.
    smalltalk at: #Array put: Array.
    smalltalk at: #Dictionary put: Dictionary.
    smalltalk at: #SystemDictionary put: SystemDictionary.
    smalltalk at: #CompiledMethod put: CompiledMethod.
    smalltalk at: #SmallInteger put: SmallInteger.
    smalltalk at: #Message put: Message.
    smalltalk at: #BlockContext put: BlockContext.
    smalltalk at: #Collection put: Collection.
    smalltalk at: #SequenceableCollection put: SequenceableCollection.
    smalltalk at: #OrderedCollection put: OrderedCollection.
    smalltalk at: #String put: String.
    smalltalk at: #Magnitude put: Magnitude.
    smalltalk at: #Number put: Number.
    
    "Register global objects"
    smalltalk at: #Smalltalk put: smalltalk.
    smalltalk at: #nil put: nil.
    smalltalk at: #true put: true.
    smalltalk at: #false put: false.
    
    "Make it globally accessible"
    Smalltalk := smalltalk.
    
    ^smalltalk! !

"Initialize the system dictionary"
SystemDictionary initialize!

"================================================================================================"
"  Compiler Stubs - To be expanded in Phase 3"
"================================================================================================"

Object subclass: #Compiler
    instanceVariableNames: 'source class category'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'!

!Compiler methodsFor: 'initialization'!
initialize
    "Initialize compiler"
    category := 'unclassified'! !

!Compiler methodsFor: 'accessing'!
source: aString
    "Set source to compile"
    source := aString! !

!Compiler methodsFor: 'accessing'!
class: aClass
    "Set class for compilation"
    class := aClass! !

!Compiler methodsFor: 'accessing'!
category: aString
    "Set method category"
    category := aString! !

!Compiler methodsFor: 'compiling'!
compile: sourceString
    "Compile source in the context of class"
    ^self compile: sourceString in: class classified: category! !

!Compiler methodsFor: 'compiling'!
compile: sourceString in: aClass classified: aCategory
    "Compile and install method - uses primitive"
    <primitive: 117>
    self primitiveFailed! !

!Compiler class methodsFor: 'instance creation'!
new
    "Create a new compiler"
    ^super new initialize! !

!Compiler class methodsFor: 'compiling'!
compile: sourceString forClass: aClass
    "Convenience method for compilation"
    ^self new
        class: aClass;
        compile: sourceString! !

Object subclass: #Parser
    instanceVariableNames: 'tokens currentToken position'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'!

!Parser methodsFor: 'parsing'!
scan: sourceString
    "Tokenize source string"
    tokens := Scanner new scan: sourceString.
    position := 1.
    currentToken := tokens at: 1! !

!Parser methodsFor: 'parsing'!
parseMethod
    "Parse a method - stub for now"
    ^CompiledMethod new! !

Object subclass: #Scanner
    instanceVariableNames: 'source position'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'!

!Scanner methodsFor: 'scanning'!
scan: sourceString
    "Scan source into tokens - stub for now"
    source := sourceString.
    position := 1.
    ^#()! !

"================================================================================================"
"  END OF PHASE 2 BOOTSTRAP"
"================================================================================================"

"At this point we have:"
"- Boolean control structures (ifTrue:, ifFalse:, whileTrue:, etc.)"
"- Message objects for reflection"
"- Block contexts for closures"
"- Basic collections (OrderedCollection, Dictionary)"
"- String manipulation"
"- Number hierarchy with SmallInteger"
"- SystemDictionary managing globals"
"- Compiler stubs ready for Phase 3"
"- All classes registered in Smalltalk global dictionary"

"System is ready for Phase 3: Loading full standard library via in-image compiler"