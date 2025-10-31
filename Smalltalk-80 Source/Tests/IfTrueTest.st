"IfTrueTest - Reproduce ifTrue: parsing/execution issue"

Object subclass: #IfTrueTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-ControlFlow'! !

!IfTrueTest methodsFor: 'testing'!

testSimpleIfTrue
	"Simple ifTrue: should work"
	| result |
	result _ 0.
	true ifTrue: [result _ 42].
	^result!

testInlineComparison
	"This pattern fails: (expr) = value ifTrue: [...]"
	| result |
	result _ 0.
	42 = 42 ifTrue: [result _ 42].
	^result!

testMethodResultComparison
	"This pattern also fails: (self method) = value ifTrue: [...]"
	| result |
	result _ 0.
	(self getValue) = 42 ifTrue: [result _ 42].
	^result!

getValue
	"Helper method"
	^42! !

"Test 1: Simple ifTrue:"
IfTrueTest new testSimpleIfTrue!

"Test 2: Inline comparison"
IfTrueTest new testInlineComparison!

"Test 3: Method result comparison"
IfTrueTest new testMethodResultComparison!
