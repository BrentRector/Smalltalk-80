# Smalltalk-80 Standard Library Classes

This is a comprehensive list of all classes defined in the Smalltalk-80 standard library, extracted from the authentic source file `Smalltalk-80.st`.

## Core Object Model
- **Object** - Root of the class hierarchy
- **Behavior** - Basic behavior for all classes  
- **ClassDescription** - Base for class and metaclass
- **Class** - Runtime representation of classes
- **Metaclass** - Metaclass implementation
- **MetaclassForMultipleInheritance** - Multiple inheritance support
- **UndefinedObject** - The class of nil

## Numbers and Mathematics
- **Number** - Abstract number class
- **Integer** - Integer numbers
- **SmallInteger** - Small integer implementation
- **LargeNegativeInteger** - Large negative integers
- **LargePositiveInteger** - Large positive integers
- **Float** - Floating point numbers
- **Fraction** - Rational numbers
- **Random** - Random number generator

## Boolean Logic
- **Boolean** - Abstract boolean class
- **True** - The true object
- **False** - The false object

## Collections
### Abstract Collections
- **Collection** - Abstract collection class
- **SequenceableCollection** - Collections with ordered elements
- **ArrayedCollection** - Fixed-size indexed collections

### Concrete Collections
- **Array** - Fixed-size array
- **OrderedCollection** - Dynamic ordered collection
- **SortedCollection** - Automatically sorted collection
- **LinkedList** - Linked list implementation
- **Interval** - Arithmetic sequence
- **Set** - Unordered collection of unique elements
- **IdentitySet** - Set using identity comparison
- **Bag** - Collection allowing duplicates
- **MappedCollection** - Collection mapped through another collection

### Dictionaries
- **Dictionary** - Key-value mapping
- **IdentityDictionary** - Dictionary using identity comparison
- **MethodDictionary** - Dictionary for storing methods
- **LiteralDictionary** - Dictionary for literal values

### Strings and Text
- **String** - Character strings
- **Text** - Styled text
- **ByteArray** - Array of bytes
- **WordArray** - Array of words
- **DisplayBitmap** - Bitmap for display
- **RunArray** - Run-length encoded array

## Streams
- **Stream** - Abstract stream class
- **PositionableStream** - Stream with position
- **ReadStream** - Read-only stream
- **WriteStream** - Write-only stream
- **ReadWriteStream** - Read-write stream
- **ExternalStream** - External file stream
- **FileStream** - File-based stream
- **SharedQueue** - Thread-safe queue

## Magnitudes and Comparisons
- **Magnitude** - Abstract comparable objects
- **Character** - Single characters
- **Date** - Calendar dates
- **Time** - Time of day
- **LookupKey** - Abstract key class
- **Association** - Key-value pair

## Graphics and Display
### Basic Graphics
- **Point** - 2D coordinate
- **Rectangle** - Rectangular area
- **DisplayObject** - Abstract display object
- **DisplayMedium** - Display medium base
- **Form** - Bitmap image
- **DisplayScreen** - Screen representation
- **InfiniteForm** - Infinitely tiled form
- **OpaqueForm** - Opaque bitmap
- **Cursor** - Mouse cursor

### Graphics Operations
- **BitBlt** - Bit block transfer
- **Pen** - Drawing pen
- **CharacterScanner** - Character layout scanner
- **CharacterBlockScanner** - Character block scanner
- **CompositionScanner** - Text composition scanner
- **DisplayScanner** - Display rendering scanner

### Paths and Shapes
- **Path** - Abstract path
- **Line** - Straight line
- **Arc** - Circular arc
- **Circle** - Circle shape
- **Curve** - Curved path
- **LinearFit** - Linear regression
- **Spline** - Spline curve
- **Quadrangle** - Four-sided polygon

## Text and Fonts
- **DisplayText** - Displayable text
- **Paragraph** - Text paragraph
- **TextList** - List of text lines
- **TextStyle** - Text styling
- **StrikeFont** - Bitmap font
- **CharacterBlock** - Character positioning
- **TextLineInterval** - Text line interval

## User Interface Framework
### Model-View-Controller
- **Controller** - Abstract controller
- **View** - Abstract view
- **StandardSystemView** - Standard window view
- **StandardSystemController** - Standard window controller

### Specific Controllers
- **MouseMenuController** - Mouse menu controller
- **ScrollController** - Scrolling controller
- **ListController** - List controller
- **LockedListController** - Locked list controller
- **SelectionInListController** - Selection in list controller
- **ChangeListController** - Change list controller
- **SwitchController** - Switch controller
- **LockedSwitchController** - Locked switch controller
- **IndicatorOnSwitchController** - Indicator switch controller
- **TextController** - Text editing controller
- **CodeController** - Code editing controller
- **AlwaysAcceptCodeController** - Always-accept code controller
- **OnlyWhenSelectedCodeController** - Conditional code controller
- **StringHolderController** - String holder controller
- **FillInTheBlankController** - Fill-in-the-blank controller
- **CRFillInTheBlankController** - CR fill-in-the-blank controller
- **ChangeController** - Change controller
- **ProjectController** - Project controller
- **TextCollectorController** - Text collector controller
- **ParagraphEditor** - Paragraph editor
- **BinaryChoiceController** - Binary choice controller
- **FormMenuController** - Form menu controller
- **BitEditor** - Bitmap editor
- **FormEditor** - Form editor
- **ScreenController** - Screen controller
- **NotifierController** - Notifier controller
- **NoController** - No-op controller

### Specific Views
- **ListView** - List display view
- **ChangeListView** - Change list view
- **SelectionInListView** - Selection list view
- **TextView** - Text display view
- **CodeView** - Code display view
- **OnlyWhenSelectedCodeView** - Conditional code view
- **StringHolderView** - String holder view
- **FillInTheBlankView** - Fill-in-the-blank view
- **ProjectView** - Project view
- **TextCollectorView** - Text collector view
- **DisplayTextView** - Display text view
- **FormView** - Form display view
- **FormHolderView** - Form holder view
- **FormMenuView** - Form menu view
- **BrowserView** - Browser view
- **InspectorView** - Inspector view
- **NotifierView** - Notifier view
- **BinaryChoiceView** - Binary choice view

### Switches and Buttons
- **Switch** - Abstract switch
- **Button** - Push button
- **OneOnSwitch** - One-on switch
- **BooleanView** - Boolean value view

## System Tools
### Browser and Inspector
- **Browser** - Class browser
- **MethodListBrowser** - Method list browser
- **Debugger** - Debugger tool
- **Inspector** - Object inspector
- **ContextInspector** - Context inspector
- **DictionaryInspector** - Dictionary inspector

### Change Management
- **Change** - Abstract change
- **ClassRelatedChange** - Class-related change
- **ClassChange** - Class change
- **ClassDefinitionChange** - Class definition change
- **ClassOtherChange** - Other class change
- **ClassCommentChange** - Class comment change
- **MethodChange** - Method change
- **MethodDefinitionChange** - Method definition change
- **MethodOtherChange** - Other method change
- **OtherChange** - Other change
- **ChangeSet** - Set of changes
- **ChangeList** - List of changes
- **ChangeScanner** - Change scanner

### File System
- **File** - File abstraction
- **AltoFile** - Alto file implementation
- **FileDirectory** - File directory
- **AltoFileDirectory** - Alto file directory
- **FilePage** - File page
- **AltoFilePage** - Alto file page
- **FileModel** - File model
- **FileList** - File list
- **AltoFileAddressArray** - Alto file address array

## Compilation and Parsing
### Parser and Scanner
- **Scanner** - Lexical scanner
- **ChangeScanner** - Change scanner
- **Parser** - Syntax parser
- **ParseStack** - Parser stack

### Parse Tree Nodes
- **ParseNode** - Abstract parse node
- **MethodNode** - Method parse node
- **BlockNode** - Block parse node
- **MessageNode** - Message parse node
- **CascadeNode** - Cascade parse node
- **AssignmentNode** - Assignment parse node
- **ReturnNode** - Return parse node
- **LeafNode** - Leaf parse node
- **LiteralNode** - Literal parse node
- **VariableNode** - Variable parse node
- **SelectorNode** - Selector parse node

### Compilation
- **Compiler** - Source compiler
- **Encoder** - Bytecode encoder
- **CompiledMethod** - Compiled method representation
- **Decompiler** - Bytecode decompiler
- **DecompilerConstructor** - Decompiler constructor
- **InstructionStream** - Instruction stream
- **InstructionPrinter** - Instruction printer

## Execution Context
- **ContextPart** - Abstract execution context
- **MethodContext** - Method execution context
- **BlockContext** - Block execution context

## Process Management
- **Process** - Process/thread
- **ProcessHandle** - Process handle
- **ProcessorScheduler** - Process scheduler
- **Semaphore** - Synchronization primitive
- **Delay** - Timed delay
- **Link** - Process queue link

## Input/Output
- **InputSensor** - Input device sensor
- **InputState** - Input state
- **KeyboardEvent** - Keyboard event
- **ControlManager** - Input control manager

## System Utilities
- **RemoteString** - Remote string reference
- **Message** - Message object
- **MethodDescription** - Method description
- **ClassOrganizer** - Class organization
- **ClassCategoryReader** - Class category reader
- **StringHolder** - String holder model
- **FillInTheBlank** - Fill-in-the-blank dialog
- **TextCollector** - Text collector
- **Project** - Project management
- **PopUpMenu** - Pop-up menu
- **ActionMenu** - Action menu
- **BinaryChoice** - Binary choice dialog
- **WindowingTransformation** - Window transformation

## Testing and Analysis
- **Benchmark** - Performance benchmark
- **MessageTally** - Message profiling
- **Checker** - Code checker
- **Explainer** - Code explainer

## UI Support
- **FormButtonCache** - Form button cache

## Total Classes: 182

This represents the complete standard library of Smalltalk-80, containing 182 classes that provide:
- Core object-oriented infrastructure
- Rich collection hierarchy 
- Comprehensive graphics and UI framework
- Complete development environment (browser, debugger, inspector)
- File system and I/O support
- Process management and concurrency
- Text processing and display
- Mathematical and utility classes

The classes are organized into a sophisticated inheritance hierarchy with Object as the root, providing the foundation for the entire Smalltalk-80 environment.
