namespace Smalltalk80.Interpreter.Graphics;

/// <summary>
/// BitBlt combination rules as defined in the Blue Book.
/// These 16 boolean operations define how source and destination bits are combined.
/// </summary>
public enum CombinationRule
{
    /// <summary>0 - Clear destination to 0 (black in Smalltalk).</summary>
    Clear = 0,

    /// <summary>1 - src AND dst - Used for masking.</summary>
    And = 1,

    /// <summary>2 - src AND (NOT dst) - Erase pattern from destination.</summary>
    AndReverse = 2,

    /// <summary>3 - src - Normal copy, most common operation.</summary>
    Copy = 3,

    /// <summary>4 - (NOT src) AND dst - Reverse erase.</summary>
    AndInverted = 4,

    /// <summary>5 - dst - No operation, destination unchanged.</summary>
    Noop = 5,

    /// <summary>6 - src XOR dst - Toggle/invert destination.</summary>
    Xor = 6,

    /// <summary>7 - src OR dst - Paint over existing pixels.</summary>
    Or = 7,

    /// <summary>8 - NOT (src OR dst).</summary>
    Nor = 8,

    /// <summary>9 - NOT (src XOR dst) - Equivalence.</summary>
    Equiv = 9,

    /// <summary>10 - NOT dst - Invert destination.</summary>
    Invert = 10,

    /// <summary>11 - src OR (NOT dst).</summary>
    OrReverse = 11,

    /// <summary>12 - NOT src - Copy inverted source.</summary>
    CopyInverted = 12,

    /// <summary>13 - (NOT src) OR dst.</summary>
    OrInverted = 13,

    /// <summary>14 - NOT (src AND dst).</summary>
    Nand = 14,

    /// <summary>15 - Set destination to 1 (white in Smalltalk).</summary>
    Set = 15
}
