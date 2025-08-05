nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Object subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'accessing'!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isNil
	"Coerces nil to true and everything else to false."! !

!Object methodsFor: 'testing'!
notNil
	"Coerces nil to false and everything else to true."! !

!Object methodsFor: 'comparing'!

= anObject
	"Answer whether the receiver and anObject are the same object (have the same object pointer)."
	<primitive: 110>
	^self == anObject!
~= anObject
	"Answer whether the receiver and anObject are not the same object."
	^(self = anObject) == false!
== anObject
	"Answer whether the receiver and anObject are the same object (have the same object pointer)."
	<primitive: 110>
	^self == anObject!
~~ anObject
	"Answer whether the receiver and anObject are not the same object."
	^(self == anObject) == false!
hash
	"Answer a SmallInteger whose value is related to the receiver's identity."
	<primitive: 75>
	^self basicAt: 2! !

!Object methodsFor: 'class membership'!

class
	"Answer the receiver's class."
	<primitive: 111>
	^self basicAt: 1!
isMemberOf: aClass
	"Answer whether the receiver is an instance of the class, aClass."
	^self class == aClass!
isKindOf: aClass
	"Answer a Boolean as to whether the class, aClass, is a superclass or class of the receiver."
	self class == aClass
		ifTrue: [^true]
		ifFalse: [^self class inheritsFrom: aClass]!
respondsTo: aSymbol
	"Answer a Boolean as to whether the method dictionary of the receiver's class contains aSymbol as a message selector."
	^self class canUnderstand: aSymbol! !

!Object methodsFor: 'accessing'!

at: index
	"Answer the value of an indexable field in the receiver. Fail if the argument index is not an Integer or is out of bounds."
	<primitive: 60>
	index isInteger
		ifTrue: [self errorSubscriptBounds: index].
	^self basicAt: index truncated!
at: index put: value
	"Store the argument value in the indexable field of the receiver indicated by index."
	<primitive: 61>
	index isInteger
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	^self basicAt: index truncated put: value!
basicAt: index
	"Answer the value of an indexable field in the receiver. Do not override this message in any subclass."
	<primitive: 60>
	index isInteger ifTrue: [self errorSubscriptBounds: index].
	^self basicAt: index truncated!
basicAt: index put: value
	"Store the second argument value in the indexable field of the receiver indicated by index. Do not override in a subclass."
	<primitive: 61>
	index isInteger
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	^self basicAt: index truncated put: value!
basicSize
	"Answer the number of indexable fields in the receiver. Do not override in any subclass."
	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
size
	"Answer the number of indexable fields in the receiver."
	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0! !

!Object methodsFor: 'copying'!

copy
	"Answer another instance just like the receiver. Subclasses typically override this method."
	^self shallowCopy!
shallowCopy
	"Answer a copy of the receiver which shares the receiver's instance variables."
	| class newObject index |
	class _ self class.
	class superclass isNil ifTrue: [^self].
	class isVariable
		ifTrue: 
			[index _ self basicSize.
			newObject _ class basicNew: index.
			[index > 0]
				whileTrue: 
					[newObject basicAt: index put: (self basicAt: index).
					index _ index - 1]]
		ifFalse: [newObject _ class basicNew].
	index _ class instSize.
	[index > 0]
		whileTrue: 
			[newObject instVarAt: index put: (self instVarAt: index).
			index _ index - 1].
	^newObject! !

!Object methodsFor: 'printing'!

printOn: aStream
	"Append to the argument aStream a sequence of characters that identifies the receiver."
	| title |
	title _ self class name.
	aStream nextPutAll: ((title at: 1) isVowel
							ifTrue: ['an ']
							ifFalse: ['a '])
						, title!
printString
	"Answer a String whose characters are a description of the receiver."
	^self class name, ' (printing not implemented)'!
storeOn: aStream
	"Append to the argument aStream a sequence of characters that is an expression whose evaluation creates an object similar to the receiver."
	aStream nextPutAll: self class name, ' (storing not implemented)'!
storeString
	"Answer a String representation of the receiver from which the receiver can be reconstructed."
	^self class name, ' (storing not implemented)'! !

!Object methodsFor: 'error handling'!

doesNotUnderstand: aMessage
	"Handle the case when the receiver does not understand aMessage."
	self error: 'Message not understood: ', aMessage selector!
error: aString
	"Handle an error condition."
	self halt!
halt
	"This is a simple message to use for inserting breakpoints during debugging."
	self! !

!Object methodsFor: 'system primitives'!

become: otherObject
	"Swap the instance pointers of the receiver and otherObject."
	<primitive: 72>
	self primitiveFailed!
instVarAt: index
	"Answer the value of the instance variable of the receiver at position index."
	<primitive: 73>
	self primitiveFailed!
instVarAt: index put: value
	"Store value in the instance variable of the receiver at position index."
	<primitive: 74>
	self primitiveFailed!
nextInstance
	"Answer the next instance after the receiver in the enumeration of instances of the receiver's class."
	<primitive: 78>
	^nil!
nextObject
	"Answer the next object after the receiver in the enumeration of all objects."
	<primitive: 139>
	^nil! !

!Object class methodsFor: 'instance creation'!

new
	"Answer a new instance of the receiver."
	<primitive: 70>
	^self basicNew!
basicNew
	"Answer a new instance of the receiver (a class) with no indexable fields."
	<primitive: 70>
	self primitiveFailed!
new: size
	"Answer a new instance of the receiver (a class) with size indexable fields."
	<primitive: 71>
	^self basicNew: size!
basicNew: size
	"Answer a new instance of the receiver (a class) with size indexable fields."
	<primitive: 71>
	self primitiveFailed! !

!Object class methodsFor: 'class name'!

name
	"Answer the name of the receiver as a string."
	<primitive: 111>
	^'Object'! !

!Boolean methodsFor: 'logical operations'!

& aBoolean
	"Answer true if both the receiver and aBoolean are true."
	<primitive: 111>
	^self and: [aBoolean]!
| aBoolean  
	"Answer true if either the receiver or aBoolean is true."
	<primitive: 112>
	^self or: [aBoolean]!
not
	"Answer the logical negation of the receiver."
	self subclassResponsibility!
and: alternativeBlock
	"Answer true if both the receiver and the result of evaluating alternativeBlock are true."
	self subclassResponsibility!
or: alternativeBlock
	"Answer true if either the receiver or the result of evaluating alternativeBlock is true."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!

ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise answer nil."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock
	"Answer the result of evaluating falseAlternativeBlock if the receiver is false, otherwise answer nil."
	self subclassResponsibility!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise the result of evaluating falseAlternativeBlock."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock
	"Answer the result of evaluating trueAlternativeBlock if the receiver is true, otherwise the result of evaluating falseAlternativeBlock."
	self subclassResponsibility! !

!True methodsFor: 'logical operations'!

& alternativeObject 
	"Evaluating conjunction -- answer alternativeObject since receiver is true."
	^alternativeObject!
not
	"Negation--answer false since the receiver is true."
	^false!
| aBoolean 
	"Evaluating disjunction (OR) -- answer true since the receiver is true."
	^self!
and: alternativeBlock
	"Nonevaluating conjunction -- answer the value of alternativeBlock since
	the receiver is true."
	^alternativeBlock value!
or: alternativeBlock
	"Nonevaluating disjunction -- answer true since the receiver is true."
	^self! !

!True methodsFor: 'controlling'!

ifTrue: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually 
	reach here because the expression is compiled in-line."
	^alternativeBlock value!
ifFalse: alternativeBlock 
	"Since the condition is true, the value is the true alternative, which is nil. 
	Execution does not actually reach here because the expression is compiled 
	in-line."
	^nil!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer with the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."
	^trueAlternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."
	^trueAlternativeBlock value! !

!True methodsFor: 'printing'!

printOn: aStream 
	aStream nextPutAll: 'true'! !

!False methodsFor: 'logical operations'!

& alternativeObject 
	"Evaluating conjunction -- answer false since receiver is false."
	^self!
not
	"Negation -- answer true since the receiver is false."
	^true!
| aBoolean 
	"Evaluating disjunction (OR) -- answer with the argument, aBoolean."
	^aBoolean!
and: alternativeBlock
	"Nonevaluating conjunction -- answer with false since the receiver is false."
	^self!
or: alternativeBlock
	"Nonevaluating disjunction -- answer value of alternativeBlock."
	^alternativeBlock value! !

!False methodsFor: 'controlling'!

ifTrue: alternativeBlock 
	"Since the condition is false, answer the value of the false alternative, 
	which is nil. Execution does not actually reach here because the
	expression is compiled in-line."
	^nil!
ifFalse: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually
	reach here because the expression is compiled in-line."
	^alternativeBlock value!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."
	^falseAlternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."
	^falseAlternativeBlock value! !

!False methodsFor: 'printing'!

printOn: aStream 
	"Print false."
	aStream nextPutAll: 'false'! !

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

!SmallInteger methodsFor: 'arithmetic'!

+ aNumber
    <primitive: 1>
    ^super + aNumber!
- aNumber 
	<primitive: 2>
	^super - aNumber!
* aNumber 
	<primitive: 9>
	^super * aNumber!
/ aNumber 
	<primitive: 10>
	^super / aNumber!
// aNumber 
	<primitive: 12>
	^super // aNumber!
\\ aNumber 
	<primitive: 11>
	^super \\ aNumber!
< aNumber 
	<primitive: 3>
	^super < aNumber!
<= aNumber 
	<primitive: 5>
	^super <= aNumber!
> aNumber 
	<primitive: 4>
	^super > aNumber!
>= aNumber 
	<primitive: 6>
	^super >= aNumber!
= aNumber 
	<primitive: 7>
	^super = aNumber!
~= aNumber 
	<primitive: 8>
	^super ~= aNumber! !

!SmallInteger methodsFor: 'bit manipulation'!

bitAnd: aNumber
	<primitive: 14>
	^super bitAnd: aNumber!
bitOr: aNumber
	<primitive: 15>
	^super bitOr: aNumber!
bitXor: aNumber
	<primitive: 16>
	^super bitXor: aNumber!
bitShift: aNumber
	<primitive: 17>
	^super bitShift: aNumber! !

!SmallInteger methodsFor: 'testing'!

even
	^(self bitAnd: 1) = 0!
odd
	^(self bitAnd: 1) = 1!
positive
	^self > 0!
negative  
	^self < 0!
strictlyPositive
	^self > 0!
sign
	self > 0 ifTrue: [^1].
	self < 0 ifTrue: [^-1].
	^0! !

!SmallInteger methodsFor: 'converting'!

asString
	"Answer a string representation of the receiver."
	<primitive: 105>
	^super asString!
asFloat
	"Create and answer an instance of Float whose value is the value of the receiver."
	<primitive: 40>
	self primitiveFailed!
printString
	"Answer a string representation of the receiver."
	^self asString! !

!SmallInteger methodsFor: 'system primitives'!

asObject
	"This is the inverse of Object.asOop."
	<primitive: 76>
	^self primitiveFailed!
asOop
	"Answer an integer which is unique to me, consistent with Object.asOop."
	self >= 0
		ifTrue: [^ 32768 + self]
		ifFalse: [^ 65536 + self]! !

!SmallInteger class methodsFor: 'class initialization'!

initialize
	"Initialize the SmallInteger class."
	Digitbuffer := String new: 40! !

Integer variableByteSubclass: #LargePositiveInteger
	instanceVariableNames: ''
	classVariableNames: 'MaxHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Integer variableByteSubclass: #LargeNegativeInteger
	instanceVariableNames: ''
	classVariableNames: 'MinHi '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

Magnitude subclass: #Character
	instanceVariableNames: 'value'
	classVariableNames: 'CharacterTable'
	poolDictionaries: ''
	category: 'Collections-Text'!

!Character methodsFor: 'accessing'!

asciiValue
	"Answer the receiver's ASCII numeric code."
	^value!
digitValue
	"Answer 0-9 if the receiver is $0-$9, 10-35 if it is $A-$Z, and < 0 otherwise.
	This is used to parse literal numbers of radix 2-36."
	value <= $9 asciiValue 
		ifTrue: [^value - $0 asciiValue].
	value >= $A asciiValue 
		ifTrue: [value <= $Z asciiValue ifTrue: [^value - $A asciiValue + 10]].
	^-1!
value
	"Answer the receiver's ASCII numeric code."
	^value! !

!Character methodsFor: 'comparing'!

= aCharacter
	"Answer true if the receiver and the argument are the same object (have the 
	same object pointer) and false otherwise.  Optional.  See Object documentation
	whatIsAPrimitive."
	<primitive: 110>
	^self == aCharacter!
< aCharacter
	"Answer true if the receiver's value < aCharacter's value."
	^self asciiValue < aCharacter asciiValue!
<= aCharacter
	"Answer whether the receiver's ASCII value is less than or equal to aCharacter's."
	^value <= aCharacter asciiValue!
> aCharacter
	"Answer true if the receiver's value > aCharacter's value."
	^self asciiValue > aCharacter asciiValue!
>= aCharacter
	"Answer whether the receiver's ASCII value is greater than or equal to aCharacter's."
	^value >= aCharacter asciiValue!
hash
	^value! !

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
	aStream nextPut: $$.
	aStream nextPut: self! !

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
					ifTrue: [^self class value: value+8r40]].
	^self!
asUppercase
	"Answer a Character that is the upper case letter corresponding to the receiver.  If
	the receiver is not a lower case letter, answer the receiver itself."
	8r141 <= value
		ifTrue: [value <= 8r172 
					ifTrue: [^self class value: value-8r40]].
	^self! !

!Character class methodsFor: 'instance creation'!

value: ascii
	"Answer a Character whose ASCII value is ascii."
	<primitive: 170>
	ascii < 0 ifTrue: [self error: 'Character value out of range'].
	ascii > 255 ifTrue: [self error: 'Character value out of range'].
	^CharacterTable at: ascii + 1!

cr
	"Answer the Character representing a carriage return."
	^self value: 13!
lf  
	"Answer the Character representing a line feed."
	^self value: 10!
tab
	"Answer the Character representing a tab."
	^self value: 9!
space
	"Answer the Character representing a space."
	^self value: 32! !

!Character class methodsFor: 'class initialization'!

initialize
	"Initialize the Character table."
	CharacterTable := Array new: 256.
	0 to: 255 do: [:i | CharacterTable at: i + 1 put: (self basicNew value: i)]! !

!UndefinedObject methodsFor: 'testing'!

isNil
	"Answer true: receiver is nil."
	^true!
notNil
	"Answer false: receiver is nil."
	^false! !

!UndefinedObject methodsFor: 'printing'!

printString
	"Answer 'nil'."
	^'nil'! !

Object subclass: #Collection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

Collection variableSubclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

Collection variableByteSubclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

!Collection methodsFor: 'accessing'!

size
	"Answer how many elements the receiver contains."
	<primitive: 62>
	^self basicSize!
isEmpty
	"Answer whether the receiver contains any elements."
	^self size = 0!
notEmpty
	"Answer whether the receiver contains any elements."
	^self isEmpty not! !

!Collection methodsFor: 'enumerating'!

do: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument."
	self subclassResponsibility!
collect: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument. Collect the resulting values into a collection that is like the receiver."
	| newCollection |
	newCollection _ self species new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection!
select: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument. Collect into a new collection those elements for which aBlock evaluates to true."
	| newCollection |
	newCollection _ self species new.
	self do: [:each | (aBlock value: each) ifTrue: [newCollection add: each]].
	^newCollection!
reject: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument. Collect into a new collection those elements for which aBlock evaluates to false."
	^self select: [:each | (aBlock value: each) == false]!
detect: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument. Answer the first element for which aBlock evaluates to true."
	^self detect: aBlock ifNone: [self errorNotFound]!
detect: aBlock ifNone: exceptionBlock
	"Evaluate aBlock with each of the receiver's elements as the argument. Answer the first element for which aBlock evaluates to true."
	self do: [:each | (aBlock value: each) ifTrue: [^each]].
	^exceptionBlock value!
inject: thisValue into: binaryBlock
	"Accumulate a running value associated with evaluating the argument, binaryBlock, with the current value and the receiver as block arguments."
	| nextValue |
	nextValue _ thisValue.
	self do: [:each | nextValue _ binaryBlock value: nextValue value: each].
	^nextValue!
includes: anObject
	"Answer whether anObject is one of the receiver's elements."
	self do: [:each | each = anObject ifTrue: [^true]].
	^false!
occurrencesOf: anObject
	"Answer how many of the receiver's elements are equal to anObject."
	| tally |
	tally _ 0.
	self do: [:each | anObject = each ifTrue: [tally _ tally + 1]].
	^tally! !

!Collection methodsFor: 'adding'!

add: newObject
	"Include newObject as one of the receiver's elements. Answer newObject."
	self subclassResponsibility!
addAll: aCollection
	"Include all the elements of aCollection as the receiver's elements. Answer aCollection."
	aCollection do: [:each | self add: each].
	^aCollection! !

!Collection methodsFor: 'removing'!

remove: oldObject
	"Remove oldObject from the receiver. Answer oldObject unless no such element is found."
	^self remove: oldObject ifAbsent: [self errorNotFound]!
remove: oldObject ifAbsent: anExceptionBlock
	"Remove oldObject from the receiver. If several of the elements are equal to oldObject, only one is removed. If no element is equal to oldObject, answer the result of evaluating anExceptionBlock."
	self subclassResponsibility!
removeAll: aCollection
	"Remove each element of aCollection from the receiver. Answer aCollection."
	aCollection do: [:each | self remove: each].
	^aCollection! !

!Collection methodsFor: 'converting'!

!Collection methodsFor: 'printing'!

printOn: aStream
	"Append a sequence of characters to aStream that identify the receiver."
	| tooMany |
	tooMany _ aStream position + self maxPrint.
	aStream nextPutAll: self class name, ' ('.
	self do:
		[:element |
		aStream position > tooMany ifTrue: [aStream nextPutAll: '...'. aStream nextPut: $). ^self].
		element printOn: aStream.
		aStream space].
	aStream nextPut: $)! !

!Collection methodsFor: 'private'!

species
	"Answer the class to be used for creating new collections like the receiver."
	^self class!
errorNotFound
	"Signal that an object is not found."
	self error: 'Object not found'!
errorNonIntegerIndex
	"Signal that an index is not an integer."
	self error: 'only integers should be used as indices'!
errorSubscriptBounds: index
	"Signal that an index is out of bounds."
	self error: 'subscript is out of bounds: ', index printString!
errorImproperStore
	"Signal that the wrong type of object was stored into a collection."
	self error: 'improper store into indexable object'!
maxPrint
	"Answer the maximum number of characters to use when printing the receiver."
	^5000! !

!Array methodsFor: 'accessing'!

at: index
	"Answer the element at index."
	<primitive: 60>
	^self basicAt: index!
at: index put: anObject
	"Store anObject at index. Answer anObject."
	<primitive: 61>
	^self basicAt: index put: anObject!
size
	"Answer the number of elements in the receiver."
	<primitive: 62>
	^self basicSize! !

!Array methodsFor: 'enumerating'!

do: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument."
	1 to: self size do: [:index | aBlock value: (self at: index)]! !

!Array methodsFor: 'adding'!

add: newObject
	"Arrays cannot grow, so this is an error."
	self error: 'Arrays cannot grow'! !

!Array methodsFor: 'removing'!

remove: oldObject ifAbsent: anExceptionBlock
	"Arrays cannot shrink, so this is an error."
	self error: 'Arrays cannot shrink'! !

!Array methodsFor: 'copying'!

copyWith: anElement
	"Answer a new Array with anElement added at the end."
	| newArray |
	newArray _ self class new: self size + 1.
	1 to: self size do: [:index |
		newArray at: index put: (self at: index)
	].
	newArray at: self size + 1 put: anElement.
	^newArray! !

!Array methodsFor: 'concatenation'!

, anotherArray
	"Answer a new Array containing the elements of the receiver followed by the elements of anotherArray."
	| newArray |
	newArray _ self class new: self size + anotherArray size.
	1 to: self size do: [:index |
		newArray at: index put: (self at: index)
	].
	1 to: anotherArray size do: [:index |
		newArray at: self size + index put: (anotherArray at: index)
	].
	^newArray! !

!Array class methodsFor: 'instance creation'!

new: size
	"Answer a new Array with size elements."
	<primitive: 71>
	^self basicNew: size!

with: anObject
	"Answer a new Array with one element."
	| newArray |
	newArray _ self new: 1.
	newArray at: 1 put: anObject.
	^newArray!

with: firstObject with: secondObject
	"Answer a new Array with two elements."
	| newArray |
	newArray _ self new: 2.
	newArray at: 1 put: firstObject.
	newArray at: 2 put: secondObject.
	^newArray!

with: firstObject with: secondObject with: thirdObject
	"Answer a new Array with three elements."
	| newArray |
	newArray _ self new: 3.
	newArray at: 1 put: firstObject.
	newArray at: 2 put: secondObject.
	newArray at: 3 put: thirdObject.
	^newArray! !

!String methodsFor: 'accessing'!

at: index
	"Answer the character at index."
	<primitive: 63>
	^self basicAt: index!
at: index put: aCharacter
	"Store aCharacter at index. Answer aCharacter."
	<primitive: 64>
	self basicAt: index put: aCharacter.
	^aCharacter!
size
	"Answer the number of characters in the receiver."
	<primitive: 62>
	^self basicSize! !

!String methodsFor: 'enumerating'!

do: aBlock
	"Evaluate aBlock with each of the receiver's characters as the argument."
	1 to: self size do: [:index | aBlock value: (self at: index)]! !

!String methodsFor: 'adding'!

add: newObject
	"Strings cannot grow, so this is an error."
	self error: 'Strings cannot grow'! !

!String methodsFor: 'removing'!

remove: oldObject ifAbsent: anExceptionBlock
	"Strings cannot shrink, so this is an error."
	self error: 'Strings cannot shrink'! !

!String methodsFor: 'converting'!

asSymbol
	"Answer a Symbol whose characters are the characters of the receiver."
	<primitive: 157>
	self primitiveFailed!
asUppercase
	"Answer a String made up from the receiver whose characters are all uppercase."
	| newString |
	newString _ self copy.
	1 to: newString size do: [:i | newString at: i put: (newString at: i) asUppercase].
	^newString!
asLowercase
	"Answer a String made up from the receiver whose characters are all lowercase."
	| newString |
	newString _ self copy.
	1 to: newString size do: [:i | newString at: i put: (newString at: i) asLowercase].
	^newString! !

!String methodsFor: 'comparing'!

= aString
	"Answer whether the receiver and aString are equal."
	<primitive: 158>
	^super = aString!
< aString
	"Answer whether the receiver is lexicographically less than aString."
	<primitive: 159>
	^super < aString!
<= aString
	"Answer whether the receiver is lexicographically less than or equal to aString."
	^(self < aString) or: [self = aString]!
> aString
	"Answer whether the receiver is lexicographically greater than aString."
	^aString < self!
>= aString
	"Answer whether the receiver is lexicographically greater than or equal to aString."
	^(aString < self) or: [self = aString]! !

!String methodsFor: 'copying'!

, aString
	"Answer a new string containing the characters of the receiver followed by the characters of aString."
	<primitive: 156>
	^super , aString! !

! !

Object subclass: #BlockContext
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!BlockContext methodsFor: 'evaluating'!

value
	"Evaluate the receiver block with no arguments."
	<primitive: 81>
	^self primitiveFailed!
value: anObject
	"Evaluate the receiver block with one argument."
	<primitive: 82>
	^self primitiveFailed!
value: firstObject value: secondObject
	"Evaluate the receiver block with two arguments."
	<primitive: 83>
	^self primitiveFailed! !

!BlockContext methodsFor: 'control structures'!

whileTrue: aBlock
	"Repeatedly evaluate the receiver while it returns true, also evaluate aBlock on each iteration."
	<primitive: 86>
	self value ifTrue: [aBlock value. self whileTrue: aBlock]!
whileFalse: aBlock
	"Repeatedly evaluate the receiver while it returns false, also evaluate aBlock on each iteration."
	<primitive: 87>
	self value ifFalse: [aBlock value. self whileFalse: aBlock]!
whileTrue
	"Repeatedly evaluate the receiver while it returns true."
	<primitive: 84>
	self value ifTrue: [self whileTrue]!
whileFalse
	"Repeatedly evaluate the receiver while it returns false."
	<primitive: 85>
	self value ifFalse: [self whileFalse]! !
