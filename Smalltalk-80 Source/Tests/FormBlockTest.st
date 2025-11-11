"Test Form block argument access bug"

Object subclass: #FormBlockTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests'!

!FormBlockTest methodsFor: 'testing'!
testBlockArgAccess
    "Test that block can access method arguments correctly"
    | result |
    result _ self helperMethod: 10 array: #(100 200 300).
    ^ result = 200!

helperMethod: num array: anArray
    "Simulates the pattern in Form>>setExtent:fromArray:setOffset:"
    | result |
    result _ 0.
    1 to: 2 do: [:index | result _ anArray at: index].
    ^ result! !

FormBlockTest comment:
'This test class verifies that blocks can correctly access method arguments.
It reproduces the bug in Form>>setExtent:fromArray:setOffset: where the block
[:index | bits at: index put: (anArray at: index)] incorrectly accesses
the first method argument instead of the second (anArray).'!
