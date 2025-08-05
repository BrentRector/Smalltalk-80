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
isInteger
	"Coerces Integers to true and everything else to false. Integer overrides with ^true"
	^false!
isNil
	"Coerces nil to true and everything else to false."
	^false!
notNil
	"Coerces nil to false and everything else to true."
	^true! !

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

!Object methodsFor: 'error handling'!
error: aString 
	"The default behavior for error: is the same as halt:. 
	This additional message is the one a subclass should override in order to
	change the handling of errors."

	self halt: aString!
halt
	"This is a simple message to use for inserting breakpoints during debugging."

	self error: 'Halt encountered.'!
halt: aString 
	"This message can be used for inserting breakpoints during debugging.
	It creates and schedules a Debugger with the argument, aString, as the label."

	self error: aString!
doesNotUnderstand: aMessage 
	"First check for a compound selector.  If found, try copying down code
	into the receiver's class.  If this is unsuccessful,
	announce that the receiver does not understand the argument, aMessage,
	as a message.  The default behavior is to create a Notifier containing the 
	appropriate message and to allow the user to open a Debugger. 
	Subclasses can override this message in order to modify this behavior."

	self error: 'Message not understood: ', aMessage selector!
subclassResponsibility
	"This message sets up a framework for the behavior of the class' subclasses.
	Announce that the subclass should have implemented this message."

	self error: 'My subclass should have overridden one of my messages.'!
shouldNotImplement
	"Announce that although the receiver inherits this message, it
	should not implement it."

	self error: 'This message is not appropriate for this object'!
primitiveFailed
	"Announce that a primitive has failed and there is no appropriate 
	Smalltalk code to run."

	self error: 'a primitive has failed'! !

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
	self subclassResponsibility!
eqv: aBoolean 
	"Answer true if the receiver is equivalent to aBoolean."

	^self == aBoolean!
xor: aBoolean 
	"Exclusive OR -- answer true if the receiver is not equivalent to aBoolean."

	^(self == aBoolean) not! !

!Boolean methodsFor: 'copying'!
deepCopy 
	"Receiver has two concrete subclasses, True and False.
	Only one instance of each should be made, so return self."!
shallowCopy 
	"Receiver has two concrete subclasses, True and False.
	Only one instance of each should be made, so return self."! !

!Boolean methodsFor: 'printing'!
storeOn: aStream
	self printOn: aStream! !

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

!Integer methodsFor: 'testing'!
even
	"Answer whether the receiver is divisible by 2."
	^((self digitAt: 1) bitAnd: 1) = 0!
isInteger
	"Answer true. Integers override Object>>isInteger."
	^true! !

!Number methodsFor: 'arithmetic'!

negated
	"Answer a Number that is the negation of the receiver."
	^0 - self! !

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
	^super ~= aNumber!
hash
	^self!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash! !

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
	Digitbuffer _ String new: 40! !

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
	^self!
asSymbol
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
	CharacterTable _ Array new: 256.
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

ArrayedCollection variableSubclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

ArrayedCollection variableByteSubclass: #String
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

!Collection methodsFor: 'testing'!
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
	^nextValue! !

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

Collection subclass: #SequenceableCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

!SequenceableCollection methodsFor: 'comparing'!
= otherCollection 
	"Answer whether the species of the receiver is the same as otherCollection's species,
	and the receiver's size is the same as otherCollection's size, and each of the receiver's
	elements equal the corresponding element of otherCollection"

	| size index |
	(size _ self size) = otherCollection size ifFalse: [^false].
	self species == otherCollection species ifFalse: [^false].
	index _ 0.
	[(index _ index + 1) <= size]
		whileTrue: [(self at: index) = (otherCollection at: index) ifFalse: [^false]].
	^true! !

!SequenceableCollection methodsFor: 'accessing'!
atAll: anInterval put: anObject 
	"Put anObject at every index specified by the integer elements of anInterval."

	anInterval do: [:index | self at: index put: anObject]!
atAllPut: anObject 
	"Put anObject at every one of the receiver's indices."

	| index size |
	index _ 0.
	size _ self size.
	[(index _ index + 1) <= size]
		whileTrue: [self at: index put: anObject]!
first
	"Answer the first element of the receiver.  Create an error if the receiver
	contains no elements."

	self emptyCheck.
	^self at: 1!
indexOf: anElement 
	"Answer the index of anElement within the receiver.  If the receiver does
	not contain anElement, answer 0."

	^self indexOf: anElement ifAbsent: [0]!
indexOf: anElement ifAbsent: exceptionBlock 
	"Answer the index of anElement within the receiver.  If the receiver does
	not contain anElement, answer the result of evaluating the exceptionBlock."

	(1 to: self size)
		do: [:i | (self at: i) = anElement ifTrue: [^i]].
	^exceptionBlock value!
last
	"Answer the last element of the receiver.  Create an error if the receiver
	contains no elements."

	self emptyCheck.
	^self at: self size!
replaceFrom: start to: stop with: replacement 
	"This destructively replaces elements from start to stop in the receiver.  
	Answer the receiver itself.
	Use copyReplaceFrom:to:with: for insertion/deletion which may alter the 
	size of the result."

	replacement size = (stop - start + 1)
		ifFalse: [self error: 'Size of replacement doesnt match'].
	^self replaceFrom: start to: stop with: replacement startingAt: 1!
replaceFrom: start to: stop with: replacement startingAt: repStart 
	"This destructively replaces elements from start to stop in the receiver
	starting at index, repStart, in the collection, replacement.  Answer the
	receiver.  No range checks are performed - this may be primitively implemented."

	| index repOff |
	repOff _ repStart - start.
	index _ start - 1.
	[(index _ index + 1) <= stop]
		whileTrue: [self at: index put: (replacement at: repOff + index)]!
size
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'adding'!
grow
	"The receiver becomes bigger--this is not a copy of the receiver, so all shared references survive."

	| newArray |
	newArray _ self species new: self size + self growSize.
	newArray replaceFrom: 1 to: self size with: self.
	^self become: newArray! !

!SequenceableCollection methodsFor: 'removing'!
remove: oldObject ifAbsent: anExceptionBlock 
	"SequencableCollections cannot implement removing."

	self shouldNotImplement! !

!SequenceableCollection methodsFor: 'copying'!
, aSequenceableCollection 
	"Answer with a copy of the receiver concatenated with the argument,
	a SequencableCollection."
	
	^self copyReplaceFrom: self size + 1
		  to: self size
		  with: aSequenceableCollection!
copyFrom: start to: stop 
	"Answer a copy of a subset of the receiver, starting from element at index start 
	until element at index stop."

	| newSize |
	newSize _ stop - start + 1.
	^(self species new: newSize)
		replaceFrom: 1
		to: newSize
		with: self
		startingAt: start!
copyReplaceFrom: start to: stop with: replacementCollection 
	"Answer a copy of the receiver satisfying the following conditions:

	If stop is less than start, then this is an insertion;  
		stop should be exactly start-1,  
		start = 1 means insert before the first character,  
		start = size+1 means append after last character.  
	Otherwise, this is a replacement;  
		start and stop have to be within the receiver's bounds."

	| newSequenceableCollection newSize endReplacement |
	newSize _ self size - (stop - start + 1) + replacementCollection size.
	endReplacement _ start - 1 + replacementCollection size.
	newSequenceableCollection _ self species new: newSize.
	newSequenceableCollection
		replaceFrom: 1
		to: start - 1
		with: self
		startingAt: 1.
	newSequenceableCollection
		replaceFrom: start
		to: endReplacement
		with: replacementCollection
		startingAt: 1.
	newSequenceableCollection
		replaceFrom: endReplacement + 1
		to: newSize
		with: self
		startingAt: stop + 1.
	^newSequenceableCollection!
copyWith: newElement 
	"Answer a copy of the receiver that is 1 bigger than the receiver and has 
	newElement at the last element."

	| newIC |
	newIC _ self species new: self size + 1.
	newIC 
		replaceFrom: 1
		to: self size
		with: self
		startingAt: 1.
	newIC at: newIC size put: newElement.
	^newIC!
copyWithout: oldElement 
	"Answer a copy of the receiver in which all occurrences of oldElement
	have been left out."

	| aStream |
	aStream _ WriteStream on: (self species new: self size).
	self do: [:each | oldElement = each ifFalse: [aStream nextPut: each]].
	^aStream contents!
shallowCopy
	^self copyFrom: 1 to: self size! !

!SequenceableCollection methodsFor: 'enumerating'!
collect: aBlock 
	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: [aStream nextPut: (aBlock value: (self at: index))].
	^aStream contents!
do: aBlock 
	| index length |
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: [aBlock value: (self at: index)]!
findFirst: aBlock
	"Return the index of my first element for which aBlock evaluates as true."

	| index |
	index _ 0.
	[(index _ index + 1) <= self size] whileTrue:
		[(aBlock value: (self at: index)) ifTrue: [^index]].
	^ 0!
findLast: aBlock
	"Return the index of my last element for which aBlock evaluates as true."

	| index |
	index _ self size + 1.
	[(index _ index - 1) >= 1] whileTrue:
		[(aBlock value: (self at: index)) ifTrue: [^index]].
	^ 0!
reverse
	"Answer with a new collection like me with its elements in the opposite order."

	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ self size + 1.
	[(index _ index - 1) > 0]
		whileTrue: [aStream nextPut: (self at: index)].
	^aStream contents!
reverseDo: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument, starting
	with the last element and taking each in sequence up to the first.  For
	SequenceableCollections, this is the reverse of the enumeration in do:."

	self size to: 1 by: -1 do: [:index | aBlock value: (self at: index)]!
select: aBlock 
	| aStream index length |
	aStream _ WriteStream on: (self species new: self size).
	index _ 0.
	length _ self size.
	[(index _ index + 1) <= length]
		whileTrue: 
			[(aBlock value: (self at: index)) ifTrue: [aStream nextPut: (self at: index)]].
	^aStream contents!
with: aSequenceableCollection do: aBlock 
	"Evaluate aBlock with each of the receiver's elements along with the corresponding
	element from aSequencableCollection."

	| otherCollection |
	self size ~= aSequenceableCollection size ifTrue: [^self errorNoMatch].
	otherCollection _ ReadStream on: aSequenceableCollection.
	self do: [:each | aBlock value: each value: otherCollection next]! !

!SequenceableCollection methodsFor: 'converting'!
asArray
	"Answer a new instance of Array whose elements are the elements of
	the receiver, in the same order."

	| newArray |
	newArray _ Array new: self size.
	1 to: self size do: [:index | newArray at: index put: (self at: index)].
	^newArray! !

!SequenceableCollection methodsFor: 'private'!
emptyCheck
	"Check that the receiver contains elements."
	self size = 0 ifTrue: [self error: 'attempt to index empty collection']!
errorOutOfBounds
	self error: 'indices are out of bounds'!
errorNoMatch
	self error: 'collections are of different sizes'!
growSize
	"Answer how much larger to make the receiver when it grows."
	^self size max: 4!
swap: oneIndex with: anotherIndex 
	"Move the element at oneIndex to anotherIndex, and vice-versa."

	| element |
	element _ self at: oneIndex.
	self at: oneIndex put: (self at: anotherIndex).
	self at: anotherIndex put: element! !

SequenceableCollection subclass: #ArrayedCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

!ArrayedCollection methodsFor: 'accessing'!
size
	"Answer the number of indexable fields in the receiver. This value is the 
	same as the largest legal subscript. Primitive is specified here to override 
	SequenceableCollection size. Essential. See Object documentation 
	whatIsAPrimitive. "

	<primitive: 62>
	^self basicSize! !

!ArrayedCollection methodsFor: 'adding'!
add: newObject
	self shouldNotImplement! !

!ArrayedCollection methodsFor: 'printing'!
storeOn: aStream 
	aStream nextPutAll: '(('.
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' new: '.
	aStream store: self size.
	aStream nextPut: $).
	(self storeElementsFrom: 1 to: self size on: aStream)
		ifFalse: [aStream nextPutAll: '; yourself'].
	aStream nextPut: $)! !

!ArrayedCollection methodsFor: 'private'!
defaultElement
	^nil!
storeElementsFrom: firstIndex to: lastIndex on: aStream 
	| noneYet defaultElement arrayElement |
	noneYet _ true.
	defaultElement _ self defaultElement.
	firstIndex to: lastIndex do: 
		[:index | 
		arrayElement _ self at: index.
		arrayElement = defaultElement
			ifFalse: 
				[noneYet
					ifTrue: [noneYet _ false]
					ifFalse: [aStream nextPut: $;].
				aStream nextPutAll: ' at: '.
				aStream store: index.
				aStream nextPutAll: ' put: '.
				aStream store: arrayElement]].
	^noneYet! !

!ArrayedCollection class methodsFor: 'instance creation'!
new
	"Answer a new instance of me, with size = 0."
	^self new: 0!
new: size withAll: value 
	"Answer a new instance of me, whose every element is equal to the argument,
	value."

	^(self new: size) atAllPut: value!
with: anObject 
	"Answer a new instance of me, containing only anObject."

	| newCollection |
	newCollection _ self new: 1.
	newCollection at: 1 put: anObject.
	^newCollection!
with: firstObject with: secondObject 
	"Answer a new instance of me, containing firstObject and secondObject."

	| newCollection |
	newCollection _ self new: 2.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject 
	"Answer a new instance of me, containing only these three objects."

	| newCollection |
	newCollection _ self new: 3.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	newCollection at: 3 put: thirdObject.
	^newCollection!
with: firstObject with: secondObject with: thirdObject with: fourthObject 
	"Answer a new instance of me, containing the four arguments as the elements."

	| newCollection |
	newCollection _ self new: 4.
	newCollection at: 1 put: firstObject.
	newCollection at: 2 put: secondObject.
	newCollection at: 3 put: thirdObject.
	newCollection at: 4 put: fourthObject.
	^newCollection! !

SequenceableCollection variableSubclass: #OrderedCollection
	instanceVariableNames: 'firstIndex lastIndex'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

!OrderedCollection methodsFor: 'accessing'!
after: oldObject 
	"Answer the element after oldObject.  If the receiver does not contain oldObject
	or if the receiver contains no elements after oldObject, create an error message."

	| index |
	index _ self find: oldObject.
	index = lastIndex
		ifTrue: [^self errorLastObject]
		ifFalse: [^self basicAt: index + 1]!
at: anInteger 
	"Answer with my element at index anInteger.
	at: is used by a knowledgeable client to access an existing element"

	(anInteger < 1 or: [anInteger + firstIndex - 1 > lastIndex])
		ifTrue: [self errorNoSuchElement]
		ifFalse: [^super at: anInteger + firstIndex - 1]!
at: anInteger put: anObject 
	"Put anObject at element index anInteger.
	at:put: can not be used to append, front or back, to an ordered collection; 
	 it is used by a knowledgeable client to replace an element"

	| index |
	index _ anInteger truncated.
	(index < 1 or: [index + firstIndex - 1 > lastIndex])
		ifTrue: [self errorNoSuchElement]
		ifFalse: [^super at: index + firstIndex - 1 put: anObject]!
before: oldObject 
	"Answer the element before oldObject.  If the receiver does not contain oldObject
	or if the receiver contains no elements before oldObject, create an error message."

	| index |
	index _ self find: oldObject.
	index = firstIndex
		ifTrue: [^self errorFirstObject]
		ifFalse: [^self basicAt: index - 1]!
first
	"Answer the first element.  If the receiver is empty, create an errror message.
	This is a little faster than the implementation in the superclass"

	self emptyCheck.
	^self basicAt: firstIndex!
last
	"Answer the last element.  If the receiver is empty, create an errror message.
	This is a little faster than the implementation in the superclass"

	self emptyCheck.
	^self basicAt: lastIndex!
size
	^lastIndex - firstIndex + 1! !

!OrderedCollection methodsFor: 'copying'!
copyEmpty
	"Answer a copy of the receiver that contains no elements."

	^self species new!
copyFrom: startIndex to: endIndex 
	"Answer a copy of the receiver that contains elements from position startIndex
	to endIndex."

	| targetCollection index |
	endIndex < startIndex ifTrue: [^self species new: 0].
	targetCollection _ self species new: endIndex + 1 - startIndex.
	startIndex to: endIndex do: [:index | targetCollection add: (self at: index)].
	^targetCollection!
copyReplaceFrom: start to: stop with: replacementCollection 
	"Answer a copy of the receiver with replacementCollection's elements
	in place of the receiver's start'th to stop'th elements.
	This does not expect a 1-1 map from replacementCollection to the 
	start to stop elements, so it will do an insert or append."

	| newOrderedCollection delta newIndex index mySize startIndex stopIndex |
	"if start is less than 1, ignore stop and assume this is inserting at the front. 
	if start greater than self size, ignore stop and assume this is appending. 
	otherwise, it is replacing part of me and start and stop have to be within my 
	bounds. "
	delta _ 0.
	startIndex _ start.
	stopIndex _ stop.
	start < 1
		ifTrue: [startIndex _ stopIndex _ 0]
		ifFalse: [startIndex > self size
				ifTrue: [startIndex _ stopIndex _ self size + 1]
				ifFalse: 
					[(stopIndex < (startIndex - 1) or: [stopIndex > self size])
						ifTrue: [self errorOutOfBounds].
					delta _ stopIndex - startIndex + 1]].
	newOrderedCollection _ 
		self species new: self size + replacementCollection size - delta.
	1 to: startIndex - 1 do: [:index | newOrderedCollection add: (self at: index)].
	1 to: replacementCollection size do: 
		[:index | newOrderedCollection add: (replacementCollection at: index)].
	stopIndex + 1 to: self size do: [:index | newOrderedCollection add: (self at: index)].
	^newOrderedCollection!
copyWith: newElement 
	"Answer a copy of the receiver that is 1 bigger than the receiver and 
	includes the argument, newElement, at the end."

	| newCollection |
	newCollection _ self copy.
	newCollection add: newElement.
	^newCollection!
copyWithout: oldElement 
	"Answer a copy of the receiver that does not contain any elements equal
	to oldElement."

	| newCollection each |
	newCollection _ self species new: self size.
	self do: [:each | oldElement = each ifFalse: [newCollection add: each]].
	^newCollection! !

!OrderedCollection methodsFor: 'adding'!
add: newObject 
	^self addLast: newObject!
add: newObject after: oldObject 
	"Add the argument, newObject, as an element of the receiver.  Put it
	in the position just succeeding oldObject.  Answer newObject."
	
	| index |
	index _ self find: oldObject.
	self insert: newObject before: index + 1.
	^newObject!
add: newObject before: oldObject 
	"Add the argument, newObject, as an element of the receiver.  Put it
	in the position just preceding oldObject.  Answer newObject."
	
	| index |
	index _ self find: oldObject.
	self insert: newObject before: index.
	^newObject!
addAll: anOrderedCollection 
	"Add each element of anOrderedCollection at my end.  Answer anOrderedCollection."

	^self addAllLast: anOrderedCollection!
addAllFirst: anOrderedCollection 
	"Add each element of anOrderedCollection at the beginning of the receiver.	
	Answer anOrderedCollection."

	anOrderedCollection reverseDo: [:each | self addFirst: each].
	^anOrderedCollection!
addAllLast: anOrderedCollection 
	"Add each element of anOrderedCollection at the end of the receiver.  Answer
	anOrderedCollection."

	anOrderedCollection do: [:each | self addLast: each].
	^anOrderedCollection!
addFirst: newObject 
	"Add newObject to the beginning of the receiver.  Add newObject."

	firstIndex = 1 ifTrue: [self makeRoomAtFirst].
	firstIndex _ firstIndex - 1.
	self basicAt: firstIndex put: newObject.
	^newObject!
addLast: newObject 
	"Add newObject to the end of the receiver.  Answer newObject."

	lastIndex = self basicSize ifTrue: [self makeRoomAtLast].
	lastIndex _ lastIndex + 1.
	self basicAt: lastIndex put: newObject.
	^newObject!
grow
	"Become larger.
	Typically, a subclass has to override this if the subclass adds instance variables"

	| newSelf |
	newSelf _ self species new: self size + self growSize.
	self do: [:each | newSelf addLast: each].
	self become: newSelf! !

!OrderedCollection methodsFor: 'removing'!
remove: oldObject ifAbsent: absentBlock 
	| index |
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[oldObject = (self basicAt: index)
				ifTrue: 
					[self removeIndex: index.
					^oldObject]
				ifFalse: [index _ index + 1]].
	^absentBlock value!
removeAllSuchThat: aBlock 
	"Evaluate aBlock for each element of the receiver.  Remove each element for
	which aBlock evaluates to true.  
	A subclass might have to override this message to initialize additional instance 
	variables for newCollection"

	| index element newCollection |
	newCollection _ self species new.
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[element _ self basicAt: index.
			(aBlock value: element)
				ifTrue: 
					[newCollection add: element.
					self removeIndex: index]
				ifFalse: [index _ index + 1]].
	^newCollection!
removeFirst
	"Remove the first element of the receiver.  If the receiver is empty, create an error
	message."

	| firstObject |
	self emptyCheck.
	firstObject _ self basicAt: firstIndex.
	self basicAt: firstIndex put: nil.
	firstIndex _ firstIndex + 1.
	^firstObject!
removeLast
	"Remove the last element of the receiver.  If the receiver is empty, create an error
	message."

	| lastObject |
	self emptyCheck.
	lastObject _ self basicAt: lastIndex.
	self basicAt: lastIndex put: nil.
	lastIndex _ lastIndex - 1.
	^lastObject! !

!OrderedCollection methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect the 
	resulting values into a collection that is like me.  Answer with the new 
	collection. Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self species new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection!
do: aBlock 
	"override the superclass for performance"

	| index |
	index _ firstIndex.
	[index <= lastIndex]
		whileTrue: 
			[aBlock value: (self basicAt: index).
			index _ index + 1]!
reverse
	"Answer with a new collection like me with its elements in the opposite order.
	Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self species new.
	self reverseDo: [:each | newCollection add: each].
	^newCollection!
reverseDo: aBlock 
	"override the superclass for performance"

	| index |
	index _ lastIndex.
	[index >= firstIndex]
		whileTrue: 
			[aBlock value: (self basicAt: index).
			index _ index - 1]!
select: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect into a new 
	collection like me, only those elements for which aBlock evaluates to true. 
	Override superclass in order to use add:, not at:put:."

	| newCollection |
	newCollection _ self copyEmpty.
	self do: [:each | (aBlock value: each) ifTrue: [newCollection add: each]].
	^newCollection! !

!OrderedCollection methodsFor: 'private'!
errorFirstObject
	self error: 'specified object is first object'!
errorLastObject
	self error: 'specified object is last object'!
errorNoSuchElement
	self error: 'attempt to index non-existent element in an ordered collection'!
errorNotFound
	self error: 'element not found'!
find: oldObject 
	| index |
	index _ firstIndex.
	[index <= lastIndex and: [oldObject ~= (self basicAt: index)]]
		whileTrue: [index _ index + 1].
	index <= lastIndex
		ifTrue: [^index]
		ifFalse: [self errorNotFound]!
insert: anObject before: spot 
	| index delta spotIndex|
	spotIndex _ spot.
	delta _ spotIndex - firstIndex.
	firstIndex = 1
		ifTrue: 
			[self makeRoomAtFirst.
			spotIndex _ firstIndex + delta].
	index _ firstIndex _ firstIndex - 1.
	[index < (spotIndex - 1)]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + 1).
			index _ index + 1].
	self basicAt: index put: anObject.
	^anObject!
makeRoomAtFirst
	| delta index |
	delta _ self basicSize - self size.
	delta = 0
		ifTrue: 
			[self grow.
			delta _ self basicSize - self size].
	lastIndex = self basicSize ifTrue: [^self].
	"just in case we got lucky"
	index _ self basicSize.
	[index > delta]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index - delta + firstIndex - 1).
			self basicAt: index - delta + firstIndex - 1 put: nil.
			index _ index - 1].
	firstIndex _ delta + 1.
	lastIndex _ self basicSize!
makeRoomAtLast
	| index newLast |
	newLast _ self size.
	self basicSize - self size = 0 ifTrue: [self grow].
	firstIndex = 1 ifTrue: [^self].
	"we might be here under false premises or grow did the job for us"
	index _ 1.
	[index <= newLast]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + firstIndex - 1).
			self basicAt: index + firstIndex - 1 put: nil.
			index _ index + 1].
	firstIndex _ 1.
	lastIndex _ newLast!
removeIndex: removedIndex 
	| index |
	index _ removedIndex.
	[index < lastIndex]
		whileTrue: 
			[self basicAt: index put: (self basicAt: index + 1).
			index _ index + 1].
	self basicAt: lastIndex put: nil.
	lastIndex _ lastIndex - 1!
setIndices
	firstIndex _ self basicSize // 2 max: 1.
	lastIndex _ firstIndex - 1 max: 0! !

!OrderedCollection class methodsFor: 'instance creation'!
new
	^self new: 10!
new: anInteger 
	"If a subclass adds fields, then it is necessary for that subclass to reimplement 
	new: "

	^(super new: anInteger) setIndices! !

Collection variableSubclass: #Set
	instanceVariableNames: 'tally'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Set methodsFor: 'accessing'!
at: index
	self errorNotKeyed!
at: index put: anObject
	self errorNotKeyed!
size
	^tally! !

!Set methodsFor: 'testing'!
includes: anObject 
	^(self basicAt: (self findElementOrNil: anObject)) ~~ nil!
occurrencesOf: anObject 
	(self includes: anObject)
		ifTrue: [^1]
		ifFalse: [^0]! !

!Set methodsFor: 'adding'!
add: newObject 
	| index |
	newObject == nil ifTrue: [^newObject].
	index _ self findElementOrNil: newObject.
	(self basicAt: index) == nil ifTrue: [self atNewIndex: index put: newObject].
	^newObject!
grow
	"The receiver becomes twice as big--this is not a copy of the receiver, so all shared references survive."

	| newSelf |
	newSelf _ self species new: self basicSize + self growSize.
	self do: [:each | newSelf noCheckAdd: each].
	self become: newSelf! !

!Set methodsFor: 'removing'!
remove: oldObject ifAbsent: aBlock 
	| index |
	index _ self find: oldObject ifAbsent: [^aBlock value].
	self basicAt: index put: nil.
	tally _ tally - 1.
	self fixCollisionsFrom: index.
	^oldObject! !

!Set methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of the receiver's elements as the argument.  Collect the 
	resulting values into another Set.  Answer the new Set.  We override the general
	method, so that we make a big enough set and avoid growing. "

	| newSet size index element |
	tally = 0 ifTrue: [^Set new: 2].
	newSet _ Set new: (size _ self basicSize).
	index _ 0.
	[(index _ index + 1) <= size] whileTrue:
		[(element _ self basicAt: index) == nil ifFalse:
			[newSet add: (aBlock value: element)]].
	^newSet!
do: aBlock 
	tally = 0 ifTrue: [^self].
	1 to: self basicSize do: 
		[:index | 
		(self basicAt: index) == nil ifFalse: [aBlock value: (self basicAt: index)]]! !

!Set methodsFor: 'private'!
atNewIndex: index put: anObject 
	self basicAt: index put: anObject.
	tally _ tally + 1.
	self fullCheck!
errorNotKeyed
	self error: 'Sets do not have indexed access'!
find: anObject ifAbsent: aBlock 
	| index |
	index _ self findElementOrNil: anObject.
	(self basicAt: index) == nil
		ifTrue: [^aBlock value]
		ifFalse: [^index]!
findElementOrNil: anObject 
	| index length probe pass |
	length _ self basicSize.
	pass _ 1.
	index _ anObject hash \\ length + 1.
	[(probe _ self basicAt: index) == nil or: [probe = anObject]]
		whileFalse: [(index _ index + 1) > length
				ifTrue: 
					[index _ 1.
					pass _ pass + 1.
					pass > 2 ifTrue: [^self grow findElementOrNil: anObject]]].
	^index!
fixCollisionsFrom: index 
	| myLength oldIndex nextIndex nextObject |
	oldIndex _ index.
	myLength _ self basicSize.
	[oldIndex _ oldIndex \\ myLength + 1.
	nextObject _ self basicAt: oldIndex.
	nextObject == nil]
		whileFalse: 
			[nextIndex _ self findElementOrNil: nextObject.
			nextIndex = oldIndex
				ifFalse: 
					[self basicAt: nextIndex put: nextObject.
					self basicAt: oldIndex put: nil]]!
fullCheck
	self basicSize - self size <= (self basicSize // 4) ifTrue: [self grow]!
noCheckAdd: anObject 
	self basicAt: (self findElementOrNil: anObject)
		put: anObject.
	tally _ tally + 1!
rehash
	| newSelf |
	newSelf _ self species new: self basicSize.
	self do: [:each | newSelf noCheckAdd: each].
	self become: newSelf!
setTally
	tally _ 0!
swap: oneElement with: otherElement 
	| save |
	save _ self basicAt: oneElement.
	self basicAt: oneElement put: (self basicAt: otherElement).
	self basicAt: otherElement put: save! !

!Set class methodsFor: 'instance creation'!
new
	^self new: 2!
new: anInteger 
	^(super new: (anInteger max: 1)) setTally! !

Collection subclass: #Bag
	instanceVariableNames: 'contents'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Bag methodsFor: 'accessing'!
at: index 
	self errorNotKeyed!
at: index put: anObject
	self errorNotKeyed!
size
	| tally |
	tally _ 0.
	contents do: [:each | tally _ tally + each].
	^tally! !

!Bag methodsFor: 'testing'!
includes: anObject 
	^contents includesKey: anObject!
occurrencesOf: anObject 
	(self includes: anObject)
		ifTrue: [^contents at: anObject]
		ifFalse: [^0]! !

!Bag methodsFor: 'adding'!
add: newObject 
	^self add: newObject withOccurrences: 1!
add: newObject withOccurrences: anInteger 
	"Add the element newObject to the elements of the receiver.  Do so as
	though the element were added anInteger number of times.  Answer newObject."

	(self includes: newObject)
		ifTrue: [contents at: newObject put: anInteger + (contents at: newObject)]
		ifFalse: [contents at: newObject put: anInteger].
	^newObject! !

!Bag methodsFor: 'removing'!
remove: oldObject ifAbsent: exceptionBlock 
	| count |
	(self includes: oldObject)
		ifTrue: [(count _ contents at: oldObject) = 1
				ifTrue: [contents removeKey: oldObject]
				ifFalse: [contents at: oldObject put: count - 1]]
		ifFalse: [^exceptionBlock value].
	^oldObject! !

!Bag methodsFor: 'enumerating'!
do: aBlock 
	contents associationsDo: [:assoc | assoc value timesRepeat: [aBlock value: assoc key]]! !

!Bag methodsFor: 'private'!
errorNotKeyed
	self error: 'Bags do not have indexed access'!
setDictionary
	contents _ Dictionary new! !

!Bag class methodsFor: 'instance creation'!
new
	^super new setDictionary! !

SequenceableCollection subclass: #Interval
	instanceVariableNames: 'start stop step'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

!Interval methodsFor: 'comparing'!
= anInterval 
	"Answer true if my species and anInterval species are equal, and
	if our starts, steps and sizes are equal."

	self species == anInterval species
		ifTrue: [^start = anInterval first
					and: [step = anInterval increment and: [self size = anInterval size]]]
		ifFalse: [^false]!
hash
	^(((start hash bitShift: 2)
		bitOr: stop hash)
		bitShift: 1)
		bitOr: self size!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash! !

!Interval methodsFor: 'accessing'!
at: anInteger 
	"Answer the anInteger'th element."

	(anInteger >= 1 and: [anInteger <= self size])
		ifTrue: [^start + (step * (anInteger - 1))]
		ifFalse: [self errorSubscriptBounds: anInteger]!
at: anInteger put: anObject 
	"Storing into an Interval is not allowed."

	self error: 'you can not store into an interval'!
first
	^start!
increment
	"Answer the receiver's interval increment."
	^step!
last
	^stop - (stop - start \\ step)!
size
	step < 0
		ifTrue: [start < stop
				ifTrue: [^0]
				ifFalse: [^stop - start // step + 1]]
		ifFalse: [stop < start
				ifTrue: [^0]
				ifFalse: [^stop - start // step + 1]]! !

!Interval methodsFor: 'adding'!
add: newObject 
	"Adding to an Interval is not allowed."
	self shouldNotImplement! !

!Interval methodsFor: 'removing'!
remove: newObject 
	"Removing from an Interval is not allowed."

	self error: 'elements cannot be removed from an Interval'! !

!Interval methodsFor: 'copying'!
copy
	"Return a copy of me.
	I override this message because my species is Array and copy, as inherited 
	from SequenceableCollection, uses copyFrom:to:, which creates a new object 
	of my species"

	^self shallowCopy! !

!Interval methodsFor: 'enumerating'!
collect: aBlock 
	| nextValue i result |
	result _ self species new: self size.
	nextValue _ start.
	i _ 1.
	step < 0
		ifTrue: [[stop <= nextValue]
				whileTrue: 
					[result at: i put: (aBlock value: nextValue).
					nextValue _ nextValue + step.
					i _ i + 1]]
		ifFalse: [[stop >= nextValue]
				whileTrue: 
					[result at: i put: (aBlock value: nextValue).
					nextValue _ nextValue + step.
					i _ i + 1]].
	^result!
do: aBlock 
	| aValue |
	aValue _ start.
	step < 0
		ifTrue: [[stop <= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue + step]]
		ifFalse: [[stop >= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue + step]]!
reverseDo: aBlock 
	"Evaluate aBlock for each element of my interval, in reverse order."

	| aValue |
	aValue _ stop.
	step < 0
		ifTrue: [[start >= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue - step]]
		ifFalse: [[start <= aValue]
				whileTrue: 
					[aBlock value: aValue.
					aValue _ aValue - step]]! !

!Interval methodsFor: 'printing'!
printOn: aStream 
	aStream nextPut: $(.
	start printOn: aStream.
	aStream nextPutAll: ' to: '.
	stop printOn: aStream.
	step ~= 1
		ifTrue: 
			[aStream nextPutAll: ' by: '.
			step printOn: aStream].
	aStream nextPut: $)!
storeOn: aStream 
	"This is possible because we know numbers store and print the same"

	self printOn: aStream! !

!Interval methodsFor: 'private'!
setFrom: startInteger to: stopInteger by: stepInteger 
	start _ startInteger.
	stop _ stopInteger.
	step _ stepInteger!
species
	^Array! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Interval class
	instanceVariableNames: ''!

!Interval class methodsFor: 'instance creation'!
from: startInteger to: stopInteger 
	"Answer a new instance of me, starting at startInteger, ending and
	stopInteger, and with an interval increment of 1."

	^self new
		setFrom: startInteger
		to: stopInteger
		by: 1!
from: startInteger to: stopInteger by: stepInteger 
	"Answer a new instance of me, starting at startInteger, ending and
	stopInteger, and with an interval increment of stepInteger."

	^self new
		setFrom: startInteger
		to: stopInteger
		by: stepInteger!
new
	"Create and answer with a new instance of the receiver (a class) with no indexable 
	fields.  Fail if the class is indexable.  Override SequenceableCollection new.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 70>
	self isVariable ifTrue: [^self new: 0].
	self primitiveFailed! !

Collection subclass: #MappedCollection
	instanceVariableNames: 'domain map'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

!MappedCollection methodsFor: 'accessing'!
at: anIndex 
	^domain at: (map at: anIndex)!
at: anIndex put: anObject 
	^domain at: (map at: anIndex) put: anObject!
contents
	"Answer the receiver's domain for mapping, a SequenceableCollection."
	^map collect: [:mappedIndex | domain at: mappedIndex]!
size
	^map size! !

!MappedCollection methodsFor: 'adding'!
add: newObject
	self shouldNotImplement! !

!MappedCollection methodsFor: 'copying'!
copy
	"This returns another MappedCollection whereas copyFrom:to: will return an 
	object like my domain"

	^MappedCollection collection: domain map: map! !

!MappedCollection methodsFor: 'printing'!
storeOn: aStream 
	aStream nextPut: $(.
	domain storeOn: aStream.
	aStream nextPutAll: ' mappedBy: '.
	map storeOn: aStream.
	aStream nextPut: $)! !

!MappedCollection methodsFor: 'private'!
setCollection: aCollection map: aDictionary 
	domain _ aCollection.
	map _ aDictionary!
species
	^domain species! !

!MappedCollection methodsFor: 'enumerating'!
collect: aBlock
	| aStream |
	aStream _ WriteStream on: (self species new: self size).
	self do:
		[:domainValue | 
		aStream nextPut: (aBlock value: domainValue)].
	^aStream contents!
do: aBlock
	map do:
		[:mapValue | aBlock value: (domain at: mapValue)]!
select: aBlock
	| aStream |
	aStream _ WriteStream on: (self species new: self size).
	self do:
		[:domainValue | 
		(aBlock value: domainValue)
			ifTrue: [aStream nextPut: domainValue]].
	^aStream contents! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

MappedCollection class
	instanceVariableNames: ''!

!MappedCollection class methodsFor: 'instance creation'!
collection: aCollection map: aSequenceableCollection 
	"Answer a new instance of me that maps aCollection by aSequenceableCollection."
	^self basicNew setCollection: aCollection map: aSequenceableCollection!
new
	self error: 'MappedCollections must be created using the collection:map: message'! !

OrderedCollection variableSubclass: #SortedCollection
	instanceVariableNames: 'sortBlock'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!

!SortedCollection methodsFor: 'comparing'!
= aSortedCollection 
	"Answer true if my and aSortedCollection's species are the same,
	and if our blocks are the same, and if our elements are the same."

	self species = aSortedCollection species ifFalse: [^false].
	sortBlock = aSortedCollection sortBlock
		ifTrue: [^super = aSortedCollection]
		ifFalse: [^false]! !

!SortedCollection methodsFor: 'accessing'!
at: anInteger put: anObject 
	"Storing into a SortedCollection with at:put: is not allowed."

	self error: 'to add to a sorted collection, you must use add:'!
sortBlock
	"Answer the blockContext which is the criterion for sorting elements of
	the receiver."
	^sortBlock!
sortBlock: aBlock 
	"Make the argument, aBlock, be the criterion for ordering elements of the
	receiver."

	sortBlock _ aBlock fixTemps.
	"The sortBlock must copy its home context, so as to avoid circularities!!"
	"Therefore sortBlocks with side effects may not work right"
	self size > 0 ifTrue: [self reSort]! !

!SortedCollection methodsFor: 'copying'!
copy
	| newCollection |
	newCollection _ self species sortBlock: sortBlock.
	newCollection addAll: self.
	^newCollection!
copyEmpty
	"Answer a copy of the receiver without any of the receiver's elements."

	^SortedCollection sortBlock: sortBlock! !

!SortedCollection methodsFor: 'adding'!
add: newObject 
	| nextIndex |
	self isEmpty ifTrue: [^super addLast: newObject].
	nextIndex _ self indexForInserting: newObject.
	self insert: newObject before: nextIndex.
	^newObject!
addAll: aCollection 
	aCollection size > (self size // 3)
		ifTrue: 
			["Faster to add the new elements and resort"
			aCollection do: [:each | super addLast: each].
			self reSort]
		ifFalse: ["Faster to add the elements individually in their proper places"
			aCollection do: [:each | self add: each]]!
grow
	"We must duplicate this message from OrderedCollection so the addLast: won't cause an error."

	| newSelf |
	newSelf _ self species new: self size + self growSize.
	newSelf sortBlock: sortBlock.
	newSelf addAll: self.
	self become: newSelf! !

!SortedCollection methodsFor: 'enumerating'!
collect: aBlock 
	"Evaluate aBlock with each of my elements as the argument.  Collect the 
	resulting values into an OrderedCollection  Answer with the new collection. 
	Override superclass in order to produce OrderedCollection instead of 
	SortedCollection. "

	| newCollection |
	newCollection _ OrderedCollection new.
	self do: [:each | newCollection add: (aBlock value: each)].
	^newCollection! !

!SortedCollection methodsFor: 'private'!
indexForInserting: newObject 
	| index low high |
	low _ firstIndex.
	high _ lastIndex.
	[index _ high + low // 2.
	low > high]
		whileFalse: 
			[(sortBlock value: (self basicAt: index) value: newObject)
				ifTrue: [low _ index + 1]
				ifFalse: [high _ index - 1]].
	^low!
reSort
	self sort: firstIndex to: lastIndex!
sort: i to: j 
	"Sort elements i through j of self to be nondescending according to sortBlock."

	| di dij dj tt ij k l n |
	"The prefix d means the data at that index."
	(n _ j + 1  - i) <= 1 ifTrue: [^self].	"Nothing to sort." 
	 "Sort di,dj."
	di _ self basicAt: i.
	dj _ self basicAt: j.
	(sortBlock value: di value: dj) "i.e., should di precede dj?"
		ifFalse: 
			[self swap: i with: j.
			 tt _ di.
			 di _ dj.
			 dj _ tt].
	n > 2
		ifTrue:  "More than two elements."
			[ij _ (i + j) // 2.  "ij is the midpoint of i and j."
			 dij _ self basicAt: ij.  "Sort di,dij,dj.  Make dij be their median."
			 (sortBlock value: di value: dij) "i.e. should di precede dij?"
			   ifTrue: 
				[(sortBlock value: dij value: dj) "i.e., should dij precede dj?"
				  ifFalse: 
					[self swap: j with: ij.
					 dij _ dj]]
			   ifFalse:  "i.e. di should come after dij"
				[self swap: i with: ij.
				 dij _ di].
			n > 3
			  ifTrue:  "More than three elements."
				["Find k>i and l<j such that dk,dij,dl are in reverse order.
				Swap k and l.  Repeat this procedure until k and l pass each other."
				 k _ i.
				 l _ j.
				 [[l _ l - 1.  k <= l and: [sortBlock value: dij value: (self basicAt: l)]]
				   whileTrue.  "i.e. while dl succeeds dij"
				  [k _ k + 1.  k <= l and: [sortBlock value: (self basicAt: k) value: dij]]
				   whileTrue.  "i.e. while dij succeeds dk"
				  k <= l]
				   whileTrue:
					[self swap: k with: l]. 
	"Now l<k (either 1 or 2 less), and di through dl are all less than or equal to dk
	through dj.  Sort those two segments."
				self sort: i to: l.
				self sort: k to: j]]!
swap: i with: j 
	| t |
	t _ self basicAt: i.
	self basicAt: i put: (self basicAt: j).
	self basicAt: j put: t! !

"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

SortedCollection class
	instanceVariableNames: ''!

!SortedCollection class methodsFor: 'instance creation'!
new: anInteger 
	"The default sorting function is a <= comparison on elements."

	^(super new: anInteger) sortBlock: [:x :y | x <= y]!
sortBlock: aBlock 
	"Answer a new instance of me such that its elements are sorted according
	to the criterion specified in aBlock."

	^(super new: 10) sortBlock: aBlock! !

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

!Array methodsFor: 'comparing'!
hash
	"make sure = arrays hash =ly"

	self size = 0 ifTrue: [^17171].
	^(self at: 1) hash + (self at: self size) hash!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	self size = 0 ifTrue: [^ self hash].
	^ (self first hashMappedBy: map) + (self last hashMappedBy: map)! !

!Array methodsFor: 'converting'!
asArray
	"Answer with the receiver itself."
	^self! !

!Array methodsFor: 'printing'!
isLiteral
	self detect: [:element | element isLiteral not] ifNone: [^true].
	^false!
printOn: aStream
	| tooMany |
	tooMany _ aStream position + self maxPrint.
	aStream nextPut: $(.
	self do: 
		[:element | 
		aStream position > tooMany ifTrue: [aStream nextPutAll: '...etc...)'. ^self].
		element printOn: aStream.
		aStream space].
	aStream nextPut: $)!
storeOn: aStream 
	aStream nextPut: $(.
	self do: [:element | element storeOn: aStream. aStream space].
	aStream nextPut: $)! !

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

Object subclass: #Stream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!Stream methodsFor: 'accessing'!
contents
	"Answer the contents of the receiver."
	self subclassResponsibility!
next
	"Answer the next object in the receiver."
	self subclassResponsibility!
next: anInteger 
	"Answer an Array of the next anInteger number of elements."
	| aCollection |
	aCollection _ Array new: anInteger.
	1 to: anInteger do: [:index | aCollection at: index put: self next].
	^aCollection!
next: anInteger put: anObject 
	"Put anObject into the next anInteger elements of the receiver."

	anInteger timesRepeat: [self nextPut: anObject].
	^anObject!
nextMatchFor: anObject 
	"Gobble the next element and answer whether it is equal to anObject."

	^anObject = self next!
nextPut: anObject 
	"Insert the argument, anObject, at the next position in the receiver.
	Answer anObject."
	self subclassResponsibility!
nextPutAll: aCollection 
	"Append the elements of aCollection onto the receiver.  Answer aCollection."

	aCollection do: [:v | self nextPut: v].
	^aCollection! !

!Stream methodsFor: 'testing'!
atEnd
	"Answer whether the position is greater than or equal to the limit."
	self subclassResponsibility! !

!Stream methodsFor: 'enumerating'!
do: aBlock 
	"Evaluate aBlock for each of the elements of the receiver."

	[self atEnd]
		whileFalse: [aBlock value: self next]! !

Stream subclass: #PositionableStream
	instanceVariableNames: 'collection position readLimit'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!PositionableStream methodsFor: 'accessing'!
contents
	"Answer with a copy of my collection from 1 to readLimit."

	^collection copyFrom: 1 to: readLimit!
next: anInteger 
	"Answer the next anInteger elements of the receiver."
	| newArray |
	newArray _ self contents species new: anInteger.
	1 to: anInteger do: [:index | newArray at: index put: self next].
	^newArray!
peek
	"Answer what would be returned with a self next, without
	changing position.  If the receiver is at the end, answer nil."

	| nextObject |
	self atEnd ifTrue: [^nil].
	nextObject _ self next.
	position _ position - 1.
	^nextObject!
peekFor: anObject 
	"Answer false and do not move the position if self next ~= anObject or if the
	receiver is at the end. Answer true and increment position if self next = anObject."

	| nextObject |
	self atEnd ifTrue: [^false].
	nextObject _ self next.
	"peek for matching element"
	anObject = nextObject ifTrue: [^true].
	"gobble it if found"
	position _ position - 1.
	^false!
through: anObject
	"Answer a subcollection from position to the occurrence (if any, not  
	inclusive) of anObject. If not there, answer everything."

	| newStream element |
	newStream _ WriteStream on: (collection species new: 64).
	[(self atEnd or: [(element _ self next) = anObject]) or: [newStream size > 64000]]
		whileFalse: [newStream nextPut: element].
	self atEnd
		ifFalse:	[newStream nextPut: element].
	^newStream contents!
upTo: anObject 
	"Answer a subcollection from position to the occurrence (if any, not  
	inclusive) of anObject. If not there, answer everything."

	| newStream element |
	newStream _ WriteStream on: (collection species new: 64).
	[self atEnd or: [(element _ self next) = anObject]]
		whileFalse: [newStream nextPut: element].
	^newStream contents! !

!PositionableStream methodsFor: 'testing'!
atEnd
	"Answer true if the position is greater than or equal to the limit, 
	otherwise answer false. Fail if position or readLimit is not a SmallInteger. 
	Optional. See Object documentation whatIsAPrimitive."

	<primitive: 67>
	^position >= readLimit!
isEmpty
	"Answer whether the receiver has no elements."
	^position = 0! !

!PositionableStream methodsFor: 'positioning'!
position
	"Answer the current position of accessing the stream."
	^position!
position: anInteger 
	"Set position to anInteger as long as anInteger is within the bounds of the 
	receiver's contents.  If it is not, cause an error."

	anInteger >= 0 & (anInteger <= readLimit)
		ifTrue: [position _ anInteger]
		ifFalse: [self positionError]!
reset
	"Set the receiver's position to 0."
	position _ 0!
setToEnd
	"Set the position of the receiver to the end of its stream of elements."
	position _ readLimit!
skip: anInteger 
	"Set position to position+anInteger. A subclass might choose to be more 
	helpful and select the minimum of self size and position+anInteger or 
	maximum of 1 and position+anInteger for the repositioning."

	self position: position + anInteger!
skipTo: anObject 
	"Position the receiver past the next occurrance of anObject.  Answer true if 
	anObject is found, false otherwise."

	[self atEnd]
		whileFalse: [self next = anObject ifTrue: [^true]].
	^false! !

!PositionableStream methodsFor: 'fileIn/Out'!
nextChunk
	"Answer the contents of the receiver, up to the next terminator character, with
	double terminators ignored."

	| aStream char terminator |
	terminator _ $!.
	aStream _ WriteStream on: (String new: 200).
	self skipSeparators.
	[(char _ self next)==nil]
		whileFalse: 
			[char == terminator
				ifTrue: [(self peekFor: terminator)
						ifTrue: ["doubled terminator"
							aStream nextPut: char]
						ifFalse: [^aStream contents]]
				ifFalse: [aStream nextPut: char]].
	^aStream contents!
skipSeparators
	"Move the receiver's position past any separators."

	[self atEnd == false and: [self peek isSeparator]]
		whileTrue: [self next]! !

!PositionableStream methodsFor: 'private'!
on: aCollection 
	collection _ aCollection.
	readLimit _ aCollection size.
	position _ 0.
	self reset!
positionError
	"Since I am not necessarily writable, it is up to my subclasses to override 
	position: if expanding the collection is preferrable to giving this error."

	self error: 'Attempt to set the position of a PositionableStream out of bounds'! !

!PositionableStream class methodsFor: 'instance creation'!
on: aCollection 
	"Answer a new instance of me, streaming over aCollection."

	^self basicNew on: aCollection!
on: aCollection from: firstIndex to: lastIndex 
	"Answer a new instance of me, streaming over a copy of aCollection from
	firstIndex to lastIndex."

	^self basicNew on: (aCollection copyFrom: firstIndex to: lastIndex)! !

PositionableStream subclass: #WriteStream
	instanceVariableNames: 'writeLimit'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!WriteStream methodsFor: 'accessing'!
contents
	readLimit _ readLimit max: position.
	^collection copyFrom: 1 to: position!
next
	self shouldNotImplement!
nextPut: anObject 
	"Insert the argument at the next position in the Stream represented by the 
	receiver. Fail if the collection of this stream is not an Array or a String. 
	Fail if the stream is positioned at its end, or if the position is out of 
	bounds in the collection. Fail if the argument is not of the right type for 
	the collection. Optional. See Object documentation whatIsAPrimitive."

	<primitive: 66>
	position = writeLimit
		ifTrue: [^self pastEndPut: anObject]
		ifFalse: 
			[^collection at: (position _ position + 1) put: anObject]!
size
	^readLimit _ readLimit max: position! !

!WriteStream methodsFor: 'positioning'!
position: anInteger 
	readLimit _ readLimit max: position.
	super position: anInteger!
reset
	readLimit _ readLimit max: position.
	position _ 0! !

!WriteStream methodsFor: 'character writing'!
cr
	"Append a return character to the receiver."

	self nextPut: Character cr!
crtab
	"Append a return character, followed by a single tab character, to the receiver."

	self nextPut: Character cr.
	self nextPut: Character tab!
crtab: anInteger 
	"Append a return character, followed by anInteger tab characters, to the receiver."

	self nextPut: Character cr.
	anInteger timesRepeat: [self nextPut: Character tab]!
emphasis
	^ 1
	"Allows compatibility with streams which carry emphasis"!
emphasis: ignored
	"Allows compatibility with streams which carry emphasis"!
space
	"Append a space character to the receiver."

	self nextPut: Character space!
tab
	"Append a tab character to the receiver."

	self nextPut: Character tab! !

!WriteStream methodsFor: 'printing'!
print: anObject 
	"Have anObject print on the receiver."

	anObject printOn: self!
store: anObject 
	"Have anObject print on me for rereading."

	anObject storeOn: self! !

!WriteStream methodsFor: 'fileIn/Out'!
nextChunkPut: aString 
	"Put aString onto self, doubling embedded terminators."

	| index stringSize char terminator |
	terminator _ $!.
	index _ 0.
	stringSize _ aString size.
	[(index _ index + 1) <= stringSize]
		whileTrue: 
			[char _ aString at: index.
			self nextPut: char.
			char == terminator ifTrue: ["double imbedded terminator"
				self nextPut: char]].
	self nextPut: terminator! !

!WriteStream methodsFor: 'private'!
on: aCollection 
	super on: aCollection.
	readLimit _ 0.
	writeLimit _ aCollection size!
on: aCollection from: firstIndex to: lastIndex 
	| len |
	collection _ aCollection.
	readLimit _ 
		firstIndex = 1
			ifTrue: [0]
			ifFalse: [firstIndex - 1].
	writeLimit _ lastIndex.
	position _ readLimit!
pastEndPut: anObject 
	collection _ collection , (collection species with: anObject).
	writeLimit _ collection size.
	^collection at: (position _ position + 1) put: anObject!
growToInclude: index
	"Grow the collection in a WriteStream to include index"
	| newCollection |
	newCollection _ collection class new: (collection size max: index) * 2.
	newCollection replaceFrom: 1 to: collection size with: collection.
	collection _ newCollection.
	writeLimit _ collection size! !

!WriteStream class methodsFor: 'instance creation'!
on: aCollection 
	"Answer a new instance of me, streaming over aCollection."

	^self basicNew on: aCollection!
with: aCollection 
	"Answer an instance of me on a copy of aCollection."
	
	^self on: aCollection copy!
with: aCollection from: firstIndex to: lastIndex 
	"Answer an instance of me on a copy of aCollection from firstIndex to lastIndex."
	
	^self on: (aCollection copyFrom: firstIndex to: lastIndex)! !

PositionableStream subclass: #ReadStream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!ReadStream methodsFor: 'accessing'!
next
	"Answer with the next object in the Stream represented by the receiver. 
	Fail if the collection of this stream is not an Array or a String. Fail if 
	the stream is positioned at its end, or if the position is out of bounds in 
	the collection. Optional. See Object documentation whatIsAPrimitive."

	<primitive: 65>
	position >= readLimit
		ifTrue: [^nil]
		ifFalse: [^collection at: (position _ position + 1)]!
nextPut: anObject 
	self shouldNotImplement! !

!ReadStream methodsFor: 'private'!
on: aCollection from: firstIndex to: lastIndex 
	| len |
	collection _ aCollection.
	readLimit _  lastIndex > (len _ collection size)
						ifTrue: [len]
						ifFalse: [lastIndex].
	position _ firstIndex <= 1
				ifTrue: [0]
				ifFalse: [firstIndex - 1]! !

!ReadStream class methodsFor: 'instance creation'!
on: aCollection from: firstIndex to: lastIndex 
	"Answer with a new instance streaming over a copy of aCollection from
	firstIndex to lastIndex."

	^self basicNew
		on: aCollection
		from: firstIndex
		to: lastIndex! !
