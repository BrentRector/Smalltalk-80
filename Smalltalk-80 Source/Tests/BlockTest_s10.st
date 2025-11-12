"Simple block argument test for debugging"

Object subclass: #BlockTest_s10
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!BlockTest_s10 methodsFor: 'testing'!

s10 "[1arg,0temp] use arg"
	^[:a1 | a1] value: 10! !

!BlockTest_s10 class methodsFor: 'initialization'!

initialize
	"BlockTest_s10 initialize"
	| result |
	result := self new s10.
	^result! !
