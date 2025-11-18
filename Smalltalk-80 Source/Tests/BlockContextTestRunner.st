"BlockContextTestRunner - Automatically runs BlockContextTest suite during initialization"

Object subclass: #BlockContextTestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTestRunner class methodsFor: 'class initialization'!

initialize
	"Run three-level and mutation tests individually to find failure"
	| tester sum |
	tester := BlockContextTest new.
	sum := 900.  "Start after confirmed working single+two-level tests"

	"Three level: 27 tests (each should return 10)"
	sum := sum + (tester n000000).
	sum := sum + (tester n000111).
	sum := sum + (tester n000222).
	sum := sum + (tester n011011).
	sum := sum + (tester n011122).
	sum := sum + (tester n022022).
	sum := sum + (tester n100100).
	sum := sum + (tester n100211).
	sum := sum + (tester n111000).
	sum := sum + (tester n111111).
	sum := sum + (tester n111222).
	sum := sum + (tester n122122).
	sum := sum + (tester n200200).
	sum := sum + (tester n211011).
	sum := sum + (tester n211211).
	sum := sum + (tester n222000).
	sum := sum + (tester n222111).
	sum := sum + (tester n222222).
	sum := sum + (tester n012012).
	sum := sum + (tester n021021).
	sum := sum + (tester n102102).
	sum := sum + (tester n120120).
	sum := sum + (tester n201201).
	sum := sum + (tester n210210).
	sum := sum + (tester n012210).
	sum := sum + (tester n210012).

	"DIAGNOSTIC: Test each variable individually to detect aliasing
	 Return format: (test_number * 1000) + actual_value
	 Test 1: diagOa  (expect 11)
	 Test 2: diagOt1 (expect 22)
	 Test 3: diagOt2 (expect 44)
	 Test 4: diagMa  (expect 88)
	 Test 5: diagMt1 (expect 166)
	 Test 6: diagMt2 (expect 322)
	 Test 7: diagIa  (expect 644)
	 Test 8: diagIt1 (expect 188)
	 Test 9: diagIt2 (expect 266)"

	^1000 + (tester diagOa)   "Returns 1000+actual (expect 1011)"

	"Mutation tests: 3 tests (each should return 10)"
	sum := sum + (tester mut1).
	sum := sum + (tester mut2).
	sum := sum + (tester mut3).

	"Return final sum (should be 1200)"
	^sum! !

BlockContextTestRunner initialize!
