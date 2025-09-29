"Test system validation"

| result |

"Test 1: Basic arithmetic"
result := 3 + 4.
Transcript show: 'Basic arithmetic: 3 + 4 = ', result printString; cr.

"Test 2: Method lookup"
result := 10 - 3.
Transcript show: 'Subtraction: 10 - 3 = ', result printString; cr.

"Test 3: Comparison"
result := 5 > 3.
Transcript show: 'Comparison: 5 > 3 = ', result printString; cr.

Transcript show: 'System validation complete!'; cr.
^true