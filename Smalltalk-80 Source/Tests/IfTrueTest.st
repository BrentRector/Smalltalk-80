"IfTrueTest - Verify ifTrue: works with all patterns (numeric values only)"

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
	"This pattern works: (expr) = value ifTrue: [...]"
	| result |
	result _ 0.
	42 = 42 ifTrue: [result _ 42].
	^result!

testMethodResultComparison
	"This pattern works: (self method) = value ifTrue: [...]"
	| result |
	result _ 0.
	(self getValue) = 42 ifTrue: [result _ 42].
	^result!

testInlineAssignmentPattern
	"The exact pattern from BlockContextTest that was failing"
	| p r |
	p _ 0.
	r _ self getValue.
	r = 42 ifTrue: [p _ p + 1].
	^p!

testMultipleIfTrue
	"Multiple ifTrue: calls in sequence"
	| p |
	p _ 0.
	(self getValue) = 42 ifTrue: [p _ p + 1].
	(self getValue) = 42 ifTrue: [p _ p + 1].
	(self getValue) = 42 ifTrue: [p _ p + 1].
	^p!

testIfTrueWithBlockResult
	"ifTrue: with block that returns value"
	| p r1 |
	p _ 0.
	r1 _ [42] value.
	r1 = 42 ifTrue: [p _ p + 1].
	^p!

getValue
	"Helper method returning number"
	^42! !

"Test 1: Simple ifTrue:"
IfTrueTest new testSimpleIfTrue!

"Test 2: Inline comparison"
IfTrueTest new testInlineComparison!

"Test 3: Method result comparison"
IfTrueTest new testMethodResultComparison!

"Test 4: Inline assignment pattern"
IfTrueTest new testInlineAssignmentPattern!

"Test 5: Multiple ifTrue: calls"
IfTrueTest new testMultipleIfTrue!

"Test 6: ifTrue: with block result"
IfTrueTest new testIfTrueWithBlockResult!
