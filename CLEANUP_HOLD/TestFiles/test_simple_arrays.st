!TestClass methodsFor: 'test arrays'!
testSimpleArray
    | result |
    result _ #(a b c).
    ^result!
testNumberArray
    | result |
    result _ #(1 2 3).
    ^result!
testStringArray
    | result |
    result _ #('hello' 'world').
    ^result! !