using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Provides functionality to load classes and methods from classic Smalltalk
/// file-in format sources.
/// </summary>
public static class FileInLoader
{
    /// <summary>
    /// Reads the specified file and installs any classes and methods it defines
    /// into the <see cref="SystemDictionary"/>.
    /// </summary>
    /// <param name="path">Path to the Smalltalk source file.</param>
    public static void Load(string path)
    {
        using var reader = new StreamReader(path);
        string? currentClass = null;
        bool classSide = false;
        string? selector = null;
        var body = new StringBuilder();
        var inMethod = false;

        while (reader.ReadLine() is { } line)
        {
            line = line.TrimEnd();

            if (inMethod)
            {
                if (line == "!")
                {
                    InstallMethod(currentClass!, selector!, body.ToString(), classSide);
                    body.Clear();
                    inMethod = false;
                    continue;
                }

                body.AppendLine(line);
                continue;
            }

            if (line.StartsWith("!"))
            {
                var header = line.Trim('!', ' ');
                if (header.Contains(" class methodsFor:"))
                {
                    var match = Regex.Match(header, "^(?<cls>\\w+) class");
                    if (match.Success)
                        currentClass = match.Groups["cls"].Value;
                    classSide = true;
                }
                else if (header.Contains(" methodsFor:"))
                {
                    var match = Regex.Match(header, "^(?<cls>\\w+)");
                    if (match.Success)
                        currentClass = match.Groups["cls"].Value;
                    classSide = false;
                }

                // Ignore other exclamation lines
                continue;
            }

            var subclassMatch = Regex.Match(line, @"^(?<super>\w+)\s+subclass:\s+#(?<name>\w+)");
            if (subclassMatch.Success)
            {
                var superName = subclassMatch.Groups["super"].Value;
                var className = subclassMatch.Groups["name"].Value;
                SystemDictionary.DefineClass(
                    className,
                    SystemDictionary.TryGetClass(superName, out var sup) ? sup : null);
                continue;
            }

            if (!string.IsNullOrWhiteSpace(line) && currentClass is not null)
            {
                selector = line.Trim();
                inMethod = true;
                body.Clear();
            }
        }
    }

    private static void InstallMethod(string className, string selector, string source, bool classSide)
    {
        var parser = new SmalltalkParser(source);
        var method = parser.ParseMethod();
        var cls = SystemDictionary.DefineClass(className);
        cls.MethodDictionary[new Symbol(selector)] = method;
    }
}
