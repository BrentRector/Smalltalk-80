# Cold Start Schema Changelog

## Version 1.0.2 (2025-10-09)

**Major Changes**:
- Required header field consistency (expanded fields must match encoded word)
- Added source mapping and debug metadata support
- Enhanced validation rules for header consistency and block correctness
- BlockContext object modeling for full heap fidelity
- Symbol table partitioning for single-byte send optimization
- Method dictionary validation for cold-start reproducibility

### Changes

1. **Header Field Consistency** (BREAKING):
   - Changed method and block headers from optional to **REQUIRED** expanded fields
   - Now requires: `literalCount`, `tempCount`, `argCount`, `primitiveIndex`, `largeContext`
   - All expanded fields MUST match the bit encoding in `header.word`
   - Loader validates consistency via `headerFieldsMatchWord` validation rule

2. **Source Mapping Support**:
   - Added `sourceRange` field to methods:
     - `startOffset` / `endOffset`: Character offsets in source file
     - `startLine` / `endLine`: Line numbers (1-based)
   - Enables precise navigation from bytecode back to source

3. **Debug Metadata - Source Line Maps**:
   - Added `sourceLineMap` field to methods
   - Maps bytecode PC → source line number for debugger step-through
   - Each entry: `{ "pc": <int>, "line": <int>, "columnStart"?: <int>, "columnEnd"?: <int> }`
   - Enables setting breakpoints and stepping through code at source level

4. **Enhanced Validation Rules**:
   - `headerFieldsMatchWord`: Verify header consistency (default: true)
   - `blockLiteralsMustHaveNullSelector`: Verify blocks have `selector=null` (default: true)
   - `blocksMustHaveZeroPrimitive`: Verify blocks have `primitiveIndex=0` (default: true)
   - `literalCountMatchesArrayLength`: Verify `header.literalCount` matches `literals.length` (default: true)

5. **Documentation Improvements**:
   - All header fields now have detailed bit encoding descriptions
   - Validation rule descriptions explain what each rule checks
   - Clear distinction between method headers and block headers

6. **BlockContext Object Modeling** (Enhanced):
   - Added optional `blockContextStructure` field to objects with `formatHint="blockContext"`
   - Explicit field layout: `blockArgumentCount`, `initialIP`, `homeContextRef`
   - Enables deterministic reconstruction of block invocation semantics
   - Example:
   ```json
   {
     "localId": "blockctx:000",
     "classNameIndex": 42,
     "formatHint": "blockContext",
     "fields": [
       { "type": "smallInt", "value": 2 },
       { "type": "smallInt", "value": 0 },
       { "type": "objectRef", "localId": "ctx:parent" }
     ],
     "blockContextStructure": {
       "blockArgumentCount": 2,
       "initialIP": 0,
       "homeContextRef": "ctx:parent"
     }
   }
   ```

7. **Symbol Table Partitioning** (Optional):
   - New top-level `symbolTablePartitioning` section
   - Supports `lowSendRange` for single-byte send optimization
   - `reservedSelectors` array for critical selector placement
   - `partitionStrategy`: "frequency", "manual", or "hybrid"
   - Example:
   ```json
   {
     "symbolTablePartitioning": {
       "lowSendRange": [0, 15],
       "reservedSelectors": [0, 1, 2],
       "partitionStrategy": "frequency"
     }
   }
   ```

8. **Enhanced Validation Rules** (3 new rules):
   - `methodDictionaryMustContainSelector`: Verify selectors present in method dictionaries
   - `blockContextStructureValid`: Verify BlockContext objects have valid structure
   - `optimizedSelectorsPacked`: Verify optimized selectors occupy low indices

### Breaking Changes

- Method and block `header` objects now **REQUIRE** all expanded fields (previously optional)
- Old JSON with incomplete headers will fail schema validation
- Compiler must emit all header fields: `literalCount`, `tempCount`, `argCount`, `primitiveIndex`, `largeContext`

### Migration Path

1. **Update compiler** to emit all required header fields
2. **Implement header encoding** - ensure expanded fields match `word` bit encoding
3. **Recompile all source** with updated compiler
4. **Update loader** to validate header consistency per new validation rules

### Example: Method Header v1.0.2

```json
{
  "header": {
    "word": 32770,
    "literalCount": 2,
    "tempCount": 0,
    "argCount": 1,
    "primitiveIndex": 0,
    "largeContext": false
  }
}
```

All fields are required and must be consistent with the 16-bit word encoding.

---

## Version 1.0.1 (2025-10-09)

**Major Changes**:
- Added Blue Book block compilation support
- Removed base64 bytecode encoding support
- Added BlockContext object type for full heap fidelity

### Changes

1. **New Literal Type**: `compiledMethod`
   - Blocks are now represented as separate CompiledMethod objects stored in literal frames
   - Distinguishing feature: `"selector": null` (blocks have no selector)
   - Recursive structure: blocks can contain nested blocks in their `literals` array

2. **Schema Location**:
   - File: `docs/schemas/coldstart-v1.0.2.schema.json`
   - Schema ID: `https://github.com/YourOrg/Smalltalk-80/schemas/coldstart/v1.0.1`

3. **Block CompiledMethod Structure**:
   ```json
   {
     "type": "compiledMethod",
     "selector": null,              // CRITICAL: Always null for blocks
     "numArgs": <integer>,          // Block argument count
     "numTemps": <integer>,         // Block temporary count
     "maxStack": <integer>,         // Maximum stack depth
     "header": { ... },             // Blue Book method header
     "literals": [ ... ],           // Block's literal frame (can contain nested blocks!)
     "bytecodes": [ ... ],          // Block bytecode array
     "argNames": [ ... ],           // Argument names for debugging
     "tempNames": [ ... ]           // Temporary names for debugging
   }
   ```

4. **Example: Empty Block** `[]`
   ```json
   {
     "type": "compiledMethod",
     "selector": null,
     "numArgs": 0,
     "numTemps": 0,
     "maxStack": 1,
     "header": { "word": 0, ... },
     "literals": [],
     "bytecodes": [124]  // 0x7C = RETURN_TOP
   }
   ```

5. **Example: Nested Blocks** `[:x | x > 0 ifTrue: ['positive']]`
   ```json
   {
     "type": "compiledMethod",
     "selector": null,
     "numArgs": 1,
     "literals": [
       { "type": "symbol", "index": ... },
       {
         "type": "compiledMethod",
         "selector": null,
         "numArgs": 0,
         "literals": [
           { "type": "string", "index": ... }
         ],
         "bytecodes": [32]  // PUSH_LITERAL 0
       }
     ]
   }
   ```

6. **Removed Base64 Bytecode Encoding**:
   - Bytecode arrays must now be integer arrays only: `"bytecodes": [124, 0, 32, ...]`
   - Removed `oneOf` union allowing base64 string encoding
   - Simplifies schema and eliminates decoding complexity

7. **New formatHint Value**: `blockContext`
   - Added to `formatHint` enum for objects: `["pointers","words","bytes","compiledMethod","blockContext","custom"]`
   - Used for runtime BlockContext objects (block closures)
   - Structure: Contains `home` (MethodContext), `startpc` (PC in CompiledMethod), `nargs` (argument count)
   - Enables full heap fidelity for snapshots containing active block closures

### Breaking Changes

- Old JSON files using `v1.0.0` schema will NOT validate against `v1.0.1` if they contain blocks
- Blocks were previously serialized as `{"type": "nil"}` - this is no longer valid
- Base64-encoded bytecode strings are no longer supported (integer arrays only)
- Compiler must be updated to use new block serialization format
- Interpreter must be updated to load block CompiledMethods from literals
- Any code expecting base64 bytecode encoding must be updated

### Migration Path

1. **Recompile all source files** with updated compiler
2. **Update interpreter** to recognize `compiledMethod` literal type
3. **Update loader** to instantiate block CompiledMethods from literal frames
4. **Validate** all generated JSON against new schema

### References

- **Blue Book**: Goldberg & Robson (1983), Chapter 16-17
- **Implementation**: Blocks are first-class CompiledMethod objects
- **Semantics**: Blocks execute when sent `value`, `value:`, etc. messages

### Validation

Schema enforces:
- `selector` must be `null` for blocks
- `primitiveIndex` must be `0` (blocks never have primitives)
- `literals` array can recursively contain `compiledMethod` types
- `bytecodes` array must have at least 1 element
- All required fields present

---

## Version 1.0.0 (Initial Release)

Initial schema supporting:
- Symbols, strings, classes, objects, methods
- Install order phases
- Patches for forward references
- Primitive method support
- Blue Book method headers
