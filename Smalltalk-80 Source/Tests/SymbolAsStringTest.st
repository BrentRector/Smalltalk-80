"SymbolAsStringTest - Minimal test to reproduce Symbol>>asString bug"

Object subclass: #SymbolAsStringTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!SymbolAsStringTest methodsFor: 'testing'!

test
	"Reproduce the Symbol>>asString / String>>at: bug"
	^self class name asString! !

!SymbolAsStringTest class methodsFor: 'running'!

run
	^self new test!

validate
	"Self-validating test - returns 99 if asString returns correct value"
	| result expected |
	expected _ 'SymbolAsStringTest'.
	result _ self new test.
	^result = expected ifTrue: [99] ifFalse: [0]! !!

SymbolAsStringTest validate!
