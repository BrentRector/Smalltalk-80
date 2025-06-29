// Stub implementation to allow cross-platform build without Windows Forms
using System;

public static class WidgetFactory {
    public static void CreateButtonForm(string label) {
        Console.WriteLine($"WidgetFactory: GUI not available. Requested button '{label}'.");
    }
}