# Post-Bootstrap Class Installation System

## Overview
This document describes the system for converting extracted class JSON files into executable Smalltalk bytecode that can install classes post-bootstrap using message sends rather than direct slot manipulation.

## Workflow

### Step 1: Extract Class from System Image
```bash
cd Smalltalk80.ExtractClass
dotnet run -- --class ClassName
```
This generates `extracted/ClassName.json` with:
- Class metadata (name, superclass, instance variables)
- Compiled bytecode for all methods
- Proper argument and temporary counts
- Literals array
- Separation of instance and class methods

### Step 2: Convert to Installation Script
The extracted JSON is converted to a "class installation script" JSON that describes the sequence of message sends needed to create the class.

**Input**: `extracted/ClassName.json`
**Output**: `installation-scripts/ClassName-install.json`

Example installation script structure:
```json
{
  "className": "Dictionary",
  "operations": [
    {
      "receiver": "Metaclass",
      "selector": "new",
      "args": [],
      "storeIn": "metaclass"
    },
    {
      "receiver": "Class", 
      "selector": "new",
      "args": [],
      "storeIn": "class"
    },
    {
      "receiver": "$class",
      "selector": "class:",
      "args": ["$metaclass"]
    },
    {
      "receiver": "$metaclass",
      "selector": "class:", 
      "args": ["Metaclass"]
    },
    {
      "receiver": "$metaclass",
      "selector": "thisClass:",
      "args": ["$class"]
    },
    {
      "receiver": "Symbol",
      "selector": "intern:",
      "args": ["Dictionary"],
      "storeIn": "nameSymbol"
    },
    {
      "receiver": "$class",
      "selector": "name:",
      "args": ["$nameSymbol"]
    }
  ]
}
```

Variables prefixed with `$` refer to previously stored values.

### Step 3: Compile to CompiledMethod(s)
The installation script is compiled into one or more CompiledMethod objects to manage temporary variable limits.

#### Method Grouping Strategy
Operations are grouped to avoid exceeding temporary limits:

1. **Method 1: Create Core Objects**
   - Create metaclass and class
   - Set up class/metaclass relationships
   - Temporaries: metaclass, class

2. **Method 2: Configure Class Properties**
   - Set name, superclass, instance variables, format
   - Temporaries: class, nameSymbol, superclass, instVarArray

3. **Method 3: Create Method Dictionaries**
   - Create instance and class method dictionaries
   - Set them on class and metaclass
   - Temporaries: class, metaclass, instDict, classDict

4. **Method 4+: Install Methods**
   - Create CompiledMethod objects
   - Install in dictionaries
   - May need multiple methods if many methods to install
   - Temporaries: methodDict, method, selector, bytecode, literals, header

### Step 4: Execute Installation
The generated CompiledMethod(s) are executed to install the class into the running system.

## Installation Script Operations

### Object Creation Operations
```json
{"receiver": "Metaclass", "selector": "new", "args": [], "storeIn": "metaclass"}
{"receiver": "Class", "selector": "new", "args": [], "storeIn": "class"}
{"receiver": "Array", "selector": "new:", "args": [3], "storeIn": "array"}
{"receiver": "ByteArray", "selector": "new:", "args": [10], "storeIn": "bytecode"}
{"receiver": "MethodDictionary", "selector": "new:", "args": [50], "storeIn": "methodDict"}
{"receiver": "CompiledMethod", "selector": "new", "args": [], "storeIn": "method"}
{"receiver": "Symbol", "selector": "intern:", "args": ["selectorName"], "storeIn": "selector"}
```

### Configuration Operations
```json
{"receiver": "$class", "selector": "superclass:", "args": ["$superclass"]}
{"receiver": "$class", "selector": "name:", "args": ["$nameSymbol"]}
{"receiver": "$class", "selector": "instanceVariables:", "args": ["$instVarArray"]}
{"receiver": "$class", "selector": "format:", "args": [2]}
{"receiver": "$class", "selector": "methodDictionary:", "args": ["$methodDict"]}
```

### Method Installation Operations
```json
{"receiver": "$method", "selector": "selector:", "args": ["$selector"]}
{"receiver": "$method", "selector": "bytecode:", "args": ["$bytecodeArray"]}
{"receiver": "$method", "selector": "literals:", "args": ["$literalsArray"]}
{"receiver": "$method", "selector": "header:", "args": [123]}
{"receiver": "$methodDict", "selector": "at:put:", "args": ["$selector", "$method"]}
```

### Global Registration
```json
{"receiver": "Smalltalk", "selector": "at:put:", "args": ["Dictionary", "$class"]}
```

## Bytecode Generation

Each operation in the installation script compiles to bytecode sequences:

### For Simple Message Send
```
Operation: {"receiver": "Metaclass", "selector": "new", "args": []}
Bytecode:
  - Push global: Metaclass
  - Send: new
  - Store in temp and pop
```

### For Message with Arguments
```
Operation: {"receiver": "$class", "selector": "name:", "args": ["$nameSymbol"]}
Bytecode:
  - Push temp: class
  - Push temp: nameSymbol
  - Send: name:
  - Pop (if no store needed)
```

### For Literal Arguments
```
Operation: {"receiver": "$class", "selector": "format:", "args": [2]}
Bytecode:
  - Push temp: class
  - Push constant: 2
  - Send: format:
  - Pop
```

## Method Bytecode Embedding

The full bytecode from extracted methods is included in the installation script:

```json
{
  "methodInstallations": [
    {
      "selector": "at:put:",
      "bytecode": [16, 119, 17, 224, 135, 120],
      "literals": ["self", "basicAt:put:"],
      "primitiveNumber": 61,
      "temporaryCount": 0,
      "forClass": true  // true = class method, false = instance method
    }
  ]
}
```

## Temporary Variable Management

To avoid exceeding the 16-temporary limit in Smalltalk methods:
- Group related operations into separate CompiledMethods
- Pass results between methods via globals or instance variables if needed
- Each method declares only the temporaries it needs

Example temporary allocation:
```
Method 1: | metaclass class |
Method 2: | class nameSymbol superclass instVarArray |
Method 3: | class metaclass instDict classDict |
Method 4: | dict method selector bytecode literals header |
```

## Future Enhancements

1. **ClassBuilder Integration**: Once more infrastructure is available, integrate with system ClassBuilder
2. **Batch Processing**: Install multiple classes in a single operation
3. **Dependency Resolution**: Automatically order class installations based on superclass dependencies
4. **Validation**: Verify class structure after installation
5. **Rollback**: Support for undoing failed installations

## Implementation Notes

1. **Avoid Circular Dependencies**: Ensure superclasses exist before creating subclasses
2. **Method Dictionary Sizing**: Pre-size dictionaries appropriately (typically 1.5x expected methods)
3. **Symbol Interning**: Reuse existing symbols where possible
4. **Error Handling**: Gracefully handle missing dependencies or conflicts
5. **Global Registration**: Always register in Smalltalk dictionary for visibility