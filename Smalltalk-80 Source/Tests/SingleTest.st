"SingleTest - Debug single nested block with argument"

Object subclass: #SingleTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!SingleTest methodsFor: 'testing'!

test "Should return 7: ot=4 + it=3"
	^[| ot | ot _ 4. [:ia | | it | it _ 3. ot + it] value: 999] value! !

!SingleTest class methodsFor: 'running'!

run
	^self new test! !!

SingleTest run!
