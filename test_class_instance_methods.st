!Object subclass: #Rectangle
    instanceVariableNames: 'origin corner'
    classVariableNames: 'DefaultExtent'
    poolDictionaries: ''
    category: 'Graphics-Primitives'!


!Rectangle methodsFor: 'accessing'!

origin
    "Answer the origin point"
    ^origin!

corner
    "Answer the corner point"
    ^corner!

origin: aPoint
    "Set the origin point"
    origin _ aPoint!

corner: aPoint
    "Set the corner point"
    corner _ aPoint! !

!Rectangle methodsFor: 'testing'!

containsPoint: aPoint
    "Answer whether aPoint is within this rectangle"
    ^(aPoint x >= origin x) &
     (aPoint y >= origin y) &
     (aPoint x <= corner x) &
     (aPoint y <= corner y)!

isEmpty
    "Answer whether this rectangle has zero area"
    ^origin = corner! !

!Rectangle methodsFor: 'transforming'!

translateBy: delta
    "Answer a new rectangle translated by delta"
    ^self class
        origin: origin + delta
        corner: corner + delta!

scaleBy: scale
    "Answer a new rectangle scaled by scale"
    ^self class
        origin: origin * scale
        corner: corner * scale! !

!Rectangle methodsFor: 'printing'!

printOn: aStream
    "Print a representation of this rectangle"
    origin printOn: aStream.
    aStream nextPutAll: ' corner: '.
    corner printOn: aStream! !

!Rectangle class methodsFor: 'instance creation'!

origin: originPoint corner: cornerPoint
    "Create a new rectangle with the given origin and corner"
    ^self new
        origin: originPoint;
        corner: cornerPoint;
        yourself!

origin: originPoint extent: extentPoint
    "Create a new rectangle with the given origin and extent"
    ^self new
        origin: originPoint;
        corner: originPoint + extentPoint;
        yourself!

fromUser
    "Let the user define a rectangle on the screen"
    | origin corner |
    origin _ Sensor waitButton.
    corner _ Sensor waitNoButton.
    ^self origin: origin corner: corner! !

!Rectangle class methodsFor: 'examples'!

example1
    "Rectangle example1"
    ^self origin: 10@10 corner: 100@100!

example2
    "Rectangle example2"
    ^self origin: 0@0 extent: 50@75! !

!Rectangle class methodsFor: 'defaults'!

defaultExtent
    "Answer the default extent for new rectangles"
    ^DefaultExtent ifNil: [100@100]!

defaultExtent: aPoint
    "Set the default extent for new rectangles"
    DefaultExtent _ aPoint! !