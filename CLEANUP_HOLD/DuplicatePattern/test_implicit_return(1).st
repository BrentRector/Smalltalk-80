Object subclass: #TestImplicitReturn
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing'!

!TestImplicitReturn methodsFor: 'testing'!
simpleMethod
	"This method should implicitly return self"
	1 + 2!
	
explicitReturnSelf
	"This method explicitly returns self"
	1 + 2.
	^self!
	
explicitReturnValue
	"This method explicitly returns a value"
	^42!
	
methodWithStatements
	"Multiple statements should all pop except implicit return self"
	1 + 2.
	3 + 4.
	5 + 6! !
