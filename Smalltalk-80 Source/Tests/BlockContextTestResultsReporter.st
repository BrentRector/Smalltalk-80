"BlockContextTestResultsReporter - Uses creative output techniques to display test results"

Object subclass: #BlockContextTestResultsReporter
	instanceVariableNames: ''
	classVariableNames: 'TestResults LastRunResults TestSummary'
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTestResultsReporter class methodsFor: 'execution'!

runAllTestsWithOutput
	"Run all tests and store results in class variable for retrieval"
	| testInstance totalSum testCount passCount failCount detailedResults summary |
	
	"Initialize"
	testInstance _ BlockContextTest new.
	totalSum _ 0.
	testCount _ 0.
	passCount _ 0.
	failCount _ 0.
	detailedResults _ ''.
	
	"Test method mapping with expected values (subset for demonstration)"
	#(
		(s00 10) (s01 10) (s02 10) (s10 10) (s11 10) (s12 10) (s20 10) (s21 10) (s22 10)
		(t0000 10) (t0001 10) (t0002 10) (ma1 42) (ma2 50) (mut1 10) (mut2 10) (mut3 10)
		(nlr1SimpleReturn 15) (nlr2NestedReturn 50) (nlr3ReturnWithMutation 30)
	) do: [:testSpec |
		| testMethod expected actual passed result |
		testMethod _ testSpec at: 1.
		expected _ testSpec at: 2.
		testCount _ testCount + 1.
		
		"Run the test safely"
		result _ [testInstance perform: testMethod] 
			on: Error 
			do: [:ex | 'ERROR: ', ex messageText].
			
		actual _ result.
		passed _ (actual = expected) and: [actual isNumber].
		
		passed ifTrue: [
			passCount _ passCount + 1.
			totalSum _ totalSum + actual.
			detailedResults _ detailedResults, testMethod asString, ': ', actual printString, ' (expected ', expected printString, ') ✓', String cr.
		] ifFalse: [
			failCount _ failCount + 1.
			detailedResults _ detailedResults, testMethod asString, ': ', actual printString, ' (expected ', expected printString, ') ✗', String cr.
		].
	].
	
	"Create summary"
	summary _ 'BlockContext Test Results Summary:', String cr,
		'Total Tested: ', testCount printString, String cr,
		'Passed: ', passCount printString, String cr, 
		'Failed: ', failCount printString, String cr,
		'Sum of Passing Tests: ', totalSum printString, String cr,
		'Pass Rate: ', (passCount * 100 // testCount) printString, '%', String cr, String cr.
	
	"Store in class variables"
	TestResults _ detailedResults.
	TestSummary _ summary.
	LastRunResults _ summary, detailedResults.
	
	"Return summary for immediate display"
	^summary!

runFullSuiteValidation
	"Run the complete test suite using the existing validate method"
	| result expectedSum actualResult |
	expectedSum _ 107504.
	result _ BlockContextTest validate.
	
	"The validate method returns 99 if all pass, otherwise returns the actual sum"
	actualResult _ result = 99 
		ifTrue: ['ALL TESTS PASSED - Sum matches expected ', expectedSum printString]
		ifFalse: ['TESTS FAILED - Actual sum: ', result printString, ', Expected: ', expectedSum printString].
	
	LastRunResults _ 'Full Suite Validation Result:', String cr, actualResult.
	^actualResult!

getLastResults
	"Return the last test run results"
	^LastRunResults ifNil: ['No tests have been run yet.']!

getDetailedResults  
	"Return detailed test results"
	^TestResults ifNil: ['No detailed results available.']!

getSummary
	"Return just the summary"
	^TestSummary ifNil: ['No summary available.']!

demonstrateOutput
	"Demonstrate multiple output techniques"
	| techniques |
	techniques _ ''.
	
	"Technique 1: Class variable storage"
	self runAllTestsWithOutput.
	techniques _ techniques, 'TECHNIQUE 1 - Class Variable Storage:', String cr, self getSummary, String cr.
	
	"Technique 2: Full suite validation"
	techniques _ techniques, 'TECHNIQUE 2 - Full Suite Validation:', String cr, self runFullSuiteValidation, String cr.
	
	"Technique 3: Return large result (may be truncated but visible)"
	LastRunResults _ 'COMPREHENSIVE RESULTS:', String cr, techniques, String cr, self getDetailedResults.
	
	"Return combined techniques result"
	^LastRunResults!

clearResults
	"Clear stored results"
	TestResults _ nil.
	TestSummary _ nil.
	LastRunResults _ nil.
	^'Results cleared.'! !

"Run demonstration"
BlockContextTestResultsReporter demonstrateOutput!