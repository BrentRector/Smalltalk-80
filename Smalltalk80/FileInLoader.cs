using System;
using System.Collections.Generic;
using Smalltalk80.Parser;

/// <summary>
/// Loads Smalltalk file-in sources and installs the contained classes and
/// methods into the <see cref="SystemDictionary"/>.
/// </summary>
public static class FileInLoader
{
    /// <summary>
    /// Reads the specified file and installs the classes and methods it defines
    /// into the system dictionary.
    /// </summary>
    /// <param name="path">Path to the Smalltalk source file.</param>
    public static void Load(string path)
    {
        var fileAst = FileInParser.Parse(path);
        MethodHeaderNode? currentHeader = null;

        foreach (var chunk in fileAst.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode cdef:
                    var super = SystemDictionary.TryGetClass(cdef.Superclass, out var sup) ? sup : null;
                    cdef.Options.TryGetValue("instanceVariableNames", out var ivars);
                    cdef.Options.TryGetValue("classVariableNames", out var cvars);
                    cdef.Options.TryGetValue("poolDictionaries", out var pools);
                    cdef.Options.TryGetValue("category", out var category);
                    var varList = ivars is not null ? ExtractIdentifiersFromString(ivars) : Array.Empty<string>();
                    var classList = cvars is not null ? ExtractIdentifiersFromString(cvars) : Array.Empty<string>();
                    var poolList = pools is not null ? ExtractIdentifiersFromString(pools) : Array.Empty<string>();
                    SystemDictionary.DefineClass(cdef.ClassName, super, varList, classList, poolList, category);
                    break;

                case MethodHeaderNode header:
                    currentHeader = header;
                    break;

                case MethodDefinitionNode method when currentHeader is not null:
                    var cls = SystemDictionary.DefineClass(currentHeader.ClassName);
                    var vars = currentHeader.IsClassSide ? cls.ClassVariables : cls.InstanceVariables;

                    // Reparse the method body with variable information so tracing
                    // accurately reflects the available names during compilation.
                    var parser = new SmalltalkParser(method.Source);
                    var compiled = parser.ParseMethod(method.Arguments, vars, method.Selector);
                    cls.MethodDictionary[new Symbol(method.Selector)] = compiled;
                    break;

                case ClassCommentNode:
                case ScriptNode:
                    // Ignore comments and scripts outside method context
                    break;
            }
        }
    }

    private static IReadOnlyList<string> ExtractIdentifiersFromString(string value)
        => value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
