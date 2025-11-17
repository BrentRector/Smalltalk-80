"BlockContextTestRunner - Automatically runs BlockContextTest suite during initialization"

Object subclass: #BlockContextTestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTestRunner class methodsFor: 'class initialization'!

initialize
	"Automatically run BlockContextTest suite and report result.
	Expected result: 3503 (sum of all 194 test return values)"
	| tester result |
	tester := BlockContextTest new.
	result := tester testAllCombinations.
	^ result! !

BlockContextTestRunner initialize!
