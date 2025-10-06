# 🧊 Smalltalk-80 Cold Start Loader Specification

## 📘 Overview

This document defines the architecture and operational phases of a cold-start loader for Smalltalk-80. The loader consumes a deterministic JSON bundle conforming to `coldstart-v1.0.0.schema.json` and emits a fully wired Smalltalk object heap, ready for VM execution. It operates without a running Smalltalk system and must manually construct all core objects, including `Class`, `Metaclass`, `Symbol`, `CompiledMethod`, `MethodDictionary`, and the `USTable`.

---

## 📦 Inputs

- **Cold Start Bundle**: A JSON document conforming to the schema
- **Install Order**: Defined in `installOrder.phases[]`
- **Top-Level Sections**:
  - `symbols[]`, `strings[]`
  - `classes[]`, `objects[]`, `methods[]`, `initExpressions[]`
  - `patches[]`, `sourceFiles[]`, `partitionManifest[]`, `validationRules`

---

## 🧩 Loader Responsibilities

- Allocate all heap objects
- Resolve all references (symbol, string, localId)
- Apply all patches (forward references, circular dependencies)
- Honor installOrder for reproducibility
- Emit a valid, runnable Smalltalk heap image
- Populate the `USTable` using the Smalltalk hashing algorithm

---

## 🔁 Loader Phases

### Phase 0: Preflight

**Purpose**: Validate schema and build lookup tables.

**Actions**:
- Parse JSON bundle
- Build:
  - `localId → descriptor` maps for all top-level arrays
  - `symbolIndex → string`, `stringIndex → string`
- Validate:
  - `validationRules.allLocalIdsUnique`
  - `validationRules.allSymbolIndicesValid`
  - `validationRules.allObjectRefsResolvable`

---

### Phase 1: Allocate Class Shells

**Purpose**: Create inert `Class` and `Metaclass` objects.

**Actions**:
- For each `classes[]` entry:
  - Allocate object using `format` (see Blue Book encoding)
  - Do not set `name`, `superclass`, `methodDictionary`, or `class`
- Store `localId → objectId`

**Schema Fields**:
- `localId`, `isMetaclass`, `format`, `instVarNames`, `classVarNames`, `poolLocalIds`

---

### Phase 2: Wire Metaclass Relationships

**Purpose**: Resolve circularity between classes and metaclasses.

**Actions**:
- For each metaclass:
  - Use `metaclassOfLocalId` to link to instance-side class
  - Set `class` field to metaclass of `Class`
- For each instance-side class:
  - Set `class` field to its metaclass

**Schema Fields**:
- `metaclassOfLocalId`, `patches[]` with `operation: wireMetaclass`

---

### Phase 3: Resolve Class Fields

**Purpose**: Populate class metadata and compute field layouts.

**Actions**:
- Resolve:
  - `nameIndex → Symbol`
  - `superNameIndex → superclass object`
  - `instVarNames[]`, `classVarNames[] → symbols`
  - `poolLocalIds[] → Dictionary objects`
  - `commentStringIndex → string`
- Compute full field layout:
  - Traverse superclass chain
  - Aggregate inherited + local `instVarNames`
  - Assign absolute field indices

**Schema Fields**:
- `nameIndex`, `superNameIndex`, `instVarNames`, `classVarNames`, `poolLocalIds`, `commentStringIndex`

---

### Phase 4: Allocate Method Dictionaries

**Purpose**: Create and install `MethodDictionary` objects.

**Actions**:
- Allocate `MethodDictionary` object
- Patch into class using `patches[]`

**Schema Fields**:
- `patches[]` with `target.kind: classSlot`, `slotName: methodDictionary`

---

### Phase 5: Allocate Objects

**Purpose**: Create concrete instances (e.g., pools, associations).

**Actions**:
- Resolve `classNameIndex → Symbol → class object`
- Use `formatHint` to determine layout
- Allocate:
  - Fixed fields: `fields[]` (use `fieldValue` union)
  - Indexed fields: `indexedFields[]`
- Store `localId → objectId`

**Schema Fields**:
- `objects[].localId`, `classNameIndex`, `formatHint`, `fields[]`, `indexedFields[]`

---

### Phase 6: Install Methods

**Purpose**: Create and install `CompiledMethod` objects.

**Actions**:
- Resolve:
  - `homeClassLocalId → class object`
  - `selectorIndex → Symbol`
  - `header.word` and optional decoded fields
  - `literals[]` using `literalValue` union
  - `bytecodes[]` (array or base64)
- Allocate `CompiledMethod`
- Insert into method dictionary

**Schema Fields**:
- `methods[].localId`, `homeClassLocalId`, `selectorIndex`, `header`, `literals[]`, `bytecodes`

---

### Phase 7: Apply Patches

**Purpose**: Resolve forward references and late bindings.

**Actions**:
- For each patch:
  - Resolve `target.kind`
  - Resolve `value` (objectRef, symbolRef, smallInt, nil)
  - Perform operation:
    - `setValue`, `setAssociationValue`, `wireMetaclass`, `addToSystemDictionary`

**Schema Fields**:
- `patches[].target`, `operation`, `value`

---

### Phase 8: Execute Init Expressions

**Purpose**: Run top-level DoIts to finalize system state.

**Actions**:
- Compile each into `CompiledMethod`
- Execute in global context
- Follow `installOrder`

**Schema Fields**:
- `initExpressions[].localId`, `header`, `literals[]`, `bytecodes`

---

### Phase 9: Populate USTable (Symbol Table)

**Purpose**: Create and populate the global symbol table used for interned symbol lookup.

**Actions**:
- Allocate the `USTable` object (typically a `Dictionary` or `Set`)
- For each `symbols[]` entry:
  - Locate corresponding `Symbol` object
  - Compute hash using Smalltalk’s canonical symbol hashing algorithm
  - Insert into `USTable` using that hash

**Requirements**:
- Must be performed last, after all `Symbol` objects are allocated
- Must use the same hashing algorithm the VM will use at runtime
- Must ensure no collisions or misplacements that would break symbol lookup

**Note**: This step is not explicitly described in the schema but is essential for runtime correctness.

---

## 🧠 Special Considerations

### Field Index Resolution

- Required for `objects[].fields[]`
- Must compute absolute field index using class graph traversal
- VM-recognized classes may override layout

### Symbol Bootstrapping

- `symbols[]` must be allocated before `Symbol` class is usable
- Allocate inert `Symbol` objects, patch later

### Determinism

- All allocations keyed by `localId`
- Install order must be strictly followed
- Patches resolve forward references and cycles

### VM Layout Overrides

Classes like `Symbol`, `CompiledMethod`, and `MethodDictionary` require hardcoded layout logic:
- `Symbol`: indexed string or byte array
- `CompiledMethod`: header + literal frame + bytecodes
- `MethodDictionary`: selector → method associations

---

## ✅ Output

- A fully wired Smalltalk-80 heap image
- All objects allocated and linked
- `USTable` populated using VM-compatible hashing
- Ready for VM execution
