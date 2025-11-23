using System.Diagnostics.CodeAnalysis;

namespace Smalltalk80.Interpreter.Shared.Types;

/// <summary>
/// Well-known objects that the VM needs for fast access during execution.
/// Replaces the previous SpecialObjectsArray with a focused, typed API.
///
/// These objects are initialized during cold start after the actual Smalltalk
/// objects (UndefinedObject instance for Nil, True instance, False instance)
/// have been allocated and become'd with the heap manager's initial nil OOP.
/// </summary>
public readonly struct KnownObjects
{
    /// <summary>
    /// The Nil object (UndefinedObject instance).
    /// Used for uninitialized variables, empty returns, etc.
    /// </summary>
    public required SmalltalkOOP Nil { get; init; }

    /// <summary>
    /// The True object (True instance).
    /// Used for boolean true value.
    /// </summary>
    public required SmalltalkOOP True { get; init; }

    /// <summary>
    /// The False object (False instance).
    /// Used for boolean false value.
    /// </summary>
    public required SmalltalkOOP False { get; init; }

    /// <summary>
    /// The Smalltalk global (SystemDictionary instance).
    /// Root of all classes, methods, symbols, and global variables.
    /// Used for global variable lookups and symbol table access.
    /// </summary>
    public required SmalltalkOOP Smalltalk { get; init; }

    /// <summary>
    /// The Display global (DisplayScreen instance).
    /// Primary display bitmap for rendering graphics.
    /// Cached for fast access during BitBlt operations.
    /// </summary>
    public required SmalltalkOOP Display { get; init; }

    /// <summary>
    /// Creates a KnownObjects instance with all required objects.
    /// </summary>
    [SetsRequiredMembers]
    public KnownObjects(SmalltalkOOP nil, SmalltalkOOP @true, SmalltalkOOP @false, SmalltalkOOP smalltalk, SmalltalkOOP display)
    {
        Nil = nil;
        True = @true;
        False = @false;
        Smalltalk = smalltalk;
        Display = display;
    }
}
