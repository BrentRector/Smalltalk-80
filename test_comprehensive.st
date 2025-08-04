Object subclass: #ComprehensiveTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!ComprehensiveTest methodsFor: 'testing'!
testOriginalIssue
	"Test the exact issue that was originally failing: keyword message parsing with block arguments"
	| result arg0 |
	
	"This exact syntax was failing before the fix"
	arg0 := true.
	result := arg0 ifTrue: [42] ifFalse: [99].
	^result!

testVariousKeywordMessages  
	"Test various forms of keyword messages to ensure they all work"
	| result |
	
	"Simple keyword with one argument"
	result := true ifTrue: [1].
	
	"Keyword with two arguments - the original problem case"
	result := false ifTrue: [2] ifFalse: [3].
	
	"Boolean logic with block arguments"
	result := true and: [true].
	result := false or: [true].
	
	^result!

testNestedBlocks
	"Test that nested blocks work correctly"
	| result |
	result := true ifTrue: [
		false ifTrue: [1] ifFalse: [
			true ifTrue: [2] ifFalse: [3]
		]
	] ifFalse: [4].
	^result! !
