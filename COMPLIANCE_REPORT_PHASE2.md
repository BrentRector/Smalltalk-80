# Phase 2 Migration Report: Method Installers to SObjectRef

## Summary

Phase 2 of the migration has been completed. All method installer files have been updated to use `SObjectRef` instead of `SClass`.

## Files Updated

### Method Installer Files (11 files)
1. ✅ KernelMethodsInstaller.cs
2. ✅ CompilationMethodsInstaller.cs
3. ✅ BehaviorMethodsInstaller.cs
4. ✅ BooleanMethodsInstaller.cs
5. ✅ CollectionMethodsInstaller.cs
6. ✅ ContextMethodsInstaller.cs
7. ✅ ObjectMethodsInstaller.cs
8. ✅ ProcessMethodsInstaller.cs
9. ✅ NumericMethodsInstaller.cs
10. ✅ StreamMethodsInstaller.cs

### Core Support File
11. ✅ SCompiledMethod.cs - Added `CreateCompiledMethodRef()` factory method

## Changes Made

### 1. New Factory Method
Added `SCompiledMethod.CreateCompiledMethodRef()` to create compiled methods as SObjectRef:
```csharp
public static SObjectRef CreateCompiledMethodRef(
    string selector,
    byte[] bytecodes,
    SObjectRef[] literals,
    int numArgs,
    int numTemps,
    int primitiveIndex = 0)
```

### 2. Helper Methods
Added `SCompiledMethod.ConvertLiteralsToRefs()` to convert old-style literals to SObjectRef:
```csharp
public static SObjectRef[] ConvertLiteralsToRefs(params SObject[] literals)
```

### 3. Pattern Updates
- Replaced `SystemDictionary.*Class` → `SystemDictionary.*ClassRef`
- Replaced `var *class` → `var *classRef`
- Replaced `== null` checks → `.IsNil` checks
- Replaced `SCompiledMethod.CreateCompiledMethod` → `SCompiledMethod.CreateCompiledMethodRef`
- Replaced `SArray.From(...)` → `SCompiledMethod.ConvertLiteralsToRefs(...)`
- Replaced `new SArray()` → `[]` (empty array)

## Current Build Issues

### Critical Issues
1. **Missing ToObject() method**: SCompiledMethod.cs line 124 tries to use `literalsRef.ToObject()` but this method doesn't exist
   - **Solution**: Need to rewrite to avoid ToObject() or implement proper conversion

2. **Missing using directives**: CompilationMethodsInstaller.cs missing proper using statements
   - **Solution**: Add `using Smalltalk80.Interpreter.Core.Values;` where needed

3. **SystemDictionary.LookupClassRef not found**: Line 67 in CompilationMethodsInstaller.cs
   - **Solution**: Need to implement or use correct method name

### Non-Critical Issues
1. GetMethodCount() not available for SObjectRef (commented out in CompilationMethodsInstaller.cs)
2. Nested ConvertLiteralsToRefs in ObjectMethodsInstaller.cs line 181 (fixed)

## Next Steps

### Phase 3: Fix Build Errors
1. ❌ Remove dependency on ToObject() method in SCompiledMethod
2. ❌ Implement proper SystemDictionary.LookupClassRef or use correct method
3. ❌ Add any missing extension methods to SObjectRefClassExtensions

### Phase 4: Update Remaining Components
1. ❌ Update BytecodeInterpreter to use SObjectRef
2. ❌ Update PrimitiveDispatcher to use SObjectRef
3. ❌ Update SystemDictionary remaining methods
4. ❌ Update VM and execution context

## Technical Debt

1. **Literal Arrays**: Currently creating literals array in object memory then converting back - inefficient
2. **Method Storage**: CompiledMethod still stored as SObject internally, wrapped in SObjectRef
3. **Class References**: Need to ensure all class references use SObjectRef consistently

## Success Criteria

✅ All method installer files updated to new pattern
✅ Factory method created for SObjectRef compiled methods
✅ Helper methods for literal conversion
⚠️ Build not yet successful - requires Phase 3 fixes

## Files Backed Up

All original files have been backed up with timestamp:
- `*.cs.backup_YYYYMMDD_HHMMSS`

## Recommendations

1. **Immediate**: Fix the ToObject() issue by rewriting CreateCompiledMethodRef to not need old SArray
2. **Short-term**: Implement missing SystemDictionary methods
3. **Long-term**: Fully migrate SCompiledMethod internals to use SObjectRef throughout