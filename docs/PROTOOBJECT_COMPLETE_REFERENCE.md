# ProtoObject Complete Reference

## What Is ProtoObject?

ProtoObject is the minimal root class in modern Smalltalk dialects (especially Squeak and Pharo), designed to support:

- **Bootstrapping the class/metaclass lattice** - Avoids circular dependencies
- **Minimal memory footprint** - For proxy and kernel objects
- **VM startup support** - Avoiding circular dependencies during initialization

It replaces Object as the ultimate superclass, but intentionally lacks most behavior.

## 🔍 Methods in ProtoObject

Here's the complete method set that ProtoObject defines:

| Category | Method Name | Purpose |
|----------|-------------|---------|
| **Basic behavior** | `==`, `~=` | Identity comparison |
| **Basic behavior** | `identityHash` | Identity-based hash value |
| **Error handling** | `doesNotUnderstand:` | Trap for unrecognized messages |
| **Debugging** | `printOn:` | Minimal string representation |
| **Control** | `yourself` | Returns receiver |
| **Reflection** | `class` | Returns the receiver's class |
| **Testing** | `isNil`, `notNil` | Nil testing |
| **Performing** | `perform:`, `perform:with:` | Dynamic message sending |

These are bare essentials—no collections, no control structures, no user interaction.

## 🧠 What's Factored Out to Object?

Object adds rich behavior that ProtoObject intentionally omits:

| Feature Area | Methods in Object (not in ProtoObject) |
|--------------|----------------------------------------|
| **Control structures** | `ifTrue:`, `ifFalse:`, `whileTrue:`, `repeat` |
| **Exception handling** | `error:`, `primitiveFailed`, `signal`, `on:do:` |
| **Collection protocol** | `do:`, `collect:`, `select:`, `reject:` |
| **Copying** | `copy`, `shallowCopy`, `deepCopy` |
| **Comparison** | `=`, `~=`, `hash` (equality vs identity) |
| **Dependency** | `addDependent:`, `removeDependent:`, `changed:` |
| **Debugging tools** | `halt`, `inspect`, `explore` |
| **Instance variables** | `instVarAt:`, `instVarAt:put:`, `instVarNames` |
| **Printing** | `printString`, `storeString` |

## 🧬 Method Inheritance: ProtoObject vs Object

| Method Name | Defined In | Purpose / Notes |
|-------------|------------|-----------------|
| `==` | ProtoObject | Identity comparison |
| `~=` | ProtoObject | Negated identity comparison |
| `identityHash` | ProtoObject | Identity hash |
| `yourself` | ProtoObject | Returns receiver |
| `doesNotUnderstand:` | ProtoObject | Message trap for undefined selectors |
| `class` | ProtoObject | Returns class of receiver |
| `isNil` | ProtoObject | Checks for nil |
| `notNil` | ProtoObject | Checks for non-nil |
| `perform:` | ProtoObject | Dynamic message send |
| `printOn:` | ProtoObject | Minimal string representation |
| `=` | Object | Equality (may differ from identity) |
| `hash` | Object | Content-based hash |
| `copy` | Object | Shallow copy |
| `error:` | Object | Raises an error |
| `halt` | Object | Debugging breakpoint |
| `inspect` | Object | Opens inspector UI |
| `addDependent:` | Object | Dependency mechanism |
| `changed:` | Object | Notify dependents |
| `printString` | Object | Full string representation |

## 📄 Printable Reference Sheet: Method Categories

| Category | Method Name(s) | Defined In | Notes |
|----------|---------------|------------|-------|
| **Identity** | `==`, `~=`, `identityHash` | ProtoObject | Basic identity and hashing |
| **Self-reference** | `yourself` | ProtoObject | Returns receiver |
| **Error handling** | `doesNotUnderstand:` | ProtoObject | Message trap |
| **Printing** | `printOn:` | ProtoObject | Minimal string representation |
| **Reflection** | `class` | ProtoObject | Access class |
| **Nil testing** | `isNil`, `notNil` | ProtoObject | Nil checks |
| **Performing** | `perform:`, `perform:with:` | ProtoObject | Dynamic sends |
| **Equality** | `=`, `hash` | Object | Content comparison |
| **Control flow** | `ifTrue:`, `ifFalse:`, `whileTrue:` | Object | Conditional logic |
| **Instance vars** | `instVarAt:`, `instVarNames` | Object | Variable access |
| **Copying** | `copy`, `shallowCopy` | Object | Object duplication |
| **Debugging** | `halt`, `inspect`, `error:` | Object | Developer tools |
| **Dependency** | `addDependent:`, `changed:` | Object | Observer pattern |

## 🧭 Diagram: ProtoObject as Root for Proxy Classes

```
         +------------------+
         |   ProtoObject    |  ← Minimal root class
         +------------------+
         /        |          \
        /         |           \
       /          |            \
+----------------+ | +--------------------+
| UndefinedObject| | |    MessageSend     |
| (nil singleton)| | | (deferred messages)|
+----------------+ | +--------------------+
                   |
            +------------+
            |   Object   | ← Common protocol
            +------------+
            /     |      \
           /      |       \
    [Boolean] [Magnitude] [Collection]
       |          |           |
    [True]    [Number]     [Array]
    [False]   [Integer]    [Dictionary]
              [SmallInt]   [Set]
```

### 🔹 Why This Matters

1. **UndefinedObject** must not trigger side effects during VM bootstrap
2. **MessageSend** relies on ProtoObject's minimal footprint
3. **doesNotUnderstand:** in ProtoObject is critical for handling deferred or failed sends safely
4. These classes avoid pulling in Object's full behavior during early bootstrap

## 🗺️ Slot-Level Memory Map: ProtoObject and Subclasses

This layout assumes Smalltalk-80 slot structure (0-based indexing):

### 🔹 ProtoObject (8 slots as Class)
```
Offset    Slot Name              Contents
---------------------------------------------
0x00      superclass             nil (ROOT!)
0x04      methodDict             Dictionary {}
0x08      format                 0
0x0C      instanceVariables      Array #()
0x10      organization           nil
0x14      name                   Symbol #'ProtoObject'
0x18      classPool              Dictionary {}
0x1C      sharedPools            Array #()
```

### 🔹 UndefinedObject (inherits from ProtoObject)
```
Offset    Slot Name              Contents
---------------------------------------------
0x00      superclass             ProtoObject
0x04      methodDict             Dictionary {#isNil→true, #notNil→false}
0x08      format                 0
0x0C      instanceVariables      Array #()
0x10      organization           nil
0x14      name                   Symbol #'UndefinedObject'
0x18      classPool              Dictionary {}
0x1C      sharedPools            Array #()
```

### 🔹 Object (adds common protocol)
```
Offset    Slot Name              Contents
---------------------------------------------
0x00      superclass             ProtoObject
0x04      methodDict             Dictionary {large}
0x08      format                 0
0x0C      instanceVariables      Array #()
0x10      organization           ClassOrganizer
0x14      name                   Symbol #'Object'
0x18      classPool              Dictionary {'DependentsFields'→nil}
0x1C      sharedPools            Array #()
```

## 🔁 Overridden Methods in Object (Shadowing ProtoObject)

| Method Name | Defined In | Override Behavior |
|-------------|------------|-------------------|
| `printOn:` | Object | Richer string formatting with class name |
| `doesNotUnderstand:` | Object | Enhanced error reporting with debugger |

## Critical Design Decisions

### Why ProtoObject Exists

1. **Circular Dependency Resolution**: During bootstrap, Class needs Behavior needs ProtoObject, but ProtoObject's class is Class. Having a minimal ProtoObject breaks this cycle.

2. **Proxy Support**: Classes like `ProtoObjectProxy`, `RemoteObject`, and `TransparentForwarder` need to intercept ALL messages except the absolute minimum. ProtoObject provides that minimum.

3. **VM Optimization**: The VM can make assumptions about ProtoObject methods being primitive or trivial, enabling optimizations.

4. **Memory Efficiency**: Kernel objects that inherit from ProtoObject avoid the overhead of Object's rich protocol.

### What Must Stay in ProtoObject

These methods MUST be in ProtoObject because:

- `==` and `identityHash`: Required for identity comparison in VM
- `class`: Required for method lookup
- `doesNotUnderstand:`: Required for message handling
- `isNil`/`notNil`: Required for nil testing in primitives
- `perform:`: Required for dynamic dispatch
- `yourself`: Required for cascade returns

### What Can Move to Object

Everything else can safely be in Object because:
- Control flow (`ifTrue:`) - syntax sugar, not VM requirement
- Error handling (`error:`) - debugging convenience
- Copying (`copy`) - application-level feature
- Dependencies (`changed:`) - design pattern support

## Bootstrap Implications

During bootstrap, the creation order becomes:

1. **ProtoObject** - Created with nil superclass
2. **Behavior** - Extends ProtoObject
3. **Class** - Extends Behavior
4. **Metaclass** - Extends Class
5. **Object** - Extends ProtoObject (adds common protocol)
6. **UndefinedObject** - Extends ProtoObject (for nil)

This ensures the metaclass loop can be established without requiring Object's full protocol, making the bootstrap more robust and the resulting system more flexible for proxies and special objects.