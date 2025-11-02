# Dictionary Growth Fix - Integration with Smalltalk's Mechanism

## Problem
The `DictionaryAtomicInsertIfAbsent` method in `VirtualMachine.cs` was throwing an exception when the dictionary was full, preventing Smalltalk's normal dictionary growth mechanism from working. This caused failures when classes like `LargeNegativeInteger` tried to initialize class variables in dictionaries that were at capacity.

## Root Cause
The VM was trying to enforce policy (throwing exception on full dictionary) instead of being a primitive operation that returns a failure signal and lets Smalltalk handle the policy.

## Solution
Modified the VM to properly integrate with Smalltalk's dictionary growth mechanism by:

1. **Changed `DictionaryAtomicInsertIfAbsent` return contract**:
   - Previously: Threw `InvalidOperationException` when dictionary was full
   - Now: Returns `nil` to signal dictionary is full
   - Smalltalk code can then handle growth via `fullCheck` → `grow` → `become:`

2. **Updated `AddAssociationToClassPoolIfNeeded` to handle nil return**:
   - Checks if `DictionaryAtomicInsertIfAbsent` returned `nil`
   - If so, returns without error, allowing Smalltalk's `at:put:` method to run
   - The Smalltalk method will handle dictionary growth transparently

## Smalltalk's Dictionary Growth Mechanism
When a dictionary becomes full, Smalltalk handles it elegantly:

1. **`at:put:`** - Entry point for adding key-value pairs
2. **`atNewIndex:put:`** - Increments tally and adds new association
3. **`fullCheck`** - Checks if `basicSize - size <= (basicSize // 4)`
4. **`grow`** - Creates new larger dictionary, copies elements
5. **`become:`** - Swaps identity of old and new dictionaries

## Files Modified
- **`VirtualMachine.cs`** (lines 4359-4425):
  - Updated `DictionaryAtomicInsertIfAbsent` documentation
  - Changed to return `nil` instead of throwing exception
  - Added detailed comments explaining the Smalltalk growth mechanism

- **`VirtualMachine.cs`** (lines 4196-4307):
  - Updated `AddAssociationToClassPoolIfNeeded` documentation
  - Added check for `nil` return from `DictionaryAtomicInsertIfAbsent`
  - Returns gracefully to let Smalltalk handle growth

## Testing
- Build successful with `dotnet build -c Debug`
- `LargeNegativeInteger initialize` now executes successfully
- The VM properly delegates dictionary growth to Smalltalk code

## Key Principle
The VM provides **primitive operations**, not **policy**. Policy decisions like when and how to grow a dictionary belong in the Smalltalk code, not in the VM. The VM should only signal success/failure and let Smalltalk handle the rest.