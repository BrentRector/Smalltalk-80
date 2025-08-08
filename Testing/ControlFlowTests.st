TestCase subclass: #ControlFlowTests
    instanceVariableNames: 'counter result'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing-ControlFlow'!

!ControlFlowTests methodsFor: 'initialization'!
setUp
    "Initialize test variables"
    counter := 0.
    result := nil! !

!ControlFlowTests methodsFor: 'tests'!
testIfTrue
    "Test ifTrue: control structure"
    result := false.
    true ifTrue: [result := true].
    self assertTrue: result.
    
    result := false.
    false ifTrue: [result := true].
    self assertFalse: result! !

!ControlFlowTests methodsFor: 'tests'!
testIfFalse
    "Test ifFalse: control structure"
    result := false.
    false ifFalse: [result := true].
    self assertTrue: result.
    
    result := false.
    true ifFalse: [result := true].
    self assertFalse: result! !

!ControlFlowTests methodsFor: 'tests'!
testIfTrueIfFalse
    "Test ifTrue:ifFalse: control structure"
    result := true ifTrue: ['yes'] ifFalse: ['no'].
    self assertEqual: result to: 'yes'.
    
    result := false ifTrue: ['yes'] ifFalse: ['no'].
    self assertEqual: result to: 'no'! !

!ControlFlowTests methodsFor: 'tests'!
testWhileTrue
    "Test whileTrue: loop"
    counter := 0.
    [counter < 5] whileTrue: [counter := counter + 1].
    self assertEqual: counter to: 5! !

!ControlFlowTests methodsFor: 'tests'!
testWhileFalse
    "Test whileFalse: loop"
    counter := 0.
    [counter >= 5] whileFalse: [counter := counter + 1].
    self assertEqual: counter to: 5! !

!ControlFlowTests methodsFor: 'tests'!
testBlockValue
    "Test block value method"
    result := [42] value.
    self assertEqual: result to: 42.
    
    result := [3 + 4] value.
    self assertEqual: result to: 7! !

!ControlFlowTests methodsFor: 'tests'!
testNestedControlFlow
    "Test nested control structures"
    counter := 0.
    result := 0.
    
    [counter < 3] whileTrue: [
        counter := counter + 1.
        counter = 2 ifTrue: [
            result := result + 10
        ] ifFalse: [
            result := result + 1
        ]
    ].
    
    self assertEqual: counter to: 3.
    self assertEqual: result to: 12 "1 + 10 + 1"! !

!ControlFlowTests methodsFor: 'tests'!
testMethodLookup
    "Test that method lookup works"
    | method |
    method := 3 class lookupSelector: #+.
    self assertNotNil: method.
    
    method := Object lookupSelector: #class.
    self assertNotNil: method! !

!ControlFlowTests methodsFor: 'tests'!
testPerformPrimitive
    "Test perform: primitive"
    result := 3 perform: #+  with: 4.
    self assertEqual: result to: 7.
    
    result := 'hello' perform: #size.
    self assertEqual: result to: 5! !