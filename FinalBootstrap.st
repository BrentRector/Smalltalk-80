"FinalBootstrap.st - Minimal bootstrap with proper file out format"
"This file uses the standard Smalltalk file out format"
"NOTE: ProtoObject has no superclass in bootstrap context"

"============================================"
"ProtoObject - Absolute Root"
"============================================"

nil subclass: #ProtoObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!ProtoObject methodsFor: 'comparing'!
== anObject
	"Primitive. Answer whether the receiver and the argument are the same object."
	<primitive: 110>
	self primitiveFailed! !

!ProtoObject methodsFor: 'comparing'!
~~ anObject
	"Answer whether the receiver and the argument are not the same object"
	^(self == anObject) not! !

!ProtoObject methodsFor: 'accessing'!
identityHash
	"Answer a SmallInteger whose value is related to the receiver's identity."
	<primitive: 75>
	self primitiveFailed! !

!ProtoObject methodsFor: 'accessing'!
yourself
	"Answer self"
	^self! !

!ProtoObject methodsFor: 'accessing'!
class
	"Primitive. Answer the object which is the receiver's class"
	<primitive: 111>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
doesNotUnderstand: aMessage
	"Handle the fact that there was an attempt to send the given message to the receiver."
	self error: 'Message not understood: ', aMessage selector! !

!ProtoObject methodsFor: 'error handling'!
error: aString
	"Throw an error with the given message."
	<primitive: 131>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
primitiveFailed
	"Called when a primitive fails."
	self error: 'Primitive failed'! !

!ProtoObject methodsFor: 'testing'!
isNil
	"Coerces nil to true and everything else to false"
	^false! !

!ProtoObject methodsFor: 'testing'!
notNil
	"Coerces nil to false and everything else to true"
	^true! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol
	"Send the unary selector, aSymbol, to the receiver."
	<primitive: 83>
	self primitiveFailed! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol with: anObject
	"Send the selector, aSymbol, to the receiver with anObject as its argument."
	<primitive: 84>
	self primitiveFailed! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol withArguments: argArray
	"Send the selector, aSymbol, to the receiver with arguments from argArray."
	<primitive: 84>
	self primitiveFailed! !

!ProtoObject methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' ('.
	aStream nextPutAll: self identityHash printString.
	aStream nextPut: $)! !

!ProtoObject methodsFor: 'printing'!
printString
	"Answer a String whose characters are a description of the receiver."
	| stream |
	stream := WriteStream on: (String new: 16).
	self printOn: stream.
	^stream contents! !

!ProtoObject class methodsFor: 'instance creation'!
new
	"Create a new instance of ProtoObject"
	^self basicNew! !

!ProtoObject class methodsFor: 'instance creation'!
basicNew
	"Primitive. Create a new instance of the receiver"
	<primitive: 70>
	self primitiveFailed! !

!ProtoObject class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#ProtoObject! !

"============================================"
"UndefinedObject - nil singleton"
"============================================"

ProtoObject subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!UndefinedObject methodsFor: 'testing'!
isNil
	"Answer true since the receiver is nil"
	^true! !

!UndefinedObject methodsFor: 'testing'!
notNil
	"Answer false since the receiver is nil"
	^false! !

!UndefinedObject methodsFor: 'printing'!
printOn: aStream
	"Print 'nil' on the stream"
	aStream nextPutAll: 'nil'! !

!UndefinedObject methodsFor: 'controlling'!
ifNil: nilBlock
	"Evaluate the block since receiver is nil"
	^nilBlock value! !

!UndefinedObject methodsFor: 'controlling'!
ifNotNil: notNilBlock
	"Return self since receiver is nil"
	^self! !

!UndefinedObject methodsFor: 'controlling'!
ifNil: nilBlock ifNotNil: notNilBlock
	"Evaluate nilBlock since receiver is nil"
	^nilBlock value! !

!UndefinedObject class methodsFor: 'accessing'!
instance
	"Return the singleton nil"
	^nil! !

!UndefinedObject class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#UndefinedObject! !

"============================================"
"MessageSend - Deferred message"
"============================================"

ProtoObject subclass: #MessageSend
	instanceVariableNames: 'receiver selector arguments'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Messaging'!

!MessageSend methodsFor: 'accessing'!
receiver
	"Answer the receiver of the message"
	^receiver! !

!MessageSend methodsFor: 'accessing'!
receiver: anObject
	"Set the receiver of the message"
	receiver := anObject! !

!MessageSend methodsFor: 'accessing'!
selector
	"Answer the selector of the message"
	^selector! !

!MessageSend methodsFor: 'accessing'!
selector: aSymbol
	"Set the selector of the message"
	selector := aSymbol! !

!MessageSend methodsFor: 'accessing'!
arguments
	"Answer the arguments of the message"
	^arguments! !

!MessageSend methodsFor: 'accessing'!
arguments: anArray
	"Set the arguments of the message"
	arguments := anArray! !

!MessageSend methodsFor: 'execution'!
value
	"Send the message to the receiver with its arguments"
	^receiver perform: selector withArguments: arguments! !

!MessageSend methodsFor: 'printing'!
printOn: aStream
	"Print representation of the deferred message"
	aStream nextPutAll: receiver printString.
	aStream nextPutAll: ' >> '.
	aStream nextPutAll: selector printString.
	aStream nextPutAll: ' '.
	aStream nextPutAll: arguments printString! !

!MessageSend class methodsFor: 'instance creation'!
receiver: aReceiver selector: aSelector arguments: argArray
	"Create a new MessageSend"
	^self new
		receiver: aReceiver;
		selector: aSelector;
		arguments: argArray;
		yourself! !

!MessageSend class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#MessageSend! !

"============================================"
"MethodContext - Execution frame"
"============================================"

ProtoObject subclass: #MethodContext
	instanceVariableNames: 'sender receiver method pc stack'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!MethodContext methodsFor: 'accessing'!
sender
	"Answer the sender of this context"
	^sender! !

!MethodContext methodsFor: 'accessing'!
sender: aContext
	"Set the sender of this context"
	sender := aContext! !

!MethodContext methodsFor: 'accessing'!
receiver
	"Answer the receiver of the method in this context"
	^receiver! !

!MethodContext methodsFor: 'accessing'!
receiver: anObject
	"Set the receiver of the method in this context"
	receiver := anObject! !

!MethodContext methodsFor: 'accessing'!
method
	"Answer the method being executed"
	^method! !

!MethodContext methodsFor: 'accessing'!
method: aCompiledMethod
	"Set the method being executed"
	method := aCompiledMethod! !

!MethodContext methodsFor: 'accessing'!
pc
	"Answer the program counter"
	^pc! !

!MethodContext methodsFor: 'accessing'!
pc: anInteger
	"Set the program counter"
	pc := anInteger! !

!MethodContext methodsFor: 'accessing'!
stack
	"Answer the stack"
	^stack! !

!MethodContext methodsFor: 'accessing'!
stack: anArray
	"Set the stack"
	stack := anArray! !

!MethodContext methodsFor: 'printing'!
printOn: aStream
	"Print context description"
	aStream nextPutAll: 'Context for '.
	method 
		ifNotNil: [aStream nextPutAll: method selector printString]
		ifNil: [aStream nextPutAll: 'nil']! !

!MethodContext class methodsFor: 'instance creation'!
newFor: aMethod
	"Create a new context for a given method"
	^self new
		method: aMethod;
		pc: 0;
		yourself! !

!MethodContext class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#MethodContext! !

"============================================"
"Object - Common protocol"
"============================================"

ProtoObject subclass: #Object
	instanceVariableNames: ''
	classVariableNames: 'DependentsFields'
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'comparing'!
= anObject
	"Answer whether the receiver and argument represent the same object."
	^self == anObject! !

!Object methodsFor: 'comparing'!
~= anObject
	"Answer whether the receiver and argument do not represent the same object."
	^(self = anObject) not! !

!Object methodsFor: 'comparing'!
hash
	"Answer an integer hash value for the receiver."
	^self identityHash! !

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
printOn: aStream
	"Append to aStream a sequence of characters that identifies the receiver."
	| title |
	title := self class name.
	aStream
		nextPutAll: (title first isVowel ifTrue: ['an '] ifFalse: ['a ']);
		nextPutAll: title! !

!Object methodsFor: 'system primitives'!
instVarAt: index
	"Primitive. Answer the value of the instance variable at the given index."
	<primitive: 73>
	self primitiveFailed! !

!Object methodsFor: 'system primitives'!
instVarAt: index put: value
	"Primitive. Store the value in the instance variable at the given index."
	<primitive: 74>
	self primitiveFailed! !

!Object methodsFor: 'testing'!
isMemberOf: aClass
	"Answer whether the receiver is an instance of the class."
	^self class == aClass! !

!Object methodsFor: 'testing'!
isKindOf: aClass
	"Answer whether the receiver's class is aClass or inherits from aClass."
	^self class == aClass or: [self class inheritsFrom: aClass]! !

!Object methodsFor: 'controlling'!
ifNil: nilBlock
	"Return self since receiver is not nil"
	^self! !

!Object methodsFor: 'controlling'!
ifNotNil: notNilBlock
	"Evaluate notNilBlock with receiver"
	^notNilBlock value: self! !

!Object methodsFor: 'controlling'!
ifNil: nilBlock ifNotNil: notNilBlock
	"Evaluate notNilBlock since receiver is not nil"
	^notNilBlock value: self! !

"============================================"
"Behavior - Metaclass foundation"
"============================================"

ProtoObject subclass: #Behavior
	instanceVariableNames: 'superclass methodDict format'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!Behavior methodsFor: 'instance creation'!
basicNew
	"Primitive. Answer an instance of the receiver"
	<primitive: 70>
	self primitiveFailed! !

!Behavior methodsFor: 'instance creation'!
basicNew: anInteger
	"Primitive. Answer an instance with anInteger indexable variables"
	<primitive: 71>
	self primitiveFailed! !

!Behavior methodsFor: 'instance creation'!
new
	"Answer a new instance of the receiver"
	^self basicNew! !

!Behavior methodsFor: 'instance creation'!
new: anInteger
	"Answer a new instance with anInteger indexable variables"
	^self basicNew: anInteger! !

!Behavior methodsFor: 'accessing'!
superclass
	"Answer the receiver's superclass"
	^superclass! !

!Behavior methodsFor: 'accessing'!
methodDict
	"Answer the receiver's method dictionary"
	^methodDict! !

!Behavior methodsFor: 'accessing'!
format
	"Answer the receiver's format"
	^format! !

!Behavior methodsFor: 'testing'!
inheritsFrom: aClass
	"Answer whether the receiver inherits from aClass"
	| aSuperclass |
	aSuperclass := superclass.
	[aSuperclass == nil]
		whileFalse: 
			[aSuperclass == aClass ifTrue: [^true].
			aSuperclass := aSuperclass superclass].
	^false! !

!Behavior methodsFor: 'accessing method dictionary'!
selectors
	"Answer a Set of all the message selectors the receiver understands."
	^methodDict keys! !

!Behavior methodsFor: 'accessing method dictionary'!
allSelectors
	"Answer a Set of all the message selectors the receiver and its superclasses understand."
	| selectors |
	selectors := Set new.
	self withAllSuperclassesDo: [:class | selectors addAll: class selectors].
	^selectors! !

!Behavior methodsFor: 'accessing class hierarchy'!
withAllSuperclassesDo: aBlock
	"Evaluate the block for the receiver and each of its superclasses."
	aBlock value: self.
	superclass ifNotNil: [superclass withAllSuperclassesDo: aBlock]! !

Behavior subclass: #ClassDescription
	instanceVariableNames: 'instanceVariables organization'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!ClassDescription methodsFor: 'accessing'!
instanceVariables
	"Answer the instance variables"
	^instanceVariables! !

!ClassDescription methodsFor: 'accessing'!
organization
	"Answer the organization"
	^organization! !

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
	"Answer the receiver's class pool dictionary"
	^classPool! !

!Class methodsFor: 'accessing'!
sharedPools
	"Answer the receiver's shared pools"
	^sharedPools! !

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
	"Evaluating conjunction. Evaluate the argument."
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
| aBoolean
	"Evaluating disjunction. Evaluate the argument."
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
not
	"Negation."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
and: aBlock
	"Nonevaluating conjunction. If the receiver is true, answer the value of aBlock."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
or: aBlock
	"Nonevaluating disjunction. If the receiver is false, answer the value of aBlock."
	self subclassResponsibility! !

Boolean subclass: #True
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!True methodsFor: 'logical operations'!
& aBoolean
	"Answer aBoolean since receiver is true"
	^aBoolean! !

!True methodsFor: 'logical operations'!
| aBoolean
	"Answer true since receiver is true"
	^true! !

!True methodsFor: 'logical operations'!
not
	"Answer false since receiver is true"
	^false! !

!True methodsFor: 'controlling'!
and: aBlock
	"Answer the value of aBlock since receiver is true"
	^aBlock value! !

!True methodsFor: 'controlling'!
or: aBlock
	"Answer true since receiver is true"
	^true! !

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

!True methodsFor: 'printing'!
printOn: aStream
	"Print 'true'"
	aStream nextPutAll: 'true'! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!False methodsFor: 'logical operations'!
& aBoolean
	"Answer false since receiver is false"
	^false! !

!False methodsFor: 'logical operations'!
| aBoolean
	"Answer aBoolean since receiver is false"
	^aBoolean! !

!False methodsFor: 'logical operations'!
not
	"Answer true since receiver is false"
	^true! !

!False methodsFor: 'controlling'!
and: aBlock
	"Answer false since receiver is false"
	^false! !

!False methodsFor: 'controlling'!
or: aBlock
	"Answer the value of aBlock since receiver is false"
	^aBlock value! !

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

!False methodsFor: 'printing'!
printOn: aStream
	"Print 'false'"
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
	"Answer whether the receiver is less than the argument."
	self subclassResponsibility! !

!Magnitude methodsFor: 'comparing'!
<= aMagnitude
	"Answer whether the receiver is less than or equal to the argument."
	^(self > aMagnitude) not! !

!Magnitude methodsFor: 'comparing'!
> aMagnitude
	"Answer whether the receiver is greater than the argument."
	^aMagnitude < self! !

!Magnitude methodsFor: 'comparing'!
>= aMagnitude
	"Answer whether the receiver is greater than or equal to the argument."
	^(self < aMagnitude) not! !

!Magnitude methodsFor: 'comparing'!
between: min and: max
	"Answer whether the receiver is between min and max."
	^self >= min and: [self <= max]! !

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

!Number methodsFor: 'arithmetic'!
+ aNumber
	"Answer the sum of the receiver and aNumber."
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
- aNumber
	"Answer the difference between the receiver and aNumber."
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
* aNumber
	"Answer the result of multiplying the receiver by aNumber."
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
/ aNumber
	"Answer the result of dividing the receiver by aNumber."
	self subclassResponsibility! !

!Number methodsFor: 'arithmetic'!
// aNumber
	"Integer division."
	^(self / aNumber) floor! !

!Number methodsFor: 'arithmetic'!
\\ aNumber
	"Modulo."
	^self - ((self // aNumber) * aNumber)! !

!Number methodsFor: 'arithmetic'!
negated
	"Answer the negation of the receiver."
	^0 - self! !

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
	"Primitive. Add the receiver to aNumber and answer the result."
	<primitive: 1>
	^super + aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
	"Primitive. Subtract aNumber from the receiver and answer the result."
	<primitive: 2>
	^super - aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
* aNumber
	"Primitive. Multiply the receiver by aNumber and answer the result."
	<primitive: 9>
	^super * aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
/ aNumber
	"Primitive. Divide the receiver by aNumber and answer the result."
	<primitive: 10>
	^super / aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
// aNumber
	"Primitive. Divide the receiver by aNumber and answer the integer result."
	<primitive: 12>
	^super // aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
\\ aNumber
	"Primitive. Take the receiver modulo aNumber and answer the result."
	<primitive: 11>
	^super \\ aNumber! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
	"Primitive. Answer whether the receiver is less than aNumber."
	<primitive: 3>
	^super < aNumber! !

!SmallInteger methodsFor: 'comparing'!
> aNumber
	"Primitive. Answer whether the receiver is greater than aNumber."
	<primitive: 4>
	^super > aNumber! !

!SmallInteger methodsFor: 'comparing'!
<= aNumber
	"Primitive. Answer whether the receiver is less than or equal to aNumber."
	<primitive: 5>
	^super <= aNumber! !

!SmallInteger methodsFor: 'comparing'!
>= aNumber
	"Primitive. Answer whether the receiver is greater than or equal to aNumber."
	<primitive: 6>
	^super >= aNumber! !

!SmallInteger methodsFor: 'comparing'!
= aNumber
	"Primitive. Answer whether the receiver is equal to aNumber."
	<primitive: 7>
	^super = aNumber! !

!SmallInteger methodsFor: 'comparing'!
~= aNumber
	"Primitive. Answer whether the receiver is not equal to aNumber."
	<primitive: 8>
	^super ~= aNumber! !

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
	"Answer the number of elements in the receiver."
	| tally |
	tally := 0.
	self do: [:each | tally := tally + 1].
	^tally! !

!Collection methodsFor: 'testing'!
isEmpty
	"Answer whether the receiver is empty."
	^self size = 0! !

!Collection methodsFor: 'testing'!
notEmpty
	"Answer whether the receiver contains any elements."
	^self isEmpty not! !

!Collection methodsFor: 'enumerating'!
do: aBlock
	"Evaluate aBlock with each of the receiver's elements as the argument."
	self subclassResponsibility! !

Collection subclass: #SequenceableCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

!SequenceableCollection methodsFor: 'accessing'!
at: index
	"Answer the element at index."
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
at: index put: value
	"Store value at index."
	self subclassResponsibility! !

!SequenceableCollection methodsFor: 'accessing'!
first
	"Answer the first element."
	^self at: 1! !

!SequenceableCollection methodsFor: 'accessing'!
last
	"Answer the last element."
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
	"Primitive. Answer the element at index."
	<primitive: 60>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
at: index put: value
	"Primitive. Store value at index."
	<primitive: 61>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
size
	"Primitive. Answer the number of indexable fields in the receiver."
	<primitive: 62>
	self primitiveFailed! !

!Array methodsFor: 'enumerating'!
do: aBlock
	"Evaluate aBlock with each of the receiver's elements."
	1 to: self size do: [:i | aBlock value: (self at: i)]! !

ArrayedCollection subclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

!String methodsFor: 'accessing'!
at: index
	"Primitive. Answer the Character at index."
	<primitive: 63>
	self primitiveFailed! !

!String methodsFor: 'accessing'!
at: index put: aCharacter
	"Primitive. Store the Character at index."
	<primitive: 64>
	self primitiveFailed! !

!String methodsFor: 'accessing'!
size
	"Primitive. Answer the number of indexable fields in the receiver."
	<primitive: 62>
	self primitiveFailed! !

ArrayedCollection subclass: #Symbol
	instanceVariableNames: ''
	classVariableNames: 'SymbolTable'
	poolDictionaries: ''
	category: 'Collections-Text'!

!Symbol methodsFor: 'comparing'!
= aSymbol
	"Symbols are unique; equality is identity."
	^self == aSymbol! !

!Symbol methodsFor: 'comparing'!
hash
	"Answer the receiver's identity hash."
	^self identityHash! !

!Symbol methodsFor: 'printing'!
printOn: aStream
	"Print the symbol"
	aStream nextPut: $#.
	aStream nextPutAll: self! !

"============================================"
"Unordered Collections"
"============================================"

Collection subclass: #Set
	instanceVariableNames: 'tally array'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Set methodsFor: 'accessing'!
size
	"Answer the number of elements."
	^tally! !

Set subclass: #Dictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Dictionary methodsFor: 'accessing'!
at: key
	"Answer the value at key."
	^self at: key ifAbsent: [self errorKeyNotFound: key]! !

!Dictionary methodsFor: 'accessing'!
at: key put: value
	"Set the value at key."
	| assoc |
	assoc := self associationAt: key ifAbsent: [
		self add: (Association key: key value: value).
		^value].
	assoc value: value.
	^value! !

!Dictionary methodsFor: 'accessing'!
at: key ifAbsent: aBlock
	"Answer the value at key, or evaluate aBlock if not found."
	| assoc |
	assoc := self associationAt: key ifAbsent: [^aBlock value].
	^assoc value! !

!Dictionary methodsFor: 'private'!
associationAt: key ifAbsent: aBlock
	"Answer the association for key, or evaluate aBlock if not found."
	self subclassResponsibility! !

!Dictionary methodsFor: 'private'!
errorKeyNotFound: key
	"Signal an error that key was not found."
	self error: 'Key not found: ', key printString! !

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
	"Answer the key"
	^key! !

!LookupKey methodsFor: 'accessing'!
key: aKey
	"Set the key"
	key := aKey! !

!LookupKey methodsFor: 'comparing'!
= aLookupKey
	"Answer whether the receiver and argument have equal keys."
	self species = aLookupKey species
		ifTrue: [^key = aLookupKey key]
		ifFalse: [^false]! !

!LookupKey methodsFor: 'comparing'!
hash
	"Answer the hash of the key."
	^key hash! !

LookupKey subclass: #Association
	instanceVariableNames: 'value'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

!Association methodsFor: 'accessing'!
value
	"Answer the value"
	^value! !

!Association methodsFor: 'accessing'!
value: anObject
	"Set the value"
	value := anObject! !

!Association methodsFor: 'printing'!
printOn: aStream
	"Print the association"
	key printOn: aStream.
	aStream nextPutAll: '->'.
	value printOn: aStream! !

!Association class methodsFor: 'instance creation'!
key: aKey value: aValue
	"Answer a new Association with the given key and value."
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
	"Evaluate the receiver with no arguments."
	<primitive: 81>
	self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1
	"Evaluate the receiver with one argument."
	<primitive: 81>
	self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
value: arg1 value: arg2
	"Evaluate the receiver with two arguments."
	<primitive: 81>
	self primitiveFailed! !

!BlockContext methodsFor: 'evaluating'!
valueWithArguments: argArray
	"Evaluate the receiver with arguments from argArray."
	<primitive: 82>
	self primitiveFailed! !

Object subclass: #CompiledMethod
	instanceVariableNames: 'header literals bytecodes'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!CompiledMethod methodsFor: 'accessing'!
numArgs
	"Answer the number of arguments."
	^(header bitShift: -24) bitAnd: 15! !

!CompiledMethod methodsFor: 'accessing'!
numTemps
	"Answer the number of temporaries."
	^(header bitShift: -18) bitAnd: 63! !

!CompiledMethod methodsFor: 'accessing'!
primitive
	"Answer the primitive index or 0."
	^header bitAnd: 511! !

!CompiledMethod methodsFor: 'accessing'!
selector
	"Answer the selector from the last literal."
	literals isEmpty ifTrue: [^nil].
	^literals last! !

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
"Streams"
"============================================"

Object subclass: #Stream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!Stream methodsFor: 'accessing'!
next
	"Answer the next object accessible by the receiver."
	self subclassResponsibility! !

!Stream methodsFor: 'accessing'!
nextPut: anObject
	"Store anObject as the next element accessible by the receiver."
	self subclassResponsibility! !

!Stream methodsFor: 'accessing'!
nextPutAll: aCollection
	"Store the elements of aCollection as the next elements accessible by the receiver."
	aCollection do: [:each | self nextPut: each].
	^aCollection! !

!Stream methodsFor: 'accessing'!
contents
	"Answer all of the contents of the receiver."
	self subclassResponsibility! !

Stream subclass: #WriteStream
	instanceVariableNames: 'collection position writeLimit'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!WriteStream methodsFor: 'accessing'!
nextPut: anObject
	"Store anObject as the next element."
	position >= writeLimit
		ifTrue: [self growTo: position + 10].
	position := position + 1.
	collection at: position put: anObject.
	^anObject! !

!WriteStream methodsFor: 'accessing'!
contents
	"Answer the contents up to the current position."
	^collection copyFrom: 1 to: position! !

!WriteStream class methodsFor: 'instance creation'!
on: aCollection
	"Answer a new WriteStream on aCollection."
	^self new on: aCollection! !

"============================================"
"END OF BOOTSTRAP"
"============================================"