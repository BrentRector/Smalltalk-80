Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!

!Point methodsFor: 'accessing'!

x
    ^x!

y  
    ^y!

x: newX
    x := newX!

y: newY
    y := newY! !

!Point methodsFor: 'arithmetic'!

+ aPoint
    ^self class new
        x: x + aPoint x;
        y: y + aPoint y;
        yourself! !

!Point class methodsFor: 'instance creation'!

x: xValue y: yValue
    ^self new
        x: xValue;
        y: yValue;
        yourself! !