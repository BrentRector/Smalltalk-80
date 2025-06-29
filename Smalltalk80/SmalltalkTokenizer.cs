using System.Text.RegularExpressions;

public class SmalltalkTokenizer
{
    private readonly string[] tokens;
    private int position = 0;

    public SmalltalkTokenizer(string source)
    {
        // Escape backslashes properly to avoid CS1009 error
        tokens = Regex.Split(source, "(\\s+|:|\\.|\\(|\\))")
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToArray();
    }

    public string Peek() => position < tokens.Length ? tokens[position] : null;
    public string Next() => position < tokens.Length ? tokens[position++] : null;
    public bool HasNext => position < tokens.Length;
}