# Fundamental Object Methods and Bytecodes

## Core Instance Methods to Add

### Accessing
1. **yourself** - `^self`
   - Bytecode: [176] = ReturnReceiver

2. **size** - Returns 0 for non-indexable objects
   - Primitive: 62
   - Fallback bytecode: [32, 180] = PushLiteralConstant0 (0), ReturnTop
   - Note: Literal 0 must be SmallInteger 0

3. **basicSize** - Returns 0 for non-indexable objects  
   - Primitive: 62
   - Fallback bytecode: [32, 180] = PushLiteralConstant0 (0), ReturnTop

### Testing
4. **isNil** - `^false`
   - Bytecode: [178] = ReturnFalse

5. **notNil** - `^true`
   - Bytecode: [177] = ReturnTrue

6. **isInteger** - `^false`
   - Bytecode: [178] = ReturnFalse

### Comparing
7. **==** (identity comparison)
   - Primitive: 110
   - Fallback: self primitiveFailed

8. **~=** (not equal) - `^self = anObject == false`
   - Bytecode needs to:
     1. Push self (receiver)
     2. Push argument 0 (anObject)
     3. Send #= (needs literal)
     4. Push false
     5. Send #==
     6. Return top
   - Complex - needs literals

9. **~~** (not identical) - `^(self == anObject) not`
   - Bytecode needs to:
     1. Push self
     2. Push argument 0
     3. Send #==
     4. Send #not
     5. Return top
   - Complex - needs literals

10. **=** (equality, default to identity) - `^self == anObject`
    - Bytecode: [176, 16, 192, 180]
      - 176 = ReturnReceiver (push self)
      - 16 = PushTemporaryVariable0 (anObject)
      - 192 = SendMessage0 (needs #== in literal 0)
      - 180 = ReturnTop

11. **hash**
    - Primitive: 75
    - Fallback: self primitiveFailed

### Copying
12. **copy** - `^self shallowCopy`
    - Bytecode: [176, 192, 180]
      - 176 = ReturnReceiver (push self) 
      - 192 = SendMessage0 (needs #shallowCopy in literal 0)
      - 180 = ReturnTop

13. **shallowCopy** - Complex implementation, skip for now

### Error Handling (Simplified)
14. **error:** - Simplified version for bootstrap
    - Could just halt or return self for now
    - Bytecode: [176] = ReturnReceiver (simplified)

15. **doesNotUnderstand:** - Simplified version
    - Could just call error: with message selector
    - Complex - needs message handling

16. **primitiveFailed** - `self error: 'a primitive has failed'`
    - Complex - needs to send error:

17. **shouldNotImplement** - `self error: 'This message is not appropriate for this object'`
    - Complex - needs to send error:

18. **subclassResponsibility** - `self error: 'My subclass should have overridden one of my messages.'`
    - Complex - needs to send error:

### System Primitives
19. **instVarAt:** - Access instance variable by index
    - Primitive: 73
    - Fallback: Complex (needs basicAt: calculation)

20. **instVarAt:put:** - Set instance variable by index
    - Primitive: 74
    - Fallback: Complex (needs basicAt:put: calculation)

21. **class** - Return the class of the receiver
    - Primitive: 111
    - No fallback needed (essential primitive)

22. **perform:** - Send a message with selector
    - Primitive: 83
    - Fallback: Complex

23. **perform:with:** - Send a message with selector and one argument
    - Primitive: 84
    - Fallback: Complex

## Core Class Methods to Add

### Instance Creation
1. **new** - Inherited from Behavior, already have it

2. **basicNew** - Inherited from Behavior, already have it

3. **basicNew:** - Inherited from Behavior, need to add

## Methods We'll Add in Phase 2

Based on importance and simplicity of implementation:

### Essential Simple Methods (Add Now)
- yourself [176]
- isNil [178]
- notNil [177]
- isInteger [178]
- class (primitive 111)

### Primitive-Based Methods (Add Now)
- == (primitive 110)
- hash (primitive 75)
- size (primitive 62)
- basicSize (primitive 62)
- instVarAt: (primitive 73)
- instVarAt:put: (primitive 74)

### Methods Requiring Literals (Add with proper literal support)
- = (needs #== literal)
- copy (needs #shallowCopy literal)
- ~= (needs #= and #== literals)
- ~~ (needs #== and #not literals)

### Complex Methods (Skip for now or simplify)
- error: (simplify to just return self)
- doesNotUnderstand: (skip or simplify)
- shallowCopy (complex implementation)
- perform: family (complex)

## Bytecode Examples

### yourself
```
[176]  # ReturnReceiver
```

### isNil  
```
[178]  # ReturnFalse
```

### notNil
```
[177]  # ReturnTrue
```

### == (identity comparison)
```
Header: 110  # Primitive number
[176]        # ReturnReceiver (fallback if primitive fails)
```

### size
```
Header: 62   # Primitive number
[32, 180]    # PushLiteralConstant0 (0), ReturnTop (fallback)
```