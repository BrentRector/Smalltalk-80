using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Handles ordering of init expression execution.
/// Uses a simple explicit list - run in order specified, with test classes last.
/// </summary>
public static class InitExpressionOrdering
{
    /// <summary>
    /// Returns the ordered list of init expression source strings.
    /// This list matches exactly what's in coldstart.json, ordered by successful run order.
    /// New expressions not in this list run after these but before test classes.
    /// </summary>
    private static readonly List<string> KnownOrder = new()
    {
        // Order matches current successful run sequence
        "Character initialize",           // index 54
        "Float initialize",               // index 120
        "SmallInteger initialize",        // index 239
        "BinaryChoiceView initialize",    // index 31
        "ClassOrganizer initialize",      // index 67
        "CompiledMethod initialize",      // index 74
        "Cursor initialize",              // index 83
        "Date initialize",                // index 86
        "Decompiler initialize",          // index 90
        "Explainer initialize",           // index 105
        "FileDirectory initialize",       // index 111
        "Form initialize",                // index 122
        "InputSensor initialize",         // index 137
        "InputState initialize",          // index 141
        "InstructionStream initialize",   // index 146
        "MessageNode initialize",         // index 170
        "Object initialize",              // index 189
        "Paragraph initialize",           // index 197
        "ParseNode initialize",           // index 201
        "ProcessorScheduler initialize",  // index 212
        "Project initialize",             // index 214
        "Scanner initialize",             // index 227
        "StringHolder initialize",        // index 249
        "Symbol initialize",              // index 257
        "SystemDictionary initialize",    // index 260
        "SystemTracer initialize",        // index 263
        "Text initialize",                // index 265
        "TextList initialize",            // index 272
        "WindowsFileDirectory initialize",// index 8
        "WindowsFileSystem initialize",   // index 2
        "File initialize",                // index 109
        "BitEditor initialize",           // index 34
        "FormEditor initialize",          // index 125
        "Debugger initialize",            // index 88
        "LargePositiveInteger initialize",// index 153
        "LargeNegativeInteger initialize",// index 151
        "Delay initialize",               // index 93
        "ChangeListController initialize",// index 49
        "InputSensor install",            // index 138
        "InputSensor initSensor",         // index 139
        "NotifierController initialize",  // index 185
        "ParagraphEditor initialize",     // index 199
        "ProjectController initialize",   // index 216
        "ScreenController initialize",    // index 229
        "StandardSystemController initialize", // index 243
        "StringHolderController initialize",   // index 251
        "VariableNode initialize",        // index 279
        "FormMenuView initialize"         // index 129 - MUST run before FormDisplayTest
    };

    /// <summary>
    /// Class name prefixes for test classes that always run last.
    /// </summary>
    private static readonly List<string> TestClassPrefixes = new()
    {
        "FormDisplayTest"
    };

    /// <summary>
    /// Returns dependency order for backward compatibility.
    /// </summary>
    public static List<string> GetDependencyOrder()
    {
        var result = new List<string>();
        foreach (var source in KnownOrder)
        {
            // Convert "Foo initialize" to "Foo class" format
            var className = source.Replace(" initialize", " class")
                                  .Replace(" install", " class")
                                  .Replace(" initSensor", " class");
            if (!result.Contains(className))
                result.Add(className);
        }
        foreach (var prefix in TestClassPrefixes)
        {
            result.Add($"{prefix} class");
        }
        return result;
    }

    /// <summary>
    /// Checks if an init expression source belongs to a test class.
    /// </summary>
    private static bool IsTestClass(string? source)
    {
        if (source == null) return false;
        foreach (var prefix in TestClassPrefixes)
        {
            if (source.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Reorders init expression indices according to explicit order.
    /// </summary>
    public static List<int> ReorderByDependencies(
        List<int> originalIndices,
        ColdStartBundle bundle,
        List<string> dependencyOrder)
    {
        var result = new List<int>();
        var remaining = new HashSet<int>(originalIndices);
        var testClassIndices = new List<int>();

        // First pass: identify test class indices and separate them
        foreach (var idx in originalIndices)
        {
            var source = bundle.InitExpressions[idx].Source;
            if (IsTestClass(source))
            {
                testClassIndices.Add(idx);
                remaining.Remove(idx);
            }
        }

        // Build source-to-index map (excluding test classes)
        var sourceToIndex = new Dictionary<string, int>();
        foreach (var idx in remaining)
        {
            var source = bundle.InitExpressions[idx].Source;
            if (source != null)
            {
                sourceToIndex[source] = idx;
            }
        }

        // 1. Add known expressions in order
        foreach (var source in KnownOrder)
        {
            if (sourceToIndex.TryGetValue(source, out int idx) && remaining.Contains(idx))
            {
                result.Add(idx);
                remaining.Remove(idx);
            }
        }

        // 2. Add any NEW expressions (not in KnownOrder, not test classes)
        if (remaining.Count > 0)
        {
            var newExpressions = remaining.OrderBy(i => i).ToList();
            Console.WriteLine($"  ℹ️  {newExpressions.Count} new init expressions (running before test classes):");
            foreach (var idx in newExpressions)
            {
                var src = bundle.InitExpressions[idx].Source ?? "(null)";
                Console.WriteLine($"      - {src}");
            }
            result.AddRange(newExpressions);
        }

        // 3. Add test classes LAST
        result.AddRange(testClassIndices.OrderBy(i => i));

        return result;
    }
}
