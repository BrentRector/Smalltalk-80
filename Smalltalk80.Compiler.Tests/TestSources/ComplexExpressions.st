Object subclass: #ComplexExpressions
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Expressions'!

!ComplexExpressions methodsFor: 'arithmetic'!

complexArithmetic
    "Test complex arithmetic expressions"
    | a b c result |
    a := 10.
    b := 20.
    c := 30.
    result := ((a + b) * c - (a * 2)) / (b - a).
    ^result!

chainedOperations
    "Test chained binary operations"
    ^1 + 2 * 3 - 4 / 2 + 5!

nestedParentheses
    "Test deeply nested parentheses"
    ^((((1 + 2) * (3 + 4)) - ((5 * 6) / 2)) + 10)! !

!ComplexExpressions methodsFor: 'conditionals'!

nestedConditionals: aNumber
    "Test nested if statements"
    aNumber < 0 ifTrue: [
        aNumber < -10 
            ifTrue: [^'very negative']
            ifFalse: [^'negative']].
    aNumber = 0 ifTrue: [^'zero'].
    aNumber < 10
        ifTrue: [^'small positive']
        ifFalse: [
            aNumber < 100
                ifTrue: [^'medium positive']
                ifFalse: [^'large positive']]!

guardClauses: aValue
    "Test multiple guard clauses"
    aValue isNil ifTrue: [^'nil'].
    aValue = 0 ifTrue: [^'zero'].
    aValue < 0 ifTrue: [^'negative'].
    ^'positive'! !

!ComplexExpressions methodsFor: 'blocks'!

blockComposition
    "Test complex block operations"
    | add mult compose |
    add := [:x | x + 10].
    mult := [:x | x * 2].
    compose := [:f :g | [:x | f value: (g value: x)]].
    ^(compose value: add value: mult) value: 5!

recursiveBlock
    "Test recursive block"
    | factorial |
    factorial := [:n | 
        n <= 1 
            ifTrue: [1]
            ifFalse: [n * (factorial value: n - 1)]].
    ^factorial value: 5!

blockWithMultipleParams
    "Test block with multiple parameters"
    | calculate |
    calculate := [:a :b :c | (a + b) * c].
    ^calculate value: 2 value: 3 value: 4! !

!ComplexExpressions methodsFor: 'cascades'!

cascadeExample
    "Test message cascades"
    | array |
    array := Array new: 5.
    array 
        at: 1 put: 'first';
        at: 2 put: 'second';
        at: 3 put: 'third';
        at: 4 put: 'fourth';
        at: 5 put: 'fifth'.
    ^array!

cascadeWithBlocks
    "Test cascades with block arguments"
    ^OrderedCollection new
        add: 1;
        add: 2;
        add: 3;
        select: [:each | each > 1];
        collect: [:each | each * 2];
        yourself! !