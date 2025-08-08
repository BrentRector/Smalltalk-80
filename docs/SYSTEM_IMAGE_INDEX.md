# Smalltalk-80 System Image Index

This document provides a comprehensive index of all classes, methods, and content in the system image file `Smalltalk-80.st`.

**Generated:** August 5, 2025  
**Source:** `d:\Users\brent\OneDrive\Src\Smalltalk-80\Smalltalk80\Smalltalk-80 Source\Smalltalk-80.st`  
**File Size:** 41,400 lines

## Class Hierarchy Overview

### Core Classes (Present in System Image)
- **Object** (line 26320) - Root class with comprehensive method suite
- **UndefinedObject** - The nil object
- **Boolean, True, False** - Boolean hierarchy
- **Magnitude** - Abstract comparison class
  - **Number** - Abstract numeric class
    - **Integer** - Abstract integer class  
      - **SmallInteger** - Small integers
      - **LargePositiveInteger** - Large positive integers
      - **LargeNegativeInteger** - Large negative integers
    - **Float** - Floating point numbers
    - **Fraction** - Rational numbers
  - **Character** - Individual characters
  - **Date, Time** - Date and time classes
  - **LookupKey** - Abstract key class
    - **Association** - Key-value pairs

### Collection Classes (Present in System Image)
- **Collection** - Abstract collection class
  - **SequenceableCollection** - Ordered collections
    - **ArrayedCollection** - Fixed-size indexed collections
      - **Array** - General purpose arrays
      - **String** - Character strings
        - **Symbol** - Immutable symbol strings
      - **ByteArray** - Byte arrays
      - **Text** - Formatted text
      - **WordArray** - Word arrays
      - **CompiledMethod** - Compiled method bytecode
      - **RunArray** - Run-length encoded arrays
        - **AltoFileAddressArray** - File address arrays
    - **OrderedCollection** - Variable-size ordered collections
      - **SortedCollection** - Automatically sorted collections
    - **LinkedList** - Linked list implementation
      - **Semaphore** - Synchronization primitive
    - **Interval** - Arithmetic progressions
      - **TextLineInterval** - Text line ranges
  - **Set** - Hash-based unique collections
    - **IdentitySet** - Identity-based sets
    - **Dictionary** - Hash-based key-value collections
      - **IdentityDictionary** - Identity-based dictionaries
      - **LiteralDictionary** - Literal value dictionaries
      - **MethodDictionary** - Method dictionaries
      - **SystemDictionary** - System global dictionary
  - **Bag** - Collections allowing duplicates
  - **MappedCollection** - Mapped view of other collections

### Stream Classes (Present in System Image)
- **Stream** - Abstract stream class
  - **PositionableStream** - Streams with positioning
    - **ReadStream** - Read-only streams
    - **WriteStream** - Write streams
      - **ReadWriteStream** - Bidirectional streams
        - **ExternalStream** - External file streams
          - **FileStream** - File I/O streams
  - **Random** - Random number streams

### Block and Process Classes
- **BlockContext** - Executable code blocks
- **ContextPart** - Abstract execution context
  - **MethodContext** - Method execution contexts
- **Process** - Process control
- **ProcessHandle** - Process management
- **ProcessorScheduler** - Process scheduling
- **Delay** - Time delays
- **SharedQueue** - Inter-process communication

### Parser and Compiler Classes
- **ParseNode** - Abstract parse tree node
  - **LeafNode** - Leaf parse nodes
    - **LiteralNode** - Literal values
    - **SelectorNode** - Method selectors
    - **VariableNode** - Variable references
  - **AssignmentNode** - Assignment expressions
  - **BlockNode** - Block expressions
  - **CascadeNode** - Cascade expressions
  - **MessageNode** - Message sends
  - **MethodNode** - Method definitions
  - **ReturnNode** - Return statements
  - **DecompilerConstructor** - Decompiler support
- **Scanner** - Lexical analyzer
  - **Parser** - Syntax analyzer
  - **ChangeScanner** - Change file scanner
- **Compiler** - Compilation controller
- **Encoder** - Bytecode generation
- **InstructionStream** - Bytecode stream reader
  - **InstructionPrinter** - Bytecode disassembler
  - **Decompiler** - Source reconstruction
- **SyntaxError** - Syntax error handling
- **ParseStack** - Parser stack

### Graphics and Display Classes
- **Point** - 2D coordinates
- **Rectangle** - Rectangular areas
  - **Quadrangle** - Four-sided areas
  - **CharacterBlock** - Character positioning
- **DisplayObject** - Abstract displayable object
  - **DisplayMedium** - Display surfaces
    - **Form** - Bitmap images
      - **Cursor** - Mouse cursors
      - **DisplayScreen** - Screen display
  - **Path** - Vector graphics paths
    - **Arc** - Circular arcs
      - **Circle** - Circles  
    - **Line** - Straight lines
    - **Curve** - Curved paths
    - **Spline** - Spline curves
    - **LinearFit** - Linear regression
  - **DisplayText** - Displayable text
    - **Paragraph** - Text paragraphs
      - **TextList** - Text lists
  - **InfiniteForm** - Tiled forms
  - **OpaqueForm** - Opaque forms
- **BitBlt** - Bit block transfer
  - **CharacterScanner** - Text scanning
    - **CharacterBlockScanner** - Character block scanning
    - **CompositionScanner** - Text composition
    - **DisplayScanner** - Display text scanning
  - **Pen** - Drawing pen
- **WindowingTransformation** - Coordinate transformation

### User Interface Classes
- **View** - Abstract view class
  - **StandardSystemView** - Standard window
    - **BrowserView** - Code browser window
    - **InspectorView** - Object inspector window  
    - **NotifierView** - Error notification window
  - **TextView** - Text display view
    - **CodeView** - Code editing view
      - **OnlyWhenSelectedCodeView** - Conditional code view
  - **ListView** - List display view
    - **SelectionInListView** - Selectable list view
    - **ChangeListView** - Change list view
  - **StringHolderView** - Text holder view
    - **FillInTheBlankView** - Text input dialog
    - **TextCollectorView** - Text collection view
    - **ProjectView** - Project workspace view
  - **FormView** - Form display view
    - **FormHolderView** - Form container view
  - **FormMenuView** - Form menu view
  - **SwitchView** - Switch control view
    - **BooleanView** - Boolean switch view
  - **BinaryChoiceView** - Yes/no dialog view
- **Controller** - Abstract controller class
  - **StandardSystemController** - Standard window controller
    - **NotifierController** - Error dialog controller
  - **MouseMenuController** - Mouse menu controller
    - **ScreenController** - Screen controller
    - **ScrollController** - Scrolling controller
      - **ParagraphEditor** - Text editor controller
        - **TextController** - Basic text controller
          - **CodeController** - Code editing controller
            - **AlwaysAcceptCodeController** - Auto-accept code controller
            - **OnlyWhenSelectedCodeController** - Conditional code controller
    - **BitEditor** - Bit editor controller
    - **FormEditor** - Form editor controller
  - **ListController** - List controller
    - **LockedListController** - Locked list controller
      - **ChangeListController** - Change list controller
    - **SelectionInListController** - Selection list controller
  - **StringHolderController** - Text holder controller
    - **ChangeController** - Change text controller
    - **FillInTheBlankController** - Text input controller
      - **CRFillInTheBlankController** - CR-accepting input controller
    - **TextCollectorController** - Text collector controller
    - **ProjectController** - Project controller
  - **SwitchController** - Switch controller
    - **LockedSwitchController** - Locked switch controller
    - **IndicatorOnSwitchController** - Indicator switch controller
  - **BinaryChoiceController** - Binary choice controller
  - **FormMenuController** - Form menu controller
  - **NoController** - Null controller
- **PopUpMenu** - Pop-up menu
  - **ActionMenu** - Action menu with selectors

### Model Classes
- **StringHolder** - Text model
  - **FillInTheBlank** - Text input model
  - **ChangeList** - Change list model
  - **Project** - Project workspace model
  - **TextCollector** - Text collection model
- **Switch** - Switch model
  - **Button** - Button model
  - **OneOnSwitch** - Exclusive switch model
- **BinaryChoice** - Binary choice model

### Development Tools
- **Browser** - Code browser
  - **Debugger** - Debugger interface
  - **MethodListBrowser** - Method browser
- **Inspector** - Object inspector
  - **ContextInspector** - Context inspector
  - **DictionaryInspector** - Dictionary inspector
- **SystemTracer** - System tracing
- **Benchmark** - Performance benchmarking
- **Explainer** - Code explanation

### File System Classes
- **File** - Abstract file class
  - **AltoFile** - Alto file system file
- **FileDirectory** - File directory
  - **AltoFileDirectory** - Alto file directory
- **FilePage** - File page management
  - **AltoFilePage** - Alto file page
- **FileModel** - File model
- **FileList** - File listing

### Text and Typography
- **TextStyle** - Text styling information
- **StrikeFont** - Bitmap fonts
- **FormButtonCache** - Button form cache

### System Classes
- **Behavior** - Class behavior
  - **ClassDescription** - Class description
    - **Class** - Regular classes
    - **Metaclass** - Metaclasses
      - **MetaclassForMultipleInheritance** - Multiple inheritance support
- **ClassOrganizer** - Class organization
  - **SystemOrganizer** - System organization
- **ClassCategoryReader** - Class loading

### Change Management
- **Change** - Abstract change
  - **ClassRelatedChange** - Class-related changes
    - **ClassChange** - Class changes
      - **ClassDefinitionChange** - Class definition changes
      - **ClassCommentChange** - Class comment changes
      - **ClassOtherChange** - Other class changes
    - **MethodChange** - Method changes
      - **MethodDefinitionChange** - Method definition changes
      - **MethodOtherChange** - Other method changes
  - **OtherChange** - Other changes
- **ChangeSet** - Collection of changes
- **Checker** - Change validation

### Input/Output
- **InputSensor** - Input device interface
- **InputState** - Input state tracking
- **KeyboardEvent** - Keyboard events
- **ControlManager** - Control management

### Utility Classes
- **Link** - Linked list node
- **Message** - Message objects
- **MethodDescription** - Method descriptions
- **MessageTally** - Message timing
- **RemoteString** - Remote string reference

## Method Categories Quick Reference

### Object Class Method Categories (line 26320+)
1. **initialize-release** - Object lifecycle
2. **accessing** - Property access and indexed access
3. **testing** - Object type testing
4. **comparing** - Object comparison and hashing
5. **copying** - Object duplication
6. **dependents access** - Observer pattern support
7. **updating** - Change notification
8. **printing** - String representation
9. **class membership** - Class hierarchy queries
10. **message handling** - Dynamic message sending
11. **error handling** - Error and exception handling
12. **user interface** - Debugging and inspection
13. **system primitives** - Low-level system operations
14. **system simulation** - VM simulation support
15. **private** - Internal implementation

## Key Findings for StandardLibrary Comparison

### Classes We Have That Match System Image
✅ Object, UndefinedObject, Boolean, True, False
✅ Magnitude, Number, Integer, SmallInteger, LargePositiveInteger, LargeNegativeInteger
✅ Character, Collection, SequenceableCollection, ArrayedCollection
✅ Array, String, Symbol, OrderedCollection, SortedCollection
✅ Interval, Set, IdentitySet, Bag, MappedCollection
✅ Stream, PositionableStream, ReadStream, WriteStream
✅ BlockContext

### Classes Missing from Our StandardLibrary
❌ Float, Fraction (numeric types)
❌ Dictionary, IdentityDictionary, MethodDictionary (key-value collections)
❌ ByteArray, Text, WordArray, CompiledMethod (specialized arrays)
❌ LinkedList, Semaphore (linked structures and synchronization)
❌ Date, Time (temporal classes)
❌ LookupKey, Association (key-value abstractions)
❌ All graphics, UI, parser, compiler, and system classes

### Object Class Analysis
Our Object class is **significantly simplified** compared to the system image Object class:
- System image: ~15 method categories, ~80+ methods
- Our version: ~8 method categories, ~40 methods
- **Missing critical functionality:** dependents/updating, message handling (perform:), comprehensive error handling, user interface support

## Usage Guide

### To Find a Class Definition
1. Search for `subclass: #ClassName` to find the class definition
2. Look for line numbers in parentheses for quick navigation

### To Find Methods for a Class
1. Search for `!ClassName methodsFor:` to find method categories
2. Each category groups related methods together

### To Compare with StandardLibrary
1. Use this index to quickly locate system image classes
2. Compare method categories and individual methods
3. Identify missing functionality for potential addition

## Next Steps for StandardLibrary Enhancement

### Priority 1: Core Missing Types
1. **Float** - Essential numeric type
2. **Dictionary/IdentityDictionary** - Key-value collections
3. **Association/LookupKey** - Key-value pairs

### Priority 2: Enhanced Object Support
1. Add missing Object method categories (dependents, updating, message handling)
2. Add missing error handling methods
3. Add user interface support (inspect methods)

### Priority 3: Extended Collections
1. **ByteArray** - Byte-level data
2. **LinkedList** - Alternative collection structure
3. **Date/Time** - Temporal support

This index provides the roadmap for systematically enhancing our StandardLibrary to match the full system image functionality.
