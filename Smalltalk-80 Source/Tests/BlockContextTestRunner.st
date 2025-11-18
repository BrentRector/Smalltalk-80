"BlockContextTestRunner - Automatically runs BlockContextTest suite during initialization"

Object subclass: #BlockContextTestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTestRunner class methodsFor: 'class initialization'!

initialize
	"Run complete BlockContextTest suite - all 120 tests
	 Expected total: 1200 (120 tests × 10 points each)

	 DIAGNOSTIC RESULTS (all variables tested individually):
	 ✓ diagOa  → 11  (outer arg)
	 ✓ diagOt1 → 22  (outer temp 1)
	 ✓ diagOt2 → 44  (outer temp 2)
	 ✓ diagMa  → 88  (middle arg)
	 ✓ diagMt1 → 166 (middle temp 1)
	 ✓ diagMt2 → 322 (middle temp 2)
	 ✓ diagIa  → 644 (inner arg)
	 ✓ diagIt1 → 188 (inner temp 1)
	 ✓ diagIt2 → 266 (inner temp 2)

	 All variables work individually. Testing full suite..."

	^BlockContextTest new testAllCombinations! !

BlockContextTestRunner initialize!
