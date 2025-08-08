TestCase subclass: #ObjectMethodsTest
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Testing-Core'!

!ObjectMethodsTest methodsFor: 'tests'!

testYourself
    "Test that yourself returns self"
    | obj result |
    obj _ Object new.
    result _ obj yourself.
    self assert: result == obj!

testIsNil
    "Test that isNil returns false for Object"
    | obj |
    obj _ Object new.
    self assertFalse: obj isNil!

testNotNil
    "Test that notNil returns true for Object"
    | obj |
    obj _ Object new.
    self assertTrue: obj notNil!

testIsInteger
    "Test that isInteger returns false for Object"
    | obj |
    obj _ Object new.
    self assertFalse: obj isInteger!

testIdentity
    "Test that == works for identity comparison"
    | obj1 obj2 |
    obj1 _ Object new.
    obj2 _ Object new.
    self assert: obj1 == obj1.
    self assertFalse: obj1 == obj2!

testEquals
    "Test that = defaults to identity comparison"
    | obj1 obj2 |
    obj1 _ Object new.
    obj2 _ Object new.
    self assert: obj1 = obj1.
    self assertFalse: obj1 = obj2!

testClass
    "Test that class returns the object's class"
    | obj |
    obj _ Object new.
    self assert: obj class = Object!

testSize
    "Test that size returns 0 for non-indexable objects"
    | obj |
    obj _ Object new.
    self assertEqual: obj size to: 0!

testBasicSize
    "Test that basicSize returns 0 for non-indexable objects"
    | obj |
    obj _ Object new.
    self assertEqual: obj basicSize to: 0!

testHash
    "Test that hash returns an integer"
    | obj hash1 hash2 |
    obj _ Object new.
    hash1 _ obj hash.
    hash2 _ obj hash.
    "Hash should be consistent"
    self assert: hash1 = hash2!

testError
    "Test that error: returns self (simplified version)"
    | obj result |
    obj _ Object new.
    result _ obj error: 'test error'.
    "In our simplified version, error just returns self"
    self assert: result == obj!

! !