"Test if Object class variables were initialized"
| depFields errorRec |
depFields _ Object instVarNamed: #DependentsFields.
errorRec _ Object instVarNamed: #ErrorRecursion.

'Testing Object initialization:' printNl.
'  DependentsFields: ' print.
depFields printNl.
'  ErrorRecursion: ' print. 
errorRec printNl.

depFields isNil
	ifTrue: ['ERROR: DependentsFields is nil!' printNl]
	ifFalse: ['SUCCESS: DependentsFields was initialized' printNl].
	
errorRec isNil
	ifTrue: ['ERROR: ErrorRecursion is nil!' printNl]
	ifFalse: [
		'SUCCESS: ErrorRecursion was initialized to: ' print.
		errorRec printNl
	]!