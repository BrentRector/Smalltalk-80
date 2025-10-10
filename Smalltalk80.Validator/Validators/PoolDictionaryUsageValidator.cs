using System.Text.Json;
using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates pool dictionary usage:
/// - Pool localIds reference actual Dictionary objects
/// - Pool dictionaries contain expected symbol keys
/// - Classes that reference pools have them available
/// </summary>
public class PoolDictionaryUsageValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();
        var context = new ValidationContext(bundle);
        
        // Collect all pool dictionaries
        var poolDictionaries = new Dictionary<string, PoolInfo>();
        
        // Find all pool dictionary objects
        foreach (var obj in bundle.Objects)
        {
            if (IsPoolDictionary(obj, context))
            {
                var poolInfo = ExtractPoolInfo(obj, context);
                poolDictionaries[obj.LocalId] = poolInfo;
            }
        }
        
        // Validate pool references from classes
        int classesWithPools = 0;
        int validPoolReferences = 0;
        int invalidPoolReferences = 0;
        
        foreach (var cls in bundle.Classes)
        {
            if (cls.PoolLocalIds != null && cls.PoolLocalIds.Length > 0)
            {
                classesWithPools++;
                
                foreach (var poolLocalId in cls.PoolLocalIds)
                {
                    if (string.IsNullOrEmpty(poolLocalId))
                        continue;
                    
                    if (!context.LocalIdExists(poolLocalId))
                    {
                        invalidPoolReferences++;
                        result.AddDiagnostic(
                            DiagnosticCodes.PatchTargetMissing,
                            Severity.Error,
                            $"Class '{context.GetSymbol(cls.NameIndex)}' references non-existent pool '{poolLocalId}'",
                            new DiagnosticLocation("classes", cls.LocalId),
                            "Pool dictionary must exist");
                    }
                    else if (!poolDictionaries.ContainsKey(poolLocalId))
                    {
                        // Object exists but isn't a dictionary
                        if (context.ObjectByLocalId.TryGetValue(poolLocalId, out var obj))
                        {
                            var className = obj.ClassNameIndex.HasValue ? 
                                context.GetSymbol(obj.ClassNameIndex.Value) : "unknown";
                            
                            if (!IsDictionaryClass(className))
                            {
                                invalidPoolReferences++;
                                result.AddDiagnostic(
                                    DiagnosticCodes.PatchInvalid,
                                    Severity.Error,
                                    $"Class '{context.GetSymbol(cls.NameIndex)}' pool reference '{poolLocalId}' " +
                                    $"is not a Dictionary (class: {className})",
                                    new DiagnosticLocation("classes", cls.LocalId),
                                    "Pool must be a Dictionary object");
                            }
                            else
                            {
                                validPoolReferences++;
                            }
                        }
                    }
                    else
                    {
                        validPoolReferences++;
                        
                        // Validate pool contents are accessible
                        var poolInfo = poolDictionaries[poolLocalId];
                        ValidatePoolContents(result, context, cls, poolLocalId, poolInfo);
                    }
                }
            }
        }
        
        // Check for orphaned pool dictionaries (pools not referenced by any class)
        var referencedPools = new HashSet<string>();
        foreach (var cls in bundle.Classes)
        {
            if (cls.PoolLocalIds != null)
            {
                foreach (var poolId in cls.PoolLocalIds)
                {
                    if (!string.IsNullOrEmpty(poolId))
                        referencedPools.Add(poolId);
                }
            }
        }
        
        int orphanedPools = 0;
        foreach (var poolId in poolDictionaries.Keys)
        {
            if (!referencedPools.Contains(poolId))
            {
                orphanedPools++;
                result.AddDiagnostic(
                    DiagnosticCodes.PatchInvalid,
                    Severity.Warning,
                    $"Pool dictionary '{poolId}' is not referenced by any class",
                    new DiagnosticLocation("objects", poolId),
                    "Consider removing unused pool dictionary");
            }
        }
        
        // Add summary
        if (invalidPoolReferences == 0)
        {
            result.AddInfo($"✓ Pool dictionaries validated: {poolDictionaries.Count} pools, " +
                          $"{classesWithPools} classes with pools, {orphanedPools} orphaned");
        }
        else
        {
            result.AddInfo($"Found {invalidPoolReferences} invalid pool references out of " +
                          $"{validPoolReferences + invalidPoolReferences} total");
        }
        
        return result;
    }
    
    private class PoolInfo
    {
        public HashSet<int> SymbolKeys { get; } = new();
        public Dictionary<int, object> Entries { get; } = new();
    }
    
    private bool IsPoolDictionary(ObjectDefinition obj, ValidationContext context)
    {
        // Check if object is a pool dictionary
        
        // 1. Check localId naming
        if (obj.LocalId.StartsWith("pool:"))
            return true;
        
        // 2. Check class
        if (obj.ClassNameIndex.HasValue)
        {
            var className = context.GetSymbol(obj.ClassNameIndex.Value);
            if (className != null && 
                (className.Contains("Pool") || 
                 className == "Dictionary" || 
                 className == "IdentityDictionary" ||
                 className == "SystemDictionary"))
            {
                // Additional heuristic: pools often have symbolic keys
                return true;
            }
        }
        
        // 3. Check formatHint
        if (obj.FormatHint == "pool" || obj.FormatHint == "poolDictionary")
            return true;
        
        return false;
    }
    
    private bool IsDictionaryClass(string? className)
    {
        if (className == null)
            return false;
        
        return className.Contains("Dictionary") || 
               className.Contains("Pool") ||
               className == "Set" ||
               className == "IdentitySet";
    }
    
    private PoolInfo ExtractPoolInfo(ObjectDefinition poolObj, ValidationContext context)
    {
        var info = new PoolInfo();
        
        // Extract entries from pool dictionary
        if (poolObj.IndexedFields != null)
        {
            foreach (var field in poolObj.IndexedFields)
            {
                ExtractPoolEntry(field, info, context);
            }
        }
        
        if (poolObj.Fields != null)
        {
            foreach (var field in poolObj.Fields)
            {
                ExtractPoolEntry(field, info, context);
            }
        }
        
        return info;
    }
    
    private void ExtractPoolEntry(object field, PoolInfo info, ValidationContext context)
    {
        if (field is JsonElement jsonField && jsonField.ValueKind == JsonValueKind.Object)
        {
            // Look for association structure
            if (jsonField.TryGetProperty("association", out var assoc))
            {
                ExtractAssociationEntry(assoc, info);
            }
            else if (jsonField.TryGetProperty("key", out var key) && 
                     jsonField.TryGetProperty("value", out var value))
            {
                // Direct key-value
                if (key.TryGetProperty("index", out var keyIndex))
                {
                    var symbolIndex = keyIndex.GetInt32();
                    info.SymbolKeys.Add(symbolIndex);
                    info.Entries[symbolIndex] = value;
                }
            }
        }
    }
    
    private void ExtractAssociationEntry(JsonElement assoc, PoolInfo info)
    {
        if (assoc.TryGetProperty("keyIndex", out var keyIndex))
        {
            var symbolIndex = keyIndex.GetInt32();
            info.SymbolKeys.Add(symbolIndex);
            
            if (assoc.TryGetProperty("value", out var value))
            {
                info.Entries[symbolIndex] = value;
            }
        }
        else if (assoc.TryGetProperty("key", out var key))
        {
            if (key.TryGetProperty("index", out var index))
            {
                var symbolIndex = index.GetInt32();
                info.SymbolKeys.Add(symbolIndex);
            }
        }
    }
    
    private void ValidatePoolContents(ValidationResult result, ValidationContext context,
        ClassDefinition cls, string poolLocalId, PoolInfo poolInfo)
    {
        var className = context.GetSymbol(cls.NameIndex);
        
        // Check if pool has any entries
        if (poolInfo.SymbolKeys.Count == 0)
        {
            result.AddDiagnostic(
                DiagnosticCodes.PatchInvalid,
                Severity.Warning,
                $"Pool dictionary '{poolLocalId}' referenced by class '{className}' is empty",
                new DiagnosticLocation("objects", poolLocalId),
                "Pool dictionaries should contain shared constants");
        }
        
        // Validate all keys are valid symbols
        foreach (var symbolIndex in poolInfo.SymbolKeys)
        {
            var symbol = context.GetSymbol(symbolIndex);
            if (symbol == null)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.PatchInvalid,
                    Severity.Error,
                    $"Pool dictionary '{poolLocalId}' has invalid symbol key index {symbolIndex}",
                    new DiagnosticLocation("objects", poolLocalId),
                    "Pool keys must be valid symbols");
            }
        }
        
        // Check for common pool patterns
        ValidateCommonPoolPatterns(result, context, poolLocalId, poolInfo);
    }
    
    private void ValidateCommonPoolPatterns(ValidationResult result, ValidationContext context,
        string poolLocalId, PoolInfo poolInfo)
    {
        // Check for common pool dictionary patterns
        var symbolNames = new List<string>();
        foreach (var symbolIndex in poolInfo.SymbolKeys)
        {
            var symbol = context.GetSymbol(symbolIndex);
            if (symbol != null)
                symbolNames.Add(symbol);
        }
        
        // Common patterns:
        // 1. Character constants pool (Cr, Lf, Tab, Space, etc.)
        if (symbolNames.Any(s => s == "Cr" || s == "Lf" || s == "Tab" || s == "Space"))
        {
            // This appears to be a character constants pool - validate it has expected entries
            var expectedChars = new[] { "Cr", "Lf", "Tab", "Space", "Backspace" };
            foreach (var expected in expectedChars)
            {
                if (!symbolNames.Contains(expected))
                {
                    result.AddDiagnostic(
                        DiagnosticCodes.PatchInvalid,
                        Severity.Info,
                        $"Character constants pool '{poolLocalId}' missing common entry '{expected}'",
                        new DiagnosticLocation("objects", poolLocalId),
                        $"Consider adding '{expected}' to character constants pool");
                }
            }
        }
        
        // 2. Text constants pool (TextEmphasis, etc.)
        if (symbolNames.Any(s => s.StartsWith("Text")))
        {
            // Text constants pool - just note it
            if (poolInfo.SymbolKeys.Count < 3)
            {
                result.AddDiagnostic(
                    DiagnosticCodes.PatchInvalid,
                    Severity.Info,
                    $"Text constants pool '{poolLocalId}' has only {poolInfo.SymbolKeys.Count} entries",
                    new DiagnosticLocation("objects", poolLocalId),
                    "Text constant pools typically have multiple emphasis constants");
            }
        }
    }
}