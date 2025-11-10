"Test block temporary variable access - s01 equivalent"
Object subclass: #TestBlockTemp
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Tests'!

!TestBlockTemp methodsFor: 'testing'!

testS01
	"[0arg,1temp] use own temp - should return 10"
	^[| t1 | t1 _ 10. t1] value!
!
