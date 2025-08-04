nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'accessing'!
yourself
	"Answer the receiver."
	^self! !

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Character
	instanceVariableNames: 'value '
	classVariableNames: 'CharacterTable '
	poolDictionaries: ''
	category: 'Collections-Text'!

!Character methodsFor: 'testing'!
isDigit
	"Answer whether the receiver is a digit."
	^self >= $0 and: [self <= $9]! !
