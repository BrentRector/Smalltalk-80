"PrimitiveTest - Comprehensive validation of all implemented primitives"

Object subclass: #PrimitiveTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Primitives'! !

!PrimitiveTest methodsFor: 'arithmetic-smallint'!

testAdd
	"Primitive 1: SmallInteger>>"
	^(5 + 3) = 8!

testSubtract
	"Primitive 2: SmallInteger>>-"
	^(10 - 4) = 6!

testLessThan
	"Primitive 3: SmallInteger>><"
	^(3 < 5) = true!

testGreaterThan
	"Primitive 4: SmallInteger>>>"
	^(7 > 2) = true!

testLessOrEqual
	"Primitive 5: SmallInteger>><="
	^(4 <= 4) = true!

testGreaterOrEqual
	"Primitive 6: SmallInteger>>="
	^(6 >= 5) = true!

testEqual
	"Primitive 7: SmallInteger>>="
	^(42 = 42) = true!

testNotEqual
	"Primitive 8: SmallInteger>>~="
	^(10 ~= 20) = true!

testMultiply
	"Primitive 9: SmallInteger>>*"
	^(6 * 7) = 42!

testDivide
	"Primitive 10: SmallInteger>>/"
	^(84 / 2) = 42!

testMod
	"Primitive 11: SmallInteger>>\\"
	^(17 \\ 5) = 2!

testQuo
	"Primitive 12: SmallInteger>>//"
	^(17 // 5) = 3!

testBitAnd
	"Primitive 13: SmallInteger>>bitAnd:"
	^(12 bitAnd: 10) = 8!

testBitOr
	"Primitive 14: SmallInteger>>bitOr:"
	^(12 bitOr: 10) = 14!

testBitXor
	"Primitive 15: SmallInteger>>bitXor:"
	^(12 bitXor: 10) = 6!

testBitShift
	"Primitive 17: SmallInteger>>bitShift:"
	^(4 bitShift: 2) = 16! !

!PrimitiveTest methodsFor: 'point'!

testMakePoint
	"Primitive 18: Number>>@"
	| p |
	p _ 10 @ 20.
	^(p x = 10) & (p y = 20)! !

!PrimitiveTest methodsFor: 'object'!

testAt
	"Primitive 60: Array>>at:"
	| arr |
	arr _ Array new: 3.
	arr at: 1 put: 42.
	^(arr at: 1) = 42!

testAtPut
	"Primitive 61: Array>>at:put:"
	| arr |
	arr _ Array new: 3.
	arr at: 2 put: 99.
	^(arr at: 2) = 99!

testSize
	"Primitive 62: Array>>size"
	| arr |
	arr _ Array new: 5.
	^arr size = 5!

testNew
	"Primitive 70: Behavior>>new"
	| obj |
	obj _ Object new.
	^obj class = Object!

testNewWithSize
	"Primitive 71: Behavior>>new:"
	| arr |
	arr _ Array new: 10.
	^arr size = 10!

testInstVarAt
	"Primitive 73: Object>>instVarAt:"
	| pt |
	pt _ 5 @ 10.
	^(pt instVarAt: 1) = 5!

testInstVarAtPut
	"Primitive 74: Object>>instVarAtPut:"
	| pt |
	pt _ 0 @ 0.
	pt instVarAt: 1 put: 42.
	^(pt instVarAt: 1) = 42!

testHash
	"Primitive 75: Object>>hash"
	| h |
	h _ 42 hash.
	^h class = SmallInteger!

testIdentityEquals
	"Primitive 110: Object>>=="
	| a b |
	a _ 42.
	b _ 42.
	^a == b!

testClass
	"Primitive 111: Object>>class"
	^42 class = SmallInteger! !

"Test 1: Add"
PrimitiveTest new testAdd!

"Test 2: Subtract"
PrimitiveTest new testSubtract!

"Test 3: LessThan"
PrimitiveTest new testLessThan!

"Test 4: GreaterThan"
PrimitiveTest new testGreaterThan!

"Test 5: LessOrEqual"
PrimitiveTest new testLessOrEqual!

"Test 6: GreaterOrEqual"
PrimitiveTest new testGreaterOrEqual!

"Test 7: Equal"
PrimitiveTest new testEqual!

"Test 8: NotEqual"
PrimitiveTest new testNotEqual!

"Test 9: Multiply"
PrimitiveTest new testMultiply!

"Test 10: Divide"
PrimitiveTest new testDivide!

"Test 11: Mod"
PrimitiveTest new testMod!

"Test 12: Quo"
PrimitiveTest new testQuo!

"Test 13: BitAnd"
PrimitiveTest new testBitAnd!

"Test 14: BitOr"
PrimitiveTest new testBitOr!

"Test 15: BitXor"
PrimitiveTest new testBitXor!

"Test 16: BitShift"
PrimitiveTest new testBitShift!

"Test 17: MakePoint"
PrimitiveTest new testMakePoint!

"Test 18: At"
PrimitiveTest new testAt!

"Test 19: AtPut"
PrimitiveTest new testAtPut!

"Test 20: Size"
PrimitiveTest new testSize!

"Test 21: New"
PrimitiveTest new testNew!

"Test 22: NewWithSize"
PrimitiveTest new testNewWithSize!

"Test 23: InstVarAt"
PrimitiveTest new testInstVarAt!

"Test 24: InstVarAtPut"
PrimitiveTest new testInstVarAtPut!

"Test 25: Hash"
PrimitiveTest new testHash!

"Test 26: IdentityEquals"
PrimitiveTest new testIdentityEquals!

"Test 27: Class"
PrimitiveTest new testClass!
