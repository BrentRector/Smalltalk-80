"Simple test class for validation"

Object subclass: #SimpleTestClass
	instanceVariableNames: 'testValue'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

!SimpleTestClass methodsFor: 'accessing'!
setTestValue: anInteger
	"Set the test value"
	testValue := anInteger!

getTestValue
	"Get the test value"  
	^testValue! !

!SimpleTestClass methodsFor: 'testing'!
simpleTest
	"A simple test method"
	^'simple test passed'! !
