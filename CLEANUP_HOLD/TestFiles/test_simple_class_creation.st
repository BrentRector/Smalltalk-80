"Simple test of class creation via Smalltalk after Phase 2 bootstrap"

"Create a simple test class"
Object subclass: #SimpleTestClass
	instanceVariableNames: 'value'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

"Add a method"
!SimpleTestClass methodsFor: 'accessing'!
value
	^value!

value: anObject
	value := anObject! !

"Test it"
| test |
test := SimpleTestClass new.
test value: 42.
Transcript show: 'SimpleTestClass value = '; show: test value; cr.
Transcript show: 'Success!'; cr.