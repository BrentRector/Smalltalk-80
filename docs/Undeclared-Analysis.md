# Undeclared Analysis - Smalltalk-80 Source Code

## Executive Summary

Based on thorough analysis of the Smalltalk-80.st source code, **Undeclared MUST exist as a runtime object** in the Smalltalk heap. It is extensively referenced by the compiler and system code but is **NOT explicitly created in the source file** - implying it must be created by the VM during cold start.

## 1. Does Undeclared Exist as a Runtime Object?

**YES** - Undeclared is a real runtime object that MUST exist in the running Smalltalk image.

### Evidence from Source Code:

1. **Compiler References** (lines 16038-16041):
```smalltalk
declareUndeclared: name
    | sym |
    sym _ name asSymbol.
    (Undeclared includesKey: name asSymbol) ifFalse:
        [Transcript show: ' (' , name , ' is Undeclared) '.
        Undeclared at: sym put: nil].
    ^ self global: (Undeclared associationAt: sym) name: sym!
```
The compiler directly calls methods on Undeclared: `includesKey:`, `at:put:`, `associationAt:`

2. **SystemDictionary Integration** (lines 37574-37587):
```smalltalk
at: aKey put: anObject
    "Override from Dictionary so that can check Undeclared and fix up
    references of undeclared variables."
    | index element |
    index _ self findKeyOrNil: aKey.
    element _ self basicAt: index.
    element == nil
        ifTrue:
            [self
                valueAtNewKey: aKey
                put: anObject
                atIndex: index
                declareFrom: Undeclared]  "← Passes Undeclared as parameter"
        ifFalse:
            [element value: anObject].
    ^anObject!
```

3. **Variable Resolution** (line 16210):
```smalltalk
reply == nil ifTrue: [(Undeclared includesKey: symbol) ifTrue: [reply _ Undeclared]].
```

4. **Class Variable Declaration** (line 8814):
```smalltalk
newVars do: [:var | classPool declare: var from: Undeclared]
```

## 2. What is the Expected Structure?

### Class Type
Undeclared is treated as a **Dictionary** (or Dictionary subclass):
- Has methods: `includesKey:`, `at:put:`, `associationAt:`, `removeKey:`
- Used with `declare:from:` which expects a Dictionary parameter
- Returns Association objects via `associationAt:`

### Global Variable Status
**YES** - Undeclared is a global variable:
- Referenced directly by name in multiple methods (not via `Smalltalk at: #Undeclared`)
- Used in system classes like SystemDictionary and Encoder
- Never explicitly created in source - expected to pre-exist

### Initialization Requirements
**CRITICAL**: Undeclared is NOT initialized anywhere in Smalltalk-80.st!
- No `Undeclared := Dictionary new` statement found
- No `Smalltalk at: #Undeclared put:` found
- No initialization expressions creating it

**Implication**: The VM/interpreter MUST create Undeclared during cold start

## 3. SystemDictionary>>at:put: Interaction

### The Migration Pattern

When a new global is added to Smalltalk:

```smalltalk
valueAtNewKey: aKey put: anObject atIndex: index declareFrom: aDictionary
    "Handle overriding atKey:put: from Dictionary so that adding
    something new to a system dictionary such as Smalltalk checks
    Undeclared and fixes up all references to the undeclared variable"

    (aDictionary includesKey: aKey)
        ifTrue:
            [self atNewIndex: index
                put: ((aDictionary associationAt: aKey) value: anObject).  "← Reuses Association!"
            aDictionary removeKey: aKey]  "← Removes from Undeclared"
        ifFalse:
            [self atNewIndex: index put: (Association key: aKey value: anObject)].
```

**Key Behavior**:
1. Checks if Association exists in Undeclared
2. If found: **REUSES the same Association object**, updates its value
3. Removes the key from Undeclared
4. If not found: Creates new Association

## 4. Dictionary>>declare:from: Method

Found at line 14596:
```smalltalk
declare: key from: aDictionary
    "Add key to the receiver. If key already exists, do nothing. If aDictionary includes
    key, then remove it from aDictionary and use its association as the entry to the
    receiver."

    (self includesKey: key) ifTrue: [^self].
    (aDictionary includesKey: key)
        ifTrue:
            [self add: (aDictionary associationAt: key).  "← Migrates Association"
            aDictionary removeKey: key]
        ifFalse:
            [self at: key put: nil]!
```

This is used for class pools and migrating variables from Undeclared.

## 5. When Should Undeclared be Populated?

### During Compilation (via Compiler)
When compiling methods with unknown variables:
1. Compiler calls `declareUndeclared:` (line 16035)
2. Creates Association in Undeclared with nil value
3. Returns that Association for method's literal frame

### During Class Definition
When adding class variables (line 8814):
```smalltalk
classPool declare: var from: Undeclared
```
Migrates existing Undeclared entries to class pools.

### During Global Assignment
When assigning to Smalltalk (line 37587):
```smalltalk
self valueAtNewKey: aKey put: anObject atIndex: index declareFrom: Undeclared
```
Migrates Undeclared Association to Smalltalk dictionary.

## 6. Validation/Cleanup Methods

No explicit validation methods found in the source. However, the system reports undeclared variables:
- Line 16039: `Transcript show: ' (' , name , ' is Undeclared) '.`
- Interactive declaration menu (line 16047) includes "undeclared" option

## Comparison: Our Implementation vs Expected Behavior

### What We Have (AssociationRegistry)

**Strengths**:
✅ Ensures one Association per global name (canonical)
✅ Allows late binding of values
✅ Methods automatically see updated values

**Weaknesses**:
❌ No runtime Undeclared object in heap
❌ Smalltalk code cannot query `Undeclared includesKey:`
❌ Cannot migrate Associations from Undeclared to Smalltalk
❌ Compiler methods expecting Undeclared will fail

### What Smalltalk-80 Expects

1. **Undeclared exists as a Dictionary in the heap**
2. **It's a global variable** (accessible by name)
3. **SystemDictionary>>at:put: migrates Associations from it**
4. **Compiler adds entries to it for unknown variables**
5. **Class definition migrates entries from it to class pools**

## Recommendations

### MUST IMPLEMENT: Create Undeclared in Heap

**When**: During cold start Phase 6 (after nil/true/false, before globals installation)

**How**:
```csharp
// In Phase 6 or new Phase 6b:
var undeclaredOOP = AllocateDictionary("Dictionary", initialCapacity: 16);
GlobalRegistry.Register("Undeclared", undeclaredOOP);

// Add to Smalltalk dictionary
var smalltalkOOP = GlobalRegistry.Get("Smalltalk");
var undeclaredAssoc = CreateAssociation("Undeclared", undeclaredOOP);
AddToDictionary(smalltalkOOP, undeclaredAssoc);
```

### MUST IMPLEMENT: Populate During Compilation

During Phase 4 (Method Compilation), when encountering unknown variables:

```csharp
if (!IsKnownVariable(varName))
{
    // Create Association in Undeclared
    var assocOOP = GetOrCreateUndeclaredAssociation(varName);
    // Use this Association in method's literal frame
    methodLiterals.Add(assocOOP);
}
```

### SHOULD IMPLEMENT: Association Migration

The current AssociationRegistry approach is close but needs adjustment:

1. **Keep AssociationRegistry** for canonicalization
2. **Also maintain Undeclared dictionary in heap**
3. **When creating Association for unknown variable**:
   - Add to AssociationRegistry (as now)
   - Also add to Undeclared dictionary in heap
4. **When global gets defined** (init expressions):
   - SystemDictionary>>at:put: will handle migration automatically
   - Removes from Undeclared, keeps same Association

### Optional: Validation Support

Add method to report Undeclared contents:
```smalltalk
SystemDictionary>>reportUndeclared
    Undeclared isEmpty
        ifTrue: [Transcript show: 'No undeclared variables']
        ifFalse: [
            Transcript show: 'Undeclared variables: '.
            Undeclared keys do: [:key | Transcript show: key; space]].
```

## Implementation Priority

1. **CRITICAL**: Create Undeclared Dictionary in heap during cold start
2. **CRITICAL**: Register as global "Undeclared"
3. **CRITICAL**: Add to Smalltalk dictionary
4. **IMPORTANT**: Populate during method compilation (unknown variables)
5. **NICE**: Validation/reporting methods

## Files to Modify

1. **Phase 6 or new Phase 6b**: Create and register Undeclared
2. **Phase 4 (Method Compilation)**: Add unknown variables to Undeclared
3. **Phase 5 (Globals Installation)**: Ensure Undeclared included in Smalltalk
4. **AssociationRegistry**: Continue using for canonicalization
5. **Init Expression Execution**: SystemDictionary>>at:put: will handle migration

## Conclusion

Our current implementation is **functionally incomplete** because Smalltalk code expects to find and interact with an Undeclared dictionary object. While our AssociationRegistry provides canonicalization, we MUST create an actual Undeclared Dictionary in the heap for compatibility with the Smalltalk-80 source code.

The good news: Our AssociationRegistry can coexist with Undeclared. We just need to ensure Associations for unknown variables are added to both the registry (for canonicalization) and the Undeclared dictionary (for Smalltalk code to query/migrate).