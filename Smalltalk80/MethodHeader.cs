using System;

/// <summary>
/// Represents the encoded header information of a <see cref="CompiledMethod"/>.
/// </summary>
public readonly record struct MethodHeader(int Raw, int Extension)
{
    /// <summary>
    /// Gets the number of temporary variables used by the method.
    /// Blue Book format: Bits 10-15 contain the temporary count.
    /// </summary>
    public int TemporaryCount => (Raw >> 10) & 0x3F;

    /// <summary>
    /// Gets a value indicating whether a large context is required.
    /// Blue Book format: Bit 9 is the large context flag.
    /// </summary>
    public bool UsesLargeContext => ((Raw >> 9) & 1) != 0;

    /// <summary>
    /// Gets the primitive index from the header.
    /// Blue Book format: Bits 0-8 contain the primitive number (0-511).
    /// </summary>
    public int PrimitiveIndex => Raw & 0x1FF;

    /// <summary>
    /// Gets the number of literals referenced by the method.
    /// Note: This is not stored in the Blue Book header format and must be
    /// determined from the method's literal array length.
    /// </summary>
    public int LiteralCount => 0; // Not stored in Blue Book header

    /// <summary>
    /// Gets the flag value stored in the header.
    /// Note: Not used in Blue Book format - keeping for compatibility.
    /// </summary>
    public int FlagValue => 0;

    /// <summary>
    /// Gets the argument count if encoded using an extension literal.
    /// Note: In Blue Book format, argument count is derived from the selector
    /// or stored separately, not in the header.
    /// </summary>
    public int ArgumentCount => 0; // Not stored in Blue Book header
}
