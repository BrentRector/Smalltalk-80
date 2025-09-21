Object subclass: #TestClass
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'accessing'!

x
    ^x!

x: anObject
    x _ anObject!

y
    ^y!

y: anObject
    y _ anObject!
! !

!TestClass methodsFor: 'arithmetic'!

add: aNumber
    ^x + aNumber!

multiply: aNumber
    ^x * aNumber!
! !

!TestClass methodsFor: 'testing'!

testMethod
    | temp |
    temp _ 42.
    ^temp + 1!
! !