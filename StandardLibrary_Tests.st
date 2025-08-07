"StandardLibrary_Tests.st - Comprehensive Test Suite for All StandardLibrary Classes"
"Generated August 5, 2025 - Tests for 34 classes with 177 methods"

TestCase subclass: #ObjectTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!ObjectTests methodsFor: 'testing-identity'!
testEquality
	"Test Object>>#= and Object>>#~="
	| obj1 obj2 |
	obj1 _ Object new.
	obj2 _ Object new.
	self assertTrue: obj1 = obj1.
	self assertFalse: obj1 = obj2.
	self assertFalse: obj1 ~= obj1.
	self assertTrue: obj1 ~= obj2!

testIdentity
	"Test Object>>#== and Object>>#~~"
	| obj1 obj2 |
	obj1 _ Object new.
	obj2 _ Object new.
	self assertTrue: obj1 == obj1.
	self assertFalse: obj1 == obj2.
	self assertFalse: obj1 ~~ obj1.
	self assertTrue: obj1 ~~ obj2!

testHash
	"Test Object>>#hash"
	| obj |
	obj _ Object new.
	self assertNotNil: obj hash.
	self assertEqual: obj hash to: obj hash! !

!ObjectTests methodsFor: 'testing-introspection'!
testClass
	"Test Object>>#class"
	| obj |
	obj _ Object new.
	self assertEqual: obj class to: Object!

testIsMemberOf
	"Test Object>>#isMemberOf:"
	| obj |
	obj _ Object new.
	self assertTrue: (obj isMemberOf: Object).
	self assertFalse: (obj isMemberOf: Boolean)!

testIsKindOf
	"Test Object>>#isKindOf:"
	| obj |
	obj _ Object new.
	self assertTrue: (obj isKindOf: Object).
	self assertFalse: (obj isKindOf: Boolean)!

testRespondsTo
	"Test Object>>#respondsTo:"
	| obj |
	obj _ Object new.
	self assertTrue: (obj respondsTo: #class).
	self assertTrue: (obj respondsTo: #yourself).
	self assertFalse: (obj respondsTo: #nonExistentMethod)! !

!ObjectTests methodsFor: 'testing-copying'!
testCopy
	"Test Object>>#copy"
	| obj copy |
	obj _ Object new.
	copy _ obj copy.
	self assertFalse: obj == copy.
	self assertEqual: obj class to: copy class!

testShallowCopy
	"Test Object>>#shallowCopy"
	| obj copy |
	obj _ Object new.
	copy _ obj shallowCopy.
	self assertFalse: obj == copy.
	self assertEqual: obj class to: copy class! !

!ObjectTests methodsFor: 'testing-primitives'!
testBasicSize
	"Test Object>>#basicSize"
	| obj |
	obj _ Object new.
	self assertEqual: obj basicSize to: 0!

testSize
	"Test Object>>#size"
	| obj |
	obj _ Object new.
	self assertEqual: obj size to: 0!

testYourself
	"Test Object>>#yourself"
	| obj |
	obj _ Object new.
	self assertTrue: obj yourself == obj! !

!ObjectTests methodsFor: 'testing-types'!
testIsInteger
	"Test Object>>#isInteger"
	| obj |
	obj _ Object new.
	self assertFalse: obj isInteger!

testIsNil
	"Test Object>>#isNil"
	| obj |
	obj _ Object new.
	self assertFalse: obj isNil!

testNotNil
	"Test Object>>#notNil"
	| obj |
	obj _ Object new.
	self assertTrue: obj notNil! !

!ObjectTests methodsFor: 'running'!
performTest
	"Run all Object tests"
	self resetTestCounts.
	self testEquality.
	self testIdentity.
	self testHash.
	self testClass.
	self testIsMemberOf.
	self testIsKindOf.
	self testRespondsTo.
	self testCopy.
	self testShallowCopy.
	self testBasicSize.
	self testSize.
	self testYourself.
	self testIsInteger.
	self testIsNil.
	self testNotNil! !

TestCase subclass: #UndefinedObjectTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!UndefinedObjectTests methodsFor: 'testing'!
testIsNil
	"Test UndefinedObject>>#isNil"
	self assertTrue: nil isNil!

testNotNil
	"Test UndefinedObject>>#notNil"
	self assertFalse: nil notNil!

testPrintString
	"Test UndefinedObject>>#printString"
	self assertEqual: nil printString to: 'nil'! !

!UndefinedObjectTests methodsFor: 'running'!
performTest
	"Run all UndefinedObject tests"
	self resetTestCounts.
	self testIsNil.
	self testNotNil.
	self testPrintString! !

TestCase subclass: #SmallIntegerTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Numbers'!

!SmallIntegerTests methodsFor: 'testing-arithmetic'!
testAddition
	"Test SmallInteger>>+"
	self assertEqual: 2 + 3 to: 5.
	self assertEqual: 0 + 5 to: 5.
	self assertEqual: (-3) + 7 to: 4!

testSubtraction
	"Test SmallInteger>>-"
	self assertEqual: 5 - 3 to: 2.
	self assertEqual: 0 - 5 to: -5.
	self assertEqual: 10 - 10 to: 0!

testMultiplication
	"Test SmallInteger>>*"
	self assertEqual: 3 * 4 to: 12.
	self assertEqual: 0 * 5 to: 0.
	self assertEqual: (-2) * 3 to: -6!

testDivision
	"Test SmallInteger>>/"
	self assertEqual: 12 / 4 to: 3.
	self assertEqual: 15 / 3 to: 5!

testIntegerDivision
	"Test SmallInteger>>//"
	self assertEqual: 15 // 4 to: 3.
	self assertEqual: 20 // 5 to: 4!

testModulo
	"Test SmallInteger>>\\"
	self assertEqual: 15 \\ 4 to: 3.
	self assertEqual: 20 \\ 6 to: 2! !

!SmallIntegerTests methodsFor: 'testing-comparison'!
testLessThan
	"Test SmallInteger>><"
	self assertTrue: 3 < 5.
	self assertFalse: 5 < 3.
	self assertFalse: 5 < 5!

testLessEqual
	"Test SmallInteger>><="
	self assertTrue: 3 <= 5.
	self assertTrue: 5 <= 5.
	self assertFalse: 7 <= 5!

testGreaterThan
	"Test SmallInteger>>>"
	self assertTrue: 5 > 3.
	self assertFalse: 3 > 5.
	self assertFalse: 5 > 5!

testGreaterEqual
	"Test SmallInteger>>>=)"
	self assertTrue: 5 >= 3.
	self assertTrue: 5 >= 5.
	self assertFalse: 3 >= 5!

testEquality
	"Test SmallInteger>>#="
	self assertTrue: 5 = 5.
	self assertFalse: 5 = 3!

testInequality
	"Test SmallInteger>>#~="
	self assertFalse: 5 ~= 5.
	self assertTrue: 5 ~= 3! !

!SmallIntegerTests methodsFor: 'testing-bit-operations'!
testBitAnd
	"Test SmallInteger>>#bitAnd:"
	self assertEqual: (12 bitAnd: 10) to: 8.
	self assertEqual: (15 bitAnd: 7) to: 7!

testBitOr
	"Test SmallInteger>>#bitOr:"
	self assertEqual: (12 bitOr: 10) to: 14.
	self assertEqual: (8 bitOr: 4) to: 12!

testBitXor
	"Test SmallInteger>>#bitXor:"
	self assertEqual: (12 bitXor: 10) to: 6.
	self assertEqual: (15 bitXor: 15) to: 0!

testBitShift
	"Test SmallInteger>>#bitShift:"
	self assertEqual: (4 bitShift: 2) to: 16.
	self assertEqual: (16 bitShift: -2) to: 4! !

!SmallIntegerTests methodsFor: 'testing-properties'!
testEven
	"Test SmallInteger>>#even"
	self assertTrue: 4 even.
	self assertFalse: 5 even.
	self assertTrue: 0 even!

testOdd
	"Test SmallInteger>>#odd"
	self assertFalse: 4 odd.
	self assertTrue: 5 odd.
	self assertFalse: 0 odd!

testPositive
	"Test SmallInteger>>#positive"
	self assertTrue: 5 positive.
	self assertFalse: (-5) positive.
	self assertTrue: 0 positive!

testNegative
	"Test SmallInteger>>#negative"
	self assertFalse: 5 negative.
	self assertTrue: (-5) negative.
	self assertFalse: 0 negative!

testStrictlyPositive
	"Test SmallInteger>>#strictlyPositive"
	self assertTrue: 5 strictlyPositive.
	self assertFalse: (-5) strictlyPositive.
	self assertFalse: 0 strictlyPositive!

testSign
	"Test SmallInteger>>#sign"
	self assertEqual: 5 sign to: 1.
	self assertEqual: (-5) sign to: -1.
	self assertEqual: 0 sign to: 0! !

!SmallIntegerTests methodsFor: 'testing-conversion'!
testAsString
	"Test SmallInteger>>#asString"
	self assertEqual: 123 asString to: '123'.
	self assertEqual: (-456) asString to: '-456'.
	self assertEqual: 0 asString to: '0'! !

!SmallIntegerTests methodsFor: 'running'!
performTest
	"Run all SmallInteger tests"
	self resetTestCounts.
	self testAddition.
	self testSubtraction.
	self testMultiplication.
	self testDivision.
	self testIntegerDivision.
	self testModulo.
	self testLessThan.
	self testLessEqual.
	self testGreaterThan.
	self testGreaterEqual.
	self testEquality.
	self testInequality.
	self testBitAnd.
	self testBitOr.
	self testBitXor.
	self testBitShift.
	self testEven.
	self testOdd.
	self testPositive.
	self testNegative.
	self testStrictlyPositive.
	self testSign.
	self testAsString! !

TestCase subclass: #CharacterTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Kernel'!

!CharacterTests methodsFor: 'testing-properties'!
testAsciiValue
	"Test Character>>#asciiValue"
	self assertEqual: $A asciiValue to: 65.
	self assertEqual: $a asciiValue to: 97.
	self assertEqual: $0 asciiValue to: 48!

testDigitValue
	"Test Character>>#digitValue"
	self assertEqual: $0 digitValue to: 0.
	self assertEqual: $9 digitValue to: 9.
	self assertEqual: $A digitValue to: 10.
	self assertEqual: $F digitValue to: 15!

testValue
	"Test Character>>#value"
	self assertEqual: $A value to: 65.
	self assertEqual: $Z value to: 90! !

!CharacterTests methodsFor: 'testing-classification'!
testIsDigit
	"Test Character>>#isDigit"
	self assertTrue: $0 isDigit.
	self assertTrue: $9 isDigit.
	self assertFalse: $A isDigit.
	self assertFalse: $z isDigit!

testIsLetter
	"Test Character>>#isLetter"
	self assertTrue: $A isLetter.
	self assertTrue: $z isLetter.
	self assertFalse: $0 isDigit.
	self assertFalse: $# isLetter!

testIsUppercase
	"Test Character>>#isUppercase"
	self assertTrue: $A isUppercase.
	self assertTrue: $Z isUppercase.
	self assertFalse: $a isUppercase.
	self assertFalse: $z isUppercase!

testIsLowercase
	"Test Character>>#isLowercase"
	self assertFalse: $A isLowercase.
	self assertFalse: $Z isLowercase.
	self assertTrue: $a isLowercase.
	self assertTrue: $z isLowercase!

testIsVowel
	"Test Character>>#isVowel"
	self assertTrue: $a isVowel.
	self assertTrue: $e isVowel.
	self assertTrue: $i isVowel.
	self assertTrue: $o isVowel.
	self assertTrue: $u isVowel.
	self assertFalse: $b isVowel.
	self assertFalse: $z isVowel! !

!CharacterTests methodsFor: 'testing-conversion'!
testAsUppercase
	"Test Character>>#asUppercase"
	self assertEqual: $a asUppercase to: $A.
	self assertEqual: $z asUppercase to: $Z.
	self assertEqual: $A asUppercase to: $A!

testAsLowercase
	"Test Character>>#asLowercase"
	self assertEqual: $A asLowercase to: $a.
	self assertEqual: $Z asLowercase to: $z.
	self assertEqual: $a asLowercase to: $a!

testAsInteger
	"Test Character>>#asInteger"
	self assertEqual: $A asInteger to: 65.
	self assertEqual: $0 asInteger to: 48! !

!CharacterTests methodsFor: 'testing-comparison'!
testEquality
	"Test Character>>#="
	self assertTrue: $A = $A.
	self assertFalse: $A = $B!

testLessThan
	"Test Character>>#<"
	self assertTrue: $A < $B.
	self assertFalse: $B < $A.
	self assertTrue: $0 < $9! !

!CharacterTests methodsFor: 'running'!
performTest
	"Run all Character tests"
	self resetTestCounts.
	self testAsciiValue.
	self testDigitValue.
	self testValue.
	self testIsDigit.
	self testIsLetter.
	self testIsUppercase.
	self testIsLowercase.
	self testIsVowel.
	self testAsUppercase.
	self testAsLowercase.
	self testAsInteger.
	self testEquality.
	self testLessThan! !

TestCase subclass: #ArrayTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!ArrayTests methodsFor: 'testing-creation'!
testNew
	"Test Array class>>#new:"
	| array |
	array _ Array new: 3.
	self assertEqual: array size to: 3.
	self assertEqual: (array at: 1) to: nil.
	self assertEqual: (array at: 2) to: nil.
	self assertEqual: (array at: 3) to: nil!

testWith
	"Test Array class>>#with:"
	| array |
	array _ Array with: 'hello'.
	self assertEqual: array size to: 1.
	self assertEqual: (array at: 1) to: 'hello'!

testWithWith
	"Test Array class>>#with:with:"
	| array |
	array _ Array with: 'hello' with: 'world'.
	self assertEqual: array size to: 2.
	self assertEqual: (array at: 1) to: 'hello'.
	self assertEqual: (array at: 2) to: 'world'! !

!ArrayTests methodsFor: 'testing-accessing'!
testAtPut
	"Test Array>>#at:put:"
	| array |
	array _ Array new: 3.
	array at: 1 put: 'first'.
	array at: 2 put: 'second'.
	array at: 3 put: 'third'.
	self assertEqual: (array at: 1) to: 'first'.
	self assertEqual: (array at: 2) to: 'second'.
	self assertEqual: (array at: 3) to: 'third'!

testAt
	"Test Array>>#at:"
	| array |
	array _ Array with: 'hello' with: 'world'.
	self assertEqual: (array at: 1) to: 'hello'.
	self assertEqual: (array at: 2) to: 'world'!

testSize
	"Test Array>>#size"
	| array |
	array _ Array new: 5.
	self assertEqual: array size to: 5.
	array _ Array with: 1 with: 2 with: 3.
	self assertEqual: array size to: 3! !

!ArrayTests methodsFor: 'testing-enumeration'!
testDo
	"Test Array>>#do:"
	| array result |
	array _ Array with: 1 with: 2 with: 3.
	result _ 0.
	array do: [:each | result _ result + each].
	self assertEqual: result to: 6! !

!ArrayTests methodsFor: 'testing-copying'!
testCopyWith
	"Test Array>>#copyWith:"
	| array newArray |
	array _ Array with: 1 with: 2.
	newArray _ array copyWith: 3.
	self assertEqual: newArray size to: 3.
	self assertEqual: (newArray at: 1) to: 1.
	self assertEqual: (newArray at: 2) to: 2.
	self assertEqual: (newArray at: 3) to: 3! !

!ArrayTests methodsFor: 'running'!
performTest
	"Run all Array tests"
	self resetTestCounts.
	self testNew.
	self testWith.
	self testWithWith.
	self testAtPut.
	self testAt.
	self testSize.
	self testDo.
	self testCopyWith! !

TestCase subclass: #StringTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!StringTests methodsFor: 'testing-accessing'!
testAtPut
	"Test String>>#at:put:"
	| string |
	string _ 'hello'.
	string at: 1 put: $H.
	self assertEqual: (string at: 1) to: $H!

testAt
	"Test String>>#at:"
	| string |
	string _ 'hello'.
	self assertEqual: (string at: 1) to: $h.
	self assertEqual: (string at: 5) to: $o!

testSize
	"Test String>>#size"
	self assertEqual: 'hello' size to: 5.
	self assertEqual: '' size to: 0! !

!StringTests methodsFor: 'testing-comparison'!
testEquality
	"Test String>>#="
	self assertTrue: 'hello' = 'hello'.
	self assertFalse: 'hello' = 'world'!

testLessThan
	"Test String>>#<"
	self assertTrue: 'abc' < 'def'.
	self assertFalse: 'def' < 'abc'.
	self assertTrue: 'a' < 'aa'! !

!StringTests methodsFor: 'testing-conversion'!
testAsSymbol
	"Test String>>#asSymbol"
	| symbol |
	symbol _ 'hello' asSymbol.
	self assertEqual: symbol asString to: 'hello'!

testAsUppercase
	"Test String>>#asUppercase"
	self assertEqual: 'hello' asUppercase to: 'HELLO'.
	self assertEqual: 'WORLD' asUppercase to: 'WORLD'!

testAsLowercase
	"Test String>>#asLowercase"
	self assertEqual: 'HELLO' asLowercase to: 'hello'.
	self assertEqual: 'world' asLowercase to: 'world'! !

!StringTests methodsFor: 'testing-concatenation'!
testConcatenation
	"Test String>>#,"
	| result |
	result _ 'hello' , ' world'.
	self assertEqual: result to: 'hello world'! !

!StringTests methodsFor: 'running'!
performTest
	"Run all String tests"
	self resetTestCounts.
	self testAtPut.
	self testAt.
	self testSize.
	self testEquality.
	self testLessThan.
	self testAsSymbol.
	self testAsUppercase.
	self testAsLowercase.
	self testConcatenation! !

TestCase subclass: #SymbolTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

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
	| sym copy |
	sym _ #hello.
	copy _ sym copy.
	self assertTrue: sym == copy!

testImmutableShallowCopy
	"Test Symbol shallowCopy returns self"
	| sym copy |
	sym _ #hello.
	copy _ sym shallowCopy.
	self assertTrue: sym == copy! !

!SymbolTests methodsFor: 'testing-conversion'!
testAsString
	"Test Symbol>>#asString"
	| sym string |
	sym _ #hello.
	string _ sym asString.
	self assertEqual: string to: 'hello'.
	self assertFalse: sym == string!

testAsSymbol
	"Test Symbol>>#asSymbol"
	| sym result |
	sym _ #hello.
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
	self assertEqual: #hello numArgs to: 0.
	self assertEqual: #at: numArgs to: 1.
	self assertEqual: #at:put: numArgs to: 2.
	self assertEqual: #+ numArgs to: 1! !

!SymbolTests methodsFor: 'running'!
performTest
	"Run all Symbol tests"
	self resetTestCounts.
	self testIdentityEquality.
	self testHashConsistency.
	self testImmutableCopy.
	self testImmutableShallowCopy.
	self testAsString.
	self testAsSymbol.
	self testIsKeyword.
	self testNumArgs! !

TestCase subclass: #SetTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!SetTests methodsFor: 'testing-creation'!
testNew
	"Test Set class>>#new"
	| set |
	set _ Set new.
	self assertEqual: set size to: 0!

testNewWithSize
	"Test Set class>>#new:"
	| set |
	set _ Set new: 10.
	self assertEqual: set size to: 0! !

!SetTests methodsFor: 'testing-adding'!
testAdd
	"Test Set>>#add:"
	| set |
	set _ Set new.
	set add: 'hello'.
	self assertEqual: set size to: 1.
	self assertTrue: (set includes: 'hello').
	
	"Adding same element should not increase size"
	set add: 'hello'.
	self assertEqual: set size to: 1! !

!SetTests methodsFor: 'testing-membership'!
testIncludes
	"Test Set>>#includes:"
	| set |
	set _ Set new.
	set add: 'hello'.
	set add: 'world'.
	self assertTrue: (set includes: 'hello').
	self assertTrue: (set includes: 'world').
	self assertFalse: (set includes: 'foo')!

testOccurrencesOf
	"Test Set>>#occurrencesOf:"
	| set |
	set _ Set new.
	set add: 'hello'.
	self assertEqual: (set occurrencesOf: 'hello') to: 1.
	self assertEqual: (set occurrencesOf: 'world') to: 0! !

!SetTests methodsFor: 'testing-removing'!
testRemove
	"Test Set>>#remove:ifAbsent:"
	| set |
	set _ Set new.
	set add: 'hello'.
	set add: 'world'.
	set remove: 'hello' ifAbsent: [].
	self assertEqual: set size to: 1.
	self assertFalse: (set includes: 'hello').
	self assertTrue: (set includes: 'world')! !

!SetTests methodsFor: 'testing-enumeration'!
testDo
	"Test Set>>#do:"
	| set result |
	set _ Set new.
	set add: 1.
	set add: 2.
	set add: 3.
	result _ 0.
	set do: [:each | result _ result + each].
	self assertEqual: result to: 6! !

!SetTests methodsFor: 'running'!
performTest
	"Run all Set tests"
	self resetTestCounts.
	self testNew.
	self testNewWithSize.
	self testAdd.
	self testIncludes.
	self testOccurrencesOf.
	self testRemove.
	self testDo! !

TestCase subclass: #DictionaryTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!DictionaryTests methodsFor: 'testing-creation'!
testNew
	"Test Dictionary class>>#new"
	| dict |
	dict _ Dictionary new.
	self assertEqual: dict size to: 0! !

!DictionaryTests methodsFor: 'testing-accessing'!
testAtPut
	"Test Dictionary>>#at:put:"
	| dict |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	self assertEqual: dict size to: 2.
	self assertEqual: (dict at: 'key1') to: 'value1'.
	self assertEqual: (dict at: 'key2') to: 'value2'!

testAtIfAbsent
	"Test Dictionary>>#at:ifAbsent:"
	| dict result |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	result _ dict at: 'key1' ifAbsent: ['not found'].
	self assertEqual: result to: 'value1'.
	result _ dict at: 'nonexistent' ifAbsent: ['not found'].
	self assertEqual: result to: 'not found'! !

!DictionaryTests methodsFor: 'testing-keys-values'!
testKeys
	"Test Dictionary>>#keys"
	| dict keys |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	keys _ dict keys.
	self assertEqual: keys size to: 2.
	self assertTrue: (keys includes: 'key1').
	self assertTrue: (keys includes: 'key2')!

testValues
	"Test Dictionary>>#values"
	| dict values |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	values _ dict values.
	self assertEqual: values size to: 2!

testIncludesKey
	"Test Dictionary>>#includesKey:"
	| dict |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	self assertTrue: (dict includesKey: 'key1').
	self assertFalse: (dict includesKey: 'nonexistent')! !

!DictionaryTests methodsFor: 'testing-associations'!
testAssociations
	"Test Dictionary>>#associations"
	| dict associations |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	associations _ dict associations.
	self assertEqual: associations size to: 2!

testAssociationAt
	"Test Dictionary>>#associationAt:"
	| dict assoc |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	assoc _ dict associationAt: 'key1'.
	self assertEqual: assoc key to: 'key1'.
	self assertEqual: assoc value to: 'value1'! !

!DictionaryTests methodsFor: 'testing-removing'!
testRemoveKey
	"Test Dictionary>>#removeKey:"
	| dict |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	dict removeKey: 'key1'.
	self assertEqual: dict size to: 1.
	self assertFalse: (dict includesKey: 'key1').
	self assertTrue: (dict includesKey: 'key2')! !

!DictionaryTests methodsFor: 'testing-enumeration'!
testKeysDo
	"Test Dictionary>>#keysDo:"
	| dict keys |
	dict _ Dictionary new.
	dict at: 'key1' put: 'value1'.
	dict at: 'key2' put: 'value2'.
	keys _ OrderedCollection new.
	dict keysDo: [:key | keys add: key].
	self assertEqual: keys size to: 2!

testDo
	"Test Dictionary>>#do:"
	| dict values |
	dict _ Dictionary new.
	dict at: 'key1' put: 1.
	dict at: 'key2' put: 2.
	values _ 0.
	dict do: [:value | values _ values + value].
	self assertEqual: values to: 3! !

!DictionaryTests methodsFor: 'running'!
performTest
	"Run all Dictionary tests"
	self resetTestCounts.
	self testNew.
	self testAtPut.
	self testAtIfAbsent.
	self testKeys.
	self testValues.
	self testIncludesKey.
	self testAssociations.
	self testAssociationAt.
	self testRemoveKey.
	self testKeysDo.
	self testDo! !

TestCase subclass: #AssociationTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!AssociationTests methodsFor: 'testing-creation'!
testKeyValue
	"Test Association class>>#key:value:"
	| assoc |
	assoc _ Association key: 'myKey' value: 'myValue'.
	self assertEqual: assoc key to: 'myKey'.
	self assertEqual: assoc value to: 'myValue'! !

!AssociationTests methodsFor: 'testing-accessing'!
testKey
	"Test Association>>#key"
	| assoc |
	assoc _ Association key: 'testKey' value: 'testValue'.
	self assertEqual: assoc key to: 'testKey'!

testValue
	"Test Association>>#value"
	| assoc |
	assoc _ Association key: 'testKey' value: 'testValue'.
	self assertEqual: assoc value to: 'testValue'!

testValueSet
	"Test Association>>#value:"
	| assoc |
	assoc _ Association key: 'testKey' value: 'testValue'.
	assoc value: 'newValue'.
	self assertEqual: assoc value to: 'newValue'! !

!AssociationTests methodsFor: 'running'!
performTest
	"Run all Association tests"
	self resetTestCounts.
	self testKeyValue.
	self testKey.
	self testValue.
	self testValueSet! !

TestCase subclass: #OrderedCollectionTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Collections'!

!OrderedCollectionTests methodsFor: 'testing-creation'!
testNew
	"Test OrderedCollection class>>#new"
	| coll |
	coll _ OrderedCollection new.
	self assertEqual: coll size to: 0! !

!OrderedCollectionTests methodsFor: 'testing-adding'!
testAdd
	"Test OrderedCollection>>#add:"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll add: 'second'.
	self assertEqual: coll size to: 2.
	self assertEqual: (coll at: 1) to: 'first'.
	self assertEqual: (coll at: 2) to: 'second'!

testAddFirst
	"Test OrderedCollection>>#addFirst:"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'second'.
	coll addFirst: 'first'.
	self assertEqual: coll size to: 2.
	self assertEqual: (coll at: 1) to: 'first'.
	self assertEqual: (coll at: 2) to: 'second'!

testAddLast
	"Test OrderedCollection>>#addLast:"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll addLast: 'last'.
	self assertEqual: coll size to: 2.
	self assertEqual: (coll at: 1) to: 'first'.
	self assertEqual: (coll at: 2) to: 'last'! !

!OrderedCollectionTests methodsFor: 'testing-accessing'!
testFirst
	"Test OrderedCollection>>#first"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll add: 'second'.
	self assertEqual: coll first to: 'first'!

testLast
	"Test OrderedCollection>>#last"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll add: 'last'.
	self assertEqual: coll last to: 'last'! !

!OrderedCollectionTests methodsFor: 'testing-removing'!
testRemoveFirst
	"Test OrderedCollection>>#removeFirst"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll add: 'second'.
	coll removeFirst.
	self assertEqual: coll size to: 1.
	self assertEqual: coll first to: 'second'!

testRemoveLast
	"Test OrderedCollection>>#removeLast"
	| coll |
	coll _ OrderedCollection new.
	coll add: 'first'.
	coll add: 'last'.
	coll removeLast.
	self assertEqual: coll size to: 1.
	self assertEqual: coll first to: 'first'! !

!OrderedCollectionTests methodsFor: 'running'!
performTest
	"Run all OrderedCollection tests"
	self resetTestCounts.
	self testNew.
	self testAdd.
	self testAddFirst.
	self testAddLast.
	self testFirst.
	self testLast.
	self testRemoveFirst.
	self testRemoveLast! !

TestCase subclass: #StandardLibraryTestSuite
	instanceVariableNames: 'objectTests undefinedObjectTests smallIntegerTests characterTests arrayTests stringTests symbolTests setTests dictionaryTests associationTests orderedCollectionTests'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-StandardLibrary'!

!StandardLibraryTestSuite methodsFor: 'initialization'!
initialize
	"Initialize all test cases"
	super initialize.
	objectTests _ ObjectTests new.
	undefinedObjectTests _ UndefinedObjectTests new.
	smallIntegerTests _ SmallIntegerTests new.
	characterTests _ CharacterTests new.
	arrayTests _ ArrayTests new.
	stringTests _ StringTests new.
	symbolTests _ SymbolTests new.
	setTests _ SetTests new.
	dictionaryTests _ DictionaryTests new.
	associationTests _ AssociationTests new.
	orderedCollectionTests _ OrderedCollectionTests new! !

!StandardLibraryTestSuite methodsFor: 'running'!
performTest
	"Run all StandardLibrary tests"
	self resetTestCounts.
	
	"Run all test suites"
	objectTests performTest.
	undefinedObjectTests performTest.
	smallIntegerTests performTest.
	characterTests performTest.
	arrayTests performTest.
	stringTests performTest.
	symbolTests performTest.
	setTests performTest.
	dictionaryTests performTest.
	associationTests performTest.
	orderedCollectionTests performTest!

runAllTests
	"Run all tests and return summary"
	self initialize.
	self performTest.
	^'StandardLibrary Test Suite: All tests completed'! !

"End of StandardLibrary_Tests.st"
