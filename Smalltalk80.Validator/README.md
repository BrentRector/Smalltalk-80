# Smalltalk80.Validator

A comprehensive validation tool for Smalltalk-80 cold-start JSON bundles. Ensures structural integrity, semantic correctness, and Blue Book compliance of compiled Smalltalk-80 system images.

## Quick Start

### Installation

Build the validator as part of the Smalltalk-80 solution:

```bash
dotnet build Smalltalk80.sln -p:RunCompilerTests=false
```

### Basic Usage

Validate a cold-start JSON file:

```bash
dotnet run --project Smalltalk80.Validator -- path/to/coldstart.json
```

Example output:

```
Smalltalk-80 Validator
Input: bin/Debug/net9.0/coldstart.json

Loaded: 10059 methods, 474 classes

✓ Validation PASSED
```

### Command Line Options

```
validator <path-to-coldstart.json>
```

**Parameters:**
- `<path-to-coldstart.json>` - Path to the JSON bundle to validate (required)

**Exit Codes:**
- `0` - Validation passed (no errors)
- `1` - Validation failed (errors found) or file not found

---

## Overview

### Architecture

The validator is organized into four separate projects for modularity and testability:

- **[Smalltalk80.Validator](Smalltalk80.Validator.csproj)** - CLI application (main entry point)
- **[Core/](Core/)** - Core validation types (`ValidationContext`, `Diagnostic`, `IValidator`, 17 validator implementations)
- **[Engine/](Engine/)** - Orchestration (`ValidatorRunner`, `ValidatorPipeline`)
- **[Tests/](Tests/)** - Unit tests and test fixtures

### Validation Pipeline

The validator runs 17 validators in four phases:

1. **Phase 1: Structural Integrity** (fast, fail-fast)
   - Metadata counts
   - LocalId uniqueness
   - Symbol/string UTF-8 and uniqueness

2. **Phase 2: Semantic Validation**
   - Method headers
   - Bytecode integrity (CFG-based stack analysis)
   - Literal frames
   - Patches
   - Install order

3. **Phase 3: Deep Semantic Validation**
   - Class hierarchy
   - Method primitives
   - Method selectors
   - Pool dictionaries
   - Object formats
   - Block context closure graphs

4. **Phase 4: Static Analysis** (heavy validators)
   - Dead code detection

### Diagnostic System

Each validator produces structured diagnostics with:

- **Code** - Unique error code (e.g., `STV-META-COUNT-MISMATCH`)
- **Severity** - `Error`, `Warning`, or `Info`
- **Message** - Human-readable description
- **Locations** - Affected entities (section, localId, index, PC)
- **Suggestion** - Recommended fix
- **Metadata** - Additional context (optional)

Example diagnostic:

```csharp
new Diagnostic(
    Code: "STV-BYTECODE-STACK-ERROR",
    Severity: Error,
    Message: "Stack underflow at pc=42, opcode=0x61 (POP_STORE_IVAR_1)",
    Locations: [new Location("methods", "method:001234", Pc: 42)],
    Suggestion: "Fix bytecode generation to maintain stack discipline"
)
```

### Performance Characteristics

- **Fast validators** (16 validators) - Run in parallel, complete in <1 second for 10K methods
- **Heavy validators** (1 validator) - CFG-based analysis, ~2-3 seconds for 10K methods
- **Parallel execution** - Multiple validators run concurrently using `Task.WhenAll`
- **Deduplication** - Diagnostics are deduplicated by (Code + Location) to avoid noise
- **Memory efficiency** - Uses `JsonElement` for direct JSON access without POCO deserialization

---

## Validator Reference

### Phase 1: Structural Integrity

#### 1. MetadataCountsValidator

**Purpose:** Validates that metadata counts match actual array lengths in the bundle.

**What it validates:**
- `metadata.symbolCount` matches `symbols.length`
- `metadata.stringCount` matches `strings.length`
- `metadata.classCount` matches `classes.length`
- `metadata.methodCount` matches `methods.length`
- `metadata.objectCount` matches `objects.length`
- `metadata.initExpressionCount` matches `initExpressions.length`

**Error codes:**
- `STV-META-MISSING` - Metadata section is absent
- `STV-META-COUNT-MISMATCH` - Declared count doesn't match actual count

**Example diagnostic:**

```
STV-META-COUNT-MISMATCH: metadata.methodCount=10000 but actual array length=10059
Location: metadata/methodCount
Suggestion: Update metadata.methodCount to 10059 or fix compiler emitter
```

**Why it matters:** Metadata mismatches indicate compiler bugs or corrupted JSON. The interpreter relies on these counts for memory allocation.

---

#### 2. LocalIdUniquenessValidator

**Purpose:** Ensures all `localId` values are unique across the bundle and follow naming conventions.

**What it validates:**
- No duplicate `localId` values across all entities
- LocalIds use canonical prefixes: `class:`, `meta:`, `method:`, `object:`, `assoc:`, `pool:`, `blockctx:`, `init:`, `patch:`, `block:`
- Prefix matches the entity's section (e.g., `class:` only in `classes` section)
- No empty or missing localIds

**Error codes:**
- `STV-LOCALID-DUP` - Duplicate localId found
- `STV-LOCALID-PREFIX` - Non-standard or mismatched prefix
- `STV-LOCALID-MISSING` - Empty or missing localId

**Example diagnostic:**

```
STV-LOCALID-DUP: LocalId 'method:000042' appears 2 times across sections
Locations: methods/method:000042/0, methods/method:000042/1
Suggestion: Ensure compiler emits unique localIds
```

**Why it matters:** Duplicate localIds break allocation mapping and patch resolution. The interpreter uses localIds to wire object references during deserialization.

---

#### 3. SymbolUtf8AndUniquenessValidator

**Purpose:** Validates that symbols and strings are valid UTF-8 and symbols are unique.

**What it validates:**
- All symbols are valid UTF-8 encoded strings
- No duplicate symbols (symbols must be unique for interning)
- Symbols don't contain problematic control characters
- Strings are valid UTF-8 (warnings for null characters)
- No null or empty symbols

**Error codes:**
- `STV-SYMBOL-INVALID-UTF8` - Invalid UTF-8 encoding or control characters
- `STV-SYMBOL-DUP` - Duplicate symbol found
- `STV-STRING-INVALID-UTF8` - Invalid UTF-8 in string

**Example diagnostic:**

```
STV-SYMBOL-DUP: Duplicate symbol 'ifTrue:' at indices 42 and 137
Locations: symbols/42, symbols/137
Suggestion: Deduplicate symbols in the compiler emitter
```

**Why it matters:** Duplicate symbols defeat interning assumptions and selector packing. Symbol indices are used throughout the system for method lookup and must be unique.

---

### Phase 2: Bytecode Integrity

#### 4. BytecodeValidator (Heavy)

**Purpose:** Control-Flow-Graph-based stack depth validation using worklist propagation. This is the most sophisticated validator in the system.

**What it validates:**
- All bytes are in valid range [0, 255]
- Stack discipline maintained (no underflow/overflow)
- CFG-based analysis handles all control flow paths
- Join points have consistent stack depths
- Return instructions have correct stack depth (1 for RETURN_TOP)
- Extended bytecodes have required operand bytes
- Operand indices reference valid literals, temps, and instance variables

**Error codes:**
- `STV-BYTECODE-INVALID` - Invalid bytecode value or malformed instruction
- `STV-BYTECODE-STACK-ERROR` - Stack underflow, overflow, or inconsistency
- `STV-OPERAND-OUT-OF-RANGE` - Operand index exceeds bounds

**Example diagnostic:**

```
STV-BYTECODE-STACK-ERROR: Stack underflow at pc=42, opcode=0x61 (POP_STORE_IVAR_1): depth before=0, effect=-1, depth after=-1 (reached from PC 39)
Location: methods/method:001234/42
Suggestion: Fix bytecode generation to maintain stack discipline
```

**Algorithm details:**

1. **CFG Construction** - Parse bytecodes to build control flow graph
   - Identify jumps (conditional/unconditional), returns, fall-through
   - Build adjacency list: PC → list of successor PCs

2. **Worklist Propagation** - Compute stack depth at each PC
   - Start from PC 0 with depth 0
   - Propagate depths through CFG using queue-based flood fill
   - Compute stack effect for each instruction
   - Detect underflow (depth < 0) and inconsistent join points

3. **Return Validation** - Verify return instructions
   - `RETURN_TOP` (0x7C) must have depth=1
   - `RETURN_TOP_FROM_BLOCK` (0x7D) must have depth=1
   - `RETURN_TOP_FROM_METHOD` (0x7E) must have depth=1

4. **Operand Validation** - Check index bounds (only if stack is valid)
   - Instance variable indices < class instVarCount
   - Temporary indices < (argCount + tempCount)
   - Literal indices < literal frame size

**Special handling:**
- Blocks have `instVarCount = Int32.MaxValue` to skip instance variable validation
- Extended bytecodes (0x80) require sub-opcode and operand bytes
- Send instructions compute effect as: `-(argCount + 1) + 1 = -argCount`

**Why it matters:** Invalid bytecode causes VM crashes. CFG-based analysis is more robust than linear scanning because it handles complex control flow (loops, branches, exception handlers).

---

#### 5. OperandValidator (Helper)

**Purpose:** Static analysis helper that validates bytecode operand indices without execution.

**What it validates:**
- Instance variable indices are in range [0, instVarCount)
- Temporary variable indices are in range [0, argCount + tempCount)
- Literal indices are in range [0, literalCount)
- Selector literal indices for send instructions
- Extended bytecode operand structure

**Error codes:**
- `STV-OPERAND-OUT-OF-RANGE` - Operand index out of bounds

**Example diagnostic:**

```
STV-OPERAND-OUT-OF-RANGE: Literal index 42 out of range (max 31)
Location: methods/method:001234/15
Suggestion: Fix bytecode operand indices
```

**Bytecode coverage:**
- `0x00-0x0F` - PUSH_IVAR_0..15 → validate instance variable index
- `0x10-0x1F` - PUSH_TEMP_0..15 → validate temp index
- `0x20-0x3F` - PUSH_LITERAL_CONST → validate literal index
- `0x40-0x5F` - PUSH_LITERAL_VAR → validate literal index
- `0x60-0x67` - POP_STORE_IVAR → validate instance variable index
- `0x68-0x6F` - POP_STORE_TEMP → validate temp index
- `0x80` - EXTENDED → validate sub-opcode and operands
- `0xD0-0xFF` - SEND_LIT → validate selector literal index

**Why it matters:** Out-of-bounds operands cause array access violations in the VM. This validator catches these errors before runtime.

---

### Phase 3: Method Structure

#### 6. MethodHeaderValidator

**Purpose:** Validates method headers conform to Blue Book encoding specification.

**What it validates:**
- Header word encoding matches Blue Book format (bits 0-15):
  - Bits 0-5: Literal count (0-63)
  - Bit 6: Large frame flag
  - Bits 7-11: Temporary count (0-31)
  - Bits 12-14: Flags field (0-7)
  - Bit 15: Reserved
- Flags field interpretation:
  - 0-4: Simple form (argCount = flags, no primitive)
  - 7: Extended form (argCount and primitive in last literal)
  - 5-6: Quick-return forms (not yet supported)
- Header fields match decoded word values
- Actual literal count matches header
- Temp/arg names match header counts
- Selector indices are valid

**Error codes:**
- `STV-METHOD-HEADER-INVALID` - Header encoding error or field mismatch

**Example diagnostic:**

```
STV-METHOD-HEADER-INVALID: Header argCount (3) doesn't match decoded value (2) from flags=2
Location: methods/method:001234
Suggestion: Update header.argCount to 2
```

**Blue Book header encoding:**

```
Format (16 bits):
┌─────────────┬────────┬────┬────────────┬────────────────┐
│  Reserved   │ Flags  │ LF │   Temps    │    Literals    │
│   (1 bit)   │(3 bits)│(1) │  (5 bits)  │   (6 bits)     │
└─────────────┴────────┴────┴────────────┴────────────────┘
     15       14-12    11  10-7  6        5-0

LF = Large Frame (extended context)
Flags interpretation:
  0-4: argCount (simple form, no primitive)
  7:   Extended (last literal = (primitiveIndex | (argCount << 8)))
```

**Why it matters:** The interpreter uses header encoding to determine method arity, context size, and primitive index. Incorrect headers cause method invocation failures.

---

#### 7. LiteralFrameValidator

**Purpose:** Validates literal frames reference valid symbols/strings and follow Blue Book requirements.

**What it validates:**
- Symbol literal indices are in range [0, symbols.length)
- String literal indices are in range [0, strings.length)
- Association keys are symbols (Blue Book requirement)
- Association key indices reference valid symbols

**Error codes:**
- `STV-LITERAL-FRAME-INVALID` - Invalid literal reference or structure

**Example diagnostic:**

```
STV-LITERAL-FRAME-INVALID: Literal[5] symbol index 9999 out of bounds (symbols.length = 3914)
Location: methods/method:001234
Suggestion: Symbol index must be < 3914
```

**Literal types validated:**
- `symbol` - `{"type": "symbol", "index": 42}`
- `string` - `{"type": "string", "index": 137}`
- `association` - `{"type": "association", "association": {"keyType": "symbol", "keyIndex": 42, ...}}`

**Why it matters:** Invalid literal references cause VM crashes when accessing symbol/string tables. Association keys must be symbols per Blue Book specification.

---

### Phase 4: Install Order

#### 8. PatchValidator

**Purpose:** Validates basic patch structure and required fields.

**What it validates:**
- Every patch has a `patchId`
- Every patch specifies an `operation`
- Operations are non-empty strings

**Error codes:**
- `STV-PATCH-INVALID` - Missing patchId or operation

**Example diagnostic:**

```
STV-PATCH-INVALID: Patch 'patch:000042' missing operation
Location: patches/patch:000042
Suggestion: All patches must specify an operation
```

**Why it matters:** Patches apply late-stage fixups during deserialization. Missing fields cause patch application failures.

---

#### 9. InstallOrderValidator

**Purpose:** Validates install order contains all entities and has no duplicates.

**What it validates:**
- All classes, objects, methods, init expressions, and patches appear in install order
- No entity appears multiple times
- Phase references point to existing entities
- Special handling for patches (use `patchId` not `localId`)

**Error codes:**
- `STV-INSTALL-MISSING` - Install order references non-existent localId
- `STV-INSTALL-OMITTED` - Entity not found in any install phase
- `STV-INSTALL-DUP` - Entity appears in multiple phases

**Example diagnostic:**

```
STV-INSTALL-OMITTED: LocalId 'method:001234' not found in any install phase
Location: installOrder
Suggestion: Add entity to appropriate install phase
```

**Critical fix applied:** Patches use `patchId` property instead of `localId`, so validator checks both:

```csharp
foreach (var patch in ctx.Patches)
{
    var patchId = patch.GetStringProp("patchId");  // Not "localId"!
    if (patchId != null) expectedLocalIds.Add(patchId);
}
```

**Why it matters:** The interpreter processes entities in install order. Missing entities cause allocation failures; duplicates cause wiring conflicts.

---

#### 10. InstallOrderCoverageValidator

**Purpose:** Enhanced install order validation with phase kind matching.

**What it validates:**
- All entities appear exactly once in install order
- Phase `kind` matches entity types:
  - `"classes"` phase contains `classes` entities
  - `"methods"` phase contains `methods` entities
  - `"objects"` phase contains `objects` entities
  - `"initExpressions"` or `"inits"` phase contains `initExpressions`
  - `"patches"` phase contains `patches`
- No orphaned entities (exist in bundle but not in install order)
- Special handling for patches (check both `IndexByLocalId` and `Patches` collection)

**Error codes:**
- `STV-INSTALL-COVERAGE-MISSING` - Entity in install order doesn't exist
- `STV-INSTALL-COVERAGE-PHASE-MISMATCH` - Phase kind doesn't match entity type

**Example diagnostic:**

```
STV-INSTALL-KIND-MISMATCH: Phase 'Methods' has kind='methods' but contains classes entity 'class:000042'
Location: installOrder/class:000042
Suggestion: Move entity to appropriate phase or update phase kind to 'classes'
```

**Critical fix applied:** Patches use `patchId`, so validator checks both indices:

```csharp
bool exists = ctx.IndexByLocalId.ContainsKey(localId);
if (!exists && localId.StartsWith("patch:"))
{
    // Patches use patchId, not localId, so check in Patches collection
    exists = ctx.Patches.Any(p => p.GetStringProp("patchId") == localId);
}
```

**Why it matters:** Phase kind validation ensures correct dependency ordering. Objects must be allocated before methods reference them.

---

### Phase 5: Semantic Checks

#### 11. ClassHierarchySanityValidator

**Purpose:** Validates class hierarchy is well-formed and acyclic.

**What it validates:**
- Superclass references point to existing classes
- No inheritance cycles (A → B → C → A)
- Root classes properly marked (nil superclass)
- Metaclass hierarchy mirrors instance hierarchy
- System has expected root class (Object)
- Unusual inheritance patterns flagged

**Error codes:**
- `STV-CLASS-SUPER-MISSING` - Superclass reference invalid or missing
- `STV-CLASS-SUPER-CYCLE` - Inheritance cycle detected

**Example diagnostic:**

```
STV-CLASS-SUPER-CYCLE: Inheritance cycle detected: 'MyClass' -> 'YourClass' forms a cycle
Location: classes
Suggestion: Remove cycle from inheritance chain
```

**Cycle detection algorithm:**
1. Build superclass map: `className → superclassName`
2. For each class, walk up inheritance chain
3. Track visited classes to detect cycles
4. Use recursion stack to identify cycle participants

**Known root classes:** `Object`, `UndefinedObject`, `ProtoObject`

**Why it matters:** Inheritance cycles cause infinite loops in method lookup. Missing superclasses prevent class initialization.

---

#### 12. SuperclassResolutionValidator

_(Note: This validator is mentioned but not found in current code. It may have been merged into ClassHierarchySanityValidator.)_

---

#### 13. InstanceVariableValidator

_(Note: This validator is mentioned but not found in current code. Instance variable validation is performed by OperandValidator and ClassHierarchySanityValidator.)_

---

#### 14. MethodSelectorPresenceValidator

**Purpose:** Ensures method selectors are present and valid (except for blocks).

**What it validates:**
- Regular methods have valid selector indices
- Selectors are non-empty strings
- Blocks (selectorIndex=-1) are skipped
- Selector index is in range [0, symbols.length)

**Error codes:**
- `STV-METHOD-SELECTOR-INVALID` - Invalid or missing selector

**Example diagnostic:**

```
STV-METHOD-SELECTOR-INVALID: Method 'method:001234' has invalid selector index 9999
Location: methods/method:001234
Suggestion: Method must have valid selector
```

**Why it matters:** Method selectors are used for method dictionary lookup. Invalid selectors prevent method invocation.

---

#### 15. LiteralConsistencyValidator

_(Note: This validator is mentioned but appears to be covered by LiteralFrameValidator in the current implementation.)_

---

#### 16. MethodPrimitiveValidityValidator

**Purpose:** Validates primitive indices and encoding in method headers.

**What it validates:**
- Primitive indices are in valid range [0, 255]
- Blocks have primitiveIndex=0 (blocks cannot have primitives)
- Methods with primitives use extended header form (flags=7)
- Primitive encoding matches header flags

**Error codes:**
- `STV-METHOD-PRIMITIVE-INVALID` - Invalid primitive index or encoding

**Example diagnostic:**

```
STV-METHOD-PRIMITIVE-INVALID: Block 'block:000042' has primitiveIndex=75 (blocks cannot have primitives)
Location: methods/block:000042
Suggestion: Set primitiveIndex to 0 for blocks
```

**Why it matters:** Primitives are VM-level operations. Invalid primitive indices cause runtime errors when the VM attempts to dispatch them.

---

#### 17. BlockCompilationValidator → BlockContextClosureGraphValidator

**Purpose:** Validates BlockContext closure graph integrity and lexical scoping.

**What it validates:**
- `homeContext` chains terminate at methods (not cycles)
- `outerContext` references point to methods (lexical parent)
- No orphaned blocks
- Proper lexical scoping structure
- No cycles in closure graph

**Error codes:**
- `STV-BLOCK-HOME-MISSING` - homeContext reference invalid
- `STV-BLOCK-OUTER-MISSING` - outerContext reference invalid
- `STV-BLOCK-OUTER-NOT-METHOD` - outerContext is not a method
- `STV-BLOCK-CYCLE` - Closure cycle detected

**Example diagnostic:**

```
STV-BLOCK-CYCLE: Closure cycle detected: 'blockctx:000042' -> 'blockctx:000137' forms a cycle
Location: objects/blockctx:000042
Suggestion: HomeContext chains must not form cycles
```

**Validation algorithm:**
1. Identify all BlockContext objects (formatHint="blockContext")
2. Extract homeContext and outerContext references from fields
3. Verify homeContext chains reach methods (recursive walk)
4. Verify outerContext references are methods
5. Detect cycles using DFS with recursion stack

**Why it matters:** Blocks capture their lexical environment. Invalid closure graphs cause context lookup failures and memory leaks.

---

#### 18. PoolDictionaryUsageValidator

**Purpose:** Validates pool dictionary references and usage patterns.

**What it validates:**
- Pool localIds reference actual objects
- Classes that reference pools have them available
- Pool objects are marked with appropriate formatHint
- No orphaned pool dictionaries (unreferenced)

**Error codes:**
- `STV-POOL-DICT-MISSING` - Class references non-existent pool
- `STV-POOL-DICT-INVALID` - Pool reference is not a dictionary object

**Example diagnostic:**

```
STV-POOL-DICT-MISSING: Class 'MyClass' references non-existent pool 'pool:000042'
Location: classes/class:001234
Suggestion: Pool dictionary must exist
```

**Pool identification:**
- LocalId starts with `pool:`
- `formatHint = "pool"` or `"poolDictionary"`

**Why it matters:** Pool dictionaries provide shared variables (like class variables but shared across unrelated classes). Invalid references cause variable lookup failures.

---

#### 19. ObjectFormatValidator

**Purpose:** Validates object formats match class format words.

**What it validates:**
- `formatHint` is compatible with class format word
- Field counts match class fixed field count
- Blue Book format word interpretation (Chapter 27):
  - Bit 0: isPointers (1=contains pointers)
  - Bit 1: isWords (1=word-addressed)
  - Bit 2: isIndexable (1=has indexed fields)
  - Bits 4-14: fixedFields (0-2047)

**Error codes:**
- `STV-OBJECT-FORMAT-INVALID` - Format mismatch or invalid field count

**Example diagnostic:**

```
STV-OBJECT-FORMAT-INVALID: Object formatHint 'pointers' is incompatible with class format (instanceSpec=0x3, indexable=false, pointers=false)
Location: objects/object:001234
Suggestion: Update formatHint to match class format
```

**Format hints:**
- `"pointers"` - instanceSpec=0x01 or (0x02 && hasPointers)
- `"words"` - instanceSpec=0x02 && !hasPointers
- `"bytes"` - instanceSpec=0x03
- `"blockContext"` - instanceSpec=0x02 && hasPointers && isIndexable
- `"compiledMethod"` - instanceSpec=0x02 && !hasPointers && isIndexable

**Why it matters:** Object format determines memory layout and GC behavior. Mismatches cause memory corruption.

---

#### 20. PatchTargetExistenceValidator

**Purpose:** Validates patch targets exist and operations are valid.

**What it validates:**
- Target localIds exist in bundle
- Field/literal indices are in range
- Operations are valid: `wireMetaclass`, `setValue`, `setAssociationValue`, `addMethod`, `setSuperclass`, `setField`, `setLiteral`, `setInstVar`, `setClassVar`, `addToMethodDictionary`, `initializeClass`, `custom`
- Operation-specific requirements (e.g., `addMethod` requires value)

**Error codes:**
- `STV-PATCH-TARGET-MISSING` - Target localId doesn't exist
- `STV-PATCH-INVALID` - Invalid operation or missing required fields

**Example diagnostic:**

```
STV-PATCH-TARGET-MISSING: Patch 'patch:000042' targets non-existent localId 'method:009999'
Location: patches/patch:000042
Suggestion: Target localId must exist
```

**Target kinds validated:**
- `"metaclassWiring"` - Must target metaclass (localId starts with `meta:`)
- `"methodLiteral"` - Literal index must be < method's literal count
- `"objectField"` - Field index must be < object's field count

**Why it matters:** Patches wire object references during deserialization. Invalid targets cause allocation failures.

---

### Phase 6: Static Analysis (Heavy Validators)

#### 21. DeadCodeDetectionValidator (Heavy)

**Purpose:** Detects unreachable bytecode using control flow analysis.

**What it validates:**
- Identifies unreachable bytecode not targeted by any jumps
- Detects code after unconditional returns
- Flags large unreachable regions (likely compiler bugs)

**Error codes:**
- `STV-DEAD-CODE-DETECTED` - Dead code found
- `STV-BYTECODE-UNREACHABLE` - Unreachable bytecode region

**Example diagnostic:**

```
STV-BYTECODE-UNREACHABLE: Method 'method:001234' has 15 bytes of unreachable code at PC 42-56
Location: methods/method:001234/42
Suggestion: Large unreachable regions indicate compiler bugs
```

**Detection algorithm:**
1. **Identify jump targets** - First pass marks all jump destination PCs
2. **Flood fill** - Second pass marks reachable code:
   - Start from PC 0
   - Follow fall-through for normal instructions
   - Follow targets for jumps
   - Stop at returns
3. **Find dead regions** - Third pass identifies contiguous unreachable regions

**Severity thresholds:**
- ≥10 bytes unreachable → `Warning` (likely compiler bug)
- 3-9 bytes unreachable → `Info` (minor optimization opportunity)
- <3 bytes unreachable → Ignored (not reported)

**Why it matters:** Large dead code regions indicate compiler bugs. Small regions indicate missed optimization opportunities. Dead code bloats the image without adding functionality.

---

## Diagnostic System

### Diagnostic Structure

```csharp
public record Diagnostic(
    string Code,              // Unique error code (e.g., "STV-BYTECODE-STACK-ERROR")
    Severity Severity,        // Error, Warning, or Info
    string Message,           // Human-readable description
    IReadOnlyList<Location> Locations,  // Affected entities
    string Suggestion,        // Recommended fix
    IReadOnlyDictionary<string,string>? Metadata = null  // Additional context
);
```

### Severity Levels

- **Error** - Must be fixed for interpreter to work correctly
  - Examples: Stack underflow, missing localId, invalid bytecode
  - Exit code: 1 (validation failed)

- **Warning** - Should be fixed but may not prevent execution
  - Examples: Dead code, non-standard naming, unusual patterns
  - Exit code: 0 (validation passed, but warnings present)

- **Info** - Informational, no action required
  - Examples: Small dead code regions, unused pool dictionaries
  - Exit code: 0

### Location Format

```csharp
public record Location(
    string Section,       // "classes", "methods", "objects", etc.
    string? LocalId = null,   // Entity localId (if applicable)
    int? Index = null,        // Array index (if applicable)
    int? Pc = null            // Bytecode PC (if applicable)
);
```

**Examples:**
- `new Location("methods", "method:001234")` - Method entity
- `new Location("symbols", Index: 42)` - Symbol at index 42
- `new Location("methods", "method:001234", Pc: 15)` - Bytecode at PC 15

### Suggestions

Every diagnostic includes an actionable suggestion:

- "Update metadata.methodCount to 10059"
- "Fix bytecode generation to maintain stack discipline"
- "Remove cycle from inheritance chain"
- "Set primitiveIndex to 0 for blocks"

### Metadata

Optional key-value pairs for additional context:

```csharp
Metadata: new Dictionary<string, string>
{
    ["depth_before"] = "0",
    ["effect"] = "-1",
    ["depth_after"] = "-1",
    ["opcode"] = "0x61",
    ["predecessor"] = "39"
}
```

---

## Extension Guide

### Creating Custom Validators

Implement the `IValidator` interface:

```csharp
using Smalltalk80.Validator.Core;
using Smalltalk80.Validator.Core.Diagnostics;

public class MyCustomValidator : IValidator
{
    public string Id => "MyCustomValidator";
    public bool IsHeavy => false;  // Set to true for expensive validators

    public Task<IEnumerable<Diagnostic>> ValidateAsync(
        ValidationContext ctx,
        CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Your validation logic here
        foreach (var method in ctx.Methods)
        {
            // Access method properties
            var localId = method.GetStringProp("localId");
            var bytecodes = method.GetIntArray("bytecodes");

            // Add diagnostics
            diagnostics.Add(new Diagnostic(
                "MY-CUSTOM-ERROR",
                Severity.Error,
                "Custom validation failed",
                new[] { new Location("methods", localId) },
                "Fix the issue"));
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
```

### Implementing IValidator

**Required properties:**
- `string Id` - Unique identifier for the validator
- `bool IsHeavy` - Set to `true` for expensive validators (CFG analysis, etc.)

**Required method:**
- `Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct)`

**Best practices:**
- Return empty list for success (no diagnostics)
- Use descriptive error codes with prefix `MY-` or `STV-`
- Provide actionable suggestions
- Include location information
- Use appropriate severity levels
- Make validators stateless (no instance fields)
- Support cancellation tokens for long-running operations

### Adding Validators to Pipeline

Edit [Program.cs](Program.cs:29-53):

```csharp
var pipeline = new ValidatorPipeline()
    // Existing validators...
    .AddValidator(new MetadataCountsValidator())
    .AddValidator(new LocalIdUniquenessValidator())

    // Add your custom validator
    .AddValidator(new MyCustomValidator())

    // More validators...
    .AddValidator(new DeadCodeDetectionValidator());

var runner = pipeline.Build();
var diagnostics = await runner.RunAllAsync(ctx, includeHeavy: true);
```

**Pipeline ordering:**
1. Fast structural validators first (fail-fast)
2. Semantic validators second
3. Heavy validators last (only if needed)

### Testing Validators

Create test fixtures in [Tests/Fixtures/](Tests/Fixtures/):

```csharp
using Xunit;
using Smalltalk80.Validator.Core;

public class MyCustomValidatorTests
{
    [Fact]
    public async Task ValidateAsync_WhenValid_ReturnsNoDiagnostics()
    {
        // Arrange
        var json = LoadTestFixture("valid-bundle.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new MyCustomValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ValidateAsync_WhenInvalid_ReturnsError()
    {
        // Arrange
        var json = LoadTestFixture("invalid-bundle.json");
        using var doc = JsonDocument.Parse(json);
        var ctx = ValidationContext.Build(doc);
        var validator = new MyCustomValidator();

        // Act
        var diagnostics = await validator.ValidateAsync(ctx);

        // Assert
        var error = Assert.Single(diagnostics);
        Assert.Equal("MY-CUSTOM-ERROR", error.Code);
        Assert.Equal(Severity.Error, error.Severity);
    }
}
```

---

## Technical Details

### JSON Schema Version

The validator targets **v1.0.3** of the cold-start JSON schema:

- Schema location: `docs/schemas/coldstart-v1.0.3.schema.json`
- Blocks compile to separate CompiledMethod objects (selector=null)
- All method headers include: word, literalCount, tempCount, argCount, primitiveIndex, largeContext
- Bytecodes as integer arrays (no base64)
- Patches use `patchId` instead of `localId`

### ValidationContext Implementation

The `ValidationContext` class provides efficient read-only access to the JSON bundle:

```csharp
public sealed class ValidationContext
{
    public JsonDocument Bundle { get; }
    public IReadOnlyList<string> Symbols { get; init; }
    public IReadOnlyList<JsonElement> Classes { get; init; }
    public IReadOnlyList<JsonElement> Methods { get; init; }
    public IReadOnlyList<JsonElement> Objects { get; init; }
    public IReadOnlyList<JsonElement> InitExpressions { get; init; }
    public IReadOnlyList<JsonElement> Patches { get; init; }

    // O(1) lookup by localId
    public IReadOnlyDictionary<string, IReadOnlyList<EntityRef>> IndexByLocalId { get; init; }

    public static ValidationContext Build(JsonDocument doc) { /* ... */ }
}
```

**Key features:**
- Uses `JsonElement` for direct JSON access (no POCO deserialization overhead)
- Builds `IndexByLocalId` for O(1) lookups during validation
- Immutable after construction (thread-safe for parallel validators)
- Lazy evaluation of expensive properties

**EntityRef structure:**

```csharp
public record EntityRef(string Section, JsonElement Item);
```

### Parallel Execution

The `ValidatorRunner` executes validators in parallel:

```csharp
public async Task<IReadOnlyList<Diagnostic>> RunAllAsync(
    ValidationContext ctx,
    bool includeHeavy = false,
    CancellationToken ct = default)
{
    var tasks = new List<Task<IEnumerable<Diagnostic>>>();

    foreach (var v in _validators.Where(v => !v.IsHeavy || includeHeavy))
    {
        tasks.Add(RunValidatorSafe(v, ctx, ct));
    }

    await Task.WhenAll(tasks).ConfigureAwait(false);

    var combined = tasks.SelectMany(t => t.Result).ToList();

    // Deduplicate diagnostics by (Code + Location)
    var dedup = combined
        .GroupBy(d => (d.Code, LocKey(d.Locations)))
        .Select(g => g.First())
        .OrderBy(d => d.Severity)
        .ThenBy(d => d.Code)
        .ToList();

    return dedup;
}
```

**Performance characteristics:**
- Non-heavy validators run in parallel (16 validators)
- Heavy validators run sequentially (1 validator)
- Exception handling wraps each validator to prevent cascading failures
- Total validation time: ~2-3 seconds for 10K methods

### Diagnostic Deduplication

Diagnostics are deduplicated by combining error code and location:

```csharp
private string LocKey(IReadOnlyList<Location> locs)
{
    return string.Join(";", locs
        .Select(loc => $"{loc.Section}/{loc.LocalId}/{loc.Index}/{loc.Pc}")
        .OrderBy(s => s));
}
```

**Why deduplication matters:**
- Same error can be detected by multiple validators
- Reduces noise in output
- Stable ordering (Severity → Code → Location)

### Special Handling: patchId vs localId

**Critical distinction:** Patches use `patchId` property instead of `localId`.

**Affected validators:**
1. **InstallOrderValidator** - Collects patch IDs:
   ```csharp
   foreach (var patch in ctx.Patches)
   {
       var patchId = patch.GetStringProp("patchId");  // Not "localId"!
       if (patchId != null) expectedLocalIds.Add(patchId);
   }
   ```

2. **InstallOrderCoverageValidator** - Checks both indices:
   ```csharp
   bool exists = ctx.IndexByLocalId.ContainsKey(localId);
   if (!exists && localId.StartsWith("patch:"))
   {
       exists = ctx.Patches.Any(p => p.GetStringProp("patchId") == localId);
   }
   ```

**Why this matters:** This was a critical bug fix that resolved 1,746 validation errors. Patches are stored separately and use different property names.

---

## Troubleshooting

### Common Errors

#### "File not found: coldstart.json"

**Cause:** Incorrect path to JSON file.

**Fix:**
```bash
# Use absolute path
dotnet run --project Smalltalk80.Validator -- /full/path/to/coldstart.json

# Or relative path from Smalltalk80.Validator directory
dotnet run --project Smalltalk80.Validator -- ../bin/Debug/net9.0/coldstart.json
```

#### "STV-META-COUNT-MISMATCH" errors

**Cause:** Compiler emitted incorrect metadata counts.

**Fix:** Regenerate JSON with updated compiler:
```bash
cd "Smalltalk-80 Source"
dotnet run --project ../Smalltalk80.Compiler -- Smalltalk-80.st
```

#### "STV-BYTECODE-STACK-ERROR" errors

**Cause:** Compiler generated invalid bytecode sequences.

**Fix:** This indicates a compiler bug. Report the error with:
- Method localId
- Bytecode sequence (from JSON)
- Stack trace from error message

#### "STV-INSTALL-OMITTED" for 1,746 patches

**Cause:** Install order uses `localId` but patches use `patchId`.

**Fix:** This should be handled automatically by the validator. If you see this error, ensure you're running the latest version:
```bash
git pull
dotnet build -p:RunCompilerTests=false
```

### Performance Issues

#### Validation takes too long (>10 seconds)

**Cause:** Heavy validators enabled with very large bundles.

**Fix:** Run without heavy validators:
```bash
# Modify Program.cs line 56:
var diagnostics = await runner.RunAllAsync(ctx, includeHeavy: false);
```

#### Out of memory errors

**Cause:** Bundle is extremely large (>100MB JSON).

**Fix:**
- Increase heap size: `dotnet run --property:DOTNET_GCHeapHardLimit=4000000000`
- Split bundle into smaller chunks
- Use streaming JSON parser (requires code changes)

### Heavy Validator Usage

Heavy validators perform expensive analysis (CFG construction, dead code detection). They can be disabled for faster validation:

```csharp
// In Program.cs
var diagnostics = await runner.RunAllAsync(ctx, includeHeavy: false);
```

**Heavy validators:**
- `BytecodeValidator` - CFG-based stack analysis (~1-2 seconds)
- `BlockContextClosureGraphValidator` - Closure graph traversal (<1 second)
- `DeadCodeDetectionValidator` - Reachability analysis (<1 second)

**When to disable:**
- CI/CD pipelines (use `includeHeavy: false`)
- Quick validation during development
- Very large bundles (>20K methods)

**When to enable:**
- Final validation before release
- Investigating compiler bugs
- Debugging bytecode issues

---

## References

### Smalltalk-80 Blue Book

*Smalltalk-80: The Language and its Implementation* by Adele Goldberg and David Robson (1983)

**Relevant chapters:**
- Chapter 30: The Bytecode Interpreter
  - Bytecode encoding and stack effects
  - Control flow instructions
  - Method invocation protocol

- Chapter 29: The Virtual Image
  - Object format words
  - Class hierarchy
  - Method compilation

### JSON Schema Documentation

**Schema location:** [docs/schemas/coldstart-v1.0.3.schema.json](../docs/schemas/coldstart-v1.0.3.schema.json)

**Key sections:**
- Metadata structure
- Class definitions
- Method compilation format
- Install order phases
- Patch operations

### Related Projects

- **[Smalltalk80.Compiler](../Smalltalk80.Compiler/)** - Compiles Smalltalk-80.st to JSON
- **[Smalltalk80.Interpreter](../Smalltalk80.Interpreter/)** - VM runtime that consumes validated JSON
- **[Smalltalk-80 Source](../Smalltalk-80%20Source/)** - Canonical source file (READ-ONLY)

### Architecture Documentation

**[docs/Interpreter-Architecture.md](../docs/Interpreter-Architecture.md)** - Detailed interpreter architecture

**Relevant sections:**
- Phase 6: Object Allocation
- Phase 7: Method Installation
- Phase 8: Patch Application
- Bytecode specifications

---

## License

See [LICENSE](../LICENSE) in the root directory.

## Contributing

See [CONTRIBUTING.md](../CONTRIBUTING.md) for contribution guidelines.

---

**Version:** 1.0.3
**Last Updated:** 2025-01-10
**Validation Coverage:** 17 validators, 100% passing on production data (10,059 methods, 474 classes)
