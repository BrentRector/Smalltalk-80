"Test self-hosting compilation capability"
"This script tests that we can compile new Smalltalk methods from within Smalltalk"

| result testMethod |

"Test 1: Try to compile a simple method"
Transcript show: 'Testing self-hosting compilation...'; cr.

"Attempt to compile a new method on Object"
result := Object compile: 'testSelfHosting
    "A test method compiled from within Smalltalk"
    ^42' 
    classified: 'testing'.

result isNil 
    ifTrue: [
        Transcript show: 'FAILED: Could not compile method'; cr.
        ^false
    ]
    ifFalse: [
        Transcript show: 'SUCCESS: Method compiled'; cr
    ].

"Test 2: Try to execute the compiled method"
testMethod := Object new testSelfHosting.
testMethod = 42
    ifTrue: [
        Transcript show: 'SUCCESS: Compiled method executes correctly with result: '.
        Transcript show: testMethod printString; cr
    ]
    ifFalse: [
        Transcript show: 'FAILED: Method returned incorrect value: '.
        Transcript show: testMethod printString; cr
    ].

"Test 3: Try to compile a method with arguments"
result := Object compile: 'addNumbers: a and: b
    "Add two numbers"
    ^a + b'
    classified: 'arithmetic'.

result isNil
    ifTrue: [
        Transcript show: 'FAILED: Could not compile method with arguments'; cr
    ]
    ifFalse: [
        Transcript show: 'SUCCESS: Method with arguments compiled'; cr.
        testMethod := Object new addNumbers: 10 and: 20.
        testMethod = 30
            ifTrue: [
                Transcript show: 'SUCCESS: Method with args executes correctly: '.
                Transcript show: testMethod printString; cr
            ]
            ifFalse: [
                Transcript show: 'FAILED: Method with args returned: '.
                Transcript show: testMethod printString; cr
            ]
    ].

"Test 4: Try to compile a method with temps and blocks"  
result := Object compile: 'testBlock
    "Test method with block"
    | temp |
    temp := 5.
    ^[:x | x + temp] value: 10'
    classified: 'blocks'.

result isNil
    ifTrue: [
        Transcript show: 'FAILED: Could not compile method with blocks'; cr
    ]
    ifFalse: [
        Transcript show: 'SUCCESS: Method with blocks compiled'; cr.
        testMethod := Object new testBlock.
        testMethod = 15
            ifTrue: [
                Transcript show: 'SUCCESS: Block method executes correctly: '.
                Transcript show: testMethod printString; cr
            ]
            ifFalse: [
                Transcript show: 'FAILED: Block method returned: '.
                Transcript show: testMethod printString; cr
            ]
    ].

Transcript show: '=== Self-hosting compilation tests completed ==='; cr.
^true