"BlockArgTest - Test block argument corruption bug"

Object subclass: #BlockArgTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!BlockArgTest methodsFor: 'testing'!

test
	"Simplified reproduction of Character initialize bug"
	| result |
	result _ Array new: 3.
	0 to: 2 do: [:i | | obj |
		obj _ self makeObject: i.
		result at: i + 1 put: obj].
	^result!

makeObject: value
	"Returns self to test if block arg gets corrupted"
	^self! !

!BlockArgTest class methodsFor: 'running'!

run
	^self new test! !!

BlockArgTest run!
