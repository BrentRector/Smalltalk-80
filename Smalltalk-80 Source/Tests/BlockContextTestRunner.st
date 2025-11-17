"BlockContextTestRunner - Automatically runs BlockContextTest suite during initialization"

Object subclass: #BlockContextTestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTestRunner class methodsFor: 'class initialization'!

initialize
	"Run individual test methods to isolate failures"
	| tester sum |
	tester := BlockContextTest new.
	sum := 0.

	"Single level: 9 tests (should total 90)"
	sum := sum + (tester s00).
	sum := sum + (tester s01).
	sum := sum + (tester s02).
	sum := sum + (tester s10).
	sum := sum + (tester s11).
	sum := sum + (tester s12).
	sum := sum + (tester s20).
	sum := sum + (tester s21).
	sum := sum + (tester s22).

	"If we got this far, single-level tests passed (sum should be 90)"
	sum = 90 ifFalse: [^sum negated].  "Return negative to indicate single-level failure"

	"Two level: 81 tests (should add 810 for total 900)"
	sum := sum + (tester t0000).
	sum := sum + (tester t0001).
	sum := sum + (tester t0002).
	sum := sum + (tester t0010).
	sum := sum + (tester t0011).
	sum := sum + (tester t0012).
	sum := sum + (tester t0020).
	sum := sum + (tester t0021).
	sum := sum + (tester t0022).
	sum := sum + (tester t0100).
	sum := sum + (tester t0101).
	sum := sum + (tester t0102).
	sum := sum + (tester t0110).
	sum := sum + (tester t0111).
	sum := sum + (tester t0112).
	sum := sum + (tester t0120).
	sum := sum + (tester t0121).
	sum := sum + (tester t0122).
	sum := sum + (tester t0200).
	sum := sum + (tester t0201).
	sum := sum + (tester t0202).
	sum := sum + (tester t0210).
	sum := sum + (tester t0211).
	sum := sum + (tester t0212).
	sum := sum + (tester t0220).
	sum := sum + (tester t0221).
	sum := sum + (tester t0222).
	sum := sum + (tester t1000).
	sum := sum + (tester t1001).
	sum := sum + (tester t1002).
	sum := sum + (tester t1010).
	sum := sum + (tester t1011).
	sum := sum + (tester t1012).
	sum := sum + (tester t1020).
	sum := sum + (tester t1021).
	sum := sum + (tester t1022).
	sum := sum + (tester t1100).
	sum := sum + (tester t1101).
	sum := sum + (tester t1102).
	sum := sum + (tester t1110).
	sum := sum + (tester t1111).
	sum := sum + (tester t1112).
	sum := sum + (tester t1120).
	sum := sum + (tester t1121).
	sum := sum + (tester t1122).
	sum := sum + (tester t1200).
	sum := sum + (tester t1201).
	sum := sum + (tester t1202).
	sum := sum + (tester t1210).
	sum := sum + (tester t1211).
	sum := sum + (tester t1212).
	sum := sum + (tester t1220).
	sum := sum + (tester t1221).
	sum := sum + (tester t1222).
	sum := sum + (tester t2000).
	sum := sum + (tester t2001).
	sum := sum + (tester t2002).
	sum := sum + (tester t2010).
	sum := sum + (tester t2011).
	sum := sum + (tester t2012).
	sum := sum + (tester t2020).
	sum := sum + (tester t2021).
	sum := sum + (tester t2022).
	sum := sum + (tester t2100).
	sum := sum + (tester t2101).
	sum := sum + (tester t2102).
	sum := sum + (tester t2110).
	sum := sum + (tester t2111).
	sum := sum + (tester t2112).
	sum := sum + (tester t2120).
	sum := sum + (tester t2121).
	sum := sum + (tester t2122).
	sum := sum + (tester t2200).
	sum := sum + (tester t2201).
	sum := sum + (tester t2202).
	sum := sum + (tester t2210).
	sum := sum + (tester t2211).
	sum := sum + (tester t2212).
	sum := sum + (tester t2220).
	sum := sum + (tester t2221).
	sum := sum + (tester t2222).

	"If we got this far, return sum (should be 900 if two-level all passed)"
	^sum! !

BlockContextTestRunner initialize!
