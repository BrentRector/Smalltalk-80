Object subclass: #T1000Test
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!T1000Test methodsFor: 'testing'!

t1000 "O[1,0] I[0,0]: outer arg, inner accesses it"
	^[:oa | [oa] value] value: 10! !

"Run the test"
T1000Test new t1000!
