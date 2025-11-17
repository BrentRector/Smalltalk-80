"Minimal test for block with both arguments and temporaries"

Object subclass: #MinimalBlockArgsTempsTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'!

!MinimalBlockArgsTempsTest methodsFor: 'testing'!

test1
	"Block with 1 arg and 1 temp"
	^[:ia | | it | it _ 3. ia + it] value: 7!

test2
	"Nested: outer temp, inner has arg and temp"
	^[| ot | ot _ 4. [:ia | | it | it _ 3. ot + it] value: 999] value!

runAll
	| sum |
	sum _ 0.
	sum _ sum + self test1.
	sum _ sum + self test2.
	^sum! !

!MinimalBlockArgsTempsTest class methodsFor: 'running'!

run
	^self new runAll! !!

"Run minimal test"
MinimalBlockArgsTempsTest run!
