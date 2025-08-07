# Complete Fix: Bootstrap with Real Smalltalk Objects

## The Problem

The current bootstrap creates C# shadow structures instead of real Smalltalk objects:
- Classes use C# `List<string>` for class variables (just names!)
- No real Smalltalk Dictionary for `classPool`
- Methods stored in C# `Dictionary<Symbol, CompiledMethod>`
- Class variables have nowhere to be stored

## The Solution: Use Only Primitives During Bootstrap

During bootstrap, only VM primitives are available. We must build everything using:

### Core Primitives Needed

1. **Primitive 70** - `new` - Create object with no fields
2. **Primitive 71** - `new:` - Create object with N fields  
3. **Primitive 60** - `at:` - Get field at index
4. **Primitive 61** - `at:put:` - Set field at index

### Bootstrap Process Using Primitives

```csharp
// Step 1: Allocate raw object for Class (8 fields)
var objectClass = Primitive71_New(null, 8);

// Step 2: Set up fields using primitive 61 (at:put:)
Primitive61_AtPut(objectClass, 1, superclass);      // Field 0: superclass
Primitive61_AtPut(objectClass, 2, methodDict);      // Field 1: methodDict
Primitive61_AtPut(objectClass, 3, format);          // Field 2: format
Primitive61_AtPut(objectClass, 4, instVars);        // Field 3: instanceVariables
Primitive61_AtPut(objectClass, 5, organization);    // Field 4: organization
Primitive61_AtPut(objectClass, 6, name);            // Field 5: name
Primitive61_AtPut(objectClass, 7, classPoolDict);   // Field 6: classPool (THE KEY!)
Primitive61_AtPut(objectClass, 8, sharedPools);     // Field 7: sharedPools
```

### Creating the classPool Dictionary

```csharp
// Create real Smalltalk Dictionary for classPool
var classPoolDict = Primitive71_New(dictionaryClass, 2);  // Dict has: tally, array
Primitive61_AtPut(classPoolDict, 1, zero);                // tally = 0
Primitive61_AtPut(classPoolDict, 2, emptyArray);          // array for entries

// Add class variables (e.g., for Object: DependentsFields, ErrorRecursion)
var association = Primitive71_New(associationClass, 2);   // key, value
Primitive61_AtPut(association, 1, Symbol("DependentsFields"));
Primitive61_AtPut(association, 2, nil);
// Add association to dictionary using Dictionary's primitive operations
```

## Key Insights

1. **Bootstrap can only use primitives** - No Smalltalk methods available yet

2. **Everything must be a real Smalltalk object** - No C# collections or structures

3. **classPool is at field index 6** in Class objects (0-based: index 6, 1-based: index 7)

4. **Dictionary structure** in Smalltalk:
   - Inherits from Set
   - Has `tally` (count) and `array` (storage)
   - Array contains Associations (key->value pairs)

## Implementation Steps

### Phase 1: Create Core Objects
```csharp
// Allocate using primitive 71
var nil = AllocateObject(0);
var objectClass = AllocateObject(8);
var dictionaryClass = AllocateObject(8);
var associationClass = AllocateObject(8);
```

### Phase 2: Set Up Class Hierarchy
```csharp
// Use primitive 61 to set superclass pointers
SetField(behaviorClass, SUPERCLASS_INDEX, objectClass);
SetField(classDescriptionClass, SUPERCLASS_INDEX, behaviorClass);
SetField(classClass, SUPERCLASS_INDEX, classDescriptionClass);
```

### Phase 3: Create classPools
```csharp
// For each class, create and set classPool Dictionary
var classPool = CreateEmptyDictionary();
SetField(classObject, CLASSPOOL_INDEX, classPool);
```

### Phase 4: Initialize Class Variables
```csharp
// Add class variables to classPool
AddToClassPool(objectClassPool, "DependentsFields", nil);
AddToClassPool(objectClassPool, "ErrorRecursion", nil);
```

## Compiler Fix Required

The compiler must also check classPool:

```csharp
// In AstMethodCompiler.LoadVariable()
if (compilingClass != null)
{
    // Get classPool from field 6 of the class
    var classPool = Primitive60_At(compilingClass, 7); // 1-based
    if (HasKey(classPool, variableName))
    {
        var association = GetAssociation(classPool, variableName);
        EmitPushLiteralVariable(association);
        return;
    }
}
```

## Benefits

1. **Real Smalltalk objects** - No C# shadows
2. **Class variables work** - Stored in real classPool Dictionary
3. **Proper object model** - Everything is a Smalltalk object
4. **Foundation for self-hosting** - After bootstrap, Smalltalk creates its own classes

## Testing

After this fix:
```smalltalk
Object initialize.         "Sets DependentsFields and ErrorRecursion in classPool"
Float initialize.          "Sets Pi, Halfpi, etc. in Float's classPool"
Float instVarNamed: #Pi.   "Returns 3.14159 from classPool"
```

## Summary

The bootstrap must:
1. Use ONLY primitives (70, 71, 60, 61)
2. Create REAL Smalltalk objects (no C# structures)
3. Create REAL Dictionary objects for classPool
4. Store classPool at field index 6 of Class objects
5. Compiler must check classPool for class variables

This creates a proper Smalltalk system where everything is a real Smalltalk object manipulated through primitives and eventually through Smalltalk methods.