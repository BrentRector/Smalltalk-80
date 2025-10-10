namespace Smalltalk80.Interpreter.Graphics;

/// <summary>
/// Describes a complete BitBlt operation with all parameters.
/// Based on Blue Book BitBlt specification.
/// </summary>
public class BitBltOperation
{
    /// <summary>Destination bitmap bits.</summary>
    public required Memory<byte> DestBits { get; init; }

    /// <summary>Source bitmap bits (may be same as dest).</summary>
    public required Memory<byte> SourceBits { get; init; }

    /// <summary>Halftone pattern bits (optional, for texture fills).</summary>
    public Memory<byte>? HalftoneBits { get; init; }

    /// <summary>Destination bitmap width in pixels.</summary>
    public required int DestWidth { get; init; }

    /// <summary>Destination bitmap height in pixels.</summary>
    public required int DestHeight { get; init; }

    /// <summary>Source bitmap width in pixels.</summary>
    public required int SourceWidth { get; init; }

    /// <summary>Source bitmap height in pixels.</summary>
    public required int SourceHeight { get; init; }

    /// <summary>Halftone pattern width (typically 16).</summary>
    public int HalftoneWidth { get; init; } = 16;

    /// <summary>Halftone pattern height (typically 16).</summary>
    public int HalftoneHeight { get; init; } = 16;

    /// <summary>Destination X coordinate (top-left of operation).</summary>
    public required int DestX { get; init; }

    /// <summary>Destination Y coordinate (top-left of operation).</summary>
    public required int DestY { get; init; }

    /// <summary>Source X coordinate (top-left of source).</summary>
    public required int SourceX { get; init; }

    /// <summary>Source Y coordinate (top-left of source).</summary>
    public required int SourceY { get; init; }

    /// <summary>Width of operation in pixels.</summary>
    public required int Width { get; init; }

    /// <summary>Height of operation in pixels.</summary>
    public required int Height { get; init; }

    /// <summary>Clipping rectangle X (in destination coordinates).</summary>
    public int ClipX { get; init; } = 0;

    /// <summary>Clipping rectangle Y (in destination coordinates).</summary>
    public int ClipY { get; init; } = 0;

    /// <summary>Clipping rectangle width.</summary>
    public int ClipWidth { get; init; } = int.MaxValue;

    /// <summary>Clipping rectangle height.</summary>
    public int ClipHeight { get; init; } = int.MaxValue;

    /// <summary>Combination rule (0-15).</summary>
    public required CombinationRule Rule { get; init; }
}
