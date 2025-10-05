using Smalltalk80.Interpreter.Core.BlueBook;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Generated;
using Smalltalk80.Interpreter.Memory;
using System.Reflection;

namespace Smalltalk80.Interpreter.ColdStart;

/// <summary>
/// Installs all methods from compiler-generated ColdStartData into the class hierarchy.
/// This is the ONLY method installer - all method data comes from the compiler's analysis
/// of the Smalltalk-80.st source file.
/// </summary>
public static class ColdStartMethodInstaller
{
    /// <summary>
    /// Installs all methods from ColdStartData into all cold start classes.
    /// </summary>
    public static void InstallAllMethods()
    {
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartMethodInstaller] Installing {ColdStartData.Statistics.TotalMethods} methods from compiler-generated data");
        }

        // Get the ColdStartData.Classes type which contains all class data
        var classesType = typeof(ColdStartData.Classes);
        var nestedTypes = classesType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static);

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartMethodInstaller] Found {nestedTypes.Length} class types in ColdStartData.Classes");
        }

        int totalInstalled = 0;

        // Iterate through all nested class types (each represents a Smalltalk class)
        foreach (var classType in nestedTypes)
        {
            var className = classType.Name;

            // Install instance methods
            int instanceCount = InstallMethodsForClass(className, classType, isClassSide: false);
            totalInstalled += instanceCount;

            // Install class methods
            int classCount = InstallMethodsForClass(className, classType, isClassSide: true);
            totalInstalled += classCount;
        }

        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[ColdStartMethodInstaller] Successfully installed {totalInstalled} methods");
        }
    }

    /// <summary>
    /// Installs all instance or class methods for a specific class.
    /// Returns the number of methods installed.
    /// </summary>
    private static int InstallMethodsForClass(string className, Type classType, bool isClassSide)
    {
        // Get the InstanceMethods or ClassMethods nested type
        var methodsTypeName = isClassSide ? "ClassMethods" : "InstanceMethods";
        var methodsType = classType.GetNestedType(methodsTypeName, BindingFlags.Public | BindingFlags.Static);

        if (methodsType == null)
        {
            // No methods for this class (e.g., some classes have no class methods)
            return 0;
        }

        // Get the class reference from SystemDictionary
        var classRefNullable = Core.Objects.SystemDictionary.LookupClassRef(className);
        if (!classRefNullable.HasValue || classRefNullable.Value.IsNil)
        {
            // Skip classes that don't exist (e.g., ArrayedCollection is a superclass not in cold start)
            // This is expected - the compiler includes superclasses but SystemDictionary doesn't create them all
            return 0;
        }

        var classRef = classRefNullable.Value;

        // For class methods, we need the metaclass
        nint targetClassOop = isClassSide
            ? ObjectMemory.GetClass(classRef.AsObjectPointer()).AsObjectPointer()
            : classRef.AsObjectPointer();

        // Iterate through all method types (each nested type represents one method)
        int count = 0;
        foreach (var methodType in methodsType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            InstallMethod(className, targetClassOop, methodType, isClassSide);
            count++;
        }

        return count;
    }

    /// <summary>
    /// Installs a single method into a class.
    /// </summary>
    private static void InstallMethod(string className, nint targetClassOop, Type methodType, bool isClassSide)
    {
        try
        {
            // Read method metadata from static fields
            var selector = (string)methodType.GetField("Selector")!.GetValue(null)!;
            var numArgs = (int)methodType.GetField("NumArgs")!.GetValue(null)!;
            var numTemps = (int)methodType.GetField("NumTemps")!.GetValue(null)!;
            var primitiveIndex = (int)methodType.GetField("PrimitiveIndex")!.GetValue(null)!;

            // Read bytecode array
            var bytecodeField = methodType.GetField("Bytecode");
            var bytecode = (byte[])bytecodeField!.GetValue(null)!;

            // Read literals array (if it exists)
            SObjectRef[] literals;
            var literalsField = methodType.GetField("Literals");
            if (literalsField != null)
            {
                var literalStrings = (string[])literalsField.GetValue(null)!;
                literals = new SObjectRef[literalStrings.Length];

                for (int i = 0; i < literalStrings.Length; i++)
                {
                    literals[i] = ParseLiteral(literalStrings[i]);
                }
            }
            else
            {
                literals = Array.Empty<SObjectRef>();
            }

            // Create the compiled method
            var methodOop = ObjectMemory.CreateCompiledMethod(
                selector,
                bytecode,
                literals,
                numArgs,
                numTemps,
                primitiveIndex
            );

            // Install the method in the class's method dictionary
            ObjectMemory.AddMethod(targetClassOop, selector, methodOop);

            if ((Program.RuntimeOptions?.EnableColdStartLogging ?? false) &&
                (selector == "hash" || selector == "=" || selector == "new"))
            {
                var side = isClassSide ? "class" : "instance";
                Console.WriteLine($"[ColdStartMethodInstaller] Installed {className}>>{selector} ({side} side, {bytecode.Length} bytes, {literals.Length} literals, primitive {primitiveIndex})");
            }
        }
        catch (Exception ex)
        {
            var side = isClassSide ? "class" : "instance";
            Console.Error.WriteLine($"[ColdStartMethodInstaller] ERROR installing {className} {side} method {methodType.Name}: {ex.Message}");
        }
    }

    /// <summary>
    /// Parses a literal string from ColdStartData into an SObjectRef.
    /// </summary>
    public static SObjectRef ParseLiteral(string literal)
    {
        // Symbol: starts with #
        if (literal.StartsWith("#"))
        {
            return ObjectMemory.InternSymbol(literal.Substring(1));
        }

        // SmallInteger: numeric value
        if (int.TryParse(literal, out int intValue))
        {
            return SObjectRef.FromSmallInteger(intValue);
        }

        // String: quoted
        if (literal.StartsWith("'") && literal.EndsWith("'"))
        {
            var stringValue = literal.Substring(1, literal.Length - 2);
            return ObjectMemory.CreateString(stringValue);
        }

        // Character: $X format
        if (literal.StartsWith("$") && literal.Length == 2)
        {
            var characterClass = Core.Objects.SystemDictionary.CharacterClassRef;
            return BlueBookFactory.CreateCharacter(characterClass, literal[1]);
        }

        // Array: [...] format
        if (literal.StartsWith("[") && literal.EndsWith("]"))
        {
            // Parse array elements
            var content = literal.Substring(1, literal.Length - 2);
            var arrayClass = Core.Objects.SystemDictionary.ArrayClassRef;

            if (string.IsNullOrWhiteSpace(content))
            {
                return BlueBookFactory.CreateArray(arrayClass, 0);
            }

            // Split by comma and recursively parse
            var elements = SplitArrayElements(content);
            var array = BlueBookFactory.CreateArray(arrayClass, elements.Length);
            for (int i = 0; i < elements.Length; i++)
            {
                array.SetIndexedPointer(i, ParseLiteral(elements[i].Trim()));
            }
            return array;
        }

        // Association: Association(key:X,value:Y) format
        if (literal.StartsWith("Association("))
        {
            var match = System.Text.RegularExpressions.Regex.Match(literal, @"Association\(key:([^,]+),value:(.+)\)");
            if (match.Success)
            {
                var associationClass = Core.Objects.SystemDictionary.AssociationClassRef;
                var keyLiteral = match.Groups[1].Value.Trim();
                var valueLiteral = match.Groups[2].Value.Trim();

                // CRITICAL: Association keys for global variables must be SYMBOLS, not the class objects themselves
                // If the key is a bare identifier (like "ActionMenu"), intern it as a symbol
                // Otherwise parse it normally (for symbols like "#foo" or other literal types)
                SObjectRef key;
                if (System.Text.RegularExpressions.Regex.IsMatch(keyLiteral, @"^[A-Z][a-zA-Z0-9]*$"))
                {
                    // Bare identifier - intern as symbol
                    key = ObjectMemory.InternSymbol(keyLiteral);
                }
                else
                {
                    // Parse normally (handles #symbol, strings, etc.)
                    key = ParseLiteral(keyLiteral);
                }

                var value = ParseLiteral(valueLiteral);
                return BlueBookFactory.CreateAssociation(associationClass, key, value);
            }
        }

        // Global variable: bare identifier (lookup in SystemDictionary)
        // This handles class names like "ActionMenu" or "Character"
        if (System.Text.RegularExpressions.Regex.IsMatch(literal, @"^[A-Z][a-zA-Z0-9]*$"))
        {
            var globalRef = Core.Objects.SystemDictionary.LookupGlobalRef(literal);
            if (globalRef.HasValue && !globalRef.Value.IsNil)
            {
                return globalRef.Value;
            }
            // Fall through to error if not found
        }

        // nil literal
        if (literal == "nil")
        {
            return SObjectRef.Nil;
        }

        // Unknown literal type - return nil
        Console.Error.WriteLine($"[ColdStartMethodInstaller] WARNING: Unknown literal format: {literal}");
        return SObjectRef.Nil;
    }

    /// <summary>
    /// Splits array elements by comma, respecting nested structures.
    /// </summary>
    private static string[] SplitArrayElements(string content)
    {
        var elements = new List<string>();
        var current = new System.Text.StringBuilder();
        int depth = 0;

        foreach (char c in content)
        {
            if (c == '[' || c == '(')
            {
                depth++;
                current.Append(c);
            }
            else if (c == ']' || c == ')')
            {
                depth--;
                current.Append(c);
            }
            else if (c == ',' && depth == 0)
            {
                elements.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
        {
            elements.Add(current.ToString());
        }

        return elements.ToArray();
    }
}
