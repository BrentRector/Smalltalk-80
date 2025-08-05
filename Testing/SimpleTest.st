"Simple Test to debug output issues"

TestCase subclass: #SimpleTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Simple'!

!SimpleTest methodsFor: 'testing'!
testBasic
	"A very simple test"
	self assertEqual: 2 to: 1 + 1!

testTrue
	"Test true"
	self assertTrue: true!

testFalse
	"Test false"
	self assertFalse: false! !

!SimpleTest methodsFor: 'running'!
performTest
	"Run simple tests"
	self resetTestCounts.
	self runIndividualTest: #testBasic.
	self runIndividualTest: #testTrue.
	self runIndividualTest: #testFalse.
	'SimpleTest completed with ', self getTestCounts! !
