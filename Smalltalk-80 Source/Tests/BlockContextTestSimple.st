"Standalone BlockContextTest - no dependencies"

Object subclass: #BlockContextTest
	instanceVariableNames: 'testValue classTestValue'
	classVariableNames: 'ClassValue'
	poolDictionaries: ''
	category: 'Tests-BlockContext'! !

!BlockContextTest class methodsFor: 'initialization'!

initialize
	"Initialize class variable for testing"
	ClassValue _ 42! !

BlockContextTest initialize!

!BlockContextTest methodsFor: 'method-arg-tests'!

ma1 "Method 1 arg, 2-level block accesses it"
	^[:arg | [[arg] value] value] value: 42!

ma2 "Method 2 args, 3-level block accesses both"
	^[:arg1 :arg2 | [[[arg1 + arg2] value] value] value] value: 20 value: 22!

t0001 "O[0,0] I[0,1]: inner has temp, accesses method temp"
	| m | m _ 5. ^[[| t | t _ 5. m + t] value] value! !

!BlockContextTest class methodsFor: 'testing'!

runTests
	| sum inst |
	inst _ self new.
	sum _ 0.
	sum _ sum + (inst ma1).
	sum _ sum + (inst ma2).
	sum _ sum + (inst t0001).
	^sum! !

BlockContextTest runTests!
