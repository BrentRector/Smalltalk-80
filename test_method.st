ArrayedCollection subclass: #TestClass
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!TestClass class methodsFor: 'instance creation'!
with: firstObject
	"Test method with one arg"
	^self new: 1!
	
with: firstObject with: secondObject
	"Test method with two args"
	^self new: 2!
	
with: firstObject with: secondObject with: thirdObject
	"Test method with three args"
	^self new: 3! !