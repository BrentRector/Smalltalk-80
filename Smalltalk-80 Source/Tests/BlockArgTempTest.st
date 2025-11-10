"BlockArgTempTest - Test block argument access with block temp"

Object subclass: #BlockArgTempTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!BlockArgTempTest methodsFor: 'testing'!

test
	"Test block argument access when block also has temps"
	^1 to: 3 do: [:i | | temp |
		temp := i * 10.
		i + 1]! !

!BlockArgTempTest class methodsFor: 'running'!

run
	^self new test!!

BlockArgTempTest run!
