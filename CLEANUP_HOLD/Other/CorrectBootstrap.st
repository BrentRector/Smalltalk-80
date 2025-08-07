"CorrectBootstrap.st - Minimal bootstrap WITHOUT explicit metaclass definitions"
"Metaclasses are automatically created by the system when classes are defined"

"============================================"
"ProtoObject - The root class"
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
	"Handle unrecognized messages"
	self error: 'Message not understood: ', aMessage selector! !

!ProtoObject methodsFor: 'error handling'!
error: aString
	"Signal an error with the given message."
	<primitive: 131>
	self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
primitiveFailed
	"Called when a primitive fails."
	self error: 'Primitive failed'! !

!ProtoObject methodsFor: 'testing'!
isNil
	"Answer false for all objects except nil"
	^false! !

!ProtoObject methodsFor: 'testing'!
notNil
	"Answer true for all objects except nil"
	^true! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol
	"Send the unary selector to the receiver."
	<primitive: 83>
	self primitiveFailed! !

!ProtoObject methodsFor: 'performing'!
perform: aSymbol with: anObject
	"Send the selector to the receiver with one argument."
	<primitive: 84>
	self primitiveFailed! !

!ProtoObject methodsFor: 'printing'!
printOn: aStream
	"Append a textual representation of the receiver to aStream"
	aStream nextPutAll: self class name.
	aStream nextPutAll: ' ('.
	aStream nextPutAll: self identityHash printString.
	aStream nextPut: $)! !

"Class methods for ProtoObject"
!ProtoObject class methodsFor: 'instance creation'!
new
	"Answer a new instance of the receiver"
	^self basicNew! !

!ProtoObject class methodsFor: 'instance creation'!
basicNew
	"Primitive. Answer a new instance of the receiver"
	<primitive: 70>
	self primitiveFailed! !

"============================================"
"Behavior - Foundation of metaclass system"
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
	"Answer whether the receiver inherits from aClass"
	| aSuperclass |
	aSuperclass := superclass.
	[aSuperclass == nil]
		whileFalse: 
			[aSuperclass == aClass ifTrue: [^true].
			aSuperclass := aSuperclass superclass].
	^false! !

"============================================"
"ClassDescription - Adds organization"
"============================================"

Behavior subclass: #ClassDescription
	instanceVariableNames: 'instanceVariables organization'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!ClassDescription methodsFor: 'accessing'!
instanceVariables
	"Answer the instance variable names"
	^instanceVariables! !

!ClassDescription methodsFor: 'accessing'!
organization
	"Answer the organization"
	^organization! !

"============================================"
"Class - Regular classes"
"============================================"

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

"============================================"
"Metaclass - The class of classes"
"============================================"

ClassDescription subclass: #Metaclass
	instanceVariableNames: 'thisClass'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

!Metaclass methodsFor: 'accessing'!
thisClass
	"Answer the class that the receiver is the metaclass of"
	^thisClass! !

!Metaclass methodsFor: 'accessing'!
name
	"Answer the name of the receiver"
	thisClass ifNil: [^'a Metaclass'].
	^thisClass name, ' class'! !

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
	"Print 'nil' on the stream"
	aStream nextPutAll: 'nil'! !

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
printString
	"Answer a String representation of the receiver."
	| stream |
	stream := WriteStream on: (String new: 16).
	self printOn: stream.
	^stream contents! !

!Object methodsFor: 'printing'!
printOn: aStream
	"Append a textual representation of the receiver to aStream"
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

"============================================"
"Boolean - Abstract boolean class"
"============================================"

Object subclass: #Boolean
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Boolean methodsFor: 'logical operations'!
& aBoolean
	"Evaluating conjunction."
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
| aBoolean
	"Evaluating disjunction."
	self subclassResponsibility! !

!Boolean methodsFor: 'logical operations'!
not
	"Negation."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
ifTrue: trueBlock
	"If receiver is true, evaluate trueBlock."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
ifFalse: falseBlock
	"If receiver is false, evaluate falseBlock."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
	"Evaluate trueBlock or falseBlock depending on receiver."
	self subclassResponsibility! !

"============================================"
"True - The true singleton"
"============================================"

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

"============================================"
"False - The false singleton"
"============================================"

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
"SmallInteger - Immediate integers"
"============================================"

Object subclass: #SmallInteger
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Numbers'!

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber
	"Primitive. Add the receiver to aNumber."
	<primitive: 1>
	self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
	"Primitive. Subtract aNumber from the receiver."
	<primitive: 2>
	self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
* aNumber
	"Primitive. Multiply the receiver by aNumber."
	<primitive: 9>
	self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
	"Primitive. Answer whether the receiver is less than aNumber."
	<primitive: 3>
	self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
= aNumber
	"Primitive. Answer whether the receiver equals aNumber."
	<primitive: 7>
	self primitiveFailed! !

!SmallInteger methodsFor: 'printing'!
printOn: aStream
	"Print the integer value"
	<primitive: 23>
	super printOn: aStream! !

"============================================"
"Symbol - Unique strings"
"============================================"

Object subclass: #Symbol
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
"Array - Fixed-size collections"
"============================================"

Object subclass: #Array
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
	"Primitive. Answer the number of elements."
	<primitive: 62>
	self primitiveFailed! !

"============================================"
"Dictionary - Key-value collections"
"============================================"

Object subclass: #Dictionary
	instanceVariableNames: 'tally array'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

!Dictionary methodsFor: 'accessing'!
at: key
	"Answer the value at key."
	^self at: key ifAbsent: [self error: 'Key not found: ', key printString]! !

!Dictionary methodsFor: 'accessing'!
at: key put: value
	"Set the value at key."
	self subclassResponsibility! !

!Dictionary methodsFor: 'accessing'!
at: key ifAbsent: aBlock
	"Answer the value at key, or evaluate aBlock if absent."
	self subclassResponsibility! !

!Dictionary methodsFor: 'accessing'!
size
	"Answer the number of elements."
	^tally! !

"============================================"
"Association - Key-value pairs"
"============================================"

Object subclass: #Association
	instanceVariableNames: 'key value'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Support'!

!Association methodsFor: 'accessing'!
key
	"Answer the key"
	^key! !

!Association methodsFor: 'accessing'!
key: aKey
	"Set the key"
	key := aKey! !

!Association methodsFor: 'accessing'!
value
	"Answer the value"
	^value! !

!Association methodsFor: 'accessing'!
value: anObject
	"Set the value"
	value := anObject! !

!Association class methodsFor: 'instance creation'!
key: aKey value: aValue
	"Create a new Association"
	^self new key: aKey; value: aValue; yourself! !

"============================================"
"CompiledMethod - Compiled code"
"============================================"

Object subclass: #CompiledMethod
	instanceVariableNames: 'header literals bytecodes'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!CompiledMethod methodsFor: 'accessing'!
selector
	"Answer the selector (last literal)"
	literals isEmpty ifTrue: [^nil].
	^literals last! !

!CompiledMethod methodsFor: 'accessing'!
numArgs
	"Answer the number of arguments"
	^(header bitShift: -24) bitAnd: 15! !

!CompiledMethod methodsFor: 'accessing'!
numTemps
	"Answer the number of temporaries"
	^(header bitShift: -18) bitAnd: 63! !

"============================================"
"BlockContext - Block closures"
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

"============================================"
"Stream - Abstract stream"
"============================================"

Object subclass: #Stream
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Streams'!

!Stream methodsFor: 'accessing'!
nextPut: anObject
	"Store anObject as the next element."
	self subclassResponsibility! !

!Stream methodsFor: 'accessing'!
nextPutAll: aCollection
	"Store all elements of aCollection."
	aCollection do: [:each | self nextPut: each].
	^aCollection! !

"============================================"
"String - Character sequences"
"============================================"

Object subclass: #String
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
	"Primitive. Answer the number of characters."
	<primitive: 62>
	self primitiveFailed! !

!String class methodsFor: 'instance creation'!
new: size
	"Create a new String of the given size"
	^self basicNew: size! !

"============================================"
"WriteStream - Stream for building strings"
"============================================"

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
nextPutAll: aCollection
	"Store all elements of aCollection."
	aCollection do: [:each | self nextPut: each].
	^aCollection! !

!WriteStream methodsFor: 'accessing'!
contents
	"Answer the contents."
	^collection copyFrom: 1 to: position! !

!WriteStream methodsFor: 'private'!
on: aCollection
	"Initialize on aCollection"
	collection := aCollection.
	position := 0.
	writeLimit := aCollection size.
	^self! !

!WriteStream class methodsFor: 'instance creation'!
on: aCollection
	"Create a new WriteStream on aCollection"
	^self new on: aCollection! !

"============================================"
"Character - Individual characters"
"============================================"

Object subclass: #Character
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Text'!

!Character methodsFor: 'testing'!
isVowel
	"Answer whether the receiver is a vowel"
	^'aeiouAEIOU' includes: self! !

"============================================"
"END OF BOOTSTRAP"
"============================================"