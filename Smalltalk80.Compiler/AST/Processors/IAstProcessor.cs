namespace Smalltalk80.Compiler.AST.Processors;

/// <summary>
/// Interface for AST processors that transform or analyze AST nodes.
/// </summary>
public interface IAstProcessor
{
    /// <summary>
    /// Gets the name of this processor.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes an AST node and produces a result.
    /// </summary>
    /// <param name="node">The AST node to process.</param>
    /// <param name="context">The processing context.</param>
    /// <returns>The processing result.</returns>
    ProcessorResult Process(AstNode node, ProcessorContext context);
}

/// <summary>
/// Context information for AST processing.
/// </summary>
public sealed class ProcessorContext
{
    /// <summary>
    /// Gets the source file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the original source code.
    /// </summary>
    public string SourceCode { get; }

    /// <summary>
    /// Gets additional options for processing.
    /// </summary>
    public Dictionary<string, object> Options { get; }

    /// <summary>
    /// Initializes a new instance of the ProcessorContext class.
    /// </summary>
    /// <param name="fileName">The source file name.</param>
    /// <param name="sourceCode">The original source code.</param>
    /// <param name="options">Additional processing options.</param>
    public ProcessorContext(string fileName, string sourceCode, Dictionary<string, object>? options = null)
    {
        FileName = fileName;
        SourceCode = sourceCode;
        Options = options ?? new Dictionary<string, object>();
    }
}

/// <summary>
/// Result of AST processing.
/// </summary>
public abstract class ProcessorResult
{
    /// <summary>
    /// Gets whether the processing was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Gets any error messages.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Initializes a new instance of the ProcessorResult class.
    /// </summary>
    /// <param name="success">Whether processing was successful.</param>
    /// <param name="errorMessage">Any error message.</param>
    protected ProcessorResult(bool success, string? errorMessage = null)
    {
        Success = success;
        ErrorMessage = errorMessage;
    }
}

/// <summary>
/// Result containing compiled bytecode.
/// </summary>
public sealed class BytecodeResult : ProcessorResult
{
    /// <summary>
    /// Gets the compiled bytecode data.
    /// </summary>
    public string? JsonOutput { get; }

    /// <summary>
    /// Initializes a new instance of the BytecodeResult class.
    /// </summary>
    /// <param name="jsonOutput">The JSON-formatted bytecode output.</param>
    public BytecodeResult(string jsonOutput) : base(true)
    {
        JsonOutput = jsonOutput;
    }

    /// <summary>
    /// Creates a failure result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <returns>A failed BytecodeResult.</returns>
    public static BytecodeResult Failure(string errorMessage)
    {
        return new BytecodeResult(null!, errorMessage);
    }

    private BytecodeResult(string? jsonOutput, string errorMessage) : base(false, errorMessage)
    {
        JsonOutput = jsonOutput;
    }
}