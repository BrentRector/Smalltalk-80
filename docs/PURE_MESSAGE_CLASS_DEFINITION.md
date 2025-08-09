# Pure Message-Based Class Definition

## Goal
Define classes using only message sends, no special VM primitives or direct memory manipulation beyond what's exposed through the message protocol.

## Example: Creating Point Class

### Prerequisites
These classes and methods must exist in Phase 2:
- Metaclass with `basicNew`, `instanceClass:`, `superclass:`, `methodDictionary:`, `format:`
- Symbol with `intern:` or String with `asSymbol`
- Array with `new:`, `at:`, `at:put:`
- ByteArray with `new:`, `at:`, `at:put:`
- CompiledMethod with `newMethod:header:` or `new:numLiterals:`, plus `byteAt:put:`, `literalAt:put:`
- MethodDictionary with `new`, `at:put:`
- SystemDictionary (Smalltalk) with `at:put:`
- ClassOrganization with `new`, `classify:under:`

### Step-by-Step Message Sequence

```smalltalk
"1. Prepare names and symbols"
nameString := 'Point'.
nameSymbol := nameString asSymbol.
superclass := Object.
instVarStrings := #('x' 'y').
instVarSymbols := instVarStrings collect: [:s | s asSymbol].
categoryString := 'Geometry'.

"2. Create method dictionaries"
instanceMethodDict := MethodDictionary new.
classMethodDict := MethodDictionary new.

"3. Create organization objects"
instanceOrg := ClassOrganization new.
classOrg := ClassOrganization new.

"4. Create the metaclass"
metaclass := Metaclass basicNew.
metaclass superclass: superclass class.
metaclass methodDictionary: classMethodDict.
metaclass format: (Behavior formatForFixed: 5 variable: false words: false pointers: true).
metaclass instVarNames: #().
metaclass organization: classOrg.

"5. Create the class (as instance of metaclass)"
class := metaclass basicNew.
class superclass: superclass.
class methodDictionary: instanceMethodDict.
class format: (Behavior formatForFixed: 2 variable: false words: false pointers: true).
class name: nameSymbol.
class instVarNames: instVarSymbols.
class classPool: Dictionary new.
class sharedPools: Array new.
class organization: instanceOrg.

"6. Link metaclass and class"
metaclass instanceClass: class.

"7. Register in global namespace"
Smalltalk at: nameSymbol put: class.

"8. Add instance method 'x' (accessor)"
"First create the compiled method"
xSelector := 'x' asSymbol.
xMethod := CompiledMethod newMethod: 2 header: 0.
xMethod byteAt: 1 put: 0.  "push instance variable 0"
xMethod byteAt: 2 put: 124. "return top"

"Install in class"
class addSelector: xSelector withMethod: xMethod.
instanceOrg classify: xSelector under: 'accessing'.

"9. Add instance method 'x:' (setter)"
xSetSelector := 'x:' asSymbol.
xSetMethod := CompiledMethod newMethod: 3 header: 16r10001. "1 arg"
xSetMethod byteAt: 1 put: 16. "push first argument"
xSetMethod byteAt: 2 put: 96. "store in instance variable 0"
xSetMethod byteAt: 3 put: 124. "return top"

class addSelector: xSetSelector withMethod: xSetMethod.
instanceOrg classify: xSetSelector under: 'accessing'.

"10. Add class method 'x:y:' (constructor)"
xySelector := 'x:y:' asSymbol.
xyLiterals := Array with: class with: xSetSelector with: 'y:' asSymbol.
xyMethod := CompiledMethod new: 10 numLiterals: 3.
xyMethod literalAt: 1 put: (xyLiterals at: 1).
xyMethod literalAt: 2 put: (xyLiterals at: 2).
xyMethod literalAt: 3 put: (xyLiterals at: 3).

"Bytecode for: ^self new x: x; y: y"
xyMethod byteAt: 1 put: 112.  "push self (the class)"
xyMethod byteAt: 2 put: 208.  "send new"
xyMethod byteAt: 3 put: 16.   "push first arg"
xyMethod byteAt: 4 put: 225.  "send literal 2 (x:)"
xyMethod byteAt: 5 put: 135.  "pop"
xyMethod byteAt: 6 put: 17.   "push second arg"
xyMethod byteAt: 7 put: 226.  "send literal 3 (y:)"
xyMethod byteAt: 8 put: 124.  "return top"

metaclass addSelector: xySelector withMethod: xyMethod.
classOrg classify: xySelector under: 'instance creation'.
```

## Validation Sequence

After creating the class, validate it works:

```smalltalk
"Test class exists"
(Smalltalk at: #Point) notNil.  "→ true"

"Test class structure"
Point superclass = Object.  "→ true"
Point instVarNames = #(x y).  "→ true"
Point methodDictionary keys includes: #x.  "→ true"

"Test instantiation (once 'new' works)"
pt := Point new.
pt x: 10.
pt x.  "→ 10"

"Test class method"
pt2 := Point x: 5 y: 7.
pt2 x.  "→ 5"
pt2 y.  "→ 7"
```

## Minimum Required Bytecodes

For basic class and method definition:
- **0-15**: Push receiver variable (instance vars)
- **16-31**: Push temporary/argument
- **32-63**: Push literal constant
- **64-95**: Push literal variable
- **96-103**: Store and pop receiver variable
- **104-111**: Store and pop temporary
- **112-119**: Push receiver, true, false, nil, -1, 0, 1, 2
- **120-123**: Return receiver, true, false, nil
- **124-125**: Return top from method/block
- **128-175**: Send literal selector
- **176-191**: Send special selector
- **192-255**: Extended/special operations

## Format Integer Encoding (Blue Book)

```
Bits 0-5:   Number of named instance variables (0-63)
Bits 6-8:   Instance specification:
            0 = pointers, non-indexable
            1 = pointers, indexable
            2 = words, indexable
            3 = bytes, indexable
Bits 9-15:  Class ID/other flags
```

Helper method needed:
```smalltalk
Behavior class>>formatForFixed: instSize variable: isVar words: isWords pointers: isPointers
    | spec |
    spec := 0.
    isPointers ifTrue: [
        isVar ifTrue: [spec := 1] ifFalse: [spec := 0]
    ] ifFalse: [
        isWords ifTrue: [spec := 2] ifFalse: [spec := 3]
    ].
    ^(spec bitShift: 6) bitOr: instSize
```

## Summary of Missing Pieces

### Critical (Blockers)
1. **Metaclass** class with proper initialization protocol
2. **Symbol>>intern:** or **String>>asSymbol** for selector/name creation
3. **CompiledMethod>>byteAt:put:** for bytecode installation
4. **CompiledMethod class>>newMethod:header:** for proper method allocation
5. **Array/ByteArray** allocation and access

### Important (Quality of Life)
1. **ClassOrganization** for method categorization
2. Format computation helpers
3. **String concatenation** for metaclass naming
4. **Collection>>collect:** for bulk symbol conversion

### Nice to Have
1. Source code management
2. Method recompilation
3. Class validation utilities
4. Dependency tracking

## Next Steps

1. Add minimal missing classes to Phase 2 (Object, Metaclass, Array, String, Symbol)
2. Add missing methods (byteAt:put:, newMethod:header:, asSymbol, etc.)
3. Test pure message-based class creation
4. Move to Phase 3 with full class creation via messages only