Object subclass: #SimpleBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Tests'!

!SimpleBlockTest methodsFor: 'testing'!

testZeroArgs
	"Test 0-arg block"
	| result |
	result := [ 42 ] value.
	^result!

testOneArg
	"Test 1-arg block"
	| result |
	result := [ :x | x + 10 ] value: 5.
	^result!

testTwoArgs
	"Test 2-arg block"
	| result |
	result := [ :x :y | x * y ] value: 3 value: 4.
	^result!

testNested
	"Test nested blocks"
	| result |
	result := [ [ 99 ] value ] value.
	^result!

testMutation
	"Test captured variable mutation"
	| x block |
	x := 0.
	block := [ x := x + 1 ].
	block value.
	block value.
	^x!

runAll
	"Run all tests and return results"
	| results |
	results := Array new: 5.
	results at: 1 put: self testZeroArgs.
	results at: 2 put: self testOneArg.
	results at: 3 put: self testTwoArgs.
	results at: 4 put: self testNested.
	results at: 5 put: self testMutation.
	^results! !

!SimpleBlockTest class methodsFor: 'running'!

run
	"Run all simple block tests"
	^self new runAll! !!

"Run SimpleBlockTest suite"!
SimpleBlockTest run!
