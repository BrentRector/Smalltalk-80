using System;

namespace Smalltalk80.Exceptions;

/// <summary>
/// Exception thrown during bootstrap operations.
/// </summary>
public sealed class BootstrapException : Exception
{
    /// <summary>
    /// Gets the bootstrap phase where the error occurred.
    /// </summary>
    public string? Phase { get; }

    /// <summary>
    /// Gets the file being processed when the error occurred, if applicable.
    /// </summary>
    public string? FileName { get; }

    /// <summary>
    /// Initializes a new instance of the BootstrapException class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="phase">The bootstrap phase where the error occurred.</param>
    /// <param name="fileName">The file being processed.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public BootstrapException(string message, string? phase = null, string? fileName = null, Exception? innerException = null)
        : base(message, innerException)
    {
        Phase = phase;
        FileName = fileName;
    }

    /// <summary>
    /// Creates a BootstrapException for a missing file error.
    /// </summary>
    /// <param name="fileName">The missing file name.</param>
    /// <param name="phase">The bootstrap phase.</param>
    /// <returns>A new BootstrapException instance.</returns>
    public static BootstrapException FileNotFound(string fileName, string? phase = null)
    {
        return new BootstrapException(
            $"Bootstrap file not found: {fileName}",
            phase,
            fileName);
    }

    /// <summary>
    /// Creates a BootstrapException for invalid file content.
    /// </summary>
    /// <param name="fileName">The file with invalid content.</param>
    /// <param name="reason">The reason the content is invalid.</param>
    /// <param name="phase">The bootstrap phase.</param>
    /// <returns>A new BootstrapException instance.</returns>
    public static BootstrapException InvalidContent(string fileName, string reason, string? phase = null)
    {
        return new BootstrapException(
            $"Invalid bootstrap file content in {fileName}: {reason}",
            phase,
            fileName);
    }

    /// <summary>
    /// Gets a detailed error message including bootstrap context.
    /// </summary>
    public string DetailedMessage
    {
        get
        {
            var details = Message;
            
            if (!string.IsNullOrEmpty(Phase))
            {
                details += $"\n  Phase: {Phase}";
            }
            
            if (!string.IsNullOrEmpty(FileName))
            {
                details += $"\n  File: {FileName}";
            }
            
            return details;
        }
    }
}