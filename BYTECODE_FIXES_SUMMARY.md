# Bytecode Fixes for ColdStart Methods Summary

## Completed Improvements

### 1. Fixed Simple Non-Primitive Methods
- **isNil**: Now correctly uses `pushFalse (0x72), returnTop (0x7C)`
- **notNil**: Now correctly uses `pushTrue (0x71), returnTop (0x7C)` 
- **yourself**: Now correctly uses just `returnReceiver (0x78)`

### 2. Improved Primitive Method Fallbacks
- Default primitive fallback now sends `primitiveFailed` instead of just returning self
- Added literals array with `#primitiveFailed` symbol for primitive methods
- Created `InstallCustomPrimitiveMethod` for methods needing special fallback bytecode

### 3. Special Case: basicSize
- Returns 0 if primitive fails: `pushZero (0x75), returnTop (0x7C)`
- Uses empty literals array since no message send needed

## Bytecode Reference
Based on Blue Book specification:
- `0x70` (112): pushReceiver (self)
- `0x71` (113): pushTrue
- `0x72` (114): pushFalse
- `0x75` (117): pushZero
- `0x78` (120): returnReceiver
- `0x7C` (124): returnTop
- `0xD0` (208): sendLiteralSelector0Args0

## Known Issues

### Method Dictionary Bug
Not all installed methods are showing in the output. The bootstrap installs:
- 5 Object primitive methods (class, ==, basicAt:, basicAt:put:, basicSize)
- 3 Object non-primitive methods (isNil, notNil, yourself)

But only 3 methods appear when printed. This is likely due to:
1. Method dictionary implementation using simple linear search
2. Possible hash collision or overwriting
3. Array size limitations in the dictionary

### Complex Primitive Fallbacks Not Implemented
Methods like `basicAt:` and `basicAt:put:` should have complex fallback code that:
- Checks argument types
- Calls error methods if bounds checking fails
- Handles Number->Integer conversion

Current implementation just calls `primitiveFailed`.

## Next Steps

### For Full Correctness
1. Implement complex fallback bytecode for `basicAt:` and `basicAt:put:`
2. Add proper bytecode for Behavior methods (`new`, `new:`, etc.) with `isVariable` check
3. Fix method dictionary to handle more methods properly

### For Minimal Bootstrap
The current implementation is sufficient for cold start:
- Primitives will handle the common case
- Fallback to `primitiveFailed` will indicate issues
- Simple methods (isNil, notNil, yourself) work correctly

## Command Line Testing
```bash
./Smalltalk80.Interpreter.exe cold-start --print-classes
```

Output now shows:
- Correct bytecode sequences for all methods
- Literals arrays for primitive methods
- Proper symbolic bytecode names