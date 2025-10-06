# Smalltalk-80 Interpreter Architecture

## 32-bit Tagged Object References (OOPs)

This interpreter uses a **32-bit tagged pointer** architecture for all Smalltalk object references, providing:
- **31-bit SmallInteger** immediate values
- **31-bit Object Table Index** for heap-allocated objects

### Tagging Scheme

All object references (`SObjectRef`) are 32-bit values with LSB tagging:

```
SmallInteger:        xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1  (LSB = 1)
Object Table Index:  xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx0  (LSB = 0)
```

### SmallInteger Encoding

SmallIntegers are **never allocated** on the heap. They use immediate encoding:

```csharp
// Encode: left shift 1 bit, set LSB = 1
int oop = (value << 1) | 1;

// Decode: arithmetic right shift 1 bit
int value = oop >> 1;
```

**Range**: -2^30 to 2^30-1 (1,073,741,824 values)

**Examples**:
- `0` → `0b00000000000000000000000000000001` (OOP = 1)
- `1` → `0b00000000000000000000000000000011` (OOP = 3)
- `-1` → `0b11111111111111111111111111111111` (OOP = -1)
- `42` → `0b00000000000000000000000001010101` (OOP = 85)

### Object Table Index Encoding

Heap-allocated objects are stored in an **ObjectTable** and referenced by index:

```csharp
// Encode: left shift 1 bit, LSB = 0 (automatic)
int oop = index << 1;

// Decode: right shift 1 bit
int index = oop >> 1;
```

**Capacity**: 2^31 objects (2,147,483,648 entries)

**Examples**:
- Index 0 → OOP `0b00000000000000000000000000000000` (reserved for nil)
- Index 1 → OOP `0b00000000000000000000000000000010` (OOP = 2)
- Index 2 → OOP `0b00000000000000000000000000000100` (OOP = 4)

### ObjectTable Structure

The ObjectTable is a segmented array that:
1. Maps 31-bit indices to `ObjectData` structures
2. Each segment contains 4096 entries
3. Segments are allocated on-demand (no reallocation/copying)
4. Free list provides O(1) allocation

```csharp
public sealed class ObjectTable
{
    // Allocate object and return index
    public int Allocate(ObjectMemory.ObjectData data);

    // Convert between OOP and index
    public static int IndexFromOop(int oop) => oop >> 1;
    public static int OopFromIndex(int index) => index << 1;
}
```

### SObjectRef API

```csharp
// SmallInteger creation (no allocation)
SObjectRef.FromSmallInteger(42);  // Returns tagged OOP

// Object reference creation
SObjectRef.FromObjectPointer(oop); // oop = index << 1

// Tag checking
bool isSmallInt = objRef.IsSmallInteger;  // Check LSB == 1
bool isObject = objRef.IsObject;          // Check LSB == 0 && != 0
bool isNil = objRef.IsNil;                // Check == 0

// Value extraction
int value = objRef.AsSmallInteger();      // value = oop >> 1
int oop = objRef.AsObjectPointer();       // Returns raw OOP
```

### Memory Layout

```
┌────────────────────────────────────────────┐
│ SmallInteger (Immediate - No Allocation)   │
├────────────────────────────────────────────┤
│ 31-bit value | 1 (LSB = 1)                 │
└────────────────────────────────────────────┘

┌────────────────────────────────────────────┐
│ Object Reference (Heap-Allocated)          │
├────────────────────────────────────────────┤
│ 31-bit index | 0 (LSB = 0)                 │
│              ↓                              │
│         ObjectTable[index]                 │
│              ↓                              │
│         ObjectData                         │
│         - ClassOop                         │
│         - Format                           │
│         - Fields (SObjectRef[])            │
│         - IndexedData (byte[]/ushort[])    │
└────────────────────────────────────────────┘
```

### Blue Book Compliance

This design strictly follows the Smalltalk-80 Blue Book specification:
- **Only SmallInteger** uses immediate encoding
- **All other objects** are heap-allocated (Character, Symbol, String, etc.)
- **LSB tagging** for efficient type discrimination
- **31-bit ranges** for both integers and object table

### Performance Characteristics

- **SmallInteger operations**: O(1), no allocation
- **Object allocation**: O(1) with free list
- **Object access**: O(1) table lookup
- **Tag check**: O(1) bitwise AND
- **GC marking**: O(n) with mark bit in ObjectData
