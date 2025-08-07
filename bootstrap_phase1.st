"================================================================================================"
"  bootstrap_phase1.st - Symbolic Description of Phase 1 VM Primitive Operations"
"  This file documents what needs to be done but is NOT executed as Smalltalk code"
"  Instead, it will be converted to bootstrap_phase1.json for VM primitive execution"
"================================================================================================"

"================================================================================================"
"  STEP 1: Create the self-referential Metaclass"
"================================================================================================"

"Create Metaclass (self-referential)"
| Metaclass |
Metaclass := VMPrimitive newClass.
VMPrimitive setSlot: Metaclass slot: 0 to: Metaclass.  "class pointer points to itself"
VMPrimitive setNamedSlot: Metaclass name: #name to: #Metaclass.
VMPrimitive setNamedSlot: Metaclass name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Metaclass name: #format to: 0.

"================================================================================================"
"  STEP 2: Create Class"
"================================================================================================"

| Class |
Class := VMPrimitive newClass.
VMPrimitive setSlot: Class slot: 0 to: Metaclass.  "class pointer"
VMPrimitive setNamedSlot: Class name: #name to: #Class.
VMPrimitive setNamedSlot: Class name: #superclass to: nil.  "temporary, will be Behavior"
VMPrimitive setNamedSlot: Class name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Class name: #format to: 0.

"Now fix Metaclass's superclass to be Class"
VMPrimitive setNamedSlot: Metaclass name: #superclass to: Class.

"================================================================================================"
"  STEP 3: Create Behavior"
"================================================================================================"

| Behavior |
Behavior := VMPrimitive newClass.
VMPrimitive setSlot: Behavior slot: 0 to: Class.  "Behavior is an instance of Class"
VMPrimitive setNamedSlot: Behavior name: #name to: #Behavior.
VMPrimitive setNamedSlot: Behavior name: #superclass to: nil.  "temporary, will be Object"
VMPrimitive setNamedSlot: Behavior name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Behavior name: #instanceVariables to: (VMPrimitive newArray: 0).
VMPrimitive setNamedSlot: Behavior name: #format to: 0.

"Fix Class's superclass to be Behavior"
VMPrimitive setNamedSlot: Class name: #superclass to: Behavior.

"================================================================================================"
"  STEP 4: Create ProtoObject (the root class)"
"================================================================================================"

"First create ProtoObject's metaclass"
| ProtoObjectMetaclass |
ProtoObjectMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: ProtoObjectMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: ProtoObjectMetaclass name: #name to: #'ProtoObject class'.
VMPrimitive setNamedSlot: ProtoObjectMetaclass name: #superclass to: Class.  "ProtoObject class → Class"
VMPrimitive setNamedSlot: ProtoObjectMetaclass name: #methodDict to: (VMPrimitive newDictionary).

"Create ProtoObject"
| ProtoObject |
ProtoObject := VMPrimitive newClass.
VMPrimitive setSlot: ProtoObject slot: 0 to: ProtoObjectMetaclass.
VMPrimitive setNamedSlot: ProtoObject name: #name to: #ProtoObject.
VMPrimitive setNamedSlot: ProtoObject name: #superclass to: nil.  "ProtoObject is the root"
VMPrimitive setNamedSlot: ProtoObject name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: ProtoObject name: #instanceVariables to: (VMPrimitive newArray: 0).
VMPrimitive setNamedSlot: ProtoObject name: #format to: 0.

"================================================================================================"
"  STEP 5: Create Object"
"================================================================================================"

"First create Object's metaclass"
| ObjectMetaclass |
ObjectMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: ObjectMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: ObjectMetaclass name: #name to: #'Object class'.
VMPrimitive setNamedSlot: ObjectMetaclass name: #superclass to: ProtoObjectMetaclass.
VMPrimitive setNamedSlot: ObjectMetaclass name: #methodDict to: (VMPrimitive newDictionary).

"Create Object"
| Object |
Object := VMPrimitive newClass.
VMPrimitive setSlot: Object slot: 0 to: ObjectMetaclass.
VMPrimitive setNamedSlot: Object name: #name to: #Object.
VMPrimitive setNamedSlot: Object name: #superclass to: ProtoObject.
VMPrimitive setNamedSlot: Object name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Object name: #instanceVariables to: (VMPrimitive newArray: 0).
VMPrimitive setNamedSlot: Object name: #format to: 0.

"================================================================================================"
"  STEP 6: Fix Behavior's superclass to Object"
"================================================================================================"

VMPrimitive setNamedSlot: Behavior name: #superclass to: Object.

"Create Behavior's metaclass and fix the metaclass chain"
| BehaviorMetaclass |
BehaviorMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: BehaviorMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: BehaviorMetaclass name: #name to: #'Behavior class'.
VMPrimitive setNamedSlot: BehaviorMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: BehaviorMetaclass name: #methodDict to: (VMPrimitive newDictionary).

"Fix Behavior's class pointer"
VMPrimitive setSlot: Behavior slot: 0 to: BehaviorMetaclass.

"================================================================================================"
"  STEP 7: Create ClassDescription"
"================================================================================================"

"Create ClassDescription's metaclass"
| ClassDescriptionMetaclass |
ClassDescriptionMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: ClassDescriptionMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: ClassDescriptionMetaclass name: #name to: #'ClassDescription class'.
VMPrimitive setNamedSlot: ClassDescriptionMetaclass name: #superclass to: BehaviorMetaclass.
VMPrimitive setNamedSlot: ClassDescriptionMetaclass name: #methodDict to: (VMPrimitive newDictionary).

"Create ClassDescription"
| ClassDescription |
ClassDescription := VMPrimitive newClass.
VMPrimitive setSlot: ClassDescription slot: 0 to: ClassDescriptionMetaclass.
VMPrimitive setNamedSlot: ClassDescription name: #name to: #ClassDescription.
VMPrimitive setNamedSlot: ClassDescription name: #superclass to: Behavior.
VMPrimitive setNamedSlot: ClassDescription name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: ClassDescription name: #instanceVariables to: (VMPrimitive newArray: 0).
VMPrimitive setNamedSlot: ClassDescription name: #format to: 0.

"Fix Class's superclass to be ClassDescription"
VMPrimitive setNamedSlot: Class name: #superclass to: ClassDescription.

"Fix Class's metaclass"
| ClassMetaclass |
ClassMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: ClassMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: ClassMetaclass name: #name to: #'Class class'.
VMPrimitive setNamedSlot: ClassMetaclass name: #superclass to: ClassDescriptionMetaclass.
VMPrimitive setNamedSlot: ClassMetaclass name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setSlot: Class slot: 0 to: ClassMetaclass.

"================================================================================================"
"  STEP 8: Create essential support classes"
"================================================================================================"

"Create UndefinedObject for nil"
| UndefinedObjectMetaclass UndefinedObject |
UndefinedObjectMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: UndefinedObjectMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: UndefinedObjectMetaclass name: #name to: #'UndefinedObject class'.
VMPrimitive setNamedSlot: UndefinedObjectMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: UndefinedObjectMetaclass name: #methodDict to: (VMPrimitive newDictionary).

UndefinedObject := VMPrimitive newClass.
VMPrimitive setSlot: UndefinedObject slot: 0 to: UndefinedObjectMetaclass.
VMPrimitive setNamedSlot: UndefinedObject name: #name to: #UndefinedObject.
VMPrimitive setNamedSlot: UndefinedObject name: #superclass to: Object.
VMPrimitive setNamedSlot: UndefinedObject name: #methodDict to: (VMPrimitive newDictionary).

"Create Boolean hierarchy"
| BooleanMetaclass Boolean |
BooleanMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: BooleanMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: BooleanMetaclass name: #name to: #'Boolean class'.
VMPrimitive setNamedSlot: BooleanMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: BooleanMetaclass name: #methodDict to: (VMPrimitive newDictionary).

Boolean := VMPrimitive newClass.
VMPrimitive setSlot: Boolean slot: 0 to: BooleanMetaclass.
VMPrimitive setNamedSlot: Boolean name: #name to: #Boolean.
VMPrimitive setNamedSlot: Boolean name: #superclass to: Object.
VMPrimitive setNamedSlot: Boolean name: #methodDict to: (VMPrimitive newDictionary).

| TrueMetaclass True |
TrueMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: TrueMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: TrueMetaclass name: #name to: #'True class'.
VMPrimitive setNamedSlot: TrueMetaclass name: #superclass to: BooleanMetaclass.
VMPrimitive setNamedSlot: TrueMetaclass name: #methodDict to: (VMPrimitive newDictionary).

True := VMPrimitive newClass.
VMPrimitive setSlot: True slot: 0 to: TrueMetaclass.
VMPrimitive setNamedSlot: True name: #name to: #True.
VMPrimitive setNamedSlot: True name: #superclass to: Boolean.
VMPrimitive setNamedSlot: True name: #methodDict to: (VMPrimitive newDictionary).

| FalseMetaclass False |
FalseMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: FalseMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: FalseMetaclass name: #name to: #'False class'.
VMPrimitive setNamedSlot: FalseMetaclass name: #superclass to: BooleanMetaclass.
VMPrimitive setNamedSlot: FalseMetaclass name: #methodDict to: (VMPrimitive newDictionary).

False := VMPrimitive newClass.
VMPrimitive setSlot: False slot: 0 to: FalseMetaclass.
VMPrimitive setNamedSlot: False name: #name to: #False.
VMPrimitive setNamedSlot: False name: #superclass to: Boolean.
VMPrimitive setNamedSlot: False name: #methodDict to: (VMPrimitive newDictionary).

"Create Symbol"
| SymbolMetaclass Symbol |
SymbolMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: SymbolMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: SymbolMetaclass name: #name to: #'Symbol class'.
VMPrimitive setNamedSlot: SymbolMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: SymbolMetaclass name: #methodDict to: (VMPrimitive newDictionary).

Symbol := VMPrimitive newClass.
VMPrimitive setSlot: Symbol slot: 0 to: SymbolMetaclass.
VMPrimitive setNamedSlot: Symbol name: #name to: #Symbol.
VMPrimitive setNamedSlot: Symbol name: #superclass to: Object.
VMPrimitive setNamedSlot: Symbol name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Symbol name: #instanceVariables to: #(value).
VMPrimitive setNamedSlot: Symbol name: #format to: 2.  "bytes format"

"Create Array"
| ArrayMetaclass Array |
ArrayMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: ArrayMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: ArrayMetaclass name: #name to: #'Array class'.
VMPrimitive setNamedSlot: ArrayMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: ArrayMetaclass name: #methodDict to: (VMPrimitive newDictionary).

Array := VMPrimitive newClass.
VMPrimitive setSlot: Array slot: 0 to: ArrayMetaclass.
VMPrimitive setNamedSlot: Array name: #name to: #Array.
VMPrimitive setNamedSlot: Array name: #superclass to: Object.
VMPrimitive setNamedSlot: Array name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Array name: #format to: 1.  "variable format"

"Create Dictionary"
| DictionaryMetaclass Dictionary |
DictionaryMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: DictionaryMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: DictionaryMetaclass name: #name to: #'Dictionary class'.
VMPrimitive setNamedSlot: DictionaryMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: DictionaryMetaclass name: #methodDict to: (VMPrimitive newDictionary).

Dictionary := VMPrimitive newClass.
VMPrimitive setSlot: Dictionary slot: 0 to: DictionaryMetaclass.
VMPrimitive setNamedSlot: Dictionary name: #name to: #Dictionary.
VMPrimitive setNamedSlot: Dictionary name: #superclass to: Object.
VMPrimitive setNamedSlot: Dictionary name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: Dictionary name: #instanceVariables to: #(keys values).

"Create MethodDictionary"
| MethodDictionaryMetaclass MethodDictionary |
MethodDictionaryMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: MethodDictionaryMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: MethodDictionaryMetaclass name: #name to: #'MethodDictionary class'.
VMPrimitive setNamedSlot: MethodDictionaryMetaclass name: #superclass to: DictionaryMetaclass.
VMPrimitive setNamedSlot: MethodDictionaryMetaclass name: #methodDict to: (VMPrimitive newDictionary).

MethodDictionary := VMPrimitive newClass.
VMPrimitive setSlot: MethodDictionary slot: 0 to: MethodDictionaryMetaclass.
VMPrimitive setNamedSlot: MethodDictionary name: #name to: #MethodDictionary.
VMPrimitive setNamedSlot: MethodDictionary name: #superclass to: Dictionary.
VMPrimitive setNamedSlot: MethodDictionary name: #methodDict to: (VMPrimitive newDictionary).

"Create CompiledMethod"
| CompiledMethodMetaclass CompiledMethod |
CompiledMethodMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: CompiledMethodMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: CompiledMethodMetaclass name: #name to: #'CompiledMethod class'.
VMPrimitive setNamedSlot: CompiledMethodMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: CompiledMethodMetaclass name: #methodDict to: (VMPrimitive newDictionary).

CompiledMethod := VMPrimitive newClass.
VMPrimitive setSlot: CompiledMethod slot: 0 to: CompiledMethodMetaclass.
VMPrimitive setNamedSlot: CompiledMethod name: #name to: #CompiledMethod.
VMPrimitive setNamedSlot: CompiledMethod name: #superclass to: Object.
VMPrimitive setNamedSlot: CompiledMethod name: #methodDict to: (VMPrimitive newDictionary).
VMPrimitive setNamedSlot: CompiledMethod name: #format to: 3.  "compiled method format"

"Create SmallInteger"
| SmallIntegerMetaclass SmallInteger |
SmallIntegerMetaclass := VMPrimitive newClass.
VMPrimitive setSlot: SmallIntegerMetaclass slot: 0 to: Metaclass.
VMPrimitive setNamedSlot: SmallIntegerMetaclass name: #name to: #'SmallInteger class'.
VMPrimitive setNamedSlot: SmallIntegerMetaclass name: #superclass to: ObjectMetaclass.
VMPrimitive setNamedSlot: SmallIntegerMetaclass name: #methodDict to: (VMPrimitive newDictionary).

SmallInteger := VMPrimitive newClass.
VMPrimitive setSlot: SmallInteger slot: 0 to: SmallIntegerMetaclass.
VMPrimitive setNamedSlot: SmallInteger name: #name to: #SmallInteger.
VMPrimitive setNamedSlot: SmallInteger name: #superclass to: Object.
VMPrimitive setNamedSlot: SmallInteger name: #methodDict to: (VMPrimitive newDictionary).

"================================================================================================"
"  STEP 9: Install critical primitive methods"
"================================================================================================"

VMPrimitive installMethod: ProtoObject 
    selector: #doesNotUnderstand: 
    source: 'doesNotUnderstand: aMessage
        <primitive: 999>
        self error: ''Message not understood'''.

VMPrimitive installMethod: Object
    selector: #class
    source: 'class
        <primitive: 111>
        ^self primitiveFailed'.

VMPrimitive installMethod: Object
    selector: #==
    source: '== anObject
        <primitive: 110>
        ^self primitiveFailed'.

VMPrimitive installMethod: Object
    selector: #perform:
    source: 'perform: aSymbol
        <primitive: 83>
        ^self primitiveFailed'.

VMPrimitive installMethod: Object
    selector: #perform:with:
    source: 'perform: aSymbol with: anObject
        <primitive: 84>
        ^self primitiveFailed'.

VMPrimitive installMethod: Behavior
    selector: #new
    source: 'new
        <primitive: 70>
        ^self basicNew initialize'.

VMPrimitive installMethod: Behavior
    selector: #basicNew
    source: 'basicNew
        <primitive: 70>
        ^self primitiveFailed'.

VMPrimitive installMethod: Behavior
    selector: #basicNew:
    source: 'basicNew: size
        <primitive: 71>
        ^self primitiveFailed'.

VMPrimitive installMethod: Array
    selector: #at:
    source: 'at: index
        <primitive: 60>
        ^self primitiveFailed'.

VMPrimitive installMethod: Array
    selector: #at:put:
    source: 'at: index put: value
        <primitive: 61>
        ^self primitiveFailed'.

VMPrimitive installMethod: Array
    selector: #size
    source: 'size
        <primitive: 62>
        ^self primitiveFailed'.

VMPrimitive installMethod: True
    selector: #ifTrue:
    source: 'ifTrue: trueBlock
        ^trueBlock value'.

VMPrimitive installMethod: False
    selector: #ifFalse:
    source: 'ifFalse: falseBlock
        ^falseBlock value'.

"================================================================================================"
"  STEP 10: Create global objects"
"================================================================================================"

| nil true false Smalltalk |
nil := VMPrimitive newInstance: UndefinedObject.
true := VMPrimitive newInstance: True.
false := VMPrimitive newInstance: False.
Smalltalk := VMPrimitive newInstance: Dictionary.

VMPrimitive registerGlobal: #nil value: nil.
VMPrimitive registerGlobal: #true value: true.
VMPrimitive registerGlobal: #false value: false.
VMPrimitive registerGlobal: #Smalltalk value: Smalltalk.

"Register all classes in Smalltalk dictionary"
VMPrimitive at: Smalltalk put: #ProtoObject value: ProtoObject.
VMPrimitive at: Smalltalk put: #Object value: Object.
VMPrimitive at: Smalltalk put: #Behavior value: Behavior.
VMPrimitive at: Smalltalk put: #ClassDescription value: ClassDescription.
VMPrimitive at: Smalltalk put: #Class value: Class.
VMPrimitive at: Smalltalk put: #Metaclass value: Metaclass.
VMPrimitive at: Smalltalk put: #UndefinedObject value: UndefinedObject.
VMPrimitive at: Smalltalk put: #Boolean value: Boolean.
VMPrimitive at: Smalltalk put: #True value: True.
VMPrimitive at: Smalltalk put: #False value: False.
VMPrimitive at: Smalltalk put: #Symbol value: Symbol.
VMPrimitive at: Smalltalk put: #Array value: Array.
VMPrimitive at: Smalltalk put: #Dictionary value: Dictionary.
VMPrimitive at: Smalltalk put: #MethodDictionary value: MethodDictionary.
VMPrimitive at: Smalltalk put: #CompiledMethod value: CompiledMethod.
VMPrimitive at: Smalltalk put: #SmallInteger value: SmallInteger.

"================================================================================================"
"  END OF PHASE 1: Minimal class lattice established via VM primitives"
"================================================================================================"