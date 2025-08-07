"MinimalBootstrap.st - Comprehensive minimal bootstrap with primitive annotations"
"This file defines the absolute minimum classes needed to bootstrap Smalltalk-80"
"Each method includes documentation about which VM primitive it maps to"

"============================================"
"ProtoObject - The absolute root of the class hierarchy"
"============================================"

nil subclass: #ProtoObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!ProtoObject methodsFor: 'system primitives'!
class
    "Access class pointer (slot 0 in every object)"
    <primitive: 111>
    self primitiveFailed! !

!ProtoObject methodsFor: 'system primitives'!
basicAt: index
    "Primitive access to indexed instance variables"
    <primitive: 60>
    self primitiveFailed! !

!ProtoObject methodsFor: 'system primitives'!
basicAt: index put: value
    "Primitive store into indexed instance variables"
    <primitive: 61>
    self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
doesNotUnderstand: aMessage
    "Default fallback for unrecognized messages - the ultimate backstop"
    <primitive: 131>
    self primitiveFailed! !

!ProtoObject methodsFor: 'comparing'!
==
    "Primitive identity test - are these the exact same object?"
    <primitive: 110>
    self primitiveFailed! !

!ProtoObject methodsFor: 'error handling'!
primitiveFailed
    "Called when a primitive fails"
    self error: 'Primitive failed'! !

!ProtoObject methodsFor: 'error handling'!
error: aString
    "Signal an error - minimal implementation"
    <primitive: 131>
    self primitiveFailed! !

"============================================"
"Object - Common protocol inherited by most classes"
"============================================"

ProtoObject subclass: #Object
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!Object methodsFor: 'message performing'!
perform: selector
    "Send selector to self - basic message dispatch"
    <primitive: 83>
    self primitiveFailed! !

!Object methodsFor: 'message performing'!
perform: selector with: anObject
    "Send selector to self with one argument"
    <primitive: 84>
    self primitiveFailed! !

!Object methodsFor: 'instance creation'!
new
    "Create a new instance - convenience method"
    ^self basicNew! !

!Object methodsFor: 'instance creation'!
basicNew
    "Primitive instance creation"
    <primitive: 70>
    self primitiveFailed! !

!Object methodsFor: 'instance creation'!
basicNew: size
    "Primitive instance creation with indexed variables"
    <primitive: 71>
    self primitiveFailed! !

!Object methodsFor: 'system primitives'!
instVarAt: index
    "Access named instance variable by index"
    <primitive: 73>
    self primitiveFailed! !

!Object methodsFor: 'system primitives'!
instVarAt: index put: value
    "Store into named instance variable by index"
    <primitive: 74>
    self primitiveFailed! !

!Object class methodsFor: 'instance creation'!
new
    "Create a new instance - class side"
    ^self basicNew! !

!Object class methodsFor: 'instance creation'!
basicNew
    "Primitive instance creation - class side"
    <primitive: 70>
    self primitiveFailed! !

!Object class methodsFor: 'instance creation'!
basicNew: size
    "Primitive instance creation with indexed variables - class side"
    <primitive: 71>
    self primitiveFailed! !

"============================================"
"Behavior - Foundation for all class-like objects"
"============================================"

Object subclass: #Behavior
    instanceVariableNames: 'superclass methodDict format'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Classes'!

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
    "Answer the receiver's format (instance specification)"
    ^format! !

!Behavior methodsFor: 'instance creation'!
new
    "Create a new instance"
    ^self basicNew! !

!Behavior methodsFor: 'instance creation'!
basicNew
    "Primitive instance creation"
    <primitive: 70>
    self primitiveFailed! !

!Behavior methodsFor: 'instance creation'!
basicNew: size
    "Primitive instance creation with indexed variables"
    <primitive: 71>
    self primitiveFailed! !

!Behavior methodsFor: 'compiling'!
compile: sourceString
    "Compile and install a method from source"
    <primitive: 117>
    self primitiveFailed! !

!Behavior methodsFor: 'method dictionary'!
addSelector: selector withMethod: compiledMethod
    "Add a compiled method to the method dictionary"
    methodDict at: selector put: compiledMethod! !

"============================================"
"ClassDescription - Adds organization to classes"
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
    "Answer the method organization"
    ^organization! !

"============================================"
"Class - Regular classes (not metaclasses)"
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
    "Answer the class variable dictionary (slot 6!)"
    ^classPool! !

!Class methodsFor: 'accessing'!
sharedPools
    "Answer the shared pool dictionaries"
    ^sharedPools! !

!Class methodsFor: 'class hierarchy'!
subclass: className instanceVariableNames: instVarString classVariableNames: classVarString poolDictionaries: poolString category: categoryName
    "Create a subclass - this is the standard class creation protocol"
    <primitive: 177>
    self primitiveFailed! !

"============================================"
"Metaclass - The class of all classes"
"============================================"

ClassDescription subclass: #Metaclass
    instanceVariableNames: 'thisClass'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Classes'!

!Metaclass methodsFor: 'accessing'!
thisClass
    "Answer the class that this metaclass is the class of"
    ^thisClass! !

!Metaclass methodsFor: 'accessing'!
name
    "Answer the name of the receiver"
    thisClass ifNil: [^'a Metaclass'].
    ^thisClass name, ' class'! !

"============================================"
"Symbol - Unique strings for selectors and names"
"============================================"

Object subclass: #Symbol
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
    "Answer the receiver's hash"
    <primitive: 76>
    self primitiveFailed! !

!Symbol class methodsFor: 'instance creation'!
intern: aString
    "Answer the unique symbol with aString as its characters"
    <primitive: 91>
    self primitiveFailed! !

"============================================"
"Array - Fixed-size indexed collections"
"============================================"

Object subclass: #Array
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Arrayed'!

!Array methodsFor: 'accessing'!
at: index
    "Answer the element at index"
    <primitive: 60>
    self primitiveFailed! !

!Array methodsFor: 'accessing'!
at: index put: value
    "Store value at index"
    <primitive: 61>
    self primitiveFailed! !

!Array methodsFor: 'accessing'!
size
    "Answer the number of elements"
    <primitive: 62>
    self primitiveFailed! !

!Array class methodsFor: 'instance creation'!
new: size
    "Create an array of the given size"
    ^self basicNew: size! !

"============================================"
"Dictionary - Key-value mapping (for method dictionaries)"
"============================================"

Object subclass: #Dictionary
    instanceVariableNames: 'tally array'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Unordered'!

!Dictionary methodsFor: 'accessing'!
at: key
    "Answer the value associated with key"
    ^self at: key ifAbsent: [self errorKeyNotFound: key]! !

!Dictionary methodsFor: 'accessing'!
at: key put: value
    "Set the value at key"
    <primitive: 96>
    self primitiveFailed! !

!Dictionary methodsFor: 'accessing'!
at: key ifAbsent: aBlock
    "Answer the value at key, or evaluate aBlock if not found"
    <primitive: 97>
    ^aBlock value! !

!Dictionary methodsFor: 'private'!
errorKeyNotFound: key
    "Signal that key was not found"
    self error: 'Key not found: ', key printString! !

!Dictionary class methodsFor: 'instance creation'!
new
    "Create a new empty dictionary"
    ^self basicNew initialize! !

"============================================"
"MethodDictionary - Specialized dictionary for methods"
"============================================"

Dictionary subclass: #MethodDictionary
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!

!MethodDictionary methodsFor: 'accessing'!
at: selector put: compiledMethod
    "Store a compiled method - used by the VM during method installation"
    <primitive: 98>
    super at: selector put: compiledMethod! !

"============================================"
"CompiledMethod - Bytecode representation of methods"
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
primitive
    "Answer the primitive index or 0"
    ^header bitAnd: 511! !

"============================================"
"UndefinedObject - The class of nil"
"============================================"

ProtoObject subclass: #UndefinedObject
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!UndefinedObject methodsFor: 'testing'!
isNil
    "Answer true since receiver is nil"
    ^true! !

!UndefinedObject methodsFor: 'testing'!
notNil
    "Answer false since receiver is nil"
    ^false! !

"============================================"
"Boolean hierarchy - true and false"
"============================================"

Object subclass: #Boolean
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!Boolean methodsFor: 'logical operations'!
not
    "Negation - must be implemented by subclasses"
    self subclassResponsibility! !

Boolean subclass: #True
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!True methodsFor: 'logical operations'!
not
    "Answer false"
    ^false! !

!True methodsFor: 'controlling'!
ifTrue: trueBlock
    "Evaluate trueBlock since receiver is true"
    ^trueBlock value! !

!True methodsFor: 'controlling'!
ifFalse: falseBlock
    "Answer nil since receiver is true"
    ^nil! !

!True methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
    "Evaluate trueBlock since receiver is true"
    ^trueBlock value! !

Boolean subclass: #False
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Objects'!

!False methodsFor: 'logical operations'!
not
    "Answer true"
    ^true! !

!False methodsFor: 'controlling'!
ifTrue: trueBlock
    "Answer nil since receiver is false"
    ^nil! !

!False methodsFor: 'controlling'!
ifFalse: falseBlock
    "Evaluate falseBlock since receiver is false"
    ^falseBlock value! !

!False methodsFor: 'controlling'!
ifTrue: trueBlock ifFalse: falseBlock
    "Evaluate falseBlock since receiver is false"
    ^falseBlock value! !

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
    "Add aNumber to receiver"
    <primitive: 1>
    self primitiveFailed! !

!SmallInteger methodsFor: 'arithmetic'!
- aNumber
    "Subtract aNumber from receiver"
    <primitive: 2>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
    "Answer whether receiver is less than aNumber"
    <primitive: 3>
    self primitiveFailed! !

!SmallInteger methodsFor: 'comparing'!
= aNumber
    "Answer whether receiver equals aNumber"
    <primitive: 7>
    self primitiveFailed! !

"============================================"
"END OF MINIMAL BOOTSTRAP"
"============================================"