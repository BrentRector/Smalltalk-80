TestClass methodsFor: 'testing' !

testMethod
    | collection |
    collection := #(1 2 3).
    collection do: [:index | index printString].
    ^nil !
