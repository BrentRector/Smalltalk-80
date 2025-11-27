namespace Smalltalk80.Interpreter.Engine.InitExecution;

/// <summary>
/// Provides the explicit execution order for init expressions.
/// </summary>
public static class InitExpressionOrdering
{
    /// <summary>
    /// Returns the explicit execution order for init expressions.
    /// This is the exact order they should run, with FormDisplayTest last.
    /// </summary>
    public static List<string> GetExecutionOrder()
    {
        return new List<string>
        {
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
            "FormMenuView initialize",
            "TextCollector initialize",
            "FormDisplayTest initialize"  // Must run last - depends on FormMenuView loading Forms
        };
    }
}
