" Simple test for implicit return - no Transcript "

Object subclass: #SimpleTestObject
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing-ImplicitReturn'!

!SimpleTestObject methodsFor: 'testing'!

simpleMethod
    " This method has no explicit return, should return self "
    value := 42.

getValue
    " Returns the stored value "
    ^value
! !

" Simple test - create and test "
| testObj result1 |

testObj := SimpleTestObject new.
result1 := testObj simpleMethod.

" Basic checks without Transcript "
result1 == testObj.
testObj getValue = 42.
