# Migration Task List: From C# Class Management to Smalltalk

## Overview
Move class creation and management from C# code into Smalltalk itself, keeping only the absolute minimum bootstrap in C#.

## Phase 1: Minimal Bootstrap in C# ✅ = Complete, 🔄 = In Progress, ⏳ = Pending

### 1.1 Identify Absolute Minimum Bootstrap Classes ⏳
**Goal**: Determine the bare minimum classes needed to bootstrap the system.

**Required Bootstrap Classes** (must exist before we can execute ANY Smalltalk code):
- [ ] `Object` - Root of all classes
- [ ] `UndefinedObject` - For nil
- [ ] `Class` - To create other classes  
- [ ] `Metaclass` - For class-side behavior
- [ ] `Behavior` - Superclass of Class/Metaclass
- [ ] `ClassDescription` - Superclass of Class/Metaclass
- [ ] `Boolean`, `True`, `False` - For conditionals
- [ ] `Symbol` - For selectors and class names
- [ ] `String` - For source code
- [ ] `Array` - For literal arrays in methods
- [ ] `Association` - For variable bindings
- [ ] `Dictionary` - For method dictionaries
- [ ] `SystemDictionary` - For the Smalltalk global
- [ ] `CompiledMethod` - To hold compiled methods
- [ ] `SmallInteger` - For basic arithmetic
- [ ] `BlockContext` - For blocks (needed by conditionals)

### 1.2 Create Minimal C# Bootstrap ⏳
- [ ] Create bootstrap loader that only creates the above classes
- [ ] Ensure each bootstrap class has minimal structure:
  - Instance variables
  - Metaclass relationship
  - Superclass chain
  - Empty method dictionary
- [ ] Register bootstrap classes in Smalltalk dictionary

### 1.3 Load Bootstrap Methods ⏳
- [ ] Load essential methods for bootstrap classes from Smalltalk source:
  - `Object>>class`
  - `Object>>isNil`, `Object>>notNil`
  - `Object>>==`, `Object>>=`
  - `Class>>new`, `Class>>new:`
  - `Class>>subclass:instanceVariableNames:...`
  - `Behavior>>new`, `Behavior>>basicNew`
  - `Boolean>>ifTrue:`, `Boolean>>ifFalse:`, etc.
  - `Dictionary>>at:`, `Dictionary>>at:put:`
  - `Symbol>>asSymbol`, `String>>asSymbol`

## Phase 2: Implement Class Creation in Smalltalk

### 2.1 Verify Class Creation Methods ⏳
- [ ] Ensure `Class>>subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:` exists
- [ ] Ensure `Class>>variableSubclass:instanceVariableNames:...` exists
- [ ] Ensure `Class>>variableByteSubclass:instanceVariableNames:...` exists
- [ ] Verify `Class class>>name:inEnvironment:subclassOf:...` implementation
- [ ] Test that these methods properly:
  - Create new class object
  - Set up metaclass
  - Register in Smalltalk dictionary: `Smalltalk at: className put: newClass`
  - Set up instance/class variables
  - Update superclass's subclasses list

### 2.2 Modify Parser/Compiler ⏳
**Current Behavior**: Parser treats class definitions as special syntax and creates ClassDefinitionNode
**Target Behavior**: Parser treats class definitions as message sends

- [ ] Modify grammar/parser to recognize class definition as a message expression:
  ```smalltalk
  Object subclass: #MyClass instanceVariableNames: '' ...
  ```
  Should parse as: Send `subclass:instanceVariableNames:...` to `Object` with arguments

- [ ] Alternative (simpler): Keep current parsing but change execution:
  - [ ] In SystemImageLoader, instead of calling `SystemDictionary.DefineClass()`
  - [ ] Generate and execute Smalltalk expression:
    ```csharp
    var expression = $"{superclass} subclass: #{className} instanceVariableNames: '{instVars}' ...";
    vm.EvaluateSource(expression);
    ```

### 2.3 Implement Primitive Support ⏳
Some primitives may be needed for class creation:
- [ ] Primitive for basic object allocation (already exists as #70, #71)
- [ ] Primitive for setting class pointer (may need to add)
- [ ] Primitive for manipulating method dictionary (optional, can be done in Smalltalk)

## Phase 3: Modify File Loading Process

### 3.1 Two-Phase Loading Strategy ⏳
Since Smalltalk can have forward references, we need a strategy:

**Option A: Three-Pass Loading** (Recommended)
1. [ ] Pass 1: Load bootstrap classes only (from C#)
2. [ ] Pass 2: Execute all class definitions as Smalltalk expressions
3. [ ] Pass 3: Load all methods

**Option B: Lazy Class Creation**
1. [ ] Create stub classes on first reference
2. [ ] Fill in details when actual definition is encountered
3. [ ] Fix up forward references at end

### 3.2 Modify SystemImageLoader ⏳
```csharp
public static void LoadSystemImage(string path)
{
    // Phase 1: Bootstrap
    BootstrapMinimalClasses();
    
    // Phase 2: Parse file
    var fileAst = FileInReader.Parse(path);
    
    // Phase 3: Execute class definitions
    foreach (var chunk in fileAst.Chunks)
    {
        if (chunk is ClassDefinitionNode classDef)
        {
            // Generate Smalltalk expression
            var expr = GenerateClassCreationExpression(classDef);
            // Execute in VM
            vm.EvaluateSource(expr);
        }
    }
    
    // Phase 4: Load methods
    foreach (var chunk in fileAst.Chunks)
    {
        if (chunk is MethodDefinitionNode methodDef)
        {
            LoadMethod(methodDef);
        }
    }
}
```

### 3.3 Handle Special Cases ⏳
- [ ] Pool dictionaries: Ensure they're created as globals first
- [ ] Class variables: Ensure they're properly initialized
- [ ] Category organization: Implement SystemOrganization in Smalltalk

## Phase 4: Testing and Validation

### 4.1 Create Test Cases ⏳
- [ ] Test creating a simple class:
  ```smalltalk
  Object subclass: #TestClass
      instanceVariableNames: 'x y'
      classVariableNames: 'SharedVar'
      poolDictionaries: ''
      category: 'Tests'
  ```
  
- [ ] Verify class is in Smalltalk:
  ```smalltalk
  (Smalltalk at: #TestClass) notNil
  ```

- [ ] Test method addition works:
  ```smalltalk
  !TestClass methodsFor: 'accessing'!
  x
      ^x
  ! !
  ```

- [ ] Test class can be instantiated:
  ```smalltalk
  TestClass new
  ```

### 4.2 Validate StandardLibrary Loading ⏳
- [ ] Ensure StandardLibrary.st loads completely
- [ ] All classes registered in Smalltalk
- [ ] All methods compiled and installed
- [ ] Class variables initialized properly

### 4.3 Performance Testing ⏳
- [ ] Compare load times: C# direct vs Smalltalk execution
- [ ] Profile to identify bottlenecks
- [ ] Optimize if necessary (maybe cache compiled expressions)

## Phase 5: Cleanup and Documentation

### 5.1 Remove Obsolete C# Code ⏳
- [ ] Remove `SystemDictionary.DefineClass()` method
- [ ] Remove direct class manipulation from C#
- [ ] Keep only primitive support and VM operations

### 5.2 Update Documentation ⏳
- [ ] Document the bootstrap process
- [ ] Explain the class creation flow
- [ ] Update CLAUDE.md with new architecture

### 5.3 Create Migration Guide ⏳
- [ ] Document what changed
- [ ] Explain benefits of new approach
- [ ] Provide troubleshooting guide

## Implementation Order

**Recommended Sequence**:
1. Start with Phase 1.1-1.2: Create minimal bootstrap
2. Test bootstrap can execute simple expressions
3. Implement Phase 2.2 (Alternative approach first - simpler)
4. Test with a single class definition
5. Implement Phase 3.2: Modify loader
6. Test with full StandardLibrary
7. Clean up and optimize

## Benefits of This Migration

1. **True Smalltalk System**: Classes manage themselves
2. **Simpler C# Code**: Less duplication of Smalltalk logic
3. **More Flexible**: Can modify class creation in Smalltalk
4. **Better Debugging**: Class creation happens in Smalltalk where it can be traced
5. **Proper Global Management**: Everything goes through Smalltalk dictionary

## Risks and Mitigation

| Risk | Mitigation |
|------|------------|
| Bootstrap failures | Keep current system as fallback |
| Performance degradation | Profile and optimize critical paths |
| Forward references | Implement proper multi-pass loading |
| Debugging difficulty | Add extensive logging during transition |

## Success Criteria

- [ ] All StandardLibrary classes load successfully
- [ ] `Smalltalk at: #ClassName` returns the class for all classes
- [ ] Class creation through Smalltalk code works
- [ ] No direct class creation in C# (except bootstrap)
- [ ] Tests pass
- [ ] Performance acceptable (< 2x slower than current)