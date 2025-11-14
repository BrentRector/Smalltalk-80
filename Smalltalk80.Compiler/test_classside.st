Object subclass: #Float
	instanceVariableNames: ''
	classVariableNames: 'Pi'
	poolDictionaries: ''
	category: 'Test'!

!Float class methodsFor: 'class initialization'!
initialize
	"Test method"
	Pi _ 3.14159265358979323846264338327950288! !

!Float methodsFor: 'instance methods'!
asFloat
	"Return self"
	^self! !