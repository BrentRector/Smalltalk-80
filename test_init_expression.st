Object subclass: #TestClass
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'accessing'!

value
    ^value!

value: anObject
    value _ anObject! !

"This is an initialization expression"
Smalltalk at: #TestGlobal put: 42!