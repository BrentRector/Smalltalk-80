Two class descriptions named Interpreter and ObjectMemory make up the formal specification of the Smalltalk-80 virtual machine. The implementation of Interpreter will be presented in detail in this chapter and the following two; the implementation of ObjectMemory in Chapter 30.

A potential source of confusion in these chapters comes from the two Smalltalk systems involved in the descriptions: the system containing Interpreter and ObjectMemory and the system being interpreted. Interpreter and ObjectMemory have methods and instance variables and they also manipulate methods and instance variables in the system they interpret. To minimize the confusion, we will use a different set of terminology for each system. The methods of Interpreter and ObjectMemory will be called routines; the word method will be reserved for the methods being interpreted. Similarly, the instance variables of Interpreter and ObjectMemory will be called registers; the word instance variable will be reserved for the instance variables of objects in the system being interpreted.

The arguments of the routines and the contents of the registers of Interpreter and ObjectMemory will almost always be instances of Integer (SmallIntegers and LargePositiveIntegers). This can also be a source of confusion since there are Integers in the interpreted system. The Integers that are arguments to routines and contents of registers represent object pointers and numerical values of the interpreted system. Some of these will represent the object pointers or values of Integers in the interpreted system.

The interpreter routines in this specification will all be in the form of Smalltalk method definitions. For example

```smalltalk
routineName: argumentName
    | temporaryVariable |
    temporaryVariable ← self anotherRoutine: argumentName.
    ↑temporaryVariable ← 1
```

The routines in the specification will contain five types of expression.

1. **Calls on other routines of the interpreter.** Since both the invocation and definition of the routine are in Interpreter, they will appear as messages to self.
   - self headerOf: newMethod
   - self storeInstructionPointerValue: value
     inContext: contextPointer

2. **Calls on routines of the object memory.** An Interpreter uses the name memory to refer to its object memory, so these calls will appear as messages to memory.
   - memory fetchClassOf: newMethod
   - memory storePointer: senderIndex
     ofObject: contextPointer
     withValue: activeContext

3. **Arithmetic operations on object pointers and numerical values.** Arithmetic operations will be represented by standard Smalltalk arithmetic expressions, so they will appear as messages to the numbers themselves.
   - receiverValue + argumentValue
   - selectorPointer bitShift: -1

4. **Array accesses.** Certain tables maintained by the interpreter are represented in the formal specification by Arrays. Access to these will appear as at: and at:put: messages to the Arrays.
   - methodCache at: hash
   - semaphoreList at: semaphoreIndex put: semaphorePointer

5. **Conditional control structures.** The control structures of the virtual machine will be represented by standard Smalltalk conditional control structures. Conditional selections will appear as messages to Booleans. Conditional repetitions will appear as messages to blocks.
   - index < length ifTrue: [ ... ]
   - sizeFlag = 1 ifTrue: [ ... ]
     ifFalse: [ ... ]
   - [currentClass ~= NilPointer] whileTrue: [ ... ]

The definition of Interpreter describes the function of the Smalltalk-80 bytecode interpreter; however, the form of a machine language implementation of the interpreter may be very different, particularly in the control structures it uses. The dispatch to the appropriate routine to execute a bytecode is an example of something a machine language interpreter might do differently. To find the right routine to execute, a machine language interpreter would probably do some kind of address arithmetic to calculate where to jump; whereas, as we will see, Interpreter does a series of conditionals and routine calls. In a machine language implementation, the routines that execute each bytecode would simply jump back to the beginning of the bytecode fetch routine when they were finished, instead of returning through the routine call structure.

Another difference between Interpreter and a machine language implementation is the degree of optimization of the code. For the sake of clarity, the routines specified in this chapter have not been optimized. For example, to perform a task, Interpreter may fetch a pointer from the object memory several times in different routines, when a more optimized interpreter might save the value in a register for later use. Many of the routines in the formal specification will not be subroutines in a machine language implementation, but will be written in-line instead.
