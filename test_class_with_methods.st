!Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!

!Point methodsFor: 'accessing'!

x
    "Return the x coordinate"
    ^x!

y
    "Return the y coordinate"
    ^y!

x: newX
    "Set the x coordinate"
    x := newX!

y: newY
    "Set the y coordinate"
    y := newY! !

!Point methodsFor: 'arithmetic'!

+ aPoint
    "Answer a new Point that is the sum of the receiver and aPoint"
    ^self class x: x + aPoint x y: y + aPoint y!

- aPoint
    "Answer a new Point that is the difference of the receiver and aPoint"
    ^self class x: x - aPoint x y: y - aPoint y! !

!Point class methodsFor: 'instance creation'!

x: xValue y: yValue
    "Answer a new Point with the given coordinates"
    ^self new x: xValue; y: yValue; yourself! !