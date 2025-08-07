"Test class with its own methods for validation"

Object subclass: #TestClass
	instanceVariableNames: 'value result'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

!TestClass methodsFor: 'testing'!
runBasicTests
	"Run fundamental tests to verify system functionality"
	| success failure |
	success := 0.
	failure := 0.
	
	"Test 1: Basic arithmetic"
	(2 + 3) = 5 ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 2: Boolean logic"
	true not = false ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 3: Instance variable assignment"
	self setValue: 42.
	self getValue = 42 ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Report results"
	^Array with: success with: failure!

testArithmetic
	"Test basic arithmetic operations"
	| result |
	result := 2 + 3.
	result = 5 ifFalse: [ self error: 'Addition test failed' ].
	
	result := 10 - 4.
	result = 6 ifFalse: [ self error: 'Subtraction test failed' ].
	
	result := 3 * 4.
	result = 12 ifFalse: [ self error: 'Multiplication test failed' ].
	
	^'Arithmetic tests passed'!

testBooleans
	"Test boolean operations"
	true ifFalse: [ self error: 'true should be true' ].
	false ifTrue: [ self error: 'false should be false' ].
	
	^'Boolean tests passed'!

setValue: anInteger
	"Set the test value"
	value := anInteger!

getValue
	"Get the test value"
	^value!

simpleTest
	"A simple test method"
	^'test passed'! !
