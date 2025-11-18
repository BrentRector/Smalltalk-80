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

	"DIAGNOSTIC: Run n121212diag instead of n121212"
	"Expected: 511 (all 9 variables), Previous actual: 1 (only oa)"
	^(tester n121212diag)  "Return diagnostic result to decode which variables work"

	"Mutation tests: 3 tests (each should return 10)"
	sum := sum + (tester mut1).
	sum := sum + (tester mut2).
	sum := sum + (tester mut3).

	"Return final sum (should be 1200)"
	^sum! !

BlockContextTestRunner initialize!
