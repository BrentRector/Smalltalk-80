! StandardLibrary.st - Essential Smalltalk-80 Standard Library Classes
! Phase 1 & Phase 2 core classes extracted from authentic Smalltalk-80 source
! 
! This file contains the essential classes needed for basic interpreter execution:
! Phase 1: Object, Boolean, True, False, SmallInteger
! Phase 2: Number, Integer, Collection, Array, String, Class, Behavior
!
! All class definitions are extracted directly from the original Smalltalk-80.st source

nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: 'DependentsFields ErrorRecursion '
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Object comment: 'Object is the superclass of all classes.  It thus provides default behavior common to all objects, such as class access, copying and printing.'!


!Object methodsFor: 'initialize-release'!
release
	"Remove references to objects that may refer to the receiver. Answers self. 
	This message should be overidden by subclasses with any cycles, in which
	case the subclass should also include the expression super release."

	self breakDependents! !

!Object methodsFor: 'accessing'!
at: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass."

	<primitive: 60>
	index isInteger
		ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
at: index put: value 
	"Store the argument value in the indexable field of the receiver indicated by 
	index. Fail if the index is not an Integer or is out of bounds. Or fail if the 
	value is not of the right type for this kind of collection. Answer the 
	value that was stored. Essential. See documentation in Object metaclass."

	<primitive: 61>
	index isInteger
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass.  Do not override this message in any 
	subclass."

	<primitive: 60>
	(index isKindOf: Integer) ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index put: value 
	"Store the second argument value in the indexable field of the receiver 
	indicated by index. Fail if the index is not an Integer or is out of bounds. Or 
	fail if the value is not of the right type for this kind of collection. Answer 
	the value that was stored. Essential. See documentation in Object 
	metaclass. Do not override in a subclass."

	<primitive: 61>
	(index isKindOf: Integer)
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicSize
	"Answer the number of indexable fields in the receiver. This value is the 
	same as the largest legal subscript. Essential. See documentation in Object 
	metaclass. Do not override in any subclass."

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
readFromString: aString
	"Create an object based on the contents of aString."

	^self readFrom: (ReadStream on: aString)!
size
	"Answer the number of indexable fields in the receiver.  This value is the 
	same as the largest legal subscript.  Essential.  See documentation in Object 
	metaclass. "

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isInteger
	"Coerces Integers to true and everything else to false.  Integer
	overrides with ^true"

	^false!
isNil
	"Coerces nil to true and everything else to false.  UndefinedObject 
	overrides with ^true"

	^false!
notNil
	"Coerces nil to false and everything else to true.  UndefinedObject 
	overrides with ^false"

	^true! !

!Object methodsFor: 'comparing'!
= anObject 
	"Answer true if the receiver and the argument represent the same object 
	and false otherwise. If = is redefined in any subclass, consider also
	redefining the message hash."

	^self == anObject!
== anObject 
	"Answer true if the receiver and the argument are the same object (have the same 
	object pointer) and false otherwise.  Do not redefine the message == in any 
	other class!!  Essential.  No Lookup.  See documentation in Object metaclass."

	<primitive: 110>
	self primitiveFailed!
hash
	"Answer a SmallInteger whose value is half of the receiver's object pointer
	(interpreting object pointers as 16-bit signed quantities).  Fails if the 
	receiver is a SmallInteger.  Essential.  See documentation in Object metaclass."

	<primitive: 75>
	self primitiveFailed!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	^ map newHashFor: self hash!
~= anObject 
	"Answer true if the receiver and the argument do not represent the same 
	object and false otherwise."

	^self = anObject == false!
~~ anObject 
	"Answer true if the receiver and the argument are not the same object 
	(have the same object pointer) and false otherwise."

	^(self == anObject) not! !

!Object methodsFor: 'copying'!
copy
	"Answer another instance just like the receiver. Subclasses typically override
	this method;  they typically do not override shallowCopy"

	^self shallowCopy!
deepCopy
	"Answer a copy of the receiver with its own copy of each instance variable."

	| newObject class index |
	class _ self class.
	(class == Object) ifTrue: [^self].
	class isVariable
		ifTrue: 
			[index _ self basicSize.
			newObject _ class basicNew: index.
			[index > 0]
				whileTrue: 
					[newObject basicAt: index put: (self basicAt: index) deepCopy.
					index _ index - 1]]
		ifFalse: [newObject _ class basicNew].
	index _ class instSize.
	[index > 0]
		whileTrue: 
			[newObject instVarAt: index put: (self instVarAt: index) deepCopy.
			index _ index - 1].
	^newObject!
shallowCopy
	"Answer a copy of the receiver which shares the receiver's instance 
	variables. "

	| class newObject index |
	class _ self class.
	"I don't understand why the following check is here.  Object is not 
	supposed to have any instances at all."
	class == Object ifTrue: [^self].
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

!Object methodsFor: 'class membership'!
class
	"Answer the object which is the receiver's class. Essential.  See 
	documentation in Object metaclass."

	<primitive: 111>
	self primitiveFailed!
isKindOf: aClass 
	"Answer a Boolean as to whether the class, aClass, is a superclass or class of
	the receiver."

	self class == aClass
		ifTrue: [^true]
		ifFalse: [^self class inheritsFrom: aClass]!
isMemberOf: aClass 
	"Answer a Boolean as to whether the receiver is an instance of the class, aClass."

	^self class == aClass!
respondsTo: aSymbol 
	"Answer a Boolean as to whether the method dictionary of the receiver's class 
	contains aSymbol as a message selector."

	^self class canUnderstand: aSymbol! !

!Object methodsFor: 'message handling'!
perform: aSymbol 
	"Send the receiver the unary message indicated by the argument. The argument is 
	the selector of the message. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not zero. Optional. See documentation
	in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array new: 0)!
perform: aSymbol with: anObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other argument is the 
	argument of the message to be sent. Invoke messageNotUnderstood: if the 
	number of arguments expected by the selector is not one. Optional. See 
	documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: anObject)!
perform: aSymbol with: firstObject with: secondObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not two. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: firstObject with: secondObject)!
perform: aSymbol with: firstObject with: secondObject with: thirdObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not three. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array
			with: firstObject
			with: secondObject
			with: thirdObject)!
perform: selector withArguments: anArray 
	"Send the receiver the keyword message indicated by the arguments. The argument  
	selector is the selector of the message. The arguments of the message are the 
	elements of anArray. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not the same as the length of 
	anArray. Essential. See documentation in Object metaclass."

	<primitive: 84>
	self primitiveFailed! !

!Object methodsFor: 'error handling'!
error: aString 
	"The default behavior for error: is the same as halt:. 
	This additional message is the one a subclass should override in order to
	change the handling of errors."

	self halt: aString!
halt
	"This is a simple message to use for inserting breakpoints during debugging."

	self halt: 'Halt encountered.'!
halt: aString 
	"This message can be used for inserting breakpoints during debugging."
	
	self primitiveFailed!
primitiveFailed
	"Announce that a primitive has failed and there is no appropriate 
	Smalltalk code to run."

	self error: 'a primitive has failed'!
shouldNotImplement
	"Announce that although the receiver inherits this message, it
	should not implement it."

	self error: 'This message is not appropriate for this object'!
subclassResponsibility
	"This message sets up a framework for the behavior of the class' subclasses.
	Announce that the subclass should have implemented this message."

	self error: 'My subclass should have overridden one of my messages.'! !

!Object methodsFor: 'system primitives'!
asOop
	"Answer an Integer which is unique to me.  See below.
	Essential.  See also documentation in Object metaclass."
	<primitive: 75>
	self primitiveFailed!
become: otherObject 
	"Swap the instance pointers of the receiver and the argument, otherObject.  All
	variables in the entire system that used to point to the receiver now point to the
	argument, and vice-versa.  Fails if either object in a SmallInteger.  Answer the
	argument which is now the same instance pointer that formerly denoted the receiver.  	Essential.  See documentation in Object metaclass."

	<primitive: 72>
	self primitiveFailed!
instVarAt: index 
	"Answer a fixed variable in an object.  The numbering of the variables 
	corresponds to the named instance variables.  Fail if the index is not an 
	Integer or is not the index of a fixed variable.  Essential.  See documentation in 
	Object metaclass."

	<primitive: 73>
	"Access beyond fixed variables."
	^self basicAt: index - self class instSize!
instVarAt: anInteger put: anObject 
	"Store a value into a fixed variable in the receiver.  The numbering of the variables  
	corresponds to the named instance variables.  Fail if the index is not an  
	Integer or is not the index of a fixed variable.   Answer the value stored as the  
	result.  Using this message violates the principle that each object has  
	sovereign control over the storing of values into its instance variables.   
	Essential.  See documentation in Object metaclass."

	<primitive: 74>
	"Access beyond fixed fields"
	^self basicAt: anInteger - self class instSize put: anObject!
nextInstance
	"Answer the next instance after the receiver in the enumeration of all 
	instances of this class.  Fails if all instances have been enumerated.  
	Essential.  See documentation in Object metaclass."

	<primitive: 78>
	^nil! !

!Object methodsFor: 'printing'!
isLiteral
	"Answer whether the receiver has a literal text form recognized by the compiler."

	^false!
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

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self printOn: aStream.
	^aStream contents! !

!Object methodsFor: 'private'!
errorImproperStore
	"Create an error notification that an improper store was attempted."

	self error: 'Improper store into indexable object'!
errorNonIntegerIndex
	"Create an error notification that an improper object was used as an index."

	self error: 'only integers should be used as indices'!
errorSubscriptBounds: index 
	"Create an error notification that an improper integer was used as an index."

	self error: 'subscript is out of bounds: ' , index printString!
species
	"Answer the preferred class for reconstructing the receiver.  For example, 
	collections create new collections whenever enumeration messages such as 
	collect: or select: are invoked.  The new kind of collection is determined by 
	the species of the original collection.  Species and class are not always the 
	same.  For example, the species of Interval is Array."

	^self class! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Object class
	instanceVariableNames: ''!

!Object class methodsFor: 'instance creation'!
new
	"Answer a new initialized instance of the receiver (which is a class) with no 
	indexable variables.  Fail if the class is indexable.  Essential.  See documentation 
	in Object metaclass."

	<primitive: 70>
	^self basicNew!
new: size 
	"Answer an initialized instance of this class with the number of indexable
	variables specified by the argument, size.  Fail if this class is not indexable or if
	the argument is not a positive Integer.  Essential.  See documentation in Object 
	metaclass."

	<primitive: 71>
	^(self basicNew: size)! !

! ====== SmallInteger - Essential integer implementation ======

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
SmallInteger comment:
'SmallIntegers are optimized for the interpreter, stored in two-s complement form.  The allowable range is from minVal to maxVal.'!

!SmallInteger methodsFor: 'arithmetic'!
* aNumber 
	"Multiply the receiver by the argument and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 9>
	self = 0 ifTrue: [^0].
	"This eliminates the need for a self=0 check in LargeInteger *"
	^super * aNumber!
+ aNumber 
	"Add the receiver to the argument and answer with the result if it is a SmallInterger.
	Fail if the argument or the result is not a SmallInteger.  Essential.  No Lookup.  
	See Object documentation whatIsAPrimitive."

	<primitive: 1>
	^super + aNumber!
- aNumber 
	"Subtract the argument from the receiver and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 2>
	^super - aNumber!
/ aNumber 
	"This primitive (for /) divides the receiver by the argument and returns the  
	result if the division is exact.  Fail if the result is not a whole integer.  Fail if the  
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object  
	documentation whatIsAPrimitive."

	<primitive: 10>
	aNumber = 0 ifTrue: [^self error: 'division by 0'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [^(Fraction numerator: self denominator: aNumber) reduced]
		ifFalse: [^super / aNumber]!
// aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the result 
	down towards negative infinity to make it a whole integer.  Fail if the 
	argument is 0 or is not a SmallInteger.  Essential.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 12>
	^super // aNumber"Do with quo: if primitive fails"!
quo: aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the 
	result down towards zero to make it a whole integer.  Fail if the argument is 0 or is 
	not a SmallInteger.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 13>
	aNumber = 0 ifTrue: [^self error: 'Attempt to divide by zero'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [self primitiveFailed]
		ifFalse: [^super quo: aNumber]!
\\ aNumber 
	"Take the receiver modulo the argument.  The result is the remainder rounded 
	towards negative infinity, of the receiver divided by the argument.  Fail if the 
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 11>
	^super \\ aNumber"Do with // if primitive fails"! !

!SmallInteger methodsFor: 'bit manipulation'!
bitAnd: arg 
	"Logical AND the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 14>
	^arg bitAnd: self!
bitOr: arg 
	"Logical OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 15>
	^arg bitOr: self!
bitShift: arg 
	"Answers with a SmallInteger whose value (in twos-complement 
	representation) is the receiver's value (in twos-complement representation) 
	shifted left by the number of bits indicated by the argument.  Negative 
	arguments shift right.  Zeros are shifted in from the right in left shifts.  The sign 
	bit is extended in right shifts.  Fail if the result cannot be represented as a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation  
	whatIsAPrimitive. "

	<primitive: 17>
	^super bitShift: arg!
bitXor: arg 
	"Exclusive OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a 
	SmallInteger.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 16>
	^arg bitXor: self! !

!SmallInteger methodsFor: 'testing'!
even
	^(self bitAnd: 1) = 0!
odd
	^(self bitAnd: 1) = 1! !

!SmallInteger methodsFor: 'comparing'!
< aNumber 
	"Compare the receiver with the argument and answer with true if the receiver is less 
	than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 3>
	^super < aNumber!
<= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is less 
	than or equal to the argument.  Otherwise answer false.  Fail if the argument is 
	not a SmallInteger.  Optional.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 5>
	^super <= aNumber!
= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 7>
	^super = aNumber!
> aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 4>
	^super > aNumber!
>= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than or equal to the argument.  Otherwise answer false.  Fail if the 
	argument is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 6>
	^super >= aNumber!
hash
	^self!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
~= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is not 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 8>
	^super ~= aNumber! !

!SmallInteger methodsFor: 'copying'!
deepCopy!
shallowCopy! !

!SmallInteger methodsFor: 'converting'!
asFloat
	"Create and answer an instance of Float whose value is the value of the receiver.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 40>
	self primitiveFailed! !

!SmallInteger methodsFor: 'printing'!
printOn: aStream base: b 
	| i x |
	(x _ self) < 0
		ifTrue: 
			[aStream nextPut: $-.
			x _ self negated].
	i _ 0.
	[x >= b]
		whileTrue: 
			[Digitbuffer at: (i _ i + 1) put: x \\ b.
			x _ x // b].
	Digitbuffer at: (i _ i + 1) put: x.
	[i > 0]
		whileTrue: 
			[aStream nextPut: (Character digitValue: (Digitbuffer at: i)).
			i _ i - 1]! !

!SmallInteger methodsFor: 'system primitives'!
asObject
	"This is the inverse of Object.asOop; look there for details.
	See also Object documentation whatIsAPrimitive."
	<primitive: 76>
	^self primitiveFailed!
asOop
	"Answer an integer which is unique to me, consistent with Object.asOop.
	See Object.asOop for detailed documentation"
	self >= 0
		ifTrue: [^ 32768 + self]		"0...16K-1 --> 32K...48K-1"
		ifFalse: [^ 65536 + self]		"-16k...-1 --> 48K...64K-1"! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

SmallInteger class
	instanceVariableNames: 'minVal maxVal maxBits maxBytes '!

!SmallInteger class methodsFor: 'class initialization'!
initialize
	"Initialize the digit buffer, and discover the range of
	SmallIntegers on this system."

	| val bits |
	Digitbuffer _ Array new: 32.  "Big enough for 32-bit systems"
	"Discover minVal and maxVal dynamically.  Assume that
	minVal is -2^N and maxVal is (2^N)-1."
	val _ -1.
	bits _ 0.
	[(val subtractOrFail: 1) notNil]
		whileTrue:
			[val _ val + val.
			bits _ bits + 1].
	minVal _ val.
	maxVal _ (val + 1) negated.
	maxBits _ bits.
	maxBytes _ bits + 7 // 8.
	"SmallInteger initialize."! !

!SmallInteger class methodsFor: 'constants'!
maxVal
	"Answer the maximum value for a SmallInteger"
	^maxVal!
minVal
	"Answer the minimum value for a Small Integer"
	^minVal! !

!SmallInteger class methodsFor: 'instance creation'!
new
	self error: 'SmallIntegers can only be created by performing arithmetic'! !

	^nil! !

SmallInteger initialize!

! ====== Number hierarchy - Magnitude, Number, Integer ======

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Magnitudes'!
Magnitude comment: 'I am an abstract class for all objects that have the ability to be compared along a linear dimension, such as dates or times.'!

!Magnitude methodsFor: 'comparing'!
< aMagnitude 
	"Answer whether the receiver is less than the argument."

	^self subclassResponsibility!
<= aMagnitude 
	"Answer whether the receiver is less than or equal to the argument."

	^(self > aMagnitude) not!
= aMagnitude 
	"Compare the receiver with the argument and answer true if the receiver is 
	equal to the argument.  Otherwise answer false."

	^self subclassResponsibility!
> aMagnitude 
	"Answer whether the receiver is greater than the argument."

	^aMagnitude < self!
>= aMagnitude 
	"Answer whether the receiver is greater than or equal to the argument."

	^(self < aMagnitude) not!
between: min and: max 
	"Answer whether the receiver is less than or equal to the argument, max, 
	and greater than or equal to the argument, min."

	^self >= min and: [self <= max]!
max: aMagnitude 
	"Answer the receiver or the argument, whichever is greater."

	self > aMagnitude
		ifTrue: [^self]
		ifFalse: [^aMagnitude]!
min: aMagnitude 
	"Answer the receiver or the argument, whichever is lesser."

	self < aMagnitude
		ifTrue: [^self]
		ifFalse: [^aMagnitude]! !

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Number comment: 'Number is an abstract class for all numeric objects.  Number provides methods for comparing, doing arithmetic, and coercing numbers.'!

!Number methodsFor: 'arithmetic'!
* aNumber 
	"Answer the result of multiplying the receiver by aNumber."
	^self subclassResponsibility!
+ aNumber 
	"Answer the sum of the receiver and aNumber."
	^self subclassResponsibility!
- aNumber 
	"Answer the difference between the receiver and aNumber."
	^self subclassResponsibility!
/ aNumber 
	"Answer the result of dividing the receiver by aNumber."
	^self subclassResponsibility!
// aNumber 
	"Integer quotient defined by division with truncation toward negative infinity.
	9//4 = 2, -9//4 = -3. -0.9//0.4 = -3.  
	Note: this is the remainder version of //, see Integer|// for more 
	details."
	^(self / aNumber) floor!
\\ aNumber 
	"modulo. Remainder defined in terms of //. Answer a Number with the 
	same sign as aNumber. 
	e.g. 9\\4 = 1, -9\\4 = 3, 9\\-4 = -3, 0.9\\0.4 = 0.1."

	^self - (self // aNumber * aNumber)!
abs
	"Answer a Number that is the absolute value (positive magnitude) of the 
	receiver."

	self < 0
		ifTrue: [^self negated]
		ifFalse: [^self]!
negated
	"Answer a Number that is the negation of the receiver."

	^0 - self!
reciprocal
	"Answer 1 divided by the receiver.  Create an error notification if the 
	receiver is 0."

	self = 0 ifTrue: [^self error: 'zero has no reciprocal'].
	^1 / self!
squared
	"Answer the receiver multipled by itself."

	^self * self! !

!Number methodsFor: 'testing'!
even
	"Answer whether the receiver is divisible by 2."

	^self \\ 2 = 0!
negative
	"Answer whether the receiver is mathematically negative."

	^ self < 0!
odd
	"Answer whether the receiver is not divisible by 2."

	^self \\ 2 ~= 0!
positive
	"Answer whether the receiver is positive or equal to 0."

	^ self >= 0!
sign
	"Answer 1 if the receiver is positive, -1 if negative, else 0."

	self > 0 ifTrue: [^1].
	self < 0 ifTrue: [^-1].
	^0!
strictlyPositive
	"Answer whether the receiver is mathematically positive."

	^ self > 0! !

!Number methodsFor: 'converting'!
@ y 
	"Answer a Point whose x value is the receiver and whose y value is the 
	argument.  Optional.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 18>
	^Point x: self y: y!
asFloat
	"Answer a Float that best approximates the receiver."
	^self subclassResponsibility!
asInteger
	"Answer an Integer nearest the receiver toward zero."

	^self truncated! !

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
Integer comment: 'I am an abstract class for all integers.  Integer provides methods for integer arithmetic, bit manipulation, and conversion.'!

!Integer methodsFor: 'arithmetic'!
+ aNumber 
	aNumber isInteger ifTrue: [^self digitAdd: aNumber].
	^aNumber adaptToInteger: self andSend: #+!
- aNumber 
	aNumber isInteger ifTrue: [^self digitSubtract: aNumber].
	^aNumber adaptToInteger: self andSend: #-!
* aNumber 
	aNumber isInteger ifTrue: [^self digitMultiply: aNumber].
	^aNumber adaptToInteger: self andSend: #*!
/ aNumber 
	| quoRem |
	aNumber = 0 ifTrue: [^self error: 'division by zero'].
	(aNumber isInteger) ifFalse: [^aNumber adaptToInteger: self andSend: #/].
	quoRem _ self digitDiv: aNumber abs.
	(quoRem at: 2) = 0
		ifTrue: 
			[quoRem _ quoRem at: 1.
			self negative = aNumber negative
				ifTrue: [^quoRem]
				ifFalse: [^quoRem negated]]
		ifFalse: [^Fraction numerator: self denominator: aNumber]!
gcd: anInteger 
	"Answer the greatest common divisor of the receiver and anInteger."

	| n m |
	n _ self abs.
	m _ anInteger abs.
	[n = 0]
		whileFalse: 
			[n _ m \\ (m _ n)].
	^m! !

!Integer methodsFor: 'bit manipulation'!
bitAnd: n 
	"Answer an Integer whose bits are the logical AND of the receiver's bits 
	and those of the argument, n."
	^self digitLogic: n op: #bitAnd: length: (self digitLength max: n digitLength)!
bitInvert
	"Answer an Integer whose bits are the logical negation of the receiver's bits.
	Numbers are interpreted as having two's-complement representation."

	^-1 - self!
bitOr: n 
	"Answer an Integer whose bits are the logical OR of the receiver's bits and 
	those of the argument, n."
	^self digitLogic: n op: #bitOr: length: (self digitLength max: n digitLength)!
bitShift: shiftCount 
	"Answer an Integer whose value (in twos-complement representation) is 
	the receiver's value (in twos-complement representation) shifted left by the 
	number of bits indicated by the argument.  Negative arguments shift right.  
	Zeros are shifted in from the right in left shifts."
	| rShift |
	shiftCount >= 0 ifTrue: [^self digitLshift: shiftCount].
	rShift _ 0 - shiftCount.
	^(self digitRshift: (rShift bitAnd: 7) 
			bytes: (rShift bitShift: -3)
			lookfirst: self digitLength) normalize!
bitXor: n 
	"Answer an Integer whose bits are the logical XOR of the receiver's bits 
	and those of the argument, n."
	^self digitLogic: n op: #bitXor: length: (self digitLength max: n digitLength)! !

!Integer methodsFor: 'testing'!
isInteger
	^true! !

!Integer methodsFor: 'converting'!
asFloat
	"Answer a Float that best approximates the receiver."

	^self asLimitedPrecisionReal!
asInteger
	"Answer the receiver.  Answer the receiver because it is already an Integer."

	^self! !

!Integer methodsFor: 'printing'!
printOn: aStream 
	"Append a sequence of characters to aStream that describe the receiver."

	self printOn: aStream base: 10!
printOn: aStream base: b 
	"Append a sequence of characters to aStream that describe the receiver in 
	base b."

	| integer next |
	self < 0
		ifTrue: 
			[aStream nextPut: $-.
			integer _ self negated]
		ifFalse: [integer _ self].
	integer < b
		ifTrue: [aStream nextPut: (Character digitValue: integer)]
		ifFalse: 
			[integer printOn: aStream base: b.
			aStream nextPut: (Character digitValue: integer \\ b)]! !

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

!Boolean methodsFor: 'controlling'!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of trueAlternativeBlock if the receiver is true and
	the value of falseAlternativeBlock if the receiver is false.
	If the receiver is neither true nor false, create an error notification."

	self subclassResponsibility!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of falseAlternativeBlock if the receiver is false and
	the value of trueAlternativeBlock if the receiver is true.
	If the receiver is neither true nor false, create an error notification."

	^self ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock!

ifTrue: trueAlternativeBlock 
	"If the receiver is true (i.e., the condition is true), then answer the value of
	the argument, trueAlternativeBlock; otherwise answer nil.
	Create an error notification if the receiver is neither true nor false."

	^self ifTrue: trueAlternativeBlock ifFalse: [nil]!

ifFalse: falseAlternativeBlock 
	"If the receiver is false (i.e., the condition is false), then answer the value of
	the argument, falseAlternativeBlock; otherwise answer nil.
	Create an error notification if the receiver is neither true nor false."

	^self ifTrue: [nil] ifFalse: falseAlternativeBlock! !

!Boolean methodsFor: 'logical operations'!
and: alternativeBlock 
	"Nonevaluating conjunction -- If the receiver is true, answer the value of
	the argument, alternativeBlock; otherwise answer false without
	evaluating the argument."

	^self ifTrue: alternativeBlock ifFalse: [false]!

or: alternativeBlock 
	"Nonevaluating disjunction -- If the receiver is false, answer the value of
	the argument, alternativeBlock; otherwise answer true without
	evaluating the argument."

	^self ifTrue: [true] ifFalse: alternativeBlock! !

!True methodsFor: 'controlling'!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!

ifTrue: trueAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!

ifFalse: falseAlternativeBlock 
	"Since the condition is true, answer the value of the true alternative, 
	which is nil. Execution does not actually reach here because the
	expression is compiled in-line."

	^nil! !

!True methodsFor: 'logical operations'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer the value of alternativeBlock since the receiver is true."

	^alternativeBlock value!

or: alternativeBlock 
	"Nonevaluating disjunction -- answer true since the receiver is true."

	^self! !

!False methodsFor: 'controlling'!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!

ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!

ifTrue: trueAlternativeBlock 
	"Since the condition is false, answer the value of the false alternative, 
	which is nil. Execution does not actually reach here because the
	expression is compiled in-line."

	^nil!

ifFalse: falseAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value! !

!False methodsFor: 'logical operations'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer false since receiver is false."

	^self!

or: alternativeBlock 
	"Nonevaluating disjunction -- answer the value of alternativeBlock since the receiver is false."

	^alternativeBlock value! !


!False methodsFor: 'logical operations'!
& alternativeObject 
	"Evaluating conjunction -- answer false since receiver is false."

	^self!
not
	"Negation -- answer true since the receiver is false."

	^true!
| aBoolean 
	"Evaluating disjunction (OR) -- answer with the argument, aBoolean."

	^aBoolean! !

!False methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer with false since the receiver is false."

	^self!
ifFalse: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually
	reach here because the expression is compiled in-line."

	^alternativeBlock value!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!
ifTrue: alternativeBlock 
	"Since the condition is false, answer the value of the false alternative, 
	which is nil. Execution does not actually reach here because the
	expression is compiled in-line."

	^nil!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer the value of falseAlternativeBlock. Execution does not
	actually reach here because the expression is compiled in-line."

	^falseAlternativeBlock value!
or: alternativeBlock 
	"Nonevaluating disjunction -- answer value of alternativeBlock."

	^alternativeBlock value! !

!False methodsFor: 'printing'!
printOn: aStream 
	"Print false."
	aStream nextPutAll: 'false'! !Object subclass: #File
	instanceVariableNames: 'fileDirectory fileName pageCache serialNumber lastPageNumber binary readWrite error '
	classVariableNames: ''
	poolDictionaries: 'FilePool '
	category: 'Files-Abstract'!
File comment:
'I am an abstract representation of a collection of FilePages and a reference to a FileDirectory which contains me.  My instance is usually uniquely identified both by an alphanumeric name (a String) and a serial number (a ByteArray).

According to this Smalltalk model, a File System consists of coordinated subclass definitions for FilePage, File, and FileDirectory.  User programs typically do not directly access a File or its FilePages but access it as a sequence of characters or bytes through a FileStream.

Instance Variables:
	fileName		<String>
	serialNumber	<ByteArray>
	fileDirectory	<FileDirectory>
	lastPageNumber	<Integer>
	binary			<Boolean> indicates binary or textual contents
	readWrite		<Integer> tells if writing is allowed
	error			<Symbol> an error condition
	pageCache		not currently used.'!


!File methodsFor: 'accessing'!
size
	"Answer the receiver's size in pages;  a file is a collection of pages."

	lastPageNumber isNil 


! ====== SmallInteger - Optimized integer implementation ======

	classVariableNames: 'Digitbuffer '
	poolDictionaries: ''
	category: 'Numeric-Numbers'!
SmallInteger comment:
'SmallIntegers are optimized for the interpreter, stored in two-s complement form.  The allowable range is from minVal to maxVal.'!


!SmallInteger methodsFor: 'arithmetic'!
* aNumber 
	"Multiply the receiver by the argument and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 9>
	self = 0 ifTrue: [^0].
	"This eliminates the need for a self=0 check in LargeInteger *"
	^super * aNumber!
+ aNumber 
	"Add the receiver to the argument and answer with the result if it is a SmallInterger.
	Fail if the argument or the result is not a SmallInteger.  Essential.  No Lookup.  
	See Object documentation whatIsAPrimitive."

	<primitive: 1>
	^super + aNumber!
- aNumber 
	"Subtract the argument from the receiver and answer with the result if it is a 
	SmallInterger.  Fail if the argument or the result is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 2>
	^super - aNumber!
/ aNumber 
	"This primitive (for /) divides the receiver by the argument and returns the  
	result if the division is exact.  Fail if the result is not a whole integer.  Fail if the  
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object  
	documentation whatIsAPrimitive."

	<primitive: 10>
	aNumber = 0 ifTrue: [^self error: 'division by 0'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [^(Fraction numerator: self denominator: aNumber) reduced]
		ifFalse: [^super / aNumber]!
// aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the result 
	down towards negative infinity to make it a whole integer.  Fail if the 
	argument is 0 or is not a SmallInteger.  Essential.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 12>
	^super // aNumber"Do with quo: if primitive fails"!
quo: aNumber 
	"Divide the receiver by the argument and answer with the result.  Round the 
	result down towards zero to make it a whole integer.  Fail if the argument is 0 or is 
	not a SmallInteger.  Optional.  See Object documentation whatIsAPrimitive."

	<primitive: 13>
	aNumber = 0 ifTrue: [^self error: 'Attempt to divide by zero'].
	(aNumber isMemberOf: SmallInteger)
		ifTrue: [self primitiveFailed]
		ifFalse: [^super quo: aNumber]!
\\ aNumber 
	"Take the receiver modulo the argument.  The result is the remainder rounded 
	towards negative infinity, of the receiver divided by the argument.  Fail if the 
	argument is 0 or is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive. "

	<primitive: 11>
	^super \\ aNumber"Do with // if primitive fails"! !

!SmallInteger methodsFor: 'bit manipulation'!
bitAnd: arg 
	"Logical AND the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 14>
	^arg bitAnd: self!
bitOr: arg 
	"Logical OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a SmallInteger.  
	Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 15>
	^arg bitOr: self!
bitShift: arg 
	"Answers with a SmallInteger whose value (in twos-complement 
	representation) is the receiver's value (in twos-complement representation) 
	shifted left by the number of bits indicated by the argument.  Negative 
	arguments shift right.  Zeros are shifted in from the right in left shifts.  The sign 
	bit is extended in right shifts.  Fail if the result cannot be represented as a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation  
	whatIsAPrimitive. "

	<primitive: 17>
	^super bitShift: arg!
bitXor: arg 
	"Exclusive OR the twos-complement representation of the receiver with the 
	argument and answer with the result.  Fail if the argument is not a 
	SmallInteger.  Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 16>
	^arg bitXor: self!
highBit
	| i bit |
	self < 0 ifTrue: [^self error: 'highBit is not defined for negative numbers'].
	self = 0 ifTrue: [^0].
	i _ 1.
	bit _ 1.
	[self > bit]
		whileTrue:
			[i _ i + 1.
			bit _ bit + bit + 1].
	^i! !

!SmallInteger methodsFor: 'testing'!
even
	^(self bitAnd: 1) = 0!
odd
	^(self bitAnd: 1) = 1! !

!SmallInteger methodsFor: 'comparing'!
< aNumber 
	"Compare the receiver with the argument and answer with true if the receiver is less 
	than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 3>
	^super < aNumber!
<= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is less 
	than or equal to the argument.  Otherwise answer false.  Fail if the argument is 
	not a SmallInteger.  Optional.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 5>
	^super <= aNumber!
= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 7>
	^super = aNumber!
> aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation whatIsAPrimitive."

	<primitive: 4>
	^super > aNumber!
>= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is 
	greater than or equal to the argument.  Otherwise answer false.  Fail if the 
	argument is not a SmallInteger.  Optional.  No Lookup.  See Object 
	documentation whatIsAPrimitive."

	<primitive: 6>
	^super >= aNumber!
hash
	^self!
hashMappedBy: map
	"My hash is independent of my oop"
	^ self hash!
~= aNumber 
	"Compare the receiver with the argument and answer true if the receiver is not 
	equal to the argument.  Otherwise answer false.  Fail if the argument is not a 
	SmallInteger.  Essential.  No Lookup.  See Object documentation 
	whatIsAPrimitive. "

	<primitive: 8>
	^super ~= aNumber! !

!SmallInteger methodsFor: 'copying'!
deepCopy!
shallowCopy! !

!SmallInteger methodsFor: 'coercing'!
coerce: n 
	^n truncated!
generality
	^20! !

!SmallInteger methodsFor: 'converting'!
asFloat
	"Create and answer an instance of Float whose value is the value of the receiver.  
	Essential.  See Object documentation whatIsAPrimitive."

	<primitive: 40>
	self primitiveFailed! !

!SmallInteger methodsFor: 'printing'!
printOn: aStream base: b 
	| i x |
	(x _ self) < 0
		ifTrue: 
			[aStream nextPut: $-.
			x _ self negated].
	i _ 0.
	[x >= b]
		whileTrue: 
			[Digitbuffer at: (i _ i + 1) put: x \\ b.
			x _ x // b].
	Digitbuffer at: (i _ i + 1) put: x.
	[i > 0]
		whileTrue: 
			[aStream nextPut: (Character digitValue: (Digitbuffer at: i)).
			i _ i - 1]! !

!SmallInteger methodsFor: 'system primitives'!
asObject
	"This is the inverse of Object.asOop; look there for details.
	See also Object documentation whatIsAPrimitive."
	<primitive: 76>
	^self primitiveFailed!
asOop
	"Answer an integer which is unique to me, consistent with Object.asOop.
	See Object.asOop for detailed documentation"
	self >= 0
		ifTrue: [^ 32768 + self]		"0...16K-1 --> 32K...48K-1"
		ifFalse: [^ 65536 + self]		"-16k...-1 --> 48K...64K-1"!
digitAt: n 
	"Answer the value of an apparent indexable field.
	This is provided for compatibility with LargeInteger."

	 n = 1
		ifTrue: 
			["Negate carefully in case I am SmallInteger minVal"
			self < 0
				ifTrue: [^-256 - self bitAnd: 255].
			^self bitAnd: 255]
		ifFalse:
			[self < 0
				ifTrue: [^(-256 - self bitShift: -8) + 1 digitAt: n - 1].
			^(self bitShift: 8 - (n bitShift: 3)) bitAnd: 255]!
digitAt: n put: value 
	"Fails. The digits of a small integer can not be modified."

	self error: 'You cant store in a SmallInteger'!
digitLength
	"Answer with the number of indexable fields in the receiver.  This value is the
	same as the largest legal subscript.  Included so that a SmallInteger can behave
	like a LargeInteger."

	| maxSize minValue size |
	(self < 16r100 and: [self > -16r100]) ifTrue: [^1].
	maxSize _ SmallInteger maxBytes.
	maxSize = 2 ifTrue: [^2].  "Make things go fast for 16-bit systems"
	minValue _ -16r100.
	size _ 2.
	[size < maxSize]
		whileTrue:
			[minValue _ minValue bitShift: 8.
			(self <= (-1 - minValue) and: [self > minValue]) ifTrue: [^size].
			size _ size + 1].
	^maxSize!
instVarAt: i 
	"small integer has to be specially handled"

	i = 1 ifTrue: [^self].
	self error: 'argument too big for small integer instVarAt:'! !

!SmallInteger methodsFor: 'private'!
fromString: str radix: radix 
	| maxdigit c val |
	maxdigit _ 
		radix + (radix > 10
					ifTrue: [55 - 1]
					ifFalse: [48 - 1]).
	val _ 0.
	1 to: str size do: 
		[:i | 
		c _ str at: i.
		(c < 48 ifFalse: [c > maxdigit])
			ifTrue: [^false].
		val _ val * radix + (c <= 57
							ifTrue: [c - 48]
							ifFalse: 
								[c < 65 ifTrue: [^false].
								c - 55])].
	^val!
subtractOrFail: aNumber 
	"This is a private copy of the subtraction primitive,
	used by SmallInteger class initialize to discover the
	correct value of SmallInteger minVal."

	<primitive: 2>
	^nil! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

SmallInteger class
	instanceVariableNames: 'minVal maxVal maxBits maxBytes '!


!SmallInteger class methodsFor: 'class initialization'!
initialize
	"Initialize the digit buffer, and discover the range of
	SmallIntegers on this system."

	| val bits |
	Digitbuffer _ Array new: 32.  "Big enough for 32-bit systems"
	"Discover minVal and maxVal dynamically.  Assume that
	minVal is -2^N and maxVal is (2^N)-1."
	val _ -1.
	bits _ 0.
	[(val subtractOrFail: 1) notNil]
		whileTrue:
			[val _ val + val.
			bits _ bits + 1].
	minVal _ val.
	maxVal _ (val + 1) negated.
	maxBits _ bits.
	maxBytes _ bits + 7 // 8.
	"SmallInteger initialize."! !

!SmallInteger class methodsFor: 'constants'!
maxBits
	"Answer N such that maxVal is (2^N)-1."
	^maxBits!
maxBytes
	"Answer N such that maxVal < 256^N."
	^maxBytes!
maxVal
	"Answer the maximum value for a SmallInteger"
	^maxVal!
minVal
	"Answer the minimum value for a Small Integer"
	^minVal! !

!SmallInteger class methodsFor: 'documentation'!
guideToDivision
	"Handy guide to the kinds of Integer division: 
	/  exact division, returns a fraction if result is not a whole integer. 
	//  returns an Integer, rounded towards negative infinity. 
	\\ is modulo rounded towards negative infinity. 
	quo:  truncated division, rounded towards zero."! !

!SmallInteger class methodsFor: 'instance creation'!
new
	self error: 'SmallIntegers can only be created by performing arithmetic'! !

SmallInteger initialize!
OrderedCollection variableSubclass: #SortedCollection
	instanceVariableNames: 'sortBlock '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Sequenceable'!
SortedCollection comment: 'I am an ordered collection of elements, sorted via the function sortBlock.'!


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


! ====== Object - Root class of all objects ======

	instanceVariableNames: ''
	classVariableNames: 'DependentsFields ErrorRecursion '
	poolDictionaries: ''
	category: 'Kernel-Objects'!
Object comment: 'Object is the superclass of all classes.  It thus provides default behavior common to all objects, such as class access, copying and printing.'!


!Object methodsFor: 'initialize-release'!
release
	"Remove references to objects that may refer to the receiver. Answers self. 
	This message should be overidden by subclasses with any cycles, in which
	case the subclass should also include the expression super release."

	self breakDependents! !

!Object methodsFor: 'accessing'!
at: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass."

	<primitive: 60>
	index isInteger
		ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
at: index put: value 
	"Store the argument value in the indexable field of the receiver indicated by 
	index. Fail if the index is not an Integer or is out of bounds. Or fail if the 
	value is not of the right type for this kind of collection. Answer the 
	value that was stored. Essential. See documentation in Object metaclass."

	<primitive: 61>
	index isInteger
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self at: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index 
	"Answer the value of an indexable field in the receiver. Fail if the 
	argument index is not an Integer or is out of bounds. Essential. See 
	documentation in Object metaclass.  Do not override this message in any 
	subclass."

	<primitive: 60>
	(index isKindOf: Integer) ifTrue: [self errorSubscriptBounds: index].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated]
		ifFalse: [self errorNonIntegerIndex]!
basicAt: index put: value 
	"Store the second argument value in the indexable field of the receiver 
	indicated by index. Fail if the index is not an Integer or is out of bounds. Or 
	fail if the value is not of the right type for this kind of collection. Answer 
	the value that was stored. Essential. See documentation in Object 
	metaclass. Do not override in a subclass."

	<primitive: 61>
	(index isKindOf: Integer)
		ifTrue: [(index >= 1 and: [index <= self size])
					ifTrue: [self errorImproperStore]
					ifFalse: [self errorSubscriptBounds: index]].
	(index isKindOf: Number)
		ifTrue: [^self basicAt: index truncated put: value]
		ifFalse: [self errorNonIntegerIndex]!
basicSize
	"Answer the number of indexable fields in the receiver. This value is the 
	same as the largest legal subscript. Essential. See documentation in Object 
	metaclass. Do not override in any subclass."

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
readFromString: aString
	"Create an object based on the contents of aString."

	^self readFrom: (ReadStream on: aString)!
size
	"Answer the number of indexable fields in the receiver.  This value is the 
	same as the largest legal subscript.  Essential.  See documentation in Object 
	metaclass. "

	<primitive: 62>
	"The number of indexable fields of fixed-length objects is 0"
	^0!
yourself
	"Answer self."! !

!Object methodsFor: 'testing'!
isInteger
	"Coerces Integers to true and everything else to false.  Integer
	overrides with ^true"

	^false!
isNil
	"Coerces nil to true and everything else to false.  UndefinedObject 
	overrides with ^true"

	^false!
notNil
	"Coerces nil to false and everything else to true.  UndefinedObject 
	overrides with ^false"

	^true! !

!Object methodsFor: 'comparing'!
= anObject 
	"Answer true if the receiver and the argument represent the same object 
	and false otherwise. If = is redefined in any subclass, consider also
	redefining the message hash."

	^self == anObject!
== anObject 
	"Answer true if the receiver and the argument are the same object (have the same 
	object pointer) and false otherwise.  Do not redefine the message == in any 
	other class!!  Essential.  No Lookup.  See documentation in Object metaclass."

	<primitive: 110>
	self primitiveFailed!
hash
	"Answer a SmallInteger whose value is half of the receiver's object pointer
	(interpreting object pointers as 16-bit signed quantities).  Fails if the 
	receiver is a SmallInteger.  Essential.  See documentation in Object metaclass."

	<primitive: 75>
	self primitiveFailed!
hashMappedBy: map
	"Answer what my hash would be if oops changed according to map"
	^ map newHashFor: self hash!
~= anObject 
	"Answer true if the receiver and the argument do not represent the same 
	object and false otherwise."

	^self = anObject == false!
~~ anObject 
	"Answer true if the receiver and the argument are not the same object 
	(have the same object pointer) and false otherwise."

	^(self == anObject) not! !

!Object methodsFor: 'copying'!
copy
	"Answer another instance just like the receiver. Subclasses typically override
	this method;  they typically do not override shallowCopy"

	^self shallowCopy!
deepCopy
	"Answer a copy of the receiver with its own copy of each instance variable."

	| newObject class index |
	class _ self class.
	(class == Object) ifTrue: [^self].
	class isVariable
		ifTrue: 
			[index _ self basicSize.
			newObject _ class basicNew: index.
			[index > 0]
				whileTrue: 
					[newObject basicAt: index put: (self basicAt: index) deepCopy.
					index _ index - 1]]
		ifFalse: [newObject _ class basicNew].
	index _ class instSize.
	[index > 0]
		whileTrue: 
			[newObject instVarAt: index put: (self instVarAt: index) deepCopy.
			index _ index - 1].
	^newObject!
shallowCopy
	"Answer a copy of the receiver which shares the receiver's instance 
	variables. "

	| class newObject index |
	class _ self class.
	"I don't understand why the following check is here.  Object is not 
	supposed to have any instances at all."
	class == Object ifTrue: [^self].
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

!Object methodsFor: 'dependents access'!
addDependent: anObject
	"Add anObject as one of the receiver's dependents."
	(DependentsFields at: self ifAbsent: [self setDependents])
		add: anObject.
	^anObject!
dependents 
	"Answer an OrderedCollection of the objects that are dependent on the
	receiver, that is, the objects that should be notified if the receiver changes."

	^ DependentsFields at: self ifAbsent: [OrderedCollection new]!
removeDependent: anObject
	"Remove the argument, anObject, as one of the receiver's dependents."
	| dependents |
	dependents _ DependentsFields at: self ifAbsent: [^ anObject].
	dependents remove: anObject ifAbsent: [].
	dependents isEmpty ifTrue: [self breakDependents].
	^anObject! !

!Object methodsFor: 'updating'!
broadcast: aSymbol 
	"Send the argument, aSymbol, as a unary message to all of the receiver's dependents."

	self dependents ~~ nil
		ifTrue: [self dependents do: 
					[:aDependent | aDependent perform: aSymbol]]!
broadcast: aSymbol with: anObject 
	"Send the argument, aSymbol, as a keyword message with argument anObject to
	all of the receiver's dependents."

	self dependents ~~ nil
		ifTrue: [self dependents do:
					[:aDependent | aDependent perform: aSymbol with: anObject]]!
changed
	"Receiver changed in a general way; inform all the dependents by sending 
	each dependent an update: message."

	self changed: self!
changed: aParameter 
	"Receiver changed.  The change is denoted by the argument aParameter.
	Usually the argument is a Symbol that is part of the dependent's change
	protocol.  Inform all of the dependents."

	self dependents do: [:aDependent | aDependent update: aParameter]!
changeRequest
	"Receiver wants to change; check with all dependents that it is OK."

	self dependents do: [:dep | dep updateRequest ifFalse: [^false]].
	^true!
changeRequestFrom: requestor
	"Receiver wants to change; check with all dependents (other than requestor) that it is OK."

	self dependents do: [:dep | (dep == requestor or: [dep updateRequest]) ifFalse: [^false]].
	^true!
update: aParameter 
	"Receive a change notice from an object of whom the receiver is a dependent. 
	The default behavior is to do nothing;  a subclass might want to change
	itself in some way."

	^self!
updateRequest
	"Default behavior is to grant update requests;  a subclass might want to override
	this behavior if it is in the middle of making another change."

	^ true! !

!Object methodsFor: 'printing'!
isLiteral
	"Answer whether the receiver has a literal text form recognized by the compiler."

	^false!
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

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self printOn: aStream.
	^aStream contents!
storeOn: aStream 
	"Append to the argument aStream a sequence of characters that is an expression 
	whose evaluation creates an object similar to the receiver."

	aStream nextPut: $(.
	self class isVariable
		ifTrue: [aStream nextPutAll: '(', self class name, ' basicNew: ';
					store: self basicSize;
					nextPutAll: ') ']
		ifFalse: [aStream nextPutAll: self class name, ' basicNew'].
	1 to: self class instSize do:
		[:i |
		aStream nextPutAll: ' instVarAt: ';
			store: i;
			nextPutAll: ' put: ';
			store: (self instVarAt: i);
			nextPut: $;].
	1 to: self basicSize do:
		[:i |
		aStream nextPutAll: ' basicAt: ';
			store: i;
			nextPutAll: ' put: ';
			store: (self basicAt: i);
			nextPut: $;].
	aStream nextPutAll: ' yourself)'!
storeString
	"Answer a String representation of the receiver from which the receiver
	can be reconstructed."

	| aStream |
	aStream _ WriteStream on: (String new: 16).
	self storeOn: aStream.
	^aStream contents! !

!Object methodsFor: 'class membership'!
class
	"Answer the object which is the receiver's class. Essential.  See 
	documentation in Object metaclass."

	<primitive: 111>
	self primitiveFailed!
isKindOf: aClass 
	"Answer a Boolean as to whether the class, aClass, is a superclass or class of
	the receiver."

	self class == aClass
		ifTrue: [^true]
		ifFalse: [^self class inheritsFrom: aClass]!
isMemberOf: aClass 
	"Answer a Boolean as to whether the receiver is an instance of the class, aClass."

	^self class == aClass!
respondsTo: aSymbol 
	"Answer a Boolean as to whether the method dictionary of the receiver's class 
	contains aSymbol as a message selector."

	^self class canUnderstand: aSymbol! !

!Object methodsFor: 'message handling'!
perform: aSymbol 
	"Send the receiver the unary message indicated by the argument. The argument is 
	the selector of the message. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not zero. Optional. See documentation
	in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array new: 0)!
perform: aSymbol with: anObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other argument is the 
	argument of the message to be sent. Invoke messageNotUnderstood: if the 
	number of arguments expected by the selector is not one. Optional. See 
	documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: anObject)!
perform: aSymbol with: firstObject with: secondObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not two. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array with: firstObject with: secondObject)!
perform: aSymbol with: firstObject with: secondObject with: thirdObject 
	"Send the receiver the keyword message indicated by the arguments. The first 
	argument is the selector of the message. The other arguments are the 
	arguments of the message to be sent. Invoke messageNotUnderstood: if 
	the number of arguments expected by the selector is not three. Optional. 
	See documentation in Object metaclass."

	<primitive: 83>
	^self perform: aSymbol withArguments: (Array
			with: firstObject
			with: secondObject
			with: thirdObject)!
perform: selector withArguments: anArray 
	"Send the receiver the keyword message indicated by the arguments. The argument  
	selector is the selector of the message. The arguments of the message are the 
	elements of anArray. Invoke messageNotUnderstood: if the number of 
	arguments expected by the selector is not the same as the length of 
	anArray. Essential. See documentation in Object metaclass."

	<primitive: 84>
	self primitiveFailed! !

!Object methodsFor: 'error handling'!
confirm: aString 
	"Create and start up a BinaryChoice menu with the argument as the message in order
	to determine true or false.  Answers true or false."
	| answer |
	answer _ false.
	BinaryChoice
		message: aString
		displayAt: Sensor cursorPoint
		centered: true
		ifTrue: [answer _ true]
		ifFalse: [answer _ false].
	^answer!
conflictingInheritanceError
	"Browse to the method which called this,
		redefine it appropriately,
		and then restart that calling method. "

	self error: 'Conflicting methods due to multiple inheritance'!
doesNotUnderstand: aMessage 
	"First check for a compound selector.  If found, try copying down code
	into the receiver's class.  If this is unsuccessful,
	announce that the receiver does not understand the argument, aMessage,
	as a message.  The default behavior is to create a Notifier containing the 
	appropriate message and to allow the user to open a Debugger. 
	Subclasses can override this message in order to modify this behavior."
	| status gripe |

	status _ self class tryCopyingCodeFor: aMessage selector.
	status==#OK ifTrue:
		[^self perform: aMessage selector withArguments: aMessage arguments].

	gripe _ status==#HierarchyViolation
		ifTrue: [aMessage selector classPart , ' is not one of my superclasses: ']
		ifFalse: ['Message not understood: '].
	NotifierView
		openContext: thisContext
		label: gripe , aMessage selector
		contents: thisContext shortStack.
	"Try the message again if the programmer decides to proceed."
	^self perform: aMessage selector withArguments: aMessage arguments

	"3 zork."!
error: aString 
	"The default behavior for error: is the same as halt:. 
	This additional message is the one a subclass should override in order to
	change the handling of errors."

	NotifierView
		openContext: thisContext
		label: aString
		contents: thisContext shortStack

	"nil error: 'error message'."!
halt
	"This is a simple message to use for inserting breakpoints during debugging."

	NotifierView
		openContext: thisContext
		label: 'Halt encountered.'
		contents: thisContext shortStack

	"nil halt."!
halt: aString 
	"This message can be used for inserting breakpoints during debugging.
	It creates and schedules a Debugger with the argument, aString, as the label."

	NotifierView
		openContext: thisContext
		label: aString
		contents: thisContext shortStack

	"nil halt: 'Test of halt:.'."!
notify: aString 
	"Create and schedule a Notifier with the argument as the message in order
	to request confirmation before a process can proceed."


	NotifierView
		openContext: thisContext
		label: 'Notifier'
		contents: aString

	"nil notify: 'confirmation message'."!
primitiveFailed
	"Announce that a primitive has failed and there is no appropriate 
	Smalltalk code to run."

	self error: 'a primitive has failed'!
shouldNotImplement
	"Announce that although the receiver inherits this message, it
	should not implement it."

	self error: 'This message is not appropriate for this object'!
subclassResponsibility
	"This message sets up a framework for the behavior of the class' subclasses.
	Announce that the subclass should have implemented this message."

	self error: 'My subclass should have overridden one of my messages.'! !

!Object methodsFor: 'user interface'!
basicInspect
	"Create and schedule an Inspector in which the user can examine the
	receiver's variables.  This method should not be overwritten."

	InspectorView open: (Inspector inspect: self)!
inspect
	"Create and schedule an Inspector in which the user can examine the
	receiver's variables."
	self basicInspect! !

!Object methodsFor: 'system primitives'!
asOop
	"Answer an Integer which is unique to me.  See below.
	Essential.  See also documentation in Object metaclass."
	<primitive: 75>
	self primitiveFailed
	"
	Non-Stretch mapping between objects and asOop value:
	oops 0...16K-1			-->	0...16K-1
	oops 16K...32K-1			-->	-16K...-1
	SmallIntegers 0...16K		-->	32K...48K-1
	SmallIntegers -16K...0	-->	48K...64K-1

	Stretch mapping between objects and asOop value:
	oops 0...48K-1			-->	0...48K-1
	SmallIntegers -8K...-1	-->	-16K...-2 even
	SmallIntegers 0...8K-1	-->	-(16K-1)...-1 odd

	Non-Stretch Consistency check:
	| obj |
	-16384 to: 16383 do:
		[:i | obj _ i asObject.
		(obj == #NonExistentObject or: [obj asOop = i])
			ifFalse: [self error: 'inconsistency']].
	32768 to: 65536 do:
		[:i | obj _ i asObject.
		(obj == #NonExistentObject or: [obj asOop = i])
			ifFalse: [self error: 'inconsistency']].

	Stretch Consistency check:
	| obj |
	-16384 to: 49151 do:
		[:i | obj _ i asObject.
		(obj == #NonExistentObject or: [obj asOop = i])
			ifFalse: [self error: 'inconsistency']].
	"!
become: otherObject 
	"Swap the instance pointers of the receiver and the argument, otherObject.  All
	variables in the entire system that used to point to the receiver now point to the
	argument, and vice-versa.  Fails if either object in a SmallInteger.  Answer the
	argument which is now the same instance pointer that formerly denoted the receiver.  	Essential.  See documentation in Object metaclass."

	<primitive: 72>
	self primitiveFailed!
instVarAt: index 
	"Answer a fixed variable in an object.  The numbering of the variables 
	corresponds to the named instance variables.  Fail if the index is not an 
	Integer or is not the index of a fixed variable.  Essential.  See documentation in 
	Object metaclass."

	<primitive: 73>
	"Access beyond fixed variables."
	^self basicAt: index - self class instSize!
instVarAt: anInteger put: anObject 
	"Store a value into a fixed variable in the receiver.  The numbering of the variables  
	corresponds to the named instance variables.  Fail if the index is not an  
	Integer or is not the index of a fixed variable.   Answer the value stored as the  
	result.  Using this message violates the principle that each object has  
	sovereign control over the storing of values into its instance variables.   
	Essential.  See documentation in Object metaclass."

	<primitive: 74>
	"Access beyond fixed fields"
	^self basicAt: anInteger - self class instSize put: anObject!
nextInstance
	"Answer the next instance after the receiver in the enumeration of all 
	instances of this class.  Fails if all instances have been enumerated.  
	Essential.  See documentation in Object metaclass."

	<primitive: 78>
	^nil! !

!Object methodsFor: 'system simulation'!
tryPrimitive0
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses to execute primitives with no arguments.  See 
	ContextPart class initPrimitives and ContextPart doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive1: arg1 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with one argument.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive2: arg1 with: arg2 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with two arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive3: arg1 with: arg2 with: arg3 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with three arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail!
tryPrimitive4: arg1 with: arg2 with: arg3 with: arg4 
	"Warning!!!! This is not a real primitive.  This method is a template that the 
	Smalltalk simulator uses execute primitives with four arguments.  See 
	ContextPart class|initPrimitives and ContextPart|doPrimitive:receiver:args:."

	<primitive: 007>
	^#primitiveFail! !

!Object methodsFor: 'private'!
breakDependents
	"Deallocate the soft field for the receiver's dependents."

	DependentsFields removeKey: self ifAbsent: []!
errorImproperStore
	"Create an error notification that an improper store was attempted."

	self error: 'Improper store into indexable object'!
errorNonIntegerIndex
	"Create an error notification that an improper object was used as an index."

	self error: 'only integers should be used as indices'!
errorSubscriptBounds: index 
	"Create an error notification that an improper integer was used as an index."

	self error: 'subscript is out of bounds: ' , index printString!
forkEmergencyEvaluatorAt: priority
	"Fork a process running a simple Smalltalk evaluator using as little of the system as possible.  Used for desperate debugging.  may be invoked by control-shift-C."

	| stream char |
		[Display white: (0@0 extent: 1024@36).
		'EMERGENCY EVALUATOR (priority ', priority printString, ') -- type an expression terminated by ESC' displayAt: 50@0.
		Display reverse: (0@0 extent: 1024@36).
		stream _ WriteStream on: String new.
		[[Sensor keyboardPressed] whileFalse.
		(char _ Sensor keyboard) = 160 asCharacter] whileFalse:
			[char = Character backspace
				ifTrue: [stream skip: -1.  Display black: (0@18 extent: 1024@18)]
				ifFalse: [stream nextPut: char].
			stream contents displayAt: 50@18].
		Display black: (0@0 extent: 1024@18).
		(Compiler evaluate: stream contents) printString displayAt: 50@0] forkAt: priority!
mustBeBoolean
	"Catches attempts to test truth of non-Booleans.  This message is sent from the
	interpreter."

	self error: 'NonBoolean receiver--proceed for truth.'.
	^true!
nilFields
	"Store nil into all pointer fields of the receiver."

	self class isPointers ifFalse: [^self].
	1 to: self basicSize do:
		[:index | self basicAt: index put: nil].
	1 to: self class instSize do:
		[:index | self instVarAt: index put: nil].!
primitiveError: aString 
	"This method is called when the error handling results in a recursion in calling
	on error: or halt or halt:."

	| context key |
	Transcript cr.
	Transcript show: '**System Error Handling Failed** '.
	Transcript show: aString.
	Transcript cr.
	context _ thisContext sender sender.
	3 timesRepeat: 
		[context == nil ifFalse: [Transcript print: (context _ context sender); cr]].

	[Transcript show: '**type <s> for more stack; anything else restarts scheduler**'.
	Transcript cr.
	key _ Sensor keyboard.
	key = $s | (key = $S)] 
		whileTrue: 
			[5 timesRepeat: 
				[context == nil 
					ifFalse: [Transcript print: (context _ context sender); cr]]].
	ScheduledControllers searchForActiveController!
setDependents
	"Allocate the soft field for the receiver's dependents."
	| dependents |
	dependents _ OrderedCollection new.
	DependentsFields add: (Association key: self value: dependents).
	^ dependents!
species
	"Answer the preferred class for reconstructing the receiver.  For example, 
	collections create new collections whenever enumeration messages such as 
	collect: or select: are invoked.  The new kind of collection is determined by 
	the species of the original collection.  Species and class are not always the 
	same.  For example, the species of Interval is Array."

	^self class! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Object class
	instanceVariableNames: ''!


!Object class methodsFor: 'class initialization'!
initialize 
	(self confirm: 'Do you really want to initialize
class Object?  It will reset all dependents.')
		ifTrue:
			[self initializeDependentsFields.
			self initializeErrorRecursion]

	"Object initialize"! !

!Object class methodsFor: 'instance creation'!
readFrom: aStream
	"Create an object based on the contents of aStream."

	| object |
	object _ Compiler evaluate: aStream.
	(object isKindOf: self) ifFalse: [self error: self name, ' expected'].
	^object! !

!Object class methodsFor: 'documentation'!
howToModifyPrimitives
	"You are allowed to write methods which specify primitives, but please use 
	caution.  If you make a subclass of a class which contains a primitive method, 
	the subclass inherits the primitive.  The message which is implemented 
	primitively may be overridden in the subclass (E.g., see at:put: in String's 
	subclass Symbol).  The primitive behavior can be invoked using super (see 
	Symbol string:). 
	 
	A class which attempts to mimic the behavior of another class without being 
	its subclass may or may not be able to use the primitives of the original class.  
	In general, if the instance variables read or written by a primitive have the 
	same meanings and are in the same fields in both classes, the primitive will 
	work.  

	For certain frequently used 'special selectors', the compiler emits a 
	send-special-selector bytecode instead of a send-message bytecode.  
	Special selectors were created because they offer two advantages.  Code 
	which sends special selectors compiles into fewer bytes than normal.  For 
	some pairs of receiver classes and special selectors, the interpreter jumps 
	directly to a primitive routine without looking up the method in the class.  
	This is much faster than a normal message lookup. 
	 
	A selector which is a special selector solely in order to save space has a 
	normal behavior.  Methods whose selectors are special in order to 
	gain speed contain the comment, 'No Lookup'.  When the interpreter 
	encounters a send-special-selector bytecode, it checks the class of the 
	receiver and the selector.  If the class-selector pair is a no-lookup pair, 
	then the interpreter swiftly jumps to the routine which implements the 
	corresponding primitive.  (A special selector whose receiver is not of the 
	right class to make a no-lookup pair, is looked up normally).  The pairs are 
	listed below.  No-lookup methods contain a primitive number specification, 
	<primitive: xx>, which is redundant.  Since the method is not normally looked 
	up, deleting the primitive number specification cannot prevent this 
	primitive from running.  If a no-lookup primitive fails, the method is looked 
	up normally, and the expressions in it are executed. 
	 
	No Lookup pairs of (class, selector) 
	 
	SmallInteger with any of		+ - * /  \\  bitOr: bitShift: bitAnd:  // 
	SmallInteger with any of		=  ~=  >  <  >=  <= 
	Any class with					== 
	Any class with 					@ 
	Point with either of				x y 
	ContextPart with					blockCopy: 
	BlockContext with either of 		value value:
	"

	self error: 'comment only'!
whatIsAPrimitive
	"Some messages in the system are responded to primitively. A primitive   
	response is performed directly by the interpreter rather than by evaluating   
	expressions in a method. The methods for these messages indicate the   
	presence of a primitive response by including <primitive: xx> before the   
	first expression in the method.   
	  
	Primitives exist for several reasons. Certain basic or 'primitive' 
	operations cannot be performed in any other way. Smalltalk without 
	primitives can move values from one variable to another, but cannot add two 
	SmallIntegers together. Many methods for arithmetic and comparison 
	between numbers are primitives. Some primitives allow Smalltalk to 
	communicate with I/O devices such as the disk, the display, and the keyboard. 
	Some primitives exist only to make the system run faster; each does the same 
	thing as a certain Smalltalk method, and its implementation as a primitive is 
	optional.  
	  
	When the Smalltalk interpreter begins to execute a method which specifies a 
	primitive response, it tries to perform the primitive action and to return a 
	result. If the routine in the interpreter for this primitive is successful, 
	it will return a value and the expressions in the method will not be evaluated. 
	If the primitive routine is not successful, the primitive 'fails', and the 
	Smalltalk expressions in the method are executed instead. These 
	expressions are evaluated as though the primitive routine had not been 
	called.  
	  
	The Smalltalk code that is evaluated when a primitive fails usually 
	anticipates why that primitive might fail. If the primitive is optional, the 
	expressions in the method do exactly what the primitive would have done (See 
	Number @). If the primitive only works on certain classes of arguments, the 
	Smalltalk code tries to coerce the argument or appeals to a superclass to find 
	a more general way of doing the operation (see SmallInteger +). If the 
	primitive is never supposed to fail, the expressions signal an error (see 
	SmallInteger asFloat).  
	  
	Each method that specifies a primitive has a comment in it. If the primitive is 
	optional, the comment will say 'Optional'. An optional primitive that is not 
	implemented always fails, and the Smalltalk expressions do the work 
	instead.  
	 
	If a primitive is not optional, the comment will say, 'Essential'. Some 
	methods will have the comment, 'No Lookup'. See Object 
	howToModifyPrimitives for an explanation of special selectors which are 
	not looked up.  
	  
	For the primitives for +, -, *, and bitShift: in SmallInteger, and truncated 
	in Float, the primitive constructs and returns a 16-bit 
	LargePositiveInteger when the result warrants it. Returning 16-bit 
	LargePositiveIntegers from these primitives instead of failing is 
	optional in the same sense that the LargePositiveInteger arithmetic 
	primitives are optional. The comments in the SmallInteger primitives say, 
	'Fails if result is not a SmallInteger', even though the implementor has the 
	option to construct a LargePositiveInteger. For further information on 
	primitives, see the 'Primitive Methods' part of the chapter on the formal 
	specification of the interpreter in the Smalltalk book."

	self error: 'comment only'! !

!Object class methodsFor: 'private'!
initializeDependentsFields
	DependentsFields _ IdentityDictionary new: 4

	"Object initializeDependentsFields"!
initializeErrorRecursion
	ErrorRecursion _ false

	"Object initializeErrorRecursion"! !

Object initialize!
Switch subclass: #OneOnSwitch
	instanceVariableNames: 'connection '
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Interface-Menus'!
OneOnSwitch comment:
'I am a kind of Switch that can be connected to some related object, typically to a collection of my instances.  When my instance is created, its connection is set to a particular object.  When the object changes because an instance is turned on, an update message is broadcasted.  All the connected OneOnSwitches, except the changed one, turn off.  This allows OneOnSwitches to maintain the constraint that at most one of them will be on at any time.  OneOnSwitches can thus be made to act like "car radio" switches.

Instance Variables:
	connection	any object
'!


!OneOnSwitch methodsFor: 'initialize-release'!
release
	super release.
	self isConnectionSet ifTrue: [connection removeDependent: self]! !

!OneOnSwitch methodsFor: 'state'!
turnOn
	"Does nothing if it is already on.  If it is not, it is set to 'on', its dependents  
	are 	notified of the change, its connection is notified, and its action is  
	executed."

	self isOff
		ifTrue: 
			[on _ true.
			self changed.
			self notifyConnection.
			self doAction: onAction]! !

!OneOnSwitch methodsFor: 'connection'!
connection
	"Answer the object that connects the receiver to other Switches."
	^connection!
connection: anObject 
	"Set anObject to be the connection among two or more Switches."
	connection _ anObject.
	connection addDependent: self!
isConnectionSet
	"Answer whether the receiver is connected to an object that coordinates
	updates among switches."
	connection == nil
		ifTrue: [^false]
		ifFalse: [^true]!
notifyConnection
	"Send the receiver's connection (if it exists) the message 'changed: self' in order for 
	the 	connection to broadcast the change to other objects connected by the 
	connection. "
	
	self isConnectionSet ifTrue: [self connection changed: self]! !

!OneOnSwitch methodsFor: 'updating'!
update: aOneOnSwitch 
	"Does nothing if aOneOnSwitch is identical to this object.  If it is not, this 


! ====== True - The true boolean value ======

	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
True comment: 'I describe the behavior of my sole instance, true'!


!True methodsFor: 'logical operations'!
& alternativeObject 
	"Evaluating conjunction -- answer alternativeObject since receiver is true."

	^alternativeObject!
not
	"Negation--answer false since the receiver is true."

	^false!
| aBoolean 
	"Evaluating disjunction (OR) -- answer true since the receiver is true."

	^self! !

!True methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- answer the value of alternativeBlock since
	the receiver is true."

	^alternativeBlock value!
ifFalse: alternativeBlock 
	"Since the condition is true, the value is the true alternative, which is nil. 
	Execution does not actually reach here because the expression is compiled 
	in-line."

	^nil!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Answer the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!
ifTrue: alternativeBlock 
	"Answer the value of alternativeBlock. Execution does not actually 
	reach here because the expression is compiled in-line."

	^alternativeBlock value!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock 
	"Answer with the value of trueAlternativeBlock. Execution does not 
	actually reach here because the expression is compiled in-line."

	^trueAlternativeBlock value!
or: alternativeBlock 
	"Nonevaluating disjunction -- answer true since the receiver is true."

	^self! !

!True methodsFor: 'printing'!
printOn: aStream 
	aStream nextPutAll: 'true'! !Object subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!
UndefinedObject comment: 'I describe the behavior of my sole instance, nil.  nil represents a prior value for variables that have not been initialized, or for results which are meaningless.'!


!UndefinedObject methodsFor: 'initialize-release'!
release
	"Nil release is a no-op"! !

!UndefinedObject methodsFor: 'copying'!
deepCopy
	"Only one instance of UndefinedObject should ever be made, so answer 
	with self."!
shallowCopy
	"Only one instance of UndefinedObject should ever be made, so answer 
	with self."! !

!UndefinedObject methodsFor: 'printing'!
printOn: aStream 
	aStream nextPutAll: 'nil'!
storeOn: aStream 
	aStream nextPutAll: 'nil'! !

!UndefinedObject methodsFor: 'testing'!
isNil
	^true!
notNil
	^false! !


! ====== Boolean - Base class for True and False ======

	category: 'Kernel-Objects'!
Boolean comment: 'I implement behavior common to true and false.'!


!Boolean methodsFor: 'logical operations'!
& aBoolean 
	"Evaluating conjunction -- Evaluate the argument.  Then answer true if both the
	receiver and the argument are true."
	self subclassResponsibility!
eqv: aBoolean 
	"Answer true if the receiver is equivalent to aBoolean."

	^self == aBoolean!
not
	"Negation-- answer true if the receiver is false, answer false if the receiver is true."
	self subclassResponsibility!
xor: aBoolean 
	"Exclusive OR -- answer true if the receiver is not equivalent to aBoolean."

	^(self == aBoolean) not!
| aBoolean 
	"Evaluating disjunction (OR) -- Evaluate the argument.  Then answer true if
	either the receiver or the argument is true."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: alternativeBlock 
	"Nonevaluating conjunction -- if the receiver is true, answer the value of
	the argument, alternativeBlock; otherwise answer false without evaluating the
	argument."
	self subclassResponsibility!
ifFalse: alternativeBlock 
	"If the receiver is true (i.e., the condition is true), then the value is the true
	alternative, which is nil.  Otherwise answer the result of evaluating the argument,
	alternativeBlock.  Create an error if the receiver is nonBoolean.  Execution does not
	actually reach here because the expression is compiled in-line."
	self subclassResponsibility!
ifFalse: falseAlternativeBlock ifTrue: trueAlternativeBlock 
	"Same as ifTrue:ifFalse:"
	self subclassResponsibility!
ifTrue: alternativeBlock 
	"If the receiver is false (i.e., the condition is false), then the value is the false
	alternative, which is nil.  Otherwise answer the result of evaluating the argument,
	alternativeBlock.  Create an error if the receiver is nonBoolean.  Execution does not
	actually reach here because the expression is compiled in-line."
	self subclassResponsibility!
ifTrue: trueAlternativeBlock ifFalse: falseAlternativeBlock
	"If the receiver is true (i.e., the condition is true), then answer the value of the
	argument trueAlternativeBlock.  If the receiver is false, answer the result of
	evaluating the argument falseAlternativeBlock.  If the receiver is a nonBoolean
	then create an error message.  Execution does not actually reach here because the
	expression is compiled in-line."
	self subclassResponsibility!
or: alternativeBlock 
	"Nonevaluating disjunction -- if the receiver is false, answer the value of
	the argument, alternativeBlock; otherwise answer true without evaluating the
	argument."
	self subclassResponsibility! !

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
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

Boolean class
	instanceVariableNames: ''!


!Boolean class methodsFor: 'instance creation'!
new
	self error: 'You may not create any more Booleans - this is two-valued logic'! !SwitchView subclass: #BooleanView
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Interface-Menus'!
BooleanView comment:
'BooleanView is a "pluggable" view of a boolean value.  The notion of pluggable views is an
experiment in user interface design.  The idea is to provide a view which can be plugged
onto any object, rather than having to define a new subclass specific to every kind of
object which needs to be viewed.

The chief mechanism is a set of selectors, which can be thought of as an adaptor to
convert the generic textView operations (such as interrogateModel) into model-specific
operations (such as metaclassSelected).

See the creation messages in my class for an explication of the various parameters.
Browse senders of the creation messages in my class for examples in the system.'!


!BooleanView methodsFor: 'updating'!
interrogateModel
	^ (model perform: selector) = arguments first!
update: aspect
	aspect == selector ifTrue: [super update: aspect]! !
"-- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- -- "!

BooleanView class
	instanceVariableNames: ''!


