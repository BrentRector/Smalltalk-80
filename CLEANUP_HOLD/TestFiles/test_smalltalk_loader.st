"Test file for SmalltalkLoader - simple class creation"

"Create a simple test class using Smalltalk messages"
Object subclass: #TestClassFromLoader
    instanceVariableNames: 'testVar'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing'!

!TestClassFromLoader methodsFor: 'accessing'!
testVar
    ^testVar! !

!TestClassFromLoader methodsFor: 'accessing'!
testVar: aValue
    testVar _ aValue! !

!TestClassFromLoader methodsFor: 'testing'!
testMethod
    ^42! !