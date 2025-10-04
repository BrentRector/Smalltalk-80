# Cold Start Bootstrap Architecture

## Overview

The cold start bootstrap creates the complete Smalltalk-80 class hierarchy (64 classes + 64 metaclasses = 128 total class objects) from compiler-generated layout data before creating any other instances.

**Key Principles:**
1. ALL classes and metaclasses are created BEFORE any other instances
2. ALL attributes come from compiler-generated `ColdStartData.cs` (no hardcoding except class names)
3. The bootstrap handles the circular nil dependency explicitly
4. Each class and its metaclass are created together (after bootstrap triangle)

## Bootstrap Classes

The following 6 classes form the bootstrap triangle and must be created first:

1. **Object** - Root of the class hierarchy
2. **UndefinedObject** - Class of the nil singleton
3. **Behavior** - Defines method lookup behavior
4. **ClassDescription** - Abstract superclass for Class and Metaclass
5. **Class** - Defines regular classes
6. **Metaclass** - Defines class-side behavior (metaclasses)

**Why These 6?**
- Object must exist first (root of hierarchy)
- UndefinedObject must exist early to create nil singleton for Object's superclass
- Behavior/ClassDescription provide the inheritance chain for Class/Metaclass
- Class and Metaclass form the circular dependency: Metaclass.class = Metaclass

## Multi-Pass Bootstrap Architecture

### Pass 1: Create Bootstrap Classes (6 classes)

Creates the 6 fundamental classes from compiler-generated layout data.

```csharp
// Bootstrap class names (hardcoded list - only thing hardcoded)
var bootstrapClassNames = new[]
{
    "Object",
    "UndefinedObject",  // Added to allow early nil creation
    "Behavior",
    "ClassDescription",
    "Class",
    "Metaclass"
};

// Create each bootstrap class using compiler layout data
foreach (var className in bootstrapClassNames)
{
    var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), className);

    // Get superclass from cache (Object's superclass is temporarily SObjectRef.Nil = 0)
    SObjectRef superclassRef = SObjectRef.Nil;
    if (!string.IsNullOrEmpty(metadata.SuperclassName) &&
        metadata.SuperclassName != "nil")
    {
        superclassRef = _createdClasses[metadata.SuperclassName];
    }

    // Determine format from compiler data
    var format = DetermineObjectFormat(metadata.SubclassKind, className);

    // Create class object
    var classRef = ObjectMemoryNew.CreateClassObject(
        className,
        superclassRef,
        metadata.InstanceVariableNames,
        format
    );

    _createdClasses[className] = classRef;
}
```

**State After Pass 1:**
- Object.superclass = SObjectRef.Nil (temporary placeholder, value=0)
- UndefinedObject.superclass = Object (real reference)
- Behavior.superclass = Object
- ClassDescription.superclass = Behavior
- Class.superclass = ClassDescription
- Metaclass.superclass = ClassDescription
- All stored in `_createdClasses` cache
- SystemDictionary.Instance does NOT exist yet

### Interlude: Create Nil Singleton and Fix Object.superclass

Before creating metaclasses, we must resolve the nil circular dependency:

```csharp
// 1. Get UndefinedObject class (created in Pass 1)
var undefinedObjectClass = _createdClasses["UndefinedObject"];

// 2. Create nil singleton instance
SystemDictionary.NilObject = ObjectMemoryNew.CreateInstance(
    undefinedObjectClass.AsObjectPointer(),
    0  // UndefinedObject has no instance variables
);

// 3. Fix up Object's superclass to point to real nil singleton
var objectClass = _createdClasses["Object"];
ObjectMemoryNew.SetInstanceVariable(
    objectClass.AsObjectPointer(),
    ClassLayout.Superclass,  // Superclass is instance variable 0
    SystemDictionary.NilObject
);
```

**Why This Works:**
- Object is created first but with temporary nil placeholder
- UndefinedObject is created second (as subclass of Object)
- Nil singleton is created as instance of UndefinedObject
- Object.superclass is fixed up to point to real nil singleton
- This happens during bootstrap, not as a separate fixup phase

**State After Interlude:**
- Object.superclass = NilObject (real nil singleton, instance of UndefinedObject)
- SystemDictionary.NilObject exists and is properly initialized

### Pass 2: Create Metaclasses for Bootstrap Classes

Each of the 6 bootstrap classes needs a metaclass (instance of Metaclass).

```csharp
foreach (var className in bootstrapClassNames)
{
    var classRef = _createdClasses[className];

    // Get metaclass metadata from compiler data
    var metaclassName = $"{className} class";
    var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), metaclassName);

    // Determine metaclass superclass
    // For "Object class", superclass is "Class"
    // For "Foo class", superclass is "Superclass(Foo) class"
    SObjectRef metaclassSuperclass;
    if (className == "Object")
    {
        metaclassSuperclass = _createdClasses["Class"];
    }
    else
    {
        var baseClass = _createdClasses[className];
        var baseClassSuperName = GetClassName(
            baseClass.GetInstanceVariable(ClassLayout.Superclass)
        );
        metaclassSuperclass = _createdClasses[$"{baseClassSuperName} class"];
    }

    // Create metaclass as instance of Metaclass class
    var metaclassRef = ObjectMemoryNew.CreateInstance(
        _createdClasses["Metaclass"].AsObjectPointer(),
        metadata.InstanceVariableNames.Length
    );

    // Set metaclass instance variables
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        ClassLayout.Superclass,
        metaclassSuperclass
    );
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        ClassLayout.MethodDictionary,
        CreateEmptyMethodDictionary()
    );
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        ClassLayout.Format,
        SObjectRef.FromSmallInteger((int)ObjectFormat.FixedPointer)
    );
    // ... set remaining instance variables from metadata

    _createdClasses[metaclassName] = metaclassRef;

    // Wire class <-> metaclass relationship
    ObjectMemoryNew.SetInstanceVariable(
        classRef.AsObjectPointer(),
        ClassLayout.Class,
        metaclassRef
    );
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        MetaclassLayout.ThisClass,
        classRef
    );
}
```

**State After Pass 2:**
- 6 classes + 6 metaclasses = 12 class objects exist
- Each class has its metaclass set
- Each metaclass has its thisClass set
- Metaclass.class is still uninitialized (will be fixed in Pass 3)

### Pass 3: Wire Circular Metaclass Relationship

The final piece of the bootstrap triangle:

```csharp
// Metaclass.class = Metaclass (circular!)
var metaclassClass = _createdClasses["Metaclass"];
var metaclassMetaclass = _createdClasses["Metaclass class"];

ObjectMemoryNew.SetInstanceVariable(
    metaclassMetaclass.AsObjectPointer(),
    ClassLayout.Class,
    metaclassMetaclass  // Points to itself!
);
```

**State After Pass 3:**
- Bootstrap triangle is complete
- Metaclass class's class is itself
- All fundamental circular relationships established
- Real nil singleton exists and Object.superclass points to it

### Pass 4: Create Remaining Classes and Metaclasses

Create the remaining 58 classes (64 total - 6 bootstrap = 58 remaining) along with their metaclasses.

```csharp
// Get all class metadata from ColdStartData
var allClassNames = GetAllClassNamesFromColdStartData();
var remainingClasses = allClassNames.Except(bootstrapClassNames);

foreach (var className in DependencyOrder(remainingClasses))
{
    // 1. Create the class
    var metadata = ReadClassMetadata(typeof(ColdStartData.Classes), className);

    // Superclass lookup uses real nil now (no placeholder)
    SObjectRef superclassRef;
    if (string.IsNullOrEmpty(metadata.SuperclassName) ||
        metadata.SuperclassName == "nil")
    {
        superclassRef = SystemDictionary.NilObject;  // Real nil!
    }
    else
    {
        superclassRef = _createdClasses[metadata.SuperclassName];
    }

    var format = DetermineObjectFormat(metadata.SubclassKind, className);
    var classRef = ObjectMemoryNew.CreateClassObject(
        className,
        superclassRef,
        metadata.InstanceVariableNames,
        format
    );
    _createdClasses[className] = classRef;

    // 2. Create its metaclass immediately
    var metaclassMetadata = ReadClassMetadata(
        typeof(ColdStartData.Classes),
        $"{className} class"
    );

    // Determine metaclass superclass: "Superclass(Foo) class"
    var baseClassSuperName = GetClassName(superclassRef);
    SObjectRef metaclassSuperclass;
    if (baseClassSuperName == "nil")
    {
        metaclassSuperclass = _createdClasses["Class"];
    }
    else
    {
        metaclassSuperclass = _createdClasses[$"{baseClassSuperName} class"];
    }

    var metaclassRef = _createdClasses["Metaclass"].CreateInstance(
        metaclassMetadata.InstanceVariableNames.Length
    );

    // Set metaclass attributes
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        ClassLayout.Superclass,
        metaclassSuperclass
    );
    // ... set remaining instance variables

    _createdClasses[$"{className} class"] = metaclassRef;

    // 3. Wire class <-> metaclass relationship
    ObjectMemoryNew.SetInstanceVariable(
        classRef.AsObjectPointer(),
        ClassLayout.Class,
        metaclassRef
    );
    ObjectMemoryNew.SetInstanceVariable(
        metaclassRef.AsObjectPointer(),
        MetaclassLayout.ThisClass,
        classRef
    );
}
```

**State After Pass 4:**
- ALL 64 classes exist
- ALL 64 metaclasses exist
- ALL class/metaclass relationships are wired
- Total: 128 class objects in memory
- All stored in `_createdClasses` cache
- SystemDictionary.Instance STILL does not exist

**Critical Point:** Pass 4 creates each class AND its metaclass together, wiring them immediately. This eliminates the need for separate "create all classes" then "create all metaclasses" then "wire them" passes.

## Phase 2: Post-Class-Creation Initialization

After ALL 128 class objects exist, we can create other instances.

### 2.1 Create SystemDictionary Instance

```csharp
// NOW we can create Dictionary instances
var dictionaryClass = _createdClasses["Dictionary"];

// Create SystemDictionary instance (sole source of truth)
SystemDictionary.Instance = dictionaryClass.CreateInstance(initialCapacity: 100);

// Register all classes into SystemDictionary
foreach (var (className, classRef) in _createdClasses)
{
    if (!className.EndsWith(" class"))  // Only register classes, not metaclasses
    {
        var nameSymbol = ObjectMemoryNew.InternSymbolBootstrap(className);
        ObjectMemoryNew.DictionaryAtPut(
            SystemDictionary.Instance,
            nameSymbol,
            classRef
        );
    }
}
```

### 2.2 Create Singleton Objects

```csharp
// NilObject already created in bootstrap interlude

// Create True and False singletons
var trueClass = _createdClasses["True"];
var falseClass = _createdClasses["False"];

SystemDictionary.TrueObject = trueClass.CreateInstance();
SystemDictionary.FalseObject = falseClass.CreateInstance();
```

### 2.3 Create Symbol Table (USTable)

```csharp
// Create USTable for Symbol class
SymbolTableBuilder.CreateAndInstallUSTable();

// Migrate bootstrap symbols to USTable
ObjectMemoryNew.MigrateBootstrapSymbolsToUSTable();

// Rebuild SystemDictionary with USTable symbols
// (complex re-registration logic as shown in SystemDictionary.cs lines 204-248)
```

## Key Architectural Points

### 1. No Instance Creation During Class Creation

**Rule:** The first 128 objects created are ALL class objects (classes and metaclasses).

**Only Exceptions:**
- Metaclasses are instances of Metaclass (special case - they are class objects)
- Empty method dictionaries created during class creation (infrastructure)

**First Non-Class Instance:** The nil singleton (created in bootstrap interlude)

### 2. Compiler Data is the Sole Source of Truth

**Hardcoded:**
- Only the 6 bootstrap class names: `["Object", "UndefinedObject", "Behavior", "ClassDescription", "Class", "Metaclass"]`

**From Compiler Data (ColdStartData.cs):**
- SuperclassName for every class
- InstanceVariableNames for every class
- SubclassKind for every class (determines ObjectFormat)
- ClassVariableNames for every class
- All 64 classes discovered via reflection on `ColdStartData.Classes`

### 3. Nil Singleton Bootstrap Strategy

**Problem:** Object needs superclass = nil, but nil is an instance of UndefinedObject which is a subclass of Object.

**Solution:** Four-step process during bootstrap:
1. Create Object with temporary nil placeholder (SObjectRef.Nil, value=0)
2. Create UndefinedObject (can use Object as superclass)
3. Create nil singleton as instance of UndefinedObject
4. Fix up Object.superclass to point to real nil singleton

**Why Not a Later Fixup Phase?** By adding UndefinedObject to the bootstrap class list, we can create and fix nil during the bootstrap itself, eliminating the need for a separate post-bootstrap fixup phase.

### 4. Metaclass Creation Strategy

**After Bootstrap Triangle (Passes 1-3):**
- Create each remaining class AND its metaclass together
- Wire class <-> metaclass relationship immediately
- No separate "create all classes then all metaclasses" phases

**Metaclass Superclass Chain:**
- `Foo class` inherits from `Superclass(Foo) class`
- This mirrors the instance-side inheritance: `Foo` inherits from `Superclass(Foo)`
- Special case: `Object class` inherits from `Class` (no "nil class")

### 5. SystemDictionary Timing

**Wrong (Current Implementation):**
```csharp
// In CreateClass():
if (metadata.ClassName == "Dictionary")
{
    SystemDictionary.CreateInstance(...);  // TOO EARLY!
}
```

**Correct:**
```csharp
// In ColdStartClassCreator.CreateAllClasses():
// Pass 1-4: Create all 128 class objects
// Return to SystemDictionary.Initialize()

// In SystemDictionary.Initialize():
ColdStart.ColdStartClassCreator.CreateAllClasses();  // Returns after Pass 4
// NOW create SystemDictionary.Instance
var dictionaryClass = LookupClassFromCache("Dictionary");
Instance = dictionaryClass.CreateInstance(100);
```

## Implementation Status

**Current State:**
- Class creation exists but creates SystemDictionary.Instance too early
- Metaclasses not created at all
- Nil singleton created in Phase 2, but Object uses permanent SObjectRef.Nil placeholder
- No multi-pass architecture

**Required Changes:**
1. Refactor `ColdStartClassCreator.CreateAllClasses()` to implement 4-pass architecture
2. Add metaclass creation logic (currently missing entirely)
3. Add nil singleton creation interlude between Pass 1 and Pass 2
4. Move SystemDictionary.Instance creation from `CreateClass()` to `SystemDictionary.Initialize()`
5. Update all superclass lookups to use `_createdClasses` cache only (never SystemDictionary)

## Verification

After implementing the multi-pass bootstrap, verify:

1. **Class Count:** 64 classes created
2. **Metaclass Count:** 64 metaclasses created
3. **Total Class Objects:** 128
4. **Nil Correctness:** `Object.superclass == SystemDictionary.NilObject` (not SObjectRef.Nil)
5. **Metaclass Circular:** `Metaclass class.class == Metaclass class`
6. **SystemDictionary Timing:** Instance created AFTER all 128 class objects exist
7. **No Premature Instances:** First 128 objects are all class objects (except empty method dictionaries and nil)

## References

- [ColdStartClassCreator.cs](../Smalltalk80.Interpreter/ColdStart/ColdStartClassCreator.cs) - Current implementation (needs refactoring)
- [SystemDictionary.cs](../Smalltalk80.Interpreter/Core/Objects/SystemDictionary.cs) - Main initialization (lines 172-256)
- [ColdStartData.cs](../Smalltalk80.Interpreter/Generated/ColdStartData.cs) - Compiler-generated layout data
- [CLAUDE.md](CLAUDE.md) - System image regeneration process
