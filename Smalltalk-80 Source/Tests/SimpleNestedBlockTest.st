"SimpleNestedBlockTest - Minimal test to debug nested block variable access"

Object subclass: #SimpleNestedBlockTest
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests-BlockContext'!

!SimpleNestedBlockTest methodsFor: 'testing'!

test1_OuterTempOnly "Outer block has temp, inner accesses it - should return 10"
	^[| ot | ot _ 10. [ot] value] value!

test2_BothHaveTemps "Both have temps, inner accesses outer - should return 10"
	^[| ot | ot _ 5. [| it | it _ 5. ot + it] value] value!

test3_WithArgument "Inner has arg but accesses outer temp - should return 7"
	^[| ot | ot _ 4. [:ia | | it | it _ 3. ot + it] value: 999] value!

runAll
	"Run all tests and return array of results"
	| results |
	results _ Array new: 3.
	results at: 1 put: self test1_OuterTempOnly.
	results at: 2 put: self test2_BothHaveTemps.
	results at: 3 put: self test3_WithArgument.
	^results! !

!SimpleNestedBlockTest class methodsFor: 'running'!

run
	"Run all tests and return results array"
	^self new runAll! !!

"Run simple nested block tests"
SimpleNestedBlockTest run!
