using Smalltalk80.Interpreter.Schema;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Resolves field layouts for classes by computing the complete field order
/// including inherited fields from superclasses.
/// </summary>
public class LayoutResolver
{
    private readonly Dictionary<string, List<string>> _fieldMap = new();

    /// <summary>
    /// Computes the complete field layouts for all classes in topological order.
    /// Each class's layout includes inherited fields followed by its own instance variables.
    /// </summary>
    /// <param name="classes">Collection of class descriptors</param>
    /// <param name="symbols">Symbol table for looking up field names by index</param>
    public void ComputeLayouts(IEnumerable<ClassDescriptor> classes, IReadOnlyList<string> symbols)
    {
        var classMap = classes.ToDictionary(c => c.LocalId);

        foreach (var cls in TopologicalSort(classes, symbols))
        {
            // Get inherited fields from superclass (if any)
            List<string> inherited;
            if (cls.SuperNameIndex.HasValue)
            {
                string superName = symbols[cls.SuperNameIndex.Value];
                var superClass = classMap.Values.FirstOrDefault(c => symbols[c.NameIndex] == superName);
                inherited = superClass != null ? _fieldMap[superClass.LocalId] : new List<string>();
            }
            else
            {
                inherited = new List<string>();
            }

            // Get this class's own instance variables
            var own = cls.InstVarNames.Select(i => symbols[i]).ToList();

            // Combine: inherited fields first, then own fields
            _fieldMap[cls.LocalId] = inherited.Concat(own).ToList();
        }
    }

    /// <summary>
    /// Gets the field index for a named field in a class.
    /// </summary>
    /// <param name="classLocalId">Local ID of the class</param>
    /// <param name="fieldName">Name of the field</param>
    /// <returns>Zero-based index of the field, or -1 if not found</returns>
    public int GetFieldIndex(string classLocalId, string fieldName)
    {
        return _fieldMap[classLocalId].IndexOf(fieldName);
    }

    /// <summary>
    /// Gets the total number of instance variables (including inherited) for a class.
    /// </summary>
    /// <param name="classLocalId">Local ID of the class</param>
    /// <returns>Total instance variable count, or 0 if class not found</returns>
    public int GetInstanceVariableCount(string classLocalId)
    {
        return _fieldMap.TryGetValue(classLocalId, out var fields) ? fields.Count : 0;
    }

    /// <summary>
    /// Topologically sorts classes so that superclasses appear before subclasses.
    /// This ensures we process parent layouts before child layouts.
    /// </summary>
    private IEnumerable<ClassDescriptor> TopologicalSort(IEnumerable<ClassDescriptor> classes, IReadOnlyList<string> symbols)
    {
        var classArray = classes.ToArray();
        var visited = new HashSet<string>();
        var result = new List<ClassDescriptor>();

        void Visit(ClassDescriptor cls)
        {
            if (visited.Contains(cls.LocalId))
                return;

            visited.Add(cls.LocalId);

            // Visit superclass first (if it exists)
            if (cls.SuperNameIndex.HasValue)
            {
                string superName = symbols[cls.SuperNameIndex.Value];
                var superClass = classArray.FirstOrDefault(c => symbols[c.NameIndex] == superName);
                if (superClass != null)
                {
                    Visit(superClass);
                }
            }

            result.Add(cls);
        }

        // Visit all classes, which will recursively visit their superclasses first
        foreach (var cls in classArray)
        {
            Visit(cls);
        }

        return result;
    }
}
