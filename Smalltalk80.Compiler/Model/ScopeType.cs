namespace Smalltalk80.Compiler.Model;

/// <summary>
/// Defines the type of compilation scope.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// Method-level scope (outermost, depth 0).
    /// </summary>
    Method,

    /// <summary>
    /// Block-level scope (nested, depth 1+).
    /// </summary>
    Block
}
