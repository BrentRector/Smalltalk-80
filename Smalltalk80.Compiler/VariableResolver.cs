using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Compiler.Analysis;

namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Unified variable resolution for Smalltalk-80 compiler.
    /// Implements the Variable-Indexing-Architecture.md specification (Section 8.1).
    ///
    /// This class provides a single source of truth for variable resolution,
    /// eliminating code duplication between push/store operations and ensuring
    /// consistency across all variable access patterns.
    /// </summary>
    public class VariableResolver
    {
        /// <summary>
        /// Categories of variables in Smalltalk-80.
        /// </summary>
        public enum VariableType
        {
            BlockTemporary,     // Block temps (index: absolute in unified frame)
            ParentVariable,     // Parent context variable (index: absolute in unified frame)
            MethodArgument,     // Method args (index: absolute in unified frame)
            MethodTemporary,    // Method temps (index: absolute in unified frame)
            InstanceVariable,   // Instance variable (index: class layout)
            ClassVariable,      // Class variable (literal index)
            PoolVariable,       // Pool dictionary variable (literal index)
            Global,             // Global variable (literal index)
            PseudoVariable      // self, true, false, nil, super, thisContext
        }

        /// <summary>
        /// Result of variable resolution containing type, index, assignability, and pool dictionary information.
        /// </summary>
        public struct ResolutionResult
        {
            public VariableType Type { get; set; }
            public int Index { get; set; }              // Bytecode index (temp index, literal index, etc.)
            public bool IsAssignable { get; set; }      // Can this variable be assigned to?
            public string Name { get; set; }            // Original variable name
            public string? PoolDictionaryName { get; set; }  // Pool dictionary name for pool variables, null otherwise

            public ResolutionResult(VariableType type, int index, bool isAssignable, string name, string? poolDictionaryName = null)
            {
                Type = type;
                Index = index;
                IsAssignable = isAssignable;
                Name = name;
                PoolDictionaryName = poolDictionaryName;
            }
        }

        /// <summary>
        /// Resolves a variable name to its type and bytecode index.
        /// Follows the 9-step resolution priority from Variable-Indexing-Architecture.md Section 7.2.
        /// </summary>
        /// <param name="varName">Variable name to resolve</param>
        /// <param name="context">Current compilation environment</param>
        /// <param name="allocator">Optional temp frame allocator for unified temp allocation</param>
        /// <returns>Resolution result with type, index, and assignability</returns>
        /// <exception cref="InvalidOperationException">Thrown if variable not found in any scope</exception>
        public ResolutionResult ResolveVariable(string varName, CompilationEnvironment context, TempFrameAllocator? allocator = null)
        {
            // Console.WriteLine($"[RESOLVE] Resolving variable: {varName}, PoolVars.Count={context.PoolVariables.Count}");
            if (varName == "FilePool")
            {
                // Console.WriteLine($"[FILEPOOL START] Starting resolution for FilePool");
            }
            
            // Step 1: Check pseudo-variables first (highest priority, cannot be shadowed)
            var pseudoResult = TryResolvePseudoVariable(varName);
            if (pseudoResult.HasValue)
            {
                // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Returned at Step 1 (pseudo-var)");
                return pseudoResult.Value;
            }
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Passed Step 1");

            // Step 2-5: Try to resolve as temp variable using allocator (if provided)
            // This is the unified temp frame approach for method args/temps and block temps
            int slotIndex = -1;
            bool allocatorHasVar = allocator != null && allocator.TryGetSlotIndex(varName, out slotIndex);
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] After Step 2-5 check, allocatorHasVar={allocatorHasVar}");

            if (allocatorHasVar)
            {
                // Allocator has the variable - determine type based on scope and dictionaries
                // Check current scope first
                if (context.LocalArgumentIndices.TryGetValue(varName, out int argIndex))
                {
                    // Distinguish between method arguments and block arguments
                    if (context.ScopeDepth == 0)
                    {
                        // Method scope: This is a method argument (read-only pseudo-variable)
                        return new ResolutionResult(
                            VariableType.MethodArgument,
                            slotIndex,  // Absolute unified frame index
                            false,  // Method args are read-only
                            varName
                        );
                    }
                    else
                    {
                        // Block scope: This is a block argument (assignable per Blue Book)
                        return new ResolutionResult(
                            VariableType.BlockTemporary,
                            slotIndex,  // Absolute unified frame index
                            true,  // Block args ARE assignable
                            varName
                        );
                    }
                }

                if (context.LocalTemporaryIndices.ContainsKey(varName))
                {
                    return new ResolutionResult(
                        context.ScopeDepth > 0 ? VariableType.BlockTemporary : VariableType.MethodTemporary,
                        slotIndex,
                        true,  // All temps are assignable
                        varName
                    );
                }

                // Check parent scopes for the variable
                var parentContext = context.Parent;
                while (parentContext != null)
                {
                    if (parentContext.LocalArgumentIndices.ContainsKey(varName) || parentContext.LocalTemporaryIndices.ContainsKey(varName))
                    {
                        return new ResolutionResult(
                            VariableType.ParentVariable,
                            slotIndex,
                            true,  // Parent variables are assignable
                            varName
                        );
                    }
                    parentContext = parentContext.Parent;
                }

                // CRITICAL FIX: If we reach here, allocator has variable but environment doesn't
                // This indicates environment desynchronization during nested compilation
                // Trust the allocator and infer variable type from scope depth
                //
                // UNIFIED TEMP FRAME: All variables (including block args) are in unified frame
                // Infer type based on scope depth:
                // - Block scope (depth > 0): BlockTemporary (includes former block args)
                // - Method scope (depth == 0): MethodTemporary
                VariableType inferredType;
                if (context.ScopeDepth > 0)
                {
                    // Block scope: All variables are BlockTemporary (including former block args)
                    inferredType = VariableType.BlockTemporary;
                }
                else
                {
                    // Method scope: Must be method temp (args already checked above at line 95)
                    inferredType = VariableType.MethodTemporary;
                }

                return new ResolutionResult(
                    inferredType,
                    slotIndex,  // Use allocator's authoritative slot index
                    true,  // Assume assignable (safe default - will be refined below)
                    varName
                );
            }

            // FALLBACK: Use environment-based resolution (for backward compatibility)
            // This path is taken when allocator is null or variable not found in allocator

            // Step 2: Check block-local variables (if in block)
            if (context.ScopeDepth > 0)
            {
                var blockResult = TryResolveBlockLocal(varName, context);
                if (blockResult.HasValue)
                    return blockResult.Value;
            }

            // Step 3-4: Check parent scopes (hierarchical traversal for blocks)
            if (context.ScopeDepth > 0 && context.Parent != null)
            {
                var parentResult = TryResolveParentVariable(varName, context);
                if (parentResult.HasValue)
                    return parentResult.Value;
            }

            // Step 5: Check method arguments
            // IMPORTANT: Must traverse to method level to find method arguments
            // Use OwnArguments to get method's own arguments (no inheritance)
            var methodContext = context;
            while (methodContext.ScopeDepth > 0 && methodContext.Parent != null)
            {
                methodContext = methodContext.Parent;
            }

            if (methodContext.LocalArgumentIndices.TryGetValue(varName, out int methodArgIndex))
            {
                // Method arguments use DIRECT indices - NO hierarchical offset!
                // VM will traverse home chain automatically to reach the method context
                return new ResolutionResult(
                    VariableType.MethodArgument,
                    methodArgIndex,
                    false,  // Arguments are read-only
                    varName
                );
            }

            // Step 6: Check method temporaries
            // Use OwnTemporaries to get method's own temps (0-based, no inheritance)
            // (methodContext was computed in Step 5 above)
            if (methodContext.LocalTemporaryIndices.TryGetValue(varName, out int methodTempIndex))
            {
                // OwnTemporaries uses 0-based indexing within the method scope
                // Need to add method's argument count to get position in temp frame
                int baseIndex = methodContext.ArgumentCount + methodTempIndex;

                // Method temps use DIRECT indices - NO hierarchical offset!
                // VM will traverse home chain automatically to reach the method context
                return new ResolutionResult(
                    VariableType.MethodTemporary,
                    baseIndex,
                    true,  // Temporaries are assignable
                    varName
                );
            }

            // Step 7: Check instance variables
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Checking Step 7 (instance vars)");
            if (context.InstanceVariables.TryGetValue(varName, out int fieldIndex))
            {
                // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Returned at Step 7 (instance var)");
                return new ResolutionResult(
                    VariableType.InstanceVariable,
                    fieldIndex,
                    true,  // Instance variables are assignable
                    varName
                );
            }
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Passed Step 7");

            // Step 8: Check class variables
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Checking Step 8 (class vars)");
            if (context.ClassVariables.ContainsKey(varName))
            {
                // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Returned at Step 8 (class var)");
                // Class variables are resolved via literal associations
                // The actual literal index will be added by the caller
                return new ResolutionResult(
                    VariableType.ClassVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Class variables are assignable (via Association value:)
                    varName
                );
            }
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Passed Step 8");

            // Step 9: Check pool dictionary variables
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Checking Step 9 (pool vars), PoolVariables.Count={context.PoolVariables.Count}");
            if (context.PoolVariables.TryGetValue(varName, out object? poolDictName))
            {
                // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Returned at Step 9 (found in PoolVariables)");
                // Pool variables are resolved via literal associations
                // The actual literal index will be added by the caller
                // Extract the pool dictionary name from the stored value
                string? poolDictionaryName = poolDictName as string;
                
                return new ResolutionResult(
                    VariableType.PoolVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Pool variables are assignable (via Association value:)
                    varName,
                    poolDictionaryName
                );
            }
            // if (varName == "FilePool") Console.WriteLine($"[FILEPOOL] Passed Step 9");

            // Step 10: Check if this is a defined class name (before treating as pool variable)
            // Class names should be treated as globals, not pool variables
            if (context.KnownClassNames != null && context.KnownClassNames.Contains(varName))
            {
                // This is a known class name - treat as global variable
                // Console.WriteLine($"[CLASS NAME] Variable '{varName}' resolved as global (known class name)");
                
                return new ResolutionResult(
                    VariableType.Global,
                    -1,  // Literal index assigned by caller
                    false,  // Class names are read-only (cannot assign to them)
                    varName
                );
            }
            
            // Step 10b: Check if this is a pool dictionary name (before treating as pool variable)
            // Pool dictionary names (like 'FilePool') are global variables containing Dictionary instances
            // They should be treated as globals, not as pool variables
            if (varName == "FilePool")
            {
                // Console.WriteLine($"[FILEPOOL DEBUG] Resolving 'FilePool', KnownPoolDictionaryNames is {(context.KnownPoolDictionaryNames == null ? "NULL" : $"set with {context.KnownPoolDictionaryNames.Count} names")}");
            }
            if (context.KnownPoolDictionaryNames != null)
            {
                if (varName == "FilePool")
                {
                    // Console.WriteLine($"[FILEPOOL DEBUG] Checking 'FilePool' against {context.KnownPoolDictionaryNames.Count} pool dict names: [{string.Join(", ", context.KnownPoolDictionaryNames)}]");
                }
                if (context.KnownPoolDictionaryNames.Contains(varName))
                {
                    // This is a pool dictionary name - treat as global variable
                    // Console.WriteLine($"[POOL DICT NAME] Variable '{varName}' resolved as global (pool dictionary name)");
                    
                    return new ResolutionResult(
                        VariableType.Global,
                        -1,  // Literal index assigned by caller
                        true,  // Pool dictionary names are assignable (e.g., FilePool _ Dictionary new)
                        varName
                    );
                }
            }

            // Console.WriteLine($"[POOL VAR DEBUG] Variable '{varName}' falling through to Step 11");

            if (varName == "DefaultTab")
            {
                // Console.WriteLine($"[DEFAULT_TAB DEBUG] At Step 11, PoolVariables.ContainsKey('__HAS_POOLS__')={context.PoolVariables.ContainsKey("__HAS_POOLS__")}");
            }
            
            // Step 10c: Check for well-known system globals that should never be pool variables
            // These are created during cold start and should always be treated as globals
            HashSet<string> wellKnownGlobals =
            [
                "Smalltalk", "Processor", "Display", "Transcript",
                "Sensor", "ScheduledControllers", "Undeclared", "Disk"
            ];
            
            if (wellKnownGlobals.Contains(varName))
            {
                // Console.WriteLine($"[WELL-KNOWN GLOBAL] Variable '{varName}' is a well-known system global");
                return new ResolutionResult(
                    VariableType.Global,
                    -1,  // Literal index assigned by caller
                    true,  // System globals need to be assignable during initialization
                    varName
                );
            }
            
            // Step 11: Check if class has pool dictionaries and assume undefined variables are pool variables
            // For offline compiler: if the class declares pool dictionaries, undefined variables
            // should be treated as pool variable references (they'll be resolved at runtime)
            // IMPORTANT: The pool dictionary NAME itself (e.g., "TextConstants") should be treated as
            // a global variable, NOT as a pool variable!
            if (context.PoolVariables.TryGetValue("__HAS_POOLS__", out object? poolNamesObj))
            {
                // Extract pool dictionary names from the marker
                string poolNames = poolNamesObj as string ?? "";
                string[] pools = poolNames.Split(',', StringSplitOptions.RemoveEmptyEntries);
                
                // Check if this variable is actually a pool dictionary name itself
                if (pools.Contains(varName))
                {
                    // This is a pool dictionary name - treat as global, not pool variable
                    // Console.WriteLine($"[POOL DICT REFERENCE] Variable '{varName}' is a pool dictionary name, treating as global");
                    return new ResolutionResult(
                        VariableType.Global,
                        -1,  // Literal index assigned by caller
                        true,  // Pool dictionaries are assignable globals
                        varName
                    );
                }
                
                if (pools.Length > 0)
                {
                    // Assume the variable is from the first pool dictionary
                    // (In reality, it could be from any of them, but we don't know at compile time)
                    string assumedPoolDict = pools[0];
                    
                    // Console.WriteLine($"[POOL VAR ASSUMPTION] Variable '{varName}' assumed to be pool variable from '{assumedPoolDict}' (class has {pools.Length} pool(s): {poolNames})");
                    
                    return new ResolutionResult(
                        VariableType.PoolVariable,
                        -1,  // Literal index assigned by caller
                        true,  // Pool variables are assignable (via Association value:)
                        varName,
                        assumedPoolDict  // Pool dictionary name
                    );
                }
            }
            
            // Step 12: Fallback - assume global variable (will be resolved at runtime)
            // Global variables are stored as Associations in Smalltalk dictionary
            // Console.WriteLine($"[GLOBAL FALLBACK] Variable '{varName}' assumed to be global (no pool dictionaries)");
            return new ResolutionResult(
                VariableType.Global,
                -1,  // Literal index assigned by caller
                true,  // Globals are assignable (via Smalltalk at:put:)
                varName
            );
        }

        /// <summary>
        /// Try to resolve a pseudo-variable (self, true, false, nil, super, thisContext).
        /// Pseudo-variables cannot be shadowed and are always read-only.
        /// </summary>
        private ResolutionResult? TryResolvePseudoVariable(string varName)
        {
            return varName switch
            {
                "self" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                "super" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                "true" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                "false" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                "nil" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                "thisContext" => new ResolutionResult(VariableType.PseudoVariable, 0, false, varName),
                _ => null
            };
        }

        /// <summary>
        /// Try to resolve a block-local variable (block temporary).
        /// Only matches variables defined IN this block scope, not inherited from method.
        /// UNIFIED TEMP FRAME: Block arguments are now treated as block temporaries.
        /// </summary>
        private ResolutionResult? TryResolveBlockLocal(string varName, CompilationEnvironment context)
        {
            // UNIFIED TEMP FRAME: Block arguments and temps are all BlockTemporary now
            // Check this block's OWN arguments (now treated as temporaries)
            if (context.LocalArgumentIndices.TryGetValue(varName, out int argIndex))
            {
                return new ResolutionResult(
                    VariableType.BlockTemporary,
                    argIndex,
                    true,  // Block arguments ARE assignable (only METHOD arguments are pseudo-variables)
                    varName
                );
            }

            // Check this block's OWN temporaries (no inheritance!)
            if (context.LocalTemporaryIndices.TryGetValue(varName, out int tempIndex))
            {
                // OwnTemporaries uses 0-based indexing within scope
                // Add argument count to get position in temp frame
                int finalIndex = context.ArgumentCount + tempIndex;

                return new ResolutionResult(
                    VariableType.BlockTemporary,
                    finalIndex,
                    true,  // Block temporaries are assignable
                    varName
                );
            }

            return null;
        }

        /// <summary>
        /// Try to resolve a variable from parent scopes (hierarchical traversal).
        /// Walks up the parent chain accumulating offsets.
        /// UNIFIED TEMP FRAME: All variables use absolute indices from allocator.
        /// </summary>
        private ResolutionResult? TryResolveParentVariable(string varName, CompilationEnvironment context)
        {
            int accumulatedOffset = context.ArgumentCount + context.TemporariesAllocatedInFrame;
            var parentContext = context.Parent;
            int parentLevel = 1;

            while (parentContext != null)
            {
                // Check if parent is also a block (not method)
                if (parentContext.ScopeDepth > 0)
                {
                    // UNIFIED TEMP FRAME: Block arguments are now treated as temporaries
                    // Check parent block's OWN arguments (no inheritance, no collisions!)
                    if (parentContext.LocalArgumentIndices.TryGetValue(varName, out int pArgIndex))
                    {
                        // Found in parent block's own arguments
                        return new ResolutionResult(
                            VariableType.ParentVariable,
                            pArgIndex + accumulatedOffset,
                            true,  // Block arguments ARE assignable (only METHOD arguments are pseudo-variables)
                            varName
                        );
                    }

                    // Check parent block's OWN temporaries (no inheritance!)
                    if (parentContext.LocalTemporaryIndices.TryGetValue(varName, out int pTempIndex))
                    {
                        // OwnTemporaries uses 0-based indexing within the scope
                        // Need to add argument count to get the correct position in the temp frame
                        int pTempBaseIndex = parentContext.ArgumentCount + pTempIndex;

                        return new ResolutionResult(
                            VariableType.ParentVariable,
                            pTempBaseIndex + accumulatedOffset,
                            true,  // Temporaries are assignable
                            varName
                        );
                    }

                    // Not found in this parent - accumulate its locals and continue up the chain
                    accumulatedOffset += parentContext.ArgumentCount + parentContext.TemporariesAllocatedInFrame;
                    parentContext = parentContext.Parent;
                    parentLevel++;
                }
                else
                {
                    // Parent is method - stop hierarchical traversal
                    // Method variables will be checked in main resolution (steps 5-6)
                    break;
                }
            }

            return null;
        }

        /// <summary>
        /// Calculate the accumulated offset for hierarchical indexing.
        /// Walks from current block up to method, summing block-local counts.
        /// </summary>
        /// <param name="context">Current compilation environment</param>
        /// <returns>Total offset for hierarchical variable access</returns>
        private int CalculateBlockLocalOffset(CompilationEnvironment context)
        {
            int offset = 0;
            var current = context;

            // Walk from current block up to method, accumulating local counts
            while (current != null && current.ScopeDepth > 0 && current.Parent != null)
            {
                int blockLocals = current.ArgumentCount + current.TemporariesAllocatedInFrame;
                offset += blockLocals;
                current = current.Parent;
            }

            return offset;
        }
    }
}
