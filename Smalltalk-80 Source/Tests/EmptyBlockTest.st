"EmptyBlockTest - Test empty block execution"

Object subclass: #EmptyBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!EmptyBlockTest methodsFor: 'testing'!

test "Should return 42"
	^[] value isNil ifTrue: [42] ifFalse: [999]! !

!EmptyBlockTest class methodsFor: 'running'!

run
	^self new test! !!

EmptyBlockTest run!
