using System.Collections.Generic;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Model;

namespace Smalltalk80.Compiler.Infrastructure
{
    /// <summary>
    /// Interface for bytecode compilation.
    /// Compiles Smalltalk-80 AST nodes to Blue Book bytecode.
    /// </summary>
    public interface IBytecodeCompiler
    {
        /// <summary>
        /// Compiles a file-scope expression to bytecode.
        /// The expression will be compiled as if it were a method with no arguments.
        /// </summary>
        /// <param name="expression">The expression AST node.</param>
        /// <returns>Compiled method data with bytecode and literals.</returns>
        CompiledMethodData CompileExpressionFromTree(
            Smalltalk80Parser.ExpressionContext expression);

        /// <summary>
        /// Compiles a Smalltalk method from a parse tree to Blue Book bytecode.
        /// </summary>
        /// <param name="methodDef">The method definition AST node.</param>
        /// <param name="instanceVars">List of instance variable names for the class.</param>
        /// <param name="classVars">Dictionary of class variable names to values.</param>
        /// <param name="poolVars">Dictionary of pool variable names to values.</param>
        /// <param name="options">Compiler options (e.g., tail recursion optimization).</param>
        /// <param name="knownClassNames">Set of all class names for proper variable resolution.</param>
        /// <param name="knownPoolDictionaryNames">Set of pool dictionary names.</param>
        /// <returns>Compiled method data with bytecode and literals.</returns>
        CompiledMethodData CompileMethodFromTree(
            Smalltalk80Parser.MethodDefContext methodDef,
            List<string> instanceVars,
            Dictionary<string, object>? classVars = null,
            Dictionary<string, object>? poolVars = null,
            CompilerOptions? options = null,
            HashSet<string>? knownClassNames = null,
            HashSet<string>? knownPoolDictionaryNames = null);
    }
}
