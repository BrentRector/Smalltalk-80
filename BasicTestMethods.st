"Basic test methods using the Smalltalk-80 testing framework"

TestCase subclass: #BasicFunctionalityTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-BasicFunctionality'!

!BasicFunctionalityTests methodsFor: 'testing-arithmetic'!
testBasicArithmetic
	"Test basic arithmetic operations"
	self assertEqual: 5 to: 2 + 3.
	self assertEqual: 6 to: 10 - 4.
	self assertEqual: 12 to: 3 * 4!

testArithmeticComplex
	"Test more complex arithmetic operations"
	| result |
	result _ 2 + 3.
	self assertEqual: 5 to: result.
	
	result _ 10 - 4.
	self assertEqual: 6 to: result.
	
	result _ 3 * 4.
	self assertEqual: 12 to: result! !

!BasicFunctionalityTests methodsFor: 'testing-booleans'!
testBooleanLogic
	"Test boolean operations"
	self assertTrue: true.
	self assertFalse: false.
	self assertEqual: false to: true not!

testBooleanComplex
	"Test complex boolean operations"
	self assertTrue: (true and: [ true ]).
	self assertTrue: (false or: [ true ]).
	self assertFalse: (true and: [ false ]).
	self assertFalse: (false or: [ false ])! !

!BasicFunctionalityTests methodsFor: 'testing-collections'!
testArrayOperations
	"Test basic array operations"
	| array |
	array _ Array new: 3.
	self assertEqual: 3 to: array size.
	
	array at: 1 put: 'first'.
	self assertEqual: 'first' to: (array at: 1)!

testCollectionSize
	"Test collection size operations"
	self assertEqual: 3 to: (Array new: 3) size.
	self assertEqual: 5 to: 'hello' size! !

!BasicFunctionalityTests methodsFor: 'testing-strings'!
testStringOperations
	"Test string operations"
	| string |
	string _ 'hello'.
	self assertEqual: 5 to: string size.
	self assertEqual: $h to: (string at: 1)!

testCharacterOperations
	"Test character operations"
	self assertEqual: 65 to: $A asciiValue! !

!BasicFunctionalityTests methodsFor: 'running'!
performTest
	"Run all the basic functionality tests"
	self testBasicArithmetic.
	self testArithmeticComplex.
	self testBooleanLogic.
	self testBooleanComplex.
	self testArrayOperations.
	self testCollectionSize.
	self testStringOperations.
	self testCharacterOperations! !
