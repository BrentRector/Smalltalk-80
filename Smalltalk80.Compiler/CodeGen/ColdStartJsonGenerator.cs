using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Compiler;
using Smalltalk80.Compiler.AST;

namespace Smalltalk80.Compiler.CodeGen;

/// <summary>
/// Generates a JSON cold-start bundle from a compiled Smalltalk AST.
/// Converts the ANTLR4 AST format to the schema-compliant cold-start format.
/// </summary>
public sealed class ColdStartJsonGenerator(string sourceFileDescription)
{
    private readonly string _sourceFileDescription = sourceFileDescription;
    private readonly Dictionary<string, int> _symbolTable = new();
    private readonly List<string> _symbols = [];
    private readonly Dictionary<string, int> _stringTable = new();
    private readonly List<string> _strings = [];
    private readonly List<JsonClass> _classes = [];
    private readonly List<JsonMethod> _methods = [];
    private readonly List<JsonInitExpr> _initExprs = [];
    private readonly List<JsonObject> _objects = [];
    private readonly List<PatchDescriptor> _patches = [];
    private readonly List<InstallPhase> _installPhases = [];
    private readonly Dictionary<string, int> _selectorFrequency = new();
    private readonly Dictionary<string, string> _blockToHomeMethod = new(); // Maps block localId → home method localId
    private string? _currentMethodLocalId; // Tracks the current method being processed

    /// <summary>
    /// Block buffer for post-order emission.
    ///
    /// WHY: The loader can only resolve block references in a single pass if blocks appear AFTER
    /// their containing methods in the bundle (forward references). Previously, blocks were emitted
    /// immediately when encountered during literal conversion, causing them to appear BEFORE their
    /// parents (backward references), requiring a two-pass loader.
    ///
    /// HOW: When processing a method's literals, any blocks encountered are buffered here instead
    /// of being immediately added to _methods. After the parent method is added to _methods, all
    /// buffered blocks are added in post-order (parent first, then blocks).
    ///
    /// ACTIVATION: Set to non-null when processing a top-level method, null otherwise.
    /// - Non-null: Buffer blocks for later emission
    /// - Null: Emit blocks immediately (fallback for init expressions, etc.)
    /// </summary>
    private List<JsonMethod>? _blockBuffer;

    /// <summary>
    /// Generates the complete cold-start JSON bundle from the AST.
    /// </summary>
    /// <param name="ast">The compiled AST from the Smalltalk parser.</param>
    /// <returns>The serialized JSON string.</returns>
    public string Generate(CompilationResult ast)
    {
        // Phase 1: Build symbol and string tables
        BuildSymbolTable(ast);
        BuildStringTable(ast);

        // Phase 2: Identify optimized selectors (most frequent)
        var optimizedSelectors = IdentifyOptimizedSelectors(ast);

        // Phase 3: Convert classes (both instance and metaclass)
        ConvertClasses(ast);

        // Phase 4: Convert methods
        ConvertMethods(ast);

        // Phase 4.5: BlockContext metadata is now generated in GenerateMetadata()
        // (no longer added to objects[] array)

        // Phase 5: Convert init expressions
        ConvertInitExpressions(ast);

        // Phase 6: Generate patches for forward references
        GeneratePatches(ast);

        // Phase 7: Compute install order
        ComputeInstallOrder();

        // Phase 8: Build final JSON bundle
        var bundle = BuildColdStartBundle(optimizedSelectors);

        // Serialize with proper formatting
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(bundle, options);
    }

    /// <summary>
    /// Gets statistics about the generated cold-start bundle.
    /// </summary>
    public (int SymbolCount, int StringCount, int ClassCount, int MethodCount, int InitExpressionCount) GetStatistics()
    {
        return (_symbols.Count, _strings.Count, _classes.Count, _methods.Count, _initExprs.Count);
    }

    /// <summary>
    /// Builds the symbol table from the AST, extracting all unique symbols.
    /// </summary>
    private void BuildSymbolTable(CompilationResult ast)
    {
        var symbols = new HashSet<string>();

        // Extract from class definitions
        foreach (var cls in ast.Classes)
        {
            symbols.Add(cls.Name);
            if (!string.IsNullOrEmpty(cls.Superclass))
                symbols.Add(cls.Superclass);

            symbols.UnionWith(cls.InstanceVariables);
            symbols.UnionWith(cls.ClassVariables);
            symbols.UnionWith(cls.PoolDictionaries);

            // Extract from methods
            foreach (var method in cls.Methods.Concat(cls.ClassMethods))
            {
                symbols.Add(method.Selector);
                ExtractSymbolsFromLiterals(method.Literals, symbols);
            }
        }

        // Extract from initialization expressions
        foreach (var initExpr in ast.InitializationExpressions)
        {
            ExtractSymbolsFromLiterals(initExpr.Literals, symbols);
        }

        // Sort symbols for deterministic output
        var sortedSymbols = symbols.OrderBy(s => s).ToList();

        // Build the symbol table with indices
        for (int i = 0; i < sortedSymbols.Count; i++)
        {
            _symbols.Add(sortedSymbols[i]);
            _symbolTable[sortedSymbols[i]] = i;
        }
    }

    /// <summary>
    /// Extracts symbols from a literal collection.
    /// </summary>
    private void ExtractSymbolsFromLiterals(List<object> literals, HashSet<string> symbols)
    {
        foreach (var literal in literals)
        {
            switch (literal)
            {
                case Symbol symbol:
                    // Symbol object (added by LiteralCollector for selectors)
                    symbols.Add(symbol.Value);
                    break;

                case Association assoc:
                    // Association object - extract the key
                    if (assoc.Key is Symbol keySymbol)
                    {
                        symbols.Add(keySymbol.Value);
                    }
                    break;

                case CompiledMethodData block when block.IsBlock:
                    // Recursively extract symbols from block literals
                    if (block.Literals != null && block.Literals.Length > 0)
                    {
                        ExtractSymbolsFromLiterals(block.Literals.ToList(), symbols);
                    }
                    break;

                case string str when str.StartsWith("#"):
                    // Symbol literal
                    symbols.Add(str[1..]);
                    break;

                case string str when str.StartsWith("Association("):
                    // Association literal - extract the key
                    var keyStart = str.IndexOf("key:") + 4;
                    var keyEnd = str.IndexOf(',', keyStart);
                    if (keyEnd > keyStart)
                    {
                        var key = str[keyStart..keyEnd];
                        symbols.Add(key);
                    }
                    break;

                case List<object> array:
                    ExtractSymbolsFromLiterals(array, symbols);
                    break;

                case object[] objArray:
                    ExtractSymbolsFromLiterals(objArray.ToList(), symbols);
                    break;
            }
        }
    }

    /// <summary>
    /// Builds the string table from the AST, extracting all unique strings.
    /// </summary>
    private void BuildStringTable(CompilationResult ast)
    {
        var strings = new List<string>();

        // Extract from class comments
        foreach (var cls in ast.Classes)
        {
            if (!string.IsNullOrEmpty(cls.Comment))
            {
                strings.Add(cls.Comment);
            }

            // Extract from method sources (optional - can be large)
            foreach (var method in cls.Methods.Concat(cls.ClassMethods))
            {
                if (!string.IsNullOrEmpty(method.Source))
                {
                    strings.Add(method.Source);
                }

                ExtractStringsFromLiterals(method.Literals, strings);
            }
        }

        // Extract from initialization expressions
        foreach (var initExpr in ast.InitializationExpressions)
        {
            if (!string.IsNullOrEmpty(initExpr.Source))
            {
                strings.Add(initExpr.Source);
            }

            ExtractStringsFromLiterals(initExpr.Literals, strings);
        }

        // Build string table (strings can have duplicates, unlike symbols)
        for (int i = 0; i < strings.Count; i++)
        {
            _strings.Add(strings[i]);
            if (!_stringTable.ContainsKey(strings[i]))
            {
                _stringTable[strings[i]] = i;
            }
        }
    }

    /// <summary>
    /// Extracts strings from a literal collection.
    /// </summary>
    private void ExtractStringsFromLiterals(List<object> literals, List<string> strings)
    {
        foreach (var literal in literals)
        {
            switch (literal)
            {
                case string str when !str.StartsWith("#") && !str.StartsWith("Association("):
                    // Regular string literal
                    strings.Add(str);
                    break;

                case List<object> array:
                    ExtractStringsFromLiterals(array, strings);
                    break;

                case CompiledMethodData block:
                    // Blocks are CompiledMethodData objects stored as literals
                    // Recursively extract strings from block literals
                    if (block.Literals != null && block.Literals.Length > 0)
                    {
                        ExtractStringsFromLiterals(block.Literals.ToList(), strings);
                    }
                    break;
            }
        }
    }

    /// <summary>
    /// Identifies the most frequently used selectors for optimization.
    /// </summary>
    private List<int> IdentifyOptimizedSelectors(CompilationResult ast)
    {
        // Count selector usage frequency
        foreach (var cls in ast.Classes)
        {
            foreach (var method in cls.Methods.Concat(cls.ClassMethods))
            {
                CountSelectorUsageInLiterals(method.Literals);
            }
        }

        foreach (var initExpr in ast.InitializationExpressions)
        {
            CountSelectorUsageInLiterals(initExpr.Literals);
        }

        // Get top 32 most frequent selectors
        var topSelectors = _selectorFrequency
            .OrderByDescending(kvp => kvp.Value)
            .Take(32)
            .Select(kvp => _symbolTable.GetValueOrDefault(kvp.Key, -1))
            .Where(idx => idx >= 0)
            .ToList();

        return topSelectors;
    }

    /// <summary>
    /// Counts selector usage in literals for frequency analysis.
    /// </summary>
    private void CountSelectorUsageInLiterals(List<object> literals)
    {
        foreach (var literal in literals)
        {
            if (literal is string str && str.StartsWith("#"))
            {
                var symbol = str[1..];
                // Check if it looks like a selector (contains : or is a known message pattern)
                if (symbol.Contains(':') || IsCommonSelector(symbol))
                {
                    _selectorFrequency[symbol] = _selectorFrequency.GetValueOrDefault(symbol, 0) + 1;
                }
            }
            else if (literal is List<object> array)
            {
                CountSelectorUsageInLiterals(array);
            }
        }
    }

    /// <summary>
    /// Checks if a symbol is a common selector pattern.
    /// </summary>
    private static bool IsCommonSelector(string symbol)
    {
        // Common unary selectors
        var commonSelectors = new HashSet<string>
        {
            "new", "class", "size", "copy", "value", "yourself",
            "isNil", "notNil", "ifTrue", "ifFalse", "not",
            "first", "last", "isEmpty", "print", "printString"
        };

        return commonSelectors.Contains(symbol);
    }

    /// <summary>
    /// Converts classes from the AST to the cold-start format.
    /// </summary>
    private void ConvertClasses(CompilationResult ast)
    {
        foreach (var classNode in ast.Classes)
        {
            // Check for class>>initialize method
            var initMethod = classNode.ClassMethods.FirstOrDefault(m => m.Selector == "initialize");
            var classSideInitMethodLocalId = initMethod != null
                ? $"method:{classNode.Name}_class>>initialize"
                : null;

            // Generate instance class
            // v1.0.6+: Compute precomputed field layout (eliminates runtime topological sort)
            var instanceLayout = ComputeFieldLayout(classNode.Name, ast);

            var instanceClass = new JsonClass
            {
                LocalId = $"class:{classNode.Name}",
                NameIndex = _symbolTable[classNode.Name],
                IsMetaclass = false,
                MetaclassOfLocalId = null,
                Format = CalculateFormatWord(classNode.SubclassKind, classNode.TotalInstanceVariableCount),
                InstVarNames = classNode.InstanceVariables.Select(v => _symbolTable[v]).ToList(),
                ClassVarNames = classNode.ClassVariables.Select(v => _symbolTable[v]).ToList(),
                PoolLocalIds = classNode.PoolDictionaries.Select(p => $"pool:{p}").ToList(),
                SuperNameIndex = string.IsNullOrEmpty(classNode.Superclass) || classNode.Superclass == "nil"
                    ? null
                    : _symbolTable.GetValueOrDefault(classNode.Superclass, -1),
                Category = classNode.Category ?? "",
                CommentStringIndex = string.IsNullOrEmpty(classNode.Comment)
                    ? null
                    : _stringTable.GetValueOrDefault(classNode.Comment, -1),
                ClassSideInitMethodLocalId = classSideInitMethodLocalId,

                // v1.0.6+: Precomputed layout
                TotalInstanceVarCount = instanceLayout.Count,
                FieldLayout = instanceLayout
            };

            _classes.Add(instanceClass);

            // Generate metaclass
            // v1.0.6+: Compute precomputed layout for metaclass
            // All metaclasses have the same layout: Behavior → ClassDescription → Class → Metaclass fields
            var metaclassLayout = ComputeMetaclassFieldLayout(ast);
            var metaclassFormat = CalculateFormatWord("subclass:", metaclassLayout.Count);

            var metaclass = new JsonClass
            {
                LocalId = $"meta:{classNode.Name}",
                NameIndex = _symbolTable[classNode.Name],
                IsMetaclass = true,
                MetaclassOfLocalId = $"class:{classNode.Name}",
                Format = metaclassFormat,
                InstVarNames = [], // Metaclasses don't declare additional instance variables beyond Metaclass
                ClassVarNames = [], // Class variables belong to the instance side
                PoolLocalIds = [],
                SuperNameIndex = string.IsNullOrEmpty(classNode.Superclass) || classNode.Superclass == "nil"
                    ? null
                    : _symbolTable.GetValueOrDefault(classNode.Superclass, -1),
                Category = classNode.Category ?? "",
                CommentStringIndex = null,
                ClassSideInitMethodLocalId = null,

                // v1.0.6+: Precomputed layout for metaclass
                TotalInstanceVarCount = metaclassLayout.Count,
                FieldLayout = metaclassLayout
            };

            _classes.Add(metaclass);

            // Create pool dictionary objects if needed
            foreach (var poolName in classNode.PoolDictionaries)
            {
                if (!_objects.Any(o => o.LocalId == $"pool:{poolName}"))
                {
                    _objects.Add(new JsonObject
                    {
                        LocalId = $"pool:{poolName}",
                        ClassNameIndex = _symbolTable.GetValueOrDefault("Dictionary", -1),
                        FormatHint = "pointers",
                        Fields =
                        [
                            // Field 0: tally (inherited from Set) - initialized to 0
                            new Dictionary<string, object>
                            {
                                ["type"] = "smallInt",
                                ["value"] = 0
                            }
                        ],
                        IndexedFields = []
                    });
                }
            }
        }
    }

    /// <summary>
    /// Calculates the format word for a class.
    /// Per Blue Book Chapter 16 and Interpreter-Architecture.md:
    /// Bits 15-14: Unused (00)
    /// Bits 13-12: Instance Specification (00=no fields, 01=pointers, 10=words, 11=bytes)
    /// Bits 11-6:  Fixed fields count (0-63) - INCLUDES ALL INHERITED INSTANCE VARIABLES
    /// Bit 5:      Unused
    /// Bit 4:      Indexable flag (1 = has indexed portion)
    /// Bit 3:      Pointer flag (1 = indexed portion contains pointers)
    /// Bits 2-0:   Unused
    /// </summary>
    private static int CalculateFormatWord(string subclassKind, int fixedFieldCount)
    {
        int format = 0;

        // Instance specification (bits 13-12)
        int instSpec = subclassKind switch
        {
            "subclass:" => 0x01,                 // 01 = only pointer fields
            "variableSubclass:" => 0x02,         // 10 = word-indexable (with pointers)
            "variableByteSubclass:" => 0x03,     // 11 = byte-indexable
            "variableWordSubclass:" => 0x02,     // 10 = word-indexable (no pointers)
            _ => 0x01                            // Default to pointer fields
        };
        format |= (instSpec << 12);

        // Fixed fields count (bits 11-6)
        format |= ((fixedFieldCount & 0x3F) << 6);

        // Indexable flag (bit 4)
        if (subclassKind.StartsWith("variable"))
            format |= 0x10;

        // Pointer flag (bit 3) - set for subclass: and variableSubclass:
        if (subclassKind == "subclass:" || subclassKind == "variableSubclass:")
            format |= 0x08;

        return format;
    }

    /// <summary>
    /// v1.0.6+: Computes the complete field layout for a class including inherited fields.
    /// Returns fields in the correct slot order: inherited fields first, then own fields.
    /// </summary>
    private List<JsonFieldLayoutEntry> ComputeFieldLayout(string className, CompilationResult ast)
    {
        var layout = new List<JsonFieldLayoutEntry>();
        var classChain = new List<ClassDefinitionNode>();

        // Build inheritance chain (bottom-up: subclass → superclass)
        var currentClassName = className;
        while (!string.IsNullOrEmpty(currentClassName) && currentClassName != "nil")
        {
            var classNode = ast.Classes.FirstOrDefault(c => c.Name == currentClassName);
            if (classNode == null)
                break;

            classChain.Add(classNode);
            currentClassName = classNode.Superclass;
        }

        // Process in reverse order (superclass → subclass) to get correct slot indices
        int slotIndex = 0;
        for (int i = classChain.Count - 1; i >= 0; i--)
        {
            var cls = classChain[i];
            // inheritedFrom is null for the target class (i=0), non-null for inherited fields
            string? inheritedFrom = (i > 0) ? $"class:{cls.Name}" : null;

            // SPECIAL CASE: Blue Book architectural constants for VM-critical classes
            // These field indices are fixed by the Smalltalk-80 specification and cannot change

            if (cls.Name == "Behavior")
            {
                // Blue Book Chapter 27: SuperclassIndex := 0. MessageDictionaryIndex := 1. InstanceSpecificationIndex := 2.
                var requiredFields = new[] { "superclass", "methodDict", "format" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: Behavior class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The Blue Book VM architecture requires Behavior to define 'superclass', 'methodDict', and 'format' " +
                        "at fixed indices 0, 1, and 2 respectively.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["superclass"], SlotIndex = 0, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["methodDict"], SlotIndex = 1, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["format"], SlotIndex = 2, InheritedFrom = inheritedFrom });

                slotIndex = 3;
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else if (cls.Name == "InstructionStream")
            {
                // Blue Book Chapter 27: SenderIndex := 0. InstructionPointerIndex := 1.
                var requiredFields = new[] { "sender", "pc" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: InstructionStream class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The Blue Book VM architecture requires InstructionStream to define 'sender' and 'pc' at fixed indices 0 and 1.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["sender"], SlotIndex = 0, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["pc"], SlotIndex = 1, InheritedFrom = inheritedFrom });

                slotIndex = 2;
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else if (cls.Name == "ContextPart")
            {
                // Blue Book Chapter 27: StackPointerIndex := 2.
                // ContextPart inherits sender(0) and pc(1) from InstructionStream
                var requiredFields = new[] { "stackp" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: ContextPart class is missing required field 'stackp'. " +
                        "The Blue Book VM architecture requires ContextPart to define 'stackp' at fixed index 2.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["stackp"], SlotIndex = 2, InheritedFrom = inheritedFrom });

                slotIndex = 3;
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else if (cls.Name == "MethodContext")
            {
                // Blue Book Chapter 27: MethodIndex := 3. ReceiverIndex := 5.
                // MethodContext inherits sender(0), pc(1), stackp(2) from ContextPart
                var requiredFields = new[] { "method", "receiverMap", "receiver" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: MethodContext class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The Blue Book VM architecture requires MethodContext to define 'method', 'receiverMap', and 'receiver' at fixed indices 3, 4, and 5.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["method"], SlotIndex = 3, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["receiverMap"], SlotIndex = 4, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["receiver"], SlotIndex = 5, InheritedFrom = inheritedFrom });

                slotIndex = 6; // TempFrameStart
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else if (cls.Name == "BlockContext")
            {
                // Blue Book Chapter 27: BlockArgumentCountIndex := 3. InitialIPIndex := 4. HomeIndex := 5.
                // BlockContext inherits sender(0), pc(1), stackp(2) from ContextPart
                var requiredFields = new[] { "nargs", "startpc", "home" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: BlockContext class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The Blue Book VM architecture requires BlockContext to define 'nargs', 'startpc', and 'home' at fixed indices 3, 4, and 5.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["nargs"], SlotIndex = 3, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["startpc"], SlotIndex = 4, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["home"], SlotIndex = 5, InheritedFrom = inheritedFrom });

                slotIndex = 6; // TempFrameStart
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else if (cls.Name == "BlockClosure")
            {
                // VM Extension: BlockClosure field indices are fixed by our VM implementation
                var requiredFields = new[] { "method", "homeContext", "returnContext", "numArgs" };
                var missingFields = requiredFields.Where(f => !cls.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: BlockClosure class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The VM extension requires BlockClosure to define 'method', 'homeContext', 'returnContext', and 'numArgs' at fixed indices 0, 1, 2, and 3.");
                }

                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["method"], SlotIndex = 0, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["homeContext"], SlotIndex = 1, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["returnContext"], SlotIndex = 2, InheritedFrom = inheritedFrom });
                layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable["numArgs"], SlotIndex = 3, InheritedFrom = inheritedFrom });

                slotIndex = 4;
                foreach (var instVarName in cls.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName)) continue;
                    layout.Add(new JsonFieldLayoutEntry { NameIndex = _symbolTable[instVarName], SlotIndex = slotIndex++, InheritedFrom = inheritedFrom });
                }
            }
            else
            {
                // Normal field layout for all other classes
                foreach (var instVarName in cls.InstanceVariables)
                {
                    int nameIndex = _symbolTable[instVarName];
                    layout.Add(new JsonFieldLayoutEntry
                    {
                        NameIndex = nameIndex,
                        SlotIndex = slotIndex++,
                        InheritedFrom = inheritedFrom
                    });
                }
            }
        }

        return layout;
    }

    /// <summary>
    /// v1.0.6+: Computes field layout for a metaclass.
    /// All metaclasses follow the chain: meta:Foo → meta:Bar → ... → meta:Object → class:Class → ... → class:Behavior
    /// The layout includes fields from class:Behavior → class:ClassDescription → class:Class → class:Metaclass
    /// </summary>
    private List<JsonFieldLayoutEntry> ComputeMetaclassFieldLayout(CompilationResult ast)
    {
        var layout = new List<JsonFieldLayoutEntry>();

        // All metaclasses have the same layout: Behavior + ClassDescription + Class + Metaclass fields
        // Build the chain: Behavior → ClassDescription → Class → Metaclass
        var metaclassChain = new[] { "Behavior", "ClassDescription", "Class", "Metaclass" };

        int slotIndex = 0;
        foreach (var className in metaclassChain)
        {
            var classNode = ast.Classes.FirstOrDefault(c => c.Name == className);
            if (classNode == null)
                continue;

            // SPECIAL CASE: Behavior class must have superclass, methodDict, format at fixed indices 0, 1, 2
            // This is a Blue Book architectural invariant that the VM depends on
            if (className == "Behavior")
            {
                // Verify Behavior has exactly the required fields
                var requiredFields = new[] { "superclass", "methodDict", "format" };
                var missingFields = requiredFields.Where(f => !classNode.InstanceVariables.Contains(f)).ToList();

                if (missingFields.Any())
                {
                    throw new InvalidOperationException(
                        $"FATAL: Behavior class is missing required fields: {string.Join(", ", missingFields)}. " +
                        "The Blue Book VM architecture requires Behavior to define 'superclass', 'methodDict', and 'format' " +
                        "at fixed indices 0, 1, and 2 respectively.");
                }

                // Force fixed indices regardless of declaration order
                layout.Add(new JsonFieldLayoutEntry
                {
                    NameIndex = _symbolTable["superclass"],
                    SlotIndex = 0,
                    InheritedFrom = $"class:{className}"
                });

                layout.Add(new JsonFieldLayoutEntry
                {
                    NameIndex = _symbolTable["methodDict"],
                    SlotIndex = 1,
                    InheritedFrom = $"class:{className}"
                });

                layout.Add(new JsonFieldLayoutEntry
                {
                    NameIndex = _symbolTable["format"],
                    SlotIndex = 2,
                    InheritedFrom = $"class:{className}"
                });

                // Add remaining fields (e.g., subclasses) starting at index 3
                slotIndex = 3;
                foreach (var instVarName in classNode.InstanceVariables)
                {
                    if (requiredFields.Contains(instVarName))
                        continue; // Skip - already added at fixed indices

                    int nameIndex = _symbolTable[instVarName];
                    layout.Add(new JsonFieldLayoutEntry
                    {
                        NameIndex = nameIndex,
                        SlotIndex = slotIndex++,
                        InheritedFrom = $"class:{className}"
                    });
                }
            }
            else
            {
                // Normal field layout for other classes in the metaclass chain
                foreach (var instVarName in classNode.InstanceVariables)
                {
                    int nameIndex = _symbolTable[instVarName];
                    layout.Add(new JsonFieldLayoutEntry
                    {
                        NameIndex = nameIndex,
                        SlotIndex = slotIndex++,
                        InheritedFrom = $"class:{className}"
                    });
                }
            }
        }

        return layout;
    }

    /// <summary>
    /// Converts methods from the AST to the cold-start format.
    /// </summary>
    private void ConvertMethods(CompilationResult ast)
    {
        foreach (var classNode in ast.Classes)
        {
            // Convert instance methods
            foreach (var methodNode in classNode.Methods)
            {
                // Set current method context for block homeContext tracking
                _currentMethodLocalId = $"method:{classNode.Name}>>{methodNode.Selector}";

                // ACTIVATE BLOCK BUFFERING FOR POST-ORDER EMISSION
                //
                // Any blocks encountered in this method's literals will be buffered instead of
                // immediately added to _methods. This ensures the parent method appears BEFORE
                // its blocks in the bundle, creating forward references that enable single-pass
                // method compilation in the loader.
                _blockBuffer = new List<JsonMethod>();

                // For extended form (flags=7), add encoded argCount+primitive as last literal
                var literals = ConvertLiterals(methodNode.Literals);
                int literalCount = methodNode.Literals.Count;

                bool isExtendedForm = methodNode.PrimitiveIndex > 0 || methodNode.NumArgs > 4;
                if (isExtendedForm)
                {
                    // Encode argCount (high 5 bits) and primitive (low 8 bits) into SmallInteger
                    int encoded = (methodNode.NumArgs << 8) | methodNode.PrimitiveIndex;
                    literals.Add(new Dictionary<string, object>
                    {
                        ["type"] = "smallInt",
                        ["value"] = encoded
                    });
                    literalCount++;
                }

                var method = new JsonMethod
                {
                    LocalId = $"method:{classNode.Name}>>{methodNode.Selector}",
                    HomeClassLocalId = $"class:{classNode.Name}",
                    SelectorIndex = _symbolTable[methodNode.Selector],
                    Category = string.IsNullOrEmpty(methodNode.Category) ? null : methodNode.Category,
                    Header = new MethodHeader
                    {
                        Word = CalculateMethodHeaderWord(
                            Math.Min(literalCount, 63),  // Cap at 63 for header encoding
                            methodNode.NumTemps,
                            methodNode.NumArgs,
                            methodNode.PrimitiveIndex,
                            methodNode.StackDepth > 32,
                            methodNode.IsQuickReturnSelf,
                            methodNode.QuickReturnFieldIndex
                        ),
                        // LiteralCount: Capped at 63 (6-bit header limit)
                        // Actual literal count available via literals.Length
                        LiteralCount = Math.Min(literalCount, 63),
                        // TempCount: Always matches what's encoded in Word
                        TempCount = methodNode.NumTemps,
                        // ArgCount: For extended form (primitives, >4 args), decoded from last literal
                        ArgCount = methodNode.NumArgs,
                        PrimitiveIndex = methodNode.PrimitiveIndex,
                        LargeContext = methodNode.StackDepth > 32
                    },
                    Literals = literals,
                    Bytecodes = methodNode.Bytecode,
                    Source = methodNode.Source,
                    ArgNames = methodNode.ArgNames.ToArray(),
                    TempNames = methodNode.TempNames.ToArray()
                };

                // POST-ORDER EMISSION: Parent method first, then buffered blocks
                //
                // Emit the parent method first, creating forward references from this method
                // to any blocks it contains. Then emit all buffered blocks in the order they
                // were encountered (depth-first post-order for nested blocks).
                //
                // RESULT: Bundle ordering will be [parent_method, block1, block2_nested, ...]
                // instead of the previous [block2_nested, block1, parent_method].
                _methods.Add(method);                 // Parent first
                _methods.AddRange(_blockBuffer);      // Then blocks

                // DEACTIVATE BUFFERING
                // Clear the buffer for the next method. Setting to null also signals that
                // any blocks processed outside of method context should be emitted immediately.
                _blockBuffer = null;
            }

            // Convert class methods
            foreach (var methodNode in classNode.ClassMethods)
            {
                // Set current method context for block homeContext tracking
                _currentMethodLocalId = $"method:{classNode.Name}_class>>{methodNode.Selector}";

                // ACTIVATE BLOCK BUFFERING FOR POST-ORDER EMISSION
                // (Same as instance methods - see comments above for detailed explanation)
                _blockBuffer = new List<JsonMethod>();

                // For extended form (flags=7), add encoded argCount+primitive as last literal
                var literals = ConvertLiterals(methodNode.Literals);
                int literalCount = methodNode.Literals.Count;

                bool isExtendedForm = methodNode.PrimitiveIndex > 0 || methodNode.NumArgs > 4;
                if (isExtendedForm)
                {
                    // Encode argCount (high 5 bits) and primitive (low 8 bits) into SmallInteger
                    int encoded = (methodNode.NumArgs << 8) | methodNode.PrimitiveIndex;
                    literals.Add(new Dictionary<string, object>
                    {
                        ["type"] = "smallInt",
                        ["value"] = encoded
                    });
                    literalCount++;
                }

                var method = new JsonMethod
                {
                    LocalId = $"method:{classNode.Name}_class>>{methodNode.Selector}",
                    HomeClassLocalId = $"meta:{classNode.Name}",
                    SelectorIndex = _symbolTable[methodNode.Selector],
                    Category = string.IsNullOrEmpty(methodNode.Category) ? null : methodNode.Category,
                    Header = new MethodHeader
                    {
                        Word = CalculateMethodHeaderWord(
                            Math.Min(literalCount, 63),  // Cap at 63 for header encoding
                            methodNode.NumTemps,
                            methodNode.NumArgs,
                            methodNode.PrimitiveIndex,
                            methodNode.StackDepth > 32,
                            methodNode.IsQuickReturnSelf,
                            methodNode.QuickReturnFieldIndex
                        ),
                        // LiteralCount: Capped at 63 (6-bit header limit)
                        // Actual literal count available via literals.Length
                        LiteralCount = Math.Min(literalCount, 63),
                        // TempCount: Always matches what's encoded in Word
                        TempCount = methodNode.NumTemps,
                        // ArgCount: For extended form (primitives, >4 args), decoded from last literal
                        ArgCount = methodNode.NumArgs,
                        PrimitiveIndex = methodNode.PrimitiveIndex,
                        LargeContext = methodNode.StackDepth > 32
                    },
                    Literals = literals,
                    Bytecodes = methodNode.Bytecode,
                    Source = methodNode.Source,
                    ArgNames = methodNode.ArgNames.ToArray(),
                    TempNames = methodNode.TempNames.ToArray()
                };

                // POST-ORDER EMISSION: Parent method first, then buffered blocks
                // (Same as instance methods - see comments above for detailed explanation)
                _methods.Add(method);                 // Parent first
                _methods.AddRange(_blockBuffer);      // Then blocks
                _blockBuffer = null;                  // Deactivate buffering
            }
        }
    }

    /// <summary>
    /// Creates BlockContext metadata entries for each block method.
    /// v1.0.4: BlockContext objects are now METADATA-ONLY (not runtime objects).
    ///
    /// These metadata entries are used by:
    /// - Validators: to check closure graph integrity and lexical scoping
    /// - Debuggers: to inspect block closure structure without execution
    /// - Tooling: to analyze block relationships and homeContext chains
    ///
    /// Loader does NOT allocate these as runtime objects during cold start.
    /// </summary>
    private List<BlockContextMetadata> CreateBlockContextMetadata()
    {
        var blockContexts = new List<BlockContextMetadata>();

        // Iterate through all methods and create metadata for blocks (selectorIndex == -1)
        foreach (var method in _methods.Where(m => m.SelectorIndex == -1))
        {
            // Look up the homeContext for this block
            string? homeContextLocalId = _blockToHomeMethod.GetValueOrDefault(method.LocalId);

            // Find the outermost method in the closure chain
            string? outerContextLocalId = FindOutermostMethod(method.LocalId);

            // Create BlockContext metadata (NOT a runtime object)
            blockContexts.Add(new BlockContextMetadata
            {
                BlockMethodId = method.LocalId,
                InitialIP = 0, // Blocks always start at IP 0
                BlockArgumentCount = method.Header.ArgCount ?? 0,
                HomeContext = homeContextLocalId,
                OuterContext = outerContextLocalId
            });
        }

        return blockContexts;
    }

    /// <summary>
    /// Finds the outermost method in the closure chain by following homeContext links.
    /// Returns the localId of the root method, or null if no homeContext exists.
    /// </summary>
    private string? FindOutermostMethod(string blockLocalId)
    {
        string? current = blockLocalId;
        string? outermost = null;

        // Follow the homeContext chain to the root
        while (current != null && _blockToHomeMethod.TryGetValue(current, out string? parent))
        {
            outermost = parent;

            // If the parent is a method (starts with "method:"), we've reached the root
            if (parent.StartsWith("method:"))
            {
                break;
            }

            // Otherwise, the parent is another block, keep following the chain
            current = parent;
        }

        return outermost;
    }

    /// <summary>
    /// Calculates the Blue Book method header word with quick-return optimization support.
    ///
    /// This method encodes method metadata into a single 15-bit header word following the
    /// Smalltalk-80 Blue Book specification. The header format supports multiple method forms
    /// including ordinary methods, quick-return methods (flags=5/6), and extended methods (flags=7).
    ///
    /// QUICK-RETURN CONSTRAINT ENFORCEMENT:
    ///
    /// This method enforces a critical semantic constraint from the Blue Book:
    /// "Quick-return forms (flags=5, flags=6) and primitives are MUTUALLY EXCLUSIVE"
    ///
    /// WHY: A method with a primitive declaration must use the extended form (flags=7) because:
    ///   - The primitive index must be encoded in the last literal slot
    ///   - Quick-return forms cannot have literals for primitive encoding
    ///   - The VM must know whether to attempt primitive execution vs. quick-return bypass
    ///   - Mixing the two would create ambiguous execution semantics
    ///
    /// HOW: We enforce this by checking primitives FIRST in the flags selection logic:
    ///   1. If primitiveIndex > 0 OR argCount > 4: Use flags=7 (extended form)
    ///   2. Else if isQuickReturnSelf: Use flags=5 (quick-return-self)
    ///   3. Else if quickReturnFieldIndex >= 0: Use flags=6 (quick-return-field)
    ///   4. Else: Use flags=argCount (simple form, 0-4)
    ///
    /// This priority ordering ensures that even if DetectQuickReturn() identified a method
    /// as quick-return-eligible, we will use flags=7 if it has a primitive, thus preserving
    /// the mutual exclusivity constraint.
    ///
    /// EXAMPLES:
    ///   position                              → flags=6 (quick-return-field, ivar 2)
    ///   position &lt;primitive: 153&gt; ^pos  → flags=7 (primitive takes precedence)
    ///   asArray ^self                         → flags=5 (quick-return-self)
    ///   at: index ^array at: index            → flags=7 (5+ args not eligible for quick-return)
    ///
    /// See docs/Interpreter-Architecture.md "Quick-Return Flag Eligibility Constraints"
    /// for complete documentation of the 8 constraints enforced by DetectQuickReturn().
    /// </summary>
    /// <param name="literalCount">Number of literals in the method's literal frame</param>
    /// <param name="tempCount">Number of temporary variables (including arguments)</param>
    /// <param name="argCount">Number of method arguments</param>
    /// <param name="primitiveIndex">Primitive number (0 if none)</param>
    /// <param name="largeContext">True if method needs large context (unused in Blue Book)</param>
    /// <param name="isQuickReturnSelf">True if method body is exactly "^self" (detected by DetectQuickReturn)</param>
    /// <param name="quickReturnFieldIndex">Instance variable index if method is "^ivar", or -1 if not quick-return-field</param>
    /// <returns>15-bit header word encoding method metadata per Blue Book format</returns>
    private static int CalculateMethodHeaderWord(
        int literalCount,
        int tempCount,
        int argCount,
        int primitiveIndex,
        bool largeContext,
        bool isQuickReturnSelf,
        int quickReturnFieldIndex)
    {
        // Method header word format per docs/Interpreter-Architecture.md lines 5015-5020:
        // Bits 0-5:   numLiterals (6 bits, 0-63)
        // Bit 6:      needsLargeFrame flag
        // Bits 7-11:  numTemps (5 bits, 0-31)
        // Bits 12-14: flags field (3 bits)
        // Bit 15:     Reserved/unused

        int header = 0;

        // Bits 0-5: numLiterals (6 bits, max 63)
        header |= (literalCount & 0x3F);

        // Bit 6: needsLargeFrame
        if (largeContext)
            header |= 0x40;

        // Bits 7-11: numTemps (5 bits, max 31)
        header |= (tempCount & 0x1F) << 7;

        // Bits 12-14: flags (3 bits)
        int flags;
        if (primitiveIndex > 0 || argCount > 4)
            flags = 7;  // Extended form - args/primitive in last literal
        else
            flags = argCount;  // Simple form (0-4) - flags encode argCount directly

        header |= (flags & 0x7) << 12;

        return header;
    }

    /// <summary>
    /// Converts literals from the AST format to the cold-start discriminated union format.
    /// </summary>
    private List<object> ConvertLiterals(List<object> literals)
    {
        var result = new List<object>();

        foreach (var literal in literals)
        {
            result.Add(ConvertLiteral(literal));
        }

        return result;
    }

    /// <summary>
    /// Converts a single literal to the cold-start format.
    /// </summary>
    private object ConvertLiteral(object literal)
    {
        return literal switch
        {
            // Block (CompiledMethodData with null selector)
            // Blocks are stored as separate CompiledMethod objects per Blue Book spec
            CompiledMethodData block when block.IsBlock => ConvertBlockToMethod(block),

            // Symbol object (added by LiteralCollector for selectors)
            Symbol symbol => new Dictionary<string, object>
            {
                ["type"] = "symbol",
                ["index"] = _symbolTable.GetValueOrDefault(symbol.Value, -1)
            },

            // Association object
            Association assoc => new Dictionary<string, object>
            {
                ["type"] = "association",
                ["association"] = new Dictionary<string, object?>
                {
                    ["keyType"] = "symbol",
                    ["keyIndex"] = _symbolTable.GetValueOrDefault(((Symbol)assoc.Key).Value, -1),
                    ["valueRef"] = null // Will be patched later
                }
            },

            // Symbol literal in "#symbolName" format
            string str when str.StartsWith("#") => new Dictionary<string, object>
            {
                ["type"] = "symbol",
                ["index"] = _symbolTable.GetValueOrDefault(str[1..], -1)
            },

            // Association literal
            string str when str.StartsWith("Association(") => ConvertAssociation(str),

            // Regular string
            string str => new Dictionary<string, object>
            {
                ["type"] = "string",
                ["index"] = _stringTable.GetValueOrDefault(str, -1)
            },

            // SmallInteger
            int i => new Dictionary<string, object>
            {
                ["type"] = "smallInt",
                ["value"] = i
            },

            // Float
            double d => new Dictionary<string, object>
            {
                ["type"] = "float",
                ["value"] = d
            },

            // Character (if we had char type)
            char c => new Dictionary<string, object>
            {
                ["type"] = "character",
                ["codePoint"] = (int)c
            },

            // Array
            List<object> arr => new Dictionary<string, object>
            {
                ["type"] = "array",
                ["elements"] = arr.Select(ConvertLiteral).ToList()
            },

            // Array (object array from LiteralCollector)
            object[] objArr => new Dictionary<string, object>
            {
                ["type"] = "array",
                ["elements"] = objArr.Select(ConvertLiteral).ToList()
            },

            // Nil or unknown
            _ => new Dictionary<string, object> { ["type"] = "nil" }
        };
    }

    /// <summary>
    /// Converts a block (CompiledMethodData with null selector) to a method reference.
    /// Blocks are added to the methods collection and referenced as compiledMethod literals.
    /// This method also tracks homeContext relationships for closure metadata.
    /// </summary>
    private object ConvertBlockToMethod(CompiledMethodData block)
    {
        // Generate a unique localId for the block
        var blockLocalId = $"block:{Guid.NewGuid():N}";

        // Track the homeContext relationship
        // The _currentMethodLocalId may be either a method or a parent block
        if (_currentMethodLocalId != null)
        {
            _blockToHomeMethod[blockLocalId] = _currentMethodLocalId;
        }

        // Save the current method context and set this block as the new context for nested blocks
        var savedMethodContext = _currentMethodLocalId;
        _currentMethodLocalId = blockLocalId;

        // NESTED BLOCK HANDLING: Save current buffer and create a local buffer for nested blocks
        //
        // If this block contains nested blocks, we need to emit them AFTER this block,
        // not before. Save the parent's buffer and create a fresh one for this block's
        // nested children.
        var savedBuffer = _blockBuffer;
        _blockBuffer = new List<JsonMethod>();

        // Convert block's literals recursively (blocks can contain blocks)
        // Nested blocks will be added to our local _blockBuffer
        var convertedLiterals = ConvertLiterals(block.Literals.ToList());

        // Capture nested blocks before restoring parent buffer
        var nestedBlocks = _blockBuffer;

        // Restore the parent context and buffer
        _currentMethodLocalId = savedMethodContext;
        _blockBuffer = savedBuffer;

        // Create the JSON method object for the block
        var jsonMethod = new JsonMethod
        {
            LocalId = blockLocalId,
            HomeClassLocalId = "", // Empty string for blocks (no home class)
            SelectorIndex = -1, // Blocks have no selector
            Header = new MethodHeader
            {
                Word = CalculateMethodHeaderWord(
                    block.Literals.Length,
                    block.TemporaryCount,
                    block.ArgumentCount,
                    0, // Blocks never have primitives
                    block.MaxStackDepth > 16,
                    false, // Blocks are never quick-return-self
                    -1     // Blocks are never quick-return-field
                ),
                LiteralCount = block.Literals.Length,
                TempCount = block.TemporaryCount,
                ArgCount = block.ArgumentCount,
                PrimitiveIndex = 0, // Blocks never have primitives
                LargeContext = block.MaxStackDepth > 16
            },
            Literals = convertedLiterals,
            Bytecodes = block.Bytecode.Select(b => (int)b).ToList(),
            Source = null, // Blocks don't have source in this context
            ArgNames = block.ArgumentNames.ToArray(),
            TempNames = block.TemporaryNames.ToArray()
        };

        // POST-ORDER EMISSION: Buffer block and its nested blocks
        //
        // Add this block to the parent's buffer, then add all nested blocks that were
        // collected during literal conversion. This ensures proper post-order:
        //   [parent_method, outer_block, nested_block1, nested_block2, ...]
        //
        // If _blockBuffer is null, we're in init expression context - emit immediately.
        if (_blockBuffer != null)
        {
            _blockBuffer.Add(jsonMethod);           // Add this block first
            _blockBuffer.AddRange(nestedBlocks);    // Then add its nested blocks
        }
        else
        {
            // Fallback for init expressions - emit block and nested blocks immediately
            _methods.Add(jsonMethod);
            _methods.AddRange(nestedBlocks);
        }

        // Return a reference to the block as a compiledMethod literal
        return new Dictionary<string, object>
        {
            ["type"] = "compiledMethod",
            ["localId"] = blockLocalId
        };
    }

    /// <summary>
    /// Converts an Association string to the cold-start format.
    /// </summary>
    private object ConvertAssociation(string assocStr)
    {
        // Parse "Association(key:X,value:Y)" format
        var keyStart = assocStr.IndexOf("key:") + 4;
        var keyEnd = assocStr.IndexOf(',', keyStart);
        var key = keyEnd > keyStart ? assocStr[keyStart..keyEnd] : "";

        return new Dictionary<string, object>
        {
            ["type"] = "association",
            ["association"] = new Dictionary<string, object?>
            {
                ["keyType"] = "symbol",
                ["keyIndex"] = _symbolTable.GetValueOrDefault(key, -1),
                ["valueRef"] = null // Will be patched later
            }
        };
    }

    /// <summary>
    /// Converts initialization expressions from the AST to the cold-start format.
    /// </summary>
    private void ConvertInitExpressions(CompilationResult ast)
    {
        int counter = 0;

        foreach (var initExpr in ast.InitializationExpressions)
        {
            var jsonInitExpr = new JsonInitExpr
            {
                LocalId = $"init:{counter:D3}",
                Header = new MethodHeader
                {
                    Word = CalculateMethodHeaderWord(
                        Math.Min(initExpr.Literals.Count, 63),
                        initExpr.NumTemps,
                        0, // No arguments for init expressions
                        0, // No primitive
                        initExpr.StackDepth > 32,
                        false, // Init expressions are never quick-return-self
                        -1     // Init expressions are never quick-return-field
                    ),
                    LiteralCount = Math.Min(initExpr.Literals.Count, 63),
                    TempCount = initExpr.NumTemps,
                    ArgCount = 0,
                    PrimitiveIndex = 0,
                    LargeContext = initExpr.StackDepth > 32
                },
                Literals = ConvertLiterals(initExpr.Literals),
                Bytecodes = initExpr.Bytecode,
                Source = initExpr.Source
            };

            _initExprs.Add(jsonInitExpr);
            counter++;
        }
    }

    /// <summary>
    /// Generates patches for forward references and circular dependencies.
    /// </summary>
    private void GeneratePatches(CompilationResult ast)
    {
        // Build set of valid class names for filtering
        var validClassNames = new HashSet<string>(_classes.Select(c => GetClassNameFromLocalId(c.LocalId)));

        // Generate patches for Association values in method literals
        foreach (var method in _methods)
        {
            for (int i = 0; i < method.Literals.Count; i++)
            {
                if (method.Literals[i] is Dictionary<string, object> literal &&
                    literal.TryGetValue("type", out var type) &&
                    type.ToString() == "association")
                {
                    var assoc = literal["association"] as Dictionary<string, object>;
                    if (assoc != null && assoc["valueRef"] == null)
                    {
                        // Get the association key name
                        var keyIndex = Convert.ToInt32(assoc["keyIndex"]);
                        var keyName = keyIndex >= 0 && keyIndex < _symbols.Count ? _symbols[keyIndex] : "";

                        // Only generate patches for associations that reference actual classes
                        // Skip temp variables, method args, and other non-class symbols
                        if (!string.IsNullOrEmpty(keyName) && validClassNames.Contains(keyName))
                        {
                            _patches.Add(new PatchDescriptor
                            {
                                Type = "ReplaceLiteral",
                                MethodLocalId = method.LocalId,
                                LiteralIndex = i,
                                Value = new FieldValue
                                {
                                    Type = "objectRef",
                                    LocalId = $"class:{keyName}"
                                }
                            });
                        }
                        // For non-class associations (temps, args, etc.), leave value as nil
                        // These will be resolved at runtime by the compiler/interpreter
                    }
                }
            }
        }

        // Note: Metaclass wiring patches are no longer needed - handled automatically in Phase 2
        // The interpreter's Phase 2 (Phase2_ClassShells.cs) now properly wires metaclasses
        // during class shell creation, so we don't need explicit patches for this.
    }

    /// <summary>
    /// Computes the installation order for classes, methods, and initialization expressions.
    /// </summary>
    private void ComputeInstallOrder()
    {
        // Phase 1: Bootstrap classes (Object, Behavior, Class, Metaclass)
        var bootstrapClasses = new[] { "Object", "Behavior", "Class", "Metaclass", "CompiledMethod" };
        var bootstrapLocalIds = _classes
            .Where(c => bootstrapClasses.Contains(GetClassNameFromLocalId(c.LocalId)))
            .Select(c => c.LocalId)
            .ToList();

        if (bootstrapLocalIds.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Bootstrap Classes",
                Kind = "classes",
                LocalIds = bootstrapLocalIds
            });
        }

        // Phase 2: Core classes (everything else)
        var coreClassLocalIds = _classes
            .Where(c => !bootstrapLocalIds.Contains(c.LocalId))
            .Select(c => c.LocalId)
            .ToList();

        if (coreClassLocalIds.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Core Classes",
                Kind = "classes",
                LocalIds = coreClassLocalIds
            });
        }

        // Phase 3: Pool dictionaries
        if (_objects.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Pool Dictionaries",
                Kind = "objects",
                LocalIds = _objects.Select(o => o.LocalId).ToList()
            });
        }

        // Phase 4: Methods
        if (_methods.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Methods",
                Kind = "methods",
                LocalIds = _methods.Select(m => m.LocalId).ToList()
            });
        }

        // Phase 5: Initialization expressions
        if (_initExprs.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Initialization Expressions",
                Kind = "initExpressions",
                LocalIds = _initExprs.Select(e => e.LocalId).ToList()
            });
        }

        // Phase 6: Patches
        if (_patches.Count > 0)
        {
            _installPhases.Add(new InstallPhase
            {
                Name = "Patches",
                Kind = "patches",
                LocalIds = _patches.Select((p, idx) => $"patch:{idx:D3}").ToList()
            });
        }
    }

    /// <summary>
    /// Extracts class name from a localId.
    /// </summary>
    private static string GetClassNameFromLocalId(string localId)
    {
        if (localId.StartsWith("class:"))
            return localId[6..];
        if (localId.StartsWith("meta:"))
            return localId[5..];
        return "";
    }

    /// <summary>
    /// Builds the final cold-start bundle with all sections.
    /// </summary>
    private ColdStartBundle BuildColdStartBundle(List<int> optimizedSelectors)
    {
        return new ColdStartBundle
        {
            Metadata = GenerateMetadata(),
            Symbols = _symbols,
            Strings = _strings,
            OptimizedSelectors = optimizedSelectors,
            Classes = _classes,
            Objects = _objects,
            Methods = _methods,
            InitExpressions = _initExprs,
            InstallOrder = new InstallOrder { Phases = _installPhases },
            Patches = _patches
        };
    }

    /// <summary>
    /// Generates metadata for the cold-start bundle.
    /// </summary>
    private Metadata GenerateMetadata()
    {
        // For multi-file bundles, skip checksum calculation
        // (would need to combine checksums from all source files)
        string checksum = "multi-file-bundle";

        var version = GetType().Assembly.GetName().Version;

        // Count BlockContext metadata entries (one per block method)
        var blockContexts = CreateBlockContextMetadata();

        return new Metadata
        {
            Version = "1.0.6",
            Generator = $"Smalltalk80.Compiler v{version}",
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            SourceFile = _sourceFileDescription,
            SourceChecksum = checksum,
            SymbolCount = _symbols.Count,
            StringCount = _strings.Count,
            ClassCount = _classes.Count,
            MethodCount = _methods.Count,
            ObjectCount = _objects.Count,
            InitExpressionCount = _initExprs.Count,
            BlockContextCount = blockContexts.Count,
            BlockContexts = blockContexts
        };
    }
}

// JSON Data Models matching the schema

internal sealed record ColdStartBundle
{
    public required Metadata Metadata { get; init; }
    public required List<string> Symbols { get; init; }
    public required List<string> Strings { get; init; }
    public required List<int> OptimizedSelectors { get; init; }
    public required List<JsonClass> Classes { get; init; }
    public required List<JsonObject> Objects { get; init; }
    public required List<JsonMethod> Methods { get; init; }
    public required List<JsonInitExpr> InitExpressions { get; init; }
    public required InstallOrder InstallOrder { get; init; }
    public required List<PatchDescriptor> Patches { get; init; }
}

internal sealed record Metadata
{
    public required string Version { get; init; }
    public required string Generator { get; init; }
    public required string GeneratedAt { get; init; }
    public string? SourceFile { get; init; }
    public required string SourceChecksum { get; init; }
    public required int SymbolCount { get; init; }
    public required int StringCount { get; init; }
    public required int ClassCount { get; init; }
    public required int MethodCount { get; init; }
    public required int ObjectCount { get; init; }
    public required int InitExpressionCount { get; init; }
    public int? BlockContextCount { get; init; }
    public List<BlockContextMetadata>? BlockContexts { get; init; }
}

internal sealed record BlockContextMetadata
{
    public required string BlockMethodId { get; init; }
    public required int InitialIP { get; init; }
    public required int BlockArgumentCount { get; init; }
    public string? HomeContext { get; init; }
    public string? OuterContext { get; init; }
}

/// <summary>
/// v1.0.6+: Represents a single field in a class's precomputed layout.
/// Emitted by compiler to eliminate runtime topological sort in interpreter.
/// </summary>
internal sealed record JsonFieldLayoutEntry
{
    public required int NameIndex { get; init; }
    public required int SlotIndex { get; init; }
    public string? InheritedFrom { get; init; }
}

internal sealed record JsonClass
{
    public required string LocalId { get; init; }
    public required int NameIndex { get; init; }
    public required bool IsMetaclass { get; init; }
    public string? MetaclassOfLocalId { get; init; }
    public required int Format { get; init; }
    public required List<int> InstVarNames { get; init; }
    public required List<int> ClassVarNames { get; init; }
    public required List<string> PoolLocalIds { get; init; }
    public int? SuperNameIndex { get; init; }
    public required string Category { get; init; }
    public int? CommentStringIndex { get; init; }
    public string? ClassSideInitMethodLocalId { get; init; }

    // v1.0.6+: Precomputed layout (eliminates runtime topological sort)
    public required int TotalInstanceVarCount { get; init; }
    public required List<JsonFieldLayoutEntry> FieldLayout { get; init; }
}

internal sealed record JsonObject
{
    public required string LocalId { get; init; }
    public required int ClassNameIndex { get; init; }
    public required string FormatHint { get; init; }
    public required List<object> Fields { get; init; }
    public required List<object> IndexedFields { get; init; }
}

internal sealed record JsonMethod
{
    public required string LocalId { get; init; }
    public required string HomeClassLocalId { get; init; }
    public required int SelectorIndex { get; init; }
    public string? Category { get; init; }
    public required MethodHeader Header { get; init; }
    public required List<object> Literals { get; init; }
    public required List<int> Bytecodes { get; init; }
    public string? Source { get; init; }
    public string[]? ArgNames { get; init; }
    public string[]? TempNames { get; init; }
}

internal sealed record JsonInitExpr
{
    public required string LocalId { get; init; }
    public required MethodHeader Header { get; init; }
    public required List<object> Literals { get; init; }
    public required List<int> Bytecodes { get; init; }
    public string? Source { get; init; }
}

internal sealed record MethodHeader
{
    public required int Word { get; init; }
    public int? LiteralCount { get; init; }
    public int? TempCount { get; init; }
    public int? ArgCount { get; init; }
    public int? PrimitiveIndex { get; init; }
    public bool? LargeContext { get; init; }
}

internal sealed record InstallOrder
{
    public required List<InstallPhase> Phases { get; init; }
}

internal sealed record InstallPhase
{
    public required string Name { get; init; }
    public required string Kind { get; init; }
    public required List<string> LocalIds { get; init; }
}

/// <summary>
/// Patch descriptor matching the interpreter's PatchDescriptor schema.
/// Used to patch forward references and circular dependencies after allocation.
/// </summary>
internal sealed record PatchDescriptor
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("targetLocalId")]
    public string? TargetLocalId { get; init; }

    [JsonPropertyName("fieldName")]
    public string? FieldName { get; init; }

    [JsonPropertyName("value")]
    public FieldValue? Value { get; init; }

    [JsonPropertyName("classLocalId")]
    public string? ClassLocalId { get; init; }

    [JsonPropertyName("methodLocalId")]
    public string? MethodLocalId { get; init; }

    [JsonPropertyName("literalIndex")]
    public int? LiteralIndex { get; init; }
}

/// <summary>
/// Field value discriminated union matching the interpreter's FieldValue schema.
/// </summary>
internal sealed record FieldValue
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("value")]
    public object? Value { get; init; }

    [JsonPropertyName("index")]
    public int? Index { get; init; }

    [JsonPropertyName("localId")]
    public string? LocalId { get; init; }

    [JsonPropertyName("codePoint")]
    public int? CodePoint { get; init; }

    [JsonPropertyName("encoding")]
    public string? Encoding { get; init; }

    [JsonPropertyName("data")]
    public string? Data { get; init; }
}