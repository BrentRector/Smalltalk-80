"TestLibrary.st - Phase 1 Test Framework"

Object subclass: #TestCase
	instanceVariableNames: 'testSelector failureMessage testsRun testsPassed testsFailed'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'initialization'!
initialize
	"Initialize a new test case"
	testSelector _ nil.
	failureMessage _ nil.
	testsRun _ 0.
	testsPassed _ 0.
	testsFailed _ 0.
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

!TestCase methodsFor: 'test tracking'!
recordTestStart
	"Record that a test has started"
	testsRun _ testsRun + 1!

recordTestPassed
	"Record that a test has passed"
	testsPassed _ testsPassed + 1!

recordTestFailed
	"Record that a test has failed"
	testsFailed _ testsFailed + 1!

runIndividualTest: testMethodSelector
	"Run a single test method and track results"
	self recordTestStart.
	self perform: testMethodSelector.
	self recordTestPassed.
	^true!

getTestCounts
	"Return test statistics as a string"
	^testsRun printString, ' tests run, ', 
	 testsPassed printString, ' passed, ', 
	 testsFailed printString, ' failed'!

resetTestCounts
	"Reset test counters"
	testsRun _ 0.
	testsPassed _ 0.
	testsFailed _ 0! !

!TestCase methodsFor: 'running'!
runTest
	"Run this test case - simple version"
	self setUp.
	self performTest.
	self tearDown.
	^'Test completed'!

performTest
	"Perform the actual test - subclasses should override"
	self resetTestCounts.
	self error: 'Subclass must implement performTest'!

setUp
	"Set up for testing - subclasses may override"
	^self!

tearDown
	"Clean up after testing - subclasses may override"
	^self!

printTestResults
	"Print test results summary"
	| summary |
	summary _ self class name, ': ', self getTestCounts.
	summary _ summary, ' (', 
	((testsPassed = testsRun) 
		ifTrue: ['ALL PASSED'] 
		ifFalse: [testsFailed printString, ' FAILED']), 
	')'.
	^summary! !

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
