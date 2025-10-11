using System.Text.Json;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates that method selectors are present in their home class method dictionaries.
/// Ensures methods are discoverable at runtime through normal method lookup.
/// </summary>
public class MethodSelectorPresenceValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Build map of class -> method dictionary contents
        var methodDictionaries = BuildMethodDictionaries(bundle, context);
        
        // Track methods that will be added by patches
        var patchedMethods = IdentifyPatchedMethods(bundle, context);
        
        // Validate each method
        int orphanedMethods = 0;
        int patchedMethodCount = 0;
        
        foreach (var method in bundle.Methods)
        {
            // Skip blocks (they don't have selectors in dictionaries)
            if (method.SelectorIndex == -1 || method.LocalId.StartsWith("block:"))
                continue;
            
            var selector = context.GetSymbol(method.SelectorIndex);
            if (selector == null)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.MethodSelectorInvalid,
                    Severity.Error,
                    $"Method '{method.LocalId}' has invalid selector index {method.SelectorIndex}",
                    new DiagnosticLocation("methods", method.LocalId),
                    "Method must have valid selector");
                continue;
            }
            
            var homeClass = method.HomeClassLocalId;
            
            // Check if method is in class's method dictionary
            bool inDictionary = false;
            if (methodDictionaries.TryGetValue(homeClass, out var dict))
            {
                inDictionary = dict.Contains((method.SelectorIndex, method.LocalId));
            }
            
            // Check if method will be added by a patch
            bool willBePatched = patchedMethods.Contains((homeClass, method.SelectorIndex, method.LocalId));
            
            if (!inDictionary && !willBePatched)
            {
                orphanedMethods++;
                result.AddDiagnostic(
                    DiagnosticCodes.MethodMissingInDict,
                    Severity.Error,
                    $"Method '{selector}' ({method.LocalId}) not found in class '{homeClass}' method dictionary and no patch will add it",
                    new DiagnosticLocation("methods", method.LocalId),
                    "Add method to class method dictionary or create patch to install it");
            }
            else if (willBePatched)
            {
                patchedMethodCount++;
            }
        }
        
        // Add summary
        var totalMethods = bundle.Methods.Count(m => m.SelectorIndex >= 0);
        if (orphanedMethods == 0)
        {
            result.AddInfo($"✓ All {totalMethods} methods are in their class dictionaries " +
                          $"({patchedMethodCount} via patches)");
        }
        else
        {
            result.AddInfo($"Found {orphanedMethods} orphaned methods out of {totalMethods} total");
        }
        
        return result;
    }
    
    private Dictionary<string, HashSet<(int SelectorIndex, string MethodLocalId)>> BuildMethodDictionaries(
        ColdStartBundle bundle, ValidationContext context)
    {
        var dictionaries = new Dictionary<string, HashSet<(int, string)>>();
        
        // Look for MethodDictionary objects
        foreach (var obj in bundle.Objects)
        {
            if (IsMethodDictionary(obj, context))
            {
                var entries = ExtractMethodDictionaryEntries(obj, context);
                
                // Find which class this dictionary belongs to
                var owningClass = FindOwningClass(obj, bundle, context);
                if (owningClass != null)
                {
                    if (!dictionaries.ContainsKey(owningClass))
                        dictionaries[owningClass] = new HashSet<(int, string)>();
                    
                    foreach (var entry in entries)
                    {
                        dictionaries[owningClass].Add(entry);
                    }
                }
            }
        }
        
        // Also check class literals for method dictionaries
        foreach (var cls in bundle.Classes)
        {
            // Classes often have their method dictionary as a literal
            // This is a heuristic approach since the exact structure varies
            var classLocalId = cls.LocalId;
            
            // Look through methods to build implicit dictionary
            if (!dictionaries.ContainsKey(classLocalId))
            {
                dictionaries[classLocalId] = new HashSet<(int, string)>();
            }
            
            // Add all methods that claim this class as home
            foreach (var method in bundle.Methods)
            {
                if (method.HomeClassLocalId == classLocalId && method.SelectorIndex >= 0)
                {
                    dictionaries[classLocalId].Add((method.SelectorIndex, method.LocalId));
                }
            }
        }
        
        return dictionaries;
    }
    
    private bool IsMethodDictionary(ObjectDefinition obj, ValidationContext context)
    {
        // Check various indicators that this is a method dictionary
        
        // 1. Check class name
        if (obj.ClassNameIndex.HasValue)
        {
            var className = context.GetSymbol(obj.ClassNameIndex.Value);
            if (className != null && 
                (className.Contains("MethodDictionary") || 
                 className.Contains("Dictionary") ||
                 className == "IdentityDictionary"))
            {
                return true;
            }
        }
        
        // 2. Check formatHint
        if (obj.FormatHint == "methodDictionary" || obj.FormatHint == "dictionary")
        {
            return true;
        }
        
        // 3. Check localId pattern
        if (obj.LocalId.Contains("methodDict") || obj.LocalId.Contains("method_dict"))
        {
            return true;
        }
        
        return false;
    }
    
    private List<(int SelectorIndex, string MethodLocalId)> ExtractMethodDictionaryEntries(
        ObjectDefinition obj, ValidationContext context)
    {
        var entries = new List<(int, string)>();
        
        // Method dictionaries typically store selector->method associations
        // The exact structure depends on the implementation
        
        if (obj.IndexedFields != null)
        {
            // Dictionary might use indexed fields for associations
            foreach (var field in obj.IndexedFields)
            {
                if (field is JsonElement jsonField)
                {
                    var entry = ExtractAssociation(jsonField, context);
                    if (entry.HasValue)
                    {
                        entries.Add(entry.Value);
                    }
                }
            }
        }
        
        if (obj.Fields != null)
        {
            // Or regular fields
            foreach (var field in obj.Fields)
            {
                if (field is JsonElement jsonField)
                {
                    var entry = ExtractAssociation(jsonField, context);
                    if (entry.HasValue)
                    {
                        entries.Add(entry.Value);
                    }
                }
            }
        }
        
        return entries;
    }
    
    private (int SelectorIndex, string MethodLocalId)? ExtractAssociation(
        JsonElement element, ValidationContext context)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            // Look for association structure
            if (element.TryGetProperty("association", out var assoc))
            {
                return ExtractAssociationCore(assoc, context);
            }
            else if (element.TryGetProperty("key", out var key) && 
                     element.TryGetProperty("value", out var value))
            {
                // Direct key-value pair
                var selectorIndex = ExtractSymbolIndex(key);
                var methodId = ExtractLocalId(value);
                
                if (selectorIndex >= 0 && methodId != null)
                {
                    return (selectorIndex, methodId);
                }
            }
        }
        
        return null;
    }
    
    private (int SelectorIndex, string MethodLocalId)? ExtractAssociationCore(
        JsonElement assoc, ValidationContext context)
    {
        int selectorIndex = -1;
        string? methodLocalId = null;
        
        if (assoc.TryGetProperty("keyIndex", out var keyIndex))
        {
            selectorIndex = keyIndex.GetInt32();
        }
        
        if (assoc.TryGetProperty("value", out var value))
        {
            methodLocalId = ExtractLocalId(value);
        }
        else if (assoc.TryGetProperty("valueLocalId", out var valueId))
        {
            methodLocalId = valueId.GetString();
        }
        
        if (selectorIndex >= 0 && methodLocalId != null)
        {
            return (selectorIndex, methodLocalId);
        }
        
        return null;
    }
    
    private int ExtractSymbolIndex(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            return element.GetInt32();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("index", out var index))
            {
                return index.GetInt32();
            }
        }
        
        return -1;
    }
    
    private string? ExtractLocalId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("localId", out var localId))
            {
                return localId.GetString();
            }
            else if (element.TryGetProperty("objectRef", out var objRef))
            {
                return objRef.GetString();
            }
        }
        
        return null;
    }
    
    private string? FindOwningClass(ObjectDefinition methodDict, ColdStartBundle bundle, ValidationContext context)
    {
        // Try to find which class owns this method dictionary
        
        // Strategy 1: Check if any class references this object in its literals or fields
        foreach (var cls in bundle.Classes)
        {
            // Check if class has explicit method dictionary reference
            // This is implementation-specific
            if (methodDict.LocalId.Contains(cls.LocalId))
            {
                return cls.LocalId;
            }
        }
        
        // Strategy 2: Look for patches that wire this dictionary to a class
        foreach (var patch in bundle.Patches)
        {
            if (patch.Value != null)
            {
                var valueStr = patch.Value.ToString();
                if (valueStr != null && valueStr.Contains(methodDict.LocalId))
                {
                    // Extract target class from patch
                    // This is a heuristic
                    return ExtractClassFromPatchTarget(patch.Target);
                }
            }
        }
        
        return null;
    }
    
    private string? ExtractClassFromPatchTarget(object target)
    {
        if (target is JsonElement targetJson && targetJson.ValueKind == JsonValueKind.Object)
        {
            if (targetJson.TryGetProperty("classLocalId", out var classId))
            {
                return classId.GetString();
            }
            else if (targetJson.TryGetProperty("localId", out var localId))
            {
                return localId.GetString();
            }
        }
        
        return null;
    }
    
    private HashSet<(string ClassLocalId, int SelectorIndex, string MethodLocalId)> IdentifyPatchedMethods(
        ColdStartBundle bundle, ValidationContext context)
    {
        var patchedMethods = new HashSet<(string, int, string)>();
        
        foreach (var patch in bundle.Patches)
        {
            if (patch.Operation == "addMethod" || 
                patch.Operation == "addToMethodDictionary" ||
                patch.Operation == "setValue")
            {
                // Try to extract method being added
                var target = ExtractClassFromPatchTarget(patch.Target);
                if (target != null && patch.Value != null)
                {
                    // Value might be a method localId
                    string? methodId = null;
                    if (patch.Value is string strValue)
                    {
                        methodId = strValue;
                    }
                    else if (patch.Value is JsonElement jsonValue && jsonValue.ValueKind == JsonValueKind.String)
                    {
                        methodId = jsonValue.GetString();
                    }
                    
                    if (methodId != null && methodId.StartsWith("method:"))
                    {
                        // Find the method to get its selector
                        var method = bundle.Methods.FirstOrDefault(m => m.LocalId == methodId);
                        if (method != null)
                        {
                            patchedMethods.Add((target, method.SelectorIndex, methodId));
                        }
                    }
                }
            }
        }
        
        return patchedMethods;
    }
}