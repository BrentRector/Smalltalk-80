Object subclass: #TestClass
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!TestClass methodsFor: 'testing'!
testCascade
	| stream |
	stream nextPut: $a; nextPut: $b.
	^stream! !
