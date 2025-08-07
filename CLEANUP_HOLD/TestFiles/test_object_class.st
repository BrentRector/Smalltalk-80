Object subclass: #TestObjectInit
	instanceVariableNames: ''
	classVariableNames: 'TestClassVar '
	poolDictionaries: ''
	category: 'Test'!

!TestObjectInit class methodsFor: 'testing'!
testInitialize
	"This mimics what Object initialize does"
	self testConfirm
		ifTrue: [self doInit]!

testConfirm
	"Simulate confirm: without UI - just return true"
	^true!
	
doInit
	"Initialize the class variable"
	TestClassVar _ 'Initialized successfully'.
	^TestClassVar!
	
getTestVar
	^TestClassVar! !

"Test the initialization"
TestObjectInit testInitialize.
TestObjectInit getTestVar printNl!