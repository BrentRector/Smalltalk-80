"Comprehensive tests for Character class"

TestCase subclass: #CharacterTests
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-Character'!

!CharacterTests methodsFor: 'testing-accessing'!
testAsciiValue
	"Test Character>>asciiValue"
	self assertEqual: 65 to: $A asciiValue.
	self assertEqual: 97 to: $a asciiValue.
	self assertEqual: 48 to: $0 asciiValue.
	self assertEqual: 32 to: Character space asciiValue!

testDigitValue
	"Test Character>>digitValue"
	self assertEqual: 0 to: $0 digitValue.
	self assertEqual: 5 to: $5 digitValue.
	self assertEqual: 9 to: $9 digitValue.
	self assertEqual: 10 to: $A digitValue.
	self assertEqual: 15 to: $F digitValue.
	self assertEqual: 35 to: $Z digitValue.
	self assertTrue: $@ digitValue < 0!

testValue
	"Test Character>>value"
	self assertEqual: 65 to: $A value.
	self assertEqual: 97 to: $a value.
	self assertEqual: 48 to: $0 value! !

!CharacterTests methodsFor: 'testing-comparing'!
testEquality
	"Test Character>>="
	self assertTrue: $A = $A.
	self assertFalse: $A = $B.
	self assertFalse: $A = $a!

testLessThan
	"Test Character>><"
	self assertTrue: $A < $B.
	self assertTrue: $A < $a.
	self assertFalse: $B < $A!

testLessThanOrEqual
	"Test Character>>="
	self assertTrue: $A <= $A.
	self assertTrue: $A <= $B.
	self assertFalse: $B <= $A!

testGreaterThan
	"Test Character>>>"
	self assertTrue: $B > $A.
	self assertTrue: $a > $A.
	self assertFalse: $A > $B!

testGreaterThanOrEqual
	"Test Character>>="
	self assertTrue: $A >= $A.
	self assertTrue: $B >= $A.
	self assertFalse: $A >= $B!

testHash
	"Test Character>>hash"
	self assertEqual: $A asciiValue to: $A hash.
	self assertEqual: $a asciiValue to: $a hash! !

!CharacterTests methodsFor: 'testing-testing'!
testIsAlphaNumeric
	"Test Character>>isAlphaNumeric"
	self assertTrue: $A isAlphaNumeric.
	self assertTrue: $z isAlphaNumeric.
	self assertTrue: $5 isAlphaNumeric.
	self assertFalse: $@ isAlphaNumeric.
	self assertFalse: Character space isAlphaNumeric!

testIsDigit
	"Test Character>>isDigit"
	self assertTrue: $0 isDigit.
	self assertTrue: $5 isDigit.
	self assertTrue: $9 isDigit.
	self assertFalse: $A isDigit.
	self assertFalse: $a isDigit!

testIsLetter
	"Test Character>>isLetter"
	self assertTrue: $A isLetter.
	self assertTrue: $Z isLetter.
	self assertTrue: $a isLetter.
	self assertTrue: $z isLetter.
	self assertFalse: $0 isLetter.
	self assertFalse: $@ isLetter!

testIsLowercase
	"Test Character>>isLowercase"
	self assertTrue: $a isLowercase.
	self assertTrue: $z isLowercase.
	self assertFalse: $A isLowercase.
	self assertFalse: $Z isLowercase.
	self assertFalse: $0 isLowercase!

testIsUppercase
	"Test Character>>isUppercase"
	self assertTrue: $A isUppercase.
	self assertTrue: $Z isUppercase.
	self assertFalse: $a isUppercase.
	self assertFalse: $z isUppercase.
	self assertFalse: $0 isUppercase!

testIsSeparator
	"Test Character>>isSeparator"
	self assertTrue: Character space isSeparator.
	self assertTrue: Character cr isSeparator.
	self assertTrue: Character tab isSeparator.
	self assertTrue: Character lf isSeparator.
	self assertFalse: $A isSeparator.
	self assertFalse: $0 isSeparator!

testIsVowel
	"Test Character>>isVowel"
	self assertTrue: $A isVowel.
	self assertTrue: $E isVowel.
	self assertTrue: $I isVowel.
	self assertTrue: $O isVowel.
	self assertTrue: $U isVowel.
	self assertTrue: $a isVowel.
	self assertTrue: $e isVowel.
	self assertTrue: $i isVowel.
	self assertTrue: $o isVowel.
	self assertTrue: $u isVowel.
	self assertFalse: $B isVowel.
	self assertFalse: $b isVowel!

testTokenish
	"Test Character>>tokenish"
	self assertTrue: $A tokenish.
	self assertTrue: $a tokenish.
	self assertTrue: $5 tokenish.
	self assertTrue: $: tokenish.
	self assertFalse: $@ tokenish.
	self assertFalse: Character space tokenish! !

!CharacterTests methodsFor: 'testing-copying'!
testCopy
	"Test Character>>copy - Characters are unique"
	self assertEqual: $A to: $A copy.
	self assertTrue: $A == $A copy!

testDeepCopy
	"Test Character>>deepCopy - Characters are unique"
	self assertEqual: $A to: $A deepCopy.
	self assertTrue: $A == $A deepCopy! !

!CharacterTests methodsFor: 'testing-converting'!
testAsCharacter
	"Test Character>>asCharacter"
	self assertEqual: $A to: $A asCharacter.
	self assertTrue: $A == $A asCharacter!

testAsInteger
	"Test Character>>asInteger"
	self assertEqual: 65 to: $A asInteger.
	self assertEqual: 97 to: $a asInteger!

testAsLowercase
	"Test Character>>asLowercase"
	self assertEqual: $a to: $A asLowercase.
	self assertEqual: $z to: $Z asLowercase.
	self assertEqual: $a to: $a asLowercase.
	self assertEqual: $5 to: $5 asLowercase!

testAsUppercase
	"Test Character>>asUppercase"
	self assertEqual: $A to: $a asUppercase.
	self assertEqual: $Z to: $z asUppercase.
	self assertEqual: $A to: $A asUppercase.
	self assertEqual: $5 to: $5 asUppercase!

testAsSymbol
	"Test Character>>asSymbol"
	self assertEqual: $A to: $A asSymbol! !

!CharacterTests methodsFor: 'testing-class-methods'!
testCharacterCreation
	"Test Character class>>value:"
	self assertEqual: $A to: (Character value: 65).
	self assertEqual: $a to: (Character value: 97).
	self assertEqual: $0 to: (Character value: 48)!

testSpecialCharacters
	"Test Character class special character creation"
	self assertEqual: 13 to: Character cr asciiValue.
	self assertEqual: 10 to: Character lf asciiValue.
	self assertEqual: 9 to: Character tab asciiValue.
	self assertEqual: 32 to: Character space asciiValue! !

!CharacterTests methodsFor: 'running'!
performTest
	"Run all the character tests"
	self resetTestCounts.
	self runIndividualTest: #testAsciiValue.
	self runIndividualTest: #testDigitValue.
	self runIndividualTest: #testValue.
	self runIndividualTest: #testEquality.
	self runIndividualTest: #testLessThan.
	self runIndividualTest: #testLessThanOrEqual.
	self runIndividualTest: #testGreaterThan.
	self runIndividualTest: #testGreaterThanOrEqual.
	self runIndividualTest: #testHash.
	self runIndividualTest: #testIsAlphaNumeric.
	self runIndividualTest: #testIsDigit.
	self runIndividualTest: #testIsLetter.
	self runIndividualTest: #testIsLowercase.
	self runIndividualTest: #testIsUppercase.
	self runIndividualTest: #testIsSeparator.
	self runIndividualTest: #testIsVowel.
	self runIndividualTest: #testTokenish.
	self runIndividualTest: #testCopy.
	self runIndividualTest: #testDeepCopy.
	self runIndividualTest: #testAsCharacter.
	self runIndividualTest: #testAsInteger.
	self runIndividualTest: #testAsLowercase.
	self runIndividualTest: #testAsUppercase.
	self runIndividualTest: #testAsSymbol.
	self runIndividualTest: #testCharacterCreation.
	self runIndividualTest: #testSpecialCharacters.
	self printTestResults! !
