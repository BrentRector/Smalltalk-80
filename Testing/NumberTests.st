"Comprehensive tests for Number and Integer classes"

TestCase subclass: #NumberTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Number'!

!NumberTests methodsFor: 'testing-arithmetic'!
testNegated
	"Test Number>>negated"
	self assertEqual: -5 to: 5 negated.
	self assertEqual: 5 to: -5 negated.
	self assertEqual: 0 to: 0 negated! !

!NumberTests methodsFor: 'testing-integer-specific'!
testIsInteger
	"Test Integer>>isInteger override"
	self assertTrue: 42 isInteger.
	self assertTrue: 0 isInteger.
	self assertTrue: -17 isInteger!

testEven
	"Test Integer>>even"
	self assertTrue: 0 even.
	self assertTrue: 2 even.
	self assertTrue: 4 even.
	self assertTrue: -2 even.
	self assertFalse: 1 even.
	self assertFalse: 3 even.
	self assertFalse: -1 even.
	self assertFalse: -3 even! !

!NumberTests methodsFor: 'running'!
performTest
	"Run all the number tests"
	self resetTestCounts.
	self runIndividualTest: #testNegated.
	self runIndividualTest: #testIsInteger.
	self runIndividualTest: #testEven.
	self printTestResults! !

TestCase subclass: #SmallIntegerTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-SmallInteger'!

!SmallIntegerTests methodsFor: 'testing-arithmetic'!
testAddition
	"Test SmallInteger>>+"
	self assertEqual: 5 to: 2 + 3.
	self assertEqual: 0 to: 5 + -5.
	self assertEqual: 10 to: 7 + 3!

testSubtraction
	"Test SmallInteger>>-"
	self assertEqual: 2 to: 5 - 3.
	self assertEqual: 10 to: 5 - -5.
	self assertEqual: 0 to: 7 - 7!

testMultiplication
	"Test SmallInteger>>*"
	self assertEqual: 12 to: 3 * 4.
	self assertEqual: 0 to: 5 * 0.
	self assertEqual: -15 to: 3 * -5!

testDivision
	"Test SmallInteger>>/"
	self assertEqual: 3 to: 12 / 4.
	self assertEqual: 2 to: 10 / 5!

testIntegerDivision
	"Test SmallInteger>>//"
	self assertEqual: 3 to: 10 // 3.
	self assertEqual: 2 to: 9 // 4!

testModulo
	"Test SmallInteger>>\\\"
	self assertEqual: 1 to: 10 \\ 3.
	self assertEqual: 0 to: 12 \\ 4.
	self assertEqual: 2 to: 17 \\ 5! !

!SmallIntegerTests methodsFor: 'testing-comparison'!
testLessThan
	"Test SmallInteger>><"
	self assertTrue: 3 < 5.
	self assertFalse: 5 < 3.
	self assertFalse: 5 < 5!

testLessThanOrEqual
	"Test SmallInteger>>="
	self assertTrue: 3 <= 5.
	self assertTrue: 5 <= 5.
	self assertFalse: 7 <= 5!

testGreaterThan
	"Test SmallInteger>>>"
	self assertTrue: 5 > 3.
	self assertFalse: 3 > 5.
	self assertFalse: 5 > 5!

testGreaterThanOrEqual
	"Test SmallInteger>>="
	self assertTrue: 5 >= 3.
	self assertTrue: 5 >= 5.
	self assertFalse: 3 >= 5!

testEquality
	"Test SmallInteger>>="
	self assertTrue: 5 = 5.
	self assertFalse: 5 = 3.
	self assertFalse: 5 ~= 5.
	self assertTrue: 5 ~= 3! !

!SmallIntegerTests methodsFor: 'testing-bit-manipulation'!
testBitAnd
	"Test SmallInteger>>bitAnd:"
	self assertEqual: 4 to: (12 bitAnd: 6).
	self assertEqual: 0 to: (5 bitAnd: 10).
	self assertEqual: 15 to: (15 bitAnd: 15)!

testBitOr
	"Test SmallInteger>>bitOr:"
	self assertEqual: 14 to: (12 bitOr: 6).
	self assertEqual: 15 to: (5 bitOr: 10).
	self assertEqual: 15 to: (15 bitOr: 15)!

testBitXor
	"Test SmallInteger>>bitXor:"
	self assertEqual: 10 to: (12 bitXor: 6).
	self assertEqual: 15 to: (5 bitXor: 10).
	self assertEqual: 0 to: (15 bitXor: 15)!

testBitShift
	"Test SmallInteger>>bitShift:"
	self assertEqual: 8 to: (2 bitShift: 2).
	self assertEqual: 3 to: (12 bitShift: -2).
	self assertEqual: 0 to: (1 bitShift: -1)! !

!SmallIntegerTests methodsFor: 'testing-testing'!
testEven
	"Test SmallInteger>>even"
	self assertTrue: 0 even.
	self assertTrue: 2 even.
	self assertTrue: 4 even.
	self assertFalse: 1 even.
	self assertFalse: 3 even!

testOdd
	"Test SmallInteger>>odd"
	self assertFalse: 0 odd.
	self assertFalse: 2 odd.
	self assertFalse: 4 odd.
	self assertTrue: 1 odd.
	self assertTrue: 3 odd!

testPositive
	"Test SmallInteger>>positive"
	self assertTrue: 1 positive.
	self assertTrue: 42 positive.
	self assertFalse: 0 positive.
	self assertFalse: -1 positive!

testNegative
	"Test SmallInteger>>negative"
	self assertTrue: -1 negative.
	self assertTrue: -42 negative.
	self assertFalse: 0 negative.
	self assertFalse: 1 negative!

testStrictlyPositive
	"Test SmallInteger>>strictlyPositive"
	self assertTrue: 1 strictlyPositive.
	self assertTrue: 42 strictlyPositive.
	self assertFalse: 0 strictlyPositive.
	self assertFalse: -1 strictlyPositive!

testSign
	"Test SmallInteger>>sign"
	self assertEqual: 1 to: 42 sign.
	self assertEqual: -1 to: -42 sign.
	self assertEqual: 0 to: 0 sign! !

!SmallIntegerTests methodsFor: 'testing-converting'!
testAsString
	"Test SmallInteger>>asString"
	self assertEqual: '42' to: 42 asString.
	self assertEqual: '0' to: 0 asString.
	self assertEqual: '-17' to: -17 asString!

testPrintString
	"Test SmallInteger>>printString"
	self assertEqual: '42' to: 42 printString.
	self assertEqual: '0' to: 0 printString.
	self assertEqual: '-17' to: -17 printString! !

!SmallIntegerTests methodsFor: 'testing-system'!
testHash
	"Test SmallInteger>>hash"
	self assertEqual: 42 to: 42 hash.
	self assertEqual: 0 to: 0 hash.
	self assertEqual: -5 to: -5 hash! !

!SmallIntegerTests methodsFor: 'running'!
performTest
	"Run all the SmallInteger tests"
	self resetTestCounts.
	self runIndividualTest: #testAddition.
	self runIndividualTest: #testSubtraction.
	self runIndividualTest: #testMultiplication.
	self runIndividualTest: #testDivision.
	self runIndividualTest: #testIntegerDivision.
	self runIndividualTest: #testModulo.
	self runIndividualTest: #testLessThan.
	self runIndividualTest: #testLessThanOrEqual.
	self runIndividualTest: #testGreaterThan.
	self runIndividualTest: #testGreaterThanOrEqual.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testBitAnd.
	self runIndividualTest: #testBitOr.
	self runIndividualTest: #testBitXor.
	self runIndividualTest: #testBitShift.
	self runIndividualTest: #testEven.
	self runIndividualTest: #testOdd.
	self runIndividualTest: #testPositive.
	self runIndividualTest: #testNegative.
	self runIndividualTest: #testStrictlyPositive.
	self runIndividualTest: #testSign.
	self runIndividualTest: #testAsString.
	self runIndividualTest: #testPrintString.
	self runIndividualTest: #testHash.
	self printTestResults! !
