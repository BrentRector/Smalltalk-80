nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Object comment: 'Object is the root class for almost all classes in the system.'!

!Object methodsFor: 'accessing'!
yourself
	"Answer the receiver."
	^self! !

!Object methodsFor: 'comparing'!
= anObject 
	"Answer whether the receiver and the argument represent the same object."
	^self == anObject!

hash
	"Answer a SmallInteger whose value is related to the receiver's identity."
	<primitive: 75>
	self primitiveFailed!

~= anObject 
	"Answer whether the receiver and the argument do not represent the same object."
	^self = anObject == false! !

!Object methodsFor: 'testing'!
isNil
	"Answer true if the receiver is nil."
	^false! !

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Boolean comment: 'Boolean is the abstract superclass of the "true" and "false" objects.'!

!Boolean methodsFor: 'logical operations'!
& aBoolean 
	"Evaluating conjunction -- answer true if both the receiver and the argument are true."
	self subclassResponsibility!

not
	"Negation--answer true if the receiver is false, answer false if the receiver is true."
	self subclassResponsibility!

| aBoolean 
	"Evaluating disjunction -- answer true if either the receiver or the argument is true."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- if the receiver is true, answer the value of alternativeBlock, otherwise answer false without evaluating alternativeBlock."
	self subclassResponsibility!

ifFalse: alternativeBlock 
	"If the receiver is false, answer the value of alternativeBlock. Otherwise, answer nil."
	self subclassResponsibility!

ifTrue: alternativeBlock 
	"If the receiver is true, answer the value of alternativeBlock. Otherwise, answer nil."
	self subclassResponsibility!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the value of trueAlternativeBlock if the receiver is true, otherwise answer the value of falseAlternativeBlock."
	self subclassResponsibility!

or: alternativeBlock 
	"Nonevaluating disjunction -- if the receiver is false, answer the value of alternativeBlock, otherwise answer true without evaluating alternativeBlock."
	self subclassResponsibility! !

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
True comment: 'True defines the behavior of the unique true object.'!

!True methodsFor: 'logical operations'!
& aBoolean 
	"Evaluating conjunction -- answer aBoolean since the receiver is true."
	^aBoolean!

not
	"Negation -- answer false since the receiver is true."
	^false!

| aBoolean 
	"Evaluating disjunction -- answer true since the receiver is true."
	^true! !

!True methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer the value of alternativeBlock since the receiver is true."
	^alternativeBlock value!

ifFalse: alternativeBlock 
	"If the receiver is false, answer the value of alternativeBlock. Since the receiver is true, answer nil."
	^nil!

ifTrue: alternativeBlock 
	"If the receiver is true, answer the value of alternativeBlock. Since the receiver is true, answer the block value."
	^alternativeBlock value!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the value of trueAlternativeBlock since the receiver is true."
	^trueAlternativeBlock value!

or: alternativeBlock 
	"Nonevaluating disjunction -- answer true since the receiver is true."
	^true! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
False comment: 'False defines the behavior of the unique false object.'!

!False methodsFor: 'logical operations'!
& aBoolean 
	"Evaluating conjunction -- answer false since the receiver is false."
	^false!

not
	"Negation -- answer true since the receiver is false."
	^true!

| aBoolean 
	"Evaluating disjunction -- answer aBoolean since the receiver is false."
	^aBoolean! !

!False methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer false since the receiver is false."
	^false!

ifFalse: alternativeBlock 
	"If the receiver is false, answer the value of alternativeBlock. Since the receiver is false, answer the block value."
	^alternativeBlock value!

ifTrue: alternativeBlock 
	"If the receiver is true, answer the value of alternativeBlock. Since the receiver is false, answer nil."
	^nil!

ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the value of falseAlternativeBlock since the receiver is false."
	^falseAlternativeBlock value!

or: alternativeBlock 
	"Nonevaluating disjunction -- answer the value of alternativeBlock since the receiver is false."
	^alternativeBlock value! !

Object subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!
SmallInteger comment: 'SmallIntegers are 31-bit signed integers. Arithmetic operations return SmallIntegers when the result is in the SmallInteger range.'!

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber 
	"Primitive. Add the receiver to the argument and answer with the result if it is a SmallInteger. Fail if the argument or the result is not a SmallInteger. Essential."
	<primitive: 1>
	^super + aNumber!

- aNumber 
	"Primitive. Subtract the argument from the receiver and answer with the result if it is a SmallInteger. Fail if the argument or the result is not a SmallInteger. Essential."
	<primitive: 2>
	^super - aNumber!

* aNumber 
	"Primitive. Multiply the receiver by the argument and answer with the result if it is a SmallInteger. Fail if the argument or the result is not a SmallInteger. Essential."
	<primitive: 9>
	^super * aNumber!

/ aNumber 
	"Primitive. Divide the receiver by the argument and answer with the result if it is a SmallInteger. Fail if the argument or the result is not a SmallInteger. Essential."
	<primitive: 10>
	^super / aNumber! !

!SmallInteger methodsFor: 'comparing'!
< aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is less than the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 3>
	^super < aNumber!

<= aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is less than or equal to the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 5>
	^super <= aNumber!

> aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is greater than the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 4>
	^super > aNumber!

>= aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is greater than or equal to the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 6>
	^super >= aNumber!

= aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is equal to the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 7>
	^super = aNumber!

~= aNumber 
	"Primitive. Compare the receiver with the argument and answer with true if the receiver is not equal to the argument. Otherwise answer false. Fail if the argument is not a SmallInteger. Essential."
	<primitive: 8>
	^super ~= aNumber! !

Object subclass: #ArrayedCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!
ArrayedCollection comment: 'ArrayedCollection is the abstract superclass of classes whose instances are collections of objects stored in a variable number of indexable instance variables.'!

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Magnitude comment: 'Magnitude is the abstract superclass of classes that provide a total ordering on their instances.'!

Magnitude subclass: #Character
	instanceVariableNames: 'value '
	classVariableNames: 'CharacterTable '
	poolDictionaries: ''
	category: 'Collections-Text'!
Character comment: 'This class represents characters by storing their associated ASCII (extended to 256 codes) code.  The instances of this class are created uniquely, so that all instances $R (for instance) are identical'!

!Character methodsFor: 'comparing'!
< aCharacter 
	"Answer true if the receiver's value < aCharacter's value."

	^self asciiValue < aCharacter asciiValue!

= aCharacter 
	"Answer true if the receiver and the argument are the same object (have the 
	same object pointer) and false otherwise.  Optional.  See Object documentation
	whatIsAPrimitive."

	<primitive: 110>
	^self == aCharacter!

> aCharacter 
	"Answer true if the receiver's value > aCharacter's value."

	^self asciiValue > aCharacter asciiValue!

hash
	^value! !

!Character methodsFor: 'accessing'!
asciiValue
	"Answer the value of the receiver."
	^value!

digitValue
	"Answer 0-9 if the receiver is $0-$9, 10-35 if it is $A-$Z, and < 0 otherwise.
	This is used to parse literal numbers of radix 2-36."

	value <= $9 asciiValue 
		ifTrue: [^value - $0 asciiValue].
	value >= $A asciiValue 
		ifTrue: [value <= $Z asciiValue ifTrue: [^value - $A asciiValue + 10]].
	^-1! !

!Character methodsFor: 'testing'!
isAlphaNumeric
	"Answer whether the receiver is a letter or a digit."
	^self isLetter or: [self isDigit]!

isDigit
	"Answer whether the receiver is a digit."
	^self >= $0 and: [self <= $9]!

isLetter
	"Answer whether the receiver is a letter."
	^(8r141 <= value and: [value <= 8r172])
		or: [8r101 <= value and: [value <= 8r132]]!

isLowercase
	"Answer whether the receiver is a lowercase letter."
	^self >= $a and: [self <= $z]!

isSeparator
	"Answer whether the receiver is one of the separator characters--space,
	cr, tab, line feed, or form feed."

	value = 32 ifTrue: [^true].	"space"
	value = 13 ifTrue: [^true].	"cr"
	value = 9 ifTrue: [^true].	"tab"
	value = 10 ifTrue: [^true].	"line feed"
	value = 12 ifTrue: [^true].	"form feed"
	^false!

isUppercase
	"Answer whether the receiver is an uppercase letter."
	^self >= $A and: [self <= $Z]!

isVowel
	"Answer whether the receiver is one of the vowels, AEIOU, in upper or lower case."

	^'AEIOU' includes: self asUppercase!

tokenish
	"Answer whether the receiver is a valid token-character--letter, digit, or colon."

	^self isLetter or: [self isDigit or: [self = $:]]! !

!Character methodsFor: 'copying'!
copy
	"Answer with me because Characters are unique."!

deepCopy
	"Answer with me becuase Characters are unique."! !

!Character methodsFor: 'printing'!
isLiteral
	^true!

printOn: aStream 
	aStream nextPut: $$.
	aStream nextPut: self!

storeOn: aStream
	"Character literals are preceded by '$'."
	aStream nextPut: $$; nextPut: self! !

!Character methodsFor: 'converting'!
asCharacter
	"Answer the receiver itself."
	^self!

asInteger
	"Answer the value of the receiver."
	^value!

asLowercase
	"Answer a Character that is the lower case letter corresponding to the receiver.  If
	the receiver is not an upper case letter, answer the receiver itself."
	
	8r101 <= value 
		ifTrue: [value <= 8r132 
					ifTrue: [^Character value: value+8r40]]!

asSymbol
	^Symbol internCharacter: self!

asUppercase
	"Answer a Character that is the upper case letter corresponding to the receiver.  If
	the receiver is not a lower case letter, answer the receiver itself."
	
	8r141 <= value
		ifTrue: [value <= 8r172 
					ifTrue: [^Character value: value-8r40]]! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Character class
	instanceVariableNames: ''!

!Character class methodsFor: 'class initialization'!
initialize
	"Create the table of unique Characters.  This code is not shown so that the
	user can not destroy the system by trying to recreate the table."! !

!Character class methodsFor: 'instance creation'!
digitValue: x 
	"Answer the Character whose digit value is x.  For example, 
	answer $9 for x=9, $0 for x=0, $A for x=10, $Z for x=35."

	| index |
	index _ x truncated.
	^CharacterTable at: 
		(index < 10
			ifTrue: [48 + index]
			ifFalse: [55 + index])
		+ 1!

new
	"Creating new characters is not allowed."

	self error: 'cannot create new characters'!

value: anInteger 
	"Answer the Character whose value is anInteger.
	Characters are unique; they are stored in the class variable CharacterTable."

	^CharacterTable at: anInteger + 1! !

!Character class methodsFor: 'accessing untypeable characters'!
backspace
	"Answer the Character representing a backspace."
	^self value: 8!

cr
	"Answer the Character representing a carriage return."
	^self value: 13!

esc
	"Answer the Character representing an escape."
	^self value: 160!

newPage
	"Answer the Character representing a form feed."
	^self value: 12!

space
	"Answer the Character representing a space."
	^self value: 32!

tab
	"Answer the Character representing a tab."
	^self value: 9! !

!Character class methodsFor: 'constants'!
characterTable
	"Answer the class variable in which unique Characters are stored."
	^CharacterTable! !

ArrayedCollection variableByteSubclass: #String
	instanceVariableNames: ''
	classVariableNames: 'StringBlter '
	poolDictionaries: ''
	category: 'Collections-Text'!
String comment: 'I am an indexed collection of Characters.  I really store 8-bit bytes, but my access protocol translates between these and real Character instances.'!

!String methodsFor: 'comparing'!
< aString 
	"Answer true if and only if the receiver collates before aString.  The collation
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 1!

<= aString 
	"Answer true if and only if the receiver collates before aString or is the 
	same as aString.  The collation sequence is ascii with case differences ignored."

	^(self compare: aString) <= 2!

> aString 
	"Answer true if and only if the receiver collates after aString.  The collation
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 3!

>= aString 
	"Answer true if and only if the receiver collates after aString or is the 
	same as aString.  The collation sequence is ascii with case differences ignored."

	^(self compare: aString) >= 2!

hash 
	| l m |
	l := m := self size.
	l <= 2 ifTrue: [
		l = 2 ifTrue: [m := 3] ifFalse: [
			l = 1 ifTrue: [^((self at: 1) asciiValue bitAnd: 127) * 106].
			^21845]].
	^(self at: 1) asciiValue * 48 + ((self at: (m - 1)) asciiValue + l)!

hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!

match: text 
	"Answer whether text matches the pattern in the receiver.  Matching
	ignores upper/lower case differences.  Where the receiver contains #, text may
	contain any single character.  Where the receiver contains *, text may contain any
	sequence of characters."

	| pattern scanning p t back textStream startScan |
	pattern _ ReadStream on: self.
	textStream _ ReadStream on: text.
	scanning _ false.
	[pattern atEnd]
		whileFalse: 
			[p _ pattern next.
			p = $*
				ifTrue: 
					[pattern atEnd ifTrue: [^true].
					scanning _ true.
					startScan _ pattern position]
				ifFalse: 
					[textStream atEnd ifTrue: [^false].
					t _ textStream next.
					(t asUppercase = p asUppercase or: [p = $#])
						ifFalse: 
							[scanning ifFalse: [^false].
							back _ startScan - pattern position.
							pattern skip: back.
							textStream skip: back + 1]].
			(scanning and: [pattern atEnd and: [textStream atEnd not]])
				ifTrue: [back _ startScan - pattern position.
						pattern skip: back.
						textStream skip: back + 1]
			].
	^textStream atEnd!

sameAs: aString 
	"Answer whether the receiver collates precisely with aString. The collation 
	sequence is ascii with case differences ignored."

	^(self compare: aString) = 2!

spellAgainst: aString 
	"Answer an integer between 0 and 100 indicating how similar the argument is to the receiver.  No case conversion is done."

	| i1 i2 size1 size2 score maxLen |
	size1 _ self size.
	size2 _ aString size.
	maxLen _ size1 max: size2.
	score _ 0.
	i1 _ i2 _ 1.
	[i1 <= size1 and: [i2 <= size2]] whileTrue:
		[(self at: i1) = (aString at: i2)
			ifTrue: [score _ score+1. 		"match"
					i1 _ i1+1. 				"advance both"
					i2 _ i2+1]
			ifFalse: [(i2 < size2 and: [(self at: i1) = (aString at: i2+1)])
						ifTrue: [i2 _ i2+1] 	"skip in other"
			ifFalse: [(i1 < size1 and: [(self at: i1+1) = (aString at: i2)])
						ifTrue: [i1 _ i1+1] 	"skip in self"
			ifFalse: [i1 _ i1+1. 				"miss - advance both"
					i2 _ i2+1] ] ] ].

	score = maxLen
		ifTrue: [^100]
		ifFalse: [^100*score//maxLen]! !

!String methodsFor: 'accessing'!
at: index 
	"Answer the Character stored in the field of the receiver indexed by the 
	argument.  Fail if the index argument is not an Integer or is out of bounds.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 63>
	^Character value: (super at: index)!

at: index put: aCharacter 
	"Store the Character in the field of the receiver indicated by the index.  Fail if the 
	index is not an Integer or is out of bounds, or if the argument is not a Character.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 64>
	(aCharacter isKindOf: Character)
		ifTrue: [self errorNonIntegerIndex]
		ifFalse: [self error: 'Strings only store Characters']!

basicAt: index 
	"Answer the Character stored in the field of the receiver indexed by the 
	argument.  Fail if the index argument is not an Integer or is out of bounds.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 63>
	^Character value: (super at: index)!

basicAt: index put: aCharacter 
	"Store the Character in the field of the receiver indicated by the index.  Fail if the 
	index is not an Integer or is out of bounds, or if the argument is not a Character.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 64>
	(aCharacter isKindOf: Character)
		ifTrue: [self errorNonIntegerIndex]
		ifFalse: [self error: 'Strings only store Characters']!

findString: subString startingAt: start 
	"Answer the index of subString within the receiver, starting at start. If the receiver does not contain subString, answer 0."

	| aCharacter index |
	subString isEmpty ifTrue: [^0].
	aCharacter _ subString first.
	start to: self size - subString size + 1 do:
		[:startIndex |
		(self at: startIndex) = aCharacter ifTrue:
			[index _ 1.
			[(self at: startIndex+index-1) = (subString at: index)] whileTrue:
				[index = subString size ifTrue: [^startIndex].
				index _ index+1]]].
	^0!

replaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver."
	(replacement isKindOf: String)
		ifTrue:
			[self primReplaceFrom: start to: stop with: replacement startingAt: repStart]
		ifFalse:
			[super replaceFrom: start to: stop with: replacement startingAt: repStart]!

replaceFrom: start to: stop withByteArray: aByteArray startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the byte array, aByteArray.  Answer the
	receiver."
	| index repOff characterTable |
	<primitive: 105>
	repOff _ repStart - start.
	characterTable _ Character characterTable.
	index _ start - 1.
	[(index _ index + 1) <= stop]
		whileTrue: 
			[self at: index put: (characterTable at: (aByteArray at: repOff + index)+1)]!

size
	"Answer the number of indexable fields in the receiver.  This value is the 
	same as the largest legal subscript.  Essential.  See Object documentation
	whatIsAPrimitive."

	<primitive: 62>
	^self basicSize!

string
	"Answer the receiver itself.  This is for compatibility with other text classes."
	^self! !

!String methodsFor: 'copying'!
copyUpTo: aCharacter 
	"Answer a copy of the receiver from index 1 to the first occurrence of 
	aCharacter, non-inclusive."

	| index |
	index _ self indexOf: aCharacter ifAbsent: [^self].
	^self copyFrom: 1 to: index-1!

deepCopy
	"DeepCopy would otherwise mean make a copy of the character;  since 
	characters are unique, just return a shallowCopy."

	^self shallowCopy! !

!String methodsFor: 'printing'!
isLiteral
	^true!

printOn: aStream 
	"Print inside string quotes, doubling inbedded quotes."

	^self storeOn: aStream!

storeOn: aStream 
	"Print inside string quotes, doubling inbedded quotes."

	| i length x |
	aStream nextPut: $'.
	i _ 0.
	length _ self size.
	[(i _ i + 1) <= length]
		whileTrue: 
			[aStream nextPut: (x _ self at: i).
			x == $' ifTrue: [aStream nextPut: x]].
	"embedded quotes get doubled"
	aStream nextPut: $'! !

!String methodsFor: 'converting'!
asDisplayText
	"Answer a DisplayText whose text string is the receiver."
	^DisplayText text: self asText!

asFileName
	"Answer a string made up from the receiver that is an acceptable file name."
	^Disk checkName: self fixErrors: true!

asLowercase
	"Answer a string made up from the receiver whose characters are all lowercase."

	| aStream |
	aStream _ WriteStream on: (String new: self size).
	self do: [:aCharacter | aStream nextPut: aCharacter asLowercase].
	^aStream contents!

asNumber 
	"Answer the number created by interpreting the receiver as the string
	representation of a number."

	^Number readFromString: self!

asParagraph
	"Answer a Paragraph whose text string is the receiver."
	^Paragraph withText: self asText!

asString
	"Answer the receiver itself."
	^self!

asSymbol
	"Answer the unique symbol whose characters are the characters of the string."
	^Symbol intern: self!

asText
	"Answer a Text whose string is the receiver."
	^Text fromString: self!

asUppercase
	"Answer a string made up from the receiver whose characters are all uppercase."

	| aStream |
	aStream _ WriteStream on: (String new: self size).
	self do: [:aCharacter | aStream nextPut: aCharacter asUppercase].
	^aStream contents!

contractTo: charCount  
	"Shorten by ellipsis if too long"
	| half |
	self size > charCount ifTrue:
		[half _ charCount // 2.
		^ self copyReplaceFrom: half
				to: self size - (charCount-half) + 2
				with: '...']!

withCRs
	"substitute CRs for backslashes"
	^ self collect: [:char | char = $\ ifTrue: [Character cr] ifFalse: [char]]! !

!String methodsFor: 'displaying'!
displayAt: aPoint 
	"Show a representation of the receiver as a DisplayText at location
	aPoint on the display screen."
	self asDisplayText displayAt: aPoint!

displayOn: aDisplayMedium at: aPoint 
	"Show a representation of the receiver as a DisplayText at location
	aPoint on aDisplayMedium."
	self asDisplayText displayOn: aDisplayMedium at: aPoint! !

!String methodsFor: 'private'!
compare: s 
	| i len endResult u1 u2 mylen |
	mylen _ self size.
	len _ s size.
	mylen < len
		ifTrue: 
			[len _ mylen.
			endResult _ 1]
		ifFalse: [endResult _ mylen = len
						ifTrue: [2]
						ifFalse: [3]].
	i _ 0.
	[(i _ i + 1) <= len]
		whileTrue: 
			[u1 _ self at: i.
			u2 _ s at: i.
			u1 = u2
				ifFalse: 
					[u1 _ u1 asUppercase.
					u2 _ u2 asUppercase.
					u1 = u2 ifFalse:
						[^u1 < u2
							ifTrue: [1]
							ifFalse: [3]]]].
	^endResult!

primReplaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver.  No range checks are performed - this may be primitively implemented."
	<primitive: 105>
	super replaceFrom: start to: stop with: replacement startingAt: repStart!

stringhash
	^self hash! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

String class
	instanceVariableNames: ''!

!String class methodsFor: 'instance creation'!
fromString: aString 
	"Answer a new String that is a copy of the argument, aString."
	
	| newString |
	newString _ self new: aString size.
	aString size do: [:i | newString at: i put: (aString at: i)].
	^newString!

readFrom: inStream
	"Answer a new String that is determined by reading the stream, inStream.  Embedded
	double quotes become the quote Character."

	| outStream char done |
	outStream _ WriteStream on: (String new: 16).
	"go to first quote"
	inStream skipTo: $'.
	done _ false.
	[done or: [inStream atEnd]]
		whileFalse: 
			[char _ inStream next.
			char = $'
				ifTrue: 
					[char _ inStream next.
					char = $'
						ifTrue: [outStream nextPut: char]
						ifFalse: [done _ true]]
				ifFalse: [outStream nextPut: char]].
	^outStream contents! !
