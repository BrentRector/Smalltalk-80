TestClass subclass: #BlockArgTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test'!

!BlockArgTest class methodsFor: 'testing'!

test
	"Test block argument access"
	1 to: 3 do: [:i | Transcript show: i printString]
! !
