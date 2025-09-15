"Simple test of message sending after bootstrap"

| coll result |

"First test - can we get Collection?"
coll := Smalltalk at: #Collection.
Transcript cr; show: 'Collection = ', coll printString.

"Test sending a simple message"
result := coll superclass.
Transcript cr; show: 'Collection superclass = ', result printString.

"Test class creation message"
result := Collection 
    subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''  
    category: 'Test'.
    
Transcript cr; show: 'Result of class creation = ', result printString.

"Check if it was created"
result := Smalltalk at: #TestClass ifAbsent: [nil].
result isNil
    ifTrue: [Transcript cr; show: 'ERROR: TestClass not created']
    ifFalse: [Transcript cr; show: 'SUCCESS: TestClass created = ', result printString].