namespace Smalltalk80.Interpreter.Execution;

using System.Text;

/// <summary>
/// Disassembles Smalltalk-80 bytecode instructions according to the Blue Book specification.
/// </summary>
public static class BytecodeDisassembler
{
    /// <summary>
    /// Disassembles an array of bytecodes into human-readable mnemonics.
    /// </summary>
    /// <param name="bytecodes">The bytecode array to disassemble.</param>
    /// <returns>A string containing the disassembled bytecode with offsets and mnemonics.</returns>
    public static string Disassemble(byte[] bytecodes)
    {
        if (bytecodes == null || bytecodes.Length == 0)
        {
            return "(empty)";
        }

        var sb = new StringBuilder();
        var offset = 0;

        while (offset < bytecodes.Length)
        {
            var bytecode = bytecodes[offset];
            var mnemonic = GetMnemonicWithOperands(bytecodes, ref offset);
            sb.AppendLine($"    {offset - 1:X4}: {bytecode:X2}          {mnemonic}");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Gets the mnemonic for a bytecode instruction, consuming any extension bytes.
    /// </summary>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <param name="offset">The current offset, will be updated to point past the instruction.</param>
    /// <returns>The mnemonic string for the instruction.</returns>
    private static string GetMnemonicWithOperands(byte[] bytecodes, ref int offset)
    {
        if (offset >= bytecodes.Length)
        {
            return "(invalid)";
        }

        var bytecode = bytecodes[offset++];
        return GetMnemonic(bytecode, bytecodes, ref offset);
    }

    /// <summary>
    /// Gets the mnemonic for a single bytecode.
    /// </summary>
    /// <param name="bytecode">The bytecode value.</param>
    /// <param name="bytecodes">The full bytecode array for extended instructions.</param>
    /// <param name="offset">The current offset for reading extension bytes.</param>
    /// <returns>The mnemonic string for the bytecode.</returns>
    private static string GetMnemonic(byte bytecode, byte[]? bytecodes, ref int offset)
    {
        return bytecode switch
        {
            // Push receiver variable
            >= 0x00 and <= 0x0F => $"push receiver[{bytecode & 0x0F}]",

            // Push temporary variable
            >= 0x10 and <= 0x1F => $"push temp[{bytecode & 0x0F}]",

            // Push literal constant
            >= 0x20 and <= 0x3F => $"push literal[{bytecode & 0x1F}]",

            // Push literal variable
            >= 0x40 and <= 0x5F => $"push literal var[{bytecode & 0x1F}]",

            // Pop and store receiver variable
            >= 0x60 and <= 0x67 => $"pop store receiver[{bytecode & 0x07}]",

            // Pop and store temporary variable
            >= 0x68 and <= 0x6F => $"pop store temp[{bytecode & 0x07}]",

            // Push special constants
            0x70 => "push self",
            0x71 => "push true",
            0x72 => "push false",
            0x73 => "push nil",
            0x74 => "push -1",
            0x75 => "push 0",
            0x76 => "push 1",
            0x77 => "push 2",

            // Return instructions
            0x78 => "return self",
            0x79 => "return true",
            0x7A => "return false",
            0x7B => "return nil",
            0x7C => "return top",
            0x7D => "block return",

            // Unused
            0x7E or 0x7F => "(unused)",

            // Extended instructions
            >= 0x80 and <= 0x8F => DecodeExtended(bytecode, bytecodes, ref offset),

            // Send literal selector with no arguments
            >= 0x90 and <= 0xAF => $"send literal[{bytecode & 0x1F}] (0 args)",

            // Send literal selector with one argument
            >= 0xB0 and <= 0xCF => $"send literal[{bytecode & 0x1F}] (1 arg)",

            // Send literal selector with two arguments
            >= 0xD0 and <= 0xDF => $"send literal[{bytecode & 0x0F}] (2 args)",

            // Jump instructions
            >= 0xE0 and <= 0xE7 => DecodeShortJump(bytecode, true),
            >= 0xE8 and <= 0xEF => DecodeShortJump(bytecode, false),

            // Special sends
            >= 0xF0 and <= 0xFF => DecodeSpecialSend(bytecode)
        };
    }

    /// <summary>
    /// Decodes extended bytecode instructions.
    /// </summary>
    private static string DecodeExtended(byte bytecode, byte[]? bytecodes, ref int offset)
    {
        if (bytecodes == null || offset >= bytecodes.Length)
        {
            return $"extended (incomplete)";
        }

        var extByte = bytecodes[offset++];
        var type = (bytecode >> 1) & 0x07;

        return type switch
        {
            0 => $"extended push receiver[{extByte}]",
            1 => $"extended push temp[{extByte}]",
            2 => $"extended push literal[{extByte}]",
            3 => $"extended push literal var[{extByte}]",
            4 => $"extended store receiver[{extByte}]",
            5 => $"extended store temp[{extByte}]",
            6 => $"extended pop store receiver[{extByte}]",
            7 => $"extended pop store temp[{extByte}]",
            _ => $"extended (unknown type {type})"
        };
    }

    /// <summary>
    /// Decodes short jump instructions.
    /// </summary>
    private static string DecodeShortJump(byte bytecode, bool conditional)
    {
        var offset = (bytecode & 0x07) + 1;
        return conditional ? $"jump if false {offset}" : $"jump {offset}";
    }

    /// <summary>
    /// Decodes special send instructions.
    /// </summary>
    private static string DecodeSpecialSend(byte bytecode)
    {
        var specialSelectors = new[]
        {
            "+", "-", "<", ">", "<=", ">=", "=", "~=",
            "*", "/", "\\\\", "@", "bitShift:", "//", "bitAnd:", "bitOr:"
        };

        var index = bytecode & 0x0F;
        if (index < specialSelectors.Length)
        {
            var argCount = GetSpecialSelectorArgCount(specialSelectors[index]);
            return $"send {specialSelectors[index]} ({argCount} arg{(argCount == 1 ? "" : "s")})";
        }

        return $"special send {index}";
    }

    /// <summary>
    /// Gets the argument count for a special selector.
    /// </summary>
    private static int GetSpecialSelectorArgCount(string selector)
    {
        return selector switch
        {
            "+" or "-" or "*" or "/" or "\\\\" or "//" or
            "<" or ">" or "<=" or ">=" or "=" or "~=" or
            "@" or "bitShift:" or "bitAnd:" or "bitOr:" => 1,
            _ => 0
        };
    }

    /// <summary>
    /// Gets a simple one-line disassembly of bytecodes.
    /// </summary>
    /// <param name="bytecodes">The bytecode array.</param>
    /// <returns>A single-line string with bytecode values and mnemonics.</returns>
    public static string GetSimpleDisassembly(byte[] bytecodes)
    {
        if (bytecodes == null || bytecodes.Length == 0)
        {
            return "(empty)";
        }

        var mnemonics = new List<string>();
        var offset = 0;

        while (offset < bytecodes.Length)
        {
            var bytecode = bytecodes[offset];
            var mnemonic = GetMnemonicWithOperands(bytecodes, ref offset);
            mnemonics.Add($"{bytecode:X2}:{mnemonic}");
        }

        return string.Join(", ", mnemonics);
    }
}