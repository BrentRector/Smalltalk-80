# Complete Schema Diff: v1.0.7 → v1.0.8

**Purpose**: Comprehensive analysis of ALL changes between schema versions  
**Status**: Implementation Guide  
**Date**: 2025-11-15

---

## Executive Summary

This document catalogs **EVERY** structural difference between v1.0.7 and v1.0.8 schemas, organized by impact level. Both compiler (emission) and interpreter (loading) changes are identified.

---

## Section 1: Top-Level Structure Changes

### 1.1 Required Fields

**v1.0.7**: 10 required fields
```json
"required": [
  "metadata", "symbols", "strings", "optimizedSelectors",
  "classes", "objects", "methods", "initExpressions",
  "installOrder", "patches"
]
```

**v1.0.8**: 11 required fields (ADDED `poolDependencyGraph`)
```json
"required": [
  "metadata", "symbols", "strings", "optimizedSelectors",
  "classes", "objects", "methods", "initExpressions",
  "installOrder", "patches",
  "poolDependencyGraph"  // ← NEW REQUIRED
]
```

**Impact**:
- ✅ **Compiler**: MUST emit `poolDependencyGraph` section (new code required)
- ✅ **Interpreter**: MUST parse `poolDependencyGraph` section (new code required)
- ⚠️ **Breaking**: v1.0.7 bundles are INVALID under v1.0.8 schema (missing required field)

---

## Section 2: Metadata Changes

### 2.1 Removed Fields

**v1.0.7 had these fields** (REMOVED in v1.0.8):
```json
{
  "blockContextCount": 0,        // ← REMOVED
  "blockContexts": []            // ← REMOVED (entire array)
}
```

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `blockContextCount` or `blockContexts` fields
- ⚠️ **Interpreter**: MUST NOT expect these fields
- ℹ️ **Reason**: BlockContext metadata was v1.0.4-1.0.6 feature, removed in v1.0.8

### 2.2 Description Changes

**v1.0.7**: Long description with version history
```
"v1.0.4: BlockContext objects moved...v1.0.5: BlockContext metadata uses...v1.0.7: Added undeclared[]"
```

**v1.0.8**: Simplified description
```
"v1.0.8 adds pool variable reference tracking and dependency graph metadata for canonical init ordering."
```

**Impact**: Documentation only, no code changes

---

## Section 3: Methods Array Changes

### 3.1 New Optional Field: `poolReferences`

**v1.0.7**: No pool tracking
```json
{
  "localId": "method:Text_class>>initialize",
  "homeClassLocalId": "meta:Text",
  "selectorIndex": 4,
  "header": {...},
  "literals": [...],
  "bytecodes": [...]
}
```

**v1.0.8**: Added `poolReferences` array (OPTIONAL)
```json
{
  "localId": "method:Text_class>>initialize",
  "homeClassLocalId": "meta:Text",
  "selectorIndex": 4,
  "header": {...},
  "literals": [...],
  "bytecodes": [...],
  "poolReferences": [        // ← NEW OPTIONAL FIELD
    {
      "poolLocalId": "pool:TextConstants",
      "symbolIndex": 123,
      "usage": "produce"
    }
  ]
}
```

**Field Schema**:
```json
"poolReferences": {
  "type": "array",
  "items": {
    "type": "object",
    "required": ["poolLocalId", "symbolIndex", "usage"],
    "properties": {
      "poolLocalId": { "type": "string" },
      "symbolIndex": { "type": "integer" },
      "usage": { "enum": ["consume", "produce"] }
    }
  }
}
```

**Impact**:
- ✅ **Compiler**: SHOULD emit `poolReferences[]` for methods with pool variables
- ✅ **Interpreter**: MUST handle methods with/without `poolReferences` field
- ℹ️ **Optional**: Backward compatible (can be omitted)

### 3.2 Removed Optional Fields

**v1.0.7 had these fields** (REMOVED in v1.0.8):
```json
{
  "fileIndex": 0,              // ← REMOVED
  "sourceRange": {...},        // ← REMOVED
  "sourceLineMap": [...]       // ← REMOVED
}
```

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit these debug/source mapping fields
- ⚠️ **Interpreter**: MUST NOT expect these fields
- ℹ️ **Reason**: Source mapping deferred to future schema version

---

## Section 4: InitExpressions Array Changes

### 4.1 New Optional Field: `poolReferences`

**Same structure as methods** (see Section 3.1)

**Impact**:
- ✅ **Compiler**: SHOULD emit `poolReferences[]` for init expressions with pool variables
- ✅ **Interpreter**: MUST handle init expressions with/without `poolReferences` field

---

## Section 5: New Top-Level Section: `poolDependencyGraph`

### 5.1 Complete Structure (NEW REQUIRED SECTION)

```json
"poolDependencyGraph": {
  "producers": [
    {
      "classLocalId": "meta:Text",
      "poolLocalId": "pool:TextConstants",
      "symbols": [123, 124],
      "methodLocalId": "method:Text_class>>initialize",  // optional
      "notes": "..."                                     // optional
    }
  ],
  "consumers": [
    {
      "classLocalId": "class:Paragraph",
      "poolLocalId": "pool:TextConstants",
      "symbols": [123],
      "methodLocalId": "method:Paragraph>>initialize",   // optional
      "notes": "..."                                     // optional
    }
  ],
  "edges": [
    {
      "from": "class:Paragraph",
      "to": "meta:Text",
      "symbols": [123],                                  // optional
      "notes": "..."                                     // optional
    }
  ],
  "executionOrder": [
    "meta:Text",
    "class:Paragraph",
    "class:Scanner"
  ],
  "diagnostics": {                                       // optional
    "messages": [...],
    "errors": [...],
    "notes": "..."
  }
}
```

**Field Definitions**:

#### `producers` (REQUIRED array)
- `classLocalId` (REQUIRED string): Producer class/metaclass localId
- `poolLocalId` (REQUIRED string): Pool dictionary object localId
- `symbols` (REQUIRED array of integers): Symbol indices produced
- `methodLocalId` (OPTIONAL string): Producing method localId
- `notes` (OPTIONAL string): Human-readable notes

#### `consumers` (REQUIRED array)
- `classLocalId` (REQUIRED string): Consumer class/metaclass localId
- `poolLocalId` (REQUIRED string): Pool dictionary object localId
- `symbols` (REQUIRED array of integers): Symbol indices consumed
- `methodLocalId` (OPTIONAL string): Consuming method localId
- `notes` (OPTIONAL string): Human-readable notes

#### `edges` (REQUIRED array)
- `from` (REQUIRED string): Consumer classLocalId
- `to` (REQUIRED string): Producer classLocalId
- `symbols` (OPTIONAL array of integers): Symbol indices creating this edge
- `notes` (OPTIONAL string): Human-readable notes

#### `executionOrder` (REQUIRED array of strings)
- Topologically sorted list of class/metaclass localIds
- Loader MUST execute init expressions in this order

#### `diagnostics` (OPTIONAL object)
- `messages` (OPTIONAL array of strings): Diagnostic messages
- `errors` (OPTIONAL array of strings): Validation errors
- `notes` (OPTIONAL string): Summary notes

**Impact**:
- ✅ **Compiler**: MUST build and emit entire `poolDependencyGraph` section
- ✅ **Interpreter**: MUST parse and USE `executionOrder` for init expression sequencing
- ⚠️ **Breaking**: Required section, cannot be omitted

---

## Section 6: Definitions Changes

### 6.1 New Definition: `methodHeader`

**v1.0.7**: Inline header definition in methods
```json
"header": {
  "type": "object",
  "required": ["word", "literalCount", "tempCount", "argCount", "primitiveIndex", "largeContext"],
  "properties": {...}
}
```

**v1.0.8**: Extracted to reusable definition
```json
"header": {
  "$ref": "#/definitions/methodHeader"
}
```

**Definition** (in `definitions` section):
```json
"methodHeader": {
  "type": "object",
  "required": ["word", "literalCount", "tempCount", "argCount", "primitiveIndex", "largeContext"],
  "properties": {
    "word": { "type": "integer", "minimum": 0, "maximum": 65535 },
    "literalCount": { "type": "integer", "minimum": 0, "maximum": 127 },
    "tempCount": { "type": "integer", "minimum": 0, "maximum": 31 },
    "argCount": { "type": "integer", "minimum": 0, "maximum": 7 },
    "primitiveIndex": { "type": "integer", "minimum": 0, "maximum": 255 },
    "largeContext": { "type": "boolean" }
  },
  "additionalProperties": false
}
```

**Impact**:
- ℹ️ **Compiler**: No change (same structure, different reference)
- ℹ️ **Interpreter**: No change (same structure, different reference)
- ℹ️ **Benefit**: DRY principle (reused in blocks and init expressions)

### 6.2 `fieldValue` Definition

**No changes** between v1.0.7 and v1.0.8 (identical structure)

### 6.3 `literalValue` Definition

**No changes** between v1.0.7 and v1.0.8 (identical structure)

---

## Section 7: Removed Sections

### 7.1 `undeclared` Array (REMOVED)

**v1.0.7 had**:
```json
"undeclared": {
  "type": "array",
  "items": {
    "type": "object",
    "required": ["key", "references"],
    "properties": {
      "key": { "type": "string" },
      "references": { "type": "array", "items": { "type": "string" } }
    }
  }
}
```

**v1.0.8**: Entire `undeclared` section REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `undeclared` array
- ⚠️ **Interpreter**: MUST NOT parse `undeclared` array
- ℹ️ **Reason**: Undeclared tracking replaced by pool dependency graph

### 7.2 `primitives` Object (REMOVED)

**v1.0.7 had**:
```json
"primitives": {
  "type": "object",
  "properties": {
    "byIndex": { "type": "array", "items": {...} }
  }
}
```

**v1.0.8**: Entire `primitives` section REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `primitives` object
- ⚠️ **Interpreter**: MUST NOT parse `primitives` object

### 7.3 `sourceFiles` Array (REMOVED)

**v1.0.7 had**:
```json
"sourceFiles": {
  "type": "array",
  "items": {
    "type": "object",
    "required": ["index", "path"],
    "properties": {...}
  }
}
```

**v1.0.8**: Entire `sourceFiles` section REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `sourceFiles` array
- ⚠️ **Interpreter**: MUST NOT parse `sourceFiles` array

### 7.4 `partitionManifest` Array (REMOVED)

**v1.0.7 had**:
```json
"partitionManifest": {
  "type": "array",
  "items": {...}
}
```

**v1.0.8**: REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `partitionManifest`
- ⚠️ **Interpreter**: MUST NOT parse `partitionManifest`

### 7.5 `validationRules` Object (REMOVED)

**v1.0.7 had**:
```json
"validationRules": {
  "type": "object",
  "properties": {
    "allLocalIdsUnique": { "type": "boolean" },
    "behaviorFieldIndicesFixed": { "type": "boolean" },
    // ... 15+ validation rules
  }
}
```

**v1.0.8**: REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `validationRules`
- ⚠️ **Interpreter**: MUST NOT parse `validationRules`
- ℹ️ **Validation**: Rules are now implicit/hardcoded in loader

### 7.6 `symbolTablePartitioning` Object (REMOVED)

**v1.0.7 had**:
```json
"symbolTablePartitioning": {
  "type": "object",
  "properties": {
    "lowSendRange": [...],
    "reservedSelectors": [...],
    "partitionStrategy": "..."
  }
}
```

**v1.0.8**: REMOVED

**Impact**:
- ⚠️ **Compiler**: MUST NOT emit `symbolTablePartitioning`
- ⚠️ **Interpreter**: MUST NOT parse `symbolTablePartitioning`

---

## Section 8: Fields That Stayed EXACTLY The Same

These sections are **IDENTICAL** between v1.0.7 and v1.0.8:

- ✅ `symbols` array structure
- ✅ `strings` array structure
- ✅ `optimizedSelectors` array structure
- ✅ `classes` array structure (all fields unchanged)
- ✅ `objects` array structure
- ✅ `installOrder` structure
- ✅ `patches` array structure
- ✅ `fieldValue` definition
- ✅ `literalValue` definition

**Impact**: No code changes required for these sections

---

## Section 9: Summary of Changes by Impact

### 🔴 BREAKING CHANGES (Incompatible)

1. **NEW REQUIRED FIELD**: `poolDependencyGraph` at top level
   - v1.0.7 bundles CANNOT be loaded by strict v1.0.8 loader
   - Solution: Loader must provide default/empty graph for missing field

2. **REMOVED SECTIONS**: 7 entire sections deleted
   - `undeclared`, `primitives`, `sourceFiles`, `partitionManifest`, 
   - `validationRules`, `symbolTablePartitioning`, `metadata.blockContexts`

### 🟡 ADDITIVE CHANGES (Backward Compatible)

1. **OPTIONAL FIELD**: `methods[].poolReferences`
2. **OPTIONAL FIELD**: `initExpressions[].poolReferences`
3. **NEW DEFINITION**: `methodHeader` (structural refactor only)

### 🟢 UNCHANGED (No Code Impact)

1. All core data structures (symbols, strings, classes, objects, etc.)
2. Method header structure (same data, different schema reference)
3. Literal value types
4. Field value types

---

## Section 10: Implementation Checklist

### Compiler Changes

#### MUST REMOVE:
- [ ] Remove `metadata.blockContextCount` emission
- [ ] Remove `metadata.blockContexts[]` emission
- [ ] Remove `undeclared[]` emission
- [ ] Remove `primitives` emission
- [ ] Remove `sourceFiles[]` emission
- [ ] Remove `partitionManifest[]` emission
- [ ] Remove `validationRules` emission
- [ ] Remove `symbolTablePartitioning` emission
- [ ] Remove `methods[].fileIndex` emission
- [ ] Remove `methods[].sourceRange` emission
- [ ] Remove `methods[].sourceLineMap` emission

#### MUST ADD:
- [ ] Emit `poolDependencyGraph` section (REQUIRED)
  - [ ] Build `producers[]` array
  - [ ] Build `consumers[]` array
  - [ ] Build `edges[]` array
  - [ ] Compute `executionOrder[]` via topological sort
  - [ ] Optionally emit `diagnostics` object

#### SHOULD ADD (OPTIONAL):
- [ ] Emit `methods[].poolReferences[]` for methods with pool variables
- [ ] Emit `initExpressions[].poolReferences[]` for init expressions with pool variables

#### REFACTOR:
- [ ] Use `$ref: "#/definitions/methodHeader"` for headers (instead of inline)

### Interpreter Changes

#### MUST REMOVE:
- [ ] Remove parsing of `metadata.blockContextCount`
- [ ] Remove parsing of `metadata.blockContexts[]`
- [ ] Remove parsing of `undeclared[]`
- [ ] Remove parsing of `primitives`
- [ ] Remove parsing of `sourceFiles[]`
- [ ] Remove parsing of `partitionManifest[]`
- [ ] Remove parsing of `validationRules`
- [ ] Remove parsing of `symbolTablePartitioning`
- [ ] Remove parsing of `methods[].fileIndex`
- [ ] Remove parsing of `methods[].sourceRange`
- [ ] Remove parsing of `methods[].sourceLineMap`

#### MUST ADD:
- [ ] Parse `poolDependencyGraph` section (REQUIRED)
  - [ ] Parse `producers[]` array
  - [ ] Parse `consumers[]` array
  - [ ] Parse `edges[]` array
  - [ ] Parse `executionOrder[]` array
  - [ ] Optionally parse `diagnostics` object
- [ ] USE `executionOrder[]` for init expression sequencing

#### SHOULD ADD (OPTIONAL):
- [ ] Parse `methods[].poolReferences[]` (gracefully handle missing)
- [ ] Parse `initExpressions[].poolReferences[]` (gracefully handle missing)

#### MUST HANDLE:
- [ ] Missing `poolDependencyGraph` (for v1.0.7 compatibility)
  - Provide empty/default graph
  - Fall back to sequential init expression order

---

## Section 11: Migration Strategy

### Phase 1: Backward Compatibility Layer
**Add to v1.0.8 loader**:
```csharp
if (bundle.PoolDependencyGraph == null)
{
    // v1.0.7 bundle detected
    bundle.PoolDependencyGraph = new PoolDependencyGraph
    {
        Producers = new List<Producer>(),
        Consumers = new List<Consumer>(),
        Edges = new List<Edge>(),
        ExecutionOrder = bundle.InitExpressions.Select(e => e.LocalId).ToList()
    };
}
```

### Phase 2: Compiler Emission
1. Remove all deprecated fields
2. Add `poolDependencyGraph` section
3. Optionally add `poolReferences[]` arrays

### Phase 3: Interpreter Loading
1. Remove all deprecated parsers
2. Add `poolDependencyGraph` parser
3. Use `executionOrder[]` for init expressions

### Phase 4: Testing
1. Test v1.0.8 loader with v1.0.7 bundles (compatibility)
2. Test v1.0.8 loader with v1.0.8 bundles (full features)
3. Validate `executionOrder[]` produces correct init sequence

---

## Section 12: Code File Impact Analysis

### Compiler Files to Modify:

1. **ColdStartJsonGenerator.cs** (PRIMARY)
   - Remove: blockContexts, undeclared, primitives, sourceFiles, partitionManifest, validationRules, symbolTablePartitioning
   - Add: poolDependencyGraph section
   - Add: poolReferences arrays (optional)

2. **CompilerDataTypes.cs**
   - Remove: BlockContext classes
   - Add: PoolDependencyGraph classes
   - Add: PoolReference class

3. **BytecodeCompiler.cs**
   - Track pool references during compilation
   - Populate poolReferences arrays

### Interpreter Files to Modify:

1. **ColdStartBundle.cs** (Schema classes)
   - Remove: BlockContextMetadata, Undeclared, Primitives, SourceFiles, etc.
   - Add: PoolDependencyGraph class
   - Add: PoolReference class
   - Update: MethodDefinition to include poolReferences
   - Update: InitExpressionDefinition to include poolReferences

2. **Phase1_ParseBundle.cs** (JSON deserialization)
   - Remove parsing of deprecated sections
   - Add parsing of poolDependencyGraph
   - Handle missing poolDependencyGraph (v1.0.7 compat)

3. **Program.cs** (Init expression execution)
   - Use poolDependencyGraph.executionOrder instead of sequential order

---

## Section 13: Risk Assessment

### HIGH RISK:
- ⚠️ Missing `poolDependencyGraph` breaks v1.0.8 schema compliance
- ⚠️ Incorrect `executionOrder` causes init failures

### MEDIUM RISK:
- ⚠️ Backward compatibility with v1.0.7 bundles
- ⚠️ Empty pool dependency graph causes sequential fallback

### LOW RISK:
- ✅ Removing deprecated sections (cleanly isolated)
- ✅ Adding optional poolReferences (graceful degradation)

---

## Conclusion

v1.0.8 is a **major schema revision** with:
- **1 new required section** (poolDependencyGraph)
- **7 removed sections** (blockContexts, undeclared, primitives, sourceFiles, partitionManifest, validationRules, symbolTablePartitioning)
- **2 new optional fields** (poolReferences in methods/initExpressions)
- **Breaking changes** that require loader compatibility layer

**Recommendation**: Implement backward compatibility layer to support v1.0.7 bundles during transition period.
