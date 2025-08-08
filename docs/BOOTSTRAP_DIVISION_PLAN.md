# Bootstrap Division Plan

## The Division Point

We need to load classes in THREE phases:

### Phase 1: Core Bootstrap (Via C# - MinimalBootstrap.cs)
**Already implemented** - These are the absolute minimum to execute ANY Smalltalk code:
- Object, UndefinedObject
- Behavior, ClassDescription, Class, Metaclass  
- Boolean, True, False
- SmallInteger, Symbol, String, Array
- BlockContext
- Collection, Set, Dictionary, SystemDictionary, Association

### Phase 2: Class Creation Infrastructure (Via C# - Load from StandardLibrary)
**Must be loaded BEFORE we can create classes via Smalltalk**:

From StandardLibrary.st (already present):
- Character (line in StandardLibrary)
- LookupKey (line in StandardLibrary)
- IdentityDictionary (line in StandardLibrary)
- OrderedCollection (line in StandardLibrary)
- Stream, PositionableStream, WriteStream (lines in StandardLibrary)

Missing from StandardLibrary (need to add or stub):
- **MethodDictionary** - Can be stubbed as IdentityDictionary subclass
- **ClassOrganizer** - Can be stubbed with minimal functionality
- **SystemOrganizer** - Can be stubbed as global variable
- **Scanner** - Can be stubbed with basic parseVariableNames method
- **Undeclared** - Can be stubbed as empty Dictionary

### Phase 3: Everything Else (Via Smalltalk class creation)
Once Phase 1 & 2 are loaded, ALL remaining classes can be created via:
```smalltalk
SuperClass subclass: #NewClass
    instanceVariableNames: '...'
    classVariableNames: '...'
    poolDictionaries: ''
    category: '...'
```

## Implementation Strategy

### Step 1: Add Stub Classes
Create minimal stub implementations in `ClassCreationStubs.st`:
```smalltalk
IdentityDictionary subclass: #MethodDictionary
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'!

Object subclass: #ClassOrganizer
    instanceVariableNames: 'categories'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Support'!

Object subclass: #SystemOrganizer
    instanceVariableNames: 'categories'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Support'!

Object subclass: #Scanner
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'!
```

### Step 2: Modified Loading Sequence
```csharp
public VirtualMachine(bool useSmalltalklassCreation = false)
{
    if (useSmalltalkClassCreation)
    {
        // Phase 1: Core bootstrap
        MinimalBootstrap.CreateEssentialClasses();
        
        // Phase 2: Load infrastructure classes from StandardLibrary
        LoadInfrastructureClasses(); // Character through WriteStream
        LoadClassCreationStubs();     // MethodDictionary, ClassOrganizer, etc.
        
        // Initialize globals
        SystemDictionary.InitializeGlobals();
        CreateGlobalStubs(); // Undeclared, SystemOrganization
        
        // Phase 3: Load remaining classes via Smalltalk
        LoadRemainingClassesViaSmalltalk();
    }
    else
    {
        // Current approach - load everything via C#
        LoadStandardLibraryClasses();
    }
}
```

### Step 3: Test Division Point
Create test that:
1. Loads Phase 1 & 2 via C#
2. Creates a new class via Smalltalk:
```smalltalk
Object subclass: #TestClass
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''  
    category: 'Test'
```
3. Verifies class is registered in Smalltalk dictionary
4. Verifies class can be instantiated

## Classes Count

- **Phase 1**: ~27 classes (core bootstrap)
- **Phase 2**: ~15 additional classes (infrastructure)
- **Total before switching**: ~42 classes
- **Remaining**: ~200+ classes loaded via Smalltalk

## Benefits

1. **Minimal C# involvement** - Only 42 classes via C#
2. **Real Smalltalk class creation** - For majority of classes
3. **Self-hosting** - System can modify itself
4. **Testable division** - Clear boundary between phases

## Next Steps

1. Create `ClassCreationStubs.st` with minimal stubs
2. Modify loader to load Phase 2 classes
3. Test class creation works after Phase 2
4. Load rest of StandardLibrary via Smalltalk