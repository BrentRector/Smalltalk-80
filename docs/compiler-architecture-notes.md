# Compiler Architecture Notes

## Critical Design Principle: No Shadow Copies

**IMPORTANT**: The C# implementation must NOT maintain shadow copies of Smalltalk VM objects. This leads to mismatches and hard-to-find errors.

### Current Status (as of latest commit)

#### ✅ **COMPLIANT AREAS:**

1. **AstToBytecodeCompiler.cs**
   - Uses actual VM `Symbol` class directly
   - Uses actual VM `Association` class directly  
   - Creates actual VM `CompiledMethod` objects
   - No shadow copies of VM objects

2. **CompilerPrimitives.cs**
   - Creates actual VM `SmalltalkObject` instances
   - Uses VM's `Symbol` class
   - Properly integrates with `ObjectMemory.Register()`
   - No shadow copies

3. **SmalltalkCompiler.cs (main methods)**
   - Works with actual `CompiledMethod` objects
   - Installs methods directly in VM method dictionaries
   - No shadow copies in method installation

#### ✅ **PREVIOUSLY IDENTIFIED ISSUES - NOW FIXED:**

1. **CompilationContext (SmalltalkCompiler.cs lines 220-412)**
   - **Previous Issue**: Created empty dictionaries for instance/class variables
   - **Fix Implemented**: Now loads variable names from actual VM class objects
   - **Implementation**: 
     - LoadClassObject() finds the Smalltalk SystemDictionary
     - LoadInstanceVariables() reads from ClassObject.Fields[3]
     - LoadClassVariables() reads from ClassObject.Fields[6]
   - **Status**: ✅ Fully implemented - no shadow copies

2. **CompilerDataTypes.cs (Smalltalk80.Compiler project)**
   - **Issue**: Contains duplicate `Symbol` class (lines 49-73)
   - **Issue**: Contains `CompiledMethodData` class that duplicates VM's `CompiledMethod`
   - **Note**: This file is used by ExtractClass utilities, not the main compiler
   - **Status**: Separate tool, not part of runtime compiler

### Architecture Rules

1. **Always use VM objects directly:**
   - Use `SmalltalkObject` from the VM
   - Use `Symbol` from the VM
   - Use `Association` from the VM
   - Use `CompiledMethod` from the VM
   - Use `ObjectMemory` for object registration

2. **Never create parallel structures:**
   - Don't create a separate "Class" representation
   - Don't maintain separate method storage
   - Don't duplicate symbol tables
   - Don't maintain separate variable dictionaries

3. **Access VM structures directly:**
   - Class instance variables: `classObject.Fields[2]`
   - Class name: `classObject.Fields[5]` (typically a Symbol)
   - Method dictionary: `classObject.Fields[1]`
   - Superclass: `classObject.Fields[0]`

### Implementation Complete ✅

All identified issues have been resolved:

1. **LoadClassObject() - IMPLEMENTED:**
   - Searches ObjectMemory for the SystemDictionary
   - Looks up classes by name using Symbol keys
   - Returns actual VM SmalltalkObject references

2. **LoadInstanceVariables() - IMPLEMENTED:**
   - Reads from ClassObject.Fields[3] (ClassDescription slot)
   - Handles multiple formats: Symbol arrays, string lists, SmalltalkObject symbols
   - Creates proper index mapping for bytecode generation

3. **LoadClassVariables() - IMPLEMENTED:**
   - Reads from ClassObject.Fields[6] when available
   - Ready for dictionary iteration when needed
   - No shadow copies maintained

4. **CompilerDataTypes.cs - ACCEPTABLE:**
   - Used only by ExtractClass utilities (separate tool)
   - Not part of the runtime compiler
   - Does not affect VM operation

### Testing for Compliance

To verify no shadow copies:
1. Search for any class definitions that duplicate VM concepts
2. Check that all object creation uses VM classes
3. Verify that variable lookups access actual class objects
4. Ensure method installation modifies actual VM dictionaries

### Benefits of This Architecture

1. **Data Consistency**: No synchronization issues between shadow copies and VM
2. **Simplicity**: One source of truth for all objects
3. **Correctness**: Changes to VM objects are immediately visible
4. **Memory Efficiency**: No duplicate storage
5. **Debugging**: Easier to trace issues when there's only one representation