using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Executes Smalltalk-80 bytecode instructions.
/// Extracted from VirtualMachine.Bytecodes.cs for cleaner architecture.
/// </summary>
internal interface IBytecodeInterpreter
{
    /// <summary>
    /// Executes a single bytecode instruction.
    /// Returns true to continue execution, false if method returned or context switch occurred.
    /// </summary>
    /// <param name="bytecode">The bytecode to execute.</param>
    /// <param name="methodOOP">The CompiledMethod containing the bytecode.</param>
    /// <returns>True to continue execution, false to exit the bytecode loop.</returns>
    bool ExecuteBytecode(byte bytecode, SmalltalkOOP methodOOP);

    /// <summary>
    /// Initializes the selector caches for arithmetic and special bytecodes.
    /// Must be called after cold start completes and all symbols are interned.
    /// </summary>
    /// <param name="arithmeticSelectorOOPs">Cached selector OOPs for arithmetic bytecodes (0xB0-0xBF).</param>
    /// <param name="specialSelectorOOPs">Cached selector OOPs for special bytecodes (0xC0-0xCF).</param>
    void InitializeSelectorCaches(SmalltalkOOP[] arithmeticSelectorOOPs, SmalltalkOOP[] specialSelectorOOPs);

    /// <summary>
    /// Gets or sets whether verbose debug output is enabled.
    /// </summary>
    bool VerboseDebug { get; set; }

    /// <summary>
    /// Resets the instruction counter used for diagnostic event emission.
    /// </summary>
    void ResetInstructionCount();
}
