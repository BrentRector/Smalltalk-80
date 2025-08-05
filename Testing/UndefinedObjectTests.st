"Comprehensive tests for UndefinedObject (nil) class"

TestCase subclass: #UndefinedObjectTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-UndefinedObject'!

!UndefinedObjectTests methodsFor: 'testing-testing'!
testIsNil
	"Test UndefinedObject>>isNil override"
	self assertTrue: nil isNil!

testNotNil
	"Test UndefinedObject>>notNil override"
	self assertFalse: nil notNil! !

!UndefinedObjectTests methodsFor: 'testing-printing'!
testPrintString
	"Test UndefinedObject>>printString"
	self assertEqual: 'nil' to: nil printString! !

!UndefinedObjectTests methodsFor: 'testing-identity'!
testNilIdentity
	"Test that nil is identical to itself"
	self assertTrue: nil == nil.
	self assertTrue: nil = nil.
	self assertFalse: nil ~= nil.
	self assertFalse: nil ~~ nil! !

!UndefinedObjectTests methodsFor: 'running'!
performTest
	"Run all the UndefinedObject tests"
	self resetTestCounts.
	self runIndividualTest: #testIsNil.
	self runIndividualTest: #testNotNil.
	self runIndividualTest: #testPrintString.
	self runIndividualTest: #testNilIdentity.
	self printTestResults! !
