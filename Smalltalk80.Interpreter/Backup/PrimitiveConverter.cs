using System;

/// <summary>
/// Handles conversion between .NET primitive types and Smalltalk objects.
/// Extracted from MessageDispatcher.cs to improve separation of concerns and maintainability.
/// </summary>
public static class PrimitiveConverter
{
    /// <summary>
    /// Maps .NET primitive types to their corresponding Smalltalk classes.
    /// </summary>
    /// <param name="primitive">The .NET primitive object.</param>
    /// <returns>The corresponding Smalltalk class, or null if no mapping exists.</returns>
    public static SmalltalkObject? GetClassForPrimitive(object? primitive)
    {
        // Stub implementation - returns null since shadow classes removed
        // In a full implementation, this would look up class objects in ObjectMemory
        return null;
    }

    /// <summary>
    /// Converts .NET primitive values to proper Smalltalk objects.
    /// </summary>
    /// <param name="primitive">The .NET primitive value.</param>
    /// <param name="smalltalkClass">The corresponding Smalltalk class.</param>
    /// <returns>A Smalltalk object, or null if conversion is not needed/possible.</returns>
    public static SmalltalkObject? ConvertPrimitiveToSmalltalkObject(object? primitive, SmalltalkObject? smalltalkClass)
    {
        return primitive switch
        {
            bool b => ConvertBooleanToSmalltalkObject(b),
            int i => ConvertIntegerToSmalltalkObject(i),
            double d => ConvertFloatToSmalltalkObject(d),
            char c => ConvertCharacterToSmalltalkObject(c),
            string s => ConvertStringToSmalltalkObject(s),
            _ => null
        };
    }

    /// <summary>
    /// Converts a .NET boolean to the appropriate Smalltalk singleton object (True or False).
    /// </summary>
    /// <param name="value">The boolean value.</param>
    /// <returns>The True or False singleton object.</returns>
    public static SmalltalkObject? ConvertBooleanToSmalltalkObject(bool value)
    {
        // TODO: Return proper true/false singleton from object memory
        // For now, create a minimal object
        return new SmalltalkObject(null, 0);
    }

    /// <summary>
    /// Converts a .NET integer to a Smalltalk SmallInteger object.
    /// </summary>
    /// <param name="value">The integer value.</param>
    /// <returns>A SmallInteger object.</returns>
    public static SmalltalkObject? ConvertIntegerToSmalltalkObject(int value)
    {
        // TODO: Look up SmallInteger class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET double to a Smalltalk Float object.
    /// </summary>
    /// <param name="value">The double value.</param>
    /// <returns>A Float object.</returns>
    public static SmalltalkObject? ConvertFloatToSmalltalkObject(double value)
    {
        // TODO: Look up Float class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET char to a Smalltalk Character object.
    /// </summary>
    /// <param name="value">The character value.</param>
    /// <returns>A Character object.</returns>
    public static SmalltalkObject? ConvertCharacterToSmalltalkObject(char value)
    {
        // TODO: Look up Character class from object memory
        // For now, create untyped object with value
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }

    /// <summary>
    /// Converts a .NET string to a Smalltalk String object.
    /// </summary>
    /// <param name="value">The string value.</param>
    /// <returns>A String object.</returns>
    public static SmalltalkObject? ConvertStringToSmalltalkObject(string value)
    {
        // TODO: Look up String class from object memory
        // For now, store string directly in single field
        var obj = new SmalltalkObject(null, 1);
        obj.SetField(0, value);
        return obj;
    }
}