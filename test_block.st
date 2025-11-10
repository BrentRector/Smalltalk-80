"Test file for block primitive fix"

Object subclass: #TestBlock
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TestBlock methodsFor: 'testing'!

testSimpleBlock
	"Test that blocks with 0 arguments work correctly"
	| result |
	result _ [42] value.
	^result! !

!TestBlock class methodsFor: 'running'!

run
	"Run the test"
	^self new testSimpleBlock! !
