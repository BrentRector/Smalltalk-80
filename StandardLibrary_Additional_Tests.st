"StandardLibrary_Additional_Tests.st - Tests for remaining StandardLibrary classes"
"Generated August 5, 2025 - IdentitySet, IdentityDictionary, LookupKey, and other specialized classes"

TestCase subclass: #IdentitySetTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!IdentitySetTests methodsFor: 'testing-identity'!
testIdentityBasedMembership
	"Test IdentitySet uses identity for membership"
	| set str1 str2 |
	set _ IdentitySet new.
	str1 _ 'hello'.
	str2 _ 'hello' copy.
	
	"Add first string"
	set add: str1.
	self assertEqual: set size to: 1.
	self assertTrue: (set includes: str1).
	
	"Add equal but not identical string"
	set add: str2.
	self assertEqual: set size to: 2.
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
	self assertEqual: regularSet size to: 1.
	
	"Identity set treats equal but non-identical strings as different"
	identitySet add: str1.
	identitySet add: str2.
	self assertEqual: identitySet size to: 2! !

!IdentitySetTests methodsFor: 'running'!
performTest
	"Run all IdentitySet tests"
	self resetTestCounts.
	self testIdentityBasedMembership.
	self testIdentityVsEquality! !

TestCase subclass: #IdentityDictionaryTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!IdentityDictionaryTests methodsFor: 'testing-identity'!
testIdentityBasedKeys
	"Test IdentityDictionary uses identity for keys"
	| dict str1 str2 |
	dict _ IdentityDictionary new.
	str1 _ 'key'.
	str2 _ 'key' copy.
	
	"Store value with first key"
	dict at: str1 put: 'value1'.
	self assertEqual: (dict at: str1) to: 'value1'.
	
	"Store value with equal but not identical key"
	dict at: str2 put: 'value2'.
	self assertEqual: dict size to: 2.
	self assertEqual: (dict at: str1) to: 'value1'.
	self assertEqual: (dict at: str2) to: 'value2'!

testIdentityVsRegularDictionary
	"Test IdentityDictionary vs regular Dictionary behavior"
	| identityDict regularDict str1 str2 |
	identityDict _ IdentityDictionary new.
	regularDict _ Dictionary new.
	str1 _ 'key'.
	str2 _ 'key' copy.
	
	"Regular dictionary treats equal strings as same key"
	regularDict at: str1 put: 'value1'.
	regularDict at: str2 put: 'value2'.
	self assertEqual: regularDict size to: 1.
	self assertEqual: (regularDict at: str1) to: 'value2'.
	
	"Identity dictionary treats equal but non-identical strings as different keys"
	identityDict at: str1 put: 'value1'.
	identityDict at: str2 put: 'value2'.
	self assertEqual: identityDict size to: 2.
	self assertEqual: (identityDict at: str1) to: 'value1'.
	self assertEqual: (identityDict at: str2) to: 'value2'! !

!IdentityDictionaryTests methodsFor: 'testing-accessing'!
testAtPut
	"Test IdentityDictionary>>#at:put:"
	| dict obj1 obj2 |
	dict _ IdentityDictionary new.
	obj1 _ Object new.
	obj2 _ Object new.
	
	dict at: obj1 put: 'first'.
	dict at: obj2 put: 'second'.
	
	self assertEqual: (dict at: obj1) to: 'first'.
	self assertEqual: (dict at: obj2) to: 'second'.
	self assertEqual: dict size to: 2! !

!IdentityDictionaryTests methodsFor: 'running'!
performTest
	"Run all IdentityDictionary tests"
	self resetTestCounts.
	self testIdentityBasedKeys.
	self testIdentityVsRegularDictionary.
	self testAtPut! !

TestCase subclass: #LookupKeyTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!LookupKeyTests methodsFor: 'testing-creation'!
testKey
	"Test LookupKey class>>#key:"
	| lookup |
	lookup _ LookupKey key: 'testKey'.
	self assertEqual: lookup key to: 'testKey'! !

!LookupKeyTests methodsFor: 'testing-accessing'!
testKeyAccess
	"Test LookupKey>>#key"
	| lookup |
	lookup _ LookupKey key: 'myKey'.
	self assertEqual: lookup key to: 'myKey'! !

!LookupKeyTests methodsFor: 'testing-comparison'!
testEquality
	"Test LookupKey>>#="
	| lookup1 lookup2 lookup3 |
	lookup1 _ LookupKey key: 'same'.
	lookup2 _ LookupKey key: 'same'.
	lookup3 _ LookupKey key: 'different'.
	
	self assertTrue: lookup1 = lookup2.
	self assertFalse: lookup1 = lookup3!

testLessThan
	"Test LookupKey>>#<"
	| lookup1 lookup2 |
	lookup1 _ LookupKey key: 'a'.
	lookup2 _ LookupKey key: 'b'.
	
	self assertTrue: lookup1 < lookup2.
	self assertFalse: lookup2 < lookup1! !

!LookupKeyTests methodsFor: 'testing-hashing'!
testHash
	"Test LookupKey>>#hash"
	| lookup1 lookup2 |
	lookup1 _ LookupKey key: 'same'.
	lookup2 _ LookupKey key: 'same'.
	
	"Equal keys should have equal hashes"
	self assertEqual: lookup1 hash to: lookup2 hash! !

!LookupKeyTests methodsFor: 'running'!
performTest
	"Run all LookupKey tests"
	self resetTestCounts.
	self testKey.
	self testKeyAccess.
	self testEquality.
	self testLessThan.
	self testHash! !

TestCase subclass: #IntervalTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!IntervalTests methodsFor: 'testing-creation'!
testFromTo
	"Test Interval class>>#from:to:"
	| interval |
	interval _ Interval from: 1 to: 5.
	self assertEqual: interval first to: 1.
	self assertEqual: interval last to: 5.
	self assertEqual: interval size to: 5!

testFromToBy
	"Test Interval class>>#from:to:by:"
	| interval |
	interval _ Interval from: 1 to: 10 by: 2.
	self assertEqual: interval first to: 1.
	self assertEqual: interval last to: 9.
	self assertEqual: interval size to: 5! !

!IntervalTests methodsFor: 'testing-accessing'!
testAt
	"Test Interval>>#at:"
	| interval |
	interval _ Interval from: 10 to: 20 by: 2.
	self assertEqual: (interval at: 1) to: 10.
	self assertEqual: (interval at: 2) to: 12.
	self assertEqual: (interval at: 3) to: 14! !

!IntervalTests methodsFor: 'testing-enumeration'!
testDo
	"Test Interval>>#do:"
	| interval sum |
	interval _ Interval from: 1 to: 5.
	sum _ 0.
	interval do: [:each | sum _ sum + each].
	self assertEqual: sum to: 15! !

!IntervalTests methodsFor: 'testing-membership'!
testIncludes
	"Test Interval>>#includes:"
	| interval |
	interval _ Interval from: 1 to: 10 by: 2.
	self assertTrue: (interval includes: 1).
	self assertTrue: (interval includes: 5).
	self assertTrue: (interval includes: 9).
	self assertFalse: (interval includes: 2).
	self assertFalse: (interval includes: 10)! !

!IntervalTests methodsFor: 'running'!
performTest
	"Run all Interval tests"
	self resetTestCounts.
	self testFromTo.
	self testFromToBy.
	self testAt.
	self testDo.
	self testIncludes! !

TestCase subclass: #BlockTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!BlockTests methodsFor: 'testing-evaluation'!
testValue
	"Test Block>>#value"
	| block result |
	block _ [5 + 3].
	result _ block value.
	self assertEqual: result to: 8!

testValueWith
	"Test Block>>#value:"
	| block result |
	block _ [:x | x * 2].
	result _ block value: 5.
	self assertEqual: result to: 10!

testValueWithWith
	"Test Block>>#value:value:"
	| block result |
	block _ [:x :y | x + y].
	result _ block value: 3 value: 4.
	self assertEqual: result to: 7! !

!BlockTests methodsFor: 'testing-control'!
testWhileTrue
	"Test Block>>#whileTrue:"
	| i sum |
	i _ 1.
	sum _ 0.
	[i <= 5] whileTrue: [
		sum _ sum + i.
		i _ i + 1
	].
	self assertEqual: sum to: 15!

testWhileFalse
	"Test Block>>#whileFalse:"
	| i sum |
	i _ 1.
	sum _ 0.
	[i > 5] whileFalse: [
		sum _ sum + i.
		i _ i + 1
	].
	self assertEqual: sum to: 15! !

!BlockTests methodsFor: 'running'!
performTest
	"Run all Block tests"
	self resetTestCounts.
	self testValue.
	self testValueWith.
	self testValueWithWith.
	self testWhileTrue.
	self testWhileFalse! !

TestCase subclass: #ClassTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!ClassTests methodsFor: 'testing-introspection'!
testName
	"Test Class>>#name"
	self assertEqual: Object name to: #Object.
	self assertEqual: String name to: #String!

testSuperclass
	"Test Class>>#superclass"
	self assertEqual: String superclass to: ArrayedCollection.
	self assertEqual: Object superclass to: nil! !

!ClassTests methodsFor: 'testing-instance-creation'!
testNew
	"Test Class>>#new"
	| obj |
	obj _ Object new.
	self assertEqual: obj class to: Object!

testBasicNew
	"Test Class>>#basicNew"
	| obj |
	obj _ Object basicNew.
	self assertEqual: obj class to: Object! !

!ClassTests methodsFor: 'running'!
performTest
	"Run all Class tests"
	self resetTestCounts.
	self testName.
	self testSuperclass.
	self testNew.
	self testBasicNew! !

TestCase subclass: #MetaclassTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!MetaclassTests methodsFor: 'testing'!
testSoleInstance
	"Test Metaclass>>#soleInstance"
	| metaclass |
	metaclass _ Object class.
	self assertEqual: metaclass soleInstance to: Object! !

!MetaclassTests methodsFor: 'running'!
performTest
	"Run all Metaclass tests"
	self resetTestCounts.
	self testSoleInstance! !

TestCase subclass: #AdditionalTestSuite
	instanceVariableNames: 'identitySetTests identityDictionaryTests lookupKeyTests intervalTests blockTests classTests metaclassTests'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Additional'!

!AdditionalTestSuite methodsFor: 'initialization'!
initialize
	"Initialize all test cases"
	super initialize.
	identitySetTests _ IdentitySetTests new.
	identityDictionaryTests _ IdentityDictionaryTests new.
	lookupKeyTests _ LookupKeyTests new.
	intervalTests _ IntervalTests new.
	blockTests _ BlockTests new.
	classTests _ ClassTests new.
	metaclassTests _ MetaclassTests new! !

!AdditionalTestSuite methodsFor: 'running'!
performTest
	"Run all additional tests"
	self resetTestCounts.
	
	"Run all test suites"
	identitySetTests performTest.
	identityDictionaryTests performTest.
	lookupKeyTests performTest.
	intervalTests performTest.
	blockTests performTest.
	classTests performTest.
	metaclassTests performTest!

runAllTests
	"Run all tests and return summary"
	self initialize.
	self performTest.
	^'Additional StandardLibrary Test Suite: All tests completed'! !

"End of StandardLibrary_Additional_Tests.st"
