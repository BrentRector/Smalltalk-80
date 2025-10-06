using Smalltalk80.Interpreter.Core.Values;

namespace Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Static class providing access to well-known system classes.
/// In the new architecture, these will be set during cold start and remain immutable.
/// </summary>
public static class SystemDictionary
{
    private static SObjectRef _undefinedObjectClassRef;
    private static SObjectRef _smallIntegerClassRef;

    /// <summary>
    /// Gets the UndefinedObject class (nil's class).
    /// </summary>
    public static SObjectRef UndefinedObjectClassRef
    {
        get
        {
            if (_undefinedObjectClassRef.IsNil)
                throw new InvalidOperationException("UndefinedObject class not yet initialized");
            return _undefinedObjectClassRef;
        }
    }

    /// <summary>
    /// Gets the SmallInteger class.
    /// </summary>
    public static SObjectRef SmallIntegerClassRef
    {
        get
        {
            if (_smallIntegerClassRef.IsNil)
                throw new InvalidOperationException("SmallInteger class not yet initialized");
            return _smallIntegerClassRef;
        }
    }

    /// <summary>
    /// Initializes the system dictionary with well-known classes.
    /// Called once during cold start.
    /// </summary>
    internal static void Initialize(SObjectRef undefinedObjectClass, SObjectRef smallIntegerClass)
    {
        _undefinedObjectClassRef = undefinedObjectClass;
        _smallIntegerClassRef = smallIntegerClass;
    }
}
