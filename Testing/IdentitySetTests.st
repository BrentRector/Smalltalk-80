"Comprehensive tests for IdentitySet class"

TestCase subclass: #IdentitySetTests
	instanceVariableNames: 'iset'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-IdentitySet'!

!IdentitySetTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	iset _ IdentitySet new.
	iset add: #symbol1.
	iset add: #symbol2.
	iset add: #symbol3! !

!IdentitySetTests methodsFor: 'testing-identity-behavior'!
testIdentityBasedMembership
	"Test IdentitySet uses identity for membership"
	| set str1 str2 |
	set _ IdentitySet new.
	str1 _ 'hello'.
	str2 _ 'hello' copy.
	
	"Add first string"
	set add: str1.
	self assertEqual: 1 to: set size.
	self assertTrue: (set includes: str1).
	
	"Add equal but not identical string"
	set add: str2.
	self assertEqual: 2 to: set size.
	self assertTrue: (set includes: str1).
	self assertTrue: (set includes: str2)!

testIdentityVsEquality
	"Test IdentitySet vs regular Set behavior"
	| identitySet regularSet str1 str2 |
	identitySet _ IdentitySet new.
	regularSet _ Set new.
	str1 _ 'hello'.
	str2 _ 'hello' copy.
	
	"Regular set treats equal strings as same"
	regularSet add: str1.
	regularSet add: str2.
	self assertEqual: 1 to: regularSet size.
	
	"Identity set treats equal but non-identical strings as different"
	identitySet add: str1.
	identitySet add: str2.
	self assertEqual: 2 to: identitySet size! !

!IdentitySetTests methodsFor: 'testing-basic-operations'!
testAdd
	"Test IdentitySet>>add:"
	| obj1 obj2 |
	obj1 _ Object new.
	obj2 _ Object new.
	
	iset add: obj1.
	self assertEqual: 4 to: iset size.
	self assertTrue: (iset includes: obj1).
	
	"Adding same object should not increase size"
	iset add: obj1.
	self assertEqual: 4 to: iset size.
	
	"Adding different object should increase size"
	iset add: obj2.
	self assertEqual: 5 to: iset size!

testIncludes
	"Test IdentitySet>>includes:"
	self assertTrue: (iset includes: #symbol1).
	self assertTrue: (iset includes: #symbol2).
	self assertTrue: (iset includes: #symbol3).
	self assertFalse: (iset includes: #nonexistent)!

testSize
	"Test IdentitySet>>size"
	self assertEqual: 3 to: iset size.
	self assertEqual: 0 to: (IdentitySet new) size! !

!IdentitySetTests methodsFor: 'testing-removing'!
testRemove
	"Test IdentitySet>>remove:ifAbsent:"
	| testSet obj |
	testSet _ IdentitySet new.
	obj _ Object new.
	testSet add: obj.
	
	testSet remove: obj ifAbsent: [].
	self assertEqual: 0 to: testSet size.
	self assertFalse: (testSet includes: obj)! !

!IdentitySetTests methodsFor: 'testing-enumeration'!
testDo
	"Test IdentitySet>>do:"
	| count |
	count _ 0.
	iset do: [:each | count _ count + 1].
	self assertEqual: 3 to: count! !

!IdentitySetTests methodsFor: 'running'!
performTest
	"Run all the identity set tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testIdentityBasedMembership.
	self runIndividualTest: #testIdentityVsEquality.
	self runIndividualTest: #testAdd.
	self runIndividualTest: #testIncludes.
	self runIndividualTest: #testSize.
	self runIndividualTest: #testRemove.
	self runIndividualTest: #testDo.
	self printTestResults! !
