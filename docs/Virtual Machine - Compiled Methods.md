The bytecodes executed by the interpreter are found in instances of `CompiledMethod`. The bytecodes are stored as 8‑bit values, two to a word. In addition to the bytecodes, a `CompiledMethod` contains some object pointers. The first of these object pointers is called the *method header* and the rest of the object pointers make up the method’s *literal frame*. Figure 27.1 shows the structure of a `CompiledMethod` and the following routine initializes the indices used to access fields of `CompiledMethods`.

```smalltalk
initializeMethodIndices
    "Class CompiledMethod"
    HeaderIndex ← 0.
    LiteralStart ← 1.
```

The header is a `SmallInteger` that encodes certain information about the `CompiledMethod`.

```smalltalk
headerOf: methodPointer
    ↑memory fetchPointer: HeaderIndex
        ofObject: methodPointer
```

The literal frame contains pointers to objects referred to by the bytecodes. These include the selectors of messages that the method sends, and shared variables and constants to which the method refers.

```smalltalk
literal: offset ofMethod: methodPointer
    ↑memory fetchPointer: offset + LiteralStart
        ofObject: methodPointer
```

Following the header and literals of a method are the bytecodes. Methods are the only objects in the Smalltalk system that store both object pointers (in the header and literal frame) and numerical values (in the bytecodes). The form of the bytecodes will be discussed in the next chapter.

**Figure 27.1 – Structure of a CompiledMethod**

```
+----------------+
| header         |
+----------------+
| literal frame  |
|                |
+----------------+
| bytecodes      |
|                |
+----------------+
```

---

## Method Headers

Since the method header is a `SmallInteger`, its value will be encoded in its pointer. The high‑order 15 bits of the pointer are available to encode information; the low‑order bit must be a one to indicate that the pointer is for a `SmallInteger`. The header includes four bit fields that encode information about the `CompiledMethod`. Figure 27.2 shows the bit fields of a header:

```
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
|  |  |  |  |  |  |  |  |  |  |  |  |  |  |  |1 |
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
 \______/ \____________/\__/\_______________/\_/
   flag      temporary  large  literal  low-order
   value      count    context   count     bit (1)
 (3 bits)    (5 bits)    flag   (6 bits)
                       (1 bit)
```

```smalltalk
temporaryCountOf: methodPointer
    ↑self extractBits: 3 to: 7
        of: (self headerOf: methodPointer)
```

The temporary count indicates the number of temporary variables used by the `CompiledMethod`. This includes the number of arguments.

```smalltalk
largeContextFlagOf: methodPointer
    ↑self extractBits: 8 to: 8
        of: (self headerOf: methodPointer)
```

The large context flag indicates which of two sizes of `MethodContext` are needed. The flag indicates whether the sum of the maximum stack depth and the number of temporary variables needed is greater than twelve. The smaller `MethodContext`s have room for 12 and the larger have room for 32.

```smalltalk
literalCountOf: methodPointer
    ↑self literalCountOfHeader: (self headerOf: methodPointer)

literalCountOfHeader: headerPointer
    ↑self extractBits: 9 to: 14
        of: headerPointer
```

The literal count indicates the size of the `MethodContext`’s literal frame. This, in turn, indicates where the `MethodContext`’s bytecodes start.

```smalltalk
objectPointerCountOf: methodPointer
    ↑(self literalCountOf: methodPointer) + LiteralStart

initialInstructionPointerOfMethod: methodPointer
    ↑((self literalCountOf: methodPointer) + LiteralStart) * 2 + 1
```

The flag value is used to encode the number of arguments a `CompiledMethod` takes and whether or not it has an associated primitive routine.

```smalltalk
flagValueOf: methodPointer
    ↑self extractBits: 0 to: 2
        of: (self headerOf: methodPointer)
```

The eight possible flag values have the following meanings:

| flag value | meaning                                           |
|------------|---------------------------------------------------|
| 0–4        | no primitive and 0 to 4 arguments                 |
| 5          | primitive return of self (0 arguments)            |
| 6          | primitive return of an instance variable (0 args)|
| 7          | a header extension contains argument count and primitive index |

Since the majority of `CompiledMethods` have four or fewer arguments and do not have an associated primitive routine, the flag value is usually simply the number of arguments.

---

### Special Primitive Methods

Smalltalk methods that *only* return the receiver of the message (`self`) produce `CompiledMethods` that have no literals or bytecodes, only a header with a flag value of 5. In similar fashion, Smalltalk methods that *only* return the value of one of the receiver’s instance variables produce `CompiledMethods` that contain only headers with a flag value of 6. All other methods produce `CompiledMethods` with bytecodes. When the flag value is 6, the index of the instance variable to return is found in the header in the bit field ordinarily used to indicate the number of temporary variables used by the `CompiledMethod`. Figure 27.3 shows a `CompiledMethod` for a Smalltalk method that only returns a receiver instance variable.

**Figure 27.3 – Special Primitive Method Header**

```
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
|1 |1 |0 |  |  |  |  |  |0 |0 |0 |0 |0 |0 |0 |1 |
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  \___/       \______________//
   flag value       field index
   (binary 110)      (5 bits)
```

```smalltalk
fieldIndexOf: methodPointer
    ↑self extractBits: 3 to: 7
        of: (self headerOf: methodPointer)
```

The following routine returns the index of the field representing the instance variable to be returned in the case that the flag value is 6.

---

### Method Header Extensions

If the flag value is 7, the next-to-last literal is a header extension, which is another `SmallInteger`. The header extension includes two bit fields that encode the argument count and primitive index of the `CompiledMethod`. Figure 27.4 shows the bit fields of a header extension.

**Figure 27.4 – Header Extension Bits**

```
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
|0 |0 |  |  |  |  |  |  |  |  |  |  |  |  |  |1|
+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+--+
  \_/     \_________________//  \
   arg       primitive        low-order
   count      index            bit (1)
```

```smalltalk
headerExtensionOf: methodPointer
    | literalCount |
    literalCount ← self literalCountOf: methodPointer.
    ↑self literal: literalCount - 2 ofMethod: methodPointer

argumentCountOf: methodPointer
    | flagValue |
    flagValue ← self flagValueOf: methodPointer.
    flagValue < 5
        ifTrue: [↑flagValue].
    flagValue < 7
        ifTrue: [↑0]
        ifFalse: [↑self extractBits: 2 to: 6
            of: (self headerExtensionOf: methodPointer)]

primitiveIndexOf: methodPointer
    | flagValue |
    flagValue ← self flagValueOf: methodPointer.
    flagValue = 7
        ifTrue: [↑self extractBits: 7 to: 14
            of: (self headerExtensionOf: methodPointer)]
        ifFalse: [↑0]
```

Any `CompiledMethod` that sends a superclass message (i.e., a message to `super`) or contains a header extension, will have as its last literal an `Association` whose value is the class in whose message dictionary the `CompiledMethod` is found. This is called the *method class* and is accessed by the following routine.

```smalltalk
methodClassOf: methodPointer
    | literalCount association |
    literalCount ← self literalCountOf: methodPointer.
    association ← self literal: literalCount - 1
        ofMethod: methodPointer.
    ↑memory fetchPointer: ValueIndex
        ofObject: association
```

An example of a `CompiledMethod` whose literal frame contained a method class was given in the last chapter. The `CompiledMethod` for the `intersect:` message to `ShadedRectangle` was shown in the section of the last chapter called *Messages*.
