"TempTest5 - Exact reproduction of bl2BlockInArray issue"

Object subclass: #TempTest5
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest5 methodsFor: 'testing'!

test
	| blocks |
	blocks _ Array new: 3.
	1 to: 3 do: [:i | blocks at: i put: i].
	^blocks at: 1! !

!TempTest5 class methodsFor: 'running'!

run
	^self new test! !!

TempTest5 run!
