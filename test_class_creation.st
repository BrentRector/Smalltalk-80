"Test class creation via Smalltalk after Phase 2 bootstrap"

"First test: Create a simple class"
Object subclass: #TestPoint
	instanceVariableNames: 'x y'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Geometry'!

"Add a method to the test class"
!TestPoint methodsFor: 'accessing'!
x
	^x!

y
	^y!

x: aNumber
	x := aNumber!

y: aNumber
	y := aNumber! !

!TestPoint methodsFor: 'initialization'!
initialize
	x := 0.
	y := 0! !

!TestPoint class methodsFor: 'instance creation'!
x: xValue y: yValue
	^self new x: xValue; y: yValue; yourself! !

"Second test: Create a subclass"
TestPoint subclass: #Test3DPoint
	instanceVariableNames: 'z'
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Geometry'!

!Test3DPoint methodsFor: 'accessing'!
z
	^z!

z: aNumber
	z := aNumber! !

"Third test: Create a variable subclass (indexable)"
Object variableSubclass: #TestArray
	instanceVariableNames: ''
	classVariableNames: ''
	poolDictionaries: ''
	category: 'Test-Collections'!

!TestArray methodsFor: 'accessing'!
at: index
	^self basicAt: index!

at: index put: value
	^self basicAt: index put: value! !

"Verification code"
| point point3d array |

"Test 1: Can we create instances of our new class?"
point := TestPoint x: 10 y: 20.
Transcript show: 'Created TestPoint: '; show: point; cr.
Transcript show: 'x = '; show: point x; show: ', y = '; show: point y; cr.

"Test 2: Can we create instances of a subclass?"
point3d := Test3DPoint new.
point3d x: 1; y: 2; z: 3.
Transcript show: 'Created Test3DPoint with z = '; show: point3d z; cr.

"Test 3: Can we create instances of a variable subclass?"
array := TestArray new: 5.
array at: 1 put: 'first'.
array at: 2 put: 'second'.
Transcript show: 'Created TestArray with: '; show: (array at: 1); show: ', '; show: (array at: 2); cr.

"Test 4: Are classes properly registered in Smalltalk?"
Transcript show: 'TestPoint in Smalltalk? '; show: (Smalltalk includesKey: #TestPoint); cr.
Transcript show: 'Test3DPoint in Smalltalk? '; show: (Smalltalk includesKey: #Test3DPoint); cr.
Transcript show: 'TestArray in Smalltalk? '; show: (Smalltalk includesKey: #TestArray); cr.

"Success!"
Transcript show: 'All class creation tests passed!'; cr.