# Low-Level Class Definition in Smalltalk-80 Without Compiler

## Defining a Complete New Class in Classic Smalltalk-80 Using Only Low-Level Message Sends and Bytecode: A Technical Guide

### Introduction

Defining classes dynamically is a core feature of the classic Smalltalk-80 programming environment. While modern Smalltalk systems provide high-level tools and a versatile compiler (the Compiler class), the original system placed extraordinary importance on being able to build and extend the system from within, using as few external dependencies as possible. This capability was essential for self-hosting, bootstrapping, and minimal Smalltalk interpreter and VM implementations—properties precisely described in the influential "Blue Book" ("Smalltalk-80: The Language and its Implementation" by Goldberg & Robson). The scenario becomes genuinely technical and illuminating when we ask: can we define a new class using only low-level message sends and/or bytecode, without using the Compiler and with the minimum system dependencies?

This technical guide details each step of this process, grounded in the Smalltalk-80 object and class model, bytecode instruction set, fundamental message sends, and inner workings of the VM and object memory. It culminates with an explicit example of defining a new class (with class/instance methods, class variables, and instance variables) using only the most primitive Smalltalk operations, suitable for minimal interpreter contexts. We reference both foundational texts (Blue Book) and modern resources from interpreters, minimalist VMs, and ongoing open-source Smalltalk systems.

## Section 1: The Smalltalk-80 Object and Class Model

### Fundamentals of Classes, Metaclasses, and Message Sending

Smalltalk-80 is famous for its "everything is an object" philosophy, and this applies recursively to classes themselves: classes are objects (instances of their metaclasses), and metaclasses are themselves instances of the global Metaclass, forming a parallel hierarchy to the classes they describe. The class system is self-describing, which is key to the bootstrapping capacity.

Classes are not created with special syntax. Instead, creating a class is itself a message send—typically to a superclass object, usually via selectors such as `subclass:instanceVariableNames:classVariableNames:poolDictionaries:category:` (and variants for indexable classes), passing the class name and description as arguments. Methods are likewise added by message sends, either as compiled source code (ordinarily via the Compiler) or by directly updating the class's method dictionary with a CompiledMethod object.

Metaclasses serve as the repository for class-side behavior (class methods). When you create a new class, the system automatically creates its metaclass, which is a subclass of the superclass's metaclass. This setup allows class methods to be inherited in parallel to instance methods, a powerful and flexible design.

In summary:

- A class is an object with a method dictionary (for instance methods), a set of instance variable names, and optionally class variables.
- Each class has an associated metaclass (for class methods).
- Both classes and metaclasses are created and linked via message sends, not parser/declarative syntax.
- All method additions, variable additions, and more, are realized through explicit message sends or object mutations.

This reflects the "message sending all the way down" nature of Smalltalk, as described in comprehensive theoretical and practical expositions.

## Section 2: Low-Level Message Sends and the Bytecode Perspective

### 2.1 Types of Low-Level Message Sends

In Smalltalk-80, almost every action is a message send—from creating an object, defining a class, setting variables, to adding or removing methods. There are three main types of message sends:

- **Unary**: No arguments, e.g., `OrderedCollection new`.
- **Binary**: One argument, often operators, e.g., `1 + 2`.
- **Keyword**: One or more arguments, with each keyword segment ending in ":", e.g., `Object subclass: #SampleClass instanceVariableNames: 'ivar1' classVariableNames: 'CVar1' poolDictionaries: '' category: 'ManualClasses'`.

Direct manipulation at the lowest level (such as in a bootstrapping interpreter or a custom image loader or even directly in C via the VM interface) can be performed by:

- Assembling and sending these messages programmatically.
- Constructing objects in raw memory using ObjectMemory primitives, if not using the language itself.
- Creating method objects (CompiledMethod), setting up their bytecodes, associated literals, and inserting them directly into the method dictionary of a class or metaclass.

### 2.2 Smalltalk-80 Bytecode Instructions for Class and Method Definition

While much application code can be written using only message sends, defining methods and manipulating classes at the lowest level sometimes requires direct creation and manipulation of CompiledMethod objects, which encapsulate:

- A header (encodes number of local variables, arguments, flags, and other metadata).
- An array of literals (selectors, constants, etc.).
- A bytecode stream.

Relevant bytecodes include:

- **Stack manipulation**: Pushing/popping values, retrieving instance variable values.
- **Returns**: Indicating where a method should return control (with or without values).
- **Sends**: Invoking method selectors (to classes or instances), potentially causing lookup up the class hierarchy.
- **Primitive operations**: Some methods, especially those that operate closely with the VM or object memory, use "primitives", directly invoking VM routines.

For example, adding a new method typically means constructing a CompiledMethod object with desired bytecode (e.g., for an accessor method), then inserting it into the class/method dictionary.

## Section 3: Class Structure and Creation in Minimal Smalltalk

### 3.1 The Minimal Bootstrap Process (from the Blue Book and Modern Interpreters)

The classic Smalltalk-80 bootstrap process (described in the Blue Book, Chapter 27, and replicated in minimal implementations) involves:

1. Initializing essential objects and global pointers (nil, true, false, numerics, symbols).
2. Creating the minimal class hierarchy:
   - First, define Behavior (the root class of anything that has "behavior", i.e., methods).
   - Then, ClassDescription descends from Behavior.
   - Next, Class descends from ClassDescription.
   - Define Metaclass and the initial metaclass loops.
   - Establish the root Object class and its metaclass.
3. Creating MethodDictionary and installing initial methods (including subclass:...).
4. Adding the bootstrapping logic to allow further low-level message sends to define new classes, add variables, and add methods.

**Why this process matters**: The system must reach a point where it can "bootstrap itself"—i.e., be capable of defining and mutating its own classes and objects using its own facilities, without external compilers.

**Key system objects and primitives for this**:

- **ObjectMemory**: Provides allocation of objects, direct manipulation, and querying of object fields.
- **Behavior/ClassDescription/Class**: Provide interfaces for subclassing, variable assignment, and method dictionary updates.

This bootstrapping approach is essential for a minimal interpreter or for constructing a Smalltalk image from the ground up, particularly one that mirrors the Blue Book's semantics.

## Section 4: Defining Instance and Class Variables Using Message Sends

### 4.1 Instance Variables

Instance variables are specified as part of the class initialization message, e.g.:

```smalltalk
Object subclass: #SampleClass
   instanceVariableNames: 'value count'
   classVariableNames: ''
   poolDictionaries: ''
   category: 'ManualClasses'
```

The string in `instanceVariableNames:` is split (typically by spaces) and stored in the class's variable layout.

When a new instance is created, object memory arranges for additional fields, one per instance variable, accessible via the offset determined by the class layout.

Access to these variables at runtime is usually via bytecode instructions that push or pop instance variable slots.

**Low-level approach**: In a minimal interpreter, the corresponding message send (or system-specific primitive) directly sets the list of variable names and updates the class's metadata.

### 4.2 Class Variables

Class variables are similar in spirit but are:

- Shared across all instances of the class and its subclasses.
- Stored in a dictionary owned by the class object itself (separate from instance fields).

They are declared similarly:

```smalltalk
Object subclass: #SampleClass
   instanceVariableNames: ''
   classVariableNames: 'GlobalCounter GlobalFlag'
   poolDictionaries: ''
   category: 'ManualClasses'
```

Low-level manipulation can also involve sending `classVariableNames:` to the class after creation, or providing it as a constructor argument. Object memory arranges the necessary structures, making these variables accessible to both instance and class methods.

### Table: Smalltalk Variable Types (Summary and Low-Level Handling)

| Variable Type | Scope of Accessibility | Storage Location | Declared How | Accessed via |
|---|---|---|---|---|
| Instance variable | Per-instance of class | Object fields in instance | `instanceVariableNames:` | Bytecode (e.g., pushInstVar) |
| Class variable | All instances and subclasses | Dictionary owned by class object | `classVariableNames:` | Bytecode and message send |
| Class instance var | Per-class object | Object fields in class object (not insts) | `MyClass class instanceVariableNames:` | Class-side method/bytecode |
| Global variable | All code | System dictionary (Smalltalk) | Created/initialized directly | Message send |
| Pool variable | Classes sharing a pool | Shared dictionary (external) | `poolDictionaries:` | Message send |

While the classic system supports primarily the first three directly, class instance variables are found in some dialects/extensions.

**Crucial point**: The declaration and use of these variables can be performed solely by message sends—special syntax in source code is only a convenience, not a necessity.

## Section 5: Method Installation Through Manual CompiledMethod Construction

### 5.1 The CompiledMethod Object

A method in Smalltalk is physically an instance of the CompiledMethod class, which is stored in a class's method dictionary under its selector name (a Symbol). Each CompiledMethod object comprises:

- A header (number of arguments, locals, flags).
- Bytecode array.
- An array of literals.
- Source code (optional).

To add a method in the lowest-level way:

1. Construct a CompiledMethod directly—set its selector, header, literals, and bytecode.
2. Use `methodDict at: selector put: compiledMethod` to install it into the class's method dictionary.

For class (meta) methods, you operate on `MyClass class methodDict`.

#### 5.1.1 Primitive Example: Accessor Method via Bytecode

Suppose you want an accessor for the first instance variable (offset 0):

```smalltalk
CompiledMethod new
  selector: #ivar1;
  bytecodes: #(112 124); "push instVar 0, return top of stack"
  literals: #();
  methodClass: SampleClass;
  yourself.
```

- `112` is the bytecode for "push instVar 0".
- `124` is "return top of stack".
- Literals is empty.

You would then insert this into the method dictionary:

```smalltalk
SampleClass methodDict at: #ivar1 put: compiledMethod.
```

This approach can be (and is) used by minimal interpreters and in custom bootstrapping tools.

### 5.2 Instance vs. Class (Meta) Methods

- **Instance methods** are stored in the `methodDict` field of the class object.
- **Class methods** are stored in the `methodDict` field of the metaclass object (the class of the class).

To install a class method, you would:

```smalltalk
SampleClass class methodDict at: #classMethod put: compiledMethod.
```

or, more technically:

```smalltalk
SampleClass class methodDict at: #initCounter put: methodObject.
```

Constructing the compiled method is identical, except that the methodClass is set to the metaclass.

## Section 6: Minimal Example—Manual Class Creation of SampleClass

Let us walk through a complete example, defining a new class with two instance variables, two class variables, and one instance method and one class method, all manually installed:

### Step 1: Create the Class Object

This step is always a message send to a superclass. For clarity, here's the normal, high-level Smalltalk form:

```smalltalk
Object subclass: #SampleClass
    instanceVariableNames: 'value count'
    classVariableNames: 'GlobalCounter GlobalFlag'
    poolDictionaries: ''
    category: 'ManualClasses'
```

But at the lowest level (as in a minimal interpreter), you programmatically:

- Construct an array of symbols for instance variables and class variables.
- Use the object memory's primitive for subclass creation, or send this message with arguments constructed as objects.

### Step 2: Manual Creation Without Compiler

To avoid `Compiler compile:`, you instead manually construct CompiledMethod objects with bytecode sequences, selectors (as symbols), and literal arrays.

**Example: Instance Method—Accessor for value**

Bytecode to push instance variable at offset 0: (Smalltalk-80 bytecode 112 for "push self"; 124 for "return top").

No arguments or temporaries.

```smalltalk
| method |
method := CompiledMethod new.
method selector: #getValue.
method bytecodes: #(112 124).  "push self, return"
method literals: #().      "No literals"
method methodClass: SampleClass.
SampleClass methodDict at: #getValue put: method.
```

**Example: Class Method—Initialize GlobalCounter**

Suppose a class method sets GlobalCounter to zero (requires sending at:put: to class variable dictionary if not using the compiler):

```smalltalk
| classMethod |
classMethod := CompiledMethod new.
classMethod selector: #initCounter.
classMethod bytecodes: #(self-put-zero-bytecodes). "Pseudocode: manual bytecodes constructed"
classMethod literals: #().
classMethod methodClass: SampleClass class.
SampleClass class methodDict at: #initCounter put: classMethod.
```

**Note**: In reality, to produce "practical" bytecode for higher-level logic, more detailed knowledge of the variable index and method dictionary is needed. For demonstration, bytecodes are shown symbolically.

### Step 3: Verifying Structure

- SampleClass now has instance variables `value` and `count`.
- Class variables `GlobalCounter`, `GlobalFlag`.
- Instance method `getValue` accessible to its instances, returning its value.
- Class method `initCounter` available on the metaclass, setting shared variables.

### Step 4: Creating Instances and Using Methods

Once constructed this way, you can create instances of SampleClass using:

```smalltalk
sample := SampleClass new.
sample getValue. "Will invoke the manually-installed accessor"
```

To ensure all these operations are possible, the minimal interpreter must support:

- Executing the subclassing message (`subclass:...`).
- Allocating storage for instance and class variables.
- Allowing low-level pointer chasing for method dictionaries and variable bindings.
- Injecting CompiledMethod objects into a method dictionary.

## Section 7: ObjectMemory, Behavior, and VM Primitives

### 7.1 Primitives for Class and Method Definition

Certain operations required for this manual process are realized most efficiently and generically via VM-exposed primitives, e.g.:

- Create a new instance of a class: `primitiveNewClass`
- Compile or assemble a method: `primitiveCompile`
- Add a method directly: `primitiveAddMethod`

In C-based interpreters, companion functions exist to allocate and manipulate objects at this low level:

```c
OBJ __BASICNEW(OBJ cls);       // low-level instance allocation
__objPtr(newObject)->o_class = SampleClass;
```

Or, in minimal Smalltalk environments, directly update object fields or method dictionaries.

### 7.2 Inspecting and Modifying the Metaclass Structure

After class creation, Smalltalk allows you to navigate and introspect the new class and its metaclass:

```smalltalk
SampleClass class superclass   "Yields Class class"
SampleClass class class        "Yields Metaclass"
SampleClass class methodDict   "Dictionary of class (meta) methods"
```

To add further class (meta) methods:

```smalltalk
SampleClass class methodDict at: #foo put: metaMethodObject.
```

This is fundamentally symmetrical to instance method addition, only the target is the metaclass.

## Section 8: Modern Minimalist Implementations Reference

Not only are these steps described in the Blue Book, but they also underpin many minimalist or educational interpreters (e.g., dbanay/Smalltalk, SmallWorld, TinySmalltalk), and are adopted in reproducible bootstraps (as in GNU Smalltalk, Squeak, Pharo, etc.).

For example, dbanay/Smalltalk's bootstrapping script closely mirrors the Blue Book, performing explicit message sends and method dictionary manipulation at the object level. Modern interpreters frequently expose C interfaces or scripting APIs echoing this procedure.

### Example Minimal Class Creation in dbanay/Smalltalk

```smalltalk
| sampleClass method |

sampleClass := Object subclass: #SampleClass
    instanceVariableNames: 'ivar1 ivar2'
    classVariableNames: 'CVar1 CVar2'
    poolDictionaries: ''
    category: 'ManualClasses'.

method := CompiledMethod new.
method selector: #ivar1.
method bytecodes: #(112 124). "push instVar 0, return"
method literals: #().
method methodClass: sampleClass.

sampleClass methodDict at: #ivar1 put: method.
```

This sequence is fully manual—no use of the Compiler. More sophisticated methods or control structures can be constructed by assembling appropriate bytecodes or, for simplicity in modern systems, using a minimal 'assembler'.

## Section 9: Bytecode Details—Instruction Set for Class and Method Definition

The Smalltalk-80 bytecode set is well-documented (see Blue Book, Squeak/Pharo VM docs, etc.), and minimal method definitions rely on a small subset:

| Operation | Bytecode Value | Description |
|---|---|---|
| push self | 112 | Push receiver (self) onto stack |
| push instVar N | 70+N (0-7) | Push instance variable N onto stack |
| push literal N | 24+N (0-15) | Push literal object at index N in method's literal array |
| send selector | 208–255 | Send selector (from literals) to receiver |
| return top | 124 | Return top of stack |
| return self | 120 | Return self |

To assemble an accessor method for ivar1 (offset 0):

- 112 (push self)
- 124 (return top of stack)

Or, for ivar2 (offset 1):

- 113 (push instVar 1)
- 124 (return)

Advanced methods (with arguments, assignments, control) require more bytecode, but the principle remains the same. Tools such as ByteSurgeon and the Squeak/Pharo internal bytecode compilers provide further inspection of such method objects.

## Section 10: Alternative Minimal Smalltalk Implementations and APIs

Minimal Smalltalk implementations—including Little Smalltalk, SmallWorld, and various open-source minimal interpreters (e.g., dbanay/Smalltalk, TinySmalltalk, GNU Smalltalk)—follow this low-level sequence for class and method definition.

In GNU Smalltalk, the VM exposes C-level functions for creating objects, setting class pointers, assigning instance variables, and adding compiled methods, all mimicking message sends and the object memory interface described in the Blue Book. These can be mapped to direct Smalltalk message sends when implemented in the language itself.

GNU Smalltalk also provides an example in its manual:

```smalltalk
Object subclass: #Account
    instanceVariableNames: 'balance'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Finance'
```

And then descriptively, you may add methods directly via the CompiledMethod API in C:

```c
CompiledMethod class: SampleClass
    selector: #foo
    bytecodes: ...
    literals: ...
```

and then install it into the method dictionary.

Squeak/Pharo and other modern dialects expose similar (but not identical) APIs for manual method manipulation, and their bootstrapping scripts are exemplars of this process.

## Section 11: Practical Advice, Edge Cases, and Nuances

### 11.1 Pool Dictionaries and Shared Variables

Pool variables (shared across classes) are less common in modern code but still used, and may be included in the message send at class definition, e.g., `poolDictionaries: 'SomePool'`. At the lowest level, this involves adding references to external or global dictionaries of symbols and values.

### 11.2 Indexed (Variable-sized) Instance Variables

For classes such as Array, the subclassing message variants `variableSubclass:` or similar are used to indicate the presence of indexed variables. At the bytecode/object level, this affects allocation size and access patterns but not the fundamental principle of definition by message send.

### 11.3 Bootstrap Singularities (Metaclass/Mirroring)

In a truly minimal bootstrap, the creation of the first metaclass and the self-reference cycle (e.g., Metaclass's class being itself) cannot be performed solely by message sends, as there is no way to "send a message" to a non-existent object. Bootstrap code in the VM or a loader must arrange these corner cases (see discussion in bootstrapping papers and modern Smalltalk-80 minimal VMs); all else can be built by message send from this foundation.

## Conclusion

Defining a complete new class in classic Smalltalk-80 using only low-level message sends and/or bytecode, without the compiler, is both possible and central to the system's reflective power and bootstrapping capability.

The essential steps—sending `subclass:...`, specifying instance/class variables, and directly installing CompiledMethod objects into the method dictionary—can all be realized by constructing and sending objects and messages by hand, or using low-level VM/external APIs that mimic message sends.

The process underpins the flexibility and self-hosting capability of Smalltalk-80, and is realized both in the Blue Book's minimal interpreter specification and in modern minimalist Smalltalk implementations. By mastering these principles, you gain deep insight into how Smalltalk (and by extension, highly dynamic systems in general) are both bootstrapped and extended in practice.

References to code, bytecode values, and system architecture throughout this guide have been grounded in the Blue Book, open-source implementations, the GNU Smalltalk manual, and technical documentation of object memory, bytecode, and virtual machine construction.

## Appendix: Complete Example—Manual Definition of SampleClass

```smalltalk
| sampleClass method classMethod |

"Step 1: Create class via message send to Object (or any superclass)"
sampleClass := Object subclass: #SampleClass
    instanceVariableNames: 'ivar1 ivar2'
    classVariableNames: 'CVar1 CVar2'
    poolDictionaries: ''
    category: 'ManualClasses'.

"Step 2: Create instance accessor method via bytecode"
method := CompiledMethod new.
method selector: #ivar1.
method bytecodes: #(112 124). "push instVar 0, return top"
method literals: #().
method methodClass: sampleClass.
sampleClass methodDict at: #ivar1 put: method.

"Step 3: Create class method via bytecode for metaclass"
classMethod := CompiledMethod new.
classMethod selector: #getCVar1.
classMethod bytecodes: #(112 124). "push instVar 0, return top (metaclass context)"
classMethod literals: #().
classMethod methodClass: sampleClass class.
sampleClass class methodDict at: #getCVar1 put: classMethod.
```

At this point, SampleClass is a "real" Smalltalk class—created with fundamental message sends, instance and class variables, and method dictionaries manually populated with objects representing compiled methods, structured at the lowest level. All these operations mimic the very process that the first Smalltalk-80 systems performed and that modern minimalist interpreters and VMs still follow today.