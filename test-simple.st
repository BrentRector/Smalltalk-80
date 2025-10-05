Object subclass: #TestClass
    instanceVariableNames: 'x y'
    classVariableNames: 'SharedVar'
    poolDictionaries: ''
    category: 'Test-Classes'!

!TestClass methodsFor: 'accessing'!

x
    ^x!

x: aValue
    x := aValue!

y
    ^y!

y: aValue
    y := aValue!
!

!TestClass methodsFor: 'initialization'!

initialize
    super initialize.
    x := 0.
    y := 0!
!

!TestClass class methodsFor: 'instance creation'!

new
    ^super new initialize!
!

TestClass comment: 'This is a test class for the compiler.'!

"Initialize the test class"
TestClass initialize!