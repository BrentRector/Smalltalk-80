"TempTest4 - Block with arg accessing outer temp"

Object subclass: #TempTest4
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest4 methodsFor: 'testing'!

test
	| outer |
	outer _ 42.
	^[:x | outer] value: 99! !

!TempTest4 class methodsFor: 'running'!

run
	^self new test! !!

TempTest4 run!
