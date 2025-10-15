"Test case to reproduce literal duplication issue"

Object subclass: #LiteralTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests'!

!LiteralTest class methodsFor: 'testing'!

testDuplicateStrings
    "This method has the same string appearing in two places:
     1. Inside a nested array literal
     2. As a direct string literal
     This should create TWO separate String objects (not interned)"
    | result |
    result := Array with: (Array with: 'duplicate string' with: #foo)
                   with: 'duplicate string'.
    ^result!

testNestedArrays
    "This method has nested array literals with strings"
    ^#(
        ('first' #a)
        ('second' #b)
        'first'
    )!
! !
