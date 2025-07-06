using System;
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
        FileNode fileAst = FileInReader.Parse(path);
        MethodHeaderNode? currentHeader = null;

        foreach (var chunk in fileAst.Chunks)
        {
            switch (chunk)
            {
                case ClassDefinitionNode cdef:
                    var super = SystemDictionary.TryGetClass(cdef.Superclass, out var sup) ? sup : null;
                    SystemDictionary.DefineClass(
                        cdef.ClassName,
                        super,
                        cdef.InstanceVariables,
                        cdef.ClassVariables,
                        cdef.PoolDictionaries,
                        cdef.Category);
                    break;

                case MethodHeaderNode header:
                    currentHeader = header;
                    break;

                case MethodDefinitionNode method when currentHeader is not null:
                    var cls = SystemDictionary.DefineClass(currentHeader.ClassName);
                    var vars = currentHeader.IsClassSide ? cls.ClassVariables : cls.InstanceVariables;

                    // Reparse the method body with variable information so tracing
                    // accurately reflects the available names during compilation.
                    if (string.IsNullOrWhiteSpace(method.Source))
                        break;

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
}
