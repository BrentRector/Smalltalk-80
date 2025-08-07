using System.Linq;
using System.Text.RegularExpressions;

/// <summary>
///     Breaks a Smalltalk source string into lexical tokens.
/// </summary>
public sealed class SmalltalkTokenizer
{
    private readonly string[] tokens;
    private int position = 0;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SmalltalkTokenizer"/> class.
    /// </summary>
    /// <param name="source">The source string to tokenize.</param>
    public SmalltalkTokenizer(string source)
    {
        // Escape backslashes properly to avoid CS1009 error
        tokens = Regex.Split(source, "(\\s+|:|\\.|\\(|\\))")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
    }

    /// <summary>
    ///     Peeks at the next token without consuming it.
    /// </summary>
    /// <returns>The next token, or <c>null</c> if none remain.</returns>
    public string? Peek() => position < tokens.Length ? tokens[position] : null;

    /// <summary>
    ///     Retrieves the next token from the stream.
    /// </summary>
    /// <returns>The next token, or <c>null</c> if none remain.</returns>
    public string? Next() => position < tokens.Length ? tokens[position++] : null;

    /// <summary>
    ///     Gets a value indicating whether more tokens are available.
    /// </summary>
    public bool HasNext => position < tokens.Length;
}