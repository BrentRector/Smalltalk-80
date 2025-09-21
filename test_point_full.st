!Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!

!Point methodsFor: 'accessing'!

x
    ^x!

y
    ^y!

x: aNumber
    x _ aNumber!

y: aNumber
    y _ aNumber! !

!Point methodsFor: 'arithmetic'!

+ aPoint
    ^Point new x: x + aPoint x y: y + aPoint y!

- aPoint
    ^Point new x: x - aPoint x y: y - aPoint y!

* scale
    ^Point new x: x * scale y: y * scale! !

!Point methodsFor: 'initialization'!

x: xValue y: yValue
    x _ xValue.
    y _ yValue! !