using System;
using System.Collections.Generic;

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
            BlockArgument,      // Block args (index: 0-based within block)
            BlockTemporary,     // Block temps (index: offset from block args)
            ParentVariable,     // Parent context variable (index: hierarchical offset)
            MethodArgument,     // Method args (index: 0-based)
            MethodTemporary,    // Method temps (index: offset from method args)
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
        /// <returns>Resolution result with type, index, and assignability</returns>
        /// <exception cref="InvalidOperationException">Thrown if variable not found in any scope</exception>
        public ResolutionResult ResolveVariable(string varName, CompilationEnvironment context)
        {
            // Step 1: Check pseudo-variables first (highest priority, cannot be shadowed)
            var pseudoResult = TryResolvePseudoVariable(varName);
            if (pseudoResult.HasValue)
                return pseudoResult.Value;

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
            if (context.Arguments.TryGetValue(varName, out int methodArgIndex))
            {
                int finalIndex = methodArgIndex;

                // If we're in a block, add hierarchical offset
                if (context.ScopeDepth > 0)
                {
                    int offset = CalculateBlockLocalOffset(context);
                    finalIndex += offset;
                }

                return new ResolutionResult(
                    VariableType.MethodArgument,
                    finalIndex,
                    false,  // Arguments are read-only
                    varName
                );
            }

            // Step 6: Check method temporaries
            if (context.Temporaries.TryGetValue(varName, out int methodTempAbsoluteIndex))
            {
                // Determine which ArgumentCount to use for calculations
                // If we're in a block (ScopeDepth > 0), use MethodArgumentCount for the calculation
                // because methodTempAbsoluteIndex was calculated using the method's arg count
                int argCountForCalculation = context.ScopeDepth > 0
                    ? context.MethodArgumentCount
                    : context.ArgumentCount;

                // Compiler stores temps with absolute indices (including arg count)
                // Convert to relative temp index for bytecode
                int methodTempRelativeIndex = methodTempAbsoluteIndex - argCountForCalculation;
                int baseIndex = argCountForCalculation + methodTempRelativeIndex;

                int finalIndex = baseIndex;

                // If we're in a block, add cumulative block argument count as offset
                // This is because the interpreter will skip over ALL enclosing block args before accessing home temps
                if (context.ScopeDepth > 0)
                {
                    // For nested blocks: offset = cumulative count from ALL enclosing blocks
                    // Example: [:x | [:y | methodTemp]] needs offset of 2 (x + y)
                    // The interpreter's GetTemporaryVariable will subtract nargs and recurse to home
                    int offset = context.CumulativeBlockArgOffset;
                    finalIndex += offset;
                }

                return new ResolutionResult(
                    VariableType.MethodTemporary,
                    finalIndex,
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
        /// Try to resolve a block-local variable (block argument or block temporary).
        /// Only matches variables defined IN this block scope, not inherited from method.
        /// </summary>
        private ResolutionResult? TryResolveBlockLocal(string varName, CompilationEnvironment context)
        {
            // Check block arguments (these are always block-local)
            if (context.Arguments.TryGetValue(varName, out int argIndex))
            {
                return new ResolutionResult(
                    VariableType.BlockArgument,
                    argIndex,
                    true,  // Block arguments ARE assignable in Smalltalk-80 (unlike method args)
                    varName
                );
            }

            // Check block temporaries
            // NOTE: context.Temporaries contains BOTH method temps AND block temps (inline block approach).
            // We need to distinguish: if the temp's absolute index is >= method's temp boundary,
            // it's a BLOCK temp. Otherwise, it's a METHOD temp and should be handled by Step 6.
            if (context.Temporaries.TryGetValue(varName, out int tempAbsoluteIndex))
            {
                // Calculate where method temps end: methodArgCount + methodTempCount
                int methodTempBoundary = context.MethodArgumentCount + context.MethodTemporaryCount;

                // If this temp's index is before the boundary, it's a METHOD temp, not a BLOCK temp
                if (tempAbsoluteIndex < methodTempBoundary)
                {
                    // Not a block-local variable - let it fall through to Step 6 (method temporaries)
                    return null;
                }

                // This is a genuine block temporary
                // Convert absolute index to relative (exclude arg count)
                int tempRelativeIndex = tempAbsoluteIndex - context.ArgumentCount;
                int finalIndex = context.ArgumentCount + tempRelativeIndex;

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
        /// </summary>
        private ResolutionResult? TryResolveParentVariable(string varName, CompilationEnvironment context)
        {
            int accumulatedOffset = context.ArgumentCount + context.TemporaryCount;
            var parentContext = context.Parent;

            while (parentContext != null)
            {
                // Check if parent is also a block (not method)
                if (parentContext.ScopeDepth > 0)
                {
                    // Check parent block arguments
                    if (parentContext.Arguments.TryGetValue(varName, out int pArgIndex))
                    {
                        return new ResolutionResult(
                            VariableType.ParentVariable,
                            pArgIndex + accumulatedOffset,
                            true,  // Block arguments ARE assignable in Smalltalk-80 (pass-by-value)
                            varName
                        );
                    }

                    // Check parent block temporaries
                    if (parentContext.Temporaries.TryGetValue(varName, out int pTempAbsoluteIndex))
                    {
                        // Convert to relative index
                        int pTempRelativeIndex = pTempAbsoluteIndex - parentContext.ArgumentCount;
                        int pTempBaseIndex = parentContext.ArgumentCount + pTempRelativeIndex;

                        return new ResolutionResult(
                            VariableType.ParentVariable,
                            pTempBaseIndex + accumulatedOffset,
                            true,  // Temporaries are assignable
                            varName
                        );
                    }

                    // Accumulate this parent's locals for next iteration
                    accumulatedOffset += parentContext.ArgumentCount + parentContext.TemporaryCount;
                    parentContext = parentContext.Parent;
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
            while (current.ScopeDepth > 0 && current.Parent != null)
            {
                offset += current.ArgumentCount + current.TemporaryCount;
                current = current.Parent;
            }

            return offset;
        }
    }
}
