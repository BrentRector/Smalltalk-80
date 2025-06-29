using System;
using System.Collections.Generic;

/// <summary>
///     Simple assembler used for generating bytecode arrays from a textual
///     representation.
/// </summary>
public static class BytecodeAssembler
{
    /// <summary>
    ///     Converts mnemonic instructions into their bytecode form.
    /// </summary>
    /// <param name="lines">The lines containing assembly instructions.</param>
    /// <returns>The assembled bytecode.</returns>
    public static byte[] Assemble(string[] lines)
    {
        var list = new List<byte>();
        foreach (var line in lines) {
            switch (line.Trim()) {
                case "push_temp_0": list.Add(16); break;
                case "push_temp_1": list.Add(17); break;
                case "send_+": list.Add(128); break;
                case "return": list.Add(124); break;
                default: throw new Exception($"Unknown bytecode mnemonic: {line}");
            }
        }
        return list.ToArray();
    }
}