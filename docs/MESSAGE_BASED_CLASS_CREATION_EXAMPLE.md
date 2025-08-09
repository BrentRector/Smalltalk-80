# Pure Message-Based Class Creation Example

## Complete Example: Creating Point Class via Messages Only

This example shows the exact message sequence to create a Point class with instance variables x and y, plus accessor methods, using only message sends - no compiler, no special primitives beyond what's exposed through methods.

### Prerequisites Verification

Phase 2 now contains:
- **Object** (78 instance, 6 class methods) - root of hierarchy
- **Metaclass** (25 instance, 1 class methods) - for class/metaclass duality
- **Symbol** with `intern:` - for selector creation
- **String** with `asSymbol` - for symbol conversion
- **Array** (6 instance methods) - for data structures
- **ByteArray** (12 instance methods) - for bytecode
- **CompiledMethod** with `byteAt:put:` and `newMethod:header:`
- **ClassOrganizer** (25 instance, 3 class methods) - for organization
- **SystemDictionary** with `at:put:` - for global registration

### Step 1: Name and Superclass Resolution

```smalltalk
"Convert strings to symbols"
nameString := 'Point'.
nameSymbol := nameString asSymbol.                    "Symbol intern: 'Point'"
superclass := Smalltalk at: #Object.                  "Lookup Object class"
```

### Step 2: Instance Variables and Organization

```smalltalk
"Create instance variable array"
instVarStrings := #('x' 'y').                        "Array literal"
instVarSymbols := Array new: 2.
instVarSymbols at: 1 put: ('x' asSymbol).
instVarSymbols at: 2 put: ('y' asSymbol).

"Create empty class variables and pools"
classVars := Array new: 0.
pools := Array new: 0.
classPool := Dictionary new.

"Create organization"
instanceOrg := ClassOrganizer new.
classOrg := ClassOrganizer new.
```

### Step 3: Method Dictionaries

```smalltalk
"Create method dictionaries"
instanceMethodDict := MethodDictionary new.
classMethodDict := MethodDictionary new.
```

### Step 4: Create Metaclass

```smalltalk
"Create the metaclass"
metaclass := Metaclass basicNew.
metaclass superclass: (superclass class).             "Object class"
metaclass methodDictionary: classMethodDict.
metaclass instVarNames: (Array new: 0).              "No class inst vars"
metaclass organization: classOrg.

"Compute format for metaclass (fixed pointers, 0 inst vars)"
metaFormat := 0.                                      "Format for fixed pointer class"
metaclass format: metaFormat.

"Name the metaclass"
metaName := nameString , ' class'.                    "String concatenation"
metaNameSymbol := metaName asSymbol.
metaclass name: metaNameSymbol.
```

### Step 5: Create Class

```smalltalk
"Create the class as instance of metaclass"
class := metaclass basicNew.
class superclass: superclass.
class methodDictionary: instanceMethodDict.
class instVarNames: instVarSymbols.
class classPool: classPool.
class sharedPools: pools.
class organization: instanceOrg.
class name: nameSymbol.

"Compute format for class (fixed pointers, 2 inst vars)"
classFormat := 2.                                     "2 instance variables, fixed pointers"
class format: classFormat.

"Complete the metaclass-class link"
metaclass instanceClass: class.
```

### Step 6: Register Globally

```smalltalk
"Register in Smalltalk global dictionary"
Smalltalk at: nameSymbol put: class.
```

### Step 7: Install Instance Method 'x' (Accessor)

```smalltalk
"Create selector"
xSelector := 'x' asSymbol.

"Create bytecode for: ^x"
xBytecode := ByteArray new: 2.
xBytecode at: 1 put: 0.                              "Push receiver variable 0 (x)"
xBytecode at: 2 put: 124.                            "Return top from method"

"No literals needed"
xLiterals := Array new: 0.

"Compute header (0 args, 0 temps, 0 literals, no primitive)"
xHeader := 0.                                         "Simplified header"

"Create compiled method"
xMethod := CompiledMethod newMethod: 2 header: xHeader.
"OR if newMethod:header: not available:"
"xMethod := CompiledMethod new."
"xMethod objectAt: 1 put: xHeader."

"Install bytecode"
xMethod byteAt: 1 put: (xBytecode at: 1).
xMethod byteAt: 2 put: (xBytecode at: 2).

"Install in class"
class addSelector: xSelector withMethod: xMethod.
instanceOrg classify: xSelector under: 'accessing'.
```

### Step 8: Install Instance Method 'x:' (Setter)

```smalltalk
"Create selector"
xSetSelector := 'x:' asSymbol.

"Create bytecode for: x := anObject. ^anObject"
xSetBytecode := ByteArray new: 4.
xSetBytecode at: 1 put: 16.                          "Push first argument"
xSetBytecode at: 2 put: 96.                          "Store in receiver variable 0"
xSetBytecode at: 3 put: 16.                          "Push first argument again"
xSetBytecode at: 4 put: 124.                         "Return top from method"

"No literals"
xSetLiterals := Array new: 0.

"Header (1 arg, 0 temps, 0 literals)"
xSetHeader := 16r10000.                              "1 argument encoded"

"Create method"
xSetMethod := CompiledMethod newMethod: 4 header: xSetHeader.
xSetMethod byteAt: 1 put: (xSetBytecode at: 1).
xSetMethod byteAt: 2 put: (xSetBytecode at: 2).
xSetMethod byteAt: 3 put: (xSetBytecode at: 3).
xSetMethod byteAt: 4 put: (xSetBytecode at: 4).

"Install"
class addSelector: xSetSelector withMethod: xSetMethod.
instanceOrg classify: xSetSelector under: 'accessing'.
```

### Step 9: Install Class Method 'x:y:' (Constructor)

```smalltalk
"Create selector"
xySelector := 'x:y:' asSymbol.

"Create literals array"
xyLiterals := Array new: 4.
xyLiterals at: 1 put: #new.                          "Selector for new"
xyLiterals at: 2 put: xSetSelector.                  "Selector for x:"
xyLiterals at: 3 put: ('y:' asSymbol).              "Selector for y:"
xyLiterals at: 4 put: #yourself.                     "Selector for yourself"

"Create bytecode for: ^self new x: xValue; y: yValue; yourself"
xyBytecode := ByteArray new: 10.
xyBytecode at: 1 put: 112.                           "Push self (the class)"
xyBytecode at: 2 put: 208.                           "Send literal selector 0 (new)"
xyBytecode at: 3 put: 16.                            "Push first argument"
xyBytecode at: 4 put: 209.                           "Send literal selector 1 (x:)"
xyBytecode at: 5 put: 135.                           "Pop"
xyBytecode at: 6 put: 17.                            "Push second argument"
xyBytecode at: 7 put: 210.                           "Send literal selector 2 (y:)"
xyBytecode at: 8 put: 135.                           "Pop"
xyBytecode at: 9 put: 211.                           "Send literal selector 3 (yourself)"
xyBytecode at: 10 put: 124.                          "Return top"

"Header (2 args, 0 temps, 4 literals)"
xyHeader := 16r20004.                                "2 args, 4 literals"

"Create method"
xyMethod := CompiledMethod newMethod: 10 header: xyHeader.

"Install literals"
xyMethod literalAt: 1 put: (xyLiterals at: 1).
xyMethod literalAt: 2 put: (xyLiterals at: 2).
xyMethod literalAt: 3 put: (xyLiterals at: 3).
xyMethod literalAt: 4 put: (xyLiterals at: 4).

"Install bytecode"
1 to: 10 do: [:i | xyMethod byteAt: i put: (xyBytecode at: i)].

"Install in metaclass"
metaclass addSelector: xySelector withMethod: xyMethod.
classOrg classify: xySelector under: 'instance creation'.
```

## Validation

After execution, the system should have:

1. **Class registered**: `(Smalltalk at: #Point) notNil` → true
2. **Superclass correct**: `Point superclass = Object` → true
3. **Instance variables**: `Point instVarNames = #(x y)` → true
4. **Instance methods**: `Point methodDictionary keys includes: #x` → true
5. **Class methods**: `Point class methodDictionary keys includes: #x:y:` → true

## Header and Format Encoding

### Header Word Format (Blue Book)
```
Bits 0-4:   Number of arguments (0-31)
Bits 5-9:   Number of temporaries (0-31)
Bits 10-14: Number of literals (0-31)
Bit 15:     Large context flag
Bits 16-23: Primitive number (0-255)
Bits 24-27: Flags
```

### Class Format Word
```
Bits 0-5:   Number of named instance variables (0-63)
Bits 6-8:   Instance specification:
            0 = pointers, non-indexable (Object, Point)
            1 = pointers, indexable (Array)
            2 = words, indexable (WordArray)
            3 = bytes, indexable (ByteArray, String)
Bits 9-15:  Reserved/flags
```

## Helper Methods Needed

For full automation, these helpers would be useful:

```smalltalk
"Header encoding"
CompiledMethod class>>headerWithArgs: nArgs temps: nTemps literals: nLits primitive: prim
    ^(nArgs) + (nTemps bitShift: 5) + (nLits bitShift: 10) + (prim bitShift: 16)

"Format encoding"
Behavior class>>formatForFixed: nVars
    ^nVars  "bits 0-5 for fixed pointer objects"

Behavior class>>formatForVariable: type instVars: nVars
    | spec |
    type = #pointers ifTrue: [spec := 1].
    type = #words ifTrue: [spec := 2].
    type = #bytes ifTrue: [spec := 3].
    ^nVars + (spec bitShift: 6)
```

## Summary

With Phase 2 complete, we can now:
1. Create classes using only message sends
2. Install methods with hand-crafted bytecode
3. Build the class/metaclass structure properly
4. Register classes globally
5. Create a complete, working class without any compiler

The only "magic" remaining is:
- The bytecode values themselves (but these are documented)
- The header/format encoding (but these follow Blue Book spec)
- The initial Phase 1/2 bootstrap (but after that, pure messages)

This makes the entire class creation process auditable and reproducible.