Object subclass: #TestClass
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Category'!

!TestClass methodsFor: 'accessing'!
x
    ^x!

x: aValue
    x _ aValue!

y
    ^y!

y: aValue
    y _ aValue! !

!TestClass methodsFor: 'initialization'!
initialize
    x _ 0.
    y _ 0! !

!TestClass class methodsFor: 'instance creation'!
new
    ^super new initialize! !

TestClass new x: 10; y: 20!