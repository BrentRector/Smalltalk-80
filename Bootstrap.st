"Bootstrap.st - Minimal classes that are compiled into GeneratedBootstrap.cs"
"This file defines EXACTLY the classes and methods that are baked into the interpreter"

"============================================"
"Core Object System"
"============================================"

nil subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'comparing'!
== anObject
	"Primitive. Answer whether the receiver and the argument are the same object"
	<primitive: 110>
	self primitiveFailed! !

!Object methodsFor: 'accessing'!
class
	"Primitive. Answer the object which is the receiver's class"
	<primitive: 111>
	self primitiveFailed! !

!Object methodsFor: 'testing'!
isNil
	"Answer whether the receiver is nil"
	^false! !

!Object methodsFor: 'testing'!
notNil
	"Answer whether the receiver is not nil"
	^true! !

Object subclass: #UndefinedObject
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

"============================================"
"Metaclass System"
"============================================"

Object subclass: #Behavior
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

ClassDescription subclass: #Metaclass
	instanceVariableNames: 'thisClass'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Classes'!

"============================================"
"Booleans"
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

"============================================"
"Numbers"
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
	"Primitive. Add the receiver to the argument and return the result"
	<primitive: 1>
	^super + aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
	"Primitive. Subtract the argument from the receiver and return the result"
	<primitive: 2>
	^super - aNumber! !

!SmallInteger methodsFor: 'arithmetic'!
* aNumber
	"Primitive. Multiply the receiver by the argument and return the result"
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
"Symbols and Strings"
"============================================"

Object subclass: #Symbol
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Strings'!

"============================================"
"Collections"
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

ArrayedCollection variableSubclass: #Array
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Arrayed'!

!Array methodsFor: 'accessing'!
at: index
	"Primitive. Answer the object at the given index"
	<primitive: 60>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
at: index put: value
	"Primitive. Store the argument value at the given index"
	<primitive: 61>
	self primitiveFailed! !

!Array methodsFor: 'accessing'!
size
	"Primitive. Answer the number of indexable fields in the receiver"
	<primitive: 62>
	self primitiveFailed! !

ArrayedCollection variableByteSubclass: #String
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Strings'!

Collection subclass: #Set
	instanceVariableNames: 'tally array'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

Set variableSubclass: #Dictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

Dictionary variableSubclass: #SystemDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

Dictionary variableSubclass: #IdentityDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Collections-Unordered'!

IdentityDictionary subclass: #MethodDictionary
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

"============================================"
"Supporting Classes"
"============================================"

Magnitude subclass: #LookupKey
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
"Execution Machinery"
"============================================"

Object subclass: #BlockContext
	instanceVariableNames: 'nargs startpc home'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

!BlockContext methodsFor: 'evaluating'!
value
	"Primitive. Evaluate the receiver with no arguments"
	<primitive: 81>
	self primitiveFailed! !

Object subclass: #CompiledMethod
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Methods'!

"============================================"
"Organization Classes"
"============================================"

Object subclass: #ClassOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Support'!

Object subclass: #SystemOrganizer
	instanceVariableNames: 'categories'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'System-Support'!

"============================================"
"END OF BOOTSTRAP"
"============================================"
"This file should contain EXACTLY the classes and methods that are in GeneratedBootstrap.cs"
"Any additional functionality should go in StandardLibrary.st"