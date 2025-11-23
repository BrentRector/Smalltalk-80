"Test cases for negative numbers in array literals"

"Test 1: Simple negative integer"
#(-5)

"Test 2: Simple negative float"
#(-3.14159)

"Test 3: Negative float with scientific notation"
#(-1.5e10)

"Test 4: Original bug report case - negative floats"
#(-0.166667 0.00833333)

"Test 5: Mix of positive and negative integers"
#(1 -2 3 -4 5)

"Test 6: Mix of positive and negative floats"
#(1.5 -2.5 3.14 -0.001)

"Test 7: Nested array with negative numbers"
#((1 -1) (2 -2) (3 -3))

"Test 8: All negative numbers"
#(-1 -2 -3 -4 -5)

"Test 9: Negative numbers with symbols (should distinguish correctly)"
#(- 5)         "Symbol minus followed by number 5 (two elements)"

"Test 10: Negative zero"
#(-0)

"Test 11: Very small negative float"
#(-0.000001)

"Test 12: Radix notation negative (if supported)"
#(-16rFF)

"Test 13: Complex nested structure"
#(1 (-2 3) -4 (5 (-6 7)))

"Test 14: Negative with decimal fraction"
#(-0.5 -1.5 -2.5)

"Test 15: Binary operator symbols vs negative numbers"
#(+ - * /)      "Four symbols"
#(+1 -1)        "Positive and negative numbers (if parser supports +1)"

"Test 16: Edge case - just a minus symbol"
#(-)            "Should be symbol #-"

"Test 17: Multiple minus symbols"
#(- - -)        "Three symbol elements"

"Test 18: Scaled decimal notation (if supported)"
#(-1s2)

"Test 19: Large negative number"
#(-999999999)

"Test 20: Scientific notation variations"
#(-1e-5 -2.5e10 -3.14e-2)
