using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Resolves field layouts for classes by loading precomputed layouts from the JSON bundle.
/// v1.0.6+: No longer computes layouts at runtime - simply loads precomputed data.
/// </summary>
public class LayoutResolver
{
    private readonly Dictionary<string, List<string>> _fieldMap = new();
    private bool _isFrozen = false;

    /// <summary>
    /// v1.0.6+: Loads precomputed field layouts for all classes.
    /// Each class's layout is already computed by the compiler and includes inherited fields followed by own instance variables.
    /// This eliminates the need for topological sort and inheritance chain traversal.
    /// </summary>
    /// <param name="classes">Collection of class descriptors with precomputed layouts</param>
    /// <param name="symbols">Symbol table for looking up field names by index</param>
    public void LoadLayouts(IEnumerable<ClassDescriptor> classes, IReadOnlyList<string> symbols)
    {
        foreach (var cls in classes)
        {
            // v1.0.6+: Load precomputed field layout (already in correct order)
            var fields = new List<string>(cls.FieldLayout.Length);

            foreach (var entry in cls.FieldLayout)
            {
                string fieldName = symbols[entry.NameIndex];
                fields.Add(fieldName);
            }

            _fieldMap[cls.LocalId] = fields;
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
    /// Sets the field layout for a class manually (used by new loader).
    /// </summary>
    /// <param name="classLocalId">Local ID of the class</param>
    /// <param name="fields">Ordered list of field names</param>
    public void SetFieldLayout(string classLocalId, IReadOnlyList<string> fields)
    {
        if (_isFrozen)
            throw new InvalidOperationException($"Cannot set field layout after LayoutResolver is frozen. Attempted to set layout for: {classLocalId}");

        _fieldMap[classLocalId] = new List<string>(fields);
    }

    /// <summary>
    /// Freezes the LayoutResolver, preventing further modifications.
    /// Called after all layouts have been loaded during cold start.
    /// </summary>
    public void Freeze()
    {
        _isFrozen = true;
    }

    /// <summary>
    /// Gets whether the LayoutResolver has been frozen.
    /// </summary>
    public bool IsFrozen => _isFrozen;

    /// <summary>
    /// Exports all class layouts for serialization.
    /// </summary>
    public Dictionary<string, List<string>> ExportLayouts()
    {
        return new Dictionary<string, List<string>>(_fieldMap);
    }

    /// <summary>
    /// Imports class layouts from serialized data.
    /// </summary>
    public void ImportLayouts(Dictionary<string, List<string>> layouts)
    {
        if (_isFrozen)
            throw new InvalidOperationException("Cannot import layouts after LayoutResolver is frozen");

        foreach (var (classId, fields) in layouts)
        {
            _fieldMap[classId] = new List<string>(fields);
        }
    }
}
