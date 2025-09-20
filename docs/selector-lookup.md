# Smalltalk-80 Method Lookup Algorithm

## Overview

Smalltalk-80 uses a single, unified method lookup algorithm for all message sends. There is no special "two-phase" lookup - the algorithm is consistent whether sending instance methods or class methods.

## The Universal Lookup Algorithm

When a message is sent to an object, the VM performs the following steps:

1. **Get the receiver's class**
   - For regular objects: Use the class pointer from the object header
   - For SmallIntegers: Use the SmallInteger class

2. **Search for the method**
   - Start at the receiver's class
   - Look in that class's method dictionary for the selector
   - If not found, move to the superclass
   - Repeat until method is found or superclass chain ends (nil)

3. **Execute or handle failure**
   - If method found: Execute it with the receiver and arguments
   - If not found: Send `doesNotUnderstand:` to the original receiver

## Key Concepts

### Instance Methods vs Class Methods

The distinction between instance and class methods is **NOT** in the lookup algorithm, but in **WHERE** the methods are stored:

- **Instance methods**: Stored in the class's method dictionary
- **Class methods**: Stored in the metaclass's method dictionary

### What Happens When Sending Messages

#### Sending to an Instance
```smalltalk
myObject doSomething
```
- Receiver: `myObject` (an instance)
- Lookup starts at: `myObject`'s class
- Searches: The class hierarchy (class → superclass → ... → Object)
- Finds: Instance methods

#### Sending to a Class
```smalltalk
MyClass doSomething
```
- Receiver: `MyClass` (which is itself an object - an instance of its metaclass)
- Lookup starts at: `MyClass`'s class (which is its metaclass)
- Searches: The metaclass hierarchy (metaclass → supermetaclass → ... → Class)
- Finds: Class methods

## Example: Object initialize

Let's trace through the message send `Object initialize`:

1. **Receiver**: `Object` (the class object itself)
2. **Get receiver's class**: `Object class` (the metaclass of Object)
3. **Lookup sequence**:
   - Check `Object class` for selector `initialize`
     - Found? Execute it
     - Not found? Continue...
   - Check superclass of `Object class`, which is `Class`
     - Look in `Class`'s method dictionary
     - Not found? Continue...
   - Check superclass of `Class`, which is `ClassDescription`
     - Look in `ClassDescription`'s method dictionary
     - Not found? Continue...
   - Check superclass of `ClassDescription`, which is `Behavior`
     - Look in `Behavior`'s method dictionary
     - Not found? Continue...
   - Check superclass of `Behavior`, which is `Object`
     - Look in `Object`'s method dictionary
     - Not found? Continue...
   - Check superclass of `Object`, which is `nil`
     - End of chain - method not found

4. **Result**: If `initialize` is defined on `Object class`, it will be found in step 3.1

## The Metaclass Hierarchy

Understanding the metaclass hierarchy is crucial for method lookup:

```
Object ← Class ← ClassDescription ← Behavior ← Object (instance methods)
   ↑        ↑            ↑              ↑
Object    Class      ClassDescription  Behavior     (the class objects)
class     class         class          class
```

### Object's Special Case

- `Object`'s superclass is `nil`
- `Object class`'s superclass is `Class` (not nil!)
- This ensures class methods defined on `Class` are available to all metaclasses

## Common Misconceptions

### Myth: Two-Phase Lookup
**FALSE**: There is no two-phase lookup. The algorithm doesn't first check instance methods and then class methods. It follows a single chain determined by the receiver's class.

### Myth: Class Methods Are Special
**FALSE**: Class methods are just instance methods of the metaclass. When you send a message to a class, you're sending a message to an object (the class) that happens to be an instance of its metaclass.

### Myth: Instance Methods Can Be Found When Sending to Classes
**FALSE**: When sending a message to a class object, the lookup starts in the metaclass hierarchy. Instance methods (stored in the class) are not in the lookup path.

## Implementation in the Interpreter

The interpreter implements this algorithm in `MessageDispatcher.cs`:

```csharp
// Simplified pseudocode
OOP LookupMethod(OOP classOop, OOP selector) {
    var currentClass = classOop;
    
    while (!currentClass.IsNil) {
        var methodDict = currentClass.methodDictionary;
        var method = LookupInDictionary(methodDict, selector);
        
        if (!method.IsNil) {
            return method;
        }
        
        currentClass = currentClass.superclass;
    }
    
    return OOP.Nil; // Not found
}
```

## Debugging Method Lookup

When debugging method lookup issues:

1. **Verify the receiver's class**: Is it what you expect?
2. **Check the metaclass hierarchy**: Is `Object class` superclass set to `Class`?
3. **Examine method dictionaries**: Are methods installed in the right place?
4. **Trace the lookup path**: Follow the exact superclass chain

## Special Objects and Method Lookup

Some objects have special handling:

- **SmallIntegers**: Always use the SmallInteger class (not stored in object header)
- **nil**: Has class UndefinedObject
- **true/false**: Have classes True and False respectively

## Method Cache

For performance, the interpreter caches method lookups:
- Key: (class, selector) pair
- Value: (method, methodClass) pair
- Cache is cleared when classes are modified

## Summary

The Smalltalk-80 method lookup algorithm is elegant in its simplicity:
1. Start at the receiver's class
2. Walk up the superclass chain
3. Stop when method is found or chain ends

This single algorithm handles all method dispatch, with the receiver's class determining which hierarchy (instance or metaclass) is searched.