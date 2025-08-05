Object subclass: #TestClassMethods
    instanceVariableNames: ''
    classVariableNames: 'Counter'
    poolDictionaries: ''
    category: 'Test'!

!TestClassMethods class methodsFor: 'instance creation'!

new
    "Create a new instance and increment counter"
    Counter := Counter + 1.
    ^super new!

count
    "Return the current count"
    ^Counter!

initialize
    "Initialize the counter"
    Counter := 0! !

!TestClassMethods methodsFor: 'testing'!

test
    "A simple test method"
    ^42! !
