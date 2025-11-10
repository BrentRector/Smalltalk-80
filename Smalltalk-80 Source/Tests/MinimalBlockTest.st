"MinimalBlockTest - Simplest possible block test"

Object subclass: #MinimalBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!MinimalBlockTest methodsFor: 'testing'!

test "Should return 42"
	^[42] value! !

!MinimalBlockTest class methodsFor: 'running'!

run
	^self new test! !

MinimalBlockTest run!
