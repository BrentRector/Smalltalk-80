Object subclass: #PrimitiveClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Primitives'!

!PrimitiveClass methodsFor: 'primitives'!

basicNew
    <primitive: 70>
    ^self basicNew!

basicAt: index
    <primitive: 60>
    self error: 'index out of bounds'!

basicAt: index put: value
    <primitive: 61>
    self error: 'index out of bounds'!

size
    <primitive: 62>
    ^0!
