"Test class for negative number parsing in array literals"

Object subclass: #ArrayLiteralNegativeTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests'!

!ArrayLiteralNegativeTest class methodsFor: 'testing'!

testSimpleNegativeInteger
    "Test that #(-5) parses correctly as an array with one negative integer"
    | result |
    result := #(-5).
    ^ (result size = 1) and: [(result at: 1) = -5]
!

testSimpleNegativeFloat
    "Test that #(-3.14) parses correctly as an array with one negative float"
    | result |
    result := #(-3.14).
    ^ (result size = 1) and: [(result at: 1) < 0]
!

testMixedPositiveNegative
    "Test that #(1 -2 3 -4) parses correctly"
    | result |
    result := #(1 -2 3 -4).
    ^ (result size = 4)
        and: [(result at: 1) = 1]
        and: [(result at: 2) = -2]
        and: [(result at: 3) = 3]
        and: [(result at: 4) = -4]
!

testOriginalBugCase
    "Test the original bug report: #(-0.166667 0.00833333)"
    | result |
    result := #(-0.166667 0.00833333).
    ^ (result size = 2)
        and: [(result at: 1) < 0]
        and: [(result at: 2) > 0]
!

testNestedNegative
    "Test nested arrays with negative numbers: #((1 -1) (2 -2))"
    | result inner1 inner2 |
    result := #((1 -1) (2 -2)).
    inner1 := result at: 1.
    inner2 := result at: 2.
    ^ (result size = 2)
        and: [(inner1 at: 1) = 1]
        and: [(inner1 at: 2) = -1]
        and: [(inner2 at: 1) = 2]
        and: [(inner2 at: 2) = -2]
!

testMinusSymbolAlone
    "Test that #(-) parses as a symbol, not a number"
    | result |
    result := #(-).
    ^ (result size = 1) and: [(result at: 1) = #-]
!

testMinusSymbolWithSpace
    "Test that #(- 5) parses as TWO elements: symbol and number"
    | result |
    result := #(- 5).
    ^ (result size = 2)
        and: [(result at: 1) = #-]
        and: [(result at: 2) = 5]
!

runAllTests
    "Run all tests and return the number of passed tests"
    | passed |
    passed := 0.
    self testSimpleNegativeInteger ifTrue: [passed := passed + 1].
    self testSimpleNegativeFloat ifTrue: [passed := passed + 1].
    self testMixedPositiveNegative ifTrue: [passed := passed + 1].
    self testOriginalBugCase ifTrue: [passed := passed + 1].
    self testNestedNegative ifTrue: [passed := passed + 1].
    self testMinusSymbolAlone ifTrue: [passed := passed + 1].
    self testMinusSymbolWithSpace ifTrue: [passed := passed + 1].
    ^ passed
! !

"Run the tests"
ArrayLiteralNegativeTest runAllTests!
