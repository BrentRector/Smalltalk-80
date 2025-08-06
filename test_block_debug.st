" Debug block execution step by step "

" Test 1: Simple true ifTrue with empty block - should work "
true ifTrue: [].

" Test 2: Simple true ifTrue with one statement - currently fails "
true ifTrue: [1 + 2].
