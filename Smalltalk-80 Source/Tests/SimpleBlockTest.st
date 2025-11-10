"Simple test to trace literal and temp indexing"

Object subclass: #SimpleBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!SimpleBlockTest methodsFor: 'testing'!

simpleTest
	"Outer block with 1 arg, inner block with 2 args, using literals"
	^[[:outer |
		| outerTemp |
		outerTemp _ 10.
		[:inner1 :inner2 |
			outer + outerTemp + inner1 + inner2
		] value: 5 value: 6
	] value: 1] value!
!
