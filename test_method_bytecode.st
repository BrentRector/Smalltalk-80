Object subclass: #TestObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!Object methodsFor: 'comparing'!
== anObject
	"Primitive 110 - Answer whether the receiver and the argument are the same object. 
	Do not redefine the message == in any other class! Essential to Smalltalk-80 semantics."
	<primitive: 110>
	^self primitiveFailed!

~~ anObject
	"Answer whether the receiver and the argument are NOT the same object."
	^(self == anObject) not!

= anObject
	"Answer whether the receiver and the argument represent the same object."
	^self == anObject!

~= anObject
	"Answer whether the receiver and the argument do not represent the same object."
	^(self = anObject) not!

hash
	"Answer an integer hash value for the receiver. Essential method."
	<primitive: 75>
	^self identityHash!
!

!Object methodsFor: 'accessing'!
class
	"Primitive 111 - Answer the object which is the receiver's class. Essential."
	<primitive: 111>
	^self primitiveFailed!

yourself
	"Answer self. Useful in cascaded messages."
	^self!
!

!Object methodsFor: 'testing'!
isNil
	"Answer whether the receiver is the undefined object nil."
	^false!

notNil
	"Answer whether the receiver is not the undefined object nil."
	^true!

ifNil: nilBlock
	"Since the receiver is not nil, answer nil."
	^nil!

ifNotNil: notNilBlock
	"Since the receiver is not nil, evaluate notNilBlock."
	^notNilBlock value!
!

!Object methodsFor: 'copying'!
copy
	"Answer another instance just like the receiver. Subclasses typically override shallowCopy."
	^self shallowCopy!

shallowCopy
	"Primitive 148 - Answer a copy of the receiver which shares the receiver's instance variables."
	<primitive: 148>
	^self primitiveFailed!
!

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber
	"Primitive 1 - Add the receiver to the argument and answer with the result."
	<primitive: 1>
	^self adaptToNumber: aNumber andSend: #+!

- aNumber
	"Primitive 2 - Subtract the argument from the receiver and answer the result."
	<primitive: 2>
	^self adaptToNumber: aNumber andSend: #-!

* aNumber
	"Primitive 9 - Multiply the receiver by the argument and answer the result."
	<primitive: 9>
	^self adaptToNumber: aNumber andSend: #*!

/ aNumber
	"Primitive 10 - Divide the receiver by the argument and answer the result."
	<primitive: 10>
	^self adaptToNumber: aNumber andSend: #/!

// aNumber
	"Primitive 12 - Integer division."
	<primitive: 12>
	^(self / aNumber) floor!

\\ aNumber
	"Primitive 11 - Modulo."
	<primitive: 11>
	^self - ((self // aNumber) * aNumber)!
!

!SmallInteger methodsFor: 'comparing'!
< aNumber
	"Primitive 3 - Answer whether the receiver is less than the argument."
	<primitive: 3>
	^self adaptToNumber: aNumber andSend: #<!

> aNumber
	"Primitive 4 - Answer whether the receiver is greater than the argument."
	<primitive: 4>
	^self adaptToNumber: aNumber andSend: #>!

<= aNumber
	"Primitive 5 - Answer whether the receiver is less than or equal to the argument."
	<primitive: 5>
	^(self > aNumber) not!

>= aNumber
	"Primitive 6 - Answer whether the receiver is greater than or equal to the argument."
	<primitive: 6>
	^(self < aNumber) not!

= aNumber
	"Primitive 7 - Answer whether the receiver is equal to the argument."
	<primitive: 7>
	^self adaptToNumber: aNumber andSend: #=!

~= aNumber
	"Primitive 8 - Answer whether the receiver is not equal to the argument."
	<primitive: 8>
	^(self = aNumber) not!
!

!SmallInteger methodsFor: 'converting'!
asFloat
	"Primitive 40 - Answer a Float that represents the value of the receiver."
	<primitive: 40>
	^Float fromInteger: self!
!

!Float methodsFor: 'arithmetic'!
+ aNumber
	"Primitive 41 - Add the receiver to the argument and answer with the result."
	<primitive: 41>
	^aNumber adaptToFloat: self andSend: #+!

- aNumber
	"Primitive 42 - Subtract the argument from the receiver and answer the result."
	<primitive: 42>
	^aNumber adaptToFloat: self andSend: #-!

* aNumber
	"Primitive 49 - Multiply the receiver by the argument and answer the result."
	<primitive: 49>
	^aNumber adaptToFloat: self andSend: #*!

/ aNumber
	"Primitive 50 - Divide the receiver by the argument and answer the result."
	<primitive: 50>
	^aNumber adaptToFloat: self andSend: #/!
!

!Float methodsFor: 'comparing'!
< aNumber
	"Primitive 43 - Answer whether the receiver is less than the argument."
	<primitive: 43>
	^aNumber adaptToFloat: self andSend: #<!

> aNumber
	"Primitive 44 - Answer whether the receiver is greater than the argument."
	<primitive: 44>
	^aNumber adaptToFloat: self andSend: #>!

<= aNumber
	"Primitive 45 - Answer whether the receiver is less than or equal to the argument."
	<primitive: 45>
	^(self > aNumber) not!

>= aNumber
	"Primitive 46 - Answer whether the receiver is greater than or equal to the argument."
	<primitive: 46>
	^(self < aNumber) not!

= aNumber
	"Primitive 47 - Answer whether the receiver is equal to the argument."
	<primitive: 47>
	^aNumber adaptToFloat: self andSend: #=!

~= aNumber
	"Primitive 48 - Answer whether the receiver is not equal to the argument."
	<primitive: 48>
	^(self = aNumber) not!
!

!Float methodsFor: 'truncation and round off'!
truncated
	"Primitive 51 - Answer the integer part of the receiver."
	<primitive: 51>
	^self primitiveFailed!
!