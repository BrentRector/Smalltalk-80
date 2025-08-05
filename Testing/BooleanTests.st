"Comprehensive tests for Boolean, True, and False classes"

TestCase subclass: #BooleanTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Boolean'!

!BooleanTests methodsFor: 'testing-logical-operations'!
testAnd
	"Test Boolean>>& (evaluating and)"
	self assertTrue: true & true.
	self assertFalse: true & false.
	self assertFalse: false & true.
	self assertFalse: false & false!

testOr
	"Test Boolean>>| (evaluating or)"
	self assertTrue: true | true.
	self assertTrue: true | false.
	self assertTrue: false | true.
	self assertFalse: false | false!

testNot
	"Test Boolean>>not"
	self assertFalse: true not.
	self assertTrue: false not!

testAndBlock
	"Test Boolean>>and: (non-evaluating and)"
	self assertTrue: (true and: [ true ]).
	self assertFalse: (true and: [ false ]).
	self assertFalse: (false and: [ true ]).
	self assertFalse: (false and: [ false ])!

testOrBlock
	"Test Boolean>>or: (non-evaluating or)"
	self assertTrue: (true or: [ true ]).
	self assertTrue: (true or: [ false ]).
	self assertTrue: (false or: [ true ]).
	self assertFalse: (false or: [ false ])!

testEqv
	"Test Boolean>>eqv: (equivalence)"
	self assertTrue: (true eqv: true).
	self assertTrue: (false eqv: false).
	self assertFalse: (true eqv: false).
	self assertFalse: (false eqv: true)!

testXor
	"Test Boolean>>xor: (exclusive or)"
	self assertFalse: (true xor: true).
	self assertFalse: (false xor: false).
	self assertTrue: (true xor: false).
	self assertTrue: (false xor: true)! !

!BooleanTests methodsFor: 'testing-controlling'!
testIfTrue
	"Test Boolean>>ifTrue:"
	| result |
	result _ true ifTrue: [ 'success' ].
	self assertEqual: 'success' to: result.
	
	result _ false ifTrue: [ 'success' ].
	self assertEqual: nil to: result!

testIfFalse
	"Test Boolean>>ifFalse:"
	| result |
	result _ false ifFalse: [ 'success' ].
	self assertEqual: 'success' to: result.
	
	result _ true ifFalse: [ 'success' ].
	self assertEqual: nil to: result!

testIfTrueIfFalse
	"Test Boolean>>ifTrue:ifFalse:"
	| result |
	result _ true ifTrue: [ 'true branch' ] ifFalse: [ 'false branch' ].
	self assertEqual: 'true branch' to: result.
	
	result _ false ifTrue: [ 'true branch' ] ifFalse: [ 'false branch' ].
	self assertEqual: 'false branch' to: result!

testIfFalseIfTrue
	"Test Boolean>>ifFalse:ifTrue:"
	| result |
	result _ true ifFalse: [ 'false branch' ] ifTrue: [ 'true branch' ].
	self assertEqual: 'true branch' to: result.
	
	result _ false ifFalse: [ 'false branch' ] ifTrue: [ 'true branch' ].
	self assertEqual: 'false branch' to: result! !

!BooleanTests methodsFor: 'testing-copying'!
testCopying
	"Test that True and False return themselves when copied"
	self assertEqual: true to: true copy.
	self assertEqual: false to: false copy.
	self assertEqual: true to: true shallowCopy.
	self assertEqual: false to: false shallowCopy.
	self assertEqual: true to: true deepCopy.
	self assertEqual: false to: false deepCopy! !

!BooleanTests methodsFor: 'testing-true-specific'!
testTrueLogicalOperations
	"Test True-specific logical operations"
	self assertEqual: 42 to: (true & 42).
	self assertEqual: true to: (true | false).
	self assertEqual: true to: (true or: [ false ]).
	self assertEqual: 42 to: (true and: [ 42 ])! !

!BooleanTests methodsFor: 'testing-false-specific'!
testFalseLogicalOperations
	"Test False-specific logical operations"
	self assertEqual: false to: (false & 42).
	self assertEqual: 42 to: (false | 42).
	self assertEqual: false to: (false and: [ 42 ]).
	self assertEqual: 42 to: (false or: [ 42 ])! !

!BooleanTests methodsFor: 'testing-printing'!
testPrinting
	"Test Boolean printing"
	self assertEqual: 'true' to: true printString.
	self assertEqual: 'false' to: false printString! !

!BooleanTests methodsFor: 'running'!
performTest
	"Run all the boolean tests"
	self resetTestCounts.
	self runIndividualTest: #testAnd.
	self runIndividualTest: #testOr.
	self runIndividualTest: #testNot.
	self runIndividualTest: #testAndBlock.
	self runIndividualTest: #testOrBlock.
	self runIndividualTest: #testEqv.
	self runIndividualTest: #testXor.
	self runIndividualTest: #testIfTrue.
	self runIndividualTest: #testIfFalse.
	self runIndividualTest: #testIfTrueIfFalse.
	self runIndividualTest: #testIfFalseIfTrue.
	self runIndividualTest: #testCopying.
	self runIndividualTest: #testTrueLogicalOperations.
	self runIndividualTest: #testFalseLogicalOperations.
	self runIndividualTest: #testPrinting.
	self printTestResults! !
