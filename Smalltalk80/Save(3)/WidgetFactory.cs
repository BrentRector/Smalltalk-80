// Stub implementation to allow cross-platform build without Windows Forms
using System;

/// <summary>
///     Provides placeholder GUI functionality for non-Windows environments.
/// </summary>
public static class WidgetFactory
{
    /// <summary>
    ///     Creates a form containing a single button. In this build the call is logged only.
    /// </summary>
    /// <param name="label">The text that would appear on the button.</param>
    public static void CreateButtonForm(string label)
    {
        Console.WriteLine($"WidgetFactory: GUI not available. Requested button '{label}'.");
    }
}