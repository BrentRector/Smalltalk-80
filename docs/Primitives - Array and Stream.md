# Indexable Fields and Stream Primitives

The second set of primitive routines are for manipulating the indexable fields of objects both directly (by subscripting) and indirectly (by streaming). These routines make use of the 16-bit positive integer primitives, since the maximum number of indexable fields is 65 534.

---

## 1. Dispatch Subscript and Stream Primitives

```smalltalk
dispatchSubscriptAndStreamPrimitives
    primitiveIndex = 60 ifTrue: [↑self primitiveAt].
    primitiveIndex = 61 ifTrue: [↑self primitiveAtPut].
    primitiveIndex = 62 ifTrue: [↑self primitiveSize].
    primitiveIndex = 63 ifTrue: [↑self primitiveStringAt].
    primitiveIndex = 64 ifTrue: [↑self primitiveStringAtPut].
    primitiveIndex = 65 ifTrue: [↑self primitiveNext].
    primitiveIndex = 66 ifTrue: [↑self primitiveNextPut].
    primitiveIndex = 67 ifTrue: [↑self primitiveAtEnd].
```

---

## 2. Bounds Checking and Length

Before any subscripting, we must check that the index is within the legal range of an object’s indexable fields. We also need to distinguish between word-indexed and byte-indexed objects.

```smalltalk
checkIndexableBoundsOf: index in: array
    | class |
    class ← memory fetchClassOf: array.
    self success: index >= 1.
    self success: (index + (self fixedFieldsOf: class)) <= (self lengthOf: array)
```

```smalltalk
lengthOf: array
    (self isWords: (memory fetchClassOf: array))
        ifTrue:  [↑memory fetchWordLengthOf: array]
        ifFalse: [↑memory fetchByteLengthOf: array]
```

---

## 3. Subscripting Operations

### 3.1 Reading a Field

The `subscript:with:` routine assumes the index already accounts for fixed fields; it uses a **one-relative** index into the object as a whole.

```smalltalk
subscript: array with: index
    | class value |
    class ← memory fetchClassOf: array.
    (self isWords: class)
        ifTrue:  [
            (self isPointers: class)
                ifTrue:  [↑memory fetchPointer: index - 1 ofObject: array]
                ifFalse: [
                    value ← memory fetchWord: index - 1 ofObject: array.
                    ↑self positive16BitIntegerFor: value
                ]
        ]
        ifFalse: [
            value ← memory fetchByte: index - 1 ofObject: array.
            ↑memory integerObjectOf: value
        ]
```

### 3.2 Writing a Field

The `subscript:with:storing:` routine likewise writes into one-relative index positions.

```smalltalk
subscript: array with: index storing: value
    | class |
    class ← memory fetchClassOf: array.
    (self isWords: class)
        ifTrue:  [
            (self isPointers: class)
                ifTrue:  [↑memory storePointer: index - 1 ofObject: array withValue: value]
                ifFalse: [
                    self success: (memory isIntegerObject: value).
                    self success ifTrue: [
                        ↑memory
                            storeWord: index - 1 ofObject: array
                            withValue: (self positive16BitValueOf: value)
                    ]
                ]
        ]
        ifFalse: [
            self success: (memory isIntegerObject: value).
            self success ifTrue: [
                ↑memory storeByte: index - 1 ofObject: array
                    withValue: (self lowByteOf: (memory integerValueOf: value))
            ]
        ]
```

---

## 4. `primitiveAt` and `primitiveAtPut`

These primitives pop their arguments off the stack, check bounds, perform the access, and push (or fail) accordingly.

```smalltalk
primitiveAt
    | index array arrayClass result |
    index      ← self positive16BitValueOf: self popStack.
    array      ← self popStack.
    arrayClass ← memory fetchClassOf: array.
    self checkIndexableBoundsOf: index in: array.
    self success
        ifTrue: [
            index ← index + (self fixedFieldsOf: arrayClass).
            result ← self subscript: array with: index
        ].
    self success
        ifTrue: [self push: result]
        ifFalse: [self unPop: 2]
```

```smalltalk
primitiveAtPut
    | array index arrayClass value result |
    value      ← self popStack.
    index      ← self positive16BitValueOf: self popStack.
    array      ← self popStack.
    arrayClass ← memory fetchClassOf: array.
    self checkIndexableBoundsOf: index in: array.
    self success
        ifTrue: [
            index ~ index + (self fixedFieldsOf: arrayClass).
            self subscript: array
                with: index
                storing: value
        ].
    self success
        ifTrue: [self push: value]
        ifFalse: [self unPop: 3]
```

---

## 5. `primitiveSize`

Returns the number of indexable fields (the maximum legal subscript).

```smalltalk
primitiveSize
    | array class length |
    array  ← self popStack.
    class  ← memory fetchClassOf: array.
    length ← self positive16BitIntegerFor:
                 (self lengthOf: array) ←
                 (self fixedFieldsOf: class).
    self success
        ifTrue:  [self push: length]
        ifFalse: [self unPop: 1]
```

---

## 6. String‑Specific `at:` and `at:put:`

Strings store 8-bit codes but present them as `Character` objects.

```smalltalk
primitiveStringAt
    | index array ascii character |
    index     ← self positive16BitValueOf: self popStack.
    array     ← self popStack.
    self checkIndexableBoundsOf: index in: array.
    self success
        ifTrue: [
            ascii     ← memory integerValueOf:
                            (self subscript: array with: index).
            character ← memory fetchPointer: ascii ofObject: CharacterTablePointer
        ].
    self success
        ifTrue: [self push: character]
        ifFalse: [self unPop: 2]
```

```smalltalk
initializeCharacterIndex
    CharacterValueIndex ← 0
```

```smalltalk
primitiveStringAtPut
    | index array ascii character |
    character ← self popStack.
    index     ← self positive16BitValueOf: self popStack.
    array     ← self popStack.
    self checkIndexableBoundsOf: index in: array.
    self success: (memory fetchClassOf: character) = ClassCharacterPointer.
    self success
        ifTrue: [
            ascii ← memory fetchPointer: CharacterValueIndex ofObject: character.
            self subscript: array
                with: index
                storing: ascii
        ].
    self success
        ifTrue: [self push: character]
        ifFalse: [self unPop: 2]
```

---

## 7. Stream Primitives (`next`, `nextPut:`, `atEnd`)

Optional primitives for fast stream access on `Array` or `String`.

```smalltalk
initializeStreamIndices
    StreamArrayIndex      ← 0.
    StreamIndexIndex      ← 1.
    StreamReadLimitIndex  ← 2.
    StreamWriteLimitIndex ← 3.
```

```smalltalk
primitiveNext
    | stream index limit array arrayClass result ascii |
    stream     ← self popStack.
    array      ← memory fetchPointer: StreamArrayIndex ofObject: stream.
    arrayClass ← memory fetchClassOf: array.
    index      ← self fetchInteger: StreamIndexIndex    ofObject: stream.
    limit      ← self fetchInteger: StreamReadLimitIndex ofObject: stream.
    self success: index < limit.
    self success: (arrayClass = ClassArrayPointer) | (arrayClass = ClassStringPointer).
    self checkIndexableBoundsOf: index + 1 in: array.
    self success
        ifTrue: [
            index  ← index + 1.
            result ← self subscript: array with: index
        ].
    self success
        ifTrue: [self storeInteger: StreamIndexIndex ofObject: stream withValue: index].
    self success
        ifTrue: [
            arrayClass = ClassArrayPointer
                ifTrue:  [self push: result]
                ifFalse: [
                    ascii ← memory integerValueOf: result.
                    self push: (memory fetchPointer: ascii ofObject: CharacterTablePointer)
                ]
        ]
        ifFalse: [self unPop: 1]
```

```smalltalk
primitiveNextPut
    | value stream index limit array arrayClass result ascii |
    value      ← self popStack.
    stream     ← self popStack.
    array      ← memory fetchPointer: StreamArrayIndex ofObject: stream.
    arrayClass ← memory fetchClassOf: array.
    index      ← self fetchInteger: StreamIndexIndex ofObject: stream.
    limit      ← self fetchInteger: StreamWriteLimitIndex ofObject: stream.
    self success: index < limit.
    self success: (arrayClass = ClassArrayPointer) | (arrayClass = ClassStringPointer).
    self checkIndexableBoundsOf: index + 1 in: array.
    self success
        ifTrue: [
            index ← index + 1.
            (arrayClass = ClassArrayPointer)
                ifTrue:  [self subscript: array with: index storing: value]
                ifFalse: [
                    ascii ← memory fetchPointer: CharacterValueIndex ofObject: value.
                    self subscript: array with: index storing: ascii
                ]
        ].
    self success
        ifTrue: [self storeInteger: StreamIndexIndex ofObject: stream withValue: index].
    self success
        ifTrue: [self push: value]
        ifFalse: [self unPop: 2]
```

```smalltalk
primitiveAtEnd
    | stream array arrayClass length index limit |
    stream     ← self popStack.
    array      ← memory fetchPointer: StreamArrayIndex ofObject: stream.
    arrayClass ← memory fetchClassOf: array.
    length     ← self lengthOf: array.
    index      ← self fetchInteger: StreamIndexIndex ofObject: stream.
    limit      ← self fetchInteger: StreamReadLimitIndex ofObject: stream.
    self success: (arrayClass = ClassArrayPointer) | (arrayClass = ClassStringPointer).
    self success
        ifTrue: [
            ((index >= limit) | (index >= length))
                ifTrue:  [self push: TruePointer]
                ifFalse: [self push: FalsePointer]
        ]
        ifFalse: [self unPop: 1]
```
