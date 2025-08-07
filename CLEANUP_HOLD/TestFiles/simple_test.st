Object subclass: #SimpleTest
	instanceVariableNames: ''
	classVariableNames: 'WasExecuted '
	poolDictionaries: ''
	category: 'Test'!

!SimpleTest class methodsFor: 'testing'!
markExecuted
	WasExecuted _ true.
	^'MARKED AS EXECUTED'!

wasExecuted
	^WasExecuted! !

"This standalone statement should execute"
SimpleTest markExecuted printNl!