namespace Smalltalk80.Compiler.Compilation;

using Smalltalk80.Compiler.Compilation.Models;

/// <summary>
/// Maintains the state and context during the compilation of a Smalltalk system.
/// Tracks classes, methods, variables, and compilation scope.
/// </summary>
public sealed class CompilationContext
{
    private readonly Dictionary<string, ClassDefinition> _classDefinitions = new(StringComparer.Ordinal);
    private readonly List<CompiledMethod> _compiledMethods = [];
    private readonly List<InitializationExpression> _initExpressions = [];
    private readonly Stack<CompilationScope> _scopeStack = new();
    
    /// <summary>
    /// Gets the currently active class being compiled.
    /// </summary>
    public ClassDefinition? CurrentClass { get; private set; }
    
    /// <summary>
    /// Gets whether we're currently compiling a class method (vs. instance method).
    /// </summary>
    public bool IsClassMethod { get; private set; }
    
    /// <summary>
    /// Gets the current method protocol/category.
    /// </summary>
    public string CurrentProtocol { get; private set; } = "unclassified";
    
    /// <summary>
    /// Gets all class definitions compiled so far.
    /// </summary>
    public IReadOnlyDictionary<string, ClassDefinition> Classes => _classDefinitions;
    
    /// <summary>
    /// Gets all methods compiled so far.
    /// </summary>
    public IReadOnlyList<CompiledMethod> Methods => _compiledMethods;
    
    /// <summary>
    /// Gets all initialization expressions compiled so far.
    /// </summary>
    public IReadOnlyList<InitializationExpression> InitializationExpressions => _initExpressions;
    
    /// <summary>
    /// Gets the current compilation scope for variable resolution.
    /// </summary>
    public CompilationScope? CurrentScope => _scopeStack.Count > 0 ? _scopeStack.Peek() : null;

    /// <summary>
    /// Adds or updates a class definition in the compilation context.
    /// </summary>
    /// <param name="classDefinition">The class definition to add.</param>
    public void AddOrUpdateClass(ClassDefinition classDefinition)
    {
        ArgumentNullException.ThrowIfNull(classDefinition);
        _classDefinitions[classDefinition.Name] = classDefinition;
    }
    
    /// <summary>
    /// Sets the current class context for method compilation.
    /// </summary>
    /// <param name="className">The name of the class.</param>
    /// <param name="isClassMethod">Whether we're compiling class methods.</param>
    /// <returns>The class definition if found, null otherwise.</returns>
    public ClassDefinition? SetClassContext(string className, bool isClassMethod)
    {
        CurrentClass = _classDefinitions.GetValueOrDefault(className);
        IsClassMethod = isClassMethod;
        return CurrentClass;
    }
    
    /// <summary>
    /// Sets the current protocol for method categorization.
    /// </summary>
    /// <param name="protocol">The protocol name.</param>
    public void SetProtocol(string protocol)
    {
        CurrentProtocol = protocol ?? "unclassified";
    }
    
    /// <summary>
    /// Adds a compiled method to the context.
    /// </summary>
    /// <param name="method">The compiled method to add.</param>
    public void AddMethod(CompiledMethod method)
    {
        ArgumentNullException.ThrowIfNull(method);
        _compiledMethods.Add(method);
        
        // Add method reference to the class if it exists
        if (CurrentClass is not null && _classDefinitions.TryGetValue(CurrentClass.Name, out var classDef))
        {
            // Create new class with updated method references (records are immutable)
            var updatedClass = classDef with 
            { 
                MethodReferences = [..classDef.MethodReferences, method.Selector]
            };
            _classDefinitions[CurrentClass.Name] = updatedClass;
        }
    }
    
    /// <summary>
    /// Adds an initialization expression to the context.
    /// </summary>
    /// <param name="expression">The initialization expression to add.</param>
    public void AddInitializationExpression(InitializationExpression expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        _initExpressions.Add(expression);
    }
    
    /// <summary>
    /// Pushes a new compilation scope onto the stack.
    /// </summary>
    /// <param name="scope">The scope to push.</param>
    public void PushScope(CompilationScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        _scopeStack.Push(scope);
    }
    
    /// <summary>
    /// Pops the current compilation scope from the stack.
    /// </summary>
    /// <returns>The popped scope, or null if the stack was empty.</returns>
    public CompilationScope? PopScope()
    {
        return _scopeStack.Count > 0 ? _scopeStack.Pop() : null;
    }
    
    /// <summary>
    /// Computes all inherited instance variables for a class.
    /// </summary>
    /// <param name="className">The class name to compute variables for.</param>
    /// <returns>A list of all instance variables including inherited ones.</returns>
    public List<string> ComputeInheritedInstanceVariables(string className)
    {
        List<string> allVariables = [];
        HashSet<string> visited = new(StringComparer.Ordinal);
        
        var currentClass = className;
        while (!string.IsNullOrEmpty(currentClass) && 
               currentClass != "nil" && 
               visited.Add(currentClass))
        {
            if (_classDefinitions.TryGetValue(currentClass, out var classDef))
            {
                // Insert at beginning to maintain inheritance order
                allVariables.InsertRange(0, classDef.InstanceVariables);
                currentClass = classDef.Superclass;
                
                // Stop at root classes
                if (currentClass is "Object" or "nil" or "")
                {
                    break;
                }
            }
            else
            {
                // Class not found in our definitions, stop traversal
                break;
            }
        }
        
        return allVariables;
    }
    
    /// <summary>
    /// Resets the compilation context to its initial state.
    /// </summary>
    public void Reset()
    {
        _classDefinitions.Clear();
        _compiledMethods.Clear();
        _initExpressions.Clear();
        _scopeStack.Clear();
        CurrentClass = null;
        IsClassMethod = false;
        CurrentProtocol = "unclassified";
    }
}

/// <summary>
/// Represents a compilation scope with variable bindings and context.
/// </summary>
public sealed record CompilationScope
{
    /// <summary>
    /// Gets the type of this scope.
    /// </summary>
    public required ScopeType Type { get; init; }
    
    /// <summary>
    /// Gets the method arguments in this scope.
    /// </summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
    
    /// <summary>
    /// Gets the temporary variables in this scope.
    /// </summary>
    public IReadOnlyList<string> Temporaries { get; init; } = [];
    
    /// <summary>
    /// Gets the instance variables accessible in this scope.
    /// </summary>
    public IReadOnlyList<string> InstanceVariables { get; init; } = [];
    
    /// <summary>
    /// Gets the class variables accessible in this scope.
    /// </summary>
    public IReadOnlyList<string> ClassVariables { get; init; } = [];
    
    /// <summary>
    /// Gets the literal constants in this scope.
    /// </summary>
    public List<object> Literals { get; init; } = [];
    
    /// <summary>
    /// Gets the parent scope, if any.
    /// </summary>
    public CompilationScope? ParentScope { get; init; }
    
    /// <summary>
    /// Finds the index of a variable in the appropriate collection.
    /// </summary>
    /// <param name="name">The variable name to find.</param>
    /// <returns>A tuple of (variable type, index) or null if not found.</returns>
    public (VariableType Type, int Index)? FindVariable(string name)
    {
        // Check arguments first
        var argIndex = Arguments.ToList().IndexOf(name);
        if (argIndex >= 0)
            return (VariableType.Argument, argIndex);
        
        // Then temporaries
        // IMPORTANT: In Smalltalk-80 bytecode, temporaries and arguments share the same index space.
        // Arguments occupy indices 0..n-1, temporaries start at index n.
        var tempIndex = Temporaries.ToList().IndexOf(name);
        if (tempIndex >= 0)
        {
            // Adjust index to account for arguments
            var adjustedIndex = Arguments.Count + tempIndex;
            return (VariableType.Temporary, adjustedIndex);
        }
        
        // Then instance variables (only for method scopes)
        if (Type == ScopeType.Method)
        {
            var instIndex = InstanceVariables.ToList().IndexOf(name);
            if (instIndex >= 0)
                return (VariableType.Instance, instIndex);
        }
        
        // Check class variables
        var classIndex = ClassVariables.ToList().IndexOf(name);
        if (classIndex >= 0)
            return (VariableType.Class, classIndex);
        
        // Check parent scope if exists
        return ParentScope?.FindVariable(name);
    }
    
    /// <summary>
    /// Adds a literal to this scope and returns its index.
    /// </summary>
    /// <param name="literal">The literal to add.</param>
    /// <returns>The index of the literal in the pool.</returns>
    public int AddLiteral(object literal)
    {
        ArgumentNullException.ThrowIfNull(literal);
        
        // Check if literal already exists using semantic equality
        for (int i = 0; i < Literals.Count; i++)
        {
            if (LiteralsEqual(Literals[i], literal))
                return i;
        }
        
        // Add new literal
        Literals.Add(literal);
        return Literals.Count - 1;
    }
    
    /// <summary>
    /// Compares two literals for semantic equality.
    /// </summary>
    /// <param name="a">First literal to compare.</param>
    /// <param name="b">Second literal to compare.</param>
    /// <returns>True if the literals are semantically equal, false otherwise.</returns>
    private static bool LiteralsEqual(object? a, object? b)
    {
        if (a == null || b == null) return a == b;
        
        // Handle Symbol types from both BytecodeEmitter and Models namespaces
        if (a.GetType().Name == "Symbol" && b.GetType().Name == "Symbol")
        {
            // Use reflection to compare by Name/Value property
            var aProp = a.GetType().GetProperty("Name") ?? a.GetType().GetProperty("Value");
            var bProp = b.GetType().GetProperty("Name") ?? b.GetType().GetProperty("Value");
            if (aProp != null && bProp != null)
            {
                var aValue = aProp.GetValue(a) as string;
                var bValue = bProp.GetValue(b) as string;
                return aValue == bValue;
            }
        }
        
        // Handle Association types
        if (a.GetType().Name == "Association" && b.GetType().Name == "Association")
        {
            // Compare by Key property
            var aKeyProp = a.GetType().GetProperty("Key");
            var bKeyProp = b.GetType().GetProperty("Key");
            if (aKeyProp != null && bKeyProp != null)
            {
                var aKey = aKeyProp.GetValue(a);
                var bKey = bKeyProp.GetValue(b);
                
                // Keys might be strings or Symbols
                if (aKey is string aKeyStr && bKey is string bKeyStr)
                {
                    return aKeyStr == bKeyStr;
                }
                else if (aKey?.GetType().Name == "Symbol" && bKey?.GetType().Name == "Symbol")
                {
                    var aValueProp = aKey.GetType().GetProperty("Value") ?? aKey.GetType().GetProperty("Name");
                    var bValueProp = bKey.GetType().GetProperty("Value") ?? bKey.GetType().GetProperty("Name");
                    if (aValueProp != null && bValueProp != null)
                    {
                        var aValue = aValueProp.GetValue(aKey) as string;
                        var bValue = bValueProp.GetValue(bKey) as string;
                        return aValue == bValue;
                    }
                }
            }
        }
        
        // For primitive types, use standard equality
        if (a is string strA && b is string strB) return strA == strB;
        if (a is int intA && b is int intB) return intA == intB;
        if (a is bool boolA && b is bool boolB) return boolA == boolB;
        
        // Default to standard equality
        return a.Equals(b);
    }
}

/// <summary>
/// Defines the type of compilation scope.
/// </summary>
public enum ScopeType
{
    /// <summary>
    /// A method-level scope.
    /// </summary>
    Method,
    
    /// <summary>
    /// A block closure scope.
    /// </summary>
    Block,
    
    /// <summary>
    /// A top-level expression scope.
    /// </summary>
    Expression
}

/// <summary>
/// Defines types of variables in Smalltalk.
/// </summary>
public enum VariableType
{
    /// <summary>
    /// Method or block argument.
    /// </summary>
    Argument,
    
    /// <summary>
    /// Temporary variable.
    /// </summary>
    Temporary,
    
    /// <summary>
    /// Instance variable.
    /// </summary>
    Instance,
    
    /// <summary>
    /// Class variable.
    /// </summary>
    Class,
    
    /// <summary>
    /// Global variable.
    /// </summary>
    Global,
    
    /// <summary>
    /// Special pseudo-variables (self, super, true, false, nil).
    /// </summary>
    Special
}