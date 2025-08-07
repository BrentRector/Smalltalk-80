"RefactoredBootstrap.st - Minimal classes with ProtoObject as root"
"This file defines the correct hierarchy with ProtoObject as the root class"
"ProtoObject is the absolute minimal protocol - Object adds the common protocol"

"============================================"
"Root of the Object System - ProtoObject"
"============================================"

nil subclass: #ProtoObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!ProtoObject methodsFor: 'comparing'!
== anObject
	"Primitive. Answer whether the receiver and the argument are the same object"
	<primitive: 110>
	self primitiveFailed! !

!ProtoObject methodsFor: 'accessing'!
class
	"Primitive. Answer the object which is the receiver's class"
	<primitive: 111>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
doesNotUnderstand: aMessage
	"Handle the fact that there was an attempt to send the given message to the receiver 
	but the receiver does not understand this message (typically sent from the machine 
	when a message is sent to the receiver and no method is defined for that selector)."
	^self error: 'Message not understood: ', aMessage selector! !

!ProtoObject methodsFor: 'testing'!
isNil
	"Answer whether the receiver is nil"
	^false! !

!ProtoObject methodsFor: 'testing'!
notNil
	"Answer whether the receiver is not nil"
	^true! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol
	"Send the unary selector, aSymbol, to the receiver.
	Fail if the number of arguments expected by the selector is not zero."
	<primitive: 83>
	self primitiveFailed! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol with: anObject
	"Send the selector, aSymbol, to the receiver with anObject as its argument.
	Fail if the number of arguments expected by the selector is not one."
	<primitive: 84>
	self primitiveFailed! !

"============================================"
"Object - Common Protocol"
"============================================"

ProtoObject subclass: #Object
	instanceVariableNames: ''
	classVariableNames: 'DependentsFields'
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'comparing'!
= anObject
	"Answer whether the receiver and the argument represent the same object.
	By default, this is the same as ==. Subclasses may override."
	^self == anObject! !

!Object methodsFor: 'comparing'!
~= anObject
	"Answer whether the receiver and the argument do not represent the same object."
	^(self = anObject) == false! !

!Object methodsFor: 'comparing'!
hash
	"Answer an integer hash value for the receiver."
	^self identityHash! !

!Object methodsFor: 'comparing'!
identityHash
	"Answer an integer hash value for the receiver based on identity."
	<primitive: 75>
	self primitiveFailed! !

!Object methodsFor: 'copying'!
copy
	"Answer another instance just like the receiver."
	^self shallowCopy! !

!Object methodsFor: 'copying'!
shallowCopy
	"Answer a copy of the receiver which shares the receiver's instance variables."
	<primitive: 148>
	self primitiveFailed! !

!Object methodsFor: 'printing'!
printString
	"Answer a String whose characters are a description of the receiver."
	| stream |
	stream := WriteStream on: (String new: 16).
	self printOn: stream.
	^stream contents! !

!Object methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	| class |
	class := self class.
	aStream nextPutAll: (class name withArticle)! !

!Object methodsFor: 'error handling'!
error: aString
	"Signal an error with the given message."
	^Error signal: aString! !

!Object methodsFor: 'error handling'!
primitiveFailed
	"Called when a primitive fails."
	self error: 'Primitive failed'! !

!Object methodsFor: 'system primitives'!
instVarAt: index
	"Primitive. Answer the value of the instance variable at the given index."
	<primitive: 73>
	self primitiveFailed! !

!Object methodsFor: 'system primitives'!
instVarAt: index put: value
	"Primitive. Store the argument value in the instance variable at the given index."
	<primitive: 74>
	self primitiveFailed! !

ProtoObject subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!UndefinedObject methodsFor: 'testing'!
isNil
	"Answer whether the receiver is nil"
	^true! !

!UndefinedObject methodsFor: 'testing'!
notNil
	"Answer whether the receiver is not nil"
	^false! !

!UndefinedObject methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: 'nil'! !

"============================================"
"Metaclass System"
"============================================"

ProtoObject subclass: #Behavior
	instanceVariableNames: 'superclass methodDict format'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!Behavior methodsFor: 'instance creation'!
basicNew
	"Primitive. Answer an instance of the receiver with no indexable variables"
	<primitive: 70>
	self primitiveFailed! !

!Behavior methodsFor: 'instance creation'!
basicNew: anInteger
	"Primitive. Answer an instance of the receiver with anInteger indexable variables"
	<primitive: 71>
	self primitiveFailed! !

!Behavior methodsFor: 'instance creation'!
new
	"Answer a new instance of the receiver"
	^self basicNew! !

!Behavior methodsFor: 'instance creation'!
new: anInteger
	"Answer a new instance of the receiver with anInteger indexable variables"
	^self basicNew: anInteger! !

!Behavior methodsFor: 'accessing'!
superclass
	"Answer the receiver's superclass"
	^superclass! !

!Behavior methodsFor: 'accessing'!
methodDict
	"Answer the receiver's method dictionary"
	^methodDict! !

!Behavior methodsFor: 'testing'!
inheritsFrom: aClass
	"Answer whether the receiver is a subclass of aClass"
	| class |
	class := self.
	[class == nil] whileFalse: [
		class == aClass ifTrue: [^true].
		class := class superclass].
	^false! !

Behavior subclass: #ClassDescription
	instanceVariableNames: 'instanceVariables organization'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

ClassDescription subclass: #Class
	instanceVariableNames: 'name classPool sharedPools'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!Class methodsFor: 'accessing'!
name
	"Answer the receiver's name"
	^name! !

!Class methodsFor: 'accessing'!
classPool
	"Answer the receiver's class variable dictionary"
	^classPool! !

ClassDescription subclass: #Metaclass
	instanceVariableNames: 'thisClass'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!Metaclass methodsFor: 'accessing'!
thisClass
	"Answer the class that the receiver is the metaclass of"
	^thisClass! !

"============================================"
"Booleans"
"============================================"

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Boolean methodsFor: 'logical operations'!
& aBoolean
	"Answer true if both the receiver and the argument are true"
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
| aBoolean
	"Answer true if either the receiver or the argument is true"
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
not
	"Answer the opposite of the receiver"
	self subclassResponsibility! !

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!True methodsFor: 'controlling'!
ifTrue: trueBlock
	"Answer the value of trueBlock"
	^trueBlock value! !

!True methodsFor: 'controlling'!
ifFalse: falseBlock
	"Answer nil"
	^nil! !

!True methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
	"Answer the value of trueBlock"
	^trueBlock value! !

!True methodsFor: 'logical operations'!
& aBoolean
	"Answer true if both the receiver and the argument are true"
	^aBoolean! !

!True methodsFor: 'logical operations'!
| aBoolean
	"Answer true"
	^true! !

!True methodsFor: 'logical operations'!
not
	"Answer false"
	^false! !

!True methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: 'true'! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!False methodsFor: 'controlling'!
ifTrue: trueBlock
	"Answer nil"
	^nil! !

!False methodsFor: 'controlling'!
ifFalse: falseBlock
	"Answer the value of falseBlock"
	^falseBlock value! !

!False methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
	"Answer the value of falseBlock"
	^falseBlock value! !

!False methodsFor: 'logical operations'!
& aBoolean
	"Answer false"
	^false! !

!False methodsFor: 'logical operations'!
| aBoolean
	"Answer the argument"
	^aBoolean! !

!False methodsFor: 'logical operations'!
not
	"Answer true"
	^true! !

!False methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: 'false'! !

"============================================"
"Numbers"
"============================================"

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

!Magnitude methodsFor: 'comparing'!
< aMagnitude
	"Answer whether the receiver is less than the argument"
	self subclassResponsibility! !

!Magnitude methodsFor: 'comparing'!
<= aMagnitude
	"Answer whether the receiver is less than or equal to the argument"
	^(self > aMagnitude) not! !

!Magnitude methodsFor: 'comparing'!
> aMagnitude
	"Answer whether the receiver is greater than the argument"
	^aMagnitude < self! !

!Magnitude methodsFor: 'comparing'!
>= aMagnitude
	"Answer whether the receiver is greater than or equal to the argument"
	^(self < aMagnitude) not! !

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

!Number methodsFor: 'arithmetic'!
+ aNumber
	"Answer the sum of the receiver and the argument"
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
- aNumber
	"Answer the difference of the receiver and the argument"
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
* aNumber
	"Answer the product of the receiver and the argument"
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
/ aNumber
	"Answer the quotient of the receiver and the argument"
	self subclassResponsibility! !

Number subclass: #Integer
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

Integer subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber
	"Primitive. Answer the sum of the receiver and the argument"
	<primitive: 1>
	^super + aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
	"Primitive. Answer the difference of the receiver and the argument"
	<primitive: 2>
	^super - aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
* aNumber
	"Primitive. Answer the product of the receiver and the argument"
	<primitive: 9>
	^super * aNumber! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
	"Primitive. Answer whether the receiver is less than the argument"
	<primitive: 3>
	^super < aNumber! !

!SmallInteger methodsFor: 'comparing'!
= aNumber
	"Primitive. Answer whether the receiver is equal to the argument"
	<primitive: 7>
	^super = aNumber! !

"============================================"
"Collections"
"============================================"

Object subclass: #Collection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

!Collection methodsFor: 'accessing'!
size
	"Answer the number of elements in the receiver"
	| tally |
	tally := 0.
	self do: [:each | tally := tally + 1].
	^tally! !

!Collection methodsFor: 'testing'!
isEmpty
	"Answer whether the receiver contains no elements"
	^self size = 0! !

!Collection methodsFor: 'testing'!
notEmpty
	"Answer whether the receiver contains any elements"
	^self isEmpty not! !

!Collection methodsFor: 'enumerating'!
do: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument"
	self subclassResponsibility! !

Collection subclass: #SequenceableCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

!SequenceableCollection methodsFor: 'accessing'!
at: index
	"Answer the element at the given index"
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
at: index put: value
	"Store the argument value at the given index"
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
first
	"Answer the first element of the receiver"
	^self at: 1! !

!SequenceableCollection methodsFor: 'accessing'!
last
	"Answer the last element of the receiver"
	^self at: self size! !

SequenceableCollection subclass: #ArrayedCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

ArrayedCollection subclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Arrayed'!

!Array methodsFor: 'accessing'!
at: index
	"Primitive. Answer the element at the given index"
	<primitive: 60>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
at: index put: value
	"Primitive. Store the argument value at the given index"
	<primitive: 61>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
size
	"Primitive. Answer the number of elements in the receiver"
	<primitive: 62>
	self primitiveFailed! !

!Array methodsFor: 'enumerating'!
do: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument"
	1 to: self size do: [:i | aBlock value: (self at: i)]! !

ArrayedCollection subclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

!String methodsFor: 'accessing'!
at: index
	"Primitive. Answer the Character at the given index"
	<primitive: 63>
	self primitiveFailed! !

!String methodsFor: 'accessing'!
at: index put: aCharacter
	"Primitive. Store the Character at the given index"
	<primitive: 64>
	self primitiveFailed! !

!String methodsFor: 'accessing'!
size
	"Primitive. Answer the number of characters in the receiver"
	<primitive: 62>
	self primitiveFailed! !

ArrayedCollection subclass: #Symbol
	instanceVariableNames: ''
	classVariableNames: 'SymbolTable'
	poolDictionaries: ''
	category: 'Collections-Text'!

!Symbol methodsFor: 'comparing'!
= aSymbol
	"Symbols are unique - equality is identity"
	^self == aSymbol! !

!Symbol methodsFor: 'comparing'!
hash
	"Answer an integer hash value for the receiver"
	^self identityHash! !

"============================================"
"Collections - Unordered"
"============================================"

Collection subclass: #Set
	instanceVariableNames: 'tally array'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Set methodsFor: 'accessing'!
size
	"Answer the number of elements in the receiver"
	^tally! !

Set subclass: #Dictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Dictionary methodsFor: 'accessing'!
at: key
	"Answer the value associated with the key"
	^self at: key ifAbsent: [self errorKeyNotFound: key]! !

!Dictionary methodsFor: 'accessing'!
at: key put: value
	"Set the value at key to be value"
	| assoc |
	assoc := self associationAt: key ifAbsent: [
		self add: (Association key: key value: value).
		^value].
	assoc value: value.
	^value! !

Dictionary subclass: #SystemDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

Dictionary subclass: #IdentityDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

Dictionary subclass: #MethodDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

"============================================"
"Associations"
"============================================"

Object subclass: #LookupKey
	instanceVariableNames: 'key'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

!LookupKey methodsFor: 'accessing'!
key
	"Answer the receiver's key"
	^key! !

!LookupKey methodsFor: 'accessing'!
key: aKey
	"Set the receiver's key"
	key := aKey! !

LookupKey subclass: #Association
	instanceVariableNames: 'value'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

!Association methodsFor: 'accessing'!
value
	"Answer the receiver's value"
	^value! !

!Association methodsFor: 'accessing'!
value: anObject
	"Set the receiver's value"
	value := anObject! !

!Association class methodsFor: 'instance creation'!
key: aKey value: aValue
	"Answer a new instance with the given key and value"
	^self new key: aKey; value: aValue; yourself! !

"============================================"
"Blocks and Methods"
"============================================"

Object subclass: #BlockContext
	instanceVariableNames: 'nargs startpc home'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!BlockContext methodsFor: 'evaluating'!
value
	"Evaluate the receiver with no arguments"
	<primitive: 81>
	self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1
	"Evaluate the receiver with one argument"
	<primitive: 81>
	self primitiveFailed! !

Object subclass: #CompiledMethod
	instanceVariableNames: 'header literals bytecodes'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!CompiledMethod methodsFor: 'accessing'!
numArgs
	"Answer the number of arguments the receiver expects"
	^(header bitShift: -24) bitAnd: 15! !

!CompiledMethod methodsFor: 'accessing'!
numTemps
	"Answer the number of temporary variables the receiver uses"
	^(header bitShift: -18) bitAnd: 63! !

"============================================"
"System Organization"
"============================================"

Object subclass: #ClassOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

Object subclass: #SystemOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

"============================================"
"END OF BOOTSTRAP"
"============================================"