nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'accessing'!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isNil
	"Coerces nil to true and everything else to false."! !

!Object methodsFor: 'testing'!
notNil
	"Coerces nil to false and everything else to true."! !

!Object methodsFor: 'comparing'!

= anObject
	"Answer whether the receiver and anObject are the same object (have the same object pointer)."
	<primitive: 110>
	^self == anObject!
~= anObject
	"Answer whether the receiver and anObject are not the same object."
	^(self = anObject) == false!
== anObject
	"Answer whether the receiver and anObject are the same object (have the same object pointer)."
	<primitive: 110>
	^self == anObject!
~~ anObject
	"Answer whether the receiver and anObject are not the same object."
	^(self == anObject) == false!
hash
	"Answer a SmallInteger whose value is related to the receiver's identity."
	<primitive: 75>
	^self basicAt: 2! !

!Object methodsFor: 'class membership'!

class
	"Answer the receiver's class."
	<primitive: 111>
	^self basicAt: 1!
isMemberOf: aClass
	"Answer whether the receiver is an instance of the class, aClass."
	^self class == aClass! !

!Boolean methodsFor: 'logical operations'!

& aBoolean
	"Answer true if both the receiver and aBoolean are true."
	<primitive: 111>
	^self and: [aBoolean]!
| aBoolean  
	"Answer true if either the receiver or aBoolean is true."
	<primitive: 112>
	^self or: [aBoolean]!
not
	"Answer the logical negation of the receiver."
	self subclassResponsibility!
and: alternativeBlock
	"Answer true if both the receiver and the result of evaluating alternativeBlock are true."
	self subclassResponsibility!
or: alternativeBlock
	"Answer true if either the receiver or the result of evaluating alternativeBlock is true."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!

ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise answer nil."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock if the receiver is false, otherwise answer nil."
	self subclassResponsibility!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise the result of evaluating falseAlternativeBlock."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise the result of evaluating falseAlternativeBlock."
	self subclassResponsibility! !

!True methodsFor: 'logical operations'!

not
	"Answer false."
	^false!
and: alternativeBlock
	"Answer the result of evaluating alternativeBlock."
	^alternativeBlock value!
or: alternativeBlock
	"Answer true without evaluating alternativeBlock."
	^true! !

!True methodsFor: 'controlling'!

ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock."
	^trueAlternativeBlock value!
ifFalse: falseAlternativeBlock
	"Answer nil without evaluating falseAlternativeBlock."
	^nil!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock."
	^trueAlternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock."
	^trueAlternativeBlock value! !

!False methodsFor: 'logical operations'!

not
	"Answer true."
	^true!
and: alternativeBlock
	"Answer false without evaluating alternativeBlock."
	^false!
or: alternativeBlock
	"Answer the result of evaluating alternativeBlock."
	^alternativeBlock value! !

!False methodsFor: 'controlling'!

ifTrue: trueAlternativeBlock
	"Answer nil without evaluating trueAlternativeBlock."
	^nil!
ifFalse: falseAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock."
	^falseAlternativeBlock value!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock."
	^falseAlternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock."
	^falseAlternativeBlock value! !

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

!SmallInteger methodsFor: 'arithmetic'!

+ aNumber
    <primitive: 1>
    ^super + aNumber!
- aNumber 
	<primitive: 2>
	^super - aNumber!
* aNumber 
	<primitive: 9>
	^super * aNumber!
/ aNumber 
	<primitive: 10>
	^super / aNumber!
// aNumber 
	<primitive: 12>
	^super // aNumber!
\\ aNumber 
	<primitive: 11>
	^super \\ aNumber!
< aNumber 
	<primitive: 3>
	^super < aNumber!
<= aNumber 
	<primitive: 5>
	^super <= aNumber!
> aNumber 
	<primitive: 4>
	^super > aNumber!
>= aNumber 
	<primitive: 6>
	^super >= aNumber!
= aNumber 
	<primitive: 7>
	^super = aNumber!
~= aNumber 
	<primitive: 8>
	^super ~= aNumber! !

!SmallInteger methodsFor: 'bit manipulation'!

bitAnd: aNumber
	<primitive: 14>
	^super bitAnd: aNumber!
bitOr: aNumber
	<primitive: 15>
	^super bitOr: aNumber!
bitXor: aNumber
	<primitive: 16>
	^super bitXor: aNumber!
bitShift: aNumber
	<primitive: 17>
	^super bitShift: aNumber! !

!SmallInteger methodsFor: 'testing'!

even
	^(self bitAnd: 1) = 0!
odd
	^(self bitAnd: 1) = 1!
positive
	^self > 0!
negative  
	^self < 0!
strictlyPositive
	^self > 0!
sign
	self > 0 ifTrue: [^1].
	self < 0 ifTrue: [^-1].
	^0! !

Integer variableByteSubclass: #LargePositiveInteger
	instanceVariableNames: ''
	classVariableNames: 'MaxHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer variableByteSubclass: #LargeNegativeInteger
	instanceVariableNames: ''
	classVariableNames: 'MinHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Character
	instanceVariableNames: 'value'
	classVariableNames: 'CharacterTable'
	poolDictionaries: ''
	category: 'Collections-Text'!

Object subclass: #Collection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

Collection variableSubclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

Collection variableByteSubclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

!Collection methodsFor: 'accessing'!

size
	"Answer how many elements the receiver contains."
	<primitive: 62>
	^self basicSize!
isEmpty
	"Answer whether the receiver contains any elements."
	^self size = 0!
notEmpty
	"Answer whether the receiver contains any elements."
	^self isEmpty not! !

!Array methodsFor: 'accessing'!

at: index
	"Answer the element at index."
	<primitive: 60>
	^self basicAt: index!
at: index put: anObject
	"Store anObject at index. Answer anObject."
	<primitive: 61>
	^self basicAt: index put: anObject!
size
	"Answer the number of elements in the receiver."
	<primitive: 62>
	^self basicSize! !

!String methodsFor: 'accessing'!

size
	"Answer the number of characters in the receiver."
	<primitive: 62>
	^self basicSize! !
