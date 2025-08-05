"Comprehensive tests for Object class and its methods"

TestCase subclass: #ObjectTests
	instanceVariableNames: 'testObject anotherObject'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Object'!

!ObjectTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	testObject _ Object new.
	anotherObject _ Object new! !

!ObjectTests methodsFor: 'testing-accessing'!
testYourself
	"Test Object>>yourself"
	self assertEqual: testObject to: testObject yourself.
	self assertEqual: 42 to: 42 yourself.
	self assertEqual: 'hello' to: 'hello' yourself! !

!ObjectTests methodsFor: 'testing-testing'!
testIsInteger
	"Test Object>>isInteger"
	self assertFalse: testObject isInteger.
	self assertFalse: 'hello' isInteger.
	self assertFalse: true isInteger.
	self assertFalse: nil isInteger!

testIsNil
	"Test Object>>isNil"
	self assertFalse: testObject isNil.
	self assertFalse: 'hello' isNil.
	self assertFalse: 42 isNil.
	self assertFalse: true isNil!

testNotNil
	"Test Object>>notNil"
	self assertTrue: testObject notNil.
	self assertTrue: 'hello' notNil.
	self assertTrue: 42 notNil.
	self assertTrue: true notNil! !

!ObjectTests methodsFor: 'testing-comparing'!
testEquality
	"Test Object>>= and Object>>~="
	self assertTrue: testObject = testObject.
	self assertFalse: testObject = anotherObject.
	self assertFalse: testObject ~= testObject.
	self assertTrue: testObject ~= anotherObject!

testIdentity
	"Test Object>>== and Object>>~~"
	self assertTrue: testObject == testObject.
	self assertFalse: testObject == anotherObject.
	self assertFalse: testObject ~~ testObject.
	self assertTrue: testObject ~~ anotherObject!

testHash
	"Test Object>>hash"
	| hash1 hash2 |
	hash1 _ testObject hash.
	hash2 _ testObject hash.
	self assertEqual: hash1 to: hash2.
	self assertTrue: (testObject hash) isInteger! !

!ObjectTests methodsFor: 'testing-class-membership'!
testClass
	"Test Object>>class"
	self assertEqual: Object to: testObject class.
	self assertEqual: SmallInteger to: 42 class.
	self assertEqual: String to: 'hello' class!

testIsMemberOf
	"Test Object>>isMemberOf:"
	self assertTrue: (testObject isMemberOf: Object).
	self assertFalse: (testObject isMemberOf: String).
	self assertTrue: (42 isMemberOf: SmallInteger).
	self assertFalse: (42 isMemberOf: Object)!

testIsKindOf
	"Test Object>>isKindOf:"
	self assertTrue: (testObject isKindOf: Object).
	self assertTrue: (42 isKindOf: Object).
	self assertTrue: (42 isKindOf: Number).
	self assertTrue: (42 isKindOf: Integer).
	self assertTrue: (42 isKindOf: SmallInteger).
	self assertFalse: (42 isKindOf: String)! !

!ObjectTests methodsFor: 'testing-accessing'!
testSize
	"Test Object>>size and Object>>basicSize"
	self assertEqual: 0 to: testObject size.
	self assertEqual: 0 to: testObject basicSize! !

!ObjectTests methodsFor: 'testing-copying'!
testCopy
	"Test Object>>copy"
	| copy |
	copy _ testObject copy.
	self assertFalse: copy == testObject.
	self assertEqual: testObject class to: copy class!

testShallowCopy
	"Test Object>>shallowCopy"
	| copy |
	copy _ testObject shallowCopy.
	self assertFalse: copy == testObject.
	self assertEqual: testObject class to: copy class! !

!ObjectTests methodsFor: 'testing-printing'!
testPrintString
	"Test Object>>printString"
	| printString |
	printString _ testObject printString.
	self assertTrue: (printString includes: $O).
	self assertTrue: (printString includes: $b).
	self assertTrue: (printString includes: $j).
	self assertTrue: (printString includes: $e).
	self assertTrue: (printString includes: $c).
	self assertTrue: (printString includes: $t)! !

!ObjectTests methodsFor: 'running'!
performTest
	"Run all the object tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testYourself.
	self runIndividualTest: #testIsInteger.
	self runIndividualTest: #testIsNil.
	self runIndividualTest: #testNotNil.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testIdentity.
	self runIndividualTest: #testHash.
	self runIndividualTest: #testClass.
	self runIndividualTest: #testIsMemberOf.
	self runIndividualTest: #testIsKindOf.
	self runIndividualTest: #testSize.
	self runIndividualTest: #testCopy.
	self runIndividualTest: #testShallowCopy.
	self runIndividualTest: #testPrintString.
	self printTestResults! !
