"BlockContext Diagnostic Test Runner - Tests each variable individually"

Object subclass: #BlockContextDiagnostics
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextDiagnostics class methodsFor: 'diagnostics'!

testOa
	"Test outer block argument (expect 11)"
	^BlockContextTest new diagOa!

testOt1
	"Test outer block temp 1 (expect 22)"
	^BlockContextTest new diagOt1!

testOt2
	"Test outer block temp 2 (expect 44)"
	^BlockContextTest new diagOt2!

testMa
	"Test middle block argument (expect 88)"
	^BlockContextTest new diagMa!

testMt1
	"Test middle block temp 1 (expect 166)"
	^BlockContextTest new diagMt1!

testMt2
	"Test middle block temp 2 (expect 322)"
	^BlockContextTest new diagMt2!

testIa
	"Test inner block argument (expect 644)"
	^BlockContextTest new diagIa!

testIt1
	"Test inner block temp 1 (expect 188)"
	^BlockContextTest new diagIt1!

testIt2
	"Test inner block temp 2 (expect 266)"
	^BlockContextTest new diagIt2! !

"Run all 9 diagnostic tests"
BlockContextDiagnostics testOa!
BlockContextDiagnostics testOt1!
BlockContextDiagnostics testOt2!
BlockContextDiagnostics testMa!
BlockContextDiagnostics testMt1!
BlockContextDiagnostics testMt2!
BlockContextDiagnostics testIa!
BlockContextDiagnostics testIt1!
BlockContextDiagnostics testIt2!
