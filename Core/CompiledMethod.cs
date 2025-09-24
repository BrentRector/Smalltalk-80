using Core.Memory;

namespace Core;

/// <summary>
/// Represents a compiled Smalltalk method containing bytecodes and literals.
/// Follows the Blue Book specification for method structure.
/// </summary>
public sealed class CompiledMethod
{
    /// <summary>
    /// The method header containing flags and counts.
    /// Format: [primitive index: 8 bits][temp count: 5 bits][large context: 1 bit][literal count: 6 bits][arg count: 4 bits][flags: 8 bits]
    /// </summary>
    public uint Header { get; init; }

    /// <summary>
    /// The method's bytecode instructions.
    /// </summary>
    public byte[] Bytecodes { get; init; } = [];

    /// <summary>
    /// The method's literal objects (constants, selectors, etc.).
    /// </summary>
    public OOP[] Literals { get; init; } = [];

    /// <summary>
    /// Gets the number of arguments this method expects.
    /// </summary>
    public int ArgumentCount => (int)(Header & 0x0F);

    /// <summary>
    /// Gets the number of temporary variables this method uses.
    /// </summary>
    public int TemporaryCount => (int)((Header >> 18) & 0x1F);

    /// <summary>
    /// Gets the number of literals in this method.
    /// </summary>
    public int LiteralCount => (int)((Header >> 9) & 0x3F);

    /// <summary>
    /// Gets the primitive index if this is a primitive method.
    /// </summary>
    public int PrimitiveIndex => (int)((Header >> 24) & 0xFF);

    /// <summary>
    /// Indicates whether this method uses a large context.
    /// </summary>
    public bool UsesLargeContext => ((Header >> 17) & 0x01) != 0;

    /// <summary>
    /// Indicates whether this method has a primitive.
    /// </summary>
    public bool HasPrimitive => PrimitiveIndex > 0;

    /// <summary>
    /// Gets the stack size needed for this method.
    /// </summary>
    public int StackSize => TemporaryCount + MaxStackDepth;

    /// <summary>
    /// Gets the maximum stack depth for this method (computed from bytecodes).
    /// </summary>
    public int MaxStackDepth { get; init; } = 16; // Default, should be computed

    /// <summary>
    /// Creates a new CompiledMethod with the specified parameters.
    /// </summary>
    /// <param name="argumentCount">Number of arguments.</param>
    /// <param name="temporaryCount">Number of temporary variables.</param>
    /// <param name="primitiveIndex">Primitive index (0 if none).</param>
    /// <param name="literals">Method literals.</param>
    /// <param name="bytecodes">Method bytecodes.</param>
    /// <param name="useLargeContext">Whether to use a large context.</param>
    /// <returns>A new CompiledMethod instance.</returns>
    public static CompiledMethod Create(
        int argumentCount,
        int temporaryCount,
        int primitiveIndex,
        OOP[] literals,
        byte[] bytecodes,
        bool useLargeContext = false)
    {
        if (argumentCount < 0 || argumentCount > 15)
        {
            throw new ArgumentOutOfRangeException(nameof(argumentCount), "Argument count must be 0-15");
        }

        if (temporaryCount < 0 || temporaryCount > 31)
        {
            throw new ArgumentOutOfRangeException(nameof(temporaryCount), "Temporary count must be 0-31");
        }

        if (primitiveIndex < 0 || primitiveIndex > 255)
        {
            throw new ArgumentOutOfRangeException(nameof(primitiveIndex), "Primitive index must be 0-255");
        }

        if (literals.Length > 63)
        {
            throw new ArgumentOutOfRangeException(nameof(literals), "Literal count must be 0-63");
        }

        var header = (uint)argumentCount |
                    ((uint)literals.Length << 9) |
                    (useLargeContext ? 1u << 17 : 0u) |
                    ((uint)temporaryCount << 18) |
                    ((uint)primitiveIndex << 24);

        return new CompiledMethod
        {
            Header = header,
            Literals = literals,
            Bytecodes = bytecodes,
            MaxStackDepth = ComputeMaxStackDepth(bytecodes)
        };
    }

    /// <summary>
    /// Gets a literal at the specified index.
    /// </summary>
    /// <param name="index">The literal index.</param>
    /// <returns>The literal OOP.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when index is out of range.</exception>
    public OOP GetLiteral(int index)
    {
        if (index < 0 || index >= Literals.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Literal index {index} out of range");
        }

        return Literals[index];
    }

    /// <summary>
    /// Gets a bytecode at the specified program counter.
    /// </summary>
    /// <param name="pc">The program counter.</param>
    /// <returns>The bytecode.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when PC is out of range.</exception>
    public byte GetBytecode(int pc)
    {
        if (pc < 0 || pc >= Bytecodes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(pc), $"Program counter {pc} out of range");
        }

        return Bytecodes[pc];
    }

    /// <summary>
    /// Checks if the program counter is at the end of the bytecodes.
    /// </summary>
    /// <param name="pc">The program counter.</param>
    /// <returns>True if at end; otherwise, false.</returns>
    public bool IsAtEnd(int pc) => pc >= Bytecodes.Length;

    /// <summary>
    /// Creates a method header value from components.
    /// </summary>
    public static uint MakeHeader(int argCount, int tempCount, int literalCount, int primitiveIndex, bool largeContext)
    {
        return (uint)(argCount & 0x0F) |
               ((uint)(literalCount & 0x3F) << 9) |
               (largeContext ? 1u << 17 : 0u) |
               ((uint)(tempCount & 0x1F) << 18) |
               ((uint)(primitiveIndex & 0xFF) << 24);
    }

    /// <summary>
    /// Computes the maximum stack depth needed for the given bytecodes.
    /// </summary>
    private static int ComputeMaxStackDepth(byte[] bytecodes)
    {
        // Simplified computation - in full implementation would analyze bytecode flow
        var maxDepth = 0;
        var currentDepth = 0;

        for (var i = 0; i < bytecodes.Length; i++)
        {
            var bytecode = bytecodes[i];

            // Analyze bytecode effect on stack
            currentDepth += GetStackDelta(bytecode);
            maxDepth = Math.Max(maxDepth, currentDepth);

            // Handle extended bytecodes
            if (IsExtendedBytecode(bytecode))
            {
                i++; // Skip extension byte
            }
        }

        return Math.Max(maxDepth, 8); // Minimum stack size
    }

    private static int GetStackDelta(byte bytecode)
    {
        // Simplified stack effect analysis
        return bytecode switch
        {
            >= 0 and <= 15 => 1,     // Push receiver variable
            >= 16 and <= 31 => 1,    // Push temporary
            >= 32 and <= 63 => 1,    // Push literal constant
            >= 64 and <= 95 => 1,    // Push literal variable
            >= 96 and <= 103 => 0,   // Store and pop receiver variable
            >= 104 and <= 111 => 0,  // Store and pop temporary
            >= 112 and <= 119 => 1,  // Push special constants
            >= 120 and <= 123 => 0,  // Return
            >= 124 and <= 125 => 0,  // Return stack top
            >= 126 and <= 127 => 0,  // Unused
            >= 128 and <= 175 => -1, // Send (pops receiver and args, pushes result)
            >= 176 and <= 207 => 0,  // Jump/conditional
            >= 208 and <= 223 => -1, // Send special selectors
            _ => 0  // >= 224 and <= 255 - Send literal selector
        };
    }

    private static bool IsExtendedBytecode(byte bytecode)
    {
        // Extended bytecodes that have an extension byte
        return bytecode is >= 128 and <= 131 or >= 132 and <= 133 or >= 134 and <= 137;
    }

    /// <summary>
    /// Returns a string representation of this compiled method.
    /// </summary>
    public override string ToString() => 
        $"CompiledMethod[args={ArgumentCount}, temps={TemporaryCount}, literals={LiteralCount}, primitive={PrimitiveIndex}, size={Bytecodes.Length}]";
}