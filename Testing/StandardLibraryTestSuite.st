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
	  undefinedObjectTests collectionTests arrayTests stringTests blockContextTests |
	
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
	
	"Run all test suites"
	self announceTest: 'Object Tests'.
	objectTests performTest.
	
	self announceTest: 'Boolean Tests'.
	booleanTests performTest.
	
	self announceTest: 'Number Tests'.
	numberTests performTest.
	
	self announceTest: 'SmallInteger Tests'.
	smallIntegerTests performTest.
	
	self announceTest: 'Character Tests'.
	characterTests performTest.
	
	self announceTest: 'UndefinedObject Tests'.
	undefinedObjectTests performTest.
	
	self announceTest: 'Collection Tests'.
	collectionTests performTest.
	
	self announceTest: 'Array Tests'.
	arrayTests performTest.
	
	self announceTest: 'String Tests'.
	stringTests performTest.
	
	self announceTest: 'BlockContext Tests'.
	blockContextTests performTest.
	
	self announceTest: 'All Standard Library Tests Completed Successfully'!

announceTest: testName
	"Announce the start of a test suite"
	Transcript show: '--- Running: ', testName, ' ---'; cr! !

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
