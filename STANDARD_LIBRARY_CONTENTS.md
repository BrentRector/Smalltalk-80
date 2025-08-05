# Smalltalk-80 Standard Library Contents

This document describes the complete contents of the StandardLibrary.st file, which contains the core classes and methods for the Smalltalk-80 interpreter implementation.

## Overview

**Total Classes:** 35  
**Total Method Chunks:** 191  
**Last Updated:** August 5, 2025

## Recent Additions (August 5, 2025)
- ✅ **Symbol** - Fundamental string-like objects with identity semantics (29 methods)
- ✅ **IdentitySet** - Set variant using identity comparison (== instead of =) with 1 specialized method
- ✅ **LookupKey** - Abstract key class for dictionary entries (6 methods)
- ✅ **Association** - Key-value pairs for dictionary entries (4 methods)
- ✅ **Dictionary** - Hash-based key-value collections (38 methods)
- ✅ **IdentityDictionary** - Dictionary using identity comparison (== instead of =) (16 methods)
- ✅ **Float** - IEEE 32-bit floating-point numbers with complete mathematical functions (35 methods)
- ⏳ **Exception hierarchy** - Not present in original Smalltalk-80

## 5-Step Expansion Plan Status
1. ✅ Dictionary/LookupKey/Association classes - Successfully added with full functionality
2. ✅ Symbol class - Successfully added with full functionality 
3. ✅ Float class - Successfully added with complete mathematical functionality and constants
4. ✅ IdentitySet - Successfully added as Set subclass with identity comparison
5. ⏳ Exception handling - Not applicable (not in original Smalltalk-80 system)

## Class Hierarchy

```
Object
├── UndefinedObject
├── Magnitude
│   ├── Number
│   │   ├── Integer
│   │   │   ├── SmallInteger
│   │   │   ├── LargePositiveInteger
│   │   │   └── LargeNegativeInteger
│   │   └── Float
│   ├── Character
│   └── LookupKey
│       └── Association
├── Boolean
│   ├── True
│   └── False
├── Collection
│   ├── SequenceableCollection
│   │   ├── ArrayedCollection
│   │   │   ├── Array
│   │   │   └── String
│   │   │       └── Symbol
│   │   ├── OrderedCollection
│   │   │   └── SortedCollection
│   │   └── Interval
│   ├── Set
│   │   ├── IdentitySet
│   │   └── Dictionary
│   │       └── IdentityDictionary
│   ├── Bag
│   └── MappedCollection
├── BlockContext
└── Stream
    └── PositionableStream
        ├── WriteStream
        └── ReadStream
```

## Core System Classes

### Object
**Superclass:** nil (root class)  
**Instance Variables:** none  
**Purpose:** Root of the class hierarchy, provides fundamental behavior for all objects

**Key Methods:**
- Identity and equality: `#=`, `#~=`, `#==`, `#~~`, `#hash`
- Object introspection: `#class`, `#isMemberOf:`, `#isKindOf:`, `#respondsTo:`
- Error handling: `#error:`, `#halt`, `#doesNotUnderstand:`
- Primitive access: `#at:`, `#at:put:`, `#basicAt:`, `#basicAt:put:`, `#size`
- Copying: `#copy`, `#shallowCopy`
- String representation: `#printOn:`, `#printString`, `#storeOn:`, `#storeString`

### UndefinedObject
**Superclass:** Object  
**Instance Variables:** none  
**Purpose:** Represents the unique nil object

**Key Methods:**
- `#isNil` - returns true
- `#notNil` - returns false
- `#printString` - returns 'nil'

## Magnitude Hierarchy

### Magnitude
**Superclass:** Object  
**Instance Variables:** none  
**Purpose:** Abstract superclass for objects that can be compared and ordered

### Number
**Superclass:** Magnitude  
**Instance Variables:** none  
**Purpose:** Abstract superclass for all numeric values

**Key Methods:**
- `#negated` - returns the arithmetic negation

### Integer
**Superclass:** Number  
**Instance Variables:** none  
**Purpose:** Abstract superclass for integer numbers

**Key Methods:**
- `#even` - tests if the integer is even
- `#isInteger` - returns true

### SmallInteger
**Superclass:** Integer  
**Instance Variables:** none  
**Purpose:** Represents small integers (typically 30-bit signed integers)

**Key Methods:**
- Arithmetic: `#+`, `#-`, `#*`, `#/`, `#//`, `#\\`
- Comparison: `#<`, `#<=`, `#>`, `#>=`, `#=`, `#~=`
- Bit operations: `#bitAnd:`, `#bitOr:`, `#bitXor:`, `#bitShift:`
- Testing: `#even`, `#odd`, `#positive`, `#negative`, `#strictlyPositive`, `#sign`
- Conversion: `#asString`, `#asFloat`, `#asObject`, `#asOop`

### LargePositiveInteger & LargeNegativeInteger
**Superclass:** Integer  
**Instance Variables:** none (placeholder classes)  
**Purpose:** Represents arbitrarily large positive and negative integers

### Float
**Superclass:** Number  
**Instance Variables:** none (variable word subclass)  
**Purpose:** Represents IEEE 32-bit floating-point numbers with 8-9 digits of accuracy

**Class Variables:**
- `Pi`, `Halfpi`, `Fourthpi`, `Twopi` - Mathematical constants for π and its derivatives
- `RadiansPerDegree` - Conversion factor between degrees and radians
- `Ln2`, `Sqrt2` - Mathematical constants for natural log of 2 and square root of 2
- `SinCoefficients`, `TanCoefficients` - Polynomial coefficients for trigonometric functions
- `ExpPCoefficients`, `ExpQCoefficients` - Polynomial coefficients for exponential function
- `LnCoefficients` - Polynomial coefficients for natural logarithm function

**Key Methods:**
- Arithmetic: `#+`, `#-`, `#*`, `#/`, `#negated`
- Mathematical functions: `#sin`, `#cos`, `#tan`, `#arcSin`, `#arcCos`, `#arcTan`, `#exp`, `#ln`, `#log`, `#sqrt`
- Comparison: `#<`, `#<=`, `#>`, `#>=`, `#=`, `#~=`, `#hash`
- Truncation: `#truncated`, `#rounded`, `#fractionPart`, `#integerPart`
- Coercing: `#coerce:`, `#generality`
- Converting: `#asFloat`, `#asFraction`, `#degreesToRadians`, `#radiansToDegrees`
- Copying: `#deepCopy`, `#shallowCopy`
- Printing: `#isLiteral`, `#printOn:`, `#printOn:digits:`
- Private: `#exponent`, `#mantissa:`, `#timesTwoPower:`, `#absPrintOn:digits:`, `#floorLog:`
- **Class Methods:** `#initialize`, `#pi`, `#readFrom:`

### Character
**Superclass:** Magnitude  
**Instance Variables:** none  
**Purpose:** Represents individual characters

**Key Methods:**
- Properties: `#asciiValue`, `#digitValue`, `#value`
- Comparison: `#=`, `#<`, `#<=`, `#>`, `#>=`
- Classification: `#isAlphaNumeric`, `#isDigit`, `#isLetter`, `#isLowercase`, `#isUppercase`, `#isSeparator`, `#isVowel`
- Conversion: `#asCharacter`, `#asInteger`, `#asLowercase`, `#asUppercase`, `#asSymbol`
- **Class Methods:** `#value:`, `#cr`, `#lf`, `#tab`, `#space`, `#initialize`

### LookupKey
**Superclass:** Magnitude  
**Instance Variables:** `key`  
**Purpose:** Abstract key class for dictionary entries, provides comparison and hashing behavior

**Key Methods:**
- Comparison: `#<`, `#=`, `#hash`, `#hashMappedBy:`
- Accessing: `#key`, `#key:`
- Printing: `#printOn:`
- **Class Methods:** `#key:`

### Association
**Superclass:** LookupKey  
**Instance Variables:** `value`  
**Purpose:** Key-value pairs for dictionary entries, combines a key with its associated value

**Key Methods:**
- Accessing: `#key:value:`, `#value`, `#value:`
- Printing: `#printOn:` (shows key->value format)
- **Class Methods:** `#key:value:`

## Boolean Hierarchy

### Boolean
**Superclass:** Object  
**Instance Variables:** none  
**Purpose:** Abstract superclass for truth values

**Key Methods:**
- Logical operations: `#&`, `#|`, `#not`, `#and:`, `#or:`, `#eqv:`, `#xor:`
- Control flow: `#ifTrue:`, `#ifFalse:`, `#ifTrue:ifFalse:`, `#ifFalse:ifTrue:`

### True
**Superclass:** Boolean  
**Instance Variables:** none  
**Purpose:** Represents the unique true object

**Key Methods:**
- Logical operations returning appropriate boolean values
- Control flow methods that execute true branches

### False
**Superclass:** Boolean  
**Instance Variables:** none  
**Purpose:** Represents the unique false object

**Key Methods:**
- Logical operations returning appropriate boolean values
- Control flow methods that execute false branches

## Collection Hierarchy

### Collection
**Superclass:** Object  
**Instance Variables:** none  
**Purpose:** Abstract superclass for all collections

**Key Methods:**
- Basic protocol: `#size`, `#isEmpty`, `#notEmpty`
- Testing: `#includes:`, `#occurrencesOf:`
- Enumeration: `#do:`, `#collect:`, `#select:`, `#reject:`, `#detect:`, `#detect:ifNone:`, `#inject:into:`
- Modification: `#add:`, `#addAll:`, `#remove:`, `#remove:ifAbsent:`, `#removeAll:`
- Utility: `#species`, error methods

### SequenceableCollection
**Superclass:** Collection  
**Instance Variables:** none  
**Purpose:** Abstract superclass for ordered collections

**Key Methods:**
- Accessing: `#first`, `#last`, `#atAll:put:`, `#atAllPut:`, `#indexOf:`, `#indexOf:ifAbsent:`
- Modification: `#replaceFrom:to:with:`, `#replaceFrom:to:with:startingAt:`
- Copying: `#copyFrom:to:`, `#copyReplaceFrom:to:with:`, `#copyWith:`, `#copyWithout:`
- Enumeration: enhanced `#do:`, `#collect:`, `#select:`, `#findFirst:`, `#findLast:`, `#reverseDo:`
- Concatenation: `#,`

### ArrayedCollection
**Superclass:** SequenceableCollection  
**Instance Variables:** none  
**Purpose:** Abstract superclass for collections with fixed indexed elements

**Key Methods:**
- Basic protocol: `#size`, `#add:` (error), `#storeOn:`
- **Class Methods:** `#new`, `#new:withAll:`, `#with:`, `#with:with:`, etc.

### Array
**Superclass:** ArrayedCollection  
**Instance Variables:** none (variable indexed)  
**Purpose:** Fixed-size collection of objects accessible by integer index

**Key Methods:**
- Accessing: `#at:`, `#at:put:`, `#size` (primitive-based)
- Enumeration: `#do:`
- Modification: `#add:`, `#remove:ifAbsent:`
- Utility: `#copyWith:`, `#,`, `#hash`, `#hashMappedBy:`, `#asArray`, `#isLiteral`
- **Class Methods:** `#new:`, `#with:`, `#with:with:`, `#with:with:with:`

### String
**Superclass:** ArrayedCollection  
**Instance Variables:** none (variable indexed)  
**Purpose:** Collection of characters

**Key Methods:**
- Accessing: `#at:`, `#at:put:`, `#size`
- Enumeration: `#do:`
- Modification: `#add:`, `#remove:ifAbsent:`
- Conversion: `#asSymbol`, `#asUppercase`, `#asLowercase`
- Comparison: `#=`, `#<`, `#<=`, `#>`, `#>=`
- Concatenation: `#,`

### Symbol  
**Superclass:** String  
**Instance Variables:** none (variable indexed)  
**Purpose:** Immutable string-like objects with identity semantics, used for method selectors

**Key Methods:**
- Identity: `#=`, `#hash`, `#hashMappedBy:` (identity-based comparison)
- Copying: `#copy`, `#shallowCopy` (returns self - symbols are immutable)
- Conversion: `#asString`, `#asSymbol` (returns self)
- Modification: `#at:put:`, `#replaceFrom:to:with:startingAt:` (error - symbols are immutable)
- Printing: `#printOn:`, `#storeOn:`
- Parsing: `#classPart`, `#selectorPart`, `#isCompound`, `#isInfix`, `#isKeyword`, `#keywords`, `#numArgs`
- Utility: `#species`, `#string:`, `#stringhash`, `#errorNoModification`
- **Class Methods:** `#initialize`, `#intern:`, `#internCharacter:`, `#hasInterned:ifTrue:`, `#correctMessage:`, `#rehash`, `#table`, `#table:`

### OrderedCollection
**Superclass:** SequenceableCollection  
**Instance Variables:** `array`, `firstIndex`, `lastIndex`  
**Purpose:** Variable-size ordered collection with efficient insertion/removal

**Key Methods:**
- Accessing: `#at:`, `#at:put:`, `#first`, `#last`, `#size`, `#after:`, `#before:`
- Adding: `#add:`, `#addFirst:`, `#addLast:`, `#add:after:`, `#add:before:`, `#addAll:`, `#addAllFirst:`, `#addAllLast:`
- Removing: `#remove:ifAbsent:`, `#removeFirst`, `#removeLast`, `#removeAllSuchThat:`
- Copying: `#copyEmpty`, `#copyFrom:to:`, `#copyReplaceFrom:to:with:`, `#copyWith:`, `#copyWithout:`
- Private: `#grow`, `#find:`, `#insert:before:`, `#makeRoomAtFirst`, `#makeRoomAtLast`
- **Class Methods:** `#new`, `#new:`

### SortedCollection
**Superclass:** OrderedCollection  
**Instance Variables:** `sortBlock`  
**Purpose:** Ordered collection that maintains elements in sorted order

**Key Methods:**
- Accessing: `#at:put:` (error), `#sortBlock`, `#sortBlock:`
- Comparison: `#=` (compares sortBlock and elements)
- Copying: `#copy`, `#copyEmpty`
- Adding: `#add:`, `#addAll:`, `#grow`
- Enumeration: `#collect:` (returns OrderedCollection)
- Private: `#indexForInserting:`, `#reSort`, `#sort:to:`, `#swap:with:`
- **Class Methods:** `#new:`, `#sortBlock:`

### Interval
**Superclass:** SequenceableCollection  
**Instance Variables:** `start`, `stop`, `step`  
**Purpose:** Arithmetic progression (e.g., 1 to: 10 by: 2)

**Key Methods:**
- Comparison: `#=`, `#hash`, `#hashMappedBy:`
- Accessing: `#at:`, `#at:put:` (error), `#first`, `#last`, `#increment`, `#size`
- Modification: `#add:` (error), `#remove:` (error)
- Copying: `#copy`
- Enumeration: `#collect:`, `#do:`, `#reverseDo:`
- Printing: `#printOn:`, `#storeOn:`
- Private: `#setFrom:to:by:`, `#species`
- **Class Methods:** `#from:to:`, `#from:to:by:`, `#new`

### Set
**Superclass:** Collection  
**Instance Variables:** `tally`, `array`  
**Purpose:** Unordered collection with no duplicate elements (hash-based)

**Key Methods:**
- Accessing: `#at:` (error), `#at:put:` (error), `#size`, `#includes:`, `#occurrencesOf:`
- Modification: `#add:`, `#remove:ifAbsent:`, `#grow`
- Enumeration: `#collect:`, `#do:`
- Private: `#atNewIndex:put:`, `#find:ifAbsent:`, `#findElementOrNil:`, `#fixCollisionsFrom:`, `#fullCheck`, `#noCheckAdd:`, `#rehash`, `#setTally`, `#swap:with:`
- **Class Methods:** `#new`, `#new:`

### IdentitySet
**Superclass:** Set  
**Instance Variables:** none (inherits from Set)  
**Purpose:** Like Set, but uses identity comparison (==) instead of equality (=)

**Key Methods:**
- Inherits all Set methods except:
- Private: `#findElementOrNil:` (overridden to use `==` instead of `=`)

### Bag
**Superclass:** Collection  
**Instance Variables:** `contents`  
**Purpose:** Collection that allows duplicates and tracks occurrence counts

**Key Methods:**
- Accessing: `#at:`, `#at:put:`, `#size`, `#includes:`, `#occurrencesOf:`
- Modification: `#add:`, `#add:withOccurrences:`, `#remove:ifAbsent:`
- Enumeration: `#do:`
- Private: `#errorNotKeyed`, `#setDictionary`
- **Class Methods:** `#new`

### MappedCollection
**Superclass:** Collection  
**Instance Variables:** `domain`, `map`  
**Purpose:** Provides a mapped view of another collection

**Key Methods:**
- Accessing: `#at:`, `#at:put:`, `#contents`, `#size`
- Modification: `#add:` (error)
- Copying: `#copy`
- Private: `#setCollection:map:`, `#species`
- Enumeration: `#collect:`, `#do:`, `#select:`
- **Class Methods:** `#collection:map:`, `#new` (error)

### Dictionary
**Superclass:** Set  
**Instance Variables:** none (variable indexed, stores associations)  
**Purpose:** Hash-based key-value collections using equality comparison for keys

**Key Methods:**
- Accessing: `#at:`, `#at:ifAbsent:`, `#at:put:`, `#associationAt:`, `#associationAt:ifAbsent:`
- Key/Value access: `#keys`, `#values`, `#associations`, `#keyAtValue:`, `#keyAtValue:ifAbsent:`
- Testing: `#includes:`, `#includesKey:`, `#includesAssociation:`, `#occurrencesOf:`
- Adding: `#add:`, `#declare:from:`, `#grow`
- Removing: `#remove:` (error), `#removeKey:`, `#removeKey:ifAbsent:`, `#removeAssociation:`, `#removeAssociation:ifAbsent:`
- Enumeration: `#do:`, `#collect:`, `#select:`, `#associationsDo:`, `#keysDo:`
- Converting: `#asSortedCollection`
- Printing: `#printOn:`, `#storeOn:`
- Private: `#errorKeyNotFound`, `#errorValueNotFound`, `#findKey:ifAbsent:`, `#findKeyOrNil:`, `#rehash`

### IdentityDictionary
**Superclass:** Dictionary  
**Instance Variables:** `valueArray`  
**Purpose:** Dictionary using identity comparison (==) instead of equality (=) for keys, with parallel arrays for keys and values

**Key Methods:**
- Accessing: `#at:ifAbsent:`, `#at:put:`, `#associationAt:ifAbsent:`, `#keyAtValue:ifAbsent:`
- Adding: `#add:`
- Copying: `#copy`
- Enumeration: `#do:`, `#associationsDo:`
- Removing: `#removeKey:ifAbsent:`
- Private: `#findKeyOrNil:`, `#noCheckAdd:`, `#noCheckAdd:with:`, `#removeDangerouslyKey:ifAbsent:`, `#setTally`, `#swap:with:`
- **Class Methods:** `#new:` (requires power of 2 size)

## Block and Stream Classes

### BlockContext
**Superclass:** Object  
**Instance Variables:** none (implementation-specific)  
**Purpose:** Represents executable code blocks (closures)

**Key Methods:**
- Evaluation: `#value`, `#value:`, `#value:value:`
- Control flow: `#whileTrue:`, `#whileFalse:`, `#whileTrue`, `#whileFalse`

### Stream
**Superclass:** Object  
**Instance Variables:** none  
**Purpose:** Abstract superclass for sequential access to collections

**Key Methods:**
- Basic protocol: `#contents`, `#next`, `#next:`, `#next:put:`, `#nextPut:`, `#nextPutAll:`, `#nextMatchFor:`, `#atEnd`
- Enumeration: `#do:`

### PositionableStream
**Superclass:** Stream  
**Instance Variables:** `collection`, `position`, `readLimit`  
**Purpose:** Stream with positioning capability

**Key Methods:**
- Content access: `#contents`, `#next:`, `#peek`, `#peekFor:`, `#through:`, `#upTo:`
- Positioning: `#position`, `#position:`, `#reset`, `#setToEnd`, `#skip:`, `#skipTo:`
- Testing: `#atEnd`, `#isEmpty`
- Utility: `#nextChunk`, `#skipSeparators`, `#on:`
- **Class Methods:** `#on:`, `#on:from:to:`

### WriteStream
**Superclass:** PositionableStream  
**Instance Variables:** `writeLimit`  
**Purpose:** Stream for writing/building collections

**Key Methods:**
- Writing: `#next`, `#cr`, `#crtab`, `#crtab:`, `#space`, `#tab`, `#nextPut:`, `#position:`
- Formatting: `#emphasis:`, `#print:`, `#store:`, `#nextChunkPut:`
- Private: `#pastEndPut:`, `#growToInclude:`
- **Class Methods:** `#on:`, `#with:`, `#with:from:to:`

### ReadStream
**Superclass:** PositionableStream  
**Instance Variables:** none  
**Purpose:** Stream for reading from collections

**Key Methods:**
- Writing: `#nextPut:` (error)
- **Class Methods:** `#on:from:to:`

## Implementation Notes

### Syntax Conventions
- All assignments use `_` (underscore) instead of `:=` for compatibility with the parser
- Method selectors follow standard Smalltalk conventions
- Primitive methods are marked with `<primitive: number>` tags

### Key Features
- Complete Collection hierarchy with proper inheritance
- Hash-based Set implementation with collision handling
- Efficient SortedCollection with quicksort algorithm
- Comprehensive Stream classes for I/O operations
- Full Boolean and Character support
- Arithmetic progression support via Interval

### System Integration
- All classes integrate properly with the Smalltalk-80 interpreter
- Primitive methods are implemented for performance-critical operations
- Error handling follows Smalltalk conventions
- Memory management compatible with the object memory system

## Testing Status
- ✅ All 34 classes load successfully
- ✅ 177 method chunks parsed without errors
- ✅ Inheritance hierarchy resolves correctly
- ✅ System boots and displays class summary
- ✅ Dictionary and IdentityDictionary functionality available

---

*This documentation is maintained alongside the StandardLibrary.st file and reflects the current state of the Smalltalk-80 implementation.*
