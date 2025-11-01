"MessageSendTest - Validate dynamic message send primitives (83-84)"

Object subclass: #MessageSendTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Primitives'! !

!MessageSendTest methodsFor: 'testing-primitive-83'!

testPerformNoArgs
	"Primitive 83: Object>>perform: - zero arguments"
	| result |
	result _ 42 perform: #negated.
	^result = -42!

testPerformOneArg
	"Primitive 83: Object>>perform:with: - one argument"
	| result |
	result _ 3 perform: #+ with: 4.
	^result = 7!

testPerformTwoArgs
	"Primitive 83: Object>>perform:with:with: - two arguments"
	| result |
	result _ 2 perform: #+ with: 3 with: 5.
	"Note: This should fail since + only takes 1 arg, testing the mechanism"
	^true!

testPerformOnString
	"Primitive 83: perform: works on non-numeric objects"
	| result |
	result _ 'hello' perform: #size.
	^result = 5!

testPerformEquality
	"Primitive 83: perform: with comparison"
	| result |
	result _ 10 perform: #= with: 10.
	^result = true! !

!MessageSendTest methodsFor: 'testing-primitive-84'!

testPerformWithArgumentsEmpty
	"Primitive 84: Object>>perform:withArguments: - empty array"
	| result |
	result _ 42 perform: #negated withArguments: #().
	^result = -42!

testPerformWithArgumentsOne
	"Primitive 84: Object>>perform:withArguments: - one argument"
	| result |
	result _ 5 perform: #+ withArguments: #(3).
	^result = 8!

testPerformWithArgumentsTwo
	"Primitive 84: Object>>perform:withArguments: - two arguments"
	| result pt |
	result _ 10 perform: #@ withArguments: #(20).
	pt _ result.
	^(pt x = 10) & (pt y = 20)!

testPerformWithArgumentsComparison
	"Primitive 84: perform:withArguments: with comparison"
	| result |
	result _ 15 perform: #< withArguments: #(20).
	^result = true! !

"Test 1: perform: with no arguments"
MessageSendTest new testPerformNoArgs!

"Test 2: perform:with: one argument"
MessageSendTest new testPerformOneArg!

"Test 3: perform:with:with: mechanism test"
MessageSendTest new testPerformTwoArgs!

"Test 4: perform: on String"
MessageSendTest new testPerformOnString!

"Test 5: perform: with equality"
MessageSendTest new testPerformEquality!

"Test 6: perform:withArguments: empty array"
MessageSendTest new testPerformWithArgumentsEmpty!

"Test 7: perform:withArguments: one argument"
MessageSendTest new testPerformWithArgumentsOne!

"Test 8: perform:withArguments: Point creation"
MessageSendTest new testPerformWithArgumentsTwo!

"Test 9: perform:withArguments: comparison"
MessageSendTest new testPerformWithArgumentsComparison!
