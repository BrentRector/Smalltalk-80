Object>>runSimpleTests
	"Run basic tests directly in Smalltalk environment"
	| testResults success failure |
	testResults := Array new: 0.
	success := 0.
	failure := 0.
	
	"Test 1: Basic arithmetic"
	[ (2 + 3) = 5 ] ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 2: Boolean logic" 
	[ true not = false ] ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 3: Collection operations"
	[ (Array new: 3) size = 3 ] ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 4: String operations"
	[ 'hello' size = 5 ] ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Test 5: Character operations"
	[ $A asciiValue = 65 ] ifTrue: [ success := success + 1 ] ifFalse: [ failure := failure + 1 ].
	
	"Print results"
	'Test Results:' printString.
	'Success: ' printString. success printString.
	'Failure: ' printString. failure printString.
	^self

Object>>testArithmetic
	"Test basic arithmetic operations"
	| result |
	result := 2 + 3.
	result = 5 ifFalse: [ self error: 'Addition failed' ].
	
	result := 10 - 4.
	result = 6 ifFalse: [ self error: 'Subtraction failed' ].
	
	result := 3 * 4.
	result = 12 ifFalse: [ self error: 'Multiplication failed' ].
	
	^'Arithmetic tests passed'

Object>>testBooleans
	"Test boolean operations"
	true ifFalse: [ self error: 'true should be true' ].
	false ifTrue: [ self error: 'false should be false' ].
	
	(true and: [ true ]) ifFalse: [ self error: 'true and true should be true' ].
	(false or: [ true ]) ifFalse: [ self error: 'false or true should be true' ].
	
	^'Boolean tests passed'

Object>>testCollections
	"Test collection operations"
	| array |
	array := Array new: 3.
	array size = 3 ifFalse: [ self error: 'Array size incorrect' ].
	
	array at: 1 put: 'first'.
	(array at: 1) = 'first' ifFalse: [ self error: 'Array at:put: failed' ].
	
	^'Collection tests passed'

Object>>testStrings
	"Test string operations"
	| string |
	string := 'hello'.
	string size = 5 ifFalse: [ self error: 'String size incorrect' ].
	
	(string at: 1) = $h ifFalse: [ self error: 'String at: failed' ].
	
	^'String tests passed'

Object>>runAllTests
	"Run comprehensive test suite"
	| results |
	results := Array new: 0.
	
	"Execute each test and collect results"
	[ results := results copyWith: (self testArithmetic) ] 
		ifError: [ results := results copyWith: 'Arithmetic tests FAILED' ].
	
	[ results := results copyWith: (self testBooleans) ] 
		ifError: [ results := results copyWith: 'Boolean tests FAILED' ].
	
	[ results := results copyWith: (self testCollections) ] 
		ifError: [ results := results copyWith: 'Collection tests FAILED' ].
	
	[ results := results copyWith: (self testStrings) ] 
		ifError: [ results := results copyWith: 'String tests FAILED' ].
	
	"Print results"
	results do: [ :result | result printString ].
	
	^results
