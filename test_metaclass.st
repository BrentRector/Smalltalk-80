!Object methodsFor: 'testing'!
testMetaclass
    "Test that metaclass system is working"
    | result |
    result := OrderedCollection new.
    
    "Test 1: Object is root"
    Object superclass isNil ifTrue: [result add: 'Object is root'].
    
    "Test 2: Object has a metaclass"
    Object class notNil ifTrue: [result add: 'Object has metaclass'].
    
    "Test 3: SmallInteger class hierarchy"
    SmallInteger superclass == Integer ifTrue: [result add: 'SmallInteger -> Integer'].
    Integer superclass == Number ifTrue: [result add: 'Integer -> Number'].
    
    "Test 4: Metaclass parallel hierarchy"
    SmallInteger class superclass == Integer class ifTrue: [result add: 'SmallInteger class -> Integer class'].
    
    ^result! !