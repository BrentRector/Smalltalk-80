Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-InstanceVars'!

!Point methodsFor: 'accessing'!

x
    ^x!

x: newX
    x _ newX!

y
    ^y!

y: newY
    y _ newY!

setX: newX y: newY
    x _ newX.
    y _ newY!

distanceFromOrigin
    | xSquared ySquared |
    xSquared _ x * x.
    ySquared _ y * y.
    ^(xSquared + ySquared) sqrt! !
