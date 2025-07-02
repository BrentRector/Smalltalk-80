using System;

namespace Smalltalk80;

/// <summary>
/// Represents a simple stream over a <see cref="SmalltalkObject"/> array.
/// </summary>
public sealed class StreamObject : SmalltalkObject
{
    /// <summary>Index of the array field within <see cref="SmalltalkObject.Fields"/>.</summary>
    public const int ArrayField = 0;
    /// <summary>Index of the current position field.</summary>
    public const int IndexField = 1;
    /// <summary>Index of the read limit field.</summary>
    public const int ReadLimitField = 2;
    /// <summary>Index of the write limit field.</summary>
    public const int WriteLimitField = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="StreamObject"/> class.
    /// </summary>
    /// <param name="cls">The class pointer for the stream.</param>
    /// <param name="array">The array backing the stream.</param>
    /// <param name="readLimit">The maximum index that can be read.</param>
    /// <param name="writeLimit">The maximum index that can be written.</param>
    public StreamObject(
        SmalltalkClass? cls,
        SmalltalkObject array,
        int readLimit,
        int writeLimit)
        : base(cls, 4)
    {
        Fields[ArrayField] = array;
        Fields[IndexField] = 0;
        Fields[ReadLimitField] = readLimit;
        Fields[WriteLimitField] = writeLimit;
    }

    /// <summary>The array being streamed.</summary>
    public SmalltalkObject Array
    {
        get => (SmalltalkObject)Fields[ArrayField]!;
        set => Fields[ArrayField] = value;
    }

    /// <summary>The current position within the stream.</summary>
    public int Index
    {
        get => (int)(Fields[IndexField] ?? 0);
        set => Fields[IndexField] = value;
    }

    /// <summary>The maximum index that can be read.</summary>
    public int ReadLimit
    {
        get => (int)(Fields[ReadLimitField] ?? 0);
        set => Fields[ReadLimitField] = value;
    }

    /// <summary>The maximum index that can be written.</summary>
    public int WriteLimit
    {
        get => (int)(Fields[WriteLimitField] ?? 0);
        set => Fields[WriteLimitField] = value;
    }
}
