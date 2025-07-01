This section describes what might be called the data structures of the interpreter. Although they are objects, and therefore more than data structures, the interpreter treats these objects as data structures. The first two types of object correspond to data structures found in the interpreters for most languages. Methods correspond to programs, subroutines, or procedures. Contexts correspond to stack frames or activation records. The final structure described in this section, that of classes, is not used by the interpreter for most languages but only by the compiler. Classes correspond to aspects of the type declarations of some other languages. Because of the nature of Smalltalk messages, the classes must be used by the interpreter at runtime.

There are many constants included in the formal specification. They mostly represent object pointers of known objects or field indices for certain kinds of objects. Most of the constants will be named and a routine that initializes them will be included as a specification of their value. As an example, the following routines initialize the object pointers known to the interpreter.

```smalltalk
initializeSmallIntegers
    "SmallIntegers"
    MinusOnePointer ← 65535.
    ZeroPointer ← 1.
    OnePointer ← 3.
    TwoPointer ← 5.
```

```smalltalk
initializeGuaranteedPointers
    "UndefinedObject and Booleans"
    NilPointer ← 2.
    FalsePointer ← 4.
    TruePointer ← 6.
    "Root"
    SchedulerAssociationPointer ← 8.
    "Classes"
    ClassStringPointer ← 14.
    ClassArrayPointer ← 16.
    ClassMethodContextPointer ← 22.
    ClassBlockContextPointer ← 24.
    ClassPointPointer ← 26.
    ClassLargePositiveIntegerPointer ← 28.
    ClassMessagePointer ← 32.
    ClassCharacterPointer ← 40.
    "Selectors"
    DoesNotUnderstandSelector ← 42.
    CannotReturnSelector ← 44.
    MustBeBooleanSelector ← 52.
    "Tables"
    SpecialSelectorsPointer ← 48.
    CharacterTablePointer ← 50.
```
