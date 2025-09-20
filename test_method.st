Object subclass: #TestClass
	instanceVariableNames: ''
	classVariableNames: 'TestVar'
	poolDictionaries: ''
	category: 'Test'!

!TestClass class methodsFor: 'testing'!
testMethod
	"Test method with temporaries"
	| temp1 temp2 |
	temp1 _ 10.
	temp2 _ 20.
	TestVar _ temp1 + temp2.
	^self! !