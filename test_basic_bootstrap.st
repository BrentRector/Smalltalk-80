nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'accessing'!
yourself
	"Answer the receiver."
	^self! !

!Object methodsFor: 'comparing'!
= anObject 
	"Answer whether the receiver and the argument represent the same object."
	^self == anObject!

hash
	"Answer a SmallInteger whose value is related to the receiver's identity."
	<primitive: 75>
	self primitiveFailed!

~= anObject 
	"Answer whether the receiver and the argument do not represent the same object."
	^self = anObject == false! !

!Object methodsFor: 'testing'!
isNil
	"Answer true if the receiver is nil."
	^false! !

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Boolean methodsFor: 'controlling'!
ifTrue: alternativeBlock 
	"If the receiver is true, answer the value of alternativeBlock. Otherwise, answer nil."
	self subclassResponsibility! !
