namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Provides the explicit execution order for init expressions.
/// </summary>
public static class InitExpressionOrdering
{
    /// <summary>
    /// Returns the explicit execution order for init expressions.
    /// </summary>
    public static List<string> GetExecutionOrder()
    {
        return new List<string>
        {
            "Behavior init",  // Must run early - initializes SelectorsOfConflictMethods etc.
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
            "WindowsPath initialize",  // Must run before SystemDictionary (uses applicationPath:)
            "SystemDictionary initialize",
            "SystemTracer initialize",
            "Text initialize",
            "TextList initialize",
            "Color initialize",
            "SyntaxStyler initialize",
            "WindowsDirectory initialize",
            "WindowsFileSystem initialize",
            "WindowsFileStream initialize",
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
            "FormMenuView initialize",
            "TextCollector initialize",

            // BlockContext Diagnostic Tests (9 individual variable tests)
            "DiagOa initialize",
            "DiagOt1 initialize",
            "DiagOt2 initialize",
            "DiagMa initialize",
            "DiagMt1 initialize",
            "DiagMt2 initialize",
            "DiagIa initialize",
            "DiagIt1 initialize",
            "DiagIt2 initialize",
            "DiagPow10 initialize",
            "DiagZero initialize",

            // BlockContext Test Runner (runs all 120 tests, expected result: 1191)
            "BlockContextTestRunner initialize"

            // NOTE: JsonTest runAll is NOT run during init - run manually from Workspace:
            //   JsonTest runAll
        };
    }
}
