using System;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Primitives;
using Smalltalk80.Interpreter.VM;

namespace TestMessageDispatcher;

/// <summary>
/// Test program to verify MessageDispatcher and VM Send/SuperSend implementation.
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("[TEST] Testing MessageDispatcher and VM Send/SuperSend implementation...");
        Console.WriteLine();
        
        try
        {
            // Create VM instance
            var vm = new VirtualMachine();
            
            // Bootstrap the system
            Console.WriteLine("[TEST] Bootstrapping VM...");
            vm.Bootstrap();
            Console.WriteLine("[TEST] Bootstrap complete!");
            Console.WriteLine();
            
            // Test basic object creation
            Console.WriteLine("[TEST] Testing object creation...");
            var objectClass = vm.GetClass("Object");
            if (objectClass.IsNil)
            {
                Console.WriteLine("[ERROR] Object class not found!");
                return;
            }
            Console.WriteLine($"  Object class: {objectClass}");
            
            // Create a simple object
            var obj = vm.NewInstance(objectClass, 0);
            Console.WriteLine($"  Created instance: {obj}");
            Console.WriteLine();
            
            // Test symbol interning
            Console.WriteLine("[TEST] Testing symbol interning...");
            var selector1 = vm.InternSymbol("test");
            var selector2 = vm.InternSymbol("test");
            var selector3 = vm.InternSymbol("other");
            
            Console.WriteLine($"  'test' -> {selector1}");
            Console.WriteLine($"  'test' again -> {selector2}");
            Console.WriteLine($"  'other' -> {selector3}");
            Console.WriteLine($"  Same OOP for 'test'? {selector1.RawValue == selector2.RawValue}");
            Console.WriteLine();
            
            // Test MessageDispatcher caching
            Console.WriteLine("[TEST] Testing MessageDispatcher caching...");
            var dispatcher = new MessageDispatcher(vm.Memory, new PrimitiveTable(), vm);
            dispatcher.Initialize();
            
            // Simulate method lookup (will fail as we don't have methods installed yet)
            var testSelector = vm.InternSymbol("class");
            var (method, methodClass) = dispatcher.Dispatch(obj, testSelector, []);
            Console.WriteLine($"  Lookup 'class' on Object instance: method={method}, class={methodClass}");
            
            // Check cache statistics
            var stats = dispatcher.GetCacheStatistics();
            Console.WriteLine($"  Cache stats: {stats}");
            Console.WriteLine();
            
            // Test BlockContext creation
            Console.WriteLine("[TEST] Testing BlockContext creation...");
            var methodContext = new MethodContext(
                sender: null,
                receiver: obj,
                method: OOP.Nil,
                arguments: [],
                tempCount: 2,
                stackSize: 16
            );
            
            var blockContext = BlockContextFactory.CreateBlock(
                homeContext: methodContext,
                argCount: 1,
                initialPC: 10,
                tempCount: 1,
                stackSize: 8
            );
            
            Console.WriteLine($"  Created block: {blockContext}");
            Console.WriteLine($"  Block home: {blockContext.GetHome()}");
            Console.WriteLine($"  Can return non-local? {blockContext.CanReturnNonLocal}");
            Console.WriteLine();
            
            // Test block activation
            Console.WriteLine("[TEST] Testing block activation...");
            var arg = OOP.FromSmallInteger(42);
            BlockContextFactory.ActivateBlock(blockContext, [arg]);
            Console.WriteLine($"  Activated block with argument: {arg}");
            Console.WriteLine($"  Block PC: {blockContext.PC}");
            Console.WriteLine($"  Block arguments[0]: {blockContext.Arguments[0]}");
            Console.WriteLine();
            
            Console.WriteLine("[TEST] All tests completed successfully!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }
}