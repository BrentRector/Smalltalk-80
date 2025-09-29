namespace Smalltalk80.Interpreter.Core.Objects;

using System.Linq;

/// <summary>
/// Base class for all Smalltalk objects in the system.
/// </summary>
public abstract class SObject
{
    /// <summary>
    /// Gets the class of this object.
    /// </summary>
    public abstract SClass Class { get; }
    
    /// <summary>
    /// Gets whether this is an immediate value (SmallInteger or Character).
    /// </summary>
    public virtual bool IsImmediate => false;
    
    /// <summary>
    /// Gets whether this object is nil.
    /// </summary>
    public virtual bool IsNil => false;
    
    /// <summary>
    /// Gets whether this object is a class.
    /// </summary>
    public virtual bool IsClass => false;
    
    /// <summary>
    /// Sends a message to this object.
    /// </summary>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    public virtual SObject Send(string selector, params SObject[] arguments)
    {
        // Log message send if enabled
        if (Program.RuntimeOptions?.EnableSendLogging ?? false)
        {
            LogMessageSend(this, selector, arguments);
        }
        
        // Track statistics if available
        // Note: We'd need access to the VM instance to track stats properly
        // For now, this is a simplified implementation
        
        var method = Class.LookupMethod(selector);
        if (method == null)
        {
            return DoesNotUnderstand(selector, arguments);
        }
        
        return method.Execute(this, arguments);
    }
    
    /// <summary>
    /// Logs a message send for debugging purposes.
    /// </summary>
    private static void LogMessageSend(SObject receiver, string selector, SObject[] arguments)
    {
        var receiverClass = receiver.Class?.Name ?? "Unknown";
        var argString = arguments.Length == 0 
            ? "" 
            : " " + string.Join(" ", arguments.Select(arg => 
                $"{arg.ToDisplayString()}({arg.Class?.Name ?? "Unknown"})"));
        
        Console.WriteLine($"[SEND] {receiverClass}>>{selector}{argString}");
    }
    
    /// <summary>
    /// Handles messages that are not understood by this object.
    /// </summary>
    protected virtual SObject DoesNotUnderstand(string selector, SObject[] arguments)
    {
        throw new MessageNotUnderstoodException($"Message not understood: {selector}");
    }
    
    /// <summary>
    /// Converts this object to a display string.
    /// </summary>
    public abstract string ToDisplayString();
    
    /// <summary>
    /// Gets the hash code for this object.
    /// </summary>
    public abstract override int GetHashCode();
    
    /// <summary>
    /// Determines whether this object equals another object.
    /// </summary>
    public abstract override bool Equals(object? obj);
    
    /// <summary>
    /// Returns a string representation of this object.
    /// </summary>
    public override string ToString() => ToDisplayString();
}

/// <summary>
/// Exception thrown when a message is not understood.
/// </summary>
public class MessageNotUnderstoodException(string message) : Exception(message);