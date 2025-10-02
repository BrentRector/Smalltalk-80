namespace Smalltalk80.Interpreter.Core.Objects;

using System.Linq;

/// <summary>
/// Base class for all Smalltalk objects in the system.
/// NOTE: This is legacy code for GC compatibility. New code should use SObjectRef.
/// </summary>
public abstract class SObject
{
    
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
    /// NOTE: This is legacy code - new interpreter uses SObjectRef.
    /// </summary>
    /// <param name="selector">The message selector.</param>
    /// <param name="arguments">The message arguments.</param>
    /// <returns>The result of the message send.</returns>
    public virtual SObject Send(string selector, params SObject[] arguments)
    {
        throw new NotSupportedException("SObject.Send is deprecated - use SObjectRef interpreter instead");
    }

    /// <summary>
    /// Logs a message send for debugging purposes.
    /// </summary>
    private static void LogMessageSend(SObject receiver, string selector, SObject[] arguments)
    {
        var receiverClass = receiver.GetType().Name;
        var argString = arguments.Length == 0
            ? ""
            : " " + string.Join(" ", arguments.Select(arg =>
                $"{arg.ToDisplayString()}({arg.GetType().Name})"));

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