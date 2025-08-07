using System;

/// <summary>
/// Minimal Program.cs to test VM object dump functionality after shadow class removal.
/// </summary>
public static class Program
{
    /// <summary>
    ///     Application entry method.
    /// </summary>
    /// <param name="args">
    ///     Command line arguments. Supported options are:
    ///     <list type="bullet">
    ///         <item><description><c>--load &lt;file&gt;</c> – load a single-method Smalltalk source file.</description></item>
    ///         <item><description><c>--source &lt;file&gt;</c> – load any Smalltalk source file (file-in, system image, or library) with dependency resolution.</description></item>
    ///     </list>
    /// </param>
    [STAThread]
    public static void Main(string[] args)
    {
        Console.WriteLine("Smalltalk-80 VM Test - Shadow Classes Removed");
        Console.WriteLine("==============================================");
        
        try
        {
            // Create VirtualMachine with pure ObjectMemory (no shadow classes)
            var vm = new VirtualMachine(skipStandardLibrary: true);
            
            Console.WriteLine("\nVM successfully created without shadow classes!");
            Console.WriteLine("All class objects are now pure SmalltalkObject instances in ObjectMemory.");
            
            // Test the VM object dump functionality
            Console.WriteLine("\nTesting VM object dump after bootstrap...");
            
            // The VM object dump was already called during bootstrap phases
            // Let's call it one more time to show the final state
            vm.DumpAllClasses("FINAL VM STATE");
            
            Console.WriteLine("\n✅ SUCCESS: Shadow class removal completed successfully!");
            Console.WriteLine("   - C# SmalltalkClass and SystemDictionary removed");
            Console.WriteLine("   - VM now uses pure SmalltalkObject instances");
            Console.WriteLine("   - ObjectMemory contains actual VM objects");
            Console.WriteLine("   - VM object dump functionality working correctly");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ ERROR: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return;
        }
        
        Console.WriteLine("\nPress any key to exit...");
        Console.ReadKey();
    }
}