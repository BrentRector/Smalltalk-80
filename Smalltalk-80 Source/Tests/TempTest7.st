"TempTest7 - Nested blocks - verify correct value returned"

Object subclass: #TempTest7
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest7 methodsFor: 'testing'!

test
	| outer |
	outer _ 42.
	^[:x | [:y | outer] value: 2] value: 1! !

!TempTest7 class methodsFor: 'running'!

run
	| result |
	result _ self new test.
	^result = 42 ifTrue: [99] ifFalse: [result]! !!

TempTest7 run!
