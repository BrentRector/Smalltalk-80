namespace Smalltalk80.Interpreter.Core.VM.Graphics;

/// <summary>
/// BitBlt combination rules (0-15) as defined in the Blue Book.
/// These are the 16 possible boolean operations on source and destination bits.
/// </summary>
public enum CombinationRule : byte
{
    /// <summary>Rule 0: Always 0 (clear destination).</summary>
    Clear = 0,

    /// <summary>Rule 1: Source AND Destination.</summary>
    And = 1,

    /// <summary>Rule 2: Source AND (NOT Destination).</summary>
    AndReverse = 2,

    /// <summary>Rule 3: Source (copy source to destination).</summary>
    Copy = 3,

    /// <summary>Rule 4: (NOT Source) AND Destination.</summary>
    AndInverted = 4,

    /// <summary>Rule 5: Destination (no change).</summary>
    Noop = 5,

    /// <summary>Rule 6: Source XOR Destination.</summary>
    Xor = 6,

    /// <summary>Rule 7: Source OR Destination.</summary>
    Or = 7,

    /// <summary>Rule 8: NOT (Source OR Destination).</summary>
    Nor = 8,

    /// <summary>Rule 9: NOT (Source XOR Destination) (equivalence).</summary>
    Equiv = 9,

    /// <summary>Rule 10: NOT Destination (invert destination).</summary>
    Invert = 10,

    /// <summary>Rule 11: Source OR (NOT Destination).</summary>
    OrReverse = 11,

    /// <summary>Rule 12: NOT Source (copy inverted source).</summary>
    CopyInverted = 12,

    /// <summary>Rule 13: (NOT Source) OR Destination.</summary>
    OrInverted = 13,

    /// <summary>Rule 14: NOT (Source AND Destination).</summary>
    Nand = 14,

    /// <summary>Rule 15: Always 1 (set destination).</summary>
    Set = 15,

    /// <summary>
    /// Rule 16: Paint mode with grayscale alpha blending.
    /// For 32bpp: white (0xFFFFFF) = transparent, black (0x000000) = opaque.
    /// Used for anti-aliased text rendering.
    /// </summary>
    Paint = 16
}
