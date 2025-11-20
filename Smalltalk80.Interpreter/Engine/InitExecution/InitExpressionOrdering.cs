using Smalltalk80.Interpreter.Core.Schema;

namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Handles dependency-based ordering of init expression execution.
/// Based on topological sort of class dependencies (3 levels, no circular deps).
/// See docs/InitExpression-DependencyAnalysis.md for analysis.
/// </summary>
public static class InitExpressionOrdering
{
    /// <summary>
    /// Returns the complete dependency-ordered list of class names for init expression execution.
    /// Based on InitExpression-DependencyAnalysis.md topological sort (3 levels, no circular deps).
    /// </summary>
    public static List<string> GetDependencyOrder()
    {
        return new List<string>
        {
            // LEVEL 0: 80 classes with no dependencies (alphabetically sorted within level)
            // ESSENTIAL CLASSES FIRST (required for basic VM functionality)
            "Character class",        // ESSENTIAL: Required for CharacterTable (String>>at: primitive)
            "Float class",            // ESSENTIAL: Required for Float constants (Pi, E, etc.)
            "SmallInteger class",     // ESSENTIAL: Required for SmallInteger constants/utilities (moved to front)

            // Other Level 0 classes
            "Benchmark class",
            "BinaryChoiceView class",
            "BlockContextTest class",
            "BrowserView class",
            "Change class",
            "ChangesView class",
            "Class class",
            "ClassHierarchyBrowserView class",
            "ClassOrganizer class",
            "CompiledMethod class",
            "ControlManager class",
            "Cursor class",
            "Date class",
            "DebuggerView class",
            "Decompiler class",
            "DigitBuffer class",
            "DiskBrowserView class",
            "DisplayScreen class",
            "DisplayTextView class",
            "Encoder class",
            "Explainer class",
            "File class",
            "FileDirectory class",
            "FileList class",
            "Form class",
            "FormButtonView class",
            "FormView class",
            "InputSensor class",       // REQUIRED BEFORE Object: Object>>confirm: uses Sensor cursorPoint
            "InputState class",
            "Inspector class",
            "InspectorView class",
            "InstructionStream class",
            "Lexicon class",
            "ListController class",
            "ListView class",
            "LockedTextView class",
            "LowSpaceView class",
            "Magnitude class",
            "MenuView class",
            "MessageNode class",
            "MethodNode class",
            "MixedInspectorView class",
            "NameSetInspectorView class",
            "NoControllerView class",
            "Notifier class",
            "NotifierView class",
            "Object class",
            "OccupationView class",
            "OrderedCollection class",
            "Paragraph class",
            "ParseNode class",
            "Path class",
            "Point class",
            "ProcessorScheduler class",
            "Project class",
            "Rectangle class",
            "Scanner class",
            "SourceFileManager class",
            "SplinesView class",
            "StandardSystemView class",
            "StringHolder class",
            "StringHolderView class",
            "Symbol class",
            "SymbolAsStringTest class",
            "SystemDictionary class",
            "SystemTracer class",
            "SystemView class",
            "SystemWorkspace class",
            "Text class",
            "TextCollectorView class",
            "TextEditor class",
            "TextList class",
            "TileableView class",
            "UndefinedObject class",
            "WindowsFileDirectory class",
            "WindowsFileSystem class",
            "WorkspaceView class",

            // LEVEL 1: 6 classes that depend on Level 0
            "BitEditor class",      // depends on: OrderedCollection, Point
            "FormEditor class",     // depends on: TextConstants (DefaultLineGrid)
            "Debugger class",       // depends on: NotifierView, Project
            "FormMenuView class",   // depends on: DisplayScreen
            "LargePositiveInteger class",  // depends on: DigitBuffer
            "Spline class",         // depends on: Point

            // LEVEL 2: 2 classes that depend on Level 1
            "LargeNegativeInteger class",  // depends on: LargePositiveInteger
            "Delay class"                  // depends on: SortedCollection (runs last)
        };
    }

    /// <summary>
    /// Reorders init expression indices according to dependency order.
    /// Maps class names from bundle to desired execution order, preserving any not in dependency list.
    /// Within each dependency level, sorts by bytecode length (shortest first).
    /// </summary>
    public static List<int> ReorderByDependencies(
        List<int> originalIndices,
        ColdStartBundle bundle,
        List<string> dependencyOrder)
    {
        var result = new List<int>();
        var remainingIndices = new HashSet<int>(originalIndices);

        // Group classes by dependency level
        // Since the dependency order list doesn't contain comment markers,
        // we need to manually split based on known level sizes from the comments:
        // LEVEL 0: 80 classes (indices 0-79)
        // LEVEL 1: 6 classes (indices 80-85)
        // LEVEL 2: 2 classes (indices 86-87)
        var levels = new List<List<string>>
        {
            dependencyOrder.Take(80).ToList(),      // Level 0
            dependencyOrder.Skip(80).Take(6).ToList(), // Level 1
            dependencyOrder.Skip(86).ToList()       // Level 2
        };

        // Process each level
        foreach (var levelClasses in levels)
        {
            // Find indices for all classes in this level
            var levelIndices = new List<(int index, int bytecodeLength)>();

            foreach (var className in levelClasses)
            {
                var bareClassName = className.Replace(" class", "");
                var expectedSource = $"{bareClassName} initialize";

                for (int i = 0; i < bundle.InitExpressions.Length; i++)
                {
                    if (!remainingIndices.Contains(i))
                        continue;

                    var initExpr = bundle.InitExpressions[i];
                    if (initExpr.Source == expectedSource)
                    {
                        // Get bytecode length - handle both int[] and base64 string formats
                        int bytecodeLength = 0;
                        if (initExpr.Bytecodes is int[] intArray)
                        {
                            bytecodeLength = intArray.Length;
                        }
                        else if (initExpr.Bytecodes is string base64String)
                        {
                            bytecodeLength = Convert.FromBase64String(base64String).Length;
                        }

                        levelIndices.Add((i, bytecodeLength));
                        remainingIndices.Remove(i);
                        break;
                    }
                }
            }

            // Sort this level by bytecode length (shortest first)
            var sortedLevel = levelIndices.OrderBy(x => x.bytecodeLength).ToList();
            result.AddRange(sortedLevel.Select(x => x.index));
        }

        // Add any remaining indices that weren't in dependency order (at the end)
        // These are init expressions that don't match our dependency analysis
        if (remainingIndices.Count > 0)
        {
            Console.WriteLine($"  ⚠️  {remainingIndices.Count} init expressions not in dependency order (appending at end)");
            result.AddRange(remainingIndices.OrderBy(i => i));
        }

        return result;
    }
}
