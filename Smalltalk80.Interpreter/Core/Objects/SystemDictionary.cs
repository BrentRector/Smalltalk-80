using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Static class providing access to well-known system classes.
/// In the new architecture, these will be set during cold start and remain immutable.
/// </summary>
public static class SystemDictionary
{
    private static SmalltalkOOP _undefinedObjectClassRef;
    private static SmalltalkOOP _smallIntegerClassRef;

    /// <summary>
    /// Gets the UndefinedObject class (nil's class).
    /// </summary>
    public static SmalltalkOOP UndefinedObjectClassRef
    {
        get
        {
            if (_undefinedObjectClassRef == SmalltalkOOP.Null)
                throw new InvalidOperationException("UndefinedObject class not yet initialized");
            return _undefinedObjectClassRef;
        }
    }

    /// <summary>
    /// Gets the SmallInteger class.
    /// </summary>
    public static SmalltalkOOP SmallIntegerClassRef
    {
        get
        {
            if (_smallIntegerClassRef == SmalltalkOOP.Null)
                throw new InvalidOperationException("SmallInteger class not yet initialized");
            return _smallIntegerClassRef;
        }
    }

    /// <summary>
    /// Initializes the system dictionary with well-known classes.
    /// Called once during cold start.
    /// </summary>
    internal static void Initialize(SmalltalkOOP undefinedObjectClass, SmalltalkOOP smallIntegerClass)
    {
        _undefinedObjectClassRef = undefinedObjectClass;
        _smallIntegerClassRef = smallIntegerClass;
    }
}
