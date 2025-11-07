"Simple block execution test to debug infinite recursion issue"

Object subclass: #SimpleBlockExecutionTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests'! !

!SimpleBlockExecutionTest methodsFor: 'testing'!

testConstantBlock
    "Test the simplest possible block - no arguments, returns constant"
    ^ [42] value!

testOneArgBlock
    "Test block with one argument"
    ^ [:x | x + 1] value: 5!

testTwoArgBlock
    "Test block with two arguments"
    ^ [:x :y | x + y] value: 3 value: 4!

run
    "Execute all tests"
    | test1 test2 test3 |

    test1 _ self testConstantBlock.
    test2 _ self testOneArgBlock.
    test3 _ self testTwoArgBlock.

    ^ test1 + test2 + test3  "Should return 42 + 6 + 7 = 55"! !

SimpleBlockExecutionTest new run!
