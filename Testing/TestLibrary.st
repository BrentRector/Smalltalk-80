"TestLibrary.st - Phase 1 Test Framework"

Object subclass: #TestCase
	instanceVariableNames: 'testSelector failureMessage'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'initialization'!
initialize
	"Initialize a new test case"
	testSelector _ nil.
	failureMessage _ nil.
	^self! !

!TestCase methodsFor: 'assertions'!
assert: condition
	"Assert that condition is true"
	condition ifFalse: [ self fail: 'Assertion failed' ]!

assertEqual: expected to: actual
	"Assert that expected equals actual"
	expected = actual ifFalse: [ 
		self fail: 'Expected value does not match actual value'
	]!

assertTrue: condition
	"Assert that condition is true"
	self assert: condition!

assertFalse: condition
	"Assert that condition is false"
	condition ifTrue: [ self fail: 'Expected false but got true' ]!

assertNotNil: object
	"Assert that object is not nil"
	object isNil ifTrue: [ self fail: 'Expected non-nil but got nil' ]! !

!TestCase methodsFor: 'failure handling'!
fail: message
	"Signal a test failure with the given message"
	failureMessage _ message.
	self error: 'Test failed: ', message! !

!TestCase methodsFor: 'running'!
runTest
	"Run this test case - simple version"
	self setUp.
	self performTest.
	self tearDown.
	^'Test completed'!

performTest
	"Perform the actual test - subclasses should override"
	self error: 'Subclass must implement performTest'!

setUp
	"Set up for testing - subclasses may override"
	^self!

tearDown
	"Clean up after testing - subclasses may override"
	^self! !

TestCase subclass: #BasicSmalltalkTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Examples'!

!BasicSmalltalkTests methodsFor: 'testing'!
testBasicArithmetic
	"Test basic arithmetic operations"
	self assertEqual: 7 to: 3 + 4.
	self assertEqual: 12 to: 3 * 4.
	self assertEqual: 1 to: 5 - 4!

performTest
	"Run the arithmetic test"
	self testBasicArithmetic! !

Object subclass: #TestResult
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing-Framework'!

!TestResult methodsFor: 'initialization'!
initialize
"Initialize test result tracking"
^self! !

Object subclass: #TestFailure
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing-Framework'!

!TestFailure methodsFor: 'initialization'!
initialize
"Initialize test failure"
^self! !

Object subclass: #TestSuite
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing-Framework'!

!TestSuite methodsFor: 'initialization'!
initialize
"Initialize test suite"
^self! !

Object subclass: #TestRunner
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing-Framework'!

!TestRunner methodsFor: 'initialization'!
initialize
"Initialize test runner"
^self! !

TestCase subclass: #SampleBasicTest
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing-Examples'!

!SampleBasicTest methodsFor: 'testing'!
testBasicArithmetic
"Test basic arithmetic operations"
self assertEqual: 7 to: 3 + 4.
self assertEqual: 12 to: 3 * 4.
self assertEqual: 1 to: 5 - 4!

performTest
"Run all basic tests"
self testBasicArithmetic! !
