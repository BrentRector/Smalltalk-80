"Test file for the four newly implemented primitives"

"Test primitive 139 - Object>>nextObject"
TestClass
subclass: #TestObject139
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing'!

!TestObject139 methodsFor: 'testing'!
testNextObject
	"Test the nextObject primitive"
	| obj next |
	obj := Object new.
	next := obj nextObject.
	"nextObject should return an object or nil"
	^next isNil or: [next isKindOf: Object]!
!

"Test primitive 158 - String>>=aString"
TestClass
subclass: #TestString158
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing'!

!TestString158 methodsFor: 'testing'!
testStringEquals
	"Test the string equality primitive"
	| str1 str2 str3 |
	str1 := 'hello'.
	str2 := 'hello'.
	str3 := 'world'.
	^(str1 = str2) and: [(str1 = str3) not]!
!

"Test primitive 159 - String>><aString"
TestClass
subclass: #TestString159
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing'!

!TestString159 methodsFor: 'testing'!
testStringLessThan
	"Test the string less than primitive"
	| str1 str2 str3 |
	str1 := 'abc'.
	str2 := 'def'.
	str3 := 'abc'.
	^(str1 < str2) and: [(str1 < str3) not]!
!

"Test primitive 170 - Character class>>value:"
TestClass
subclass: #TestCharacter170
instanceVariableNames: ''
classVariableNames: ''
poolDictionaries: ''
category: 'Testing'!

!TestCharacter170 methodsFor: 'testing'!
testCharacterValue
	"Test the character creation primitive"
	| char65 char97 |
	char65 := Character value: 65.  "Should be 'A'"
	char97 := Character value: 97.  "Should be 'a'"
	^(char65 asciiValue = 65) and: [char97 asciiValue = 97]!
!

"Run all tests"
TestRunner new
	addTest: TestObject139 new;
	addTest: TestString158 new;
	addTest: TestString159 new; 
	addTest: TestCharacter170 new;
	runTests!
