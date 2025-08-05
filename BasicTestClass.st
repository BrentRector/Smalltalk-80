"Simple test class with basic methods"

Object subclass: #BasicTestClass
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

!BasicTestClass methodsFor: 'testing'!
simpleTest
	"A simple test method"
	^true!

arithmeticTest
	"Test basic arithmetic"
	^2 + 3!

booleanTest
	"Test boolean logic"
	^true not! !
