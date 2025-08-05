"Master test suite to run all standard library tests"

TestCase subclass: #StandardLibraryTestSuite
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Testing-StandardLibrary'!

!StandardLibraryTestSuite methodsFor: 'running'!
performTest
	"Run the complete standard library test suite"
	| objectTests booleanTests numberTests smallIntegerTests characterTests 
	  undefinedObjectTests collectionTests arrayTests stringTests blockContextTests 
	  totalTestsRun totalTestsPassed totalTestsFailed |
	
	"Initialize counters"
	totalTestsRun _ 0.
	totalTestsPassed _ 0.
	totalTestsFailed _ 0.
	
	"Create test instances"
	objectTests _ ObjectTests new.
	booleanTests _ BooleanTests new.
	numberTests _ NumberTests new.
	smallIntegerTests _ SmallIntegerTests new.
	characterTests _ CharacterTests new.
	undefinedObjectTests _ UndefinedObjectTests new.
	collectionTests _ CollectionTests new.
	arrayTests _ ArrayTests new.
	stringTests _ StringTests new.
	blockContextTests _ BlockContextTests new.
	
	"Run all test suites and collect counts"
	self announceTest: 'Object Tests'.
	objectTests performTest.
	totalTestsRun _ totalTestsRun + objectTests testsRun.
	totalTestsPassed _ totalTestsPassed + objectTests testsPassed.
	totalTestsFailed _ totalTestsFailed + objectTests testsFailed.
	
	self announceTest: 'Boolean Tests'.
	booleanTests performTest.
	totalTestsRun _ totalTestsRun + booleanTests testsRun.
	totalTestsPassed _ totalTestsPassed + booleanTests testsPassed.
	totalTestsFailed _ totalTestsFailed + booleanTests testsFailed.
	
	self announceTest: 'Number Tests'.
	numberTests performTest.
	totalTestsRun _ totalTestsRun + numberTests testsRun.
	totalTestsPassed _ totalTestsPassed + numberTests testsPassed.
	totalTestsFailed _ totalTestsFailed + numberTests testsFailed.
	
	self announceTest: 'SmallInteger Tests'.
	smallIntegerTests performTest.
	totalTestsRun _ totalTestsRun + smallIntegerTests testsRun.
	totalTestsPassed _ totalTestsPassed + smallIntegerTests testsPassed.
	totalTestsFailed _ totalTestsFailed + smallIntegerTests testsFailed.
	
	self announceTest: 'Character Tests'.
	characterTests performTest.
	totalTestsRun _ totalTestsRun + characterTests testsRun.
	totalTestsPassed _ totalTestsPassed + characterTests testsPassed.
	totalTestsFailed _ totalTestsFailed + characterTests testsFailed.
	
	self announceTest: 'UndefinedObject Tests'.
	undefinedObjectTests performTest.
	totalTestsRun _ totalTestsRun + undefinedObjectTests testsRun.
	totalTestsPassed _ totalTestsPassed + undefinedObjectTests testsPassed.
	totalTestsFailed _ totalTestsFailed + undefinedObjectTests testsFailed.
	
	self announceTest: 'Collection Tests'.
	collectionTests performTest.
	totalTestsRun _ totalTestsRun + collectionTests testsRun.
	totalTestsPassed _ totalTestsPassed + collectionTests testsPassed.
	totalTestsFailed _ totalTestsFailed + collectionTests testsFailed.
	
	self announceTest: 'Array Tests'.
	arrayTests performTest.
	totalTestsRun _ totalTestsRun + arrayTests testsRun.
	totalTestsPassed _ totalTestsPassed + arrayTests testsPassed.
	totalTestsFailed _ totalTestsFailed + arrayTests testsFailed.
	
	self announceTest: 'String Tests'.
	stringTests performTest.
	totalTestsRun _ totalTestsRun + stringTests testsRun.
	totalTestsPassed _ totalTestsPassed + stringTests testsPassed.
	totalTestsFailed _ totalTestsFailed + stringTests testsFailed.
	
	self announceTest: 'BlockContext Tests'.
	blockContextTests performTest.
	totalTestsRun _ totalTestsRun + blockContextTests testsRun.
	totalTestsPassed _ totalTestsPassed + blockContextTests testsPassed.
	totalTestsFailed _ totalTestsFailed + blockContextTests testsFailed.
	
	self printOverallTestResults: totalTestsRun passed: totalTestsPassed failed: totalTestsFailed.
	self announceTest: 'All Standard Library Tests Completed Successfully'!

announceTest: testName
	"Announce the start of a test suite"
	Transcript show: '--- Running: ', testName, ' ---'; cr!

printOverallTestResults: totalRun passed: totalPassed failed: totalFailed
	"Print overall test statistics for all test suites"
	Transcript show: '=================================================='; cr.
	Transcript show: 'OVERALL TEST SUMMARY'; cr.
	Transcript show: '=================================================='; cr.
	Transcript show: 'Total Tests Run: ', totalRun printString; cr.
	Transcript show: 'Total Tests Passed: ', totalPassed printString; cr.
	Transcript show: 'Total Tests Failed: ', totalFailed printString; cr.
	totalFailed = 0 
		ifTrue: [ Transcript show: 'STATUS: ALL TESTS PASSED!' ]
		ifFalse: [ Transcript show: 'STATUS: SOME TESTS FAILED!' ].
	Transcript cr; show: '=================================================='; cr! !

!StandardLibraryTestSuite methodsFor: 'individual test runners'!
runObjectTests
	"Run only Object tests"
	ObjectTests new performTest!

runBooleanTests
	"Run only Boolean tests"
	BooleanTests new performTest!

runNumberTests
	"Run only Number tests"
	| numberTests smallIntegerTests |
	numberTests _ NumberTests new.
	smallIntegerTests _ SmallIntegerTests new.
	numberTests performTest.
	smallIntegerTests performTest!

runCharacterTests
	"Run only Character tests"
	CharacterTests new performTest!

runCollectionTests
	"Run only Collection tests"
	| collectionTests arrayTests stringTests |
	collectionTests _ CollectionTests new.
	arrayTests _ ArrayTests new.
	stringTests _ StringTests new.
	collectionTests performTest.
	arrayTests performTest.
	stringTests performTest!

runBlockTests
	"Run only Block tests"
	BlockContextTests new performTest! !

!StandardLibraryTestSuite class methodsFor: 'running'!
run
	"Class method to run the complete test suite"
	StandardLibraryTestSuite new performTest!

runQuick
	"Run a quick subset of tests"
	| suite |
	suite _ StandardLibraryTestSuite new.
	suite runObjectTests.
	suite runBooleanTests.
	suite runNumberTests! !
