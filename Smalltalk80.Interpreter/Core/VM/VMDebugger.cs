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
        // Get executable bytecodes (excludes 3-byte source offset)
        var bytecodes = helpers.GetMethodBytecodes(methodOOP);
        Console.WriteLine($"{prefix}Bytecode dump ({bytecodes.Length} executable bytes):");
        Console.WriteLine($"{prefix}  Raw hex: {BitConverter.ToString(bytecodes).Replace("-", " ")}");

        // Decode executable bytecodes
        int i = 0;
        while (i < bytecodes.Length && i < 50)
        {
            byte b = bytecodes[i];
            int offset = i;
            string desc;

            // Blue Book bytecode ranges
            if (b >= 0x00 && b <= 0x0F)
            {
                desc = $"PUSH_IVAR_{b}";
            }
            else if (b >= 0x10 && b <= 0x1F)
            {
                desc = $"PUSH_TEMP_{b - 0x10}";
            }
            else if (b >= 0x20 && b <= 0x3F)
            {
                desc = $"PUSH_LITERAL_{b - 0x20}";
            }
            else if (b >= 0x40 && b <= 0x5F)
            {
                desc = $"PUSH_LITERAL_VAR_{b - 0x40}";
            }
            else if (b >= 0x60 && b <= 0x67)
            {
                desc = $"POP_STORE_IVAR_{b - 0x60}";
            }
            else if (b >= 0x68 && b <= 0x6F)
            {
                desc = $"POP_STORE_TEMP_{b - 0x68}";
            }
            else if (b >= 0x70 && b <= 0x77)
            {
                desc = b switch
                {
                    0x70 => "PUSH_SELF",
                    0x71 => "PUSH_TRUE",
                    0x72 => "PUSH_FALSE",
                    0x73 => "PUSH_NIL",
                    0x74 => "PUSH_MINUS_ONE",
                    0x75 => "PUSH_ZERO",
                    0x76 => "PUSH_ONE",
                    0x77 => "PUSH_TWO",
                    _ => $"PUSH_SPECIAL_{b - 0x70}"
                };
            }
            else if (b >= 0x78 && b <= 0x7F)
            {
                desc = b switch
                {
                    0x78 => "RETURN_SELF",
                    0x79 => "RETURN_TRUE",
                    0x7A => "RETURN_FALSE",
                    0x7B => "RETURN_NIL",
                    0x7C => "RETURN_STACK_TOP",
                    0x7D => "RETURN_STACK_TOP_BLOCK",
                    _ => $"RETURN_0x{b:X2}"
                };
            }
            else if (b >= 0x80 && b <= 0x87)
            {
                desc = b == 0x87 ? "POP_STACK_TOP" : $"EXTENDED_0x{b:X2}";
            }
            else if (b >= 0xB0 && b <= 0xBF)
            {
                desc = $"SEND_ARITH_{b - 0xB0}";
            }
            else if (b >= 0xC0 && b <= 0xCF)
            {
                desc = $"SEND_SPECIAL_{b - 0xC0}";
            }
            else if (b >= 0xD0 && b <= 0xDF)
            {
                desc = $"SEND0_{b - 0xD0}";
            }
            else if (b >= 0xE0 && b <= 0xEF)
            {
                desc = $"SEND1_{b - 0xE0}";
            }
            else if (b >= 0xF0 && b <= 0xFF)
            {
                desc = $"SEND2_{b - 0xF0}";
            }
            else
            {
                desc = "";
            }

            Console.WriteLine($"{prefix}  [{offset}] 0x{b:X2} {desc}");
            i++;
        }

        // Now show the source file offset (last 3 bytes of raw bytecode array)
        try
        {
            int indexedSize = heapManager.GetIndexedSize(methodOOP);
            if (indexedSize > 0)
            {
                var bytecodeArrayOOP = heapManager.GetIndexedOOP(methodOOP, indexedSize - 1);
                int totalByteCount = heapManager.GetIndexedSize(bytecodeArrayOOP);

                if (totalByteCount >= 3)
                {
                    // Read last 3 bytes
                    byte b0 = heapManager.GetIndexedByte(bytecodeArrayOOP, totalByteCount - 3);
                    byte b1 = heapManager.GetIndexedByte(bytecodeArrayOOP, totalByteCount - 2);
                    byte b2 = heapManager.GetIndexedByte(bytecodeArrayOOP, totalByteCount - 1);

                    // Blue Book uses little-endian: offset = b0 + (b1 << 8) + (b2 << 16)
                    int sourceOffset = b0 | (b1 << 8) | (b2 << 16);

                    Console.WriteLine($"{prefix}  Source file offset: {sourceOffset} (0x{sourceOffset:X6}) [bytes: 0x{b0:X2} 0x{b1:X2} 0x{b2:X2}]");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{prefix}  Source file offset: Error reading - {ex.Message}");
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

    /// <inheritdoc/>
    public void DumpCrashDiagnostics(string errorMessage)
    {
        Console.WriteLine($"\n=== CRASH DIAGNOSTICS ===");
        Console.WriteLine($"Error: {errorMessage}");
        Console.WriteLine();

        try
        {
            var activeContext = contextManager.ActiveContext;
            var contextClass = heapManager.GetClassOOP(activeContext);
            var contextClassName = helpers.GetClassName(contextClass) ?? "Unknown";
            bool isBlockContext = (contextClass == metadata.BlockContextClassOOP);

            Console.WriteLine($"Active Context: OOP({activeContext}), Class: {contextClassName}");

            // Get method OOP
            SmalltalkOOP methodOOP;
            if (isBlockContext)
            {
                // Traverse to home MethodContext
                var homeOOP = heapManager.GetInstanceVariable(activeContext, VMConstants.BLOCKCONTEXT_HOME_INDEX);
                methodOOP = heapManager.GetInstanceVariable(homeOOP, VMConstants.CONTEXT_METHOD_INDEX);
                Console.WriteLine($"  Home MethodContext: OOP({homeOOP})");
            }
            else
            {
                methodOOP = heapManager.GetInstanceVariable(activeContext, VMConstants.CONTEXT_METHOD_INDEX);
            }

            // Get method class and selector
            try
            {
                var methodClass = heapManager.GetInstanceVariable(methodOOP, 0); // homeClass at instance variable 0
                var methodClassName = helpers.GetClassName(methodClass) ?? "Unknown";

                // Try to get selector name - use localId first (more reliable, works with 0 literals)
                string selectorName = "unknown";

                // Try GetLocalId which has format "method:ClassName>>selector"
                var localId = helpers.GetLocalId(methodOOP);
                if (!string.IsNullOrEmpty(localId) && localId.Contains(">>"))
                {
                    int arrowPos = localId.IndexOf(">>");
                    selectorName = localId.Substring(arrowPos + 2);
                }
                else
                {
                    // Fall back to reading from literal[0] if method has literals
                    int indexedSize = heapManager.GetIndexedSize(methodOOP);
                    int literalCount = indexedSize - 2; // indexed size includes header + literals + bytecode array

                    if (literalCount > 0)
                    {
                        try
                        {
                            var selectorOOP = helpers.GetMethodLiteral(methodOOP, 1); // selector at literal[0], indexed slot 1
                            selectorName = helpers.GetSymbolName(selectorOOP) ?? "unknown";
                        }
                        catch
                        {
                            // Keep "unknown" if literal access fails
                        }
                    }
                }

                Console.WriteLine($"  Method: {methodClassName}>>{selectorName}, OOP({methodOOP})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Method: OOP({methodOOP}) - Error getting selector: {ex.Message}");
            }

            // Get instruction pointer
            var ipOOP = heapManager.GetInstanceVariable(activeContext, VMConstants.CONTEXT_PC_INDEX);
            int ip = ipOOP.AsSmallInteger();
            Console.WriteLine($"  Instruction Pointer (IP): {ip}");

            // Get stack pointer
            var spOOP = heapManager.GetInstanceVariable(activeContext, VMConstants.CONTEXT_STACKP_INDEX);
            int sp = spOOP.AsSmallInteger();
            Console.WriteLine($"  Stack Pointer (SP): {sp}");

            // Show current bytecode
            try
            {
                var bytecodes = helpers.GetMethodBytecodes(methodOOP);
                if (ip > 0 && ip <= bytecodes.Length)
                {
                    byte currentBytecode = bytecodes[ip - 1]; // IP points to NEXT bytecode
                    Console.WriteLine($"  Current Bytecode: 0x{currentBytecode:X2} at IP={ip - 1}");
                }
                else if (ip == 0)
                {
                    Console.WriteLine($"  Current Bytecode: <IP at start>");
                }
                else
                {
                    Console.WriteLine($"  Current Bytecode: <IP beyond bytecode array, length={bytecodes.Length}>");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Current Bytecode: Error reading - {ex.Message}");
            }

            // Show method structure
            try
            {
                int indexedSize = heapManager.GetIndexedSize(methodOOP);
                int literalCount = indexedSize - 2; // indexed size includes header + literals + bytecode array
                Console.WriteLine($"  Method Structure: indexed size={indexedSize}, literals={literalCount}");

                // Show first few literals
                int literalsToShow = Math.Min(3, literalCount);
                for (int i = 0; i < literalsToShow; i++)
                {
                    try
                    {
                        var literalOOP = helpers.GetMethodLiteral(methodOOP, i + 1); // +1 because indexed slot 0 is header
                        string literalDesc = DescribeLiteral(literalOOP);
                        Console.WriteLine($"    Literal[{i}]: {literalDesc}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Literal[{i}]: Error - {ex.Message}");
                    }
                }
                if (literalCount > 3)
                {
                    Console.WriteLine($"    ... ({literalCount - 3} more literals)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Method Structure: Error - {ex.Message}");
            }

            // Show bytecodes
            try
            {
                Console.WriteLine();
                DumpBytecodes(methodOOP, "  ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Bytecodes: Error - {ex.Message}");
            }

            // Show stack contents
            Console.WriteLine();
            DumpStack("CRASH");

            // Show sender chain
            Console.WriteLine();
            DumpSenderChain(activeContext, 10);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError generating crash diagnostics: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }

        Console.WriteLine($"=== END CRASH DIAGNOSTICS ===\n");
    }
}
