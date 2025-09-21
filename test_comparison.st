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
    ^(x + aPoint x) @ (y + aPoint y)!

- aPoint
    ^(x - aPoint x) @ (y - aPoint y)!

* scale
    ^(x * scale) @ (y * scale)! !

!Point class methodsFor: 'instance creation'!

x: xValue y: yValue
    ^self new x: xValue; y: yValue; yourself! !