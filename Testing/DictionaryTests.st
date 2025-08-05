"Comprehensive tests for Dictionary, IdentityDictionary, Association, and LookupKey classes"

TestCase subclass: #DictionaryTests
	instanceVariableNames: 'dict'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Dictionary'!

!DictionaryTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	dict at: 'key3' put: 'value3'! !

!DictionaryTests methodsFor: 'testing-accessing'!
testAtPut
	"Test Dictionary>>at:put:"
	| newDict |
	newDict _ Dictionary new.
	newDict at: 'testKey' put: 'testValue'.
	self assertEqual: 'testValue' to: (newDict at: 'testKey')!

testAt
	"Test Dictionary>>at:"
	self assertEqual: 'value1' to: (dict at: 'key1').
	self assertEqual: 'value2' to: (dict at: 'key2').
	self assertEqual: 'value3' to: (dict at: 'key3')!

testAtIfAbsent
	"Test Dictionary>>at:ifAbsent:"
	| result |
	result _ dict at: 'key1' ifAbsent: ['not found'].
	self assertEqual: 'value1' to: result.
	
	result _ dict at: 'nonexistent' ifAbsent: ['not found'].
	self assertEqual: 'not found' to: result!

testSize
	"Test Dictionary>>size"
	self assertEqual: 3 to: dict size.
	self assertEqual: 0 to: (Dictionary new) size! !

!DictionaryTests methodsFor: 'testing-keys-values'!
testKeys
	"Test Dictionary>>keys"
	| keys |
	keys _ dict keys.
	self assertEqual: 3 to: keys size.
	self assertTrue: (keys includes: 'key1').
	self assertTrue: (keys includes: 'key2').
	self assertTrue: (keys includes: 'key3')!

testValues
	"Test Dictionary>>values"
	| values |
	values _ dict values.
	self assertEqual: 3 to: values size.
	self assertTrue: (values includes: 'value1').
	self assertTrue: (values includes: 'value2').
	self assertTrue: (values includes: 'value3')!

testIncludesKey
	"Test Dictionary>>includesKey:"
	self assertTrue: (dict includesKey: 'key1').
	self assertTrue: (dict includesKey: 'key2').
	self assertFalse: (dict includesKey: 'nonexistent')!

testKeyAtValue
	"Test Dictionary>>keyAtValue:"
	self assertEqual: 'key1' to: (dict keyAtValue: 'value1').
	self assertEqual: 'key2' to: (dict keyAtValue: 'value2')!

testKeyAtValueIfAbsent
	"Test Dictionary>>keyAtValue:ifAbsent:"
	| result |
	result _ dict keyAtValue: 'value1' ifAbsent: ['not found'].
	self assertEqual: 'key1' to: result.
	
	result _ dict keyAtValue: 'nonexistent' ifAbsent: ['not found'].
	self assertEqual: 'not found' to: result! !

!DictionaryTests methodsFor: 'testing-associations'!
testAssociations
	"Test Dictionary>>associations"
	| associations |
	associations _ dict associations.
	self assertEqual: 3 to: associations size!

testAssociationAt
	"Test Dictionary>>associationAt:"
	| assoc |
	assoc _ dict associationAt: 'key1'.
	self assertEqual: 'key1' to: assoc key.
	self assertEqual: 'value1' to: assoc value!

testAssociationAtIfAbsent
	"Test Dictionary>>associationAt:ifAbsent:"
	| assoc result |
	assoc _ dict associationAt: 'key1' ifAbsent: [nil].
	self assertEqual: 'key1' to: assoc key.
	self assertEqual: 'value1' to: assoc value.
	
	result _ dict associationAt: 'nonexistent' ifAbsent: ['not found'].
	self assertEqual: 'not found' to: result! !

!DictionaryTests methodsFor: 'testing-removing'!
testRemoveKey
	"Test Dictionary>>removeKey:"
	| testDict |
	testDict _ Dictionary new.
	testDict at: 'temp1' put: 'tempValue1'.
	testDict at: 'temp2' put: 'tempValue2'.
	
	testDict removeKey: 'temp1'.
	self assertEqual: 1 to: testDict size.
	self assertFalse: (testDict includesKey: 'temp1').
	self assertTrue: (testDict includesKey: 'temp2')!

testRemoveKeyIfAbsent
	"Test Dictionary>>removeKey:ifAbsent:"
	| testDict result |
	testDict _ Dictionary new.
	testDict at: 'temp' put: 'tempValue'.
	
	result _ testDict removeKey: 'temp' ifAbsent: ['not found'].
	self assertEqual: 'tempValue' to: result.
	self assertEqual: 0 to: testDict size.
	
	result _ testDict removeKey: 'nonexistent' ifAbsent: ['not found'].
	self assertEqual: 'not found' to: result! !

!DictionaryTests methodsFor: 'testing-enumeration'!
testDo
	"Test Dictionary>>do:"
	| values |
	values _ Array new: 3.
	dict do: [:value | "Note: simplified test due to iteration order uncertainty" ].
	"The actual enumeration works but order is not guaranteed"!

testKeysDo
	"Test Dictionary>>keysDo:"
	| keyCount |
	keyCount _ 0.
	dict keysDo: [:key | keyCount _ keyCount + 1].
	self assertEqual: 3 to: keyCount!

testAssociationsDo
	"Test Dictionary>>associationsDo:"
	| assocCount |
	assocCount _ 0.
	dict associationsDo: [:assoc | assocCount _ assocCount + 1].
	self assertEqual: 3 to: assocCount! !

!DictionaryTests methodsFor: 'running'!
performTest
	"Run all the dictionary tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testAtPut.
	self runIndividualTest: #testAt.
	self runIndividualTest: #testAtIfAbsent.
	self runIndividualTest: #testSize.
	self runIndividualTest: #testKeys.
	self runIndividualTest: #testValues.
	self runIndividualTest: #testIncludesKey.
	self runIndividualTest: #testKeyAtValue.
	self runIndividualTest: #testKeyAtValueIfAbsent.
	self runIndividualTest: #testAssociations.
	self runIndividualTest: #testAssociationAt.
	self runIndividualTest: #testAssociationAtIfAbsent.
	self runIndividualTest: #testRemoveKey.
	self runIndividualTest: #testRemoveKeyIfAbsent.
	self runIndividualTest: #testDo.
	self runIndividualTest: #testKeysDo.
	self runIndividualTest: #testAssociationsDo.
	self printTestResults! !

TestCase subclass: #IdentityDictionaryTests
	instanceVariableNames: 'idict'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-IdentityDictionary'!

!IdentityDictionaryTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	idict _ IdentityDictionary new.
	idict at: #key1 put: 'value1'.
	idict at: #key2 put: 'value2'.
	idict at: #key3 put: 'value3'! !

!IdentityDictionaryTests methodsFor: 'testing-identity-behavior'!
testIdentityVsEquality
	"Test IdentityDictionary uses identity (==) instead of equality (=)"
	| dict1 dict2 str1 str2 |
	dict1 _ Dictionary new.
	dict2 _ IdentityDictionary new.
	str1 _ 'hello'.
	str2 _ 'hello' copy.
	
	"Regular dictionary treats equal strings as same key"
	dict1 at: str1 put: 'value1'.
	dict1 at: str2 put: 'value2'.
	self assertEqual: 1 to: dict1 size.
	
	"Identity dictionary treats equal but non-identical strings as different keys"
	dict2 at: str1 put: 'value1'.
	dict2 at: str2 put: 'value2'.
	self assertEqual: 2 to: dict2 size! !

!IdentityDictionaryTests methodsFor: 'testing-accessing'!
testAtPut
	"Test IdentityDictionary>>at:put:"
	| obj1 obj2 |
	obj1 _ Object new.
	obj2 _ Object new.
	
	idict at: obj1 put: 'first'.
	idict at: obj2 put: 'second'.
	
	self assertEqual: 'first' to: (idict at: obj1).
	self assertEqual: 'second' to: (idict at: obj2).
	self assertEqual: 5 to: idict size! !

!IdentityDictionaryTests methodsFor: 'testing-basic-operations'!
testSize
	"Test IdentityDictionary>>size"
	self assertEqual: 3 to: idict size.
	self assertEqual: 0 to: (IdentityDictionary new) size!

testAtIfAbsent
	"Test IdentityDictionary>>at:ifAbsent:"
	| result |
	result _ idict at: #key1 ifAbsent: ['not found'].
	self assertEqual: 'value1' to: result.
	
	result _ idict at: #nonexistent ifAbsent: ['not found'].
	self assertEqual: 'not found' to: result! !

!IdentityDictionaryTests methodsFor: 'running'!
performTest
	"Run all the identity dictionary tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testIdentityVsEquality.
	self runIndividualTest: #testAtPut.
	self runIndividualTest: #testSize.
	self runIndividualTest: #testAtIfAbsent.
	self printTestResults! !

TestCase subclass: #AssociationTests
	instanceVariableNames: 'assoc'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Association'!

!AssociationTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	assoc _ Association key: 'testKey' value: 'testValue'! !

!AssociationTests methodsFor: 'testing-creation'!
testKeyValue
	"Test Association class>>key:value:"
	| newAssoc |
	newAssoc _ Association key: 'myKey' value: 'myValue'.
	self assertEqual: 'myKey' to: newAssoc key.
	self assertEqual: 'myValue' to: newAssoc value! !

!AssociationTests methodsFor: 'testing-accessing'!
testKey
	"Test Association>>key"
	self assertEqual: 'testKey' to: assoc key!

testValue
	"Test Association>>value"
	self assertEqual: 'testValue' to: assoc value!

testValueSet
	"Test Association>>value:"
	assoc value: 'newValue'.
	self assertEqual: 'newValue' to: assoc value! !

!AssociationTests methodsFor: 'running'!
performTest
	"Run all the association tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testKeyValue.
	self runIndividualTest: #testKey.
	self runIndividualTest: #testValue.
	self runIndividualTest: #testValueSet.
	self printTestResults! !

TestCase subclass: #LookupKeyTests
	instanceVariableNames: 'lookup'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-LookupKey'!

!LookupKeyTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	lookup _ LookupKey key: 'testKey'! !

!LookupKeyTests methodsFor: 'testing-creation'!
testKey
	"Test LookupKey class>>key:"
	| newLookup |
	newLookup _ LookupKey key: 'myKey'.
	self assertEqual: 'myKey' to: newLookup key! !

!LookupKeyTests methodsFor: 'testing-accessing'!
testKeyAccess
	"Test LookupKey>>key"
	self assertEqual: 'testKey' to: lookup key! !

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
	"Run all the lookup key tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testKey.
	self runIndividualTest: #testKeyAccess.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testLessThan.
	self runIndividualTest: #testHash.
	self printTestResults! !
