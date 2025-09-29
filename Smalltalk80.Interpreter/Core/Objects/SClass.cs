namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a Smalltalk class.
/// </summary>
public class SClass : SObject
{
    private readonly Dictionary<string, SCompiledMethod> methodDictionary = [];
    
    /// <summary>
    /// Initializes a new instance of the SClass class.
    /// </summary>
    /// <param name="name">The class name.</param>
    /// <param name="superclass">The superclass.</param>
    /// <param name="instanceVariableNames">The instance variable names.</param>
    public SClass(string name, SClass? superclass = null, string[]? instanceVariableNames = null)
    {
        Name = name;
        Superclass = superclass;
        InstanceVariableNames = instanceVariableNames ?? [];
        
        // Classes are instances of their metaclass
        // The metaclass will be set later via SetMetaclass
        metaclass = SystemDictionary.MetaclassClass ?? this;
    }
    
    /// <summary>
    /// Gets the name of this class.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the superclass of this class.
    /// </summary>
    public SClass? Superclass { get; }
    
    /// <summary>
    /// Gets the instance variable names.
    /// </summary>
    public string[] InstanceVariableNames { get; }
    
    private SClass metaclass;
    
    /// <summary>
    /// Gets the class of this object (metaclass for classes).
    /// </summary>
    public override SClass Class => metaclass;
    
    /// <summary>
    /// Sets the metaclass for this class.
    /// </summary>
    /// <param name="newMetaclass">The metaclass to set.</param>
    internal void SetMetaclass(SClass newMetaclass)
    {
        metaclass = newMetaclass ?? throw new ArgumentNullException(nameof(newMetaclass));
    }
    
    /// <summary>
    /// Gets whether this object is a class.
    /// </summary>
    public override bool IsClass => true;
    
    /// <summary>
    /// Adds a method to this class.
    /// </summary>
    public void AddMethod(string selector, SCompiledMethod method)
    {
        methodDictionary[selector] = method;
    }
    
    /// <summary>
    /// Looks up a method by selector.
    /// </summary>
    public SCompiledMethod? LookupMethod(string selector)
    {
        // Try to get from cache first if VM is initialized
        if (Program.RuntimeOptions != null)
        {
            // For now, bypass cache and do direct lookup
            // The cache would be accessed through the VM instance
        }
        
        // Check this class first
        if (methodDictionary.TryGetValue(selector, out var method))
        {
            return method;
        }
        
        // Check superclass chain
        return Superclass?.LookupMethod(selector);
    }
    
    /// <summary>
    /// Gets the number of methods in this class (not including inherited methods).
    /// </summary>
    public int GetMethodCount() => methodDictionary.Count;
    
    /// <summary>
    /// Gets the method selectors defined in this class.
    /// </summary>
    public List<string> GetMethods() => methodDictionary.Keys.ToList();
    
    /// <summary>
    /// Gets the method dictionary.
    /// </summary>
    public Dictionary<string, SCompiledMethod> MethodDictionary => methodDictionary;
    
    /// <summary>
    /// Creates a new instance of this class.
    /// </summary>
    public virtual SObject CreateInstance()
    {
        // Handle special classes that need specific types
        SObject instance = Name switch
        {
            "CompiledMethod" => new SCompiledMethod(this),
            "MethodContext" => CreateMethodContextInstance(),
            _ => new SInstance(this, TotalInstanceVariableCount)
        };
        
        // Note: GC registration should be handled by the VM/Bootstrap
        // when it creates objects, not here
        
        return instance;
    }
    
    /// <summary>
    /// Creates a MethodContext instance with proper size.
    /// </summary>
    private SInstance CreateMethodContextInstance()
    {
        // MethodContext needs space for fixed fields + temps + stack
        // This is a minimal instance - actual size set when initialized
        return new SInstance(this, 32); // Default size, will be resized as needed
    }
    
    /// <summary>
    /// Creates a new instance with indexed variables.
    /// </summary>
    /// <param name="size">The number of indexed variables.</param>
    public virtual SObject CreateInstance(int size)
    {
        var instance = new SIndexedInstance(this, TotalInstanceVariableCount, size);
        
        // Note: GC registration should be handled by the VM/Bootstrap
        // when it creates objects, not here
        
        return instance;
    }
    
    /// <summary>
    /// Creates a new instance of this class (deprecated - use CreateInstance).
    /// </summary>
    [Obsolete("Use CreateInstance() instead")]
    public virtual SObject NewInstance()
    {
        return CreateInstance();
    }
    
    /// <summary>
    /// Gets the total number of instance variables (including inherited).
    /// </summary>
    public int TotalInstanceVariableCount
    {
        get
        {
            var count = InstanceVariableNames.Length;
            if (Superclass != null)
            {
                count += Superclass.TotalInstanceVariableCount;
            }
            return count;
        }
    }
    
    /// <summary>
    /// Gets the index of an instance variable by name.
    /// </summary>
    /// <param name="name">The name of the instance variable.</param>
    /// <returns>The zero-based index of the variable, or -1 if not found.</returns>
    public int GetInstanceVariableIndex(string name)
    {
        // First check superclass variables
        var baseIndex = 0;
        if (Superclass != null)
        {
            var superIndex = Superclass.GetInstanceVariableIndex(name);
            if (superIndex >= 0)
            {
                return superIndex;
            }
            baseIndex = Superclass.TotalInstanceVariableCount;
        }
        
        // Then check this class's variables
        for (var i = 0; i < InstanceVariableNames.Length; i++)
        {
            if (InstanceVariableNames[i] == name)
            {
                return baseIndex + i;
            }
        }
        
        return -1;
    }
    
    /// <summary>
    /// Converts this class to a display string.
    /// </summary>
    public override string ToDisplayString() => Name;
    
    /// <summary>
    /// Gets the hash code for this class.
    /// </summary>
    public override int GetHashCode() => Name.GetHashCode();
    
    /// <summary>
    /// Determines whether this class equals another object.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is SClass other && Name == other.Name;
    }
}