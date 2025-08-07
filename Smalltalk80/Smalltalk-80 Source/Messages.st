!PrecedenceTest methodsFor: 'all-chains'!

testMessagePrecedence
    | a b unaryResult binaryWithUnary flag stream |

    "Initialize two numbers"
    a := 2.
    b := 3.

    "1) Unary chaining on b:
       'b factorial odd asString size' is parsed as
         ((((b factorial) odd) asString) size)
         Step-by-step evaluation
         a) b factorial • Receiver: the Integer 3 • Message: factorial → Result: Integer 6
         b) (b factorial) odd • Receiver: the Integer 6 • Message: odd → Result: Boolean false
         c) ((b factorial) odd) asString • Receiver: the Boolean false • Message: asString → 'false'
         d) ((b factorial odd) asString) size • Receiver: the String 'false' • Message: size → Result: Integer 5"
    unaryResult := b factorial odd asString size.

    "2) Binary '+' with a unaryExpression on the right:
    'a + b factorial odd size' is parsed as
        a + ((b factorial) odd size)"
    binaryWithUnary := a + b factorial odd asString size.

    "3) Keyword message chaining on the boolean result:
       '… ifTrue: […] ifFalse: […]' attaches to the whole 
       binary expression"
    flag := binaryWithUnary > 10
               ifTrue:  [ #large ]
               ifFalse: [ #small ].

    "4) Cascade messages on a WriteStream:
       Each ';' re-sends to the same receiver (stream):
         • nextPutAll: flag printString     ← printString is unary on flag
         • space                            ← unary on stream
         • nextPutAll: (a + b factorial) asString 
                                           ← keyword with expression arg
         • yourself                         ← unary on stream"
    stream := WriteStream on: String new.
    stream
        nextPutAll: flag printString;
        space;
        nextPutAll: (a + b factorial) asString;
        yourself.

    "5) Final unary on the cascade result:
       '^ stream contents size' returns the size of the built string"
    ^ stream contents size.
!

!
