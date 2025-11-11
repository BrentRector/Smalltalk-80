Object subclass: #TestClass2
    instanceVariableNames: 'tally'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass2 methodsFor: 'testing'!

do: aBlock
    "Test method that matches Set>>do: pattern"
    1 to: 5 do: [:index | aBlock value: index]
! !
