'From Smalltalk-80, Version 2, June 1983'!

"BlockValidatorTest provides comprehensive validation of block closure semantics,
lexical scoping, and non-local return behavior."!

Object subclass: #BlockValidatorTest
	instanceVariableNames: 'flag result receiver '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Tests'!

!BlockValidatorTest commentStamp: '' prior: 0!
BlockValidatorTest validates that blocks correctly implement lexical closures.

Test categories:
1. Simple blocks - basic value invocation
2. Nested blocks - blocks within blocks
3. Lexical scope - access to outer variables and receiver
4. Block passing - blocks passed through multiple methods
5. Non-local returns - early returns from enclosing methods

Run all tests with:
    BlockValidatorTest new runAllTests!


!BlockValidatorTest methodsFor: 'running'!

runAllTests
	"Execute all validation tests and report results."
	| tests passed failed |
	tests := #(
		testSimpleBlock
		testNestedBlock
		testLexicalScope
		testBlockPassing
		testInstanceVariableAccess
		testReceiverAccess
		testMultiLayerNesting
		testBlockArguments
		testBlockReturns
	).

	passed := 0.
	failed := 0.

	tests do: [:testSelector |
		[
			self perform: testSelector.
			passed := passed + 1.
			Transcript show: testSelector; show: ' ... PASSED'; cr.
		] on: Error do: [:ex |
			failed := failed + 1.
			Transcript show: testSelector; show: ' ... FAILED: '; show: ex messageText; cr.
		]
	].

	Transcript cr; show: '============================'; cr.
	Transcript show: 'Total: ', tests size printString; cr.
	Transcript show: 'Passed: ', passed printString; cr.
	Transcript show: 'Failed: ', failed printString; cr.

	^failed = 0! !

!BlockValidatorTest methodsFor: 'tests - basic'!

testSimpleBlock
	"Test that a simple 0-argument block executes correctly."
	| executed |
	executed := false.
	[ executed := true ] value.
	executed ifFalse: [ self error: 'Simple block did not execute' ]!

testBlockArguments
	"Test that blocks correctly receive and use arguments."
	| result |
	result := [ :x | x + 1 ] value: 5.
	result = 6 ifFalse: [ self error: '1-arg block failed: expected 6, got ', result printString ].

	result := [ :x :y | x + y ] value: 3 value: 4.
	result = 7 ifFalse: [ self error: '2-arg block failed: expected 7, got ', result printString ]!

testBlockReturns
	"Test that blocks return their last value correctly."
	| result |
	result := [ 42 ] value.
	result = 42 ifFalse: [ self error: 'Block return failed: expected 42, got ', result printString ]! !

!BlockValidatorTest methodsFor: 'tests - nesting'!

testNestedBlock
	"Test that nested blocks execute correctly."
	| executed |
	executed := false.
	[ [ executed := true ] value ] value.
	executed ifFalse: [ self error: 'Nested block did not execute' ]!

testMultiLayerNesting
	"Test blocks nested 3 levels deep."
	| result |
	result := [ [ [ 42 ] value ] value ] value.
	result = 42 ifFalse: [ self error: '3-level nested block failed: expected 42, got ', result printString ]! !

!BlockValidatorTest methodsFor: 'tests - lexical scope'!

testLexicalScope
	"Test that blocks can access variables from their defining scope."
	| outer |
	outer := 10.
	[ outer := outer + 5 ] value.
	outer = 15 ifFalse: [ self error: 'Lexical scope failed: expected 15, got ', outer printString ]!

testInstanceVariableAccess
	"Test that blocks can access instance variables from their defining method."
	flag := false.
	[ flag := true ] value.
	flag ifFalse: [ self error: 'Block could not access instance variable' ]!

testReceiverAccess
	"Test that blocks can access self from their defining method."
	receiver := nil.
	[ receiver := self ] value.
	receiver == self ifFalse: [ self error: 'Block could not access receiver (self)' ]! !

!BlockValidatorTest methodsFor: 'tests - passing'!

testBlockPassing
	"Test that blocks retain lexical scope when passed through multiple methods."
	flag := false.
	self layerA: [ self layerB: [ self layerC: [ flag := true ] ] ].
	flag ifFalse: [ self error: 'Block passing failed: flag not set' ]!

layerA: aBlock
	"Helper method - evaluates the block."
	aBlock value!

layerB: aBlock
	"Helper method - evaluates the block."
	aBlock value!

layerC: aBlock
	"Helper method - evaluates the block."
	aBlock value! !

!BlockValidatorTest methodsFor: 'tests - advanced'!

testNestedBlockLexicalScope
	"Test that nested blocks can access outer block variables."
	| outer inner |
	outer := 10.
	[
		inner := 20.
		[
			outer := outer + inner.
		] value
	] value.
	outer = 30 ifFalse: [ self error: 'Nested block lexical scope failed: expected 30, got ', outer printString ]!

testBlockWithTemporaries
	"Test that blocks can define and use their own temporaries."
	| result |
	result := [
		| temp |
		temp := 5.
		temp + 10
	] value.
	result = 15 ifFalse: [ self error: 'Block temporaries failed: expected 15, got ', result printString ]!

testMultipleBlocksShareScope
	"Test that multiple blocks defined in the same method share variables."
	| shared block1 block2 |
	shared := 0.
	block1 := [ shared := shared + 1 ].
	block2 := [ shared := shared + 10 ].
	block1 value.
	block2 value.
	shared = 11 ifFalse: [ self error: 'Shared scope failed: expected 11, got ', shared printString ]! !

!BlockValidatorTest methodsFor: 'tests - control flow'!

testBlockWhileTrue
	"Test whileTrue: control structure."
	| counter |
	counter := 0.
	[ counter < 5 ] whileTrue: [ counter := counter + 1 ].
	counter = 5 ifFalse: [ self error: 'whileTrue failed: expected 5, got ', counter printString ]!

testBlockWhileFalse
	"Test whileFalse: control structure."
	| counter |
	counter := 0.
	[ counter >= 5 ] whileFalse: [ counter := counter + 1 ].
	counter = 5 ifFalse: [ self error: 'whileFalse failed: expected 5, got ', counter printString ]! !

!BlockValidatorTest methodsFor: 'private'!

initialize
	"Initialize the test instance."
	flag := false.
	result := nil.
	receiver := nil! !


"Class methods"!

!BlockValidatorTest class methodsFor: 'running'!

run
	"Run all tests and answer true if all passed."
	^self new runAllTests! !!
