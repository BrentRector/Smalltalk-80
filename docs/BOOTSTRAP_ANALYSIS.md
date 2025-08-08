# Bootstrap Classes Analysis: Expected vs Actual

## Phase 1 Theoretical Minimum (bootstrap_phase1_theoretical_minimum.json)

### Expected from JSON:
**10 Base Classes:**
1. Object
2. Behavior  
3. Class
4. Metaclass
5. CompiledMethod
6. MethodDictionary
7. Symbol
8. String
9. Array
10. ByteArray

**10 Metaclasses:**
1. ObjectClass
2. BehaviorClass
3. ClassClass
4. MetaclassClass
5. CompiledMethodClass
6. MethodDictionaryClass
7. SymbolClass
8. StringClass
9. ArrayClass
10. ByteArrayClass

**Total Expected: 20 class-like objects**

### Actually Created in Phase 1:
Looking at the bootstrap output, Phase 1 creates:

**Classes registered:**
1. ProtoObject (OOP 0) - **UNEXPECTED**
2. Object (OOP 2)
3. Class (OOP 4)
4. Metaclass (OOP 6)
5. UndefinedObject (OOP 8) - **UNEXPECTED**
6. Behavior (OOP 10)
7. ClassDescription (OOP 12) - **UNEXPECTED**
8. Array (OOP 16)
9. Symbol (OOP 18)

**Metaclasses registered:**
1. ProtoObjectClass (OOP 20) - **UNEXPECTED**
2. ObjectClass (OOP 22)
3. ClassClass (OOP 24)
4. MetaclassClass (OOP 26)
5. UndefinedObjectClass (OOP 28) - **UNEXPECTED**
6. BehaviorClass (OOP 30)
7. ClassDescriptionClass (OOP 32) - **UNEXPECTED**
8. ArrayClass (OOP 34)
9. SymbolClass (OOP 36)

**Missing from Phase 1:**
- CompiledMethod / CompiledMethodClass
- MethodDictionary / MethodDictionaryClass
- String / StringClass
- ByteArray / ByteArrayClass

**Extra in Phase 1:**
- ProtoObject / ProtoObjectClass
- UndefinedObject / UndefinedObjectClass
- ClassDescription / ClassDescriptionClass

## Phase 2 Canonical (bootstrap_phase2_canonical.json)

### Expected from JSON:
The Phase 2 JSON expects to create via message sends:
1. UndefinedObject (but it's already in Phase 1!)
2. SmallInteger
3. ClassDescription (but it's already in Phase 1!)
4. Boolean, True, False
5. BlockContext
6. Message
7. CompiledMethod (should have been in Phase 1!)
8. MethodContext
9. Association
10. Set, Dictionary, IdentityDictionary, MethodDictionary (MD should have been in Phase 1!)
11. Collection, SequenceableCollection
12. OrderedCollection
13. Character
14. String (should have been in Phase 1!)
15. ByteArray (should have been in Phase 1!)

### Actually Created in Phase 2:
From the bootstrap output:
1. String / StringClass (OOP 76/78)
2. Message / MessageClass (OOP 84/86)
3. CompiledMethod / CompiledMethodClass (OOP 98/100)
4. MethodContext / MethodContextClass (OOP 114/116)
5. ByteArray / ByteArrayClass (OOP 134/136)
6. Set / SetClass (OOP 146/148)
7. Dictionary / DictionaryClass (OOP 158/160)
8. IdentityDictionary / IdentityDictionaryClass (OOP 164/166)
9. MethodDictionary / MethodDictionaryClass (OOP 176/178)

## Discrepancies Summary

### Major Issues:

1. **Phase 1 is NOT using the theoretical minimum JSON!**
   - It's using a different Phase 1 that includes ProtoObject, UndefinedObject, and ClassDescription
   - It's missing CompiledMethod, MethodDictionary, String, and ByteArray from theoretical minimum

2. **Phase 2 is trying to create classes that already exist**
   - UndefinedObject is created in both phases
   - ClassDescription is created in both phases

3. **Some critical classes are created in wrong phase:**
   - CompiledMethod, MethodDictionary, String, ByteArray should be in Phase 1 (per theoretical minimum)
   - But they're actually created in Phase 2

4. **Phase 2 JSON references classes that don't exist yet**
   - It tries to use SmallInteger before creating it
   - It assumes UndefinedObject doesn't exist but it does

### The Pretty Printer Shows:
- 72 class objects after Phase 2
- This includes both classes and metaclasses
- 36 classes × 2 (class + metaclass) = 72 objects

### Root Cause:
The system is NOT actually using `bootstrap_phase1_theoretical_minimum.json`! 
It's using the regular `bootstrap_phase1.json` which has a different set of classes.

Let's verify by checking what Phase 1 file is actually being loaded.