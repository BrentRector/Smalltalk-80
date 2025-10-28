Object subclass: #ConditionalBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Tests'!

!ConditionalBlockTest methodsFor: 'testing'!

testIfTrue
	"Test ifTrue: with block"
	| result |
	result := true ifTrue: [ 100 ].
	^result!

testIfFalse
	"Test ifFalse: with block"
	| result |
	result := false ifFalse: [ 200 ].
	^result!

testIfTrueIfFalse
	"Test ifTrue:ifFalse: with blocks"
	| result |
	result := true ifTrue: [ 300 ] ifFalse: [ 400 ].
	^result!

testIfFalseIfTrue
	"Test ifFalse:ifTrue: with blocks"
	| result |
	result := false ifFalse: [ 500 ] ifTrue: [ 600 ].
	^result!

runAll
	"Run all tests and return results"
	| results |
	results := Array new: 4.
	results at: 1 put: self testIfTrue.
	results at: 2 put: self testIfFalse.
	results at: 3 put: self testIfTrueIfFalse.
	results at: 4 put: self testIfFalseIfTrue.
	^results! !

!ConditionalBlockTest class methodsFor: 'running'!

run
	"Run all conditional block tests"
	^self new runAll! !!

"Run ConditionalBlockTest suite"!
ConditionalBlockTest run!
