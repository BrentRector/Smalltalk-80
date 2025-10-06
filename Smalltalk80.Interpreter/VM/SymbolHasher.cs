namespace Smalltalk80.Interpreter.VM;

/// <summary>
/// Computes hash values for symbols using the exact Smalltalk-80 hashing algorithm.
/// This ensures that symbols placed in the USTable during cold start can be found
/// by subsequently running Smalltalk code using the same hash calculation.
/// </summary>
/// <remarks>
/// Algorithm from Smalltalk-80.st String>>hash:
///
/// hash
///     | l m |
///     (l _ m _ self size) &lt;= 2
///       ifTrue:
///         [l = 2
///           ifTrue: [m _ 3]
///           ifFalse:
///             [l = 1
///               ifTrue: [^((self at: 1) asciiValue bitAnd: 127) * 106].
///             ^21845]].
///     ^(self at: 1) asciiValue * 48 + ((self at: (m - 1)) asciiValue + l)!
///
/// Symbol lookup uses: index _ aString stringhash \\ USTable size + 1
/// </remarks>
public static class SymbolHasher
{
    /// <summary>
    /// Computes the hash value for a symbol string using Smalltalk-80's algorithm.
    /// </summary>
    /// <param name="symbol">The symbol string to hash</param>
    /// <returns>Hash value as computed by Smalltalk-80</returns>
    public static int ComputeHash(string symbol)
    {
        int length = symbol.Length;
        int m = length;

        // Handle special cases for length <= 2
        if (length <= 2)
        {
            if (length == 2)
            {
                // For length=2, set m=3 then fall through to general formula
                m = 3;
            }
            else if (length == 1)
            {
                // Single character: ((self at: 1) asciiValue bitAnd: 127) * 106
                return ((int)symbol[0] & 127) * 106;
            }
            else // length == 0
            {
                // Empty string
                return 21845;
            }
        }

        // General case (including length=2 with m=3):
        // (self at: 1) asciiValue * 48 + ((self at: (m - 1)) asciiValue + l)
        // Note: Smalltalk uses 1-based indexing, so (at: 1) is symbol[0]
        // and (at: m-1) is symbol[m-2] in 0-based indexing
        int firstChar = (int)symbol[0];
        int middleChar = (int)symbol[m - 2]; // m-1 in 1-based = m-2 in 0-based

        return firstChar * 48 + (middleChar + length);
    }

    /// <summary>
    /// Computes the USTable index for a symbol given the table size.
    /// Uses: index _ aString stringhash \\ USTable size + 1
    /// </summary>
    /// <param name="symbol">The symbol string</param>
    /// <param name="tableSize">Size of the USTable</param>
    /// <returns>1-based index into the USTable (for Smalltalk compatibility)</returns>
    public static int ComputeUSTableIndex(string symbol, int tableSize)
    {
        int hash = ComputeHash(symbol);
        return hash % tableSize + 1; // 1-based index
    }

    /// <summary>
    /// Computes the USTable index for a symbol given the table size (0-based).
    /// </summary>
    /// <param name="symbol">The symbol string</param>
    /// <param name="tableSize">Size of the USTable</param>
    /// <returns>0-based index into the USTable (for C# arrays)</returns>
    public static int ComputeUSTableIndexZeroBased(string symbol, int tableSize)
    {
        int hash = ComputeHash(symbol);
        return hash % tableSize; // 0-based index
    }
}
