"TempTest2 - Full shadowing test with addition"

Object subclass: #TempTest2
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TempTest2 methodsFor: 'testing'!

test
	| temp |
	temp _ 10.
	^[| temp | temp _ 15. temp] value + temp! !

!TempTest2 class methodsFor: 'running'!

run
	^self new test! !!

TempTest2 run!
