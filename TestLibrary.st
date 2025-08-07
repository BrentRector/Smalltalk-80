"TestLibrary.st - Smalltalk-80 Test Framework"
"Provides essential test framework classes for validating interpreter operation"
"Loaded after StandardLibrary.st when running test cases"

"==============================================================================="
"Test Framework Core Classes"
"==============================================================================="

Object subclass: #TestCase
	instanceVariableNames: 'testSelector failureMessage'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'initialization'!
initialize
	testSelector _ nil.
	failureMessage _ nil.
	^self
! !

!TestCase methodsFor: 'assertions'!
assert: condition
	"Basic assertion - condition must be true"
	condition ifFalse: [ self fail: 'Assertion failed' ]
! !

!TestCase methodsFor: 'assertions'!
assert: condition description: aString
	"Assertion with custom description"
	condition ifFalse: [ self fail: aString ]
! !

!TestCase methodsFor: 'assertions'!
assertEqual: expected to: actual
	"Assert two values are equal"
	expected = actual ifFalse: [ 
		self fail: 'Expected ', expected printString, ' but got ', actual printString 
	]
! !

!TestCase methodsFor: 'assertions'!
assertNotEqual: expected to: actual
	"Assert two values are not equal"
	expected = actual ifTrue: [ 
		self fail: 'Expected values to be different, but both were ', expected printString 
	]
! !

!TestCase methodsFor: 'assertions'!
assertTrue: condition
	"Assert condition is true"
	condition ifFalse: [ self fail: 'Expected true but got false' ]
! !

!TestCase methodsFor: 'assertions'!
assertFalse: condition
	"Assert condition is false"
	condition ifTrue: [ self fail: 'Expected false but got true' ]
! !

!TestCase methodsFor: 'assertions'!
assertNil: anObject
	"Assert object is nil"
	anObject isNil ifFalse: [ self fail: 'Expected nil but got ', anObject printString ]
! !

!TestCase methodsFor: 'assertions'!
assertNotNil: anObject
	"Assert object is not nil"
	anObject isNil ifTrue: [ self fail: 'Expected non-nil value but got nil' ]
! !

!TestCase methodsFor: 'failure handling'!
fail: aString
	"Mark test as failed with message"
	failureMessage _ aString.
	^TestFailure signal: aString
! !

!TestCase methodsFor: 'running'!
runTest
	"Execute the test method"
	| result |
	result _ TestResult new.
	
	[
		self setUp.
		self performTest.
		result recordSuccess: self
	] on: TestFailure do: [ :ex |
		result recordFailure: self message: ex messageText
	] on: Error do: [ :ex |
		result recordError: self message: ex messageText
	].
	
	[ self tearDown ] on: Error do: [ :ex |
		"Ignore tearDown errors for now"
	].
	
	^result
! !

!TestCase methodsFor: 'running'!
performTest
	"Override in subclasses to provide actual test logic"
	self subclassResponsibility
! !

!TestCase methodsFor: 'running'!
setUp
	"Override in subclasses for test setup"
	"Default implementation does nothing"
! !

!TestCase methodsFor: 'running'!
tearDown
	"Override in subclasses for test cleanup"
	"Default implementation does nothing"
! !

!TestCase methodsFor: 'accessing'!
testSelector
	^testSelector
! !

!TestCase methodsFor: 'accessing'!
testSelector: aSymbol
	testSelector _ aSymbol
! !

!TestCase methodsFor: 'accessing'!
failureMessage
	^failureMessage
! !

"==============================================================================="
"Test Result Management"
"==============================================================================="

Object subclass: #TestResult
	instanceVariableNames: 'successes failures errors'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestResult methodsFor: 'initialization'!
initialize
	successes _ Array new: 0.
	failures _ Array new: 0.
	errors _ Array new: 0.
	^self
! !

!TestResult methodsFor: 'recording'!
recordSuccess: aTestCase
	successes _ successes copyWith: aTestCase
! !

!TestResult methodsFor: 'recording'!
recordFailure: aTestCase message: aString
	failures _ failures copyWith: (Array with: aTestCase with: aString)
! !

!TestResult methodsFor: 'recording'!
recordError: aTestCase message: aString
	errors _ errors copyWith: (Array with: aTestCase with: aString)
! !

!TestResult methodsFor: 'accessing'!
successes
	^successes
! !

!TestResult methodsFor: 'accessing'!
failures
	^failures
! !

!TestResult methodsFor: 'accessing'!
errors
	^errors
! !

!TestResult methodsFor: 'testing'!
isSuccessful
	^failures size = 0 and: [ errors size = 0 ]
! !

!TestResult methodsFor: 'accessing'!
totalTests
	^successes size + failures size + errors size
! !

!TestResult methodsFor: 'printing'!
printOn: aStream
	aStream 
		nextPutAll: 'TestResult(';
		nextPutAll: self totalTests printString;
		nextPutAll: ' tests, ';
		nextPutAll: successes size printString;
		nextPutAll: ' passed, ';
		nextPutAll: failures size printString;
		nextPutAll: ' failed, ';
		nextPutAll: errors size printString;
		nextPutAll: ' errors)'
! !

"==============================================================================="
"Test Suite Management"
"==============================================================================="

Object subclass: #TestSuite
	instanceVariableNames: 'testCases name'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestSuite methodsFor: 'initialization'!
initialize
	testCases _ Array new: 0.
	name _ 'Unnamed Test Suite'.
	^self
! !

!TestSuite methodsFor: 'initialization'!
named: aString
	name _ aString.
	^self
! !

!TestSuite methodsFor: 'adding'!
addTestCase: aTestCase
	testCases _ testCases copyWith: aTestCase
! !

!TestSuite methodsFor: 'adding'!
addTestCasesFromClass: aTestClass
	"Add all test methods from a test class"
	| selectors |
	selectors _ aTestClass methodDictionary keys.
	selectors do: [ :selector |
		(selector asString startsWith: 'test') ifTrue: [
			| testCase |
			testCase _ aTestClass new.
			testCase testSelector: selector.
			self addTestCase: testCase
		]
	]
! !

!TestSuite methodsFor: 'running'!
run
	"Run all test cases and return combined result"
	| overallResult |
	overallResult _ TestResult new.
	
	testCases do: [ :testCase |
		| result |
		result _ testCase runTest.
		overallResult combineWith: result
	].
	
	^overallResult
! !

!TestSuite methodsFor: 'accessing'!
testCases
	^testCases
! !

!TestSuite methodsFor: 'accessing'!
name
	^name
! !

!TestSuite methodsFor: 'accessing'!
size
	^testCases size
! !

"==============================================================================="
"Test Exceptions"
"==============================================================================="

Error subclass: #TestFailure
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestFailure methodsFor: 'signaling'!
signal: aString
	"Signal a test failure with message"
	^super signal: aString
! !

"==============================================================================="
"Test Runner and Discovery"
"==============================================================================="

Object subclass: #TestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestRunner class methodsFor: 'running'!
runAllTests
	"Discover and run all test classes"
	| suite |
	suite _ TestSuite new named: 'All Tests'.
	self discoverTestClasses do: [ :testClass |
		suite addTestCasesFromClass: testClass
	].
	^suite run
! !

!TestRunner class methodsFor: 'running'!
runTestClass: aTestClass
	"Run all tests in a specific test class"
	| suite |
	suite _ TestSuite new named: aTestClass name.
	suite addTestCasesFromClass: aTestClass.
	^suite run
! !

!TestRunner class methodsFor: 'discovery'!
discoverTestClasses
	"Find all classes that inherit from TestCase"
	| testClasses |
	testClasses _ Array new: 0.
	SystemDictionary allClasses do: [ :cls |
		(cls inheritsFrom: TestCase) ifTrue: [
			testClasses _ testClasses copyWith: cls
		]
	].
	^testClasses
! !

!TestRunner class methodsFor: 'reporting'!
reportResults: aTestResult
	"Print test results to transcript/output"
	| stream |
	stream _ WriteStream on: String new.
	
	stream 
		nextPutAll: '=== Test Results ==='; cr;
		nextPutAll: 'Total Tests: '; nextPutAll: aTestResult totalTests printString; cr;
		nextPutAll: 'Passed: '; nextPutAll: aTestResult successes size printString; cr;
		nextPutAll: 'Failed: '; nextPutAll: aTestResult failures size printString; cr;
		nextPutAll: 'Errors: '; nextPutAll: aTestResult errors size printString; cr; cr.
	
	aTestResult failures size > 0 ifTrue: [
		stream nextPutAll: 'FAILURES:'; cr.
		aTestResult failures do: [ :failure |
			stream 
				nextPutAll: '  '; nextPutAll: failure first printString;
				nextPutAll: ': '; nextPutAll: failure second; cr
		].
		stream cr
	].
	
	aTestResult errors size > 0 ifTrue: [
		stream nextPutAll: 'ERRORS:'; cr.
		aTestResult errors do: [ :error |
			stream 
				nextPutAll: '  '; nextPutAll: error first printString;
				nextPutAll: ': '; nextPutAll: error second; cr
		].
		stream cr
	].
	
	^stream contents
! !

"==============================================================================="
"Enhanced TestResult with Combination Support"
"==============================================================================="

!TestResult methodsFor: 'combining'!
combineWith: anotherResult
	"Combine results from another TestResult"
	successes _ successes, anotherResult successes.
	failures _ failures, anotherResult failures.
	errors _ errors, anotherResult errors
! !

"==============================================================================="
"Collection Support Extensions (Basic Array Operations)"
"==============================================================================="

!Array methodsFor: 'copying'!
copyWith: anElement
	"Return a new array with anElement added"
	| newArray |
	newArray _ Array new: self size + 1.
	1 to: self size do: [ :index |
		newArray at: index put: (self at: index)
	].
	newArray at: self size + 1 put: anElement.
	^newArray
! !

!Array methodsFor: 'enumerating'!
do: aBlock
	"Evaluate aBlock with each element"
	1 to: self size do: [ :index |
		aBlock value: (self at: index)
	]
! !

!Array methodsFor: 'concatenation'!
, anotherArray
	"Concatenate with another array"
	| newArray |
	newArray _ Array new: self size + anotherArray size.
	1 to: self size do: [ :index |
		newArray at: index put: (self at: index)
	].
	1 to: anotherArray size do: [ :index |
		newArray at: self size + index put: (anotherArray at: index)
	].
	^newArray
! !

"==============================================================================="
"String Support Extensions (Basic String Operations)"
"==============================================================================="

!String methodsFor: 'testing'!
startsWith: aString
	"Test if this string starts with aString"
	| size |
	size := aString size.
	self size < size ifTrue: [ ^false ].
	1 to: size do: [ :index |
		(self at: index) = (aString at: index) ifFalse: [ ^false ]
	].
	^true
! !

!String methodsFor: 'converting'!
printString
	"Return string representation"
	^self
! !

"==============================================================================="
"Object Extensions for Test Framework"
"==============================================================================="

!Object methodsFor: 'testing'!
inheritsFrom: aClass
	"Test if this class inherits from aClass"
	| currentClass |
	currentClass := self class.
	[ currentClass notNil ] whileTrue: [
		currentClass = aClass ifTrue: [ ^true ].
		currentClass := currentClass superclass
	].
	^false
! !

!Object methodsFor: 'error handling'!
subclassResponsibility
	"Signal that subclass must implement this method"
	^Error signal: 'Subclass must implement this method'
! !

"==============================================================================="
"SystemDictionary Extensions for Test Discovery"
"==============================================================================="

!SystemDictionary class methodsFor: 'accessing'!
allClasses
	"Return all classes in the system"
	| classes |
	classes _ Array new: 0.
	self keys do: [ :key |
		| value |
		value _ self at: key.
		value isClass ifTrue: [
			classes _ classes copyWith: value
		]
	].
	^classes
! !

"==============================================================================="
"Sample Test Case for Validation"
"==============================================================================="

TestCase subclass: #SampleBasicTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Examples'!

!SampleBasicTest methodsFor: 'testing'!
testBasicArithmetic
	"Test basic arithmetic operations"
	self assertEqual: 4 to: 2 + 2.
	self assertEqual: 6 to: 2 * 3.
	self assertEqual: 1 to: 3 - 2
! !

!SampleBasicTest methodsFor: 'testing'!
testBooleanLogic
	"Test boolean operations"
	self assertTrue: true.
	self assertFalse: false.
	self assertTrue: true & true.
	self assertFalse: true & false
! !

!SampleBasicTest methodsFor: 'testing'!
testObjectIdentity
	"Test object identity and equality"
	| obj1 obj2 |
	obj1 _ Object new.
	obj2 _ Object new.
	self assertEqual: obj1 to: obj1.
	self assertNotEqual: obj1 to: obj2.
	self assertNotNil: obj1
! !

!SampleBasicTest methodsFor: 'testing'!
testConditionalExecution
	"Test conditional execution"
	| result |
	result _ true ifTrue: [ 'success' ] ifFalse: [ 'failure' ].
	self assertEqual: 'success' to: result.
	
	result _ false ifTrue: [ 'failure' ] ifFalse: [ 'success' ].
	self assertEqual: 'success' to: result
! !

"==============================================================================="
"End of TestLibrary.st"
"==============================================================================="
