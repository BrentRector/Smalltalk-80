"Test if InstructionStream's SpecialConstants was initialized"
| specialConstants |
specialConstants _ InstructionStream instVarNamed: #SpecialConstants.
specialConstants isNil
	ifTrue: ['ERROR: SpecialConstants is nil - initialize was NOT run!' printNl]
	ifFalse: [
		'SUCCESS: SpecialConstants was initialized to: ' print.
		specialConstants printNl.
		'It contains ' print.
		specialConstants size print.
		' elements' printNl
	]!