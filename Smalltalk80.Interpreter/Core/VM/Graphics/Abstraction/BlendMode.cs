// Graphics Abstraction Layer - Blend Modes
// Supports classic Smalltalk-80 rules plus modern alpha blending

namespace Silvern.Smalltalk80.Core.VM.Graphics.Abstraction;

/// <summary>
/// Blend modes for graphics operations.
/// Rules 0-15 are classic Smalltalk-80 boolean operations.
/// Rules 16+ are modern extensions for alpha blending and color operations.
/// </summary>
public enum BlendMode : byte
{
    // Classic Smalltalk-80 combination rules (0-15)
    // For 1-bit: Boolean operations
    // For 32-bit: Component-wise operations

    /// <summary>Rule 0: Clear - Always 0 (transparent black for 32-bit)</summary>
    Clear = 0,

    /// <summary>Rule 1: AND - src AND dst (Min for 32-bit components)</summary>
    And = 1,

    /// <summary>Rule 2: AND Reverse - src AND (NOT dst)</summary>
    AndReverse = 2,

    /// <summary>Rule 3: Copy - Replace dest with source</summary>
    Copy = 3,

    /// <summary>Rule 4: AND Inverted - (NOT src) AND dst</summary>
    AndInverted = 4,

    /// <summary>Rule 5: No-op - Destination unchanged</summary>
    Noop = 5,

    /// <summary>Rule 6: XOR - src XOR dst</summary>
    Xor = 6,

    /// <summary>Rule 7: OR - src OR dst (Max for 32-bit components)</summary>
    Or = 7,

    /// <summary>Rule 8: NOR - NOT (src OR dst)</summary>
    Nor = 8,

    /// <summary>Rule 9: Equiv - NOT (src XOR dst)</summary>
    Equiv = 9,

    /// <summary>Rule 10: Invert - NOT dst (ignores source)</summary>
    Invert = 10,

    /// <summary>Rule 11: OR Reverse - src OR (NOT dst)</summary>
    OrReverse = 11,

    /// <summary>Rule 12: Copy Inverted - NOT src</summary>
    CopyInverted = 12,

    /// <summary>Rule 13: OR Inverted - (NOT src) OR dst</summary>
    OrInverted = 13,

    /// <summary>Rule 14: NAND - NOT (src AND dst)</summary>
    Nand = 14,

    /// <summary>Rule 15: Set - Always 1 (opaque white for 32-bit)</summary>
    Set = 15,

    // Modern blend modes (16+) - for 32-bit color with alpha

    /// <summary>Rule 16: Paint - Copy source where halftone is set (legacy)</summary>
    Paint = 16,

    /// <summary>Rule 24: Alpha Blend - Porter-Duff source-over</summary>
    /// <remarks>result = src * srcAlpha + dst * (1 - srcAlpha)</remarks>
    AlphaBlend = 24,

    /// <summary>Rule 25: Pixel Paint - Paint source where source is not transparent</summary>
    PixelPaint = 25,

    /// <summary>Rule 26: Alpha Blend Constant - Blend with constant alpha</summary>
    AlphaBlendConstant = 26,

    /// <summary>Rule 34: RGB Add - Additive blending (clamped)</summary>
    RgbAdd = 34,

    /// <summary>Rule 35: RGB Subtract - Subtractive blending (clamped)</summary>
    RgbSubtract = 35,

    /// <summary>Rule 36: RGB Multiply - Multiplicative blending</summary>
    RgbMultiply = 36,

    /// <summary>Rule 37: RGB Screen - Screen blend mode</summary>
    RgbScreen = 37
}
