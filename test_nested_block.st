Object subclass: #TestClass
    instanceVariableNames: 'tally'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'testing'!

do: aBlock
    "Test method with nested block that accesses method parameter"
    [:index | aBlock value: index]
! !
