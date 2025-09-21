!Object subclass: #TestClass
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing'!

!TestClass methodsFor: 'accessing'!

getValue
    ^value!

setValue: aValue
    value _ aValue! !

!TestClass class methodsFor: 'instance creation'!

new
    ^super new initialize!

withValue: aValue
    ^self new setValue: aValue; yourself! !