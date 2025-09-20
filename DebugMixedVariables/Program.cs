using System;
using System.Collections.Generic;
using StandaloneCompiler;

class DebugMixedVariables
{
    static void Main()
    {
        Console.WriteLine("=== Debug Mixed Variable Compilation ===\n");
        
        var compiler = new BytecodeCompiler();
        var instanceVars = new List<string> { "value" };
        var classVars = new Dictionary<string, object> { ["Default"] = new object() };
        var poolVars = new Dictionary<string, object> { ["MaxValue"] = new object() };
        
        // The bang should be at the very end, not after each statement
        var methodSource = @"test
    | temp |
    temp := value.
    ^temp!";
        
        Console.WriteLine($"Method source:\n{methodSource}\n");
        
        var result = compiler.CompileMethod(methodSource, instanceVars, classVars, poolVars);
        
        Console.WriteLine($"Bytecodes: [{string.Join(", ", result.Bytecode)}]");
        Console.WriteLine($"Literals: {LiteralsToString(result.Literals)}");
        
        // Analyze each bytecode
        Console.WriteLine("\nBytecode analysis:");
        for (int i = 0; i < result.Bytecode.Length; i++)
        {
            Console.WriteLine($"  [{i}] = {result.Bytecode[i]:X2} ({result.Bytecode[i]}) : {DecodeBytecode(result.Bytecode[i], result.Literals)}");
        }
    }
    
    static string LiteralsToString(object[] literals)
    {
        var parts = new List<string>();
        for (int i = 0; i < literals.Length; i++)
        {
            if (literals[i] is Association assoc)
            {
                parts.Add($"#{i}: Association({assoc.Key} -> {assoc.Value ?? "nil"})");
            }
            else if (literals[i] is Symbol sym)
            {
                parts.Add($"#{i}: Symbol({sym.Value})");
            }
            else if (literals[i] == null)
            {
                parts.Add($"#{i}: null");
            }
            else
            {
                parts.Add($"#{i}: {literals[i].GetType().Name}({literals[i]})");
            }
        }
        return string.Join(", ", parts);
    }
    
    static string DecodeBytecode(byte bytecode, object[] literals)
    {
        return bytecode switch
        {
            >= 0 and <= 15 => $"PushReceiverVariable {bytecode}",
            >= 16 and <= 31 => $"PushTemporary {bytecode - 16}",
            >= 32 and <= 63 => $"PushLiteralConstant #{bytecode - 32}",
            >= 64 and <= 71 => $"PushLiteralConstant #{bytecode - 64} (short)",
            >= 72 and <= 79 => $"PushLiteralVariable #{bytecode - 72}",
            >= 80 and <= 95 => $"PushLiteralVariable #{bytecode - 80} (extended)",
            >= 96 and <= 103 => $"StoreAndPopReceiverVariable {bytecode - 96}",
            >= 104 and <= 111 => $"StoreAndPopTemporary {bytecode - 104}",
            112 => "PushReceiver",
            113 => "PushTrue", 
            114 => "PushFalse",
            115 => "PushNil",
            116 => "Push -1",
            117 => "Push 0",
            118 => "Push 1",
            119 => "Push 2",
            120 => "ReturnReceiver",
            121 => "ReturnTrue",
            122 => "ReturnFalse",
            123 => "ReturnNil",
            124 => "ReturnTop",
            125 => "ReturnTopFromBlock",
            135 => "Pop",
            136 => "Dup",
            _ => $"Unknown/Extended({bytecode:X2})"
        };
    }
}