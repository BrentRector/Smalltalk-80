" Test implicit return by creating and testing a simple object "

Object subclass: #SimpleTestObject
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing-ImplicitReturn'!

!SimpleTestObject methodsFor: 'testing'!

simpleMethod
    " This method has no explicit return, should return self "
    value := 42.

explicitReturnSelf  
    " This method explicitly returns self "
    value := 100.
    ^self

explicitReturnValue
    " This method returns a specific value "
    value := 200.
    ^value

getValue
    " Returns the stored value "
    ^value

setValue: aValue
    " Sets the value "
    value := aValue.
! !

" Create instance and test implicit returns "
| testObj result1 result2 result3 |

" Create test object "
testObj := SimpleTestObject new.

" Test 1: Method with implicit return should return self "
result1 := testObj simpleMethod.
(result1 == testObj) 
    ifTrue: [Transcript show: 'Test 1 PASSED: simpleMethod returns self'; cr]
    ifFalse: [Transcript show: 'Test 1 FAILED: simpleMethod does not return self'; cr].

" Test 2: Method with explicit return self should return self "  
result2 := testObj explicitReturnSelf.
(result2 == testObj)
    ifTrue: [Transcript show: 'Test 2 PASSED: explicitReturnSelf returns self'; cr]
    ifFalse: [Transcript show: 'Test 2 FAILED: explicitReturnSelf does not return self'; cr].

" Test 3: Method with explicit return value should return that value "
result3 := testObj explicitReturnValue.
(result3 = 200)
    ifTrue: [Transcript show: 'Test 3 PASSED: explicitReturnValue returns 200'; cr]
    ifFalse: [Transcript show: 'Test 3 FAILED: explicitReturnValue returns: ', result3 printString; cr].

" Test 4: Verify the side effects worked correctly "
(testObj getValue = 200)
    ifTrue: [Transcript show: 'Test 4 PASSED: Side effects worked correctly'; cr]
    ifFalse: [Transcript show: 'Test 4 FAILED: Side effects incorrect, value is: ', testObj getValue printString; cr].

Transcript show: 'Implicit return tests completed'; cr.
