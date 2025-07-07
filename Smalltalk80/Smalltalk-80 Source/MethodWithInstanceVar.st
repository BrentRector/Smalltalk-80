Object subclass: #LocalExample
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Parser-Tests'!

!LocalExample methodsFor: 'testing'!
setAndAdd: v
    value := v.
    ^value + 1
! !
