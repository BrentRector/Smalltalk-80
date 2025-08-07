"Essential bootstrap methods - these MUST be loaded before any other Smalltalk code can execute"

!Object methodsFor: 'system primitives'!
== anObject
	"Primitive. Answer whether the receiver and the argument are the same object
	(have the same object pointer). Essential. See Object documentation whatIsAPrimitive."
	<primitive: 110>
	self primitiveFailed!

class
	"Primitive. Answer the object which is the receiver's class. Essential. See 
	Object documentation whatIsAPrimitive."
	<primitive: 111>
	self primitiveFailed! !

!Object methodsFor: 'testing'!
isNil
	"Answer true if the receiver is nil."
	^false!

notNil
	"Answer true if the receiver is not nil."
	^true! !

!Object methodsFor: 'comparing'!
= anObject
	"Answer whether the receiver and the argument represent the same object."
	^self == anObject!

~= anObject
	"Answer whether the receiver and the argument do not represent the same object."
	^(self = anObject) == false! !

!UndefinedObject methodsFor: 'testing'!
isNil
	"Answer true because the receiver is nil."
	^true!

notNil
	"Answer false because the receiver is nil."
	^false! !

!Behavior methodsFor: 'instance creation'!
basicNew
	"Primitive. Answer an instance of the receiver (which is a class) with no 
	indexable variables. Fail if the class is indexable. Essential. See Object 
	documentation whatIsAPrimitive."
	<primitive: 70>
	self primitiveFailed!

basicNew: anInteger
	"Primitive. Answer an instance of the receiver (which is a class) whose number 
	of indexable variables is the argument, anInteger. Fail if the class is not indexable 
	or if anInteger is not a positive Integer. Essential. See Object documentation 
	whatIsAPrimitive."
	<primitive: 71>
	self primitiveFailed!

new
	"Answer a new instance of the receiver."
	^self basicNew! !

!Class methodsFor: 'instance creation'!
new
	"Answer a new instance of the receiver. If the receiver is indexable, it will have zero 
	indexable variables."
	^self basicNew!

new: anInteger
	"Answer a new instance of the receiver with anInteger number of indexable 
	variables. This method fails if the receiver is not indexable."
	^self basicNew: anInteger! !

!Class methodsFor: 'subclass creation'!
subclass: className instanceVariableNames: instVarString classVariableNames: classVarString poolDictionaries: poolString category: categoryName
	"Simplified bootstrap version - just creates the class directly in Smalltalk"
	| newClass |
	
	"For bootstrap, we'll create a minimal class"
	newClass := self basicNew.
	
	"Register it in Smalltalk"
	Smalltalk at: className put: newClass.
	
	^newClass! !

!Boolean methodsFor: 'logical operations'!
& aBoolean
	"Answer true if both the receiver and the argument are true. Otherwise answer false."
	self subclassResponsibility!

| aBoolean
	"Answer true if either the receiver or the argument is true. Otherwise answer false."
	self subclassResponsibility!

not
	"Answer the opposite boolean value of the receiver."
	self subclassResponsibility! !

!Boolean methodsFor: 'controlling'!
ifTrue: trueBlock
	"If the receiver is true, answer the value of trueBlock. Otherwise answer nil."
	self subclassResponsibility!

ifFalse: falseBlock
	"If the receiver is false, answer the value of falseBlock. Otherwise answer nil."
	self subclassResponsibility!

ifTrue: trueBlock ifFalse: falseBlock
	"If the receiver is true, answer the value of trueBlock. Otherwise answer the value of falseBlock."
	self subclassResponsibility!

ifFalse: falseBlock ifTrue: trueBlock
	"If the receiver is false, answer the value of falseBlock. Otherwise answer the value of trueBlock."
	self subclassResponsibility! !

!True methodsFor: 'logical operations'!
& aBoolean
	"Answer true if both the receiver and the argument are true."
	^aBoolean!

| aBoolean
	"Answer true because the receiver is true."
	^self!

not
	"Answer false because the receiver is true."
	^false! !

!True methodsFor: 'controlling'!
ifTrue: trueBlock
	"Answer the value of trueBlock because the receiver is true."
	^trueBlock value!

ifFalse: falseBlock
	"Answer nil because the receiver is true."
	^nil!

ifTrue: trueBlock ifFalse: falseBlock
	"Answer the value of trueBlock because the receiver is true."
	^trueBlock value!

ifFalse: falseBlock ifTrue: trueBlock
	"Answer the value of trueBlock because the receiver is true."
	^trueBlock value! !

!False methodsFor: 'logical operations'!
& aBoolean
	"Answer false because the receiver is false."
	^self!

| aBoolean
	"Answer true if the argument is true."
	^aBoolean!

not
	"Answer true because the receiver is false."
	^true! !

!False methodsFor: 'controlling'!
ifTrue: trueBlock
	"Answer nil because the receiver is false."
	^nil!

ifFalse: falseBlock
	"Answer the value of falseBlock because the receiver is false."
	^falseBlock value!

ifTrue: trueBlock ifFalse: falseBlock
	"Answer the value of falseBlock because the receiver is false."
	^falseBlock value!

ifFalse: falseBlock ifTrue: trueBlock
	"Answer the value of falseBlock because the receiver is false."
	^falseBlock value! !

!SmallInteger methodsFor: 'arithmetic'!
+ aNumber
	"Primitive. Add the receiver to aNumber and answer the result."
	<primitive: 1>
	^super + aNumber!

- aNumber
	"Primitive. Subtract aNumber from the receiver and answer the result."
	<primitive: 2>
	^super - aNumber!

* aNumber
	"Primitive. Multiply the receiver by aNumber and answer the result."
	<primitive: 9>
	^super * aNumber! !

!SmallInteger methodsFor: 'comparing'!
< aNumber
	"Primitive. Answer true if the receiver is less than aNumber."
	<primitive: 3>
	^super < aNumber!

> aNumber
	"Primitive. Answer true if the receiver is greater than aNumber."
	<primitive: 4>
	^super > aNumber!

<= aNumber
	"Primitive. Answer true if the receiver is less than or equal to aNumber."
	<primitive: 5>
	^super <= aNumber!

>= aNumber
	"Primitive. Answer true if the receiver is greater than or equal to aNumber."
	<primitive: 6>
	^super >= aNumber!

= aNumber
	"Primitive. Answer true if the receiver equals aNumber."
	<primitive: 7>
	^super = aNumber! !

!Dictionary methodsFor: 'accessing'!
at: key
	"Answer the value at key. If key is not found, create an error message."
	^self at: key ifAbsent: [self errorKeyNotFound]!

at: key put: anObject
	"Set the value at key to be anObject. If key is not found, create a new 
	entry for key and set its value to anObject. Answer anObject."
	| index element |
	index := self findKeyOrNil: key.
	element := self basicAt: index.
	element == nil
		ifTrue: [self atNewIndex: index put: (Association key: key value: anObject)]
		ifFalse: [element value: anObject].
	^anObject! !

!BlockContext methodsFor: 'evaluating'!
value
	"Primitive. Evaluate the block represented by the receiver. Essential."
	<primitive: 81>
	^self primitiveFailed! !