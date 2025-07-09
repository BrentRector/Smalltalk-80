!PrecedenceTest methodsFor: 'example-tests'!

testFullExpression
    | aStream data result |
    data := #(1 2 3) at: 2.
    aStream := WriteStream on: String new.
    result := data asString size * 2.
    (result > 4) ifTrue: [
        aStream
            nextPutAll: 'Big';
            space;
            nextPutAll: 'Data = ', data asString
    ] ifFalse: [
        aStream
            nextPutAll: 'Small'; cr
    ].
!

!
