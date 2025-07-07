Object subclass: #ExpressionSamples
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Parser-Tests'!

!ExpressionSamples methodsFor: 'examples'!
unaryExample
    ^self class
!

binaryExample
    ^1 + 2
!

keywordExample
    ^self perform: #binaryExample
!

assignmentExample
    | x |
    x := 3.
    ^x
!

cascadeExample
    | obj |
    obj := self.
    obj doThis; doThat: 1.
    ^obj
!



characterExample
    ^$a
!

! !
