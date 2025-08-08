# Fix for Class Variable System Failure

## Problem Summary

Class variables don't work because:

1. **Bootstrap Issue**: The bootstrap creates C# shadow structures (`List<string> ClassVariables`) instead of real Smalltalk Dictionary objects for `classPool`

2. **Compiler Issue**: The compiler doesn't look for class variables in the classPool when compiling methods

## Root Cause

The C# implementation maintains shadow copies of Smalltalk data structures instead of creating real Smalltalk objects and using VM primitives to manipulate them.

## The Fix

### 1. Bootstrap Changes (GeneratedBootstrap.cs)

Instead of:
```csharp
classes["Object"] = SystemDictionary.DefineClass(
    "Object",
    null,
    new List<string>(),
    new List<string>(), // Just stores names!
    ...);
```

Should be:
```csharp
// Create real Smalltalk Class object with proper Fields array
var objectClass = new SmalltalkObject(classClass, 8); // 8 fields for Class

// Set instance variables inherited from Behavior
objectClass.Fields[0] = superclass;           // superclass
objectClass.Fields[1] = methodDict;           // methodDict (Smalltalk Dictionary)
objectClass.Fields[2] = format;               // format

// From ClassDescription
objectClass.Fields[3] = instanceVarsArray;    // instanceVariables
objectClass.Fields[4] = organization;         // organization

// From Class
objectClass.Fields[5] = new Symbol("Object"); // name
objectClass.Fields[6] = classPoolDict;        // classPool (Smalltalk Dictionary!)
objectClass.Fields[7] = sharedPoolsDict;      // sharedPools

// Initialize the classPool Dictionary with class variables
// For Object: DependentsFields, ErrorRecursion
AddToClassPool(classPoolDict, "DependentsFields", nil);
AddToClassPool(classPoolDict, "ErrorRecursion", nil);
```

### 2. Compiler Changes (AstMethodCompiler.cs)

In the `LoadVariable` method, add class variable lookup:

```csharp
private void LoadVariable(string name)
{
    // ... existing checks for self, super, temporaries, instance vars ...

    // NEW: Check class variables in classPool
    if (compilingClass != null)
    {
        var classPool = GetClassPool(compilingClass);
        if (classPool != null && HasClassVariable(classPool, name))
        {
            // Create association for class variable
            var association = GetClassVariableAssociation(classPool, name);
            int literalIndex = AddLiteral(association);
            bytecodes.Add((byte)((int)Bytecode.PushLiteralVariable0 + literalIndex));
            return;
        }
    }

    // ... existing check for globals ...
}
```

### 3. Helper Methods Needed

```csharp
// Get the classPool Dictionary from a Class object
private SmalltalkObject GetClassPool(SmalltalkClass cls)
{
    // Class objects have classPool at field index 6
    if (cls.Fields.Length > 6)
        return cls.Fields[6] as SmalltalkObject;
    return null;
}

// Check if classPool contains a variable
private bool HasClassVariable(SmalltalkObject classPool, string name)
{
    // Need to send includesKey: message to the Dictionary
    // Or use a primitive to check
    return CheckDictionaryHasKey(classPool, new Symbol(name));
}
```

## Implementation Steps

1. **Create Real Dictionary Class**
   - Implement minimal Dictionary with primitive operations
   - Use Smalltalk's Set structure (tally + array of Associations)

2. **Fix Bootstrap**
   - Create real Smalltalk objects for classes
   - Initialize classPool as Smalltalk Dictionary
   - Store class variables in classPool

3. **Fix Compiler**
   - Add class variable lookup
   - Check classPool before throwing "Unknown variable"

4. **Fix Class Creation**
   - When creating new classes via `subclass:...`, ensure classPool is initialized
   - Use Smalltalk's own `declare:` method to add class variables

## Testing

After fix, this should work:
```smalltalk
Float initialize.  "Sets Pi, Halfpi, etc. in classPool"
Float pi.          "Returns 3.14159 from classPool"
```

## Benefits

1. Proper Smalltalk object model - no C# shadows
2. Class variables work correctly
3. Bootstrap uses real Smalltalk objects
4. Foundation for full Smalltalk class creation