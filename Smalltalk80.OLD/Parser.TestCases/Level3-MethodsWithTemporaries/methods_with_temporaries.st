Object subclass: #ClassWithTemporaries
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Temporaries'!

!ClassWithTemporaries methodsFor: 'computing'!

compute
    | temp1 temp2 |
    temp1 _ 10.
    temp2 _ 20.
    ^temp1 + temp2!

processData: data
    | result index |
    result _ 0.
    index _ 1.
    ^result!

complexCalculation: x with: y
    | intermediate1 intermediate2 final |
    intermediate1 _ x + y.
    intermediate2 _ x - y.
    final _ intermediate1 * intermediate2.
    ^final! !
