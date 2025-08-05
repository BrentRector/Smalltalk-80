"StandardLibrary_Boolean_Stream_Tests.st - Tests for Boolean hierarchy and Stream classes"
"Generated August 5, 2025"

TestCase subclass: #BooleanTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!BooleanTests methodsFor: 'testing-logical-operations'!
testAnd
	"Test Boolean>>#&"
	self assertTrue: true & true.
	self assertFalse: true & false.
	self assertFalse: false & true.
	self assertFalse: false & false!

testOr
	"Test Boolean>>#|"
	self assertTrue: true | true.
	self assertTrue: true | false.
	self assertTrue: false | true.
	self assertFalse: false | false!

testNot
	"Test Boolean>>#not"
	self assertFalse: true not.
	self assertTrue: false not! !

!BooleanTests methodsFor: 'testing-conditional-execution'!
testIfTrue
	"Test Boolean>>#ifTrue:"
	| result |
	result _ false.
	true ifTrue: [result _ true].
	self assertTrue: result.
	
	result _ true.
	false ifTrue: [result _ false].
	self assertTrue: result!

testIfFalse
	"Test Boolean>>#ifFalse:"
	| result |
	result _ true.
	false ifFalse: [result _ false].
	self assertFalse: result.
	
	result _ false.
	true ifFalse: [result _ true].
	self assertFalse: result!

testIfTrueIfFalse
	"Test Boolean>>#ifTrue:ifFalse:"
	| result |
	result _ true ifTrue: ['yes'] ifFalse: ['no'].
	self assertEqual: result to: 'yes'.
	
	result _ false ifTrue: ['yes'] ifFalse: ['no'].
	self assertEqual: result to: 'no'!

testIfFalseIfTrue
	"Test Boolean>>#ifFalse:ifTrue:"
	| result |
	result _ false ifFalse: ['no'] ifTrue: ['yes'].
	self assertEqual: result to: 'no'.
	
	result _ true ifFalse: ['no'] ifTrue: ['yes'].
	self assertEqual: result to: 'yes'! !

!BooleanTests methodsFor: 'testing-equality'!
testEquality
	"Test Boolean equality"
	self assertTrue: true = true.
	self assertTrue: false = false.
	self assertFalse: true = false.
	self assertFalse: false = true!

testInequality
	"Test Boolean inequality"
	self assertFalse: true ~= true.
	self assertFalse: false ~= false.
	self assertTrue: true ~= false.
	self assertTrue: false ~= true! !

!BooleanTests methodsFor: 'running'!
performTest
	"Run all Boolean tests"
	self resetTestCounts.
	self testAnd.
	self testOr.
	self testNot.
	self testIfTrue.
	self testIfFalse.
	self testIfTrueIfFalse.
	self testIfFalseIfTrue.
	self testEquality.
	self testInequality! !

TestCase subclass: #TrueTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!TrueTests methodsFor: 'testing'!
testNot
	"Test True>>#not"
	self assertFalse: true not!

testAnd
	"Test True>>#&"
	self assertTrue: true & true.
	self assertFalse: true & false!

testOr
	"Test True>>#|"
	self assertTrue: true | true.
	self assertTrue: true | false!

testIfTrue
	"Test True>>#ifTrue:"
	| executed |
	executed _ false.
	true ifTrue: [executed _ true].
	self assertTrue: executed!

testIfFalse
	"Test True>>#ifFalse:"
	| executed |
	executed _ false.
	true ifFalse: [executed _ true].
	self assertFalse: executed!

testIfTrueIfFalse
	"Test True>>#ifTrue:ifFalse:"
	| result |
	result _ true ifTrue: ['yes'] ifFalse: ['no'].
	self assertEqual: result to: 'yes'! !

!TrueTests methodsFor: 'running'!
performTest
	"Run all True tests"
	self resetTestCounts.
	self testNot.
	self testAnd.
	self testOr.
	self testIfTrue.
	self testIfFalse.
	self testIfTrueIfFalse! !

TestCase subclass: #FalseTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!FalseTests methodsFor: 'testing'!
testNot
	"Test False>>#not"
	self assertTrue: false not!

testAnd
	"Test False>>#&"
	self assertFalse: false & true.
	self assertFalse: false & false!

testOr
	"Test False>>#|"
	self assertTrue: false | true.
	self assertFalse: false | false!

testIfTrue
	"Test False>>#ifTrue:"
	| executed |
	executed _ false.
	false ifTrue: [executed _ true].
	self assertFalse: executed!

testIfFalse
	"Test False>>#ifFalse:"
	| executed |
	executed _ false.
	false ifFalse: [executed _ true].
	self assertTrue: executed!

testIfTrueIfFalse
	"Test False>>#ifTrue:ifFalse:"
	| result |
	result _ false ifTrue: ['yes'] ifFalse: ['no'].
	self assertEqual: result to: 'no'! !

!FalseTests methodsFor: 'running'!
performTest
	"Run all False tests"
	self resetTestCounts.
	self testNot.
	self testAnd.
	self testOr.
	self testIfTrue.
	self testIfFalse.
	self testIfTrueIfFalse! !

TestCase subclass: #StreamTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Streams'!

!StreamTests methodsFor: 'testing-positioning'!
testPosition
	"Test Stream>>#position"
	| stream |
	stream _ ReadWriteStream on: 'hello'.
	self assertEqual: stream position to: 0.
	stream next.
	self assertEqual: stream position to: 1!

testPositionSet
	"Test Stream>>#position:"
	| stream |
	stream _ ReadWriteStream on: 'hello'.
	stream position: 3.
	self assertEqual: stream position to: 3.
	self assertEqual: stream next to: $l! !

!StreamTests methodsFor: 'testing-reading'!
testNext
	"Test Stream>>#next"
	| stream |
	stream _ ReadWriteStream on: 'hello'.
	self assertEqual: stream next to: $h.
	self assertEqual: stream next to: $e!

testAtEnd
	"Test Stream>>#atEnd"
	| stream |
	stream _ ReadWriteStream on: 'hi'.
	self assertFalse: stream atEnd.
	stream next.
	stream next.
	self assertTrue: stream atEnd! !

!StreamTests methodsFor: 'testing-writing'!
testNextPut
	"Test Stream>>#nextPut:"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPut: $h.
	stream nextPut: $i.
	self assertEqual: stream contents to: 'hi'!

testNextPutAll
	"Test Stream>>#nextPutAll:"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPutAll: 'hello'.
	self assertEqual: stream contents to: 'hello'! !

!StreamTests methodsFor: 'running'!
performTest
	"Run all Stream tests"
	self resetTestCounts.
	self testPosition.
	self testPositionSet.
	self testNext.
	self testAtEnd.
	self testNextPut.
	self testNextPutAll! !

TestCase subclass: #ReadStreamTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Streams'!

!ReadStreamTests methodsFor: 'testing-creation'!
testOnString
	"Test ReadStream class>>#on:"
	| stream |
	stream _ ReadStream on: 'hello'.
	self assertEqual: stream next to: $h.
	self assertEqual: stream next to: $e! !

!ReadStreamTests methodsFor: 'testing-reading'!
testNextN
	"Test ReadStream>>#next:"
	| stream result |
	stream _ ReadStream on: 'hello'.
	result _ stream next: 3.
	self assertEqual: result to: 'hel'!

testUpTo
	"Test ReadStream>>#upTo:"
	| stream result |
	stream _ ReadStream on: 'hello world'.
	result _ stream upTo: $ .
	self assertEqual: result to: 'hello'! !

!ReadStreamTests methodsFor: 'running'!
performTest
	"Run all ReadStream tests"
	self resetTestCounts.
	self testOnString.
	self testNextN.
	self testUpTo! !

TestCase subclass: #WriteStreamTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Streams'!

!WriteStreamTests methodsFor: 'testing-creation'!
testOnString
	"Test WriteStream class>>#on:"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPutAll: 'hello'.
	self assertEqual: stream contents to: 'hello'! !

!WriteStreamTests methodsFor: 'testing-writing'!
testCr
	"Test WriteStream>>#cr"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPutAll: 'line1'.
	stream cr.
	stream nextPutAll: 'line2'.
	"Contents should have carriage return"
	self assertTrue: (stream contents includes: Character cr)!

testSpace
	"Test WriteStream>>#space"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPutAll: 'word1'.
	stream space.
	stream nextPutAll: 'word2'.
	self assertEqual: stream contents to: 'word1 word2'!

testTab
	"Test WriteStream>>#tab"
	| stream |
	stream _ WriteStream on: String new.
	stream nextPutAll: 'before'.
	stream tab.
	stream nextPutAll: 'after'.
	"Contents should have tab character"
	self assertTrue: (stream contents includes: Character tab)! !

!WriteStreamTests methodsFor: 'running'!
performTest
	"Run all WriteStream tests"
	self resetTestCounts.
	self testOnString.
	self testCr.
	self testSpace.
	self testTab! !

TestCase subclass: #ReadWriteStreamTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Streams'!

!ReadWriteStreamTests methodsFor: 'testing-creation'!
testOnString
	"Test ReadWriteStream class>>#on:"
	| stream |
	stream _ ReadWriteStream on: 'hello'.
	self assertEqual: stream next to: $h.
	stream nextPut: $X.
	stream reset.
	self assertEqual: stream next to: $h! !

!ReadWriteStreamTests methodsFor: 'testing-reset'!
testReset
	"Test ReadWriteStream>>#reset"
	| stream |
	stream _ ReadWriteStream on: 'hello'.
	stream next.
	stream next.
	stream reset.
	self assertEqual: stream position to: 0.
	self assertEqual: stream next to: $h! !

!ReadWriteStreamTests methodsFor: 'running'!
performTest
	"Run all ReadWriteStream tests"
	self resetTestCounts.
	self testOnString.
	self testReset! !

TestCase subclass: #BooleanStreamTestSuite
	instanceVariableNames: 'booleanTests trueTests falseTests streamTests readStreamTests writeStreamTests readWriteStreamTests'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-BooleanStream'!

!BooleanStreamTestSuite methodsFor: 'initialization'!
initialize
	"Initialize all test cases"
	super initialize.
	booleanTests _ BooleanTests new.
	trueTests _ TrueTests new.
	falseTests _ FalseTests new.
	streamTests _ StreamTests new.
	readStreamTests _ ReadStreamTests new.
	writeStreamTests _ WriteStreamTests new.
	readWriteStreamTests _ ReadWriteStreamTests new! !

!BooleanStreamTestSuite methodsFor: 'running'!
performTest
	"Run all Boolean and Stream tests"
	self resetTestCounts.
	
	"Run all test suites"
	booleanTests performTest.
	trueTests performTest.
	falseTests performTest.
	streamTests performTest.
	readStreamTests performTest.
	writeStreamTests performTest.
	readWriteStreamTests performTest!

runAllTests
	"Run all tests and return summary"
	self initialize.
	self performTest.
	^'Boolean and Stream Test Suite: All tests completed'! !

"End of StandardLibrary_Boolean_Stream_Tests.st"
