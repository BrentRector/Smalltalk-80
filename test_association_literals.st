| testClass |
"Test that Association literals are created correctly"

testClass := Object subclass: #TestAssociationLiterals
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'.

testClass compile: 'testMethod
    | assoc |
    assoc := Association key: #test value: 42.
    ^assoc' classified: 'testing'.

"Now test if the Association literal was created correctly"
| method literals |
method := testClass compiledMethodAt: #testMethod.
literals := method literals.

literals do: [:literal |
    Transcript cr; show: 'Literal: ', literal printString.
    (literal isKindOf: Association) ifTrue: [
        Transcript cr; show: '  Association key: ', literal key printString.
        Transcript cr; show: '  Association value: ', literal value printString]].

Transcript cr; show: 'Test complete'.