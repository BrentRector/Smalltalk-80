using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator;

/// <summary>
/// Provides efficient access to cold start bundle data for validators.
/// Builds indexes and lookup structures for cross-reference validation.
/// </summary>
public class ValidationContext
{
    public ColdStartBundle Bundle { get; }
    
    /// <summary>
    /// Maps localId to all occurrences (section name, index)
    /// </summary>
    public Dictionary<string, List<(string Section, int Index)>> LocalIdIndex { get; } = new();
    
    /// <summary>
    /// Maps class localId to its ClassDefinition
    /// </summary>
    public Dictionary<string, ClassDefinition> ClassByLocalId { get; } = new();
    
    /// <summary>
    /// Maps method localId to its MethodDefinition
    /// </summary>
    public Dictionary<string, MethodDefinition> MethodByLocalId { get; } = new();
    
    /// <summary>
    /// Maps object localId to its ObjectDefinition
    /// </summary>
    public Dictionary<string, ObjectDefinition> ObjectByLocalId { get; } = new();
    
    /// <summary>
    /// Maps patch id to its Patch
    /// </summary>
    public Dictionary<string, Patch> PatchByLocalId { get; } = new();
    
    /// <summary>
    /// Maps init expression localId to its InitExpression
    /// </summary>
    public Dictionary<string, InitExpression> InitExprByLocalId { get; } = new();
    
    /// <summary>
    /// Maps class name index to class localId
    /// </summary>
    public Dictionary<int, string> ClassLocalIdByNameIndex { get; } = new();
    
    /// <summary>
    /// Maps class localId to list of method localIds
    /// </summary>
    public Dictionary<string, List<string>> MethodsByClass { get; } = new();
    
    /// <summary>
    /// All localIds that appear in install order
    /// </summary>
    public HashSet<string> InstalledLocalIds { get; } = new();
    
    public ValidationContext(ColdStartBundle bundle)
    {
        Bundle = bundle;
        BuildIndexes();
    }
    
    private void BuildIndexes()
    {
        // Index classes
        for (int i = 0; i < Bundle.Classes.Length; i++)
        {
            var cls = Bundle.Classes[i];
            if (!string.IsNullOrEmpty(cls.LocalId))
            {
                AddToLocalIdIndex(cls.LocalId, "classes", i);
                ClassByLocalId[cls.LocalId] = cls;
                // NameIndex is a required field, not nullable
                ClassLocalIdByNameIndex[cls.NameIndex] = cls.LocalId;
            }
        }
        
        // Index methods
        for (int i = 0; i < Bundle.Methods.Length; i++)
        {
            var method = Bundle.Methods[i];
            if (!string.IsNullOrEmpty(method.LocalId))
            {
                AddToLocalIdIndex(method.LocalId, "methods", i);
                MethodByLocalId[method.LocalId] = method;
                
                // Track methods by class
                if (!string.IsNullOrEmpty(method.HomeClassLocalId))
                {
                    if (!MethodsByClass.ContainsKey(method.HomeClassLocalId))
                        MethodsByClass[method.HomeClassLocalId] = new List<string>();
                    MethodsByClass[method.HomeClassLocalId].Add(method.LocalId);
                }
            }
        }
        
        // Index objects
        for (int i = 0; i < Bundle.Objects.Length; i++)
        {
            var obj = Bundle.Objects[i];
            if (!string.IsNullOrEmpty(obj.LocalId))
            {
                AddToLocalIdIndex(obj.LocalId, "objects", i);
                ObjectByLocalId[obj.LocalId] = obj;
            }
        }
        
        // Index init expressions
        for (int i = 0; i < Bundle.InitExpressions.Length; i++)
        {
            var initExpr = Bundle.InitExpressions[i];
            if (!string.IsNullOrEmpty(initExpr.LocalId))
            {
                AddToLocalIdIndex(initExpr.LocalId, "initExpressions", i);
                InitExprByLocalId[initExpr.LocalId] = initExpr;
            }
        }
        
        // Index patches
        for (int i = 0; i < Bundle.Patches.Length; i++)
        {
            var patch = Bundle.Patches[i];
            if (!string.IsNullOrEmpty(patch.PatchId))
            {
                AddToLocalIdIndex(patch.PatchId, "patches", i);
                PatchByLocalId[patch.PatchId] = patch;
            }
        }
        
        // Index install order
        foreach (var phase in Bundle.InstallOrder.Phases)
        {
            foreach (var localId in phase.LocalIds)
            {
                InstalledLocalIds.Add(localId);
            }
        }
    }
    
    private void AddToLocalIdIndex(string localId, string section, int index)
    {
        if (!LocalIdIndex.ContainsKey(localId))
            LocalIdIndex[localId] = new List<(string, int)>();
        LocalIdIndex[localId].Add((section, index));
    }
    
    public bool LocalIdExists(string localId)
    {
        return LocalIdIndex.ContainsKey(localId);
    }
    
    public string? GetSymbol(int index)
    {
        if (index < 0 || index >= Bundle.Symbols.Length)
            return null;
        return Bundle.Symbols[index];
    }
    
    public string? GetString(int index)
    {
        if (index < 0 || index >= Bundle.Strings.Length)
            return null;
        return Bundle.Strings[index];
    }
}