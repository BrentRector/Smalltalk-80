using System;
using System.Collections.Generic;
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
        /// Result of variable resolution containing type, index, and assignability.
        /// </summary>
        public struct ResolutionResult
        {
            public VariableType Type { get; set; }
            public int Index { get; set; }              // Bytecode index (temp index, literal index, etc.)
            public bool IsAssignable { get; set; }      // Can this variable be assigned to?
            public string Name { get; set; }            // Original variable name

            public ResolutionResult(VariableType type, int index, bool isAssignable, string name)
            {
                Type = type;
                Index = index;
                IsAssignable = isAssignable;
                Name = name;
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
            // Step 1: Check pseudo-variables first (highest priority, cannot be shadowed)
            var pseudoResult = TryResolvePseudoVariable(varName);
            if (pseudoResult.HasValue)
                return pseudoResult.Value;

            // Step 2-5: Try to resolve as temp variable using allocator (if provided)
            // This is the unified temp frame approach for method args/temps and block temps
            int slotIndex = -1;
            bool allocatorHasVar = allocator != null && allocator.TryGetSlotIndex(varName, out slotIndex);

            if (allocatorHasVar)
            {
                // Allocator has the variable - determine type based on scope and dictionaries
                // Check current scope first
                if (context.LocalArgumentIndices.TryGetValue(varName, out int argIndex))
                {
                    // Must be method argument (scope depth == 0)
                    // Block arguments are handled above before allocator check
                    return new ResolutionResult(
                        VariableType.MethodArgument,
                        slotIndex,  // Absolute unified frame index
                        false,  // Method args are read-only
                        varName
                    );
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
            if (context.InstanceVariables.TryGetValue(varName, out int fieldIndex))
            {
                return new ResolutionResult(
                    VariableType.InstanceVariable,
                    fieldIndex,
                    true,  // Instance variables are assignable
                    varName
                );
            }

            // Step 8: Check class variables
            if (context.ClassVariables.ContainsKey(varName))
            {
                // Class variables are resolved via literal associations
                // The actual literal index will be added by the caller
                return new ResolutionResult(
                    VariableType.ClassVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Class variables are assignable (via Association value:)
                    varName
                );
            }

            // Step 9: Check pool dictionary variables
            if (context.PoolVariables.ContainsKey(varName))
            {
                // Pool variables are resolved via literal associations
                // The actual literal index will be added by the caller
                return new ResolutionResult(
                    VariableType.PoolVariable,
                    -1,  // Literal index assigned by caller
                    true,  // Pool variables are assignable (via Association value:)
                    varName
                );
            }

            // Step 10: Assume global variable (will be resolved at runtime)
            // Global variables are stored as Associations in Smalltalk dictionary
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
                    false,  // Block arguments are NOT assignable (Blue Book: pseudo-variables)
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
                            false,  // Block arguments are NOT assignable (Blue Book: pseudo-variables)
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
