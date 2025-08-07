"Stage2Bootstrap.st - Expanding the system using Smalltalk itself"
"This file is loaded AFTER the minimal bootstrap, using Smalltalk's own mechanisms"

"============================================"
"Enable class creation via Smalltalk messages"
"============================================"

!Behavior methodsFor: 'class creation'!
subclass: className instanceVariableNames: instVarString classVariableNames: classVarString poolDictionaries: poolString category: categoryName
    "Create a subclass using Smalltalk messages instead of VM primitives"
    | newClass newMetaclass |
    
    "Create the class"
    newClass := Class new.
    newClass instVarAt: 1 put: self.  "superclass"
    newClass instVarAt: 6 put: className.  "name"
    newClass instVarAt: 2 put: MethodDictionary new.  "methodDict"
    
    "Parse instance variables"
    instVarString isEmpty ifFalse: [
        newClass instVarAt: 4 put: (instVarString subStrings: ' ')].
    
    "Create class variable dictionary at slot 7 (CRITICAL!)"
    classVarString isEmpty ifFalse: [
        | classPool |
        classPool := Dictionary new.
        (classVarString subStrings: ' ') do: [:each |
            classPool at: each asSymbol put: nil].
        newClass instVarAt: 7 put: classPool].
    
    "Metaclass is automatically created by VM"
    
    "Register in Smalltalk globals"
    Smalltalk at: className asSymbol put: newClass.
    
    ^newClass! !

"============================================"
"Message objects for reflection"
"============================================"

Object subclass: #Message
    instanceVariableNames: 'selector arguments'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!

!Message methodsFor: 'accessing'!
selector
    ^selector! !

!Message methodsFor: 'accessing'!
selector: aSymbol
    selector := aSymbol! !

!Message methodsFor: 'accessing'!
arguments
    ^arguments! !

!Message methodsFor: 'accessing'!
arguments: anArray
    arguments := anArray! !

!Message methodsFor: 'sending'!
sendTo: receiver
    "Send this message to the receiver"
    ^receiver perform: selector withArguments: arguments! !

!Message class methodsFor: 'instance creation'!
selector: aSymbol arguments: argArray
    ^self new
        selector: aSymbol;
        arguments: argArray;
        yourself! !

"Update ProtoObject's doesNotUnderstand: to create proper Message"
!ProtoObject methodsFor: 'error handling'!
doesNotUnderstand: aMessage
    "Improved error handling with Message object"
    | errorString |
    errorString := 'Message not understood: ', aMessage selector.
    self error: errorString! !

"============================================"
"Extended Boolean protocol"
"============================================"

!Boolean methodsFor: 'logical operations'!
and: aBlock
    "Evaluating conjunction"
    self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
or: aBlock
    "Evaluating disjunction"
    self subclassResponsibility! !

!True methodsFor: 'logical operations'!
and: aBlock
    "Evaluate aBlock since receiver is true"
    ^aBlock value! !

!True methodsFor: 'logical operations'!
or: aBlock
    "Answer true without evaluating aBlock"
    ^true! !

!False methodsFor: 'logical operations'!
and: aBlock
    "Answer false without evaluating aBlock"
    ^false! !

!False methodsFor: 'logical operations'!
or: aBlock
    "Evaluate aBlock since receiver is false"
    ^aBlock value! !

"============================================"
"Block contexts and control structures"
"============================================"

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
repeat
    "Evaluate receiver repeatedly"
    self value.
    self repeat! !

"============================================"
"Collections expansion"
"============================================"

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

Collection subclass: #SequenceableCollection
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Abstract'!

!SequenceableCollection methodsFor: 'accessing'!
first
    "Answer first element"
    ^self at: 1! !

!SequenceableCollection methodsFor: 'accessing'!
last
    "Answer last element"
    ^self at: self size! !

!SequenceableCollection methodsFor: 'enumerating'!
do: aBlock
    "Evaluate aBlock with each element in sequence"
    1 to: self size do: [:index |
        aBlock value: (self at: index)]! !

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

"============================================"
"String enhancements"
"============================================"

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

!String methodsFor: 'printing'!
printOn: aStream
    "Print the string"
    aStream nextPut: $'.
    self do: [:char | aStream nextPut: char].
    aStream nextPut: $'! !

"============================================"
"Number hierarchy expansion"
"============================================"

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
between: min and: max
    "Answer whether receiver is between min and max"
    ^self >= min and: [self <= max]! !

Magnitude subclass: #Number
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Numbers'!

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

"SmallInteger gets more methods"
!SmallInteger methodsFor: 'arithmetic'!
* aNumber
    "Multiply by aNumber"
    <primitive: 9>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
/ aNumber
    "Divide by aNumber"
    <primitive: 10>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
// aNumber
    "Integer division"
    <primitive: 12>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
\\ aNumber
    "Modulo"
    <primitive: 11>
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
bitShift: anInteger
    "Shift bits"
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

"============================================"
"System Dictionary setup"
"============================================"

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
        value instVarAt: 6 put: key].  "Set class name"
    ^value! !

"Create and populate the global Smalltalk dictionary"
!SystemDictionary class methodsFor: 'initialization'!
initialize
    "Create and populate Smalltalk globals"
    | smalltalk |
    smalltalk := self new.
    
    "Register all classes"
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
    smalltalk at: #MethodDictionary put: MethodDictionary.
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

"============================================"
"Basic compiler stubs (to be expanded in Stage 3)"
"============================================"

Object subclass: #Compiler
    instanceVariableNames: 'source class category'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'!

!Compiler methodsFor: 'compiling'!
compile: sourceString
    "Compile source in the context of class"
    ^self compile: sourceString in: class classified: category! !

!Compiler methodsFor: 'compiling'!
compile: sourceString in: aClass classified: aCategory
    "Compile and install method - currently uses primitive"
    <primitive: 117>
    self primitiveFailed! !

!Compiler class methodsFor: 'instance creation'!
new
    "Create a new compiler"
    ^super new initialize! !

"============================================"
"END OF STAGE 2 BOOTSTRAP"
"============================================"