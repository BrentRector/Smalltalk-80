| t f result |

"Create boolean instances"
t _ true.
f _ false.

"Test basic not operation"
result _ t not.
result _ f not.

"Test and operations"
result _ t & t.
result _ t & f.
result _ f & t.
result _ f & f.

"Test or operations"  
result _ t | t.
result _ t | f.
result _ f | t.
result _ f | f.

"Test control flow - ifTrue:"
t ifTrue: [result _ 1].
f ifTrue: [result _ 2].

"Test control flow - ifFalse:"
t ifFalse: [result _ 3].
f ifFalse: [result _ 4].

"Test control flow - ifTrue:ifFalse:"
result _ t ifTrue: [5] ifFalse: [6].
result _ f ifTrue: [7] ifFalse: [8].

"Test short-circuit evaluation - and:"
result _ t and: [true].
result _ f and: [true].

"Test short-circuit evaluation - or:"
result _ t or: [false].
result _ f or: [false].