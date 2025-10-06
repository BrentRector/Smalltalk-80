using System.Text;

namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Pretty printer for Smalltalk-80 classes, displaying comprehensive class information
/// including superclass, metaclass, instance variables, and all methods with bytecodes.
/// </summary>
public static class ClassPrettyPrinter
{
    /// <summary>
    /// Generates a comprehensive formatted dump of a class.
    /// </summary>
    public static string DumpClass(ISmalltalkHeap heap, string className)
    {
        var classInfo = heap.GetClassInfo(className);
        if (classInfo == null)
            return $"Class '{className}' not found.";

        var output = new StringBuilder();

        // Header
        output.AppendLine("================================================================================");
        output.AppendLine($"Class: {classInfo.ClassName}");
        output.AppendLine("================================================================================");
        output.AppendLine();

        // Basic information
        output.AppendLine($"Superclass:    {classInfo.SuperclassName}");
        output.AppendLine($"Metaclass ID:  {classInfo.MetaclassId}");
        output.AppendLine($"Format:        {classInfo.Format}");
        output.AppendLine($"Instance Size: {classInfo.InstanceSize}");
        output.AppendLine();

        // Instance variables
        if (classInfo.InstanceVariableNames.Length > 0)
        {
            output.AppendLine("Instance Variables:");
            for (int i = 0; i < classInfo.InstanceVariableNames.Length; i++)
            {
                output.AppendLine($"  [{i}] {classInfo.InstanceVariableNames[i]}");
            }
            output.AppendLine();
        }

        // Methods
        if (classInfo.Selectors.Length > 0)
        {
            output.AppendLine($"Methods ({classInfo.Selectors.Length}):");
            output.AppendLine("--------------------------------------------------------------------------------");
            output.AppendLine();

            foreach (var selector in classInfo.Selectors.OrderBy(s => s))
            {
                var methodInfo = heap.GetMethod(classInfo.ClassId, selector);
                if (methodInfo != null)
                {
                    output.Append(FormatMethod(methodInfo, heap));
                    output.AppendLine();
                }
            }
        }
        else
        {
            output.AppendLine("No methods defined.");
            output.AppendLine();
        }

        output.AppendLine("================================================================================");
        return output.ToString();
    }

    /// <summary>
    /// Formats a single method with all details.
    /// </summary>
    private static string FormatMethod(MethodInfo method, ISmalltalkHeap heap)
    {
        var output = new StringBuilder();

        // Method header
        output.AppendLine($"Selector: {method.Selector}");
        output.AppendLine($"  Method ID:      {method.MethodId}");
        output.AppendLine($"  Header Word:    0x{method.HeaderWord:X8} ({method.HeaderWord})");
        output.AppendLine($"  Arguments:      {method.ArgumentCount}");
        output.AppendLine($"  Temporaries:    {method.TemporaryCount}");
        output.AppendLine($"  Large Context:  {method.LargeContext}");
        output.AppendLine($"  Primitive:      {(method.PrimitiveIndex > 0 ? method.PrimitiveIndex.ToString() : "none")}");
        output.AppendLine();

        // Literals
        if (method.LiteralIds.Length > 0)
        {
            output.AppendLine("  Literals:");
            for (int i = 0; i < method.LiteralIds.Length; i++)
            {
                var litId = method.LiteralIds[i];
                var litDisplay = heap.GetObjectDisplay(litId);
                output.AppendLine($"    [{i}] {litDisplay} (OOP: {litId})");
            }
            output.AppendLine();
        }

        // Bytecodes - Symbolic (with hex bytes shown after each instruction)
        if (method.Bytecodes.Length > 0)
        {
            output.AppendLine("  Bytecodes:");
            string disassembly = BytecodeDisassembler.Disassemble(
                method.Bytecodes,
                method.LiteralIds,
                heap.GetObjectDisplay,
                heap.GetOptimizedSelectors());
            output.Append(disassembly);
        }
        else
        {
            output.AppendLine("  No bytecodes.");
        }

        output.AppendLine("--------------------------------------------------------------------------------");
        return output.ToString();
    }
}
