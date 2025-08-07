" Test cases for block compilation fix "

" Test 1: Empty block should return nil "
true ifTrue: []

" Test 2: Block with statement should work "
true ifTrue: [ 'hello' ]

" Test 3: Object initialize should work now "
Object initialize

" Test 4: Nested conditionals "
true ifTrue: [ 
    false ifFalse: []
]

" Test 5: Conditional with both branches "
true ifTrue: [] ifFalse: [ 'not reached' ]
