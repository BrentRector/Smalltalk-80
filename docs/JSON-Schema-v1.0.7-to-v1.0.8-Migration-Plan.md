# JSON Schema Migration Plan: v1.0.7 → v1.0.8

**Status**: DRAFT - Ready for Implementation  
**Created**: 2025-11-15  
**Purpose**: Add pool variable tracking and dependency graph metadata for future pool dictionary validation

---

## Executive Summary

Schema v1.0.8 extends v1.0.7 with explicit pool variable reference tracking and dependency graph metadata. This enables the cold start loader to:

1. **Validate** all pool variable references before execution
2. **Order** initialize expressions canonically based on producer/consumer dependencies  
3. **Debug** pool dictionary issues with clear diagnostics

**Key Principle**: The compiler emits pool metadata; the loader consumes it for validation and ordering.

---

## Schema Changes Overview

### 🆕 New Top-Level Sections

#### 1. `poolDependencyGraph` (REQUIRED)
Centralized dependency graph showing which classes produce vs. consume pool variables.

```json
{
  "poolDependencyGraph": {
    "producers": [...],
    "consumers": [...],
    "edges": [...],
    "executionOrder": [...]
  }
}
```

### 📝 Modified Sections

#### 2. `methods[].poolReferences` (NEW FIELD)
Each method gains an optional `poolReferences[]` array tracking pool variable usage.

#### 3. `initExpressions[].poolReferences` (NEW FIELD)
Init expressions track pool references for canonical ordering.

#### 4. `metadata.poolReferenceCount` (NEW FIELD)
Total count of pool variable references across all methods.

---

## Detailed Schema Additions

### 1. Pool Dependency Graph (Top-Level)

```json
{
  "poolDependencyGraph": {
    "producers": [
      {
        "classLocalId": "meta:Text",
        "methodLocalId": "method:Text_class>>initialize",
        "poolLocalId": "pool:TextConstants",
        "symbolsProduced": [
          { "symbolIndex": 123, "symbolName": "DefaultTab" },
          { "symbolIndex": 124, "symbolName": "CaretForm" }
        ]
      }
    ],
    "consumers": [
      {
        "classLocalId": "class:Paragraph",
        "methodLocalId": "method:Paragraph>>initialize",
        "poolLocalId": "pool:TextConstants",
        "symbolsConsumed": [
          { "symbolIndex": 123, "symbolName": "DefaultTab" }
        ]
      }
    ],
    "edges": [
      {
        "from": "meta:Text",
        "to": "class:Paragraph",
        "poolLocalId": "pool:TextConstants",
        "symbols": ["DefaultTab"]
      }
    ],
    "executionOrder": [
      "meta:Text",
      "class:Paragraph",
      "class:Scanner"
    ],
    "cycles": [],
    "orphans": [],
    "validation": {
      "status": "valid",
      "errors": [],
      "warnings": []
    }
  }
}
```

**Fields**:
- `producers[]` - Classes/methods that populate pool dictionaries (via `at:put:`)
- `consumers[]` - Classes/methods that reference pool variables
- `edges[]` - Dependency relationships (consumer depends on producer)
- `executionOrder[]` - Topological sort of init expressions (canonical order)
- `cycles[]` - Circular dependencies detected (empty = no cycles)
- `orphans[]` - Pool references with no producer (validation errors)
- `validation` - Pre-computed validation status

### 2. Method Pool References

Add `poolReferences[]` to each entry in `methods[]`:

```json
{
  "localId": "method:Paragraph>>initialize",
  "homeClassLocalId": "class:Paragraph",
  "selectorIndex": 456,
  "header": {...},
  "literals": [...],
  "bytecodes": [...],
  "poolReferences": [
    {
      "poolLocalId": "pool:TextConstants",
      "symbolIndex": 123,
      "usage": "consume",
      "literalIndex": 5
    }
  ]
}
```

**Fields**:
- `poolLocalId` - Local ID of pool dictionary object (e.g., "pool:TextConstants")
- `symbolIndex` - Index into `symbols[]` for variable name
- `usage` - Either "produce" or "consume"
- `literalIndex` - Index into method's `literals[]` array where this reference appears

### 3. Init Expression Pool References

Same structure as method pool references:

```json
{
  "localId": "init:Text-initialize",
  "header": {...},
  "literals": [...],
  "bytecodes": [...],
  "poolReferences": [
    {
      "poolLocalId": "pool:TextConstants",
      "symbolIndex": 123,
      "usage": "produce",
      "literalIndex": 2
    }
  ],
  "source": "Text initialize"
}
```

### 4. Metadata Updates

Add to `metadata`:

```json
{
  "metadata": {
    "version": "1.0.8",
    "poolReferenceCount": 47,
    "poolDictionaryCount": 3,
    "poolProducerCount": 1,
    "poolConsumerCount": 12
  }
}
```

---

## Migration Strategy

### Phase 1: Schema Definition ✅ COMPLETE
- [x] Create `coldstart-v1.0.8.schema.json`  
- [x] Add `poolDependencyGraph` section
- [x] Add `poolReferences[]` to methods/initExpressions
- [x] Update metadata fields

### Phase 2: Compiler Emission (FUTURE)
**Status**: DEFERRED until pool variable implementation

**Changes Required**:
1. **VariableResolver.cs**: Tag pool variables during resolution
2. **BytecodeCompiler.cs**: Track pool references per method
3. **ColdStartJsonGenerator.cs**: Emit `poolReferences[]` arrays
4. **PoolDependencyGraph.cs**: Build dependency graph during compilation
5. **ColdStartJsonGenerator.cs**: Emit `poolDependencyGraph` section

**Estimated Effort**: 3-4 days

### Phase 3: Loader Consumption (FUTURE)
**Status**: DEFERRED until pool variable implementation

**Changes Required**:
1. **ColdStartBundleReader.cs**: Parse `poolDependencyGraph` section
2. **Phase4_MethodCompilation.cs**: Use `poolReferences[]` for validation
3. **Program.cs**: Use `executionOrder[]` for init expression sequencing
4. **PoolValidator.cs**: Validate all consumed symbols have producers

**Estimated Effort**: 2-3 days

### Phase 4: Testing & Validation (FUTURE)
**Status**: DEFERRED

- Unit tests for dependency graph construction
- Integration tests for canonical ordering
- Validation tests for missing producers
- Cycle detection tests

**Estimated Effort**: 2 days

---

## Schema Optimization

### Redundancy Elimination

**v1.0.7 Issues**:
- Association literals duplicate pool dictionary information
- No centralized dependency graph (must scan all methods)
- Init expression ordering is implicit

**v1.0.8 Solutions**:
1. **Centralized Graph**: Single `poolDependencyGraph` section eliminates duplication
2. **Compact References**: `poolReferences[]` uses indices instead of full objects
3. **Explicit Ordering**: `executionOrder[]` array is pre-computed
4. **Validation Status**: Pre-computed validation eliminates loader scans

### Size Impact

**Estimated size increase**: 2-5% for typical bundles
- New `poolDependencyGraph`: ~1-2 KB
- `poolReferences[]` per method: ~20-40 bytes each
- Metadata fields: ~100 bytes

**Offset by**:
- Elimination of redundant Association metadata
- More compact pool variable encoding

---

## Backward Compatibility

### v1.0.7 → v1.0.8

**Breaking Changes**: NONE (additive only)

**New Loaders** (v1.0.8):
- ✅ Can read v1.0.7 bundles (pool fields are optional)
- ✅ Generate empty `poolDependencyGraph` if missing
- ✅ Fall back to sequential init expression order

**Old Loaders** (v1.0.7):
- ✅ Can read v1.0.8 bundles (ignore unknown fields)
- ⚠️ Will NOT validate pool variables
- ⚠️ Will NOT use canonical ordering

**Migration Path**:
1. Deploy v1.0.8 schema (no code changes)
2. Update compiler to emit pool metadata (future)
3. Update loader to consume pool metadata (future)
4. All bundles remain compatible throughout migration

---

## Example: Complete v1.0.8 Bundle Fragment

```json
{
  "metadata": {
    "version": "1.0.8",
    "generator": "Smalltalk80.Compiler v2.0.0",
    "poolReferenceCount": 47,
    "poolDictionaryCount": 1,
    "poolProducerCount": 1,
    "poolConsumerCount": 3
  },
  
  "symbols": [
    "DefaultTab",
    "CaretForm",
    "at:",
    "put:",
    "initialize"
  ],
  
  "objects": [
    {
      "localId": "pool:TextConstants",
      "classNameIndex": 10,
      "formatHint": "pointers",
      "fields": [],
      "indexedFields": []
    }
  ],
  
  "methods": [
    {
      "localId": "method:Text_class>>initialize",
      "homeClassLocalId": "meta:Text",
      "selectorIndex": 4,
      "poolReferences": [
        {
          "poolLocalId": "pool:TextConstants",
          "symbolIndex": 0,
          "usage": "produce",
          "literalIndex": 2
        },
        {
          "poolLocalId": "pool:TextConstants",
          "symbolIndex": 1,
          "usage": "produce",
          "literalIndex": 4
        }
      ]
    },
    {
      "localId": "method:Paragraph>>initialize",
      "homeClassLocalId": "class:Paragraph",
      "selectorIndex": 4,
      "poolReferences": [
        {
          "poolLocalId": "pool:TextConstants",
          "symbolIndex": 0,
          "usage": "consume",
          "literalIndex": 1
        }
      ]
    }
  ],
  
  "poolDependencyGraph": {
    "producers": [
      {
        "classLocalId": "meta:Text",
        "methodLocalId": "method:Text_class>>initialize",
        "poolLocalId": "pool:TextConstants",
        "symbolsProduced": [
          { "symbolIndex": 0, "symbolName": "DefaultTab" },
          { "symbolIndex": 1, "symbolName": "CaretForm" }
        ]
      }
    ],
    "consumers": [
      {
        "classLocalId": "class:Paragraph",
        "methodLocalId": "method:Paragraph>>initialize",
        "poolLocalId": "pool:TextConstants",
        "symbolsConsumed": [
          { "symbolIndex": 0, "symbolName": "DefaultTab" }
        ]
      }
    ],
    "edges": [
      {
        "from": "meta:Text",
        "to": "class:Paragraph",
        "poolLocalId": "pool:TextConstants",
        "symbols": ["DefaultTab"]
      }
    ],
    "executionOrder": [
      "meta:Text",
      "class:Paragraph",
      "class:Scanner"
    ],
    "cycles": [],
    "orphans": [],
    "validation": {
      "status": "valid",
      "errors": [],
      "warnings": []
    }
  }
}
```

---

## Implementation Checklist

### Schema (CURRENT WORK)
- [x] Create `coldstart-v1.0.8.schema.json`
- [x] Define `poolDependencyGraph` structure
- [x] Add `poolReferences[]` to method schema
- [x] Add `poolReferences[]` to initExpression schema
- [x] Update metadata fields
- [ ] Add JSON schema validation rules
- [ ] Update schema documentation

### Compiler (FUTURE)
- [ ] Tag pool variables in VariableResolver
- [ ] Track pool references in BytecodeCompiler
- [ ] Build dependency graph in ColdStartJsonGenerator
- [ ] Emit `poolReferences[]` arrays
- [ ] Emit `poolDependencyGraph` section
- [ ] Emit `executionOrder[]` array
- [ ] Add producer detection heuristics
- [ ] Add consumer detection from literals

### Loader (FUTURE)
- [ ] Parse `poolDependencyGraph` section
- [ ] Parse `poolReferences[]` arrays
- [ ] Validate all consumed symbols have producers
- [ ] Detect circular dependencies
- [ ] Use `executionOrder[]` for init expressions
- [ ] Generate validation diagnostics
- [ ] Handle missing pool dictionaries gracefully

### Testing (FUTURE)
- [ ] Unit tests for dependency graph
- [ ] Integration tests for canonical ordering
- [ ] Validation tests for missing producers
- [ ] Cycle detection tests
- [ ] Backward compatibility tests
- [ ] Performance benchmarks

---

## Benefits of v1.0.8

### ✅ Determinism
- Canonical init expression ordering (no more "works sometimes")
- Reproducible cold start across runs

### ✅ Validation
- All pool references validated before execution
- Clear error messages for missing producers
- Cycle detection prevents infinite loops

### ✅ Debuggability
- Complete dependency graph for visualization
- Track which methods produce/consume which symbols
- Diagnostic reports show exact failure points

### ✅ Extensibility
- Foundation for multi-pool support
- Ready for future optimizations
- Clean separation of concerns (compiler emits, loader validates)

---

## Risk Assessment

### Low Risk
- ✅ Schema is additive (no breaking changes)
- ✅ Backward compatible with v1.0.7
- ✅ Can be deployed independently of code changes

### Medium Risk
- ⚠️ Compiler complexity increases (dependency graph construction)
- ⚠️ Bundle size increases slightly (2-5%)

### High Risk
- ❌ NONE

---

## Next Steps

### Immediate (Today)
1. ✅ Finalize v1.0.8 schema definition
2. ✅ Document migration plan (this document)
3. [ ] Update CLAUDE.md with schema version

### Short-Term (When Pool Variables Resume)
1. Implement compiler emission of `poolReferences[]`
2. Build dependency graph during compilation
3. Emit `poolDependencyGraph` section

### Long-Term (Pool Variable Phase 2)
1. Implement loader validation
2. Use canonical ordering for init expressions
3. Add comprehensive diagnostics

---

## Conclusion

Schema v1.0.8 provides a **clean, efficient, and extensible** foundation for pool variable validation and dependency management. The schema is **backward compatible**, **well-documented**, and **ready for implementation** when pool variable work resumes.

**Recommendation**: Approve schema v1.0.8 and update documentation. Implementation work deferred until pool variable development resumes.
