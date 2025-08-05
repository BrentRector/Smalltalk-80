"Simple TestCase class"

Object subclass: #TestCase
	instanceVariableNames: 'failureMessage'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Framework'!

!TestCase methodsFor: 'assertions'!
assert: condition
	"Assert that condition is true"
	condition ifFalse: [ self fail: 'Assertion failed' ]! !
