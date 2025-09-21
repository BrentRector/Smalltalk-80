# Smalltalk-80 Bootstrap Schema Documentation

## Overview

The `bootstrap-schema.json` file defines the structure for compiled Smalltalk-80 system images. Both the StandaloneCompiler and the new Smalltalk80.Compiler produce JSON output that conforms to this schema.

## Schema Structure

### Top-Level Fields

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `$schema` | string | No | Reference to the schema file (should be `"./bootstrap-schema.json"`) |
| `description` | string | No | Human-readable description of when/how the file was generated |
| `classes` | array | Yes | All class definitions in the system |
| `methods` | array | Yes | All compiled methods in the system |
| `initializationExpressions` | array | Yes | Top-level expressions to execute during system initialization |

### Class Definition Structure

Each element in the `classes` array has:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | Yes | The class name |
| `superclass` | string | Yes | The superclass name |
| `instanceVariables` | string[] | No | Instance variable names |
| `classVariables` | string[] | No | Class variable names |
| `poolDictionaries` | string[] | No | Pool dictionary names |
| `category` | string | No | System category (e.g., "Collections-Abstract") |
| `comment` | string/null | No | Class documentation |
| `methods` | MethodDefinition[] | No | Instance method definitions (full method objects) |
| `classMethods` | MethodDefinition[] | No | Class method definitions (full method objects) |

### Method Definition Structure

Each element in the `methods` array has:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `class` | string | Yes | Class name (append " class" for class methods) |
| `selector` | string | Yes | Method selector |
| `bytecode` | int[] | Yes | Compiled bytecode (array of 0-255) |
| `literals` | array | Yes | Literal frame (strings, numbers, symbols, associations) |
| `primitiveIndex` | integer | No | Primitive number (0 if not primitive) |
| `numArgs` | integer | No | Number of arguments |
| `numTemps` | integer | No | Number of temporary variables |
| `stackDepth` | integer | No | Maximum stack depth needed |
| `isClassMethod` | boolean | No | True for class methods |
| `protocol` | string | No | Method category/protocol |
| `sourceCode` | string | No | Original source code |
| `argumentNames` | string[] | No | Argument variable names |
| `temporaryNames` | string[] | No | Temporary variable names |

### Initialization Expression Structure

Each element in the `initializationExpressions` array has:

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bytecode` | int[] | Yes | Compiled bytecode |
| `literals` | array | Yes | Literal frame |
| `numTemps` | integer | No | Number of temporaries |
| `stackDepth` | integer | No | Maximum stack depth |
| `source` | string | No | Original source code |
| `receiver` | string | No | Message receiver (if applicable) |
| `selector` | string | No | Message selector (if applicable) |
| `arguments` | string[] | No | Message arguments (if applicable) |

## Literal Representation

Literals in the `literals` arrays can be:

1. **Strings**: Regular string values
2. **Numbers**: Integer or floating-point values
3. **Symbols**: Represented as strings prefixed with `#` (e.g., `"#at:put:"`)
4. **Booleans**: `true` or `false` as strings
5. **Nil**: Represented as `"nil"`
6. **Arrays**: Nested arrays of literals
7. **Associations**: Objects with structure:
   ```json
   {
     "type": "Association",
     "key": "#symbolKey",
     "value": <any literal>
   }
   ```

## Validation

Use the `validate_json.py` script to validate JSON files against this schema:

```bash
python validate_json.py <json_file>
```

This performs structural validation ensuring all required fields are present and have the correct types.

## Compiler Compatibility

Both compilers produce compatible output:
- **StandaloneCompiler**: The original C# compiler
- **Smalltalk80.Compiler**: The new modernized C# 13 compiler

The new compiler preserves additional metadata (method protocols) while maintaining full compatibility.

## Example

```json
{
  "$schema": "./bootstrap-schema.json",
  "description": "Compiled from source on 2025-09-21 11:26:26",
  "classes": [
    {
      "name": "Point",
      "superclass": "Object",
      "instanceVariables": ["x", "y"],
      "classVariables": [],
      "poolDictionaries": [],
      "category": "Graphics-Primitives",
      "methods": ["x", "y", "x:", "y:", "+", "-", "*", "x:y:"]
    }
  ],
  "methods": [
    {
      "class": "Point",
      "selector": "x",
      "bytecode": [0, 124],
      "literals": [],
      "primitiveIndex": 0,
      "numArgs": 0,
      "numTemps": 0,
      "stackDepth": 1,
      "isClassMethod": false,
      "protocol": "accessing",
      "sourceCode": "x\r\n    ^x!",
      "argumentNames": [],
      "temporaryNames": []
    }
  ],
  "initializationExpressions": [
    {
      "bytecode": [64, 33, 34, 193, 124],
      "literals": [
        {"type": "Association", "key": "#Smalltalk"},
        "#TestGlobal",
        42,
        "#at:put:"
      ],
      "numTemps": 0,
      "stackDepth": 3,
      "source": "Smalltalk at: #TestGlobal put: 42",
      "receiver": "Smalltalk",
      "selector": "at:put:",
      "arguments": ["#TestGlobal", "42"]
    }
  ]
}
```