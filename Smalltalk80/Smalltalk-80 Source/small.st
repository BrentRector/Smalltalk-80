!TestClass methodsFor: 'testing'!

testKeywordMessage
    textMode == #comment ifTrue: [^true].

!

testCascade
    aStream nextPut: $a; nextPut: $b; nextPut: $c.

!

!
