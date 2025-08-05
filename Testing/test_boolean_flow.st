Object subclass: #BooleanTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!BooleanTest methodsFor: 'testing'!
testIfTrueIfFalse
	| result arg0 |
	arg0 := true.
	result := arg0 ifTrue: [42] ifFalse: [99].
	^result! !
