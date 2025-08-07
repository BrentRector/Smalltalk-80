"Minimal Bootstrap - Just enough to test the generator"

Object subclass: #Object
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!Object methodsFor: 'system primitives'!
== anObject
	<primitive: 110>
	self primitiveFailed!

class
	<primitive: 111>
	self primitiveFailed! !

Object subclass: #UndefinedObject
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!UndefinedObject methodsFor: 'testing'!
isNil
	^true! !

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
	^trueBlock value! !

Boolean subclass: #False
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Kernel-Objects'!

!False methodsFor: 'controlling'!
ifFalse: falseBlock
	^falseBlock value! !