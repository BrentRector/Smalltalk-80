Object subclass: #ControlStructures
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test-Control'!

!ControlStructures methodsFor: 'loops'!

whileTrueExample
    "Test whileTrue: loop"
    | count sum |
    count := 1.
    sum := 0.
    [count <= 10] whileTrue: [
        sum := sum + count.
        count := count + 1].
    ^sum!

whileFalseExample
    "Test whileFalse: loop"
    | value |
    value := 100.
    [value <= 0] whileFalse: [
        value := value - 10].
    ^value!

toDoExample
    "Test to:do: loop"
    | sum |
    sum := 0.
    1 to: 100 do: [:i | sum := sum + i].
    ^sum!

toByDoExample
    "Test to:by:do: loop"
    | result |
    result := OrderedCollection new.
    1 to: 10 by: 2 do: [:i | result add: i].
    ^result!

timesRepeatExample
    "Test timesRepeat:"
    | counter |
    counter := 0.
    10 timesRepeat: [counter := counter + 1].
    ^counter! !

!ControlStructures methodsFor: 'boolean'!

andExample: x with: y
    "Test and: short-circuit"
    ^(x > 0) and: [y > 0]!

orExample: x with: y
    "Test or: short-circuit"
    ^(x > 10) or: [y > 10]!

complexBoolean: a with: b and: c
    "Test complex boolean expressions"
    ^((a > 0) and: [b > 0]) or: [(c = 0)]!

notExample: aValue
    "Test not operation"
    ^(aValue > 0) not! !

!ControlStructures methodsFor: 'early-return'!

earlyReturnExample: aNumber
    "Test early returns"
    aNumber < 0 ifTrue: [^'negative'].
    aNumber = 0 ifTrue: [^'zero'].
    aNumber = 1 ifTrue: [^'one'].
    aNumber = 2 ifTrue: [^'two'].
    ^'many'!

blockWithNonLocalReturn
    "Test non-local return from block"
    #(1 2 3 4 5) do: [:each |
        each = 3 ifTrue: [^'found three'].
    ].
    ^'not found'! !