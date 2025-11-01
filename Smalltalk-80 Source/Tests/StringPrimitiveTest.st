"StringPrimitiveTest - Validate String/ByteArray access primitives (63-64)"

Object subclass: #StringPrimitiveTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Primitives'! !

!StringPrimitiveTest methodsFor: 'testing-primitive-63'!

testBasicAtFirstChar
	"Primitive 63: String>>basicAt: - access first character"
	| str char |
	str _ 'hello'.
	char _ str basicAt: 1.
	^char = $h!

testBasicAtLastChar
	"Primitive 63: String>>basicAt: - access last character"
	| str char |
	str _ 'world'.
	char _ str basicAt: 5.
	^char = $d!

testBasicAtMiddleChar
	"Primitive 63: String>>basicAt: - access middle character"
	| str char |
	str _ 'Smalltalk'.
	char _ str basicAt: 6.
	^char = $t!

testBasicAtSingleChar
	"Primitive 63: String>>basicAt: - single character string"
	| str char |
	str _ 'X'.
	char _ str basicAt: 1.
	^char = $X! !

!StringPrimitiveTest methodsFor: 'testing-primitive-64'!

testBasicAtPutFirstChar
	"Primitive 64: String>>basicAt:put: - modify first character"
	| str |
	str _ 'hello' copy.
	str basicAt: 1 put: $H.
	^(str basicAt: 1) = $H!

testBasicAtPutLastChar
	"Primitive 64: String>>basicAt:put: - modify last character"
	| str |
	str _ 'world' copy.
	str basicAt: 5 put: $D.
	^(str basicAt: 5) = $D!

testBasicAtPutMiddleChar
	"Primitive 64: String>>basicAt:put: - modify middle character"
	| str |
	str _ 'Smalltalk' copy.
	str basicAt: 6 put: $T.
	^(str basicAt: 6) = $T!

testBasicAtPutResult
	"Primitive 64: String>>basicAt:put: - returns the character"
	| str result |
	str _ 'test' copy.
	result _ str basicAt: 1 put: $X.
	^result = $X! !

"Test 1: basicAt: first character"
StringPrimitiveTest new testBasicAtFirstChar!

"Test 2: basicAt: last character"
StringPrimitiveTest new testBasicAtLastChar!

"Test 3: basicAt: middle character"
StringPrimitiveTest new testBasicAtMiddleChar!

"Test 4: basicAt: single character"
StringPrimitiveTest new testBasicAtSingleChar!

"Test 5: basicAt:put: first character"
StringPrimitiveTest new testBasicAtPutFirstChar!

"Test 6: basicAt:put: last character"
StringPrimitiveTest new testBasicAtPutLastChar!

"Test 7: basicAt:put: middle character"
StringPrimitiveTest new testBasicAtPutMiddleChar!

"Test 8: basicAt:put: returns character"
StringPrimitiveTest new testBasicAtPutResult!
