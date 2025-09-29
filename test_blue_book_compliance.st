"Test Blue Book compliance features"

| a b result |

"Test 1: Basic arithmetic (should work as before)"
result := 3 + 4.
Transcript show: 'Basic arithmetic: 3 + 4 = '.
Transcript show: result printString; cr.

"Test 2: Block execution"
result := [:x | x * 2] value: 5.
Transcript show: 'Block execution: [:x | x * 2] value: 5 = '.
Transcript show: result printString; cr.

"Test 3: Control structures via blocks"
result := 5 > 3 
    ifTrue: ['greater'] 
    ifFalse: ['less'].
Transcript show: 'Control structure: 5 > 3 ifTrue/ifFalse = '.
Transcript show: result printString; cr.

"Test 4: become: primitive"
a := 'original'.
b := 'replacement'.
Transcript show: 'Before become: a = '; show: a printString; cr.
a become: b.
Transcript show: 'After become: a = '; show: a printString; cr.

"Test 5: thisContext access"
Transcript show: 'Testing thisContext access...'; cr.
[Transcript show: 'Context class: '.
 Transcript show: thisContext class printString; cr] value.

"Test 6: Non-local return from block"
result := [1 < 2 ifTrue: [^'early return']. 'normal return'] value.
Transcript show: 'Non-local return test: '.
Transcript show: result printString; cr.

"Test 7: Process creation"
Transcript show: 'Creating a new process...'; cr.
[Transcript show: 'Process executed'; cr] fork.

"Test 8: Semaphore signaling"
| sem |
sem := Semaphore new.
Transcript show: 'Signaling semaphore...'; cr.
sem signal.
Transcript show: 'Semaphore signaled'; cr.

Transcript show: '=== Blue Book compliance tests completed ==='; cr.
^true