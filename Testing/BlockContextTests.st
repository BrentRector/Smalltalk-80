"Comprehensive tests for BlockContext class"

TestCase subclass: #BlockContextTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-BlockContext'!

!BlockContextTests methodsFor: 'testing-evaluating'!
testValue
	"Test BlockContext>>value"
	| result block |
	block _ [ 42 ].
	result _ block value.
	self assertEqual: 42 to: result!

testValueWithOneArgument
	"Test BlockContext>>value:"
	| result block |
	block _ [:x | x * 2].
	result _ block value: 5.
	self assertEqual: 10 to: result!

testValueWithTwoArguments
	"Test BlockContext>>value:value:"
	| result block |
	block _ [:x :y | x + y].
	result _ block value: 3 value: 7.
	self assertEqual: 10 to: result! !

!BlockContextTests methodsFor: 'testing-control-structures'!
testWhileTrue
	"Test BlockContext>>whileTrue:"
	| counter result |
	counter _ 0.
	result _ Array new: 5.
	[ counter < 5 ] whileTrue: [
		counter _ counter + 1.
		result at: counter put: counter
	].
	self assertEqual: 5 to: counter.
	self assertEqual: 1 to: (result at: 1).
	self assertEqual: 5 to: (result at: 5)!

testWhileFalse
	"Test BlockContext>>whileFalse:"
	| counter |
	counter _ 0.
	[ counter >= 3 ] whileFalse: [
		counter _ counter + 1
	].
	self assertEqual: 3 to: counter!

testWhileTrueNoBlock
	"Test BlockContext>>whileTrue"
	| counter |
	counter _ 0.
	[ counter < 3 ] whileTrue: [
		counter _ counter + 1.
		counter >= 3
	] whileTrue.
	self assertEqual: 3 to: counter!

testWhileFalseNoBlock
	"Test BlockContext>>whileFalse"
	| counter |
	counter _ 10.
	[ counter <= 7 ] whileFalse: [
		counter _ counter - 1.
		counter <= 7
	] whileFalse.
	self assertEqual: 7 to: counter! !

!BlockContextTests methodsFor: 'testing-nested-blocks'!
testNestedBlocks
	"Test nested block evaluation"
	| result |
	result _ [ [ 5 ] value + [ 3 ] value ] value.
	self assertEqual: 8 to: result!

testBlockWithLocalVariables
	"Test blocks with local variables"
	| result |
	result _ [ | x y | x _ 10. y _ 20. x + y ] value.
	self assertEqual: 30 to: result! !

!BlockContextTests methodsFor: 'testing-closures'!
testClosure
	"Test block closures capturing outer variables"
	| outerVar block result |
	outerVar _ 100.
	block _ [ outerVar + 50 ].
	result _ block value.
	self assertEqual: 150 to: result!

testClosureWithModification
	"Test closure that modifies outer variable"
	| outerVar block |
	outerVar _ 10.
	block _ [ outerVar _ outerVar + 5 ].
	block value.
	self assertEqual: 15 to: outerVar! !

!BlockContextTests methodsFor: 'testing-conditional-evaluation'!
testConditionalBlocks
	"Test blocks used in conditional contexts"
	| result |
	result _ true ifTrue: [ 'success' ] ifFalse: [ 'failure' ].
	self assertEqual: 'success' to: result.
	
	result _ false ifTrue: [ 'success' ] ifFalse: [ 'failure' ].
	self assertEqual: 'failure' to: result! !

!BlockContextTests methodsFor: 'running'!
performTest
	"Run all the block context tests"
	self testValue.
	self testValueWithOneArgument.
	self testValueWithTwoArguments.
	self testWhileTrue.
	self testWhileFalse.
	self testWhileTrueNoBlock.
	self testWhileFalseNoBlock.
	self testNestedBlocks.
	self testBlockWithLocalVariables.
	self testClosure.
	self testClosureWithModification.
	self testConditionalBlocks! !
