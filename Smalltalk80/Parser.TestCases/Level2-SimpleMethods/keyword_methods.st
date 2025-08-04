Object subclass: #ClassWithKeywordMethods
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Methods'!

!ClassWithKeywordMethods methodsFor: 'accessing'!

setValue: aValue
    ^self!

setValue: aValue withName: aName
    ^self!

at: anIndex put: aValue
    ^self!

from: startIndex to: endIndex do: aBlock
    ^self!
