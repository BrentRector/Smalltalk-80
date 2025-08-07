!Object methodsFor: 'testing globals'!
testGlobals
    "Test that global variables are working"
    | result |
    result := OrderedCollection new.
    
    "Test 1: Smalltalk global exists"
    Smalltalk notNil ifTrue: [result add: 'Smalltalk exists'].
    
    "Test 2: Transcript global exists"  
    Transcript notNil ifTrue: [result add: 'Transcript exists'].
    
    "Test 3: Can store in Smalltalk"
    Smalltalk at: #MyTestGlobal put: 123.
    (Smalltalk at: #MyTestGlobal) = 123 ifTrue: [result add: 'Store/retrieve works'].
    
    ^result! !