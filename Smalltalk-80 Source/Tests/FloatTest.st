"FloatTest - Simple Float arithmetic test"

Object subclass: #FloatTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!FloatTest methodsFor: 'testing'!

test
	"Test basic Float arithmetic"
	| a b c |
	a _ 3.14.
	b _ 2.0.
	c _ a / b.
	^c! !

!FloatTest class methodsFor: 'running'!

run
	^self new test! !!

FloatTest run!
