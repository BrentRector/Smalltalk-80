"UpdatedBootstrap.st - Minimal bootstrap with proper ProtoObject hierarchy"
"This file follows the modern Smalltalk architecture with ProtoObject as the true root"
"NOTE: In bootstrap context, ProtoObject has no superclass (nil), not Object as shown in syntax"

"============================================"
"ProtoObject - Absolute Minimal Protocol"
"============================================"

nil subclass: #ProtoObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!ProtoObject methodsFor: 'comparing'!
==
	"Primitive. Answer whether the receiver and the argument are the same object.
	Do not redefine the message == in any other class!"
	<primitive: 110>
	self primitiveFailed! !

!ProtoObject methodsFor: 'comparing'!
~~ anObject
	"Answer whether the receiver and the argument are not the same object"
	^(self == anObject) not! !

!ProtoObject methodsFor: 'comparing'!
identityHash
	"Answer a SmallInteger whose value is related to the receiver's identity.
	This method must not be overridden, except by SmallInteger."
	<primitive: 75>
	self primitiveFailed! !

!ProtoObject methodsFor: 'accessing'!
yourself
	"Answer self"
	^self! !

!ProtoObject methodsFor: 'system primitives'!
class
	"Primitive. Answer the object which is the receiver's class"
	<primitive: 111>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
doesNotUnderstand: aMessage
	"Handle the fact that there was an attempt to send the given message to the receiver 
	but the receiver does not understand this message."
	self error: 'Message not understood: ', aMessage selector! !

!ProtoObject methodsFor: 'error handling'!
error: aString
	"Throw an error with the given message. This is a minimal implementation."
	<primitive: 131>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
primitiveFailed
	"Called when a primitive fails. Minimal implementation."
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

!ProtoObject methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	| class |
	class := self class.
	aStream nextPutAll: class name.
	aStream nextPutAll: ' ('.
	aStream print: self identityHash.
	aStream nextPut: $)! !

!ProtoObject class methodsFor: 'instance creation'!
new
	"Create a new instance of ProtoObject"
	^self basicNew! !

!ProtoObject class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#ProtoObject! !

"============================================"
"UndefinedObject - The nil singleton"
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
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: 'nil'! !

!UndefinedObject class methodsFor: 'accessing'!
instance
	"Return the singleton nil"
	^nil! !

!UndefinedObject class methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	^#UndefinedObject! !

"============================================"
"MessageSend - Encapsulates deferred messages"
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

!MessageSend methodsFor: 'evaluating'!
value
	"Send the message to the receiver with its arguments"
	^receiver perform: selector withArguments: arguments! !

!MessageSend methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: receiver printString.
	aStream nextPutAll: ' >> '.
	aStream nextPutAll: selector printString.
	aStream nextPutAll: ' '.
	aStream nextPutAll: arguments printString! !

!MessageSend class methodsFor: 'instance creation'!
receiver: aReceiver selector: aSelector arguments: argArray
	"Create a new MessageSend with the given receiver, selector, and arguments"
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
	"Answer the method being executed in this context"
	^method! !

!MethodContext methodsFor: 'accessing'!
method: aCompiledMethod
	"Set the method being executed in this context"
	method := aCompiledMethod! !

!MethodContext methodsFor: 'accessing'!
pc
	"Answer the program counter for this context"
	^pc! !

!MethodContext methodsFor: 'accessing'!
pc: anInteger
	"Set the program counter for this context"
	pc := anInteger! !

!MethodContext methodsFor: 'accessing'!
stack
	"Answer the stack for this context"
	^stack! !

!MethodContext methodsFor: 'accessing'!
stack: anArray
	"Set the stack for this context"
	stack := anArray! !

!MethodContext methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	aStream nextPutAll: 'Context for '.
	method ifNotNil: [aStream nextPutAll: method selector printString]
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
"Object - Common Protocol (extends ProtoObject)"
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
printString
	"Answer a String whose characters are a description of the receiver."
	| stream |
	stream := WriteStream on: (String new: 16).
	self printOn: stream.
	^stream contents! !

!Object methodsFor: 'printing'!
printOn: aStream
	"Append to the argument, aStream, a sequence of characters that identifies the receiver."
	| class article |
	class := self class.
	article := class name first isVowel ifTrue: ['an '] ifFalse: ['a '].
	aStream nextPutAll: article.
	aStream nextPutAll: class name! !

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

!Object methodsFor: 'testing'!
isMemberOf: aClass
	"Answer whether the receiver is an instance of the class argument."
	^self class == aClass! !

!Object methodsFor: 'testing'!
isKindOf: aClass
	"Answer whether the receiver's class is aClass or inherits from aClass."
	^self class == aClass or: [self class inheritsFrom: aClass]! !

!Object methodsFor: 'controlling'!
ifNil: nilBlock
	"If the receiver is nil, answer the result of evaluating nilBlock, otherwise answer self."
	^self! !

!Object methodsFor: 'controlling'!
ifNotNil: notNilBlock
	"If the receiver is not nil, answer the result of evaluating notNilBlock with the receiver as argument."
	^notNilBlock value: self! !

!Object methodsFor: 'controlling'!
ifNil: nilBlock ifNotNil: notNilBlock
	"If the receiver is nil, answer the result of evaluating nilBlock, otherwise answer the result of evaluating notNilBlock with the receiver as argument."
	^notNilBlock value: self! !

"============================================"
"Metaclass System (extends ProtoObject)"
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
"Booleans (extends Object)"
"============================================"

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
	"Print the receiver on aStream"
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
	"Print the receiver on aStream"
	aStream nextPutAll: 'false'! !

"============================================"
"Numbers (extends Object)"
"============================================"

Object subclass: #Magnitude
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

Magnitude subclass: #Number
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

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
"Collections (extends Object)"
"============================================"

Object subclass: #Collection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

Collection subclass: #SequenceableCollection
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Abstract'!

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

ArrayedCollection subclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

ArrayedCollection subclass: #Symbol
	instanceVariableNames: ''
	classVariableNames: 'SymbolTable'
	poolDictionaries: ''
	category: 'Collections-Text'!

"============================================"
"Dictionaries (extends Object)"
"============================================"

Collection subclass: #Set
	instanceVariableNames: 'tally array'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

Set subclass: #Dictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

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
"Associations (extends Object)"
"============================================"

Object subclass: #LookupKey
	instanceVariableNames: 'key'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

LookupKey subclass: #Association
	instanceVariableNames: 'value'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

"============================================"
"Blocks and Methods (extends Object/ProtoObject)"
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

Object subclass: #CompiledMethod
	instanceVariableNames: 'header literals bytecodes'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

"============================================"
"System Organization (extends Object)"
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