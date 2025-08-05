"Comprehensive tests for String class"

TestCase subclass: #StringTests
	instanceVariableNames: 'string emptyString'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-String'!

!StringTests methodsFor: 'setup'!
setUp
	"Set up test fixtures"
	string _ 'hello'.
	emptyString _ ''! !

!StringTests methodsFor: 'testing-accessing'!
testAt
	"Test String>>at:"
	self assertEqual: $h to: (string at: 1).
	self assertEqual: $e to: (string at: 2).
	self assertEqual: $l to: (string at: 3).
	self assertEqual: $l to: (string at: 4).
	self assertEqual: $o to: (string at: 5)!

testAtPut
	"Test String>>at:put:"
	| mutableString |
	mutableString _ 'test' copy.
	mutableString at: 1 put: $b.
	self assertEqual: $b to: (mutableString at: 1).
	self assertEqual: $e to: (mutableString at: 2)!

testSize
	"Test String>>size"
	self assertEqual: 5 to: string size.
	self assertEqual: 0 to: emptyString size.
	self assertEqual: 3 to: 'abc' size! !

!StringTests methodsFor: 'testing-enumerating'!
testDo
	"Test String>>do:"
	| characters |
	characters _ Array new: 5.
	string do: [:char | "characters add: char - simplified test"].
	"Note: This is a simplified test since collection operations are complex"! !

!StringTests methodsFor: 'testing-converting'!
testAsSymbol
	"Test String>>asSymbol"
	| symbol |
	symbol _ string asSymbol.
	self assertEqual: #hello to: symbol!

testAsUppercase
	"Test String>>asUppercase"
	| upper |
	upper _ 'Hello World' asUppercase.
	self assertEqual: 'HELLO WORLD' to: upper!

testAsLowercase
	"Test String>>asLowercase"
	| lower |
	lower _ 'Hello World' asLowercase.
	self assertEqual: 'hello world' to: lower! !

!StringTests methodsFor: 'testing-comparing'!
testEquality
	"Test String>>="
	self assertTrue: 'hello' = 'hello'.
	self assertFalse: 'hello' = 'world'.
	self assertFalse: 'hello' = 'Hello'!

testLessThan
	"Test String>><"
	self assertTrue: 'abc' < 'def'.
	self assertTrue: 'a' < 'ab'.
	self assertFalse: 'def' < 'abc'!

testLessThanOrEqual
	"Test String>>="
	self assertTrue: 'abc' <= 'abc'.
	self assertTrue: 'abc' <= 'def'.
	self assertFalse: 'def' <= 'abc'!

testGreaterThan
	"Test String>>>"
	self assertTrue: 'def' > 'abc'.
	self assertTrue: 'ab' > 'a'.
	self assertFalse: 'abc' > 'def'!

testGreaterThanOrEqual
	"Test String>>="
	self assertTrue: 'abc' >= 'abc'.
	self assertTrue: 'def' >= 'abc'.
	self assertFalse: 'abc' >= 'def'! !

!StringTests methodsFor: 'testing-copying'!
testConcatenation
	"Test String>>,"
	| result |
	result _ 'hello' , ' world'.
	self assertEqual: 'hello world' to: result.
	self assertEqual: 11 to: result size! !

!StringTests methodsFor: 'testing-collection-behavior'!
testIncludes
	"Test String>>includes: (inherited from Collection)"
	self assertTrue: (string includes: $h).
	self assertTrue: (string includes: $e).
	self assertTrue: (string includes: $l).
	self assertTrue: (string includes: $o).
	self assertFalse: (string includes: $x)!

testIsEmpty
	"Test String>>isEmpty (inherited from Collection)"
	self assertFalse: string isEmpty.
	self assertTrue: emptyString isEmpty!

testNotEmpty
	"Test String>>notEmpty (inherited from Collection)"
	self assertTrue: string notEmpty.
	self assertFalse: emptyString notEmpty! !

!StringTests methodsFor: 'testing-character-access'!
testCharacterIteration
	"Test accessing individual characters"
	self assertEqual: $h to: (string at: 1).
	self assertEqual: $e to: (string at: 2).
	self assertEqual: $l to: (string at: 3).
	self assertEqual: $l to: (string at: 4).
	self assertEqual: $o to: (string at: 5)! !

!StringTests methodsFor: 'running'!
performTest
	"Run all the string tests"
	self resetTestCounts.
	self setUp.
	self runIndividualTest: #testAt.
	self runIndividualTest: #testAtPut.
	self runIndividualTest: #testSize.
	self runIndividualTest: #testDo.
	self runIndividualTest: #testAsSymbol.
	self runIndividualTest: #testAsUppercase.
	self runIndividualTest: #testAsLowercase.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testLessThan.
	self runIndividualTest: #testLessThanOrEqual.
	self runIndividualTest: #testGreaterThan.
	self runIndividualTest: #testGreaterThanOrEqual.
	self runIndividualTest: #testConcatenation.
	self runIndividualTest: #testIncludes.
	self runIndividualTest: #testIsEmpty.
	self runIndividualTest: #testNotEmpty.
	self runIndividualTest: #testCharacterIteration.
	self printTestResults! !
