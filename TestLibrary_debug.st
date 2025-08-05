"TestLibrary.st - Phase 2 Test Framework - Core Classes Only"

Object subclass: #TestCase
	instanceVariableNames: 'testSelector failureMessage'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'assertions'!
assert: condition
	"Assert that condition is true"
	condition ifFalse: [ self fail: 'Assertion failed' ]!

assertEqual: expected to: actual
	"Assert that expected equals actual"
	expected = actual ifFalse: [ 
		self fail: 'Expected value does not match actual value'
	]! !

!TestCase methodsFor: 'failure handling'!
fail: message
	"Signal a test failure with the given message"
	failureMessage _ message.
	self error: 'Test failed: ', message! !

Object subclass: #TestResult
	instanceVariableNames: 'successes failures errors'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestResult methodsFor: 'initialization'!
initialize
	"Initialize a new test result"
	"Use the enhanced Array class methods"
	successes _ Array new: 0.
	failures _ Array new: 0.
	errors _ Array new: 0.
	^self! !

Object subclass: #TestSuite
	instanceVariableNames: 'testCases name'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestSuite methodsFor: 'initialization'!
initialize
	"Initialize a new test suite"
	testCases _ Array new: 0.
	name _ 'Unnamed Test Suite'.
	^self! !
