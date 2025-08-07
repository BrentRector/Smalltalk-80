"Test class with proper Smalltalk-80 assignment syntax"

Object subclass: #TestClass
	instanceVariableNames: 'value result'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

!TestClass methodsFor: 'accessing'!
getValue
	"Return the current value"
	^value!

setValue: anObject
	"Set the value using proper underscore assignment"
	value _ anObject!

getResult
	"Return the current result"
	^result!

testMethod
	"A simple test method with proper assignment"
	result _ 42.
	^result! !
