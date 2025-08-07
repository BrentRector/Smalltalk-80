Object subclass: #TestStandalone
	instanceVariableNames: ''
	classVariableNames: 'TestValue '
	poolDictionaries: ''
	category: 'Test'!

!TestStandalone class methodsFor: 'testing'!
setTestValue
	TestValue _ 'Standalone statement executed!'.
	Transcript show: 'TestStandalone>>setTestValue was called'; cr!

getTestValue
	^TestValue! !

"This is a standalone statement that should be executed during loading"
TestStandalone setTestValue!

"Another standalone statement"
'This standalone string should be evaluated' printNl!