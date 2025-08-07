# Stage 2 Bootstrap Process

## Overview

After Stage 1 (VM primitive bootstrap), the Smalltalk system has crossed a critical threshold. The foundational classes are now alive inside the VM, and we can use Smalltalk itself to expand the system. This document describes how to leverage the newly available capabilities to build out the rest of the system.

## ✅ Core Capabilities Now Available After Stage 1

### 🧠 Class Hierarchy and Instantiation
- **ProtoObject, Object, Behavior, Class** are fully defined and linked
- **Metaclasses** are automatically created by the VM
- Can instantiate objects via `Class >> new` and `Object >> new` (backed by primitive 70)
- Class hierarchy is navigable via `superclass` and `class` methods

### 🗣️ Messaging and Dispatch
- `Object >> perform:` enables dynamic message sending (primitive 83)
- `Object >> class` and `basicAt:` allow access to object's class pointer
- `Behavior >> methodDict` and method installation support
- Method lookup is wired to VM via primitives

### 📚 Symbol and Dictionary Support
- `Symbol class >> intern:` allows symbol interning (primitive 91)
- `Dictionary >> at:` and `at:put:` enable method dictionary manipulation
- MethodDictionary specialized for method storage

### 🧩 Primitive Slot Access
- `basicAt:` and `basicAt:put:` allow direct indexed slot manipulation
- `instVarAt:` and `instVarAt:put:` for named instance variables
- Essential for VM-level object layout and introspection

## 🔄 Stage 2: Expanding the System Using Smalltalk

Now we can use Smalltalk message sends instead of VM primitives to expand the system!

### 1. Define Message Objects

```smalltalk
"Create Message class for reflective dispatch"
Object subclass: #Message
    instanceVariableNames: 'selector arguments'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'.

"Install methods using Smalltalk"
Message compile: 'selector
    ^selector'.

Message compile: 'arguments
    ^arguments'.

Message compile: 'sendTo: receiver
    ^receiver perform: selector withArguments: arguments'.

"Update doesNotUnderstand: to use Message"
ProtoObject compile: 'doesNotUnderstand: aMessage
    "Default error handling with proper Message object"
    self error: ''Message not understood: '', aMessage selector printString'.
```

### 2. Complete Control Structures

The Boolean hierarchy is already defined in MinimalBootstrap.st, but we can add more methods:

```smalltalk
"Add more Boolean methods using Smalltalk compilation"
Boolean compile: 'and: aBlock
    "Evaluating conjunction"
    self subclassResponsibility'.

Boolean compile: 'or: aBlock
    "Evaluating disjunction"
    self subclassResponsibility'.

True compile: 'and: aBlock
    ^aBlock value'.

True compile: 'or: aBlock
    ^true'.

False compile: 'and: aBlock
    ^false'.

False compile: 'or: aBlock
    ^aBlock value'.

"Add whileTrue: and whileFalse: to blocks"
BlockContext compile: 'whileTrue: aBlock
    "Evaluate aBlock while receiver evaluates to true"
    [self value] whileTrue: [aBlock value]'.
```

### 3. Introduce Collections

```smalltalk
"Create OrderedCollection"
Object subclass: #OrderedCollection
    instanceVariableNames: 'array firstIndex lastIndex'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Sequenceable'.

OrderedCollection compile: 'initialize
    array := Array new: 10.
    firstIndex := 5.
    lastIndex := 4'.

OrderedCollection compile: 'add: anObject
    lastIndex := lastIndex + 1.
    lastIndex > array size ifTrue: [self grow].
    array at: lastIndex put: anObject.
    ^anObject'.

OrderedCollection compile: 'size
    ^lastIndex - firstIndex + 1'.

"Create String as bytes"
Object subclass: #String
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Collections-Text'.

String compile: 'at: index
    <primitive: 63>
    self primitiveFailed'.

String compile: 'at: index put: aCharacter
    <primitive: 64>
    self primitiveFailed'.

String compile: ', aString
    "Concatenation"
    | newString |
    newString := String new: self size + aString size.
    1 to: self size do: [:i | newString at: i put: (self at: i)].
    1 to: aString size do: [:i | newString at: self size + i put: (aString at: i)].
    ^newString'.
```

### 4. Build Compiler Infrastructure

```smalltalk
"Create Compiler stub"
Object subclass: #Compiler
    instanceVariableNames: 'sourceStream class category'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'.

Compiler compile: 'compile: sourceString in: aClass classified: aCategory
    "Compile source and install in class"
    | method |
    method := self parse: sourceString.
    aClass addSelector: method selector withMethod: method.
    ^method selector'.

"Create Parser stub"
Object subclass: #Parser
    instanceVariableNames: 'source currentToken position'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'.

Parser compile: 'parse: sourceString
    "Parse source into CompiledMethod"
    source := sourceString.
    position := 1.
    ^self parseMethod'.

"Create Scanner stub"
Object subclass: #Scanner
    instanceVariableNames: 'source position currentChar'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Compiler'.

Scanner compile: 'scan
    "Return next token"
    self skipWhitespace.
    currentChar isLetter ifTrue: [^self scanIdentifier].
    currentChar isDigit ifTrue: [^self scanNumber].
    ^self scanSpecial'.
```

### 5. Create System Dictionary

```smalltalk
"Create SystemDictionary as special Dictionary"
Dictionary subclass: #SystemDictionary
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'System-Support'.

"Create the global Smalltalk dictionary"
| smalltalk |
smalltalk := SystemDictionary new.

"Register all classes"
smalltalk at: #ProtoObject put: ProtoObject.
smalltalk at: #Object put: Object.
smalltalk at: #Behavior put: Behavior.
smalltalk at: #ClassDescription put: ClassDescription.
smalltalk at: #Class put: Class.
smalltalk at: #Metaclass put: Metaclass.
smalltalk at: #Symbol put: Symbol.
smalltalk at: #Dictionary put: Dictionary.
smalltalk at: #Array put: Array.
smalltalk at: #True put: True.
smalltalk at: #False put: False.
smalltalk at: #UndefinedObject put: UndefinedObject.
smalltalk at: #SmallInteger put: SmallInteger.

"Register globals"
smalltalk at: #Smalltalk put: smalltalk.
smalltalk at: #nil put: nil.
smalltalk at: #true put: true.
smalltalk at: #false put: false.
```

### 6. Add Block Support

```smalltalk
"Create BlockContext for closures"
Object subclass: #BlockContext
    instanceVariableNames: 'nargs startpc home'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Methods'.

BlockContext compile: 'value
    "Evaluate block with no arguments"
    <primitive: 81>
    self primitiveFailed'.

BlockContext compile: 'value: arg1
    "Evaluate block with one argument"
    <primitive: 81>
    self primitiveFailed'.

BlockContext compile: 'value: arg1 value: arg2
    "Evaluate block with two arguments"
    <primitive: 81>
    self primitiveFailed'.
```

### 7. Implement Number Hierarchy

```smalltalk
"Create Number abstract class"
Object subclass: #Number
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-Numbers'.

Number compile: '+ aNumber
    "Addition - must be implemented by subclasses"
    self subclassResponsibility'.

Number compile: '* aNumber
    "Multiplication - must be implemented by subclasses"
    self subclassResponsibility'.

Number compile: 'negated
    "Answer the negation of the receiver"
    ^0 - self'.

"SmallInteger already defined, add more methods"
SmallInteger compile: '* aNumber
    "Multiply by aNumber"
    <primitive: 9>
    self primitiveFailed'.

SmallInteger compile: '// aNumber
    "Integer division"
    <primitive: 12>
    self primitiveFailed'.

SmallInteger compile: '\\\\ aNumber
    "Modulo"
    <primitive: 11>
    self primitiveFailed'.
```

## 🎯 Stage 2 Execution Strategy

### From C# Side

```csharp
public class Stage2Bootstrap
{
    private ISmalltalkVM vm;
    
    public void ExecuteStage2()
    {
        // After Stage 1, we can now send Smalltalk messages!
        
        // Load Stage2Bootstrap.st using Smalltalk's own mechanisms
        LoadStage2File("Stage2Bootstrap.st");
        
        // Create system dictionary using Smalltalk
        CreateSystemDictionary();
        
        // Install additional methods
        InstallExtendedProtocol();
    }
    
    private void LoadStage2File(string filename)
    {
        var source = File.ReadAllText(filename);
        
        // Use Smalltalk's compiler (once available)
        // Or continue using VM primitives for method installation
        vm.PrimitiveInstallMethod(classId, selector, source);
    }
    
    private void CreateSystemDictionary()
    {
        // Create SystemDictionary instance
        int sysDictClass = vm.PrimitiveLookupClass("SystemDictionary");
        int smalltalk = vm.PrimitiveNewInstance(sysDictClass);
        
        // Register in globals
        vm.PrimitiveRegisterGlobal("Smalltalk", smalltalk);
        
        // Populate with class bindings
        RegisterAllClasses(smalltalk);
    }
}
```

### Stage2Bootstrap.st File

```smalltalk
"Stage 2 Bootstrap - Expanding the system using Smalltalk"

"First, ensure we have class creation ability"
Behavior compile: 'subclass: className
    instanceVariableNames: instVarString
    classVariableNames: classVarString
    poolDictionaries: poolString
    category: categoryName
    
    "Create a subclass using Smalltalk messages"
    | newClass |
    newClass := Class new.
    newClass superclass: self.
    newClass name: className.
    newClass instanceVariables: (instVarString subStrings).
    newClass classVariables: (classVarString subStrings).
    newClass category: categoryName.
    Smalltalk at: className put: newClass.
    ^newClass'.

"Now we can create classes using Smalltalk!"

"... rest of Stage 2 definitions ..."
```

## 📊 Stage 2 Accomplishments

After Stage 2, the system will have:

✅ **Message objects** for reflective dispatch
✅ **Complete Boolean** control structures
✅ **Basic collections** (Array, OrderedCollection, String)
✅ **Compiler stubs** ready for enhancement
✅ **System dictionary** with all class bindings
✅ **Block contexts** for closures
✅ **Extended number** protocol

## 🚀 Transition to Stage 3

With Stage 2 complete, the system is ready for:

1. **Full compiler implementation** - Parse and compile Smalltalk source
2. **Stream classes** - For I/O and text processing
3. **File system** access via primitives
4. **Graphics subsystem** initialization
5. **Development tools** (Browser, Workspace, Debugger)

The key achievement: **The system can now expand itself using Smalltalk**, reducing dependence on VM primitives and C# orchestration.

## Critical Insight

Stage 2 represents the transition from:
- **External orchestration** (C# calling VM primitives)
- To **Internal growth** (Smalltalk expanding itself)

This is the moment where Smalltalk becomes self-hosting and can bootstrap the rest of the system using its own mechanisms!