using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Partial class containing debug and diagnostic methods.
/// </summary>
public partial class VirtualMachine
{
    private void DumpSenderChain(SmalltalkOOP contextOOP, int maxDepth)
    {
        Console.WriteLine($"  SENDER CHAIN (showing up to {maxDepth} contexts):");

        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        var currentOOP = contextOOP;
        int depth = 0;

        while (currentOOP != SmalltalkOOP.Null && currentOOP != SpecialObjectsArray.Instance.Nil && depth < maxDepth)
        {
            if (currentOOP.IsSmallInteger)
            {
                Console.WriteLine($"    [{depth}] INVALID - SmallInteger {currentOOP.AsSmallInteger()}");
                break;
            }

            try
            {
                var methodOOP = _heapManager.GetInstanceVariable(currentOOP, methodIndex);

                // Get selector from method's literal[0] (stored at indexed slot 1 in our format)
                // indexed slot 0 = header, indexed slot 1 = literal[0] (selector)
                // Blocks don't have selectors, they have other literals at literal[0]
                string selectorName = "<unknown>";
                try
                {
                    var selectorOOP = GetMethodLiteral(methodOOP, 1);
                    var symbolName = GetSymbolName(selectorOOP);

                    // Check if this is a selector (Symbol) or something else (likely a block)
                    if (symbolName != null && selectorOOP != SpecialObjectsArray.Instance.Nil)
                    {
                        selectorName = symbolName;
                    }
                    else if (selectorOOP == SpecialObjectsArray.Instance.Nil)
                    {
                        // No literal[0] - this is a block
                        selectorName = "<block>";
                    }
                    else if (selectorOOP.IsSmallInteger)
                    {
                        // Probably a block with SmallInt literal
                        selectorName = "<block>";
                    }
                    else
                    {
                        // Has literal[0] but it's not a Symbol - likely a block
                        selectorName = "<block>";
                    }
                }
                catch (Exception ex)
                {
                    selectorName = $"<error: {ex.Message}>";
                }

                var classOOP = _heapManager.GetClassOOP(currentOOP);
                var className = GetClassName(classOOP) ?? "?";

                // Get symbolic names for context and method
                string contextName = GetLocalId(currentOOP) ?? "";
                string methodName = GetLocalId(methodOOP) ?? "";

                // Format context info
                string contextInfo = string.IsNullOrEmpty(contextName)
                    ? $"context=OOP({currentOOP.RawValue})"
                    : $"context={contextName} OOP({currentOOP.RawValue})";

                // Format method info
                string methodInfo = string.IsNullOrEmpty(methodName)
                    ? $"method=OOP({methodOOP.RawValue})"
                    : $"method={methodName} OOP({methodOOP.RawValue})";

                Console.WriteLine($"    [{depth}] {className}>>{selectorName} ({contextInfo}, {methodInfo})");

                var senderOOP = _heapManager.GetInstanceVariable(currentOOP, senderIndex);
                currentOOP = senderOOP;
                depth++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    [{depth}] ERROR reading context: {ex.Message}");
                break;
            }
        }

        if (depth >= maxDepth)
        {
            Console.WriteLine($"    ... (truncated, chain continues)");
        }
    }

    private void DumpStack(string label)
    {
        Console.WriteLine($"  [{label}] STACK:");
        var stackpOOP = _heapManager.GetInstanceVariable(_contextManager.ActiveContext, VMConstants.CONTEXT_STACKP_INDEX);
        int stackp = stackpOOP.AsSmallInteger();

        Console.WriteLine($"    stackp={stackp}");

        // Show top 5 stack entries
        int startIdx = Math.Max(0, stackp - 5);
        for (int i = startIdx; i < stackp; i++)
        {
            var value = _heapManager.GetIndexedOOP(_contextManager.ActiveContext, VMConstants.CONTEXT_TEMP_FRAME_START + i);
            string valueDesc = value.IsSmallInteger ? $"SmallInteger({value.AsSmallInteger()})" :
                               value == SpecialObjectsArray.Instance.Nil ? "nil" :
                               $"{_heapManager.GetClassName(value)} OOP({value})";
            Console.WriteLine($"      [{i}] = {valueDesc}");
        }
    }

    private string DescribeLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"Symbol(#{name})";
        }

        if (className == "Association")
        {
            var keyOOP = _heapManager.GetInstanceVariable(oop, VMConstants.ASSOCIATION_KEY_INDEX);
            string? keyName = GetSymbolName(keyOOP);
            return $"Association(#{keyName})";
        }

        if (className == "CompiledMethod")
        {
            return $"CompiledMethod(block)";
        }

        return $"{className}({oop})";
    }

    private string DescribeValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        if (oop == SpecialObjectsArray.Instance.Nil)
        {
            return "nil";
        }

        if (oop == SpecialObjectsArray.Instance.True)
        {
            return "true";
        }

        if (oop == SpecialObjectsArray.Instance.False)
        {
            return "false";
        }

        string className = _heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = GetSymbolName(oop);
            return $"#{name}";
        }

        return $"{className}({oop})";
    }

    private void DumpBytecodes(SmalltalkOOP methodOOP, string prefix)
    {
        var bytecodes = GetMethodBytecodes(methodOOP);
        Console.WriteLine($"{prefix}Bytecode dump ({bytecodes.Length} bytes):");

        int i = 0;
        while (i < bytecodes.Length && i < 50)
        {
            byte b = bytecodes[i];
            int offset = i;
            string desc = b switch
            {
                0x00 => "PUSH_RECEIVER",
                0x01 => "PUSH_TRUE",
                0x02 => "PUSH_FALSE",
                0x03 => "PUSH_NIL",
                0x04 => $"STORE_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x05 => $"POP_STORE_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x06 => $"PUSH_LITERAL {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x10 => $"PUSH_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x11 => $"PUSH_TEMP {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x20 => $"PUSH_LITERAL {(i + 1 < bytecodes.Length ? bytecodes[++i].ToString() : "?")}",
                0x5C => "RETURN_STACK_TOP",
                0x5D => "RETURN_NIL",
                0x5E => "RETURN_TRUE",
                0x5F => "RETURN_FALSE",
                0x87 => "POP_STACK_TOP",
                >= 0xB0 and <= 0xBF => $"SEND_ARITH (selector={(b - 0xB0)})",
                >= 0xD0 and <= 0xDF => $"SEND1 (selector={(b - 0xD0)})",
                >= 0xF0 and <= 0xFF => $"SEND2 (selector={(b - 0xF0)})",
                _ => $"0x{b:X2}"
            };

            Console.WriteLine($"{prefix}  [{offset}] {desc}");
            i++;
        }
    }

    /// <summary>
    /// Helper method to format an OOP value for debugging
    /// </summary>
    private string FormatOOPValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return $"SmallInteger({oop.AsSmallInteger()})";
        if (oop == SpecialObjectsArray.Instance.Nil)
            return "nil";
        if (oop == SpecialObjectsArray.Instance.True)
            return "true";
        if (oop == SpecialObjectsArray.Instance.False)
            return "false";

        try
        {
            string className = _heapManager.GetClassName(oop);
            return $"{className} OOP({oop})";
        }
        catch
        {
            return $"OOP({oop})";
        }
    }
}
