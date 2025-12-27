using System.Text;
using System.Text.Json;
using Smalltalk80.Compiler.AST;

namespace Smalltalk80.Compiler.CodeGen;

/// <summary>
/// Generates complete cold start initialization data for Smalltalk classes.
/// This includes exact sizes for all collections, complete field initialization data,
/// pre-compiled bytecode, and everything needed to construct the cold start image
/// with zero temporary allocations.
/// </summary>
public sealed class ColdStartDataGenerator
{
    private readonly Dictionary<string, ClassInitializationData> _classData = new();
    private readonly Dictionary<string, ClassDefinitionNode> _classDefinitions = new();
    private readonly List<AST.InitializationExpression> _initExpressions = new();

    /// <summary>
    /// Represents all data needed to initialize a single Smalltalk class during cold start.
    /// </summary>
    public sealed class ClassInitializationData
    {
        public string ClassName { get; set; } = "";
        public string SuperclassName { get; set; } = "";
        public string SubclassKind { get; set; } = "";

        // Instance variable names (will become an Array in Smalltalk)
        public List<string> InstanceVariableNames { get; set; } = new();
        public List<string> AllInstanceVariableNames { get; set; } = new(); // Including inherited

        // Class variable names (will become elements in class pool Dictionary)
        public List<string> ClassVariableNames { get; set; } = new();

        // Pool dictionary names
        public List<string> PoolDictionaryNames { get; set; } = new();

        // Method dictionary sizing and contents
        public int MethodDictionarySize { get; set; } // Exact number of instance methods
        public List<MethodInitializationData> InstanceMethods { get; set; } = new();

        // Class method dictionary sizing and contents
        public int ClassMethodDictionarySize { get; set; } // Exact number of class methods
        public List<MethodInitializationData> ClassMethods { get; set; } = new();

        // Category (string)
        public string Category { get; set; } = "";

        // Comment (string or nil)
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Represents all data needed to initialize a single compiled method.
    /// </summary>
    public sealed class MethodInitializationData
    {
        public string Selector { get; set; } = "";
        public byte[] Bytecode { get; set; } = Array.Empty<byte>();
        public int BytecodeSize { get; set; } // Exact size needed for bytecode array

        public List<object> Literals { get; set; } = new(); // Exact literals in order
        public int LiteralsSize { get; set; } // Exact size needed for literals array

        public int NumArgs { get; set; }
        public int NumTemps { get; set; }
        public int StackDepth { get; set; }
        public int PrimitiveIndex { get; set; }

        public string SourceCode { get; set; } = "";
    }

    /// <summary>
    /// Analyzes the compilation result and generates complete initialization data.
    /// </summary>
    public void Analyze(CompilationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // First pass: build class definition map
        foreach (var classDef in result.Classes)
        {
            _classDefinitions[classDef.Name] = classDef;
        }

        // Second pass: analyze each class and generate initialization data
        foreach (var classDef in result.Classes)
        {
            AnalyzeClass(classDef);
        }

        // Third pass: store initialization expressions in file order
        _initExpressions.Clear();
        if (result.InitializationExpressions != null)
        {
            _initExpressions.AddRange(result.InitializationExpressions);
        }
    }

    /// <summary>
    /// Analyzes a single class definition and generates its initialization data.
    /// </summary>
    private void AnalyzeClass(ClassDefinitionNode classDef)
    {
        var data = new ClassInitializationData
        {
            ClassName = classDef.Name,
            SuperclassName = classDef.Superclass ?? "nil",
            SubclassKind = classDef.SubclassKind,
            InstanceVariableNames = new List<string>(classDef.InstanceVariables),
            AllInstanceVariableNames = ComputeAllInstanceVariables(classDef.Name),
            ClassVariableNames = new List<string>(classDef.ClassVariables),
            PoolDictionaryNames = new List<string>(classDef.PoolDictionaries),
            Category = classDef.Category,
            Comment = classDef.Comment
        };

        // Analyze instance methods
        data.MethodDictionarySize = classDef.Methods.Count;
        foreach (var method in classDef.Methods)
        {
            data.InstanceMethods.Add(AnalyzeMethod(method));
        }

        // Analyze class methods
        data.ClassMethodDictionarySize = classDef.ClassMethods.Count;
        foreach (var method in classDef.ClassMethods)
        {
            data.ClassMethods.Add(AnalyzeMethod(method));
        }

        _classData[classDef.Name] = data;
    }

    /// <summary>
    /// Analyzes a single method and generates its initialization data.
    /// </summary>
    private MethodInitializationData AnalyzeMethod(MethodDefinitionNode method)
    {
        return new MethodInitializationData
        {
            Selector = method.Selector,
            Bytecode = method.Bytecode.Select(b => (byte)b).ToArray(),
            BytecodeSize = method.Bytecode.Count,
            Literals = new List<object>(method.Literals),
            LiteralsSize = method.Literals.Count,
            NumArgs = method.NumArgs,
            NumTemps = method.NumTemps,
            StackDepth = method.StackDepth,
            PrimitiveIndex = method.PrimitiveIndex,
            SourceCode = method.Source ?? ""
        };
    }

    /// <summary>
    /// Computes all instance variables for a class including inherited ones.
    /// </summary>
    private List<string> ComputeAllInstanceVariables(string className)
    {
        var result = new List<string>();
        var visited = new HashSet<string>();
        var chain = new Stack<string>();

        // Walk up inheritance chain
        var current = className;
        while (!string.IsNullOrEmpty(current) &&
               current != "nil" &&
               _classDefinitions.TryGetValue(current, out var classDef))
        {
            if (!visited.Add(current))
                break; // Circular reference guard

            chain.Push(current);
            current = classDef.Superclass;
        }

        // Add variables from root to leaf
        while (chain.Count > 0)
        {
            var cls = chain.Pop();
            if (_classDefinitions.TryGetValue(cls, out var classDef))
            {
                result.AddRange(classDef.InstanceVariables);
            }
        }

        return result;
    }

    /// <summary>
    /// Generates a JSON file with complete cold start initialization data.
    /// </summary>
    public string GenerateJsonData()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var sortedClasses = _classData.Values.ToList();
        sortedClasses.Sort((a, b) => StringComparer.Ordinal.Compare(a.ClassName, b.ClassName));
        return JsonSerializer.Serialize(sortedClasses, options);
    }

    /// <summary>
    /// Generates a C# file with strongly-typed initialization data.
    /// This creates a complete specification that the C# cold start code can use
    /// to construct the exact Smalltalk objects with exact sizes.
    /// </summary>
    public string GenerateCSharpData()
    {
        var output = new StringBuilder();

        // File header
        output.AppendLine("// <auto-generated>");
        output.AppendLine("//     This code was generated by the Smalltalk80 Compiler.");
        output.AppendLine("//     Contains complete cold start initialization data for all classes.");
        output.AppendLine("//     This includes exact sizes and contents for all Smalltalk objects.");
        output.AppendLine("//     Changes to this file may cause incorrect behavior and will be lost if");
        output.AppendLine("//     the code is regenerated.");
        output.AppendLine("// </auto-generated>");
        output.AppendLine();
        output.AppendLine("using System.Collections.Generic;");
        output.AppendLine();
        output.AppendLine("namespace Smalltalk80.Interpreter.Generated;");
        output.AppendLine();
        output.AppendLine("/// <summary>");
        output.AppendLine("/// Complete cold start initialization data extracted from Smalltalk source.");
        output.AppendLine("/// Provides exact sizes and contents for all Smalltalk objects to eliminate");
        output.AppendLine("/// temporary allocations during cold start.");
        output.AppendLine("/// </summary>");
        output.AppendLine("public static class ColdStartData");
        output.AppendLine("{");

        // Generate class count
        output.AppendLine($"    /// <summary>");
        output.AppendLine($"    /// Total number of classes in cold start image.");
        output.AppendLine($"    /// </summary>");
        output.AppendLine($"    public const int ClassCount = {_classData.Count};");
        output.AppendLine();

        // Generate summary statistics
        GenerateSummaryStatistics(output);

        // Generate detailed class data
        GenerateClassDataStructures(output);

        // Generate initialization expressions
        GenerateInitializationExpressions(output);

        output.AppendLine("}");

        return output.ToString();
    }

    /// <summary>
    /// Generates summary statistics about the cold start image.
    /// </summary>
    private void GenerateSummaryStatistics(StringBuilder output)
    {
        int totalInstanceMethods = _classData.Values.Sum(c => c.MethodDictionarySize);
        int totalClassMethods = _classData.Values.Sum(c => c.ClassMethodDictionarySize);
        int totalMethods = totalInstanceMethods + totalClassMethods;
        int totalBytecodeSize = _classData.Values
            .SelectMany(c => c.InstanceMethods.Concat(c.ClassMethods))
            .Sum(m => m.BytecodeSize);
        int totalLiterals = _classData.Values
            .SelectMany(c => c.InstanceMethods.Concat(c.ClassMethods))
            .Sum(m => m.LiteralsSize);

        output.AppendLine("    /// <summary>");
        output.AppendLine("    /// Cold start image statistics.");
        output.AppendLine("    /// </summary>");
        output.AppendLine("    public static class Statistics");
        output.AppendLine("    {");
        output.AppendLine("        /// <summary>Total number of classes in the cold start image.</summary>");
        output.AppendLine($"        public const int TotalClasses = {_classData.Count};");
        output.AppendLine("        /// <summary>Total number of instance methods across all classes.</summary>");
        output.AppendLine($"        public const int TotalInstanceMethods = {totalInstanceMethods};");
        output.AppendLine("        /// <summary>Total number of class methods across all classes.</summary>");
        output.AppendLine($"        public const int TotalClassMethods = {totalClassMethods};");
        output.AppendLine("        /// <summary>Total number of methods (instance and class) across all classes.</summary>");
        output.AppendLine($"        public const int TotalMethods = {totalMethods};");
        output.AppendLine("        /// <summary>Total size of all bytecode arrays in bytes.</summary>");
        output.AppendLine($"        public const int TotalBytecodeSize = {totalBytecodeSize};");
        output.AppendLine("        /// <summary>Total number of literal objects across all methods.</summary>");
        output.AppendLine($"        public const int TotalLiterals = {totalLiterals};");
        output.AppendLine("    }");
        output.AppendLine();
    }

    /// <summary>
    /// Generates the class data structures with all initialization information.
    /// </summary>
    private void GenerateClassDataStructures(StringBuilder output)
    {
        output.AppendLine("    /// <summary>");
        output.AppendLine("    /// Class initialization data indexed by class name.");
        output.AppendLine("    /// </summary>");
        output.AppendLine("    public static class Classes");
        output.AppendLine("    {");

        foreach (var (className, data) in _classData.OrderBy(kvp => kvp.Key))
        {
            GenerateClassData(output, className, data);
        }

        output.AppendLine("    }");
    }

    /// <summary>
    /// Generates initialization data for a single class.
    /// </summary>
    private void GenerateClassData(StringBuilder output, string className, ClassInitializationData data)
    {
        var safeName = MakeSafeIdentifier(className);

        output.AppendLine($"        /// <summary>");
        output.AppendLine($"        /// Initialization data for {EscapeXmlComment(className)} class.");
        output.AppendLine($"        /// Instance methods: {data.MethodDictionarySize}");
        output.AppendLine($"        /// Class methods: {data.ClassMethodDictionarySize}");
        output.AppendLine($"        /// </summary>");
        output.AppendLine($"        public static class {safeName}");
        output.AppendLine("        {");

        // Basic class information
        output.AppendLine($"            /// <summary>The name of this Smalltalk class.</summary>");
        output.AppendLine($"            public const string ClassName = \"{EscapeCSharpString(className)}\";");
        output.AppendLine($"            /// <summary>The name of the superclass.</summary>");
        output.AppendLine($"            public const string SuperclassName = \"{EscapeCSharpString(data.SuperclassName)}\";");
        output.AppendLine($"            /// <summary>The subclass kind (normal, variable, words, bytes).</summary>");
        output.AppendLine($"            public const string SubclassKind = \"{EscapeCSharpString(data.SubclassKind)}\";");
        output.AppendLine($"            /// <summary>The class category for organization.</summary>");
        output.AppendLine($"            public const string Category = \"{EscapeCSharpString(data.Category)}\";");
        output.AppendLine();

        // Instance variable names
        output.AppendLine($"            /// <summary>Instance variables defined by this class (count: {data.InstanceVariableNames.Count}).</summary>");
        output.AppendLine($"            public static readonly string[] InstanceVariableNames = {{");
        foreach (var varName in data.InstanceVariableNames)
        {
            output.AppendLine($"                \"{EscapeCSharpString(varName)}\",");
        }
        output.AppendLine("            };");
        output.AppendLine();

        // All instance variable names (including inherited)
        output.AppendLine($"            /// <summary>All instance variables including inherited (count: {data.AllInstanceVariableNames.Count}).</summary>");
        output.AppendLine($"            public static readonly string[] AllInstanceVariableNames = {{");
        foreach (var varName in data.AllInstanceVariableNames)
        {
            output.AppendLine($"                \"{EscapeCSharpString(varName)}\",");
        }
        output.AppendLine("            };");
        output.AppendLine();

        // Class variable names
        output.AppendLine($"            /// <summary>Class variables (count: {data.ClassVariableNames.Count}).</summary>");
        output.AppendLine($"            public static readonly string[] ClassVariableNames = {{");
        foreach (var varName in data.ClassVariableNames)
        {
            output.AppendLine($"                \"{EscapeCSharpString(varName)}\",");
        }
        output.AppendLine("            };");
        output.AppendLine();

        // Method dictionary size
        output.AppendLine($"            /// <summary>Exact size needed for instance method dictionary.</summary>");
        output.AppendLine($"            public const int MethodDictionarySize = {data.MethodDictionarySize};");
        output.AppendLine();
        output.AppendLine($"            /// <summary>Exact size needed for class method dictionary.</summary>");
        output.AppendLine($"            public const int ClassMethodDictionarySize = {data.ClassMethodDictionarySize};");
        output.AppendLine();

        // Instance methods
        if (data.InstanceMethods.Count > 0)
        {
            output.AppendLine($"            /// <summary>Instance methods with complete initialization data.</summary>");
            output.AppendLine($"            public static class InstanceMethods");
            output.AppendLine("            {");

            foreach (var method in data.InstanceMethods)
            {
                GenerateMethodData(output, method, "                ");
            }

            output.AppendLine("            }");
            output.AppendLine();
        }

        // Class methods
        if (data.ClassMethods.Count > 0)
        {
            output.AppendLine($"            /// <summary>Class methods with complete initialization data.</summary>");
            output.AppendLine($"            public static class ClassMethods");
            output.AppendLine("            {");

            foreach (var method in data.ClassMethods)
            {
                GenerateMethodData(output, method, "                ");
            }

            output.AppendLine("            }");
            output.AppendLine();
        }

        output.AppendLine("        }");
        output.AppendLine();
    }

    /// <summary>
    /// Generates initialization data for a single method.
    /// </summary>
    private void GenerateMethodData(StringBuilder output, MethodInitializationData method, string indent)
    {
        var safeName = MakeSafeIdentifier(method.Selector);

        output.AppendLine($"{indent}/// <summary>");
        output.AppendLine($"{indent}/// Method: {EscapeXmlComment(method.Selector)}");
        output.AppendLine($"{indent}/// Bytecode size: {method.BytecodeSize}, Literals: {method.LiteralsSize}");
        output.AppendLine($"{indent}/// Args: {method.NumArgs}, Temps: {method.NumTemps}, Stack: {method.StackDepth}");
        if (method.PrimitiveIndex > 0)
        {
            output.AppendLine($"{indent}/// Primitive: {method.PrimitiveIndex}");
        }
        output.AppendLine($"{indent}/// </summary>");
        output.AppendLine($"{indent}public static class {safeName}");
        output.AppendLine($"{indent}{{");

        output.AppendLine($"{indent}    /// <summary>The Smalltalk selector for this method.</summary>");
        output.AppendLine($"{indent}    public const string Selector = \"{EscapeCSharpString(method.Selector)}\";");
        output.AppendLine($"{indent}    /// <summary>Number of arguments this method expects.</summary>");
        output.AppendLine($"{indent}    public const int NumArgs = {method.NumArgs};");
        output.AppendLine($"{indent}    /// <summary>Number of temporary variables in this method.</summary>");
        output.AppendLine($"{indent}    public const int NumTemps = {method.NumTemps};");
        output.AppendLine($"{indent}    /// <summary>Maximum stack depth needed during method execution.</summary>");
        output.AppendLine($"{indent}    public const int StackDepth = {method.StackDepth};");
        output.AppendLine($"{indent}    /// <summary>Primitive index (0 if no primitive).</summary>");
        output.AppendLine($"{indent}    public const int PrimitiveIndex = {method.PrimitiveIndex};");
        output.AppendLine();

        // Bytecode array
        output.AppendLine($"{indent}    /// <summary>Exact bytecode array (size: {method.BytecodeSize}).</summary>");
        output.AppendLine($"{indent}    public static readonly byte[] Bytecode = {{");

        if (method.Bytecode.Length > 0)
        {
            output.Append($"{indent}        ");
            for (int i = 0; i < method.Bytecode.Length; i++)
            {
                output.Append($"0x{method.Bytecode[i]:X2}");
                if (i < method.Bytecode.Length - 1)
                {
                    output.Append(", ");
                    if ((i + 1) % 16 == 0)
                    {
                        output.AppendLine();
                        output.Append($"{indent}        ");
                    }
                }
            }
            output.AppendLine();
        }

        output.AppendLine($"{indent}    }};");
        output.AppendLine();

        // Literals array (as JSON for now - will need specialized handling)
        output.AppendLine($"{indent}    /// <summary>Exact literals array (size: {method.LiteralsSize}).</summary>");
        output.AppendLine($"{indent}    public static readonly string[] LiteralsJson = {{");
        foreach (var literal in method.Literals)
        {
            var literalJson = JsonSerializer.Serialize(literal);
            output.AppendLine($"{indent}        {EscapeCSharpStringLiteral(literalJson)},");
        }
        output.AppendLine($"{indent}    }};");

        output.AppendLine($"{indent}}}");
        output.AppendLine();
    }

    /// <summary>
    /// C# keywords that need to be prefixed with @ if used as identifiers.
    /// Includes C# 13 contextual keywords like 'file'.
    /// </summary>
    private static readonly HashSet<string> CSharpKeywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "file", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    /// <summary>
    /// Makes a valid C# identifier from a Smalltalk selector or class name.
    /// </summary>
    private static string MakeSafeIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name))
            return "Empty";

        var sb = new StringBuilder();

        // Handle leading characters
        char first = name[0];
        if (char.IsDigit(first))
        {
            sb.Append('_');
        }

        foreach (char c in name)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else
            {
                // Map special characters to names
                sb.Append(c switch
                {
                    ':' => "_colon_",
                    '+' => "_plus_",
                    '-' => "_minus_",
                    '*' => "_star_",
                    '/' => "_slash_",
                    '\\' => "_backslash_",
                    '=' => "_equals_",
                    '<' => "_less_",
                    '>' => "_greater_",
                    '@' => "_at_",
                    '~' => "_tilde_",
                    '&' => "_and_",
                    '|' => "_or_",
                    ',' => "_comma_",
                    '?' => "_question_",
                    '!' => "_bang_",
                    '%' => "_percent_",
                    _ => $"_x{(int)c:X2}_"
                });
            }
        }

        var result = sb.ToString().TrimEnd('_');
        if (string.IsNullOrEmpty(result))
            result = "Empty";

        // If the result is a C# keyword, prefix with @
        if (CSharpKeywords.Contains(result))
            result = "@" + result;

        return result;
    }

    /// <summary>
    /// Escapes a string for use in C# source code.
    /// </summary>
    private static string EscapeCSharpString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Escapes a string for use as a C# string literal (includes quotes).
    /// </summary>
    private static string EscapeCSharpStringLiteral(string input)
    {
        return "\"" + EscapeCSharpString(input) + "\"";
    }

    /// <summary>
    /// Escapes a string for use in XML comments.
    /// Truncates long strings and removes newlines since XML comments must be single-line.
    /// </summary>
    private static string EscapeXmlComment(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        // Replace newlines with spaces
        var cleaned = input
            .Replace("\r\n", " ")
            .Replace("\n", " ")
            .Replace("\r", " ");

        // Truncate if too long (over 200 characters)
        if (cleaned.Length > 200)
        {
            cleaned = cleaned.Substring(0, 197) + "...";
        }

        return cleaned
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }

    /// <summary>
    /// Gets statistics about the analyzed cold start data.
    /// </summary>
    public (int ClassCount, int MethodCount, int TotalBytecodeSize, int TotalLiterals) GetStatistics()
    {
        int classCount = _classData.Count;
        int methodCount = _classData.Values.Sum(c => c.MethodDictionarySize + c.ClassMethodDictionarySize);
        int bytecodeSize = _classData.Values
            .SelectMany(c => c.InstanceMethods.Concat(c.ClassMethods))
            .Sum(m => m.BytecodeSize);
        int literals = _classData.Values
            .SelectMany(c => c.InstanceMethods.Concat(c.ClassMethods))
            .Sum(m => m.LiteralsSize);

        return (classCount, methodCount, bytecodeSize, literals);
    }

    /// <summary>
    /// Generates C# code for initialization expressions that should be executed after cold start.
    /// These expressions are executed in file order and typically initialize class-side state.
    /// </summary>
    private void GenerateInitializationExpressions(StringBuilder output)
    {
        output.AppendLine("    /// <summary>");
        output.AppendLine("    /// Initialization expressions to execute after cold start completes.");
        output.AppendLine($"    /// Total expressions: {_initExpressions.Count}");
        output.AppendLine("    /// These are executed in file order to initialize class-side state.");
        output.AppendLine("    /// </summary>");
        output.AppendLine("    public static class InitializationExpressions");
        output.AppendLine("    {");
        output.AppendLine($"        /// <summary>Total number of initialization expressions.</summary>");
        output.AppendLine($"        public const int Count = {_initExpressions.Count};");
        output.AppendLine();

        // Generate array of source text for display/debugging
        output.AppendLine("        /// <summary>Source text of each initialization expression for display.</summary>");
        output.AppendLine("        public static readonly string[] Sources = new string[]");
        output.AppendLine("        {");
        foreach (var expr in _initExpressions)
        {
            output.AppendLine($"            \"{EscapeCSharpString(expr.Source)}\",");
        }
        output.AppendLine("        };");
        output.AppendLine();

        // Generate each expression's data
        for (int i = 0; i < _initExpressions.Count; i++)
        {
            var expr = _initExpressions[i];
            output.AppendLine($"        /// <summary>");
            output.AppendLine($"        /// Expression {i}: {EscapeXmlComment(expr.Source)}");
            output.AppendLine($"        /// </summary>");
            output.AppendLine($"        public static class Expr{i}");
            output.AppendLine("        {");

            // Source
            output.AppendLine($"            public const string Source = \"{EscapeCSharpString(expr.Source)}\";");

            // Metadata
            output.AppendLine($"            public const int NumTemps = {expr.NumTemps};");
            output.AppendLine($"            public const int StackDepth = {expr.StackDepth};");

            // Bytecode array
            output.AppendLine("            public static readonly byte[] Bytecode = new byte[]");
            output.AppendLine("            {");
            output.Append("                ");
            for (int j = 0; j < expr.Bytecode.Count; j++)
            {
                output.Append($"{expr.Bytecode[j]}");
                if (j < expr.Bytecode.Count - 1)
                {
                    output.Append(", ");
                    if ((j + 1) % 16 == 0)
                    {
                        output.AppendLine();
                        output.Append("                ");
                    }
                }
            }
            output.AppendLine();
            output.AppendLine("            };");

            // Literals array (as JSON)
            output.AppendLine($"            public static readonly string[] LiteralsJson = new string[]");
            output.AppendLine("            {");
            foreach (var literal in expr.Literals)
            {
                var literalJson = JsonSerializer.Serialize(literal);
                output.AppendLine($"                {EscapeCSharpStringLiteral(literalJson)},");
            }
            output.AppendLine("            };");

            output.AppendLine("        }");
            output.AppendLine();
        }

        output.AppendLine("    }");
        output.AppendLine();
    }
}
