namespace Smalltalk80.Interpreter.Core.BlueBook;

/// <summary>
/// Static accessor for the global ClassLayout instance.
/// Must be initialized during cold start bootstrap before use.
/// </summary>
public static class LayoutManagement
{
    private static ClassLayout? _layout;

    /// <summary>
    /// Gets the global ClassLayout instance.
    /// </summary>
    public static ClassLayout Layout => _layout ??
        throw new InvalidOperationException("ClassLayout not initialized. Call Initialize() during cold start.");

    /// <summary>
    /// Initializes the global ClassLayout instance.
    /// Must be called during cold start bootstrap.
    /// </summary>
    public static void Initialize(ClassLayout layout)
    {
        _layout = layout;
    }

    /// <summary>
    /// Resets the layout (for testing purposes).
    /// </summary>
    internal static void Reset()
    {
        _layout = null;
    }
}
