using System;

/// <summary>
/// Provides an interactive Smalltalk-80 read–eval–print loop.
/// </summary>
public static class Repl
{
    /// <summary>
    /// Starts the interactive session using a fresh <see cref="VirtualMachine"/>.
    /// </summary>
    public static void Run()
    {
        var vm = new VirtualMachine();
        vm.RunRepl();
    }
}
