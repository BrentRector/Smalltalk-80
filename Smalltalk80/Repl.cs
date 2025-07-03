using System;

/// <summary>
/// Provides an interactive Smalltalk-80 read–eval–print loop.
/// </summary>
public static class Repl
{
    /// <summary>
    /// Starts the interactive session using the provided
    /// <see cref="VirtualMachine"/> instance or a new one if <c>null</c>.
    /// </summary>
    /// <param name="vm">Optional virtual machine to host the session.</param>
    public static void Run(VirtualMachine? vm = null)
    {
        vm ??= new VirtualMachine();
        vm.RunRepl();
    }
}
