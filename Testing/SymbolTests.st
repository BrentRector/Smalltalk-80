"Comprehensive tests for Symbol class"

TestCase subclass: #SymbolTests
	instanceVariableNames: 'sym'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Symbol'!

!SymbolTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	sym _ #testSymbol! !

!SymbolTests methodsFor: 'testing-identity'!
testIdentityEquality
	"Test Symbol identity-based equality"
	| sym1 sym2 |
	sym1 _ #hello.
	sym2 _ 'hello' asSymbol.
	self assertTrue: sym1 == sym2.
	self assertTrue: sym1 = sym2!

testHashConsistency
	"Test Symbol hash consistency"
	| sym1 sym2 |
	sym1 _ #hello.
	sym2 _ 'hello' asSymbol.
	self assertEqual: sym1 hash to: sym2 hash! !

!SymbolTests methodsFor: 'testing-immutability'!
testImmutableCopy
	"Test Symbol copy returns self"
	| copy |
	copy _ sym copy.
	self assertTrue: sym == copy!

testImmutableShallowCopy
	"Test Symbol shallowCopy returns self"
	| copy |
	copy _ sym shallowCopy.
	self assertTrue: sym == copy! !

!SymbolTests methodsFor: 'testing-conversion'!
testAsString
	"Test Symbol>>#asString"
	| string |
	string _ sym asString.
	self assertEqual: 'testSymbol' to: string.
	self assertFalse: sym == string!

testAsSymbol
	"Test Symbol>>#asSymbol"
	| result |
	result _ sym asSymbol.
	self assertTrue: sym == result! !

!SymbolTests methodsFor: 'testing-parsing'!
testIsKeyword
	"Test Symbol>>#isKeyword"
	self assertTrue: #at: isKeyword.
	self assertTrue: #at:put: isKeyword.
	self assertFalse: #hello isKeyword.
	self assertFalse: #+ isKeyword!

testNumArgs
	"Test Symbol>>#numArgs"
	self assertEqual: 0 to: #hello numArgs.
	self assertEqual: 1 to: #at: numArgs.
	self assertEqual: 2 to: #at:put: numArgs.
	self assertEqual: 1 to: #+ numArgs! !

!SymbolTests methodsFor: 'testing-comparison'!
testEquality
	"Test Symbol equality"
	| sym1 sym2 sym3 |
	sym1 _ #same.
	sym2 _ #same.
	sym3 _ #different.
	
	self assertTrue: sym1 = sym2.
	self assertTrue: sym1 == sym2.
	self assertFalse: sym1 = sym3.
	self assertFalse: sym1 == sym3!

testLessThan
	"Test Symbol comparison"
	self assertTrue: #a < #b.
	self assertFalse: #b < #a.
	self assertTrue: #abc < #def! !

!SymbolTests methodsFor: 'running'!
performTest
	"Run all the symbol tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testIdentityEquality.
	self runIndividualTest: #testHashConsistency.
	self runIndividualTest: #testImmutableCopy.
	self runIndividualTest: #testImmutableShallowCopy.
	self runIndividualTest: #testAsString.
	self runIndividualTest: #testAsSymbol.
	self runIndividualTest: #testIsKeyword.
	self runIndividualTest: #testNumArgs.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testLessThan.
	self printTestResults! !
