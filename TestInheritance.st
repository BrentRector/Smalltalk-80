Object subclass: #TestBase
	instanceVariableNames: 'base1 base2'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Inheritance'!

TestBase subclass: #TestDerived
	instanceVariableNames: 'derived1'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Inheritance'!

!TestBase methodsFor: 'accessing'!
base1
	^base1!

base1: anObject
	base1 := anObject!

base2
	^base2!

base2: anObject
	base2 := anObject! !

!TestDerived methodsFor: 'accessing'!
derived1
	^derived1!

derived1: anObject
	derived1 := anObject!

testAllFields
	"Test that we can access both inherited and local fields"
	base1 := 'from base 1'.
	base2 := 'from base 2'.
	derived1 := 'from derived'.
	^{base1. base2. derived1}! !

Magnitude subclass: #TestLookupKey
	instanceVariableNames: 'testKey'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Inheritance'!

TestLookupKey subclass: #TestAssociation
	instanceVariableNames: 'testValue'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Inheritance'!

!TestLookupKey methodsFor: 'accessing'!
testKey
	^testKey!

testKey: anObject
	testKey := anObject! !

!TestAssociation methodsFor: 'accessing'!
testValue
	^testValue!

testValue: anObject
	testValue := anObject!

testBothFields
	"Test accessing both inherited key and local value"
	testKey := 'the key'.
	testValue := 'the value'.
	^testKey -> testValue! !