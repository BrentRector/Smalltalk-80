using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Utility class for class hierarchy operations shared across loader phases.
/// Provides common functionality for instance variable counting, class lookup, and variable name resolution.
/// All methods work from JSON bundle data to avoid hard-coding assumptions.
/// </summary>
public static class ClassHierarchyUtils
{
    /// <summary>
    /// Calculates the total instance variable count for a class including inherited ones.
    /// Uses JSON bundle data to traverse the inheritance chain.
    /// </summary>
    public static int CalculateTotalInstanceVariableCount(ClassDescriptor classDefinition, ColdStartBundle bundle)
    {
        int totalCount = 0;
        var currentClass = classDefinition;
        
        // Traverse superclass chain to count all instance variables
        while (currentClass != null)
        {
            totalCount += currentClass.InstVarNames.Length;
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        return totalCount;
    }

    /// <summary>
    /// Calculates the total instance variable count for a class by name.
    /// </summary>
    public static int CalculateTotalInstanceVariableCount(string className, ColdStartBundle bundle)
    {
        var classDefinition = FindClassDefinition(className, bundle);
        if (classDefinition == null)
        {
            throw new InvalidOperationException($"Class '{className}' not found in bundle");
        }
        
        var result = CalculateTotalInstanceVariableCount(classDefinition, bundle);
        
        // DEBUG: Log SystemDictionary instance variable count calculation
        if (className == "SystemDictionary")
        {
            Console.WriteLine($"[CalculateInstanceVarCount] {className} total count: {result}");
            
            // Show hierarchy
            var currentClass = classDefinition;
            while (currentClass != null)
            {
                string currentClassName = bundle.Symbols[currentClass.NameIndex];
                Console.WriteLine($"  - {currentClassName}: {currentClass.InstVarNames.Length} vars");
                
                if (currentClass.SuperNameIndex.HasValue)
                {
                    string superName = bundle.Symbols[currentClass.SuperNameIndex.Value];
                    currentClass = bundle.Classes.FirstOrDefault(c => 
                        !c.IsMetaclass && bundle.Symbols[c.NameIndex] == superName);
                }
                else
                {
                    currentClass = null;
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Finds a class definition by name in the bundle.
    /// Only searches non-metaclass definitions.
    /// </summary>
    public static ClassDescriptor? FindClassDefinition(string className, ColdStartBundle bundle)
    {
        return bundle.Classes.FirstOrDefault(c => 
            !c.IsMetaclass && bundle.Symbols[c.NameIndex] == className);
    }

    /// <summary>
    /// Finds the index of an instance variable by name in a class hierarchy.
    /// Returns -1 if not found.
    /// </summary>
    public static int FindInstanceVariableIndex(string className, string variableName, ColdStartBundle bundle)
    {
        var classDefinition = FindClassDefinition(className, bundle);
        if (classDefinition == null)
            return -1;
            
        // Build complete instance variable list including inherited ones
        var allInstVars = new List<string>();
        var currentClass = classDefinition;
        
        // Traverse superclass chain to collect all instance variables
        var classChain = new List<ClassDescriptor>();
        while (currentClass != null)
        {
            classChain.Add(currentClass);
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        // Add instance variables from superclass to subclass order
        classChain.Reverse();
        foreach (var cls in classChain)
        {
            foreach (int varIndex in cls.InstVarNames)
            {
                allInstVars.Add(bundle.Symbols[varIndex]);
            }
        }
        
        // Find the variable in the complete list
        for (int i = 0; i < allInstVars.Count; i++)
        {
            if (allInstVars[i] == variableName)
            {
                return i;
            }
        }
        
        return -1; // Not found
    }

    /// <summary>
    /// Checks if a class has a specific instance variable.
    /// </summary>
    public static bool HasInstanceVariable(string className, string variableName, ColdStartBundle bundle)
    {
        return FindInstanceVariableIndex(className, variableName, bundle) != -1;
    }

    /// <summary>
    /// Gets all instance variable names for a class in inheritance order (superclass vars first).
    /// </summary>
    public static List<string> GetAllInstanceVariableNames(string className, ColdStartBundle bundle)
    {
        var classDefinition = FindClassDefinition(className, bundle);
        if (classDefinition == null)
        {
            return new List<string>();
        }
        
        var allInstVars = new List<string>();
        var currentClass = classDefinition;
        
        // Traverse superclass chain to collect all instance variables
        var classChain = new List<ClassDescriptor>();
        while (currentClass != null)
        {
            classChain.Add(currentClass);
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        // Add instance variables from superclass to subclass order
        classChain.Reverse();
        foreach (var cls in classChain)
        {
            foreach (int varIndex in cls.InstVarNames)
            {
                allInstVars.Add(bundle.Symbols[varIndex]);
            }
        }
        
        return allInstVars;
    }

    /// <summary>
    /// Gets the symbol index for an instance variable name, or returns null if not found.
    /// </summary>
    public static int? GetInstanceVariableSymbolIndex(string className, string variableName, ColdStartBundle bundle)
    {
        var classDefinition = FindClassDefinition(className, bundle);
        if (classDefinition == null)
            return null;
            
        var currentClass = classDefinition;
        
        // Traverse superclass chain to find the variable
        while (currentClass != null)
        {
            foreach (int varIndex in currentClass.InstVarNames)
            {
                if (bundle.Symbols[varIndex] == variableName)
                {
                    return varIndex;
                }
            }
            
            if (currentClass.SuperNameIndex.HasValue)
            {
                string superName = bundle.Symbols[currentClass.SuperNameIndex.Value];
                currentClass = bundle.Classes.FirstOrDefault(c => 
                    !c.IsMetaclass && bundle.Symbols[c.NameIndex] == superName);
            }
            else
            {
                currentClass = null;
            }
        }
        
        return null; // Not found
    }

    /// <summary>
    /// Decodes bytecodes from either array or base64 string format.
    /// Centralized method to eliminate code duplication across phases.
    /// </summary>
    public static byte[] DecodeBytecodes(object bytecodes)
    {
        return bytecodes switch
        {
            System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.Array =>
                e.EnumerateArray().Select(b => (byte)b.GetInt32()).ToArray(),
            System.Text.Json.JsonElement e when e.ValueKind == System.Text.Json.JsonValueKind.String =>
                Convert.FromBase64String(e.GetString()!),
            _ => throw new InvalidOperationException("Invalid bytecode format")
        };
    }
}