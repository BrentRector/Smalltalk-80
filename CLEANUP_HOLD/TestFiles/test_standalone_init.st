nil subclass: #TestInitClass
    instanceVariableNames: ''
    classVariableNames: 'InitializationFlag'
    poolDictionaries: ''
    category: 'Testing'!

!TestInitClass class methodsFor: 'initialization'!
initialize
    "Initialize the class"
    InitializationFlag := #initialized.
    Transcript show: 'TestInitClass initialized'; cr! !

!TestInitClass class methodsFor: 'testing'!
checkInitialized
    "Check if class was initialized"
    ^InitializationFlag! !

TestInitClass initialize!

Transcript show: 'Standalone expression executed!'; cr!

Smalltalk at: #TestGlobal put: 42!