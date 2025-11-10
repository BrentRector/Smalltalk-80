"TempTest6 - Nested blocks accessing outer temp"

Object subclass: #TempTest6
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest6 methodsFor: 'testing'!

test
	| outer |
	outer _ 42.
	^[:x | [:y | outer] value: 2] value: 1! !

!TempTest6 class methodsFor: 'running'!

run
	^self new test! !!

TempTest6 run!
