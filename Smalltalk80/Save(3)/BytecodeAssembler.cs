using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
///     Simple assembler used for generating bytecode arrays from a textual
///     representation.
/// </summary>
public static class BytecodeAssembler
{
    /// <summary>
    ///     Dictionary mapping mnemonic strings to the corresponding
    ///     <see cref="Bytecode"/> value.
    /// </summary>
    private static readonly Dictionary<string, Bytecode> MnemonicMap = CreateMap();

    /// <summary>
    ///     Converts mnemonic instructions into their bytecode form.
    /// </summary>
    /// <param name="lines">The lines containing assembly instructions.</param>
    /// <returns>The assembled bytecode.</returns>
    public static byte[] Assemble(string[] lines)
    {
        var list = new List<byte>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var mnemonic = parts[0];

            if (!MnemonicMap.TryGetValue(mnemonic, out var code))
            {
                throw new Exception($"Unknown bytecode mnemonic: {mnemonic}");
            }

            list.Add((byte)code);

            for (var i = 1; i < parts.Length; i++)
            {
                if (byte.TryParse(parts[i], out var extension))
                {
                    list.Add(extension);
                }
                else
                {
                    throw new Exception($"Invalid extension byte: {parts[i]}");
                }
            }
        }

        return list.ToArray();
    }

    /// <summary>
    ///     Builds the mnemonic dictionary using all values of the
    ///     <see cref="Bytecode"/> enumeration.
    /// </summary>
    private static Dictionary<string, Bytecode> CreateMap()
    {
        var map = new Dictionary<string, Bytecode>(StringComparer.OrdinalIgnoreCase);
        foreach (var code in Enum.GetValues<Bytecode>())
        {
            map[code.ToString()] = code;
            map[ToMnemonic(code)] = code;
        }

        for (var i = 0; i <= 15; i++)
        {
            map[$"push_temp_{i}"] = (Bytecode)(16 + i);
        }

        map["return"] = Bytecode.ReturnTopFromMethod;
        return map;
    }

    /// <summary>
    ///     Converts an enum name into a snake_case mnemonic.
    /// </summary>
    private static string ToMnemonic(Bytecode code)
    {
        var name = code.ToString();
        var sb = new StringBuilder(name.Length * 2);
        for (var i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (i > 0 && (char.IsUpper(ch) || char.IsDigit(ch)))
            {
                sb.Append('_');
            }
            sb.Append(char.ToLowerInvariant(ch));
        }
        return sb.ToString();
    }
}
