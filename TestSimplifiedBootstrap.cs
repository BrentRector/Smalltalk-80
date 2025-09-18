using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace TestSimplifiedBootstrap
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("Testing simplified bootstrap approach...");
            
            try
            {
                var vm = new VirtualMachine();
                Console.WriteLine("Bootstrap completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Bootstrap failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }
    }
}