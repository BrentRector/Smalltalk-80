using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides debugging and diagnostic output for VM execution.
/// Handles stack dumps, sender chain visualization, bytecode disassembly, and value formatting.
/// </summary>
/// <param name="heapManager">Heap manager for object access.</param>
/// <param name="contextManager">Context manager for active context access.</param>
/// <param name="helpers">Helper methods for symbol/class name resolution.</param>
/// <param name="metadata">VM metadata for known objects.</param>
internal sealed class VMDebugger(
    ISmalltalkHeap heapManager,
    IContextManager contextManager,
    IBytecodeContext helpers,
    VMMetadata metadata) : IVMDebugger
{
    /// <inheritdoc/>
    public void DumpSenderChain(SmalltalkOOP contextOOP, int maxDepth)
    {
        Console.WriteLine($"  SENDER CHAIN (showing up to {maxDepth} contexts):");

        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;

        var currentOOP = contextOOP;
        int depth = 0;

        while (currentOOP != heapManager.GetNil() && currentOOP != metadata.KnownObjects.Nil && depth < maxDepth)
        {
            if (currentOOP.IsSmallInteger)
            {
                Console.WriteLine($"    [{depth}] INVALID - SmallInteger {currentOOP.AsSmallInteger()}");
                break;
            }

            try
            {
                // Check if this is a BlockContext - they store nargs at index 3, not method
                // For BlockContext, we need to traverse to home MethodContext to get the method
                var currentClassOOP = heapManager.GetClassOOP(currentOOP);
                bool isBlockContext = (currentClassOOP == metadata.BlockContextClassOOP);

                SmalltalkOOP methodOOP;
                if (isBlockContext)
                {
                    // BlockContext: traverse home chain to find MethodContext
                    var homeOOP = currentOOP;
                    int traverseCount = 0;
                    while (traverseCount < 100) // Safety limit
                    {
                        homeOOP = heapManager.GetInstanceVariable(homeOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                        var homeClassOOP = heapManager.GetClassOOP(homeOOP);
                        if (homeClassOOP != metadata.BlockContextClassOOP)
                        {
                            // Found the MethodContext
                            break;
                        }
                        traverseCount++;
                    }
                    methodOOP = heapManager.GetInstanceVariable(homeOOP, methodIndex);
                }
                else
                {
                    methodOOP = heapManager.GetInstanceVariable(currentOOP, methodIndex);
                }

                // Get selector from method's literal[0] (stored at indexed slot 1 in our format)
                // indexed slot 0 = header, indexed slot 1 = literal[0] (selector)
                // Blocks don't have selectors, they have other literals at literal[0]
                string selectorName = "<unknown>";
                try
                {
                    // Check if methodOOP is valid before trying to access it
                    // SmallIntegers and special objects (nil=1, true=2, false=3) are not valid methods
                    if (methodOOP.IsSmallInteger)
                    {
                        selectorName = $"<invalid method: SmallInt {methodOOP.RawValue}>";
                    }
                    else if (methodOOP == metadata.KnownObjects.Nil)
                    {
                        selectorName = "<invalid method: nil>";
                    }
                    else if (methodOOP.RawValue <= 10)
                    {
                        // Very low OOP values are special objects, not methods
                        selectorName = $"<invalid method: OOP({methodOOP.RawValue})>";
                    }
                    else
                    {
                        var selectorOOP = helpers.GetMethodLiteral(methodOOP, 1);
                        var symbolName = helpers.GetSymbolName(selectorOOP);

                        // Check if this is a selector (Symbol) or something else (likely a block)
                        if (symbolName is not null && selectorOOP != metadata.KnownObjects.Nil)
                        {
                            selectorName = symbolName;
                        }
                        else if (selectorOOP == metadata.KnownObjects.Nil)
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
                }
                catch (Exception ex)
                {
                    selectorName = $"<error: {ex.Message}>";
                }

                var classOOP = heapManager.GetClassOOP(currentOOP);
                var className = helpers.GetClassName(classOOP) ?? "?";

                // Get symbolic names for context and method
                string contextName = helpers.GetLocalId(currentOOP) ?? "";
                string methodName = helpers.GetLocalId(methodOOP) ?? "";

                // Format context info
                string contextInfo = string.IsNullOrEmpty(contextName)
                    ? $"context=OOP({currentOOP.RawValue})"
                    : $"context={contextName} OOP({currentOOP.RawValue})";

                // Format method info
                string methodInfo = string.IsNullOrEmpty(methodName)
                    ? $"method=OOP({methodOOP.RawValue})"
                    : $"method={methodName} OOP({methodOOP.RawValue})";

                Console.WriteLine($"    [{depth}] {className}>>{selectorName} ({contextInfo}, {methodInfo})");

                var senderOOP = heapManager.GetInstanceVariable(currentOOP, senderIndex);
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

    /// <inheritdoc/>
    public void DumpStack(string label)
    {
        Console.WriteLine($"  [{label}] STACK:");
        var stackpOOP = heapManager.GetInstanceVariable(contextManager.ActiveContext, VMConstants.CONTEXT_STACKP_INDEX);
        int stackp = stackpOOP.AsSmallInteger();

        Console.WriteLine($"    stackp={stackp}");

        // Show top 5 stack entries
        int startIdx = Math.Max(0, stackp - 5);
        for (int i = startIdx; i < stackp; i++)
        {
            var value = heapManager.GetIndexedOOP(contextManager.ActiveContext, VMConstants.CONTEXT_TEMP_FRAME_START + i);
            string valueDesc = value.IsSmallInteger
                ? $"SmallInteger({value.AsSmallInteger()})"
                : value == metadata.KnownObjects.Nil
                    ? "nil"
                    : $"{heapManager.GetClassName(value)} OOP({value})";
            Console.WriteLine($"      [{i}] = {valueDesc}");
        }
    }

    /// <inheritdoc/>
    public string DescribeLiteral(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        string className = heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = helpers.GetSymbolName(oop);
            return $"Symbol(#{name})";
        }

        if (className == "Association")
        {
            var keyOOP = heapManager.GetInstanceVariable(oop, VMConstants.ASSOCIATION_KEY_INDEX);
            string? keyName = helpers.GetSymbolName(keyOOP);
            return $"Association(#{keyName})";
        }

        if (className == "CompiledMethod")
        {
            return $"CompiledMethod(block)";
        }

        return $"{className}({oop})";
    }

    /// <inheritdoc/>
    public string DescribeValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
        {
            return $"SmallInteger({oop.AsSmallInteger()})";
        }

        if (oop == metadata.KnownObjects.Nil)
        {
            return "nil";
        }

        if (oop == metadata.KnownObjects.True)
        {
            return "true";
        }

        if (oop == metadata.KnownObjects.False)
        {
            return "false";
        }

        string className = heapManager.GetClassName(oop);

        if (className == "Symbol")
        {
            string? name = helpers.GetSymbolName(oop);
            return $"#{name}";
        }

        return $"{className}({oop})";
    }

    /// <inheritdoc/>
    public void DumpBytecodes(SmalltalkOOP methodOOP, string prefix)
    {
        var bytecodes = helpers.GetMethodBytecodes(methodOOP);
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

    /// <inheritdoc/>
    public string FormatOOPValue(SmalltalkOOP oop)
    {
        if (oop.IsSmallInteger)
            return $"SmallInteger({oop.AsSmallInteger()})";
        if (oop == metadata.KnownObjects.Nil)
            return "nil";
        if (oop == metadata.KnownObjects.True)
            return "true";
        if (oop == metadata.KnownObjects.False)
            return "false";

        try
        {
            string className = heapManager.GetClassName(oop);
            return $"{className} OOP({oop})";
        }
        catch
        {
            return $"OOP({oop})";
        }
    }
}
