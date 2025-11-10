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
	^self new test! !!

SymbolAsStringTest run!
