!MyExampleClass methodsFor: 'examples'!

loopExample
    1 to: 10 do: [:i | Transcript show: i].

!

conditionalExample
    condition ifTrue: [self doSomething] ifFalse: [self doSomethingElse].

!

cascadeExample
    object doThis; doThat: arg; doMore.

! !
