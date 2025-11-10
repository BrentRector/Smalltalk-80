"TempTest3 - Simple block accessing outer temp"

Object subclass: #TempTest3
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest3 methodsFor: 'testing'!

test
	| outer |
	outer _ 42.
	^[outer] value! !

!TempTest3 class methodsFor: 'running'!

run
	^self new test! !!

TempTest3 run!
