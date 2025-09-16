Magnitude subclass: #TestKey
	instanceVariableNames: 'myKey'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Simple'!

TestKey subclass: #TestPair
	instanceVariableNames: 'myValue'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Simple'!

!TestKey methodsFor: 'accessing'!

getKey
	^myKey!

setKey: aKey
	myKey := aKey! !

!TestPair methodsFor: 'accessing'!

getValue
	^myValue!

setValue: aValue
	myValue := aValue!

getBoth
	"Access both inherited myKey and local myValue"
	^Array with: myKey with: myValue! !