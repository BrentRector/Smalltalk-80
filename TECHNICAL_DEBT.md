# Technical Debt - Critical Items to Fix

## High Priority

### Object#confirm: Method - BOOTSTRAP WORKAROUND
**Status**: TEMPORARY IMPLEMENTATION  
**Priority**: HIGH  
**Date Added**: August 5, 2025  

**Issue**: The `Object#confirm:` method currently has a bootstrap workaround that always returns `true`. This bypasses the proper Smalltalk-80 user interaction.

**Root Cause**: The proper implementation requires:
- `BinaryChoice` class ✅ (added)
- `BinaryChoiceView` class ❌ (missing)
- `Sensor` class ❌ (missing)
- UI framework support ❌ (missing)

**Current Workaround**: 
```smalltalk
confirm: aString 
	"TEMPORARY BOOTSTRAP IMPLEMENTATION"
	^true
```

**Proper Implementation Should Be**:
```smalltalk
confirm: aString 
	| answer |
	answer _ false.
	BinaryChoice
		message: aString
		displayAt: Sensor cursorPoint
		centered: true
		ifTrue: [answer _ true]
		ifFalse: [answer _ false].
	^answer
```

**To Fix**:
1. Add `BinaryChoiceView` class from system image (lines 3560-3650)
2. Add `Sensor` class from system image 
3. Implement basic UI framework support
4. Restore original `Object#confirm:` implementation
5. Test class initialization works properly

**Impact**: 
- Class initialization during bootstrap works ✅
- User interaction for confirmations is bypassed ❌
- All confirmation dialogs automatically approve ❌

---

## Completed Items

### ✅ Class Variable Field Allocation 
**Completed**: August 5, 2025  
Fixed "Index outside bounds of array" errors by implementing proper field allocation for class variables in metaclass instances.

### ✅ Metaclass System Implementation
**Completed**: August 5, 2025  
Implemented proper Smalltalk-80 metaclass relationships with correct inheritance chains.

### ✅ Class Hierarchy from System Image
**Completed**: August 5, 2025  
Successfully extracted and added Object → Behavior → ClassDescription → Class hierarchy from the system image.
