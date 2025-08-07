!Object methodsFor: 'testing'!
testSmalltalkDict
    "Test the Smalltalk global dictionary"
    | result |
    
    "Store a global variable"
    Smalltalk at: #TestGlobal put: 42.
    
    "Retrieve it"
    result := Smalltalk at: #TestGlobal.
    
    ^result! !