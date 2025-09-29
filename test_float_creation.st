"Test Float class creation and basic operations"

| x y result |

"Test 1: Convert integer to float"
x := 42 asFloat.
Transcript show: 'Integer 42 as Float: ', x printString; cr.

"Test 2: Float arithmetic"
x := 3.14.
y := 2.0.
result := x + y.
Transcript show: '3.14 + 2.0 = ', result printString; cr.

result := x - y.
Transcript show: '3.14 - 2.0 = ', result printString; cr.

result := x * y.
Transcript show: '3.14 * 2.0 = ', result printString; cr.

result := x / y.
Transcript show: '3.14 / 2.0 = ', result printString; cr.

"Test 3: Float comparison"
Transcript show: '3.14 < 2.0? ', (x < y) printString; cr.
Transcript show: '3.14 > 2.0? ', (x > y) printString; cr.
Transcript show: '3.14 = 3.14? ', (x = x) printString; cr.

"Test 4: Float truncation"
x := 3.7.
Transcript show: '3.7 truncate = ', x truncate printString; cr.

"Test 5: Check Float class exists"
Transcript show: 'Float class name: ', Float name; cr.
Transcript show: 'Float superclass: ', Float superclass name; cr.