'Testing BlockClosure with arguments'!

Object subclass: #BlockClosureTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!BlockClosureTest methodsFor: 'testing'!

testOneArgument
	"Test block with one argument"
	| block result |
	block := [ :x | x + 10 ].
	result := block value: 5.
	Transcript show: 'One argument test: 5 + 10 = '; show: result printString; cr.
	^result!

testTwoArguments
	"Test block with two arguments"
	| block result |
	block := [ :x :y | x * y ].
	result := block value: 3 value: 4.
	Transcript show: 'Two arguments test: 3 * 4 = '; show: result printString; cr.
	^result!

testNestedBlocks
	"Test nested blocks with lexical scope"
	| outer inner result |
	outer := 10.
	result := [
		inner := 20.
		[ outer + inner ] value
	] value.
	Transcript show: 'Nested blocks: 10 + 20 = '; show: result printString; cr.
	^result!

testMutation
	"Test that blocks can mutate captured variables"
	| x block |
	x := 0.
	block := [ x := x + 1 ].
	block value.
	block value.
	Transcript show: 'Mutation test: x after 2 increments = '; show: x printString; cr.
	^x!

runAll
	"Run all tests"
	Transcript cr; show: '=== BlockClosure Tests ==='; cr; cr.
	self testOneArgument.
	self testTwoArguments.
	self testNestedBlocks.
	self testMutation.
	Transcript cr; show: '=== Tests Complete ==='; cr! !

!BlockClosureTest class methodsFor: 'running'!

run
	"Run all block closure tests"
	^self new runAll! !!
