Object subclass: #FloatTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!FloatTest class methodsFor: 'testing'!
testFloatInit
	| pi |
	pi _ Float instVarNamed: #Pi.
	pi isNil
		ifTrue: [Transcript show: 'FAILED: Float Pi is nil'; cr. ^false]
		ifFalse: [
			Transcript show: 'SUCCESS: Float Pi = '.
			Transcript show: pi printString; cr.
			^true
		]! !

FloatTest testFloatInit!