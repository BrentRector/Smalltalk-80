# Detailed Bootstrap Plan Using Only Primitives

## Overview
Bootstrap must create real Smalltalk objects using only VM primitives. The process must carefully build up from nothing to a working class system with the global Smalltalk dictionary.

## Phase 0: Understanding the Structure

### Class Instance Variables (from Smalltalk-80)
```
Behavior: superclass, methodDict, format
ClassDescription (extends Behavior): instanceVariables, organization  
Class (extends ClassDescription): name, classPool, sharedPools
Metaclass (extends ClassDescription): thisClass
```

### Field Indices (0-based)
```
Behavior:
  [0] superclass
  [1] methodDict  
  [2] format

ClassDescription adds:
  [3] instanceVariables
  [4] organization

Class adds:
  [5] name
  [6] classPool     <- Dictionary for class variables!
  [7] sharedPools

Metaclass adds (after ClassDescription):
  [5] thisClass
```

## Phase 1: Create nil, true, false

These are needed everywhere as field values.

```csharp
// Step 1.1: Create nil (instance of UndefinedObject)
nil = Primitive71(null, 0)  // new: 0 - no instance variables

// Step 1.2: Create true (instance of True)  
true = Primitive71(null, 0)  // new: 0

// Step 1.3: Create false (instance of False)
false = Primitive71(null, 0)  // new: 0
```

## Phase 2: Create Object Class and Its Metaclass

This is the most delicate part - we need Object class and Object's metaclass to exist before we can set their class pointers.

```csharp
// Step 2.1: Allocate Object class (has 8 fields as a Class instance)
objectClass = Primitive71(null, 8)  // new: 8

// Step 2.2: Allocate Object's metaclass (has 6 fields as a Metaclass instance)
objectMetaclass = Primitive71(null, 6)  // new: 6

// Step 2.3: Set Object class's class pointer to its metaclass
objectClass.classPointer = objectMetaclass

// Step 2.4: Initialize Object class fields
Primitive61(objectClass, 1, nil)           // [0] superclass = nil (Object has no superclass)
Primitive61(objectClass, 2, nil)           // [1] methodDict = nil (will create Dictionary later)
Primitive61(objectClass, 3, 0)             // [2] format = 0
Primitive61(objectClass, 4, nil)           // [3] instanceVariables = nil (will be empty array)
Primitive61(objectClass, 5, nil)           // [4] organization = nil
Primitive61(objectClass, 6, symbolObject)  // [5] name = #Object (need Symbol)
Primitive61(objectClass, 7, nil)           // [6] classPool = nil (will create Dictionary)
Primitive61(objectClass, 8, nil)           // [7] sharedPools = nil

// Step 2.5: Initialize Object metaclass fields
Primitive61(objectMetaclass, 1, nil)       // [0] superclass = nil (will be Class later)
Primitive61(objectMetaclass, 2, nil)       // [1] methodDict = nil
Primitive61(objectMetaclass, 3, 0)         // [2] format = 0
Primitive61(objectMetaclass, 4, nil)       // [3] instanceVariables = nil
Primitive61(objectMetaclass, 5, nil)       // [4] organization = nil
Primitive61(objectMetaclass, 6, objectClass) // [5] thisClass = Object
```

## Phase 3: Create Behavior, ClassDescription, Class, Metaclass

We need these before we can properly set metaclass relationships.

```csharp
// Step 3.1: Create Behavior class and metaclass
behaviorClass = Primitive71(null, 8)      // new: 8
behaviorMetaclass = Primitive71(null, 6)  // new: 6
behaviorClass.classPointer = behaviorMetaclass

// Step 3.2: Create ClassDescription class and metaclass
classDescriptionClass = Primitive71(null, 8)      // new: 8
classDescriptionMetaclass = Primitive71(null, 6)  // new: 6
classDescriptionClass.classPointer = classDescriptionMetaclass

// Step 3.3: Create Class class and metaclass
classClass = Primitive71(null, 8)      // new: 8
classMetaclass = Primitive71(null, 6)  // new: 6
classClass.classPointer = classMetaclass

// Step 3.4: Create Metaclass class and metaclass
metaclassClass = Primitive71(null, 8)      // new: 8
metaclassMetaclass = Primitive71(null, 6)  // new: 6
metaclassClass.classPointer = metaclassMetaclass

// Step 3.5: Fix up metaclass class pointers (all metaclasses are instances of Metaclass)
objectMetaclass.classPointer = metaclassClass
behaviorMetaclass.classPointer = metaclassClass
classDescriptionMetaclass.classPointer = metaclassClass
classMetaclass.classPointer = metaclassClass
metaclassMetaclass.classPointer = metaclassClass  // Metaclass class is instance of Metaclass!
```

## Phase 4: Set Superclass Hierarchy

```csharp
// Step 4.1: Class hierarchy
Primitive61(objectClass, 1, nil)                      // Object superclass = nil
Primitive61(behaviorClass, 1, objectClass)            // Behavior superclass = Object
Primitive61(classDescriptionClass, 1, behaviorClass)  // ClassDescription superclass = Behavior
Primitive61(classClass, 1, classDescriptionClass)     // Class superclass = ClassDescription
Primitive61(metaclassClass, 1, classDescriptionClass) // Metaclass superclass = ClassDescription

// Step 4.2: Metaclass hierarchy (parallels class hierarchy, but Object class superclass is Class)
Primitive61(objectMetaclass, 1, classClass)                   // Object class superclass = Class
Primitive61(behaviorMetaclass, 1, objectMetaclass)            // Behavior class superclass = Object class
Primitive61(classDescriptionMetaclass, 1, behaviorMetaclass)  // ClassDescription class superclass = Behavior class
Primitive61(classMetaclass, 1, classDescriptionMetaclass)     // Class class superclass = ClassDescription class
Primitive61(metaclassMetaclass, 1, classDescriptionMetaclass) // Metaclass class superclass = ClassDescription class

// Step 4.3: Set thisClass for all metaclasses
Primitive61(objectMetaclass, 6, objectClass)               // Object class thisClass = Object
Primitive61(behaviorMetaclass, 6, behaviorClass)           // Behavior class thisClass = Behavior
Primitive61(classDescriptionMetaclass, 6, classDescriptionClass)
Primitive61(classMetaclass, 6, classClass)
Primitive61(metaclassMetaclass, 6, metaclassClass)
```

## Phase 5: Create Collection Classes for Dictionary

We need Dictionary to create classPool and the global Smalltalk dictionary.

```csharp
// Step 5.1: Create Symbol class (needed for dictionary keys)
symbolClass = Primitive71(null, 8)
symbolMetaclass = Primitive71(null, 6)
symbolClass.classPointer = symbolMetaclass
symbolMetaclass.classPointer = metaclassClass
Primitive61(symbolClass, 1, objectClass)  // Symbol superclass = Object (simplified)

// Step 5.2: Create Collection class
collectionClass = Primitive71(null, 8)
collectionMetaclass = Primitive71(null, 6)
collectionClass.classPointer = collectionMetaclass
collectionMetaclass.classPointer = metaclassClass
Primitive61(collectionClass, 1, objectClass)  // Collection superclass = Object

// Step 5.3: Create Set class (Dictionary's superclass)
// Set has instance variables: tally, array
setClass = Primitive71(null, 8)
setMetaclass = Primitive71(null, 6)
setClass.classPointer = setMetaclass
setMetaclass.classPointer = metaclassClass
Primitive61(setClass, 1, collectionClass)  // Set superclass = Collection

// Step 5.4: Create Dictionary class
dictionaryClass = Primitive71(null, 8)
dictionaryMetaclass = Primitive71(null, 6)
dictionaryClass.classPointer = dictionaryMetaclass
dictionaryMetaclass.classPointer = metaclassClass
Primitive61(dictionaryClass, 1, setClass)  // Dictionary superclass = Set

// Step 5.5: Create Association class (for key-value pairs)
// Association has: key, value
associationClass = Primitive71(null, 8)
associationMetaclass = Primitive71(null, 6)
associationClass.classPointer = associationMetaclass
associationMetaclass.classPointer = metaclassClass
Primitive61(associationClass, 1, objectClass)  // Association superclass = Object

// Step 5.6: Create Array class (for Dictionary's internal array)
arrayClass = Primitive71(null, 8)
arrayMetaclass = Primitive71(null, 6)
arrayClass.classPointer = arrayMetaclass
arrayMetaclass.classPointer = metaclassClass
Primitive61(arrayClass, 1, objectClass)  // Array superclass = Object
```

## Phase 6: Create Method Dictionaries and Class Pools

Now we can create real Dictionary objects.

```csharp
// Step 6.1: Helper function to create empty Dictionary
CreateEmptyDictionary(dictionaryClass):
    dict = Primitive71(dictionaryClass, 2)     // new: 2 (tally, array)
    Primitive61(dict, 1, 0)                    // tally = 0
    array = Primitive71(arrayClass, 11)        // array of size 11
    for i = 1 to 11:
        Primitive61(array, i, nil)             // fill with nils
    Primitive61(dict, 2, array)                // set array
    return dict

// Step 6.2: Create method dictionaries for all classes
objectMethodDict = CreateEmptyDictionary(dictionaryClass)
Primitive61(objectClass, 2, objectMethodDict)           // Object methodDict

behaviorMethodDict = CreateEmptyDictionary(dictionaryClass)
Primitive61(behaviorClass, 2, behaviorMethodDict)       // Behavior methodDict

// ... repeat for all classes ...

// Step 6.3: Create classPools for all classes
objectClassPool = CreateEmptyDictionary(dictionaryClass)
Primitive61(objectClass, 7, objectClassPool)            // Object classPool

// Add Object's class variables to classPool
depFieldsAssoc = Primitive71(associationClass, 2)       // new: 2
Primitive61(depFieldsAssoc, 1, Symbol("DependentsFields"))
Primitive61(depFieldsAssoc, 2, nil)
// Add to dictionary (simplified - real implementation needs hashing)

errorRecAssoc = Primitive71(associationClass, 2)        // new: 2
Primitive61(errorRecAssoc, 1, Symbol("ErrorRecursion"))
Primitive61(errorRecAssoc, 2, false)
// Add to dictionary
```

## Phase 7: Create SystemDictionary Class

```csharp
// Step 7.1: Create SystemDictionary class (subclass of Dictionary)
systemDictionaryClass = Primitive71(null, 8)
systemDictionaryMetaclass = Primitive71(null, 6)
systemDictionaryClass.classPointer = systemDictionaryMetaclass
systemDictionaryMetaclass.classPointer = metaclassClass
Primitive61(systemDictionaryClass, 1, dictionaryClass)  // SystemDictionary superclass = Dictionary
```

## Phase 8: Create Global Smalltalk Instance

```csharp
// Step 8.1: Create the Smalltalk global instance
smalltalk = Primitive71(systemDictionaryClass, 2)  // new: 2 (tally, array)
Primitive61(smalltalk, 1, 0)                       // tally = 0
bigArray = Primitive71(arrayClass, 101)            // bigger array for globals
for i = 1 to 101:
    Primitive61(bigArray, i, nil)
Primitive61(smalltalk, 2, bigArray)

// Step 8.2: Add all created classes to Smalltalk dictionary
AddToDictionary(smalltalk, Symbol("Object"), objectClass)
AddToDictionary(smalltalk, Symbol("Behavior"), behaviorClass)
AddToDictionary(smalltalk, Symbol("ClassDescription"), classDescriptionClass)
AddToDictionary(smalltalk, Symbol("Class"), classClass)
AddToDictionary(smalltalk, Symbol("Metaclass"), metaclassClass)
AddToDictionary(smalltalk, Symbol("Collection"), collectionClass)
AddToDictionary(smalltalk, Symbol("Set"), setClass)
AddToDictionary(smalltalk, Symbol("Dictionary"), dictionaryClass)
AddToDictionary(smalltalk, Symbol("SystemDictionary"), systemDictionaryClass)
AddToDictionary(smalltalk, Symbol("Association"), associationClass)
AddToDictionary(smalltalk, Symbol("Array"), arrayClass)
AddToDictionary(smalltalk, Symbol("Symbol"), symbolClass)

// Step 8.3: Add special objects
AddToDictionary(smalltalk, Symbol("nil"), nil)
AddToDictionary(smalltalk, Symbol("true"), true)
AddToDictionary(smalltalk, Symbol("false"), false)
AddToDictionary(smalltalk, Symbol("Smalltalk"), smalltalk)  // Smalltalk references itself!
```

## Phase 9: Complete Remaining Bootstrap Classes

```csharp
// Add SmallInteger, String, CompiledMethod, BlockContext, etc.
// Each follows the same pattern:
// 1. Create class and metaclass
// 2. Set class pointers
// 3. Set superclass relationships
// 4. Create method dictionary and classPool
// 5. Add to Smalltalk global dictionary
```

## Critical Points

1. **Order matters**: Must create Object and core metaclass machinery first
2. **Circular references**: Metaclass class is an instance of Metaclass
3. **Smalltalk dictionary**: Must be created after Dictionary class exists
4. **All classes go in Smalltalk**: Every class must be registered in the global Smalltalk dictionary
5. **classPools are real Dictionaries**: Field [6] of every Class object is a Dictionary

## Primitives Used

- **Primitive 71**: `new:` - Allocate object with N fields
- **Primitive 61**: `at:put:` - Set field at index (1-based)
- **Primitive 60**: `at:` - Get field at index (1-based)
- Class pointer setting (internal VM operation)

## Result

After bootstrap:
- All classes are real Smalltalk objects
- All classPools are real Dictionary objects
- Global Smalltalk dictionary contains all classes
- Class variables can be stored in classPools
- System ready for Smalltalk code execution