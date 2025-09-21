Object subclass: #SimpleClass
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Classes'!

!SimpleClass methodsFor: 'accessing'!

value
    "Answer the value"
    ^value!

value: anObject
    "Set the value"
    value := anObject! !

!SimpleClass methodsFor: 'arithmetic'!

increment
    "Increment value by 1"
    value := value + 1.
    ^value!

double
    "Double the value"
    ^value * 2!

addTo: aNumber
    "Add aNumber to value"
    ^value + aNumber! !

!SimpleClass methodsFor: 'testing'!

isPositive
    "Test if value is positive"
    ^value > 0!

isZero
    "Test if value is zero"
    ^value = 0! !

!SimpleClass class methodsFor: 'instance creation'!

withValue: aValue
    "Create instance with given value"
    ^self new value: aValue; yourself! !