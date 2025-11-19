# Pool Dictionary Bootstrap Requirements

**Last Updated**: 2025-11-19

## Problem Statement

The current cold start implementation has a gap in pool dictionary handling:

1. **Offline Compiler**: Sees references to `DefaultTextStyle` but can't find `TextConstants` pool dictionary, so it puts `DefaultTextStyle` in the `Undeclared` dictionary.

2. **Cold Start Phase 5**: Currently just **logs** that pool dictionaries will be created by init expressions, but doesn't actually create them.

3. **Result**: DefaultTextStyle Association is in Undeclared, not in TextConstants, making it inaccessible to Smalltalk code that declares `poolDictionaries: 'TextConstants'`.

## Required Changes

### Phase 5 Must Perform These Steps

1. **Create Pool Dictionaries**
   - Allocate Dictionary objects for each pool (TextConstants, CharacterConstants, etc.)
   - Register them in GlobalRegistry for lookup

2. **Install in SystemDictionary**
   - Create Association for each pool dictionary
   - Install in SystemDictionary so they're globally accessible

3. **Migrate Associations from Undeclared**
   - For each pool dictionary, find its keys that are currently in Undeclared
   - Move those Associations from Undeclared to the pool dictionary
   - This handles DefaultTextStyle, DefaultTab, etc.

4. **Set Values**
   - After migration, set the value slot of the Association
   - For DefaultTextStyle: Association.value := TextStyle object

5. **Install in Class sharedPools**
   - For each class declaring the pool, add it to sharedPools array

### Migration Flow

```
Before Migration:
  Undeclared = { #DefaultTextStyle -> nil, #DefaultTab -> nil, ... }
  TextConstants = <does not exist>

After Migration:
  Undeclared = { }
  TextConstants = { #DefaultTextStyle -> aTextStyle, #DefaultTab -> 24, ... }
  SystemDictionary at: #TextConstants -> TextConstants dictionary
```

## Implementation Details

### Pool Dictionary Discovery

The bundle contains `PoolLocalIds` for each class:
```json
"PoolLocalIds": ["pool:TextConstants"]
```

### Known Pool Dictionaries

| Pool Name | Contains | Used By |
|-----------|----------|---------|
| TextConstants | DefaultTextStyle, DefaultTab, DefaultRule, ... | 10+ classes |
| CharacterConstants | Cr, Lf, Tab, ... | Character class |

### Variable Migration

Variables that should migrate from Undeclared to TextConstants:
- `DefaultTextStyle` → TextStyle object
- `DefaultTab` → 24 (SmallInteger)
- `DefaultRule` → line rule
- `DefaultMask` → nil
- `Underlined`, `CtrlA`, `CtrlB`, etc.

### Bootstrap Order

1. Phase 3: Create Undeclared dictionary, pre-create Associations
2. Phase 4: Compile methods (pool variables as binding arrays)
3. Phase 5:
   - Create pool dictionaries
   - Migrate Associations from Undeclared
   - Set values (DefaultTextStyle, etc.)
   - Install in class sharedPools

## Code Location

The implementation should go in:
- `SystemDictionaryInstaller.InitializeAllPoolDictionaries()` - currently just logs
- New method: `MigrateUndeclaredToPoolDictionaries()`
- Existing: `RuntimeObjectCreator.CreateDefaultTextStyle()` - sets value

## Validation

After bootstrap:
1. `TextConstants at: #DefaultTextStyle` should return the TextStyle object
2. `Undeclared` should not contain #DefaultTextStyle
3. Classes declaring `poolDictionaries: 'TextConstants'` should resolve DefaultTextStyle

## Impact on Font Support

This is **critical** for font support because:
- DefaultTextStyle must be in TextConstants for Smalltalk code to access it
- Without this, text rendering code will fail to find DefaultTextStyle
- The FontPrimitives work correctly, but Smalltalk code won't be able to call them

## References

- Current behavior: `SystemDictionaryInstaller.cs:112-150`
- Undeclared creation: `Phase3_ObjectAllocation.cs:190-194`
- Association migration: `DictionaryInstaller.cs:55`
