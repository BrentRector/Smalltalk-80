"Minimal wrapper to run BlockContextTest without other init expressions"

Object subclass: #TestRunner
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TestRunner class methodsFor: 'running'!

run
	"Load and execute BlockContextTest"
	^Smalltalk
		at: #BlockContextTest
		ifPresent: [:testClass | testClass new testAllCombinations]
		ifAbsent: [Error signal: 'BlockContextTest not found']! !!

TestRunner run!
