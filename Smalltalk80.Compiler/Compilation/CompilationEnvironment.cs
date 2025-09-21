using System.Collections.Immutable;

namespace Smalltalk80.Compiler.Compilation;

/// <summary>
/// Tracks variable scopes during compilation.
/// </summary>
public sealed class CompilationEnvironment
{
    private readonly Stack<VariableScope> _scopes = new();
    private string? _currentClass;
    private ImmutableArray<string> _instanceVariables = ImmutableArray<string>.Empty;
    private ImmutableArray<string> _classVariables = ImmutableArray<string>.Empty;

    /// <summary>
    /// Gets the current method selector.
    /// </summary>
    public string? CurrentSelector { get; private set; }

    /// <summary>
    /// Gets the number of arguments in the current method.
    /// </summary>
    public int ArgumentCount { get; private set; }

    /// <summary>
    /// Gets the total number of temporaries (including arguments).
    /// </summary>
    public int TemporaryCount => CurrentScope?.TotalTemporaries ?? 0;

    /// <summary>
    /// Gets the primitive number if specified.
    /// </summary>
    public int PrimitiveNumber { get; set; }

    private VariableScope? CurrentScope => _scopes.Count > 0 ? _scopes.Peek() : null;

    /// <summary>
    /// Clears all environment state.
    /// </summary>
    public void Clear()
    {
        _scopes.Clear();
        _currentClass = null;
        _instanceVariables = ImmutableArray<string>.Empty;
        _classVariables = ImmutableArray<string>.Empty;
        CurrentSelector = null;
        ArgumentCount = 0;
        PrimitiveNumber = 0;
    }

    /// <summary>
    /// Sets the current class context.
    /// </summary>
    public void SetClass(string className, ImmutableArray<string> instanceVars, ImmutableArray<string> classVars)
    {
        _currentClass = className;
        _instanceVariables = instanceVars;
        _classVariables = classVars;
    }

    /// <summary>
    /// Sets up a new method scope.
    /// </summary>
    public void SetMethod(string selector, int argCount)
    {
        CurrentSelector = selector;
        ArgumentCount = argCount;
        PrimitiveNumber = 0;
        
        // Create method scope
        _scopes.Clear();
        _scopes.Push(new VariableScope(ScopeType.Method));
    }

    /// <summary>
    /// Clears method-specific state.
    /// </summary>
    public void ClearMethod()
    {
        _scopes.Clear();
        CurrentSelector = null;
        ArgumentCount = 0;
        PrimitiveNumber = 0;
    }

    /// <summary>
    /// Enters a new block scope.
    /// </summary>
    public void EnterBlockScope()
    {
        _scopes.Push(new VariableScope(ScopeType.Block));
    }

    /// <summary>
    /// Exits the current block scope.
    /// </summary>
    public void ExitBlockScope()
    {
        if (_scopes.Count > 1 && CurrentScope?.Type == ScopeType.Block)
            _scopes.Pop();
    }

    /// <summary>
    /// Adds a method argument.
    /// </summary>
    public void AddArgument(string name, int index)
    {
        CurrentScope?.AddVariable(name, index, VariableKind.Argument);
    }

    /// <summary>
    /// Adds a temporary variable.
    /// </summary>
    public void AddTemporary(string name, int index)
    {
        CurrentScope?.AddVariable(name, index, VariableKind.Temporary);
    }

    /// <summary>
    /// Adds a block argument.
    /// </summary>
    public void AddBlockArgument(string name, int index)
    {
        CurrentScope?.AddVariable(name, index, VariableKind.BlockArgument);
    }

    /// <summary>
    /// Adds a block temporary.
    /// </summary>
    public void AddBlockTemporary(string name, int index)
    {
        CurrentScope?.AddVariable(name, index, VariableKind.BlockTemporary);
    }

    /// <summary>
    /// Tries to get a temporary or argument index.
    /// </summary>
    public bool TryGetTemporary(string name, out int index)
    {
        foreach (var scope in _scopes)
        {
            if (scope.TryGetVariable(name, out index, out var kind))
            {
                if (kind == VariableKind.Argument || kind == VariableKind.Temporary ||
                    kind == VariableKind.BlockArgument || kind == VariableKind.BlockTemporary)
                {
                    return true;
                }
            }
        }
        index = -1;
        return false;
    }

    /// <summary>
    /// Tries to get an instance variable index.
    /// </summary>
    public bool TryGetInstanceVariable(string name, out int index)
    {
        index = _instanceVariables.IndexOf(name);
        return index >= 0;
    }

    /// <summary>
    /// Tries to get a class variable.
    /// </summary>
    public bool TryGetClassVariable(string name, out int index)
    {
        index = _classVariables.IndexOf(name);
        return index >= 0;
    }

    private enum ScopeType
    {
        Method,
        Block
    }

    private enum VariableKind
    {
        Argument,
        Temporary,
        BlockArgument,
        BlockTemporary
    }

    private sealed class VariableScope
    {
        private readonly Dictionary<string, (int index, VariableKind kind)> _variables = new();
        
        public ScopeType Type { get; }
        public int TotalTemporaries { get; private set; }

        public VariableScope(ScopeType type)
        {
            Type = type;
        }

        public void AddVariable(string name, int index, VariableKind kind)
        {
            _variables[name] = (index, kind);
            if (index >= TotalTemporaries)
                TotalTemporaries = index + 1;
        }

        public bool TryGetVariable(string name, out int index, out VariableKind kind)
        {
            if (_variables.TryGetValue(name, out var info))
            {
                index = info.index;
                kind = info.kind;
                return true;
            }
            index = -1;
            kind = VariableKind.Temporary;
            return false;
        }
    }
}