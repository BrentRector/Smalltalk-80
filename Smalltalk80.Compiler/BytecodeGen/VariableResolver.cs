using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Compiler.Analysis;
using Smalltalk80.Compiler.Model;

namespace Smalltalk80.Compiler.BytecodeGen
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
        /// Well-known system globals that should never be treated as pool variables.
        /// These are created during cold start and should always be resolved as globals.
        /// </summary>
        private static readonly HashSet<string> WellKnownGlobals =
        [
            "Smalltalk", "Processor", "Display", "Transcript",
            "Sensor", "ScheduledControllers", "Undeclared", "Disk"
        ];
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
        /// Follows the 12-step resolution priority from Variable-Indexing-Architecture.md Section 7.2.
        /// </summary>
        /// <param name="varName">Variable name to resolve</param>
        /// <param name="context">Current compilation environment</param>
        /// <param name="allocator">Optional temp frame allocator for unified temp allocation</param>
        /// <returns>Resolution result with type, index, and assignability</returns>
        public ResolutionResult ResolveVariable(string varName, CompilationEnvironment context, TempFrameAllocator? allocator = null)
        {
            // Step 1: Pseudo-variables (highest priority, cannot be shadowed)
            var pseudoResult = TryResolvePseudoVariable(varName);
            if (pseudoResult.HasValue)
                return pseudoResult.Value;

            // Steps 2-5: Try unified temp frame resolution via allocator
            var allocatorResult = TryResolveFromAllocator(varName, context, allocator);
            if (allocatorResult.HasValue)
                return allocatorResult.Value;

            // Steps 2-6: Environment-based fallback (when allocator unavailable)
            var envResult = TryResolveFromEnvironment(varName, context);
            if (envResult.HasValue)
                return envResult.Value;

            // Steps 7-9: Instance, class, and pool variables
            var fieldResult = TryResolveFieldVariable(varName, context);
            if (fieldResult.HasValue)
                return fieldResult.Value;

            // Steps 10-11: Global resolution (class names, pool dictionaries, system globals)
            var globalResult = TryResolveGlobalVariable(varName, context);
            if (globalResult.HasValue)
                return globalResult.Value;

            // Step 12: Fallback - assume global variable (resolved at runtime)
            return new ResolutionResult(
                VariableType.Global,
                -1,  // Literal index assigned by caller
                true,  // Globals are assignable (via Smalltalk at:put:)
                varName
            );
        }

        /// <summary>
        /// Steps 2-5: Try to resolve variable using the unified temp frame allocator.
        /// Returns null if allocator is unavailable or variable not found.
        /// </summary>
        private ResolutionResult? TryResolveFromAllocator(string varName, CompilationEnvironment context, TempFrameAllocator? allocator)
        {
            if (allocator == null || !allocator.TryGetSlotIndex(varName, out int slotIndex))
                return null;

            // Check current scope arguments
            if (context.LocalArgumentIndices.TryGetValue(varName, out _))
            {
                return context.ScopeDepth == 0
                    ? new ResolutionResult(VariableType.MethodArgument, slotIndex, false, varName)
                    : new ResolutionResult(VariableType.BlockTemporary, slotIndex, true, varName);
            }

            // Check current scope temporaries
            if (context.LocalTemporaryIndices.ContainsKey(varName))
            {
                var type = context.ScopeDepth > 0 ? VariableType.BlockTemporary : VariableType.MethodTemporary;
                return new ResolutionResult(type, slotIndex, true, varName);
            }

            // Check parent scopes
            if (TryFindInParentScopes(varName, context))
                return new ResolutionResult(VariableType.ParentVariable, slotIndex, true, varName);

            // Allocator has variable but environment doesn't - infer type from scope depth
            var inferredType = context.ScopeDepth > 0 ? VariableType.BlockTemporary : VariableType.MethodTemporary;
            return new ResolutionResult(inferredType, slotIndex, true, varName);
        }

        /// <summary>
        /// Check if a variable exists in any parent scope.
        /// </summary>
        private static bool TryFindInParentScopes(string varName, CompilationEnvironment context)
        {
            var parentContext = context.Parent;
            while (parentContext != null)
            {
                if (parentContext.LocalArgumentIndices.ContainsKey(varName) ||
                    parentContext.LocalTemporaryIndices.ContainsKey(varName))
                    return true;
                parentContext = parentContext.Parent;
            }
            return false;
        }

        /// <summary>
        /// Steps 2-6: Environment-based resolution fallback.
        /// Used when allocator is unavailable or variable not found in allocator.
        /// </summary>
        private ResolutionResult? TryResolveFromEnvironment(string varName, CompilationEnvironment context)
        {
            // Step 2: Block-local variables
            if (context.ScopeDepth > 0)
            {
                var blockResult = TryResolveBlockLocal(varName, context);
                if (blockResult.HasValue)
                    return blockResult.Value;
            }

            // Steps 3-4: Parent scope variables
            if (context.ScopeDepth > 0 && context.Parent != null)
            {
                var parentResult = TryResolveParentVariable(varName, context);
                if (parentResult.HasValue)
                    return parentResult.Value;
            }

            // Steps 5-6: Method arguments and temporaries
            return TryResolveMethodVariable(varName, context);
        }

        /// <summary>
        /// Steps 5-6: Resolve method arguments and temporaries.
        /// Traverses to method level and checks local dictionaries.
        /// </summary>
        private ResolutionResult? TryResolveMethodVariable(string varName, CompilationEnvironment context)
        {
            // Navigate to method scope
            var methodContext = context;
            while (methodContext.ScopeDepth > 0 && methodContext.Parent != null)
                methodContext = methodContext.Parent;

            // Step 5: Method arguments
            if (methodContext.LocalArgumentIndices.TryGetValue(varName, out int methodArgIndex))
            {
                return new ResolutionResult(
                    VariableType.MethodArgument,
                    methodArgIndex,
                    false,  // Arguments are read-only
                    varName
                );
            }

            // Step 6: Method temporaries
            if (methodContext.LocalTemporaryIndices.TryGetValue(varName, out int methodTempIndex))
            {
                int baseIndex = methodContext.ArgumentCount + methodTempIndex;
                return new ResolutionResult(
                    VariableType.MethodTemporary,
                    baseIndex,
                    true,  // Temporaries are assignable
                    varName
                );
            }

            return null;
        }

        /// <summary>
        /// Steps 7-9: Resolve instance, class, and pool variables.
        /// </summary>
        private ResolutionResult? TryResolveFieldVariable(string varName, CompilationEnvironment context)
        {
            // Step 7: Instance variables
            if (context.InstanceVariables.TryGetValue(varName, out int fieldIndex))
            {
                return new ResolutionResult(
                    VariableType.InstanceVariable,
                    fieldIndex,
                    true,  // Instance variables are assignable
                    varName
                );
            }

            // Step 8: Class variables
            if (context.ClassVariables.ContainsKey(varName))
            {
                return new ResolutionResult(
                    VariableType.ClassVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Class variables are assignable (via Association value:)
                    varName
                );
            }

            // Step 9: Pool dictionary variables
            if (context.PoolVariables.TryGetValue(varName, out object? poolDictName))
            {
                return new ResolutionResult(
                    VariableType.PoolVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Pool variables are assignable (via Association value:)
                    varName,
                    poolDictName as string
                );
            }

            return null;
        }

        /// <summary>
        /// Steps 10-11: Resolve global variables (class names, pool dictionaries, system globals).
        /// </summary>
        private ResolutionResult? TryResolveGlobalVariable(string varName, CompilationEnvironment context)
        {
            // Step 10: Known class names
            if (context.KnownClassNames?.Contains(varName) == true)
            {
                return new ResolutionResult(
                    VariableType.Global,
                    -1,
                    false,  // Class names are read-only
                    varName
                );
            }

            // Step 10b: Known pool dictionary names
            if (context.KnownPoolDictionaryNames?.Contains(varName) == true)
            {
                return new ResolutionResult(
                    VariableType.Global,
                    -1,
                    true,  // Pool dictionary names are assignable
                    varName
                );
            }

            // Step 10c: Well-known system globals
            if (WellKnownGlobals.Contains(varName))
            {
                return new ResolutionResult(
                    VariableType.Global,
                    -1,
                    true,  // System globals need to be assignable during initialization
                    varName
                );
            }

            // Step 11: Infer pool variable from class pool dictionaries
            return TryResolveFromPoolDictionaries(varName, context);
        }

        /// <summary>
        /// Step 11: Check if variable should be treated as pool variable based on class pool dictionaries.
        /// </summary>
        private ResolutionResult? TryResolveFromPoolDictionaries(string varName, CompilationEnvironment context)
        {
            if (!context.PoolVariables.TryGetValue("__HAS_POOLS__", out object? poolNamesObj))
                return null;

            string poolNames = poolNamesObj as string ?? "";
            string[] pools = poolNames.Split(',', StringSplitOptions.RemoveEmptyEntries);

            // Check if variable is a pool dictionary name itself
            if (pools.Contains(varName))
            {
                return new ResolutionResult(
                    VariableType.Global,
                    -1,
                    true,  // Pool dictionaries are assignable globals
                    varName
                );
            }

            // Assume variable is from first pool dictionary
            if (pools.Length > 0)
            {
                return new ResolutionResult(
                    VariableType.PoolVariable,
                    -1,
                    true,  // Pool variables are assignable
                    varName,
                    pools[0]
                );
            }

            return null;
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
