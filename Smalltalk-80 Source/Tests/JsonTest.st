Object subclass: #JsonTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

JsonTest comment:
'Test cases for JsonReader and JsonWriter classes.'!


!JsonTest class methodsFor: 'testing'!

runAll
	"Run all JSON tests. Returns count of passed tests."
	| passed |
	passed _ 0.

	"JsonReader tests"
	passed _ passed + self testParseInteger.
	passed _ passed + self testParseNegativeInteger.
	passed _ passed + self testParseFloat.
	passed _ passed + self testParseExponent.
	passed _ passed + self testParseString.
	passed _ passed + self testParseStringEscapes.
	passed _ passed + self testParseUnicode.
	passed _ passed + self testParseTrue.
	passed _ passed + self testParseFalse.
	passed _ passed + self testParseNull.
	passed _ passed + self testParseEmptyArray.
	passed _ passed + self testParseArray.
	passed _ passed + self testParseNestedArray.
	passed _ passed + self testParseEmptyObject.
	passed _ passed + self testParseObject.
	passed _ passed + self testParseNestedObject.
	passed _ passed + self testParseWhitespace.

	"JsonWriter tests"
	passed _ passed + self testWriteInteger.
	passed _ passed + self testWriteFloat.
	passed _ passed + self testWriteString.
	passed _ passed + self testWriteStringEscapes.
	passed _ passed + self testWriteTrue.
	passed _ passed + self testWriteFalse.
	passed _ passed + self testWriteNull.
	passed _ passed + self testWriteArray.
	passed _ passed + self testWriteObject.

	"Round-trip tests"
	passed _ passed + self testRoundTripObject.
	passed _ passed + self testRoundTripArray.
	passed _ passed + self testRoundTripNested.

	Transcript show: 'JSON Tests: ', passed printString, ' passed'; cr.
	^passed!

"=== JsonReader Tests ==="

testParseInteger
	(JsonReader parse: '42') = 42 ifFalse: [Transcript show: 'FAIL: testParseInteger'; cr. ^0].
	^1!

testParseNegativeInteger
	(JsonReader parse: '-17') = -17 ifFalse: [Transcript show: 'FAIL: testParseNegativeInteger'; cr. ^0].
	^1!

testParseFloat
	(JsonReader parse: '3.14') = 3.14 ifFalse: [Transcript show: 'FAIL: testParseFloat'; cr. ^0].
	^1!

testParseExponent
	| result |
	result _ JsonReader parse: '1.5e10'.
	(result closeTo: 1.5e10) ifFalse: [Transcript show: 'FAIL: testParseExponent'; cr. ^0].
	^1!

testParseString
	(JsonReader parse: '"hello"') = 'hello' ifFalse: [Transcript show: 'FAIL: testParseString'; cr. ^0].
	^1!

testParseStringEscapes
	| result expected |
	result _ JsonReader parse: '"line1\nline2"'.
	expected _ 'line1', (String with: Character lf), 'line2'.
	result = expected ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (newline)'; cr. ^0].

	result _ JsonReader parse: '"tab\there"'.
	expected _ 'tab', (String with: Character tab), 'here'.
	result = expected ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (tab)'; cr. ^0].

	result _ JsonReader parse: '"quote\"here"'.
	result = 'quote"here' ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (quote)'; cr. ^0].

	result _ JsonReader parse: '"back\\slash"'.
	result = 'back\slash' ifFalse: [Transcript show: 'FAIL: testParseStringEscapes (backslash)'; cr. ^0].
	^1!

testParseUnicode
	| result |
	result _ JsonReader parse: '"\u0041"'.
	result = 'A' ifFalse: [Transcript show: 'FAIL: testParseUnicode (A)'; cr. ^0].

	result _ JsonReader parse: '"\u03B1"'.
	result = (String with: (Character value: 16r03B1)) ifFalse: [Transcript show: 'FAIL: testParseUnicode (alpha)'; cr. ^0].
	^1!

testParseTrue
	(JsonReader parse: 'true') == true ifFalse: [Transcript show: 'FAIL: testParseTrue'; cr. ^0].
	^1!

testParseFalse
	(JsonReader parse: 'false') == false ifFalse: [Transcript show: 'FAIL: testParseFalse'; cr. ^0].
	^1!

testParseNull
	(JsonReader parse: 'null') isNil ifFalse: [Transcript show: 'FAIL: testParseNull'; cr. ^0].
	^1!

testParseEmptyArray
	| result |
	result _ JsonReader parse: '[]'.
	(result isKindOf: Array) ifFalse: [Transcript show: 'FAIL: testParseEmptyArray (not array)'; cr. ^0].
	result isEmpty ifFalse: [Transcript show: 'FAIL: testParseEmptyArray (not empty)'; cr. ^0].
	^1!

testParseArray
	| result |
	result _ JsonReader parse: '[1, 2, 3]'.
	(result size = 3 and: [result first = 1 and: [result last = 3]])
		ifFalse: [Transcript show: 'FAIL: testParseArray'; cr. ^0].
	^1!

testParseNestedArray
	| result |
	result _ JsonReader parse: '[[1, 2], [3, 4]]'.
	((result first first = 1) and: [(result second second) = 4])
		ifFalse: [Transcript show: 'FAIL: testParseNestedArray'; cr. ^0].
	^1!

testParseEmptyObject
	| result |
	result _ JsonReader parse: '{}'.
	(result isKindOf: Dictionary) ifFalse: [Transcript show: 'FAIL: testParseEmptyObject (not dict)'; cr. ^0].
	result isEmpty ifFalse: [Transcript show: 'FAIL: testParseEmptyObject (not empty)'; cr. ^0].
	^1!

testParseObject
	| result |
	result _ JsonReader parse: '{"name": "Alice", "age": 30}'.
	((result at: 'name') = 'Alice' and: [(result at: 'age') = 30])
		ifFalse: [Transcript show: 'FAIL: testParseObject'; cr. ^0].
	^1!

testParseNestedObject
	| result |
	result _ JsonReader parse: '{"user": {"name": "Bob"}, "count": 5}'.
	(((result at: 'user') at: 'name') = 'Bob' and: [(result at: 'count') = 5])
		ifFalse: [Transcript show: 'FAIL: testParseNestedObject'; cr. ^0].
	^1!

testParseWhitespace
	| result |
	result _ JsonReader parse: '  {  "key"  :  "value"  }  '.
	(result at: 'key') = 'value' ifFalse: [Transcript show: 'FAIL: testParseWhitespace'; cr. ^0].
	^1!

"=== JsonWriter Tests ==="

testWriteInteger
	(JsonWriter stringify: 42) = '42' ifFalse: [Transcript show: 'FAIL: testWriteInteger'; cr. ^0].
	^1!

testWriteFloat
	| result |
	result _ JsonWriter stringify: 3.14.
	(result includesSubstring: '3.14') ifFalse: [Transcript show: 'FAIL: testWriteFloat'; cr. ^0].
	^1!

testWriteString
	(JsonWriter stringify: 'hello') = '"hello"' ifFalse: [Transcript show: 'FAIL: testWriteString'; cr. ^0].
	^1!

testWriteStringEscapes
	| result |
	result _ JsonWriter stringify: 'line1', (String with: Character lf), 'line2'.
	result = '"line1\nline2"' ifFalse: [Transcript show: 'FAIL: testWriteStringEscapes'; cr. ^0].
	^1!

testWriteTrue
	(JsonWriter stringify: true) = 'true' ifFalse: [Transcript show: 'FAIL: testWriteTrue'; cr. ^0].
	^1!

testWriteFalse
	(JsonWriter stringify: false) = 'false' ifFalse: [Transcript show: 'FAIL: testWriteFalse'; cr. ^0].
	^1!

testWriteNull
	(JsonWriter stringify: nil) = 'null' ifFalse: [Transcript show: 'FAIL: testWriteNull'; cr. ^0].
	^1!

testWriteArray
	(JsonWriter stringify: #(1 2 3)) = '[1,2,3]' ifFalse: [Transcript show: 'FAIL: testWriteArray'; cr. ^0].
	^1!

testWriteObject
	| dict result |
	dict _ Dictionary new at: 'key' put: 'value'; yourself.
	result _ JsonWriter stringify: dict.
	(result includesSubstring: '"key"') ifFalse: [Transcript show: 'FAIL: testWriteObject (key)'; cr. ^0].
	(result includesSubstring: '"value"') ifFalse: [Transcript show: 'FAIL: testWriteObject (value)'; cr. ^0].
	^1!

"=== Round-Trip Tests ==="

testRoundTripObject
	| original parsed |
	original _ Dictionary new at: 'name' put: 'Alice'; at: 'age' put: 30; yourself.
	parsed _ JsonReader parse: (JsonWriter stringify: original).
	((parsed at: 'name') = 'Alice' and: [(parsed at: 'age') = 30])
		ifFalse: [Transcript show: 'FAIL: testRoundTripObject'; cr. ^0].
	^1!

testRoundTripArray
	| original parsed |
	original _ #(1 2 3 4 5).
	parsed _ JsonReader parse: (JsonWriter stringify: original).
	(parsed = original) ifFalse: [Transcript show: 'FAIL: testRoundTripArray'; cr. ^0].
	^1!

testRoundTripNested
	| original parsed users |
	users _ Array
		with: (Dictionary new at: 'name' put: 'Alice'; yourself)
		with: (Dictionary new at: 'name' put: 'Bob'; yourself).
	original _ Dictionary new at: 'users' put: users; yourself.
	parsed _ JsonReader parse: (JsonWriter stringify: original).
	(((parsed at: 'users') first at: 'name') = 'Alice')
		ifFalse: [Transcript show: 'FAIL: testRoundTripNested'; cr. ^0].
	^1! !
