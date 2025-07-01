# Virtual Machine - Object Memory

The two major components of any Smalltalk-80 implementation are the bytecode interpreter and the object memory. Chapters 28 and 29 described an implementation of the bytecode interpreter. This chapter describes an implementation of the object memory. The function of the object memory is to create, store, and destroy objects, and to provide access to their fields.

Memory-management systems fall into two major categories, _real-memory_ implementations and _virtual-memory_ implementations. In a real-memory implementation, all the objects in the environment reside in primary memory that is directly addressable by the program. In a virtual-memory implementation, objects reside in more than one level of a memory hierarchy and must be shuffled among the various levels during execution. This chapter describes the design of RealObjectMemory, an object memory for a real-memory Smalltalk-80.

Although Smalltalk can be implemented on computers of any word size, this presentation will be simplified by several assumptions in the standard algorithms. The routines of RealObjectMemory assume:

- that there are eight bits in a byte,
- that there are two bytes in a word,
- that the more significant byte of a word precedes the less significant byte, and
- that the target computer is word addressed and word indexed.

Moreover, the routines assume that the address space is partitioned into 16 or fewer _segments_ of 64K (65,536) words apiece. The standard algorithms can be systematically changed to adapt them to hardware with different properties. The routines of RealObjectMemory deal almost exclusively with 16-bit integers, as would a machine-language implementation.

To access locations in the address space of the host machine, machine-language implementations use load and store instructions. In RealObjectMemory, the load and store instructions are symbolized by messages to an instance of RealWordMemory whose name is `wordMemory`. The protocol of RealWordMemory is shown below.

## RealWordMemory instance protocol

| Message                                      | Description                                                           |
|----------------------------------------------|-----------------------------------------------------------------------|
| `segment: s word: w`                         | Return word `w` of segment `s`.                                       |
| `segment: s word: w put: value`              | Store `value` into word `w` of segment `s`; return `value`.           |
| `segment: s word: w byte: byteNumber`        | Return byte `byteNumber` of word `w` of segment `s`.                  |
| `segment: s word: w byte: byteNumber put: v` | Store `value` into byte `byteNumber` of word `w` of segment `s`; return `value`. |
| `segment: s word: w bits: first to: last`    | Return bits `first` to `last` of word `w` of segment `s`.             |
| `segment: s word: w bits: first to: last put: value` | Store `value` into bits `first` to `last` of word `w` of segment `s`; return `value`. |

When it is necessary to distinguish the two bytes of a word, the left (more significant) byte will be referred to with the index 0 and the right (less significant) byte with the index 1. The most significant bit in a word will be referred to with the index 0 and the least significant with the index 15. Note that `self` is an instance of class RealObjectMemory in all routines of this chapter.

The most important thing about any implementation of the object memory is that it conform to the functional specification of the object memory interface given in Chapter 27. This chapter describes a range of possible implementations of that interface. In particular, simple versions of some routines are presented early in the chapter and refined versions are presented later as the need for those refinements becomes clear. These preliminary versions will be flagged by including the comment `**Preliminary Version**` on the first line of the routine.

---

Figure 30.1 shows the format of an allocated object in the heap. The actual data of the object are preceded by a two‑word header. The size field of the header indicates the number of words of heap that the object occupies, including the header. It is an unsigned 16‑bit number, and can range from 2 up to 65,536.

```
Figure 30.1
+------------+   < Header >
| size = N+2 |   (unsigned 16-bit count of words)
+------------+
|   CLASS    |   < Body >
+------------+
|   Field 0  |
+------------+
|   Field 1  |
+    ...     
|   Field N-2|
+------------+
|   Field N-1|
+------------+
```

When memory is segmented, it is usually convenient for a Smalltalk-80 implementation to divide the heap into _heap segments_, each in a different memory segment. As stated earlier, the routines in this chapter assume that the target computer is segmented into address spaces of 65,536 words.

### Heap Related Constants

| Constant             | Description                                                          |
|----------------------|----------------------------------------------------------------------|
| `HeapSegmentCount`   | The number of heap segments used in the implementation.              |
| `FirstHeapSegment`   | The index of the first memory segment used to store the heap.        |
| `LastHeapSegment`    | The index of the last memory segment used to store the heap (`FirstHeapSegment + HeapSegmentCount – 1`). |
| `HeapSpaceStop`      | The address of the last location used in each heap segment.         |
| `HeaderSize`         | The number of words in an object header (2).                         |

```
Figure 30.2  Fragmented vs Compacted Memory
(a) Fragmented Memory          (b) Compacted Memory
[ … ]
```

#### The Object Table

When an object is moved during compaction, all pointers to its heap memory must be updated. To make pointer updates inexpensive, only one pointer to an object's heap memory is allowed. That pointer is stored in a table called the _object table_. All references to an object must be indirected through the object table. Thus, the object pointers found in Smalltalk objects are really indices into the object table, which in turn point to the heap.

```
Figure 30.3
[ object pointer ] --> [ object table entry ] --> [ heap ]
                              ^                |
                              |________________|
```

A common arrangement is for each object table entry to occupy two words, and for the entire table to occupy 64K words or less, yielding a maximum capacity of 32K objects.

#### Object Pointers

An object pointer occupies 16 bits, apportioned as in Figure 30.4.

```
Figure 30.4
+----------------+---+
| Object Table   | 0 |
| Index (15 bits)|   |
+----------------+---+
| Immediate      | 1 |
| Signed Integer |
+----------------+
```

When the low-order bit of the object pointer is 0, the first 15 bits are an index into the object table. Up to 2^15 (32K) objects can be addressed. When the low-order bit is 1, the first 15 bits are an immediate signed integer, and no additional space in the object table or the heap is utilized. The benefit of giving special treatment to integers in the range ±2^14 is that they come and go with high frequency during arithmetic and many other operations.

```
isIntegerObject: objectPointer
    ↑ (objectPointer bitAnd: 1) = 1

cantBeIntegerObject: objectPointer
    (self isIntegerObject: objectPointer)
        ifTrue: [Sensor notify: 'A small integer has no object table entry']
```

#### Object Table Entries

The format of an object table entry is shown in Figure 30.5. If the free entry bit is on, then the entry is free. If the free entry bit is off, then the four segment bits select a heap segment and the 16 location bits locate the beginning of the space in that segment that is owned by the object table entry. The count field, the odd length bit (O), and the pointer‑fields bit (P) will be explained later in the chapter.

```
Figure 30.5
+---------+---+---+---+--------+
| COUNT   | O | P | F | SEGMENT|
+---------+---+---+---+--------+
|           LOCATION           |
+------------------------------+
```

##### Object Table Related Constants

| Constant              | Description                                                        |
|-----------------------|--------------------------------------------------------------------|
| `ObjectTableSegment`  | The number of the memory segment containing the object table.      |
| `ObjectTableStart`    | The location in `ObjectTableSegment` of the base of the object table. |
| `ObjectTableSize`     | The number of words in the object table (an even number ≤ 64K).     |
| `HugeSize`            | The smallest number that is too large to represent in an eight‑bit count field; that is, 256. |
| `NilPointer`          | The object table index of the object `nil`.                        |

```
ot: objectPointer
    self cantBeIntegerObject: objectPointer.
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer

ot: objectPointer put: value
    self cantBeIntegerObject: objectPointer.
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer
        put: value

ot: objectPointer bits: firstBitIndex to: lastBitIndex
    self cantBeIntegerObject: objectPointer.
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer
        bits: firstBitIndex to: lastBitIndex

ot: objectPointer bits: firstBitIndex to: lastBitIndex put: value
    self cantBeIntegerObject: objectPointer.
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer
        bits: firstBitIndex to: lastBitIndex
        put: value
```

The following 12 object-access subroutines load or store the various fields of the object table entry of `objectPointer`.

```
countBitsOf: objectPointer
    ↑ self ot: objectPointer bits: 0 to: 7

countBitsOf: objectPointer put: value
    ↑ self ot: objectPointer bits: 0 to: 7 put: value

oddBitOf: objectPointer
    ↑ self ot: objectPointer bits: 8 to: 8

oddBitOf: objectPointer put: value
    ↑ self ot: objectPointer bits: 8 to: 8 put: value

pointerFieldsBitOf: objectPointer
    ↑ self ot: objectPointer bits: 9 to: 9

pointerFieldsBitOf: objectPointer put: value
    ↑ self ot: objectPointer bits: 9 to: 9 put: value

freeEntryBitOf: objectPointer
    ↑ self ot: objectPointer bits: 10 to: 10

freeEntryBitOf: objectPointer put: value
    ↑ self ot: objectPointer bits: 10 to: 10 put: value

segmentOf: objectPointer
    ↑ self ot: objectPointer bits: 11 to: 14

segmentOf: objectPointer put: value
    ↑ self ot: objectPointer bits: 11 to: 14 put: value

locationOf: objectPointer
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer + 1

locationOf: objectPointer put: value
    ↑ wordMemory segment: ObjectTableSegment
        word: ObjectTableStart + objectPointer + 1
        put: value
```
