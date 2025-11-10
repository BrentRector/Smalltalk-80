"Simple test for block temp bug"

Object subclass: #BlockContextTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!BlockContextTest methodsFor: 's00 working test'!
s00 "[0arg,0temp] access method temp"
	| m | m _ 10. ^[m] value! !

!BlockContextTest methodsFor: 's01 buggy test'!
s01 "[0arg,1temp] use own temp"
	^[| t1 | t1 _ 10. t1] value! !

"Test both"
BlockContextTest new s00!
BlockContextTest new s01!
