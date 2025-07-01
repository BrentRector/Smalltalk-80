Chapter 26 gave an informal description of the object memory. Since the routines of Interpreter need to interact with the object memory, we need its formal functional specification. This will be presented as the protocol specification of class `ObjectMemory`. Chapter 30 will describe one way to implement this protocol specification.

The object memory associates a 16-bit object pointer with

1. the object pointer of a class-describing object and  
2. a set of 8- or 16-bit fields that contain object pointers or numerical values.

The interface to the object memory uses zero-relative integer indices to indicate an object’s fields. Instances of `Integer` are used for both object pointers and field indices in the interface between the interpreter and object memory.

The protocol of `ObjectMemory` contains pairs of messages for fetching and storing object pointers or numerical values in an object’s fields.

**object pointer access**
```smalltalk
fetchPointer: fieldIndex ofObject: objectPointer
    "Return the object pointer found in the field numbered fieldIndex of the object associated with objectPointer."

storePointer: fieldIndex ofObject: objectPointer withValue: valuePointer
    "Store the object pointer valuePointer in the field numbered fieldIndex of the object associated with objectPointer."
```

**word access**
```smalltalk
fetchWord: fieldIndex ofObject: objectPointer
    "Return the 16-bit numerical value found in the field numbered fieldIndex of the object associated with objectPointer."

storeWord: fieldIndex ofObject: objectPointer withValue: valueWord
    "Store the 16-bit numerical value valueWord in the field numbered fieldIndex of the object associated with objectPointer."
```

**byte access**
```smalltalk
fetchByte: byteIndex ofObject: objectPointer
    "Return the 8-bit numerical value found in the byte numbered byteIndex of the object associated with objectPointer."

storeByte: byteIndex ofObject: objectPointer withValue: valueByte
    "Store the 8-bit numerical value valueByte in the byte numbered byteIndex of the object associated with objectPointer."
```

Note that `fetchPointer:ofObject:` and `fetchWord:ofObject:` will probably be implemented in an identical fashion, since they both load a 16-bit quantity. However, the implementation of `storePointer:ofObject:` will be different from the implementation of `storeWord:ofObject:` since it will have to perform reference counting (see Chapter 30) if the object memory keeps dynamic reference counts. We have maintained a separate interface for `fetchPointer:ofObject:` and `fetchWord:ofObject:` for the sake of symmetry.

Even though most of the maintenance of reference counts can be done automatically in the `storePointer:ofObject:withValue:` routine, there are some points at which the interpreter routines must directly manipulate the reference counts. Therefore, the following two routines are included in the object memory interface. If an object memory uses only garbage collection to reclaim unreferenced objects, these routines are no-ops.

**reference counting**
```smalltalk
increaseReferencesTo: objectPointer
    "Add one to the reference count of the object whose object pointer is objectPointer."

decreaseReferencesTo: objectPointer
    "Subtract one from the reference count of the object whose object pointer is objectPointer."
```

Since every object contains the object pointer of its class description, that pointer could be considered the contents of one of the object’s fields. Unlike other fields, however, an object’s class may be fetched, but its value may not be changed. Given the special nature of this pointer, it was decided not to access it in the same way. Therefore, there is a special protocol for fetching an object’s class.

**class pointer access**
```smalltalk
fetchClassOf: objectPointer
    "Return the object pointer of the class-describing object for the object associated with objectPointer."
```

The length of an object might also be thought of as the contents of one of its fields. However, it is like the class field in that it may not be changed. There are two messages in the object memory protocol that ask for the number of words in an object and the number of bytes in an object. Note that we have not made a distinction between words and pointers in this case since we assume that they both fit in exactly one field.

**length access**
```smalltalk
fetchWordLengthOf: objectPointer
    "Return the number of fields in the object associated with objectPointer."

fetchByteLengthOf: objectPointer
    "Return the number of byte fields in the object associated with objectPointer."
```

Another important service of the object memory is to create new objects. The object memory must be supplied with a class and a length and will respond with a new object pointer. Again, there are three versions for creating objects with pointers, words, or bytes.

**object creation**
```smalltalk
instantiateClass: classPointer withPointers: instanceSize
    "Create a new instance of the class whose object pointer is classPointer with instanceSize fields that will contain pointers. Return the object pointer of the new object."

instantiateClass: classPointer withWords: instanceSize
    "Create a new instance of the class whose object pointer is classPointer with instanceSize fields that will contain 16-bit numerical values. Return the object pointer of the new object."

instantiateClass: classPointer withBytes: instanceByteSize
    "Create a new instance of the class whose object pointer is classPointer with room for instanceByteSize 8-bit numerical values. Return the object pointer of the new object."
```

Two routines of the object memory allow the instances of a class to be enumerated. These follow an arbitrary ordering of object pointers. Using the numerical order of the pointers themselves is reasonable.

**instance enumeration**
```smalltalk
initialInstanceOf: classPointer
    "Return the object pointer of the first instance of the class whose object pointer is classPointer in the defined ordering (e.g., the one with the smallest object pointer)."

instanceAfter: objectPointer
    "Return the object pointer of the next instance of the same class as the object whose object pointer is objectPointer in the defined ordering (e.g., the one with the next larger object pointer)."
```

Another routine of the object memory allows the object pointers of two objects to be interchanged.

**pointer swapping**
```smalltalk
swapPointersOf: firstPointer and: secondPointer
    "Make firstPointer refer to the object whose object pointer was secondPointer and make secondPointer refer to the object whose object pointer was firstPointer."
```

As described in Chapter 26, integers between −16384 and 16383 are encoded directly as object pointers with a 1 in the low-order bit position and the appropriate 2’s complement value stored in the high-order 15 bits. These objects are instances of class `SmallInteger`. A SmallInteger’s value, which would ordinarily be stored in a field, is actually determined from its object pointer. So instead of storing a value into a SmallInteger’s field, the interpreter must request the object pointer of a SmallInteger with the desired value (using the `integerObjectOf:` routine). And instead of fetching the value from a field, it must request the value associated with the object pointer (using the `integerValueOf:` routine). There are also two routines that determine whether an object pointer refers to a SmallInteger (`isIntegerObject:`) and whether a value is in the right range to be represented as a SmallInteger (`isIntegerValue:`). The function of the `isIntegerObject:` routine can also be performed by requesting the class of the object and seeing if it is `SmallInteger`.

**integer access**
```smalltalk
integerValueOf: objectPointer
    "Return the value of the instance of SmallInteger whose pointer is objectPointer."

integerObjectOf: value
    "Return the object pointer for an instance of SmallInteger whose value is value."

isIntegerObject: objectPointer
    "Return true if objectPointer is an instance of SmallInteger, false if not."

isIntegerValue: value
    "Return true if value can be represented as an instance of SmallInteger, false if not."
```

The interpreter provides two special routines to access fields that contain SmallIntegers. The `fetchInteger:ofObject:` routine returns the value of a SmallInteger whose pointer is stored in the specified field. The check to make sure that the pointer is for a SmallInteger is made for uses of this routine when non-SmallIntegers can be tolerated. The `primitiveFail` routine will be described in the section on primitive routines.

```smalltalk
fetchInteger: fieldIndex ofObject: objectPointer
    | integerPointer |
    integerPointer ← memory fetchPointer: fieldIndex
        ofObject: objectPointer.
    (memory isIntegerObject: integerPointer)
        ifTrue: [↑memory integerValueOf: integerPointer]
        ifFalse: [↑self primitiveFail]
```

The `storeInteger:ofObject:withValue:` routine stores the pointer of the SmallInteger with specified value in the specified field.

```smalltalk
storeInteger: fieldIndex
    ofObject: objectPointer
    withValue: integerValue
    | integerPointer |
    (memory isIntegerValue: integerValue)
        ifTrue: [integerPointer ← memory integerObjectOf: integerValue.
            memory storePointer: fieldIndex
                ofObject: objectPointer
                withValue: integerPointer]
        ifFalse: [↑self primitiveFail]
```

The interpreter also provides a routine to perform a transfer of several pointers from one object to another. It takes the number of pointers to transfer, and the initial field index and object pointer of the source and destination objects as arguments.

```smalltalk
transfer: count
    fromIndex: firstFrom
    ofObject: fromOop
    toIndex: firstTo
    ofObject: toOop
    | fromIndex toIndex lastFrom oop |
    fromIndex ← firstFrom.
    lastFrom ← firstFrom + count.
    toIndex ← firstTo.
    [fromIndex < lastFrom] whileTrue:
        [oop ← memory fetchPointer: fromIndex
            ofObject: fromOop.
         memory storePointer: toIndex
            ofObject: toOop
            withValue: oop.
         fromIndex ← fromIndex + 1.
         toIndex ← toIndex + 1]
```

The interpreter also provides routines to extract bit fields from numerical values. These routines refer to the high-order bit with index 0 and the low-order bit with index 15.

```smalltalk
extractBits: firstBitIndex to: lastBitIndex of: anInteger
    ↑(anInteger bitShift: lastBitIndex - 15)
        bitAnd: (2 raisedTo: lastBitIndex - firstBitIndex + 1) - 1

highByteOf: anInteger
    ↑self extractBits: 0 to: 7 of: anInteger

lowByteOf: anInteger
    ↑self extractBits: 8 to: 15 of: anInteger
```
