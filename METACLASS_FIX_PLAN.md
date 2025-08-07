# Metaclass Implementation Fix Plan (Classic Smalltalk-80)

## Current Issues

1. **ClassMethodDictionary shouldn't exist** - Class-side methods belong in the metaclass's regular MethodDictionary
2. **Metaclass hierarchy not parallel** - Metaclasses don't form a proper parallel hierarchy
3. **Metaclass is both a class and the class of all metaclasses** - This circular relationship needs proper handling
4. **Missing class variables on Object** - Object needs DependentsFields and ErrorRecursion class variables

## Required Changes

### 1. Fix SmalltalkClass.cs
- Remove `ClassMethodDictionary` property
- Remove `LookupClassMethod` method  
- Class-side methods will be looked up via: `class.Metaclass.Lookup(selector)`

### 2. Fix Object as Root (Classic Smalltalk-80)
In SystemDictionary.cs `InitializeMetaclassSystem()`:
```csharp
// Create Object as the root (classic Smalltalk-80)
var objectClass = new SmalltalkClass { 
    Superclass = null  // nil superclass makes Object the root
};
objectClass.ClassVariables.Add("DependentsFields");
objectClass.ClassVariables.Add("ErrorRecursion");
classes["Object"] = objectClass;
```

### 3. Fix Metaclass Hierarchy
The metaclass hierarchy must be parallel to the class hierarchy:
- `SmallInteger class` inherits from `Integer class`
- `Integer class` inherits from `Number class`
- etc.

In `CreateMetaclassFor()`:
```csharp
private static SmalltalkClass CreateMetaclassFor(SmalltalkClass cls, string className)
{
    var metaclass = new SmalltalkClass();
    
    // The metaclass inherits from the superclass's metaclass (parallel hierarchy)
    if (cls.Superclass != null && cls.Superclass.Metaclass != null)
    {
        metaclass.Superclass = cls.Superclass.Metaclass;
    }
    else if (className != "Object")
    {
        // Classes without superclass (except Object) get Class as their metaclass's superclass
        if (TryGetClass("Class", out var classClass))
            metaclass.Superclass = classClass;
    }
    
    // All metaclasses are instances of Metaclass
    if (TryGetClass("Metaclass", out var metaclassClass))
        metaclass.ClassPointer = metaclassClass;
    
    return metaclass;
}
```

### 4. Fix Bootstrap Sequence (Classic Smalltalk-80)
```csharp
public static void InitializeMetaclassSystem()
{
    // 1. Create Object (root of instance hierarchy in classic Smalltalk-80)
    var objectClass = new SmalltalkClass { Superclass = null };
    objectClass.ClassVariables.Add("DependentsFields");
    objectClass.ClassVariables.Add("ErrorRecursion");
    classes["Object"] = objectClass;
    
    // 2. Create Behavior, ClassDescription, Class, Metaclass
    var behavior = new SmalltalkClass { Superclass = objectClass };
    classes["Behavior"] = behavior;
    
    var classDescription = new SmalltalkClass { Superclass = behavior };
    classes["ClassDescription"] = classDescription;
    
    var classClass = new SmalltalkClass { Superclass = classDescription };
    classes["Class"] = classClass;
    
    var metaclassClass = new SmalltalkClass { Superclass = classDescription };
    classes["Metaclass"] = metaclassClass;
    
    // 3. Create metaclasses for each class
    // Object class -> Class (Object's metaclass inherits from Class)
    var objectMeta = new SmalltalkClass { 
        Superclass = classClass,
        ClassPointer = metaclassClass 
    };
    objectClass.Metaclass = objectMeta;
    objectClass.ClassPointer = objectMeta;
    
    // ... continue for other classes
    
    // 5. Special case: Metaclass class -> Metaclass class (circular)
    var metaclassMeta = new SmalltalkClass {
        Superclass = classDescription.Metaclass,
        ClassPointer = metaclassClass
    };
    metaclassClass.Metaclass = metaclassMeta;
    metaclassClass.ClassPointer = metaclassMeta;
    
    // Metaclass class is an instance of Metaclass (itself)
    metaclassMeta.ClassPointer = metaclassClass;
}
```

### 5. Fix Method Lookup for Class-side
In Interpreter.cs, when sending a message to a class object:
```csharp
if (receiver is SmalltalkClass classReceiver)
{
    // Look up in the metaclass for class-side methods
    var method = classReceiver.Metaclass?.Lookup(selector);
    if (method != null)
    {
        // Execute class-side method
    }
}
```

### 6. Handle "ClassName class" in Parser
The parser needs to recognize when loading methods for "ClassName class" and set `IsClassSide = true` in the MethodHeaderNode.

### 7. Instance Variables in Metaclasses
Each metaclass should have a `thisClass` instance variable pointing to its sole instance (the class).

## Testing Strategy

1. Verify ProtoObject is the root with nil superclass
2. Verify Object inherits from ProtoObject  
3. Verify parallel metaclass hierarchy
4. Test class-side method lookup via metaclass
5. Test that `SmallInteger class superclass == Integer class`
6. Test that all metaclasses are instances of Metaclass
7. Test circular relationship: `Metaclass class class == Metaclass`

## Expected Results

After these changes:
- Class-side methods will be properly stored in metaclasses
- The metaclass hierarchy will mirror the class hierarchy
- ProtoObject will be the true root class
- The Metaclass/Class/Object bootstrap will be correct
- StandardLibrary.st will load without metaclass-related errors