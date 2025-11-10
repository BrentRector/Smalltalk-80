"NonLocalReturnTest - Test non-local return from block"

Object subclass: #NonLocalReturnTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!NonLocalReturnTest methodsFor: 'testing'!

test "Should return 42"
	true ifTrue: [^42].
	^999! !

!NonLocalReturnTest class methodsFor: 'running'!

run
	^self new test! !!

NonLocalReturnTest run!
