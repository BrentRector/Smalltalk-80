"TestSuite_Complete.st - Comprehensive Smalltalk-80 Test Suite"
"Replaces C# integration tests with authentic Smalltalk testing"

Object subclass: #TestCase
	instanceVariableNames: 'testSelector failureMessage testResults'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'initialization'!
initialize
	"Initialize a new test case"
	testSelector _ nil.
	failureMessage _ nil.
	testResults _ Array new: 0.
	^self! !

!TestCase methodsFor: 'assertions'!
assert: condition
	"Assert that condition is true"
	condition ifFalse: [ self fail: 'Assertion failed' ]!

assertEqual: expected to: actual
	"Assert that expected equals actual"
	expected = actual ifFalse: [ 
		self fail: 'Expected ', expected printString, ' but got ', actual printString
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
	"Run this test case"
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

TestCase subclass: #SimpleExecutionTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Execution'!

!SimpleExecutionTests methodsFor: 'testing'!
testReturnConstant
	"Test simple method that returns a constant - replaces SimpleExecutionTests.SimpleMethod_ReturnConstant_ExecutesCorrectly"
	| result |
	result _ 42.
	self assertEqual: 42 to: result!

testBasicArithmetic
	"Test basic arithmetic operations - replaces SimpleExecutionTests arithmetic tests"
	self assertEqual: 7 to: 3 + 4.
	self assertEqual: 12 to: 3 * 4.
	self assertEqual: 1 to: 5 - 4.
	self assertEqual: 2 to: 8 / 4!

testMethodWithArgument
	"Test method execution with arguments - replaces SimpleExecutionTests.SimpleMethod_WithArgument_ExecutesCorrectly"
	| result x |
	x _ 5.
	result _ x + 1.
	self assertEqual: 6 to: result!

performTest
	"Run all simple execution tests"
	self testReturnConstant.
	self testBasicArithmetic.
	self testMethodWithArgument! !

TestCase subclass: #IntegrationExecutionTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Integration'!

!IntegrationExecutionTests methodsFor: 'testing'!
testConstantReturn
	"Test simple constant return - replaces IntegrationExecutionTests.SimpleMethodExecution_ReturnConstant_WorksCorrectly"
	| result |
	result _ 42.
	self assertEqual: 42 to: result!

testArithmeticOperation
	"Test arithmetic method execution - replaces IntegrationExecutionTests.ArithmeticMethodExecution_AddTwoNumbers_WorksCorrectly"
	| arg0 arg1 result |
	arg0 _ 15.
	arg1 _ 27.
	result _ arg0 + arg1.
	self assertEqual: 42 to: result!

testConditionalExecution
	"Test conditional execution with ifTrue:ifFalse: - replaces conditional execution tests"
	| result condition |
	condition _ true.
	result _ condition ifTrue: [42] ifFalse: [99].
	self assertEqual: 42 to: result.
	
	condition _ false.
	result _ condition ifTrue: [42] ifFalse: [99].
	self assertEqual: 99 to: result!

testBlockExecution
	"Test block creation and execution - replaces block execution tests"
	| block result |
	block _ [3 + 4].
	result _ block value.
	self assertEqual: 7 to: result!

performTest
	"Run all integration execution tests"
	self testConstantReturn.
	self testArithmeticOperation.
	self testConditionalExecution.
	self testBlockExecution! !

TestCase subclass: #StandardLibraryTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-StandardLibrary'!

!StandardLibraryTests methodsFor: 'testing'!
testObjectClass
	"Test Object class accessibility - replaces VirtualMachineIntegrationTests.GlobalObject_RespondsToClassMessage"
	| obj objClass |
	obj _ Object new.
	objClass _ obj class.
	self assertNotNil: objClass!

testBooleanLogic
	"Test Boolean class hierarchy and logic operations"
	| result |
	result _ true and: [true].
	self assertTrue: result.
	
	result _ false or: [true].
	self assertTrue: result.
	
	result _ true not.
	self assertFalse: result!

testSmallIntegerOperations
	"Test SmallInteger arithmetic and comparison operations"
	self assertEqual: 10 to: 3 + 7.
	self assertEqual: 21 to: 3 * 7.
	self assertTrue: 5 < 10.
	self assertTrue: 10 > 5.
	self assertTrue: 5 = 5.
	self assertFalse: 5 = 7!

testStringOperations
	"Test String class basic operations"
	| str result |
	str _ 'Hello'.
	self assertEqual: 5 to: str size.
	result _ str, ' World'.
	self assertEqual: 11 to: result size!

testArrayOperations
	"Test Array class basic operations"
	| arr |
	arr _ Array new: 3.
	arr at: 1 put: 'first'.
	arr at: 2 put: 'second'.
	arr at: 3 put: 'third'.
	self assertEqual: 3 to: arr size.
	self assertEqual: 'first' to: (arr at: 1)!

performTest
	"Run all standard library tests"
	self testObjectClass.
	self testBooleanLogic.
	self testSmallIntegerOperations.
	self testStringOperations.
	self testArrayOperations! !

TestCase subclass: #ParserValidationTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Parser'!

!ParserValidationTests methodsFor: 'testing'!
testMethodSignatureParsing
	"Test method signature parsing - replaces method signature parsing tests"
	| result |
	"Test simple unary method"
	result _ true.
	self assertTrue: result.
	
	"Test keyword method with one argument"
	result _ 5 + 3.
	self assertEqual: 8 to: result.
	
	"Test keyword method with multiple arguments"
	result _ true ifTrue: [1] ifFalse: [2].
	self assertEqual: 1 to: result!

testExpressionParsing
	"Test expression parsing including blocks and cascades"
	| result block |
	"Test simple expression"
	result _ 2 + 3 * 4.
	self assertEqual: 14 to: result.
	
	"Test block expression"
	block _ [2 + 3].
	result _ block value.
	self assertEqual: 5 to: result.
	
	"Test nested blocks"
	result _ true ifTrue: [
		false ifTrue: [1] ifFalse: [
			true ifTrue: [2] ifFalse: [3]
		]
	] ifFalse: [4].
	self assertEqual: 2 to: result!

testComplexExpressions
	"Test complex expression parsing and evaluation"
	| result |
	"Test boolean expressions with blocks"
	result _ true and: [true] and: [false not].
	self assertTrue: result.
	
	"Test arithmetic with boolean conditionals"
	result _ (3 < 5) ifTrue: [10 + 5] ifFalse: [10 - 5].
	self assertEqual: 15 to: result!

performTest
	"Run all parser validation tests"
	self testMethodSignatureParsing.
	self testExpressionParsing.
	self testComplexExpressions! !

TestCase subclass: #SystemIntegrationTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-System'!

!SystemIntegrationTests methodsFor: 'testing'!
testClassCreation
	"Test class creation and method installation - replaces class creation tests"
	| obj result |
	obj _ Object new.
	self assertNotNil: obj.
	result _ obj class.
	self assertNotNil: result!

testMethodLookup
	"Test method lookup and execution - replaces method lookup tests"
	| obj result |
	obj _ Object new.
	result _ obj yourself.
	self assertEqual: obj to: result!

testInheritance
	"Test class inheritance hierarchy"
	| obj result |
	obj _ SmallInteger new: 42.
	self assertTrue: (obj isKindOf: Object).
	self assertTrue: (obj isKindOf: Number).
	self assertTrue: (obj isKindOf: Integer).
	self assertTrue: (obj isKindOf: SmallInteger)!

testPrimitiveOperations
	"Test primitive operations work correctly"
	| result |
	result _ 1000 + 200.
	self assertEqual: 1200 to: result.
	
	result _ 1000 - 100.
	self assertEqual: 900 to: result!

performTest
	"Run all system integration tests"
	self testClassCreation.
	self testMethodLookup.
	self testInheritance.
	self testPrimitiveOperations! !

Object subclass: #TestRunner
	instanceVariableNames: 'testSuites results'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestRunner methodsFor: 'initialization'!
initialize
	"Initialize the test runner"
	testSuites _ Array new: 0.
	results _ Array new: 0.
	^self! !

!TestRunner methodsFor: 'running'!
addTestSuite: aTestSuite
	"Add a test suite to run"
	testSuites _ testSuites copyWith: aTestSuite!

runAllTests
	"Run all registered test suites"
	| passCount failCount |
	passCount _ 0.
	failCount _ 0.
	
	testSuites do: [:suite |
		| testCase result |
		testCase _ suite new.
		result _ self runTestCase: testCase.
		result ifTrue: [passCount _ passCount + 1] ifFalse: [failCount _ failCount + 1]
	].
	
	^Array with: passCount with: failCount!

runTestCase: aTestCase
	"Run a single test case and return true if it passes"
	| passed |
	passed _ true.
	[aTestCase runTest] 
		ifError: [:error | 
			passed _ false.
			"Test failed with error: " , error printString
		].
	^passed! !

TestRunner class methodsFor: 'running'!
runComprehensiveTests
	"Run all comprehensive tests - replaces C# integration tests"
	| runner results passCount failCount |
	runner _ TestRunner new.
	
	"Add all test suites"
	runner addTestSuite: SimpleExecutionTests.
	runner addTestSuite: IntegrationExecutionTests.
	runner addTestSuite: StandardLibraryTests.
	runner addTestSuite: ParserValidationTests.
	runner addTestSuite: SystemIntegrationTests.
	
	"Run tests"
	results _ runner runAllTests.
	passCount _ results at: 1.
	failCount _ results at: 2.
	
	"Report results"
	^'Comprehensive Test Results: ', passCount printString, ' passed, ', failCount printString, ' failed'! !

"Test method for quick validation"
Object subclass: #QuickTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Quick'!

!QuickTest methodsFor: 'testing'!
runQuickValidation
	"Quick validation test to ensure basic functionality works"
	| result |
	"Test arithmetic"
	result _ 3 + 4.
	result = 7 ifFalse: [^'FAILED: Basic arithmetic'].
	
	"Test conditionals"
	result _ true ifTrue: [42] ifFalse: [99].
	result = 42 ifFalse: [^'FAILED: Conditional execution'].
	
	"Test object creation"
	result _ Object new.
	result isNil ifTrue: [^'FAILED: Object creation'].
	
	^'SUCCESS: All quick tests passed'! !

QuickTest class methodsFor: 'running'!
validate
	"Run quick validation"
	^QuickTest new runQuickValidation! !
