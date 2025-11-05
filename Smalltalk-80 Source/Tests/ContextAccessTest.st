"ContextAccessTest - Minimal validation of variable access across nested contexts"

Object subclass: #ContextAccessTest
	instanceVariableNames: 'ivar1 ivar2'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-Context'!

!ContextAccessTest methodsFor: 'Level 1 - Method scope'!

testL1_MethodArg: a
	"Level 1: Method accesses own argument"
	^a = 10!

testL1_MethodTemp
	"Level 1: Method accesses own temp"
	| t |
	t := 20.
	^t = 20!

testL1_InstanceVar
	"Level 1: Method accesses instance variable"
	ivar1 := 30.
	^ivar1 = 30! !

!ContextAccessTest methodsFor: 'Level 2 - Block scope'!

testL2_BlockAccessesMethodTemp
	"Level 2: Block accesses parent method temp"
	| t |
	t := 40.
	^[t = 40] value!

testL2_BlockAccessesMethodArg: a
	"Level 2: Block accesses parent method argument"
	^[a = 50] value!

testL2_BlockModifiesMethodTemp
	"Level 2: Block modifies parent method temp"
	| t |
	t := 60.
	[t := 70] value.
	^t = 70!

testL2_BlockWithArgAccessesMethodTemp
	"Level 2: Block with arg accesses parent method temp"
	| t |
	t := 80.
	^[:x | t + x] value: 10!

testL2_BlockAccessesInstanceVar
	"Level 2: Block accesses instance variable"
	ivar1 := 90.
	^[ivar1 = 90] value! !

!ContextAccessTest methodsFor: 'Level 3 - Nested blocks'!

testL3_NestedBlockAccessesGrandparentTemp
	"Level 3: Nested block accesses grandparent temp"
	| t |
	t := 100.
	^[[t = 100] value] value!

testL3_NestedBlockModifiesGrandparentTemp
	"Level 3: Nested block modifies grandparent temp"
	| t |
	t := 110.
	[[t := 120] value] value.
	^t = 120!

testL3_NestedBlockWithArgsAccessesGrandparentTemp
	"Level 3: Nested blocks with args access grandparent temp"
	| t |
	t := 130.
	^[:x | [:y | t + x + y] value: 20] value: 10! !

!ContextAccessTest methodsFor: 'Running'!

runAll
	"Run all tests and return count of passed tests"
	| passed |
	passed := 0.
	(self testL1_MethodArg: 10) ifTrue: [passed := passed + 1].
	self testL1_MethodTemp ifTrue: [passed := passed + 1].
	self testL1_InstanceVar ifTrue: [passed := passed + 1].
	self testL2_BlockAccessesMethodTemp ifTrue: [passed := passed + 1].
	(self testL2_BlockAccessesMethodArg: 50) ifTrue: [passed := passed + 1].
	self testL2_BlockModifiesMethodTemp ifTrue: [passed := passed + 1].
	(self testL2_BlockWithArgAccessesMethodTemp = 90) ifTrue: [passed := passed + 1].
	self testL2_BlockAccessesInstanceVar ifTrue: [passed := passed + 1].
	self testL3_NestedBlockAccessesGrandparentTemp ifTrue: [passed := passed + 1].
	self testL3_NestedBlockModifiesGrandparentTemp ifTrue: [passed := passed + 1].
	(self testL3_NestedBlockWithArgsAccessesGrandparentTemp = 160) ifTrue: [passed := passed + 1].
	^passed! !

!ContextAccessTest class methodsFor: 'Initialization'!

initialize
	"Run all context access tests and report results"
	| test result |
	test := self new.
	result := test runAll.
	^result! !

ContextAccessTest initialize!
