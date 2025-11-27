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
    /// New expressions not in this list run after these but before test classes.
    /// </summary>
    private static readonly List<string> KnownOrder = new()
    {
        // Core classes first
        "Character initialize",
        "Float initialize",
        "SmallInteger initialize",
        "BinaryChoiceView initialize",
        "ClassOrganizer initialize",
        "CompiledMethod initialize",
        "Cursor initialize",
        "Date initialize",
        "Decompiler initialize",
        "Explainer initialize",
        "FileDirectory initialize",
        "Form initialize",
        "InputSensor initialize",
        "InputState initialize",
        "InstructionStream initialize",
        "MessageNode initialize",
        "Object initialize",
        "Paragraph initialize",
        "ParseNode initialize",
        "ProcessorScheduler initialize",
        "Project initialize",
        "Scanner initialize",
        "StringHolder initialize",
        "Symbol initialize",
        "SystemDictionary initialize",
        "SystemTracer initialize",
        "Text initialize",
        "TextList initialize",
        "WindowsFileDirectory initialize",
        "WindowsFileSystem initialize",
        "File initialize",
        "BitEditor initialize",
        "FormEditor initialize",
        "Debugger initialize",
        "LargePositiveInteger initialize",
        "LargeNegativeInteger initialize",
        "Delay initialize",
        "ChangeListController initialize",
        "InputSensor install",
        "InputSensor initSensor",
        "NotifierController initialize",
        "ParagraphEditor initialize",
        "ProjectController initialize",
        "ScreenController initialize",
        "StandardSystemController initialize",
        "StringHolderController initialize",
        "VariableNode initialize",
        "FormMenuView initialize"
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
            }
        }

        // Remove test classes from remaining - they run last
        foreach (var idx in testClassIndices)
        {
            remaining.Remove(idx);
        }

        // Build source-to-index map (excluding test classes)
        var sourceToIndex = new Dictionary<string, int>();
        for (int i = 0; i < bundle.InitExpressions.Length; i++)
        {
            if (remaining.Contains(i))
            {
                var source = bundle.InitExpressions[i].Source;
                if (source != null)
                {
                    sourceToIndex[source] = i;
                }
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
            Console.WriteLine($"  ℹ️  {newExpressions.Count} new init expressions (running before test classes)");
            result.AddRange(newExpressions);
        }

        // 3. Add test classes LAST (in the order they appear in the bundle)
        result.AddRange(testClassIndices.OrderBy(i => i));

        return result;
    }
}
