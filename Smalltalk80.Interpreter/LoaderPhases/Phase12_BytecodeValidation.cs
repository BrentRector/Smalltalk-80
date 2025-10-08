using System.Text.Json;
using Smalltalk80.Core.Bytecodes;
using Smalltalk80.Interpreter.Core.Bytecodes;
using Smalltalk80.Interpreter.Schema;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.LoaderPhases;

/// <summary>
/// Phase 12: Bytecode Validation (Optional)
/// Performs comprehensive bytecode validation including:
/// - Stack balance analysis
/// - Operand range validation
/// - Bytecode decompilation and source comparison
/// This is an optional sanity check that runs after all cold start phases complete.
/// </summary>
public class Phase12_BytecodeValidation
{
    private readonly ColdStartBundle _bundle;
    private readonly ISmalltalkHeap _heap;
    private readonly Dictionary<string, int> _localIdToObjectId;
    private readonly string[] _symbols;
    private readonly LayoutResolver _layoutResolver;

    public Phase12_BytecodeValidation(
        ColdStartBundle bundle,
        ISmalltalkHeap heap,
        Dictionary<string, int> localIdToObjectId,
        string[] symbols,
        LayoutResolver layoutResolver)
    {
        _bundle = bundle;
        _heap = heap;
        _localIdToObjectId = localIdToObjectId;
        _symbols = symbols;
        _layoutResolver = layoutResolver;
    }

    public void Run()
    {
        // Stack analysis statistics
        int totalMethods = 0;
        int methodsAnalyzed = 0;
        int maxStackDepthSeen = 0;
        var stackIssues = new List<(string className, string selector, string message, int bytecodeLength)>();
        var operandIssues = new List<(string className, string selector, string message, int bytecodeLength)>();

        // Decompilation statistics
        int decompilationSuccesses = 0;
        int decompilationFailures = 0;
        int sourceMatches = 0;
        int sourceMismatches = 0;
        var decompilationErrors = new List<(string className, string selector, string message)>();
        var mismatches = new List<(string className, string selector)>();

        foreach (var method in _bundle.Methods)
        {
            totalMethods++;
            var literalIds = method.Literals.Select(ResolveLiteralValue).ToArray();
            var bytecodes = DecodeBytecodes(method.Bytecodes);
            int homeClassId = _localIdToObjectId[method.HomeClassLocalId];

            // 1. Stack balance analysis (CFG-based)
            var stackValidation = BytecodeStackValidator.Validate(bytecodes);
            methodsAnalyzed++;

            if (stackValidation.MaxStackDepth > maxStackDepthSeen)
            {
                maxStackDepthSeen = stackValidation.MaxStackDepth;
            }

            if (!stackValidation.IsValid && stackValidation.Errors.Count > 0)
            {
                string className = GetClassNameForLocalId(method.HomeClassLocalId);
                string selector = _symbols[method.SelectorIndex];
                // Combine all errors for this method into one message
                string allErrors = string.Join("; ", stackValidation.Errors);
                stackIssues.Add((className, selector, allErrors, bytecodes.Length));
            }

            // 2. Operand validation (only if stack is valid)
            if (stackValidation.IsValid)
            {
                int instVarCount = GetInstanceVariableCount(homeClassId);

                var operandValidation = BytecodeOperandValidator.Validate(
                    bytecodes,
                    literalIds.Length,
                    method.Header.TempCount ?? 0,
                    method.Header.ArgCount ?? 0,
                    instVarCount);

                if (!operandValidation.IsValid && operandValidation.ErrorMessage != null)
                {
                    string className = GetClassNameForLocalId(method.HomeClassLocalId);
                    string selector = _symbols[method.SelectorIndex];
                    operandIssues.Add((className, selector, operandValidation.ErrorMessage, bytecodes.Length));
                }
            }

            // 3. Decompilation validation
            try
            {
                // Get class instance variable names
                var classDesc = _bundle.Classes.FirstOrDefault(c =>
                    _localIdToObjectId.TryGetValue(c.LocalId, out int id) && id == homeClassId);
                string[] instVarNames = classDesc != null
                    ? classDesc.InstVarNames.Select(idx => _symbols[idx]).ToArray()
                    : Array.Empty<string>();

                // Build temp names array from JSON (args first, then temps)
                var tempNamesList = new List<string>();
                if (method.ArgNames != null)
                {
                    tempNamesList.AddRange(method.ArgNames);
                }
                if (method.TempNames != null)
                {
                    tempNamesList.AddRange(method.TempNames);
                }
                string[] tempNames = tempNamesList.ToArray();

                // Decompile
                var decompiler = new BytecodeDecompiler(
                    (SmalltalkHeap)_heap,
                    _symbols,
                    literalIds,
                    tempNames,
                    instVarNames);

                string decompiled = decompiler.Decompile(bytecodes);
                decompilationSuccesses++;

                // Compare with original source if available
                if (!string.IsNullOrWhiteSpace(method.Source))
                {
                    string normalizedOriginal = NormalizeSource(method.Source, stripSignature: true);
                    string normalizedDecompiled = NormalizeSource(decompiled, stripSignature: false);

                    if (normalizedOriginal == normalizedDecompiled)
                    {
                        sourceMatches++;
                    }
                    else
                    {
                        sourceMismatches++;
                        string className = GetClassNameForLocalId(method.HomeClassLocalId);
                        string selector = _symbols[method.SelectorIndex];
                        mismatches.Add((className, selector));

                        // Show first mismatch in detail (optional debug output)
                        if (mismatches.Count == 1 && Environment.GetEnvironmentVariable("DEBUG_VALIDATION") == "1")
                        {
                            Console.WriteLine($"\n  === First Source Mismatch Example ===");
                            Console.WriteLine($"  Method: {className}>>{selector}");
                            Console.WriteLine($"  Original (first 200 chars):");
                            Console.WriteLine($"    {method.Source.Substring(0, Math.Min(200, method.Source.Length))}");
                            Console.WriteLine($"  Decompiled (first 200 chars):");
                            Console.WriteLine($"    {decompiled.Substring(0, Math.Min(200, decompiled.Length))}");
                            Console.WriteLine($"  ===================================\n");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                decompilationFailures++;
                string className = GetClassNameForLocalId(method.HomeClassLocalId);
                string selector = _symbols[method.SelectorIndex];
                decompilationErrors.Add((className, selector, ex.Message));
            }
        }

        // Report stack analysis results (print checkmark inline first, then details on next line)
        Console.WriteLine($"✓\n  Methods analyzed: {methodsAnalyzed}/{totalMethods}");
        Console.WriteLine($"  Max stack depth: {maxStackDepthSeen}");

        if (stackIssues.Count > 0)
        {
            Console.WriteLine($"  ⚠ Stack issues found: {stackIssues.Count}");
            Console.WriteLine($"  Showing simplest methods (by bytecode length):");
            foreach (var (className, selector, message, bytecodeLength) in stackIssues.OrderBy(x => x.bytecodeLength).Take(10))
            {
                Console.WriteLine($"    {className}>>{selector} ({bytecodeLength} bytes): {message}");
            }
            if (stackIssues.Count > 10)
            {
                Console.WriteLine($"    ... and {stackIssues.Count - 10} more");
            }
        }
        else
        {
            Console.WriteLine($"  ✓ All methods have balanced stacks");
        }

        // Report operand validation results
        if (operandIssues.Count > 0)
        {
            Console.WriteLine($"  ⚠ Operand validation issues found: {operandIssues.Count}");
            Console.WriteLine($"  Showing first 10 methods:");
            foreach (var (className, selector, message, bytecodeLength) in operandIssues.Take(10))
            {
                Console.WriteLine($"    {className}>>{selector} ({bytecodeLength} bytes): {message}");
            }
            if (operandIssues.Count > 10)
            {
                Console.WriteLine($"    ... and {operandIssues.Count - 10} more");
            }
        }
        else
        {
            Console.WriteLine($"  ✓ All bytecode operands reference valid indices");
        }

        // Report decompilation results
        Console.WriteLine($"  Methods decompiled: {decompilationSuccesses}/{totalMethods} ({decompilationSuccesses * 100.0 / totalMethods:F1}%)");

        if (decompilationFailures > 0)
        {
            Console.WriteLine($"  ⚠ Decompilation failures: {decompilationFailures}");
            Console.WriteLine($"  Showing first 10 failures:");
            foreach (var (className, selector, message) in decompilationErrors.Take(10))
            {
                Console.WriteLine($"    {className}>>{selector}: {message}");
            }
            if (decompilationErrors.Count > 10)
            {
                Console.WriteLine($"    ... and {decompilationErrors.Count - 10} more");
            }
        }

        int totalCompared = sourceMatches + sourceMismatches;
        if (totalCompared > 0)
        {
            Console.WriteLine($"  Source comparison: {sourceMatches}/{totalCompared} match ({sourceMatches * 100.0 / totalCompared:F1}%)");
            if (sourceMismatches > 0)
            {
                Console.WriteLine($"  Showing first 10 source mismatches:");
                foreach (var (className, selector) in mismatches.Take(10))
                {
                    Console.WriteLine($"    {className}>>{selector}");
                }
                if (mismatches.Count > 10)
                {
                    Console.WriteLine($"    ... and {mismatches.Count - 10} more");
                }
            }
        }
    }

    private int GetInstanceVariableCount(int classId)
    {
        // Find the class descriptor using the class ID
        var classDesc = _bundle.Classes.FirstOrDefault(c =>
            _localIdToObjectId.TryGetValue(c.LocalId, out int id) && id == classId);

        if (classDesc == null)
        {
            return 0;
        }

        // Use the layout resolver which already computed total instance variable count
        // (including inherited) in Phase 4
        return _layoutResolver.GetInstanceVariableCount(classDesc.LocalId);
    }

    private string GetClassNameForLocalId(string localId)
    {
        var classDesc = _bundle.Classes.FirstOrDefault(c => c.LocalId == localId);
        return classDesc != null ? _symbols[classDesc.NameIndex] : localId;
    }

    private byte[] DecodeBytecodes(object bytecodes)
    {
        return bytecodes switch
        {
            JsonElement e when e.ValueKind == JsonValueKind.Array =>
                e.EnumerateArray().Select(b => (byte)b.GetInt32()).ToArray(),
            JsonElement e when e.ValueKind == JsonValueKind.String =>
                Convert.FromBase64String(e.GetString()!),
            _ => throw new InvalidOperationException("Invalid bytecode format")
        };
    }

    private int ResolveLiteralValue(LiteralValue literal)
    {
        return literal.Type switch
        {
            "symbol" => _heap.InternSymbol(_symbols[literal.Index!.Value]),
            "string" => _heap.InternString(_bundle.Strings[literal.Index!.Value]),
            "smallInt" => _heap.EncodeSmallInteger(GetIntValue(literal.Value!)),
            "float" => AllocateFloat(GetFloatValue(literal.Value!)),
            "nil" => GlobalRegistry.NilObjectId,
            "true" => GlobalRegistry.TrueObjectId,
            "false" => GlobalRegistry.FalseObjectId,
            "objectRef" => _localIdToObjectId[literal.LocalId!],
            "character" => _heap.AllocateCharacter(literal.CodePoint!.Value),
            "bytes" => _heap.AllocateByteArray(literal.Encoding!, literal.Data!),
            "association" => ResolveAssociation(literal.Association!),
            "array" => _heap.AllocateArray(literal.Elements!.Select(ResolveLiteralValue).ToArray()),
            _ => throw new InvalidOperationException($"Unknown literal type: {literal.Type}")
        };
    }

    private int GetIntValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetInt32(),
            int i => i,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to int")
        };
    }

    private double GetFloatValue(object value)
    {
        return value switch
        {
            JsonElement e => e.GetDouble(),
            double d => d,
            float f => f,
            _ => throw new InvalidOperationException($"Cannot convert {value.GetType()} to double")
        };
    }

    private int AllocateFloat(double value)
    {
        byte[] bytes = BitConverter.GetBytes(value);
        return _heap.AllocateByteArray("base64", Convert.ToBase64String(bytes));
    }

    private int ResolveAssociation(AssociationLiteral assoc)
    {
        int keyId = _heap.InternSymbol(_symbols[assoc.KeyIndex]);
        int valueId = assoc.ValueRef?.LocalId != null
            ? _localIdToObjectId[assoc.ValueRef.LocalId]
            : GlobalRegistry.NilObjectId;

        return _heap.AllocateAssociation(keyId, valueId);
    }

    private static string NormalizeSource(string source, bool stripSignature)
    {
        // Strip method signature (everything up to and including the first newline)
        // The decompiler only reconstructs the method body, not the signature
        if (stripSignature)
        {
            int firstNewline = source.IndexOf('\n');
            if (firstNewline >= 0)
            {
                source = source.Substring(firstNewline + 1);
            }
        }

        // Remove trailing '!' (Smalltalk method terminator in source files)
        source = source.TrimEnd('!').Trim();

        // Normalize assignment operators: both _ and := should be treated the same
        source = source.Replace(":=", "_");

        // Remove implicit "^self" return that the decompiler adds but original source omits
        // The compiler adds bytecode 121 (RETURN_SELF) to methods without explicit returns,
        // and the decompiler shows this as "^ self", but original source doesn't show it
        string normalized = new string(source
            .Where(c => !char.IsWhiteSpace(c) && c != '.')  // Also remove periods
            .Select(char.ToLowerInvariant)
            .ToArray());

        // If the normalized source ends with "^self", remove it for comparison
        // This handles the implicit return that the decompiler shows but source doesn't
        if (normalized.EndsWith("^self"))
        {
            normalized = normalized.Substring(0, normalized.Length - 5);
        }

        return normalized;
    }
}
