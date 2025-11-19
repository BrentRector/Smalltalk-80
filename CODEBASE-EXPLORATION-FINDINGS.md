# Smalltalk-80 Interpreter Codebase Exploration Report

## 1. PROJECT STRUCTURE OVERVIEW

### Main Directories

```
/home/user/Smalltalk-80/
├── Smalltalk80.Compiler/           # Offline compiler (compiles Smalltalk source to JSON)
├── Smalltalk80.Interpreter/        # Runtime interpreter and cold start
│   ├── Core/VM/                    # Virtual machine execution engine
│   │   ├── Primitives/             # Primitive operations (60+ primitives)
│   │   ├── Interpreter/            # Bytecode interpreter loop
│   │   └── Caching/                # Method and class caches
│   ├── Engine/LoaderPhases/        # Cold start phases 1-6
│   ├── Core/HeapManager/           # Object memory management
│   └── Core/Memory/                # Special objects, symbol table
├── Smalltalk-80 Source/            # Original Smalltalk source code
└── docs/
    ├── Architecture/               # Architecture documentation
    ├── Font-and-UI-Architecture.md # UI implementation roadmap
    └── POOL-VARIABLE-IMPLEMENTATION.md # Pool dictionary handling
```

### Key Component Files

**Cold Start (Bootstrap)**:
- Phase1: Symbol table creation
- Phase2: Class infrastructure (class hierarchy, metaclasses)
- Phase3: Object allocation (nil, true, false, special objects)
- Phase4: Method compilation (convert JSON bytecode to heap objects)
- Phase5: Globals installation, DefaultTextStyle, Display creation
- Phase6: Validation and GC

## 2. PROJECT STATUS

### Interpreter Status (per CLAUDE.md)
- **Build**: 0 errors, 0 warnings
- **Cold Start**: Phases 1-6 all succeed
- **Init Expressions**: 31/46 pass (67%)
- **BlockContext Tests**: 120/120 tests PASS (returns 1191)
- **VM Core**: Fully functional, verified operational

### Font/Text Status
- ✅ DefaultTextStyle created in Phase 5 (RuntimeObjectCreator.CreateDefaultTextStyle)
- ✅ StrikeFont stubs created with minimal properties
- ✅ TextStyle initialized with fontArray
- ✅ TextConstants pool dictionary support implemented
- ❌ Actual font rendering/glyphs not yet implemented

## 3. PRIMITIVES IMPLEMENTATION

### Primitive Architecture

**File**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/`

**Pattern**: IPrimitive Interface

```csharp
public interface IPrimitive
{
    PrimitiveResult Execute(PrimitiveExecutionContext context);
}

public readonly struct PrimitiveResult
{
    public bool Success { get; }
    public SmalltalkOOP Value { get; }
    
    public static PrimitiveResult Ok(SmalltalkOOP value) => new(true, value);
    public static PrimitiveResult Fail() => new(false, SpecialObjectsArray.Instance.Nil);
    public static PrimitiveResult FromBool(bool condition) => ...
}

public readonly struct PrimitiveExecutionContext
{
    public SmalltalkOOP Receiver { get; }
    public SmalltalkOOP[] Arguments { get; }
    public ISmalltalkHeapManager HeapManager { get; }
    public IStackManager? StackManager { get; }
    public IContextManager? ContextManager { get; }
    public LayoutResolver? LayoutResolver { get; }
    public IPlatformBridge? Platform { get; }
    public RuntimeHeapAccessor? RuntimeHeapAccess { get; }
    // ... other dependencies for different primitive types
}
```

### Registered Primitives (60+ total)

**SmallInteger Arithmetic** (1-17):
- 1-2: Add, Subtract
- 3-8: Comparisons (LT, GT, LE, GE, EQ, NE)
- 9-13: Multiply, Divide, Mod, Div, Quo
- 14-17: BitAnd, BitOr, BitXor, BitShift

**Point Primitives** (18):
- 18: MakePoint (creates Point from x,y)

**LargeInteger Arithmetic** (21-37): Same operations as SmallInteger

**Float Arithmetic** (40-54):
- 40: AsFloat
- 41-48: Arithmetic and comparisons
- 49-54: Multiply, Divide, Truncated, FractionPart, Exponent, TimesTwoPower

**Object Primitives** (60-79, 110-111):
- 60-61: At (read indexed), AtPut (write indexed)
- 62: Size (indexed size)
- 63-66: String operations (StringAt, StringAtPut, Next, NextPut)
- 68-69: ObjectAt, ObjectAtPut
- 70-71: New, NewWithSize
- 72: Become
- 73-74: InstVarAt, InstVarAtPut
- 75: Hash
- 76-79: AsObject, SomeInstance, NextInstance, NewMethodHeader
- 110: IdentityEquals (==)
- 111: Class

**Control Primitives** (80-89):
- 80: BlockCopy (primitive 80 - creates block closure)
- 81-82: Value, ValueWithArguments (execute blocks)
- 83-84: Perform, PerformWith (dynamic message sending)
- 85-88: Signal, Wait, Resume, Suspend (exception handling)
- 89: FlushCache (clear method cache)

**Input Primitives** (90-95):
- 90: MousePoint
- 91: CursorLocPut
- 92: CursorLink
- 93: InputSemaphore
- 94: SampleInterval
- 95: InputWord

**Graphics Primitives** (96-104):
- 96: CopyBits (BitBlt - core graphics operation)
- 97: Snapshot
- 98: OopsLeft
- 99: CoreLeft
- 100: TimeWords
- 101: BeDisplay
- 102: BeCursor
- 103: Beep
- 104: ClipboardText

**String Primitives** (105-106):
- 105: StringReplace
- 106: AsSymbol

**System Primitives** (112-139):
- 112-116: CoreLeft, Quit, ExitToDebugger, OopsLeft, SignalAtOopsLeftWordsLeft
- 128-131: SomeInstance, NextInstance, BecomeForward, InstanceCount
- 133: ForceGC
- 139: NextObject

### Example: How to Implement a Simple Primitive

**File**: `ObjectPrimitives.cs`

```csharp
/// <summary>Primitive 62: Object size - returns number of indexed elements</summary>
public class Size : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
        if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();

        int size = context.HeapManager.GetIndexedSize(context.Receiver);
        return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(size));
    }
}

/// <summary>Primitive 111: Object class</summary>
public class Class : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

        SmalltalkOOP classOOP;
        if (context.Receiver.IsSmallInteger)
        {
            classOOP = context.RuntimeHeapAccess?.GetClass("SmallInteger")
                ?? SpecialObjectsArray.Instance.Nil;
        }
        else
        {
            classOOP = context.HeapManager.GetClassOOP(context.Receiver);
        }
        return PrimitiveResult.Ok(classOOP);
    }
}
```

### Primitive Registration

**File**: `PrimitiveRegistry.cs`

```csharp
public static PrimitiveRegistry CreateWithNewPrimitives()
{
    var registry = new PrimitiveRegistry();
    
    // Register each primitive with a number and implementation
    registry.Register(1, new SmallIntegerArithmetic.Add());
    registry.Register(2, new SmallIntegerArithmetic.Subtract());
    // ... 60+ more registrations
    registry.Register(96, new GraphicsOperations.CopyBits());
    // ...
    return registry;
}
```

### Primitive Execution Flow

```
1. VM encounters primitive bytecode (e.g., primitive 62)
2. VM calls PrimitiveRegistry.Execute(62, context)
3. Registry looks up IPrimitive implementation
4. Context is created with:
   - Receiver object
   - Arguments array
   - HeapManager (memory access)
   - Platform bridge (if available)
   - RuntimeHeapAccessor (for class/global lookups)
   - Various managers for special primitives
5. Primitive.Execute(context) returns PrimitiveResult
6. VM sets PrimitiveSuccess flag from result.Success
7. If failed, VM executes fallback Smalltalk code
```

## 4. OBJECT CREATION FROM C#

### Key Classes for Object Instantiation

**File**: `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/RuntimeObjectCreator.cs`

This file provides the canonical pattern for creating Smalltalk objects from C# during runtime:

```csharp
// Step 1: Allocate the object
var objectOOP = context.Allocator.AllocateFixedObject("StrikeFont");

// Step 2: Get the class OOP and set it
var classOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
context.Allocator.SetObjectClass(objectOOP, classOOP);

// Step 3: Initialize fields
int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(
    "StrikeFont", context.Bundle);
for (int i = 0; i < instVarCount; i++)
{
    context.Allocator.SetInstanceVariable(objectOOP, i, nilOOP);
    context.InitializedFields.Add((objectOOP, i));
}

// Step 4: Set specific instance variable values
SetInstanceVarByName(objectOOP, "StrikeFont", "name", InternString("SystemFont"));
SetInstanceVarByName(objectOOP, "StrikeFont", "ascent", SmalltalkOOP.FromSmallInteger(10));
SetInstanceVarByName(objectOOP, "StrikeFont", "descent", SmalltalkOOP.FromSmallInteger(3));
SetInstanceVarByName(objectOOP, "StrikeFont", "maxWidth", SmalltalkOOP.FromSmallInteger(8));
```

### Helper Methods for Field Access

```csharp
private void SetInstanceVarByName(SmalltalkOOP objectOOP, string className, 
                                  string varName, SmalltalkOOP value)
{
    int index = ClassHierarchyTraversal.FindInstanceVariableIndex(
        className, varName, context.Bundle);
    if (index >= 0)
    {
        context.Allocator.SetInstanceVariable(objectOOP, index, value);
        context.InitializedFields.Add((objectOOP, index));
    }
}

private SmalltalkOOP InternString(string value)
{
    byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);
    var stringOOP = context.Allocator.AllocateString("String", stringBytes.Length);
    
    for (int i = 0; i < stringBytes.Length; i++)
    {
        context.Allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
    }
    
    var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
    context.Allocator.SetObjectClass(stringOOP, stringClassOOP);
    
    return stringOOP;
}
```

### DefaultTextStyle Creation Example

**File**: `RuntimeObjectCreator.cs` (lines 23-94)

Complete example of creating a complex object hierarchy:

```csharp
public void CreateDefaultTextStyle()
{
    var nilOOP = SpecialObjectsArray.Instance.Nil;

    // Step 1: Create stub StrikeFont
    var strikeFontOOP = context.Allocator.AllocateFixedObject("StrikeFont");
    var strikeFontClassOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
    context.Allocator.SetObjectClass(strikeFontOOP, strikeFontClassOOP);

    // Initialize all fields to nil
    int strikeFontInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(
        "StrikeFont", context.Bundle);
    for (int i = 0; i < strikeFontInstVarCount; i++)
    {
        context.Allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
        context.InitializedFields.Add((strikeFontOOP, i));
    }

    // Set key properties
    SetInstanceVarByName(strikeFontOOP, "StrikeFont", "name", InternString("SystemFont"));
    SetInstanceVarByName(strikeFontOOP, "StrikeFont", "ascent", SmalltalkOOP.FromSmallInteger(10));
    SetInstanceVarByName(strikeFontOOP, "StrikeFont", "descent", SmalltalkOOP.FromSmallInteger(3));
    SetInstanceVarByName(strikeFontOOP, "StrikeFont", "maxWidth", SmalltalkOOP.FromSmallInteger(8));

    // Step 2: Create array to hold the font
    var fontArrayOOP = context.Allocator.AllocateArray("Array", 1);
    context.Allocator.SetIndexedOOP(fontArrayOOP, 0, strikeFontOOP);
    var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
    context.Allocator.SetObjectClass(fontArrayOOP, arrayClassOOP);

    // Step 3: Create TextStyle
    var textStyleOOP = context.Allocator.AllocateFixedObject("TextStyle");
    var textStyleClassOOP = GlobalRegistry.LookupClassOOP("TextStyle");
    context.Allocator.SetObjectClass(textStyleOOP, textStyleClassOOP);

    // Initialize TextStyle fields
    SetInstanceVarByName(textStyleOOP, "TextStyle", "fontArray", fontArrayOOP);
    SetInstanceVarByName(textStyleOOP, "TextStyle", "lineGrid", SmalltalkOOP.FromSmallInteger(14));
    SetInstanceVarByName(textStyleOOP, "TextStyle", "baseline", SmalltalkOOP.FromSmallInteger(10));
    SetInstanceVarByName(textStyleOOP, "TextStyle", "alignment", SmalltalkOOP.FromSmallInteger(0));

    // Step 4: Register as global
    var defaultTextStyleAssoc = AssociationRegistry.GetAll()["DefaultTextStyle"];
    context.Allocator.SetInstanceVariable(defaultTextStyleAssoc, 1, textStyleOOP);
    context.InitializedFields.Add((defaultTextStyleAssoc, 1));

    GlobalRegistry.Register("DefaultTextStyle", textStyleOOP);
}
```

### Object Types and Allocation Patterns

**File**: `ObjectGraphAllocator.cs`

```csharp
// Fixed-size object (has only instance variables, no indexed data)
var objectOOP = allocator.AllocateFixedObject("Point");

// Indexed object (has instance variables + indexed elements)
var arrayOOP = allocator.AllocateArray("Array", 10);  // Array with 10 elements

// String object (byte-indexed, variable length)
var stringOOP = allocator.AllocateString("String", 50);  // 50 byte string

// Word-indexed object (bitmap data, e.g., for Forms)
var bitmapOOP = allocator.AllocateObject("DisplayBitmap", 
                                         ObjectFormat.WordIndexed, totalWords);
```

### Key Allocator Methods

```csharp
// Get the heap manager
ISmalltalkHeapManager heapManager = context.HeapManager;

// Get instance variable count
int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount(
    className, context.Bundle);

// Set instance variable value
heapManager.SetInstanceVariable(objectOOP, index, value);

// Set indexed element (for arrays)
heapManager.SetIndexedOOP(arrayOOP, index, value);

// Set indexed byte (for strings, bitmaps)
heapManager.SetIndexedByte(byteArrayOOP, index, byteValue);

// Set indexed word (for word-indexed objects)
heapManager.SetIndexedWord(wordArrayOOP, index, wordValue);

// Get object's class
SmalltalkOOP classOOP = heapManager.GetClassOOP(objectOOP);
```

## 5. TEXTCONSTANTS AND POOL DICTIONARIES

### Architecture

**File**: `/home/user/Smalltalk-80/docs/POOL-VARIABLE-IMPLEMENTATION.md`

**Key Concept**: Pool variables are **binding metadata**, NOT Association objects.

### How Pool Dictionaries Work

**In Smalltalk Code**:
```smalltalk
Object subclass: #Text
    instanceVariableNames: 'string runs'
    classVariableNames: ''
    poolDictionaries: 'TextConstants'
    category: 'Collections-Text'

Text>>initialize
    | tab |
    tab := DefaultTab.  "← Pool variable reference"
```

**TextConstants Structure**:
- Created as empty Dictionary during Phase 3
- Populated during init expressions (e.g., via TextConstants initialize)
- Contains key-value pairs: `#DefaultTab → 24`, `#Margin → 8`, etc.

**Runtime Lookup**:
```
pushLiteralVariable: i
  ↓
Fetch literal: Array(#TextConstants, #DefaultTab)
  ↓
If element[0] is Symbol (first execution):
  - Lookup TextConstants in Smalltalk globals
  - Lookup DefaultTab in TextConstants dictionary
  - Patch literal to Array(dictOOP, #DefaultTab) for fast path
  ↓
Return value
```

### How Pool Dictionaries Are Created (Phase 5)

**File**: `Phase5_GlobalsAndPatches.cs` (lines 653-741)

```csharp
// 1. Discover all pool dictionaries from class metadata
var poolToClasses = new Dictionary<string, List<string>>();
foreach (var cls in _bundle.Classes)
{
    foreach (var poolLocalId in cls.PoolLocalIds)  // e.g., "pool:FilePool"
    {
        string poolName = poolLocalId.Substring(5);  // Extract "FilePool"
        poolToClasses[poolName].Add(className);
    }
}

// 2. For each pool dictionary
foreach (var (poolName, declaringClasses) in poolToClasses)
{
    // Check if pre-allocated in bundle (Phase 3)
    string poolLocalId = $"pool:{poolName}";
    if (_localIdToObjectId.TryGetValue(poolLocalId, out var dictionaryOOP))
    {
        // Use pre-allocated empty Dictionary
    }
    else
    {
        // Create empty Dictionary
        dictionaryOOP = _allocator.AllocateObject("Dictionary", ...);
    }
    
    // 3. Create Association: #FilePool → <Dictionary>
    var associationOOP = GetOrCreateCanonicalAssociation(poolName);
    _allocator.SetInstanceVariable(associationOOP, 1, dictionaryOOP);
    
    // 4. Install as global in Smalltalk dictionary
    InstallAssociationInSystemDictionary(associationOOP, poolName);
    
    // 5. Register in GlobalRegistry for fast lookup
    GlobalRegistry.Register(poolName, dictionaryOOP);
}
```

### Key TextConstants Classes in Smalltalk Source

**Classes Using TextConstants Pool** (from Smalltalk-80.st):
- Line 7641: Text class
- Line 7723: DisplayText class
- Line 8031: Paragraph class
- Line 11497: Selector class
- Line 15206: Editor class
- Line 15480: StrikeFont class
- Line 28217: ParagraphEditor class
- Line 28967: TextEditor class

## 6. EXISTING FONT/TEXT INFRASTRUCTURE

### StrikeFont Class Definition (Smalltalk Source, line 35882)

```smalltalk
Object subclass: #StrikeFont
    instanceVariableNames: 'name ascent descent maxWidth minAscii maxAscii xTable glyphs'
    classVariableNames: ''
    poolDictionaries: 'TextConstants'
    category: 'Graphics-Fonts'

StrikeFont comment:
'StrikeFonts are a compact encoding of a set of Forms corresponding to characters
in the ASCII character set. All the forms are placed side by side in a large form
whose height is the font height, and whose width is the sum of all the character widths.
The xTable gives the left-x coordinates of the subForms corresponding to the characters.'
```

### StrikeFont Instance Variables
- `name`: String (font name, e.g., "SystemFont")
- `ascent`: SmallInteger (pixels above baseline)
- `descent`: SmallInteger (pixels below baseline)
- `maxWidth`: SmallInteger (maximum character width)
- `minAscii`: SmallInteger (first ASCII character, e.g., 32 for space)
- `maxAscii`: SmallInteger (last ASCII character, e.g., 126 for tilde)
- `xTable`: Array (character position lookup table) - Can be nil for stubs
- `glyphs`: Form (bitmap containing all character glyphs) - Can be nil for stubs

### TextStyle Class Definition

```smalltalk
Object subclass: #TextStyle
    instanceVariableNames: 'fontArray lineGrid baseline alignment firstIndent
                          restIndent rightIndent tabsArray marginTabsArray outputMedium'
    poolDictionaries: 'TextConstants'
```

### TextStyle Instance Variables
- `fontArray`: Array of StrikeFont objects
- `lineGrid`: SmallInteger (line spacing in pixels)
- `baseline`: SmallInteger (baseline position)
- `alignment`: SmallInteger (text alignment mode)
- Other typography properties

### Key Methods Accessing TextConstants

**TextStyle class>>default** (line 30098):
```smalltalk
font _ TextStyle default defaultFont.
tabWidth _ TextConstants at: #DefaultTab.
```

**PopUpMenu initialization** (line 31598-31635):
```smalltalk
style _ TextStyle fontArray: (Array with: font).
^ self new labels: aString font: (TextStyle default fontAt: 1) lines: anArray
```

## 7. KEY FILES REFERENCE GUIDE

### Primitives
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ObjectPrimitives.cs` - Object operations
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/GraphicsPrimitives.cs` - BitBlt
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/ControlPrimitives.cs` - Block execution
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/PrimitiveRegistry.cs` - Registration
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/Primitives/PrimitiveInterfaces.cs` - IPrimitive interface

### Object Creation
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/RuntimeObjectCreator.cs` - Runtime object creation
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase3_ObjectAllocation.cs` - Phase 3 allocation
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/HeapManager/` - Heap operations

### Pool Dictionaries
- `/home/user/Smalltalk-80/docs/POOL-VARIABLE-IMPLEMENTATION.md` - Complete specification
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/Phase5Orchestrator.cs` - Pool installation
- `/home/user/Smalltalk-80/Smalltalk80.Compiler/VariableResolver.cs` - Pool variable detection

### Font/UI
- `/home/user/Smalltalk-80/docs/Font-and-UI-Architecture.md` - Complete implementation roadmap
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase5/RuntimeObjectCreator.cs` - DefaultTextStyle creation

### Classes and Inheritance
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Core/VM/GlobalRegistry.cs` - Class registration
- `/home/user/Smalltalk-80/Smalltalk80.Interpreter/Engine/LoaderPhases/Phase2_ClassInfrastructure.cs` - Class hierarchy setup

## 8. IMPLEMENTATION PATTERNS

### Pattern 1: Simple Primitive Implementation

```csharp
public class MyPrimitive : IPrimitive
{
    public PrimitiveResult Execute(PrimitiveExecutionContext context)
    {
        // Validate argument count
        if (context.Arguments.Length != expectedCount)
            return PrimitiveResult.Fail();
        
        // Validate receiver type
        if (context.Receiver.IsSmallInteger)
            return PrimitiveResult.Fail();
        
        // Get receiver data
        SmalltalkOOP receiverClass = context.HeapManager.GetClassOOP(context.Receiver);
        
        // Get arguments
        int arg1 = context.Arguments[0].AsSmallInteger();
        
        // Perform operation
        int result = someOperation(arg1);
        
        // Return result
        return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(result));
    }
}
```

### Pattern 2: Object Creation in Primitives

```csharp
// Allocate new object
SmalltalkOOP newObject = context.AllocateObject("Point", 0);  // 0 = no indexed fields

// Set instance variables
context.SetFieldValue(newObject, "class:Point", "x", value1);
context.SetFieldValue(newObject, "class:Point", "y", value2);

return PrimitiveResult.Ok(newObject);
```

### Pattern 3: Array/Indexed Access

```csharp
// Get size of indexed object
int size = context.HeapManager.GetIndexedSize(context.Receiver);

// Get indexed OOP (for Object arrays)
SmalltalkOOP element = context.HeapManager.GetIndexedOOP(context.Receiver, index);

// Get indexed byte (for String, ByteArray)
byte b = context.HeapManager.GetIndexedByte(context.Receiver, index);

// Set indexed element
context.HeapManager.SetIndexedOOP(context.Receiver, index, value);
```

### Pattern 4: Global/Class Lookup

```csharp
// Look up class
SmalltalkOOP classOOP = context.RuntimeHeapAccess?.GetClass("ClassName");

// Look up global
SmalltalkOOP globalOOP = context.GetGlobalObjectByName("global:GlobalName");

// Intern symbol
SmalltalkOOP symbolOOP = context.RuntimeHeapAccess?.InternSymbol("symbolName");
```

## 9. COMPILATION FLOW (From Source to Bytecode)

### Offline Compiler Pipeline (Smalltalk80.Compiler)

```
Smalltalk Source Code (.st file)
  ↓
FileInFormatAstBuilder (parses into AST)
  ↓
VariableResolver (resolves identifier types: temp, arg, inst var, class var, pool, global)
  ↓
BytecodeCompiler (generates bytecode, collects literals with metadata)
  ↓
ColdStartJsonGenerator (emits JSON with:
  - poolDictionaryName field for pool variables
  - Metadata for pool dictionaries
  - Full bytecode and literal frames)
  ↓
JSON Schema v1.0.8 (coldstart.json)
```

### Variable Resolution Order (VariableResolver.cs)

1. Temps (local variables)
2. Arguments
3. Instance variables
4. Class variables
5. Known class names (treated as globals)
6. Pool dictionary NAMES (Step 10b - treated as globals!)
7. Undefined vars in classes with pools → pool variables
8. Everything else → global

### Runtime Interpretation (Phase 4 Onwards)

```
JSON coldstart.json (with schema v1.0.8)
  ↓
Phase4_MethodCompilation:
  - Creates pool variable Arrays: Array(#TextConstants, #DefaultTab)
  - Creates global Associations for non-pool variables
  ↓
Phase5_GlobalsAndPatches:
  - Installs pool dictionaries as globals
  - Creates DefaultTextStyle, Display
  ↓
VirtualMachine.Execute:
  - Bytecode pushLiteralVariable:
    - First execution: resolves and patches pool variable
    - Subsequent: uses cached dictionary OOP
```

## 10. NEXT STEPS FOR FONT/UI IMPLEMENTATION

See `/home/user/Smalltalk-80/docs/Font-and-UI-Architecture.md` for complete roadmap:

### Phase 1 (DONE): Minimal Font Infrastructure
- ✅ Create DefaultTextStyle during cold-start
- ✅ Create StrikeFont stubs with metrics
- ✅ Enable PopUpMenu instantiation

### Phase 2: Display Primitive Foundation
- Create Display object (DisplayScreen with bitmap)
- Implement primitives 90-96 (display primitives)
- Create Windows display window (WinForms)

### Phase 3: BitBlt Graphics Engine
- Implement primitive 96 (copyBits)
- Support all 16 combination rules
- Form ↔ Bitmap synchronization

### Phase 4-8: Text Rendering, Windows Integration, Input System, etc.
- See Font-and-UI-Architecture.md for details
