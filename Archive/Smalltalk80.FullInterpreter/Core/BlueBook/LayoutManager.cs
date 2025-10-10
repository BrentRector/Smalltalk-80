namespace Smalltalk80.Interpreter.Core.BlueBook;

using Smalltalk80.Interpreter.ColdStart;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Manages class layouts and field indices computed from JSON cold-start data.
/// Provides cached access to instance variable indices for all classes.
/// </summary>
public class LayoutManager
{
    private readonly ColdStartJsonLoader _loader;

    // Cache: ClassName -> (FieldName -> FieldIndex)
    private readonly Dictionary<string, Dictionary<string, int>> _layoutCache = new();

    // Cache: ClassName -> total instance variable count
    private readonly Dictionary<string, int> _instanceVarCountCache = new();

    // Cache: ClassName -> list of all field names in order
    private readonly Dictionary<string, string[]> _fieldNamesCache = new();

    /// <summary>
    /// Initializes a new instance of the LayoutManager class.
    /// </summary>
    /// <param name="loader">The cold start JSON loader containing class definitions.</param>
    public LayoutManager(ColdStartJsonLoader loader)
    {
        _loader = loader;
    }

    /// <summary>
    /// Gets the field index for a named instance variable in a class.
    /// Computes by traversing inheritance chain if not cached.
    /// </summary>
    /// <param name="className">Name of the class (e.g., "Class", "Process").</param>
    /// <param name="fieldName">Name of the instance variable (e.g., "superclass", "priority").</param>
    /// <returns>Zero-based field index.</returns>
    /// <exception cref="InvalidOperationException">Thrown when field or class is not found.</exception>
    public int GetFieldIndex(string className, string fieldName)
    {
        // Check cache
        if (_layoutCache.TryGetValue(className, out var fields))
        {
            if (fields.TryGetValue(fieldName, out var index))
                return index;

            throw new InvalidOperationException(
                $"Field '{fieldName}' not found in class '{className}'. " +
                $"Available fields: {string.Join(", ", fields.Keys)}");
        }

        // Compute layout for this class
        ComputeLayout(className);

        // Try again from cache
        return GetFieldIndex(className, fieldName);
    }

    /// <summary>
    /// Gets the total number of instance variables for a class (including inherited).
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <returns>Total instance variable count including inherited fields.</returns>
    public int GetInstanceVarCount(string className)
    {
        if (_instanceVarCountCache.TryGetValue(className, out var count))
            return count;

        ComputeLayout(className);
        return _instanceVarCountCache[className];
    }

    /// <summary>
    /// Gets all field names for a class in order (inherited first, then own).
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <returns>Array of field names in inheritance order.</returns>
    public string[] GetAllFieldNames(string className)
    {
        if (_fieldNamesCache.TryGetValue(className, out var names))
            return names;

        ComputeLayout(className);
        return _fieldNamesCache[className];
    }

    /// <summary>
    /// Computes and caches the complete layout for a class by traversing inheritance.
    /// </summary>
    /// <param name="className">Name of the class to compute layout for.</param>
    /// <exception cref="InvalidOperationException">Thrown when class is not found in JSON.</exception>
    private void ComputeLayout(string className)
    {
        // Special case for Object - it has no superclass
        if (className == "Object")
        {
            ComputeObjectLayout();
            return;
        }

        // Find the class in JSON
        var jsonClass = _loader.Bundle.Classes.FirstOrDefault(c =>
            _loader.Bundle.Symbols[c.NameIndex] == className && !c.IsMetaclass);

        if (jsonClass == null)
            throw new InvalidOperationException($"Class '{className}' not found in JSON");

        // Build complete field list (superclass fields first, then own fields)
        var allFields = new List<string>();

        // Add inherited fields from superclass chain
        if (jsonClass.SuperNameIndex.HasValue)
        {
            var superName = _loader.Bundle.Symbols[jsonClass.SuperNameIndex.Value];
            var superFields = GetAllFieldNames(superName); // Recursive
            allFields.AddRange(superFields);
        }

        // Add own instance variables
        var ownFields = jsonClass.InstVarNames
            .Select(idx => _loader.Bundle.Symbols[idx])
            .ToArray();
        allFields.AddRange(ownFields);

        // Build field index map
        var fieldMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < allFields.Count; i++)
        {
            fieldMap[allFields[i]] = i;
        }

        // Cache results
        _layoutCache[className] = fieldMap;
        _instanceVarCountCache[className] = allFields.Count;
        _fieldNamesCache[className] = allFields.ToArray();
    }

    /// <summary>
    /// Special case handler for Object class which has no superclass.
    /// </summary>
    private void ComputeObjectLayout()
    {
        // Object has no instance variables of its own
        var fieldMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        _layoutCache["Object"] = fieldMap;
        _instanceVarCountCache["Object"] = 0;
        _fieldNamesCache["Object"] = [];
    }

    /// <summary>
    /// Pre-computes layouts for commonly used classes to improve startup performance.
    /// </summary>
    public void PrecomputeCommonLayouts()
    {
        var commonClasses = new[]
        {
            "Object", "Behavior", "ClassDescription", "Class", "Metaclass",
            "Process", "ProcessorScheduler", "Semaphore", "LinkedList",
            "MethodContext", "BlockContext", "CompiledMethod",
            "Array", "String", "Symbol", "Dictionary", "Set", "IdentityDictionary",
            "Point", "Rectangle", "Form", "BitBlt", "Association",
            "Character", "ByteArray", "LargePositiveInteger", "LargeNegativeInteger",
            "Float", "Fraction", "Integer", "SmallInteger"
        };

        foreach (var className in commonClasses)
        {
            try
            {
                ComputeLayout(className);
            }
            catch
            {
                // Ignore if class doesn't exist in this build
            }
        }
    }

    /// <summary>
    /// Gets the field index for a class, handling special naming conventions.
    /// Some JSON may use different naming (e.g., "methodDict" vs "methodDictionary").
    /// </summary>
    /// <param name="className">Name of the class.</param>
    /// <param name="fieldName">Name of the field.</param>
    /// <param name="alternateNames">Alternative field names to try.</param>
    /// <returns>Zero-based field index.</returns>
    public int GetFieldIndexWithAlternates(string className, string fieldName, params string[] alternateNames)
    {
        try
        {
            return GetFieldIndex(className, fieldName);
        }
        catch (InvalidOperationException)
        {
            // Try alternate names
            foreach (var altName in alternateNames)
            {
                try
                {
                    return GetFieldIndex(className, altName);
                }
                catch
                {
                    // Continue to next alternate
                }
            }

            // If all alternates failed, throw original error
            throw new InvalidOperationException(
                $"Field '{fieldName}' (or alternates: {string.Join(", ", alternateNames)}) " +
                $"not found in class '{className}'");
        }
    }

    /// <summary>
    /// Clears all cached layouts. Useful for testing or reloading.
    /// </summary>
    public void ClearCache()
    {
        _layoutCache.Clear();
        _instanceVarCountCache.Clear();
        _fieldNamesCache.Clear();
    }

    /// <summary>
    /// Gets statistics about cached layouts.
    /// </summary>
    /// <returns>Dictionary containing cache statistics.</returns>
    public Dictionary<string, int> GetCacheStatistics()
    {
        return new Dictionary<string, int>
        {
            ["CachedClasses"] = _layoutCache.Count,
            ["TotalFields"] = _layoutCache.Values.Sum(d => d.Count),
            ["CachedInstanceCounts"] = _instanceVarCountCache.Count,
            ["CachedFieldNameArrays"] = _fieldNamesCache.Count
        };
    }
}