using System;

/// <summary>
/// Represents the encoded header information of a <see cref="CompiledMethod"/>.
/// </summary>
public readonly record struct MethodHeader(int Raw, int Extension)
{
    /// <summary>
    /// Gets the number of temporary variables used by the method.
    /// </summary>
    public int TemporaryCount => (Raw >> 3) & 0x1F;

    /// <summary>
    /// Gets a value indicating whether a large context is required.
    /// </summary>
    public bool UsesLargeContext => ((Raw >> 8) & 1) != 0;

    /// <summary>
    /// Gets the number of literals referenced by the method.
    /// </summary>
    public int LiteralCount => (Raw >> 9) & 0x3F;

    /// <summary>
    /// Gets the flag value stored in the header.
    /// </summary>
    public int FlagValue => Raw & 0x7;

    /// <summary>
    /// Gets the argument count if encoded using an extension literal.
    /// </summary>
    public int ArgumentCount => FlagValue == 7 ? (Extension >> 10) & 0x1F : Math.Min(FlagValue, 4);

    /// <summary>
    /// Gets the primitive index associated with the method if any.
    /// </summary>
    public int PrimitiveIndex => FlagValue == 7 ? Extension & 0x3FF : 0;
}
