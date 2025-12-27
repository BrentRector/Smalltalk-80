using System.Collections.Generic;

namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Environment for bytecode generation tracking variable indices and scope.
/// Uses immutable scope chain - each scope has its OWN variables, no inheritance/mutation.
/// </summary>
public class CompilationEnvironment
{
    /// <summary>
    /// Type of this scope (Method or Block).
    /// </summary>
    public ScopeType Type { get; set; }


    /// <summary>
    /// Parent environment for lexical scope chain (null for method scope).
    /// Used to resolve free variables in nested blocks.
    /// </summary>
    public CompilationEnvironment? Parent { get; set; }

    /// <summary>
    /// Current scope depth (0 for method, 1+ for nested blocks).
    /// </summary>
    public int ScopeDepth { get; set; }

    /// <summary>
    /// Maps argument names to their 0-based indices within THIS scope only (no inheritance).
    /// For method scope: method arguments. For block scope: block arguments only.
    ///
    /// IMPORTANT: These indices have different meanings depending on scope:
    /// - Method arguments (scope depth = 0): Used to look up absolute slot index from TempFrameAllocator
    /// - Block arguments (scope depth > 0): Emitted directly as block-relative indices (0, 1, 2...)
    ///   because block arguments are stored in BlockContext indexed slots, not unified temp frame
    /// </summary>
    public Dictionary<string, int> LocalArgumentIndices { get; set; } = [];

    /// <summary>
    /// Maps temporary variable names to their 0-based indices within THIS scope only (no inheritance).
    /// These indices are used to look up absolute slot indices from TempFrameAllocator.
    /// </summary>
    public Dictionary<string, int> LocalTemporaryIndices { get; set; } = [];

    /// <summary>
    /// [DEPRECATED - Use LocalArgumentIndices] Map of argument names to their indices.
    /// This property is maintained for backward compatibility but should not be used for new code.
    /// </summary>
    public Dictionary<string, int> Arguments { get; } = [];

    /// <summary>
    /// [DEPRECATED - Use LocalTemporaryIndices] Map of temporary variable names to their indices.
    /// This property is maintained for backward compatibility but should not be used for new code.
    /// </summary>
    public Dictionary<string, int> Temporaries { get; } = [];

    /// <summary>
    /// Map of instance variable names to their indices.
    /// </summary>
    public Dictionary<string, int> InstanceVariables { get; } = [];

    /// <summary>
    /// Hidden temporaries for cascade receivers and scratch values.
    /// </summary>
    public Dictionary<string, int> HiddenTemporaries { get; } = [];

    /// <summary>
    /// Class variables (shared among all instances of a class).
    /// These are stored as Associations in the literal frame.
    /// </summary>
    public Dictionary<string, object> ClassVariables { get; } = [];

    /// <summary>
    /// Pool variables (shared dictionaries imported by classes).
    /// These are stored as Associations in the literal frame.
    /// </summary>
    public Dictionary<string, object> PoolVariables { get; } = [];

    /// <summary>
    /// Set of all known class names in the system (for distinguishing class names from pool variables).
    /// Populated during compilation to allow proper variable resolution.
    /// Class names should be treated as globals, not pool variables.
    /// </summary>
    public HashSet<string>? KnownClassNames { get; set; }

    /// <summary>
    /// Set of all pool dictionary names declared across all classes (e.g., 'FilePool', 'TextConstants').
    /// Pool dictionary names are global variables that contain Dictionary instances.
    /// They should be treated as globals during assignment, not as pool variables.
    /// </summary>
    public HashSet<string>? KnownPoolDictionaryNames { get; set; }

    /// <summary>
    /// Total number of arguments.
    /// </summary>
    public int ArgumentCount { get; set; }

    /// <summary>
    /// Total number of temporary slots allocated in the frame (including both named and hidden).
    /// This represents the actual size of the temp frame, not just the count of named temps.
    /// Used for allocating the next available temp slot index.
    /// </summary>
    public int TemporariesAllocatedInFrame { get; set; }

    /// <summary>
    /// Method's original argument count (before entering any blocks).
    /// Used to distinguish method-level variables from block-level variables.
    /// </summary>
    public int MethodArgumentCount { get; set; }

    /// <summary>
    /// Method's original temporary count (before entering any blocks).
    /// Used to distinguish method-level variables from block-level variables.
    /// </summary>
    public int MethodTemporaryCount { get; set; }

    /// <summary>
    /// Cumulative block argument offset for nested blocks.
    /// Tracks the total number of block arguments from ALL enclosing blocks.
    /// Used to correctly calculate temp variable offsets in nested block scenarios.
    /// Example: For [:x | [:y | methodTemp]], when compiling 'methodTemp' in inner block:
    ///   - This will be 2 (1 arg from outer block + 1 arg from inner block)
    ///   - Ensures PUSH_TEMP skips both x and y to reach methodTemp
    /// </summary>
    public int CumulativeBlockArgOffset { get; set; } = 0;

    /// <summary>
    /// Allocates a hidden temporary variable.
    /// </summary>
    public int AllocateHiddenTemp(string name)
    {
        if (!HiddenTemporaries.ContainsKey(name))
        {
            var index = ArgumentCount + TemporariesAllocatedInFrame;
            HiddenTemporaries[name] = index;
            TemporariesAllocatedInFrame++;
        }
        return HiddenTemporaries[name];
    }

    /// <summary>
    /// Checks if a variable name is an argument (not assignable).
    /// </summary>
    public bool IsArgument(string name)
    {
        return Arguments.ContainsKey(name);
    }

    /// <summary>
    /// Gets the absolute temporary index (including argument count).
    /// Returns -1 if not found.
    /// </summary>
    public int GetAbsoluteTemporaryIndex(string name)
    {
        return Temporaries.TryGetValue(name, out var index) ? index : -1;
    }

    /// <summary>
    /// Gets the relative temporary index (excluding argument count).
    /// Returns -1 if not found.
    /// </summary>
    public int GetRelativeTemporaryIndex(string name)
    {
        if (Temporaries.TryGetValue(name, out var absoluteIndex))
        {
            return absoluteIndex - ArgumentCount;  // Convert to relative
        }
        return -1;
    }
}
