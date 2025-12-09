using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Provides debugging and diagnostic output for VM execution.
/// Handles stack dumps, sender chain visualization, and value formatting.
/// </summary>
internal interface IVMDebugger
{
    /// <summary>
    /// Dumps the sender chain from a context, showing the call stack.
    /// </summary>
    /// <param name="contextOOP">Starting context to dump from.</param>
    /// <param name="maxDepth">Maximum number of contexts to show.</param>
    void DumpSenderChain(SmalltalkOOP contextOOP, int maxDepth);

    /// <summary>
    /// Dumps the current operand stack contents.
    /// </summary>
    /// <param name="label">Label to identify this stack dump.</param>
    void DumpStack(string label);

    /// <summary>
    /// Formats a literal value for debug display.
    /// </summary>
    /// <param name="oop">OOP of the literal to describe.</param>
    /// <returns>Human-readable description of the literal.</returns>
    string DescribeLiteral(SmalltalkOOP oop);

    /// <summary>
    /// Formats a value for debug display.
    /// </summary>
    /// <param name="oop">OOP of the value to describe.</param>
    /// <returns>Human-readable description of the value.</returns>
    string DescribeValue(SmalltalkOOP oop);

    /// <summary>
    /// Dumps the bytecodes of a CompiledMethod.
    /// </summary>
    /// <param name="methodOOP">OOP of the CompiledMethod.</param>
    /// <param name="prefix">Prefix string for indentation.</param>
    void DumpBytecodes(SmalltalkOOP methodOOP, string prefix);

    /// <summary>
    /// Formats an OOP value for debugging output.
    /// </summary>
    /// <param name="oop">OOP to format.</param>
    /// <returns>Formatted string representation.</returns>
    string FormatOOPValue(SmalltalkOOP oop);

    /// <summary>
    /// Dumps comprehensive crash diagnostics including active context, method, IP, bytecode, stack, and sender chain.
    /// </summary>
    /// <param name="errorMessage">The error message that triggered the crash.</param>
    void DumpCrashDiagnostics(string errorMessage);
}
