using System;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Test runner for UndefinedObject methods.
/// </summary>
public static class TestUndefinedObject
{
    public static void RunTests(VirtualMachine vm)
    {
        Console.WriteLine("\n=== TESTING UndefinedObject METHODS ===\n");
        
        // Enable VM debug for this test
        VMPrimitiveAdapter.EnableVMDebugOutput = false;
        
        var adapter = vm.Adapter;
        if (adapter == null)
        {
            Console.WriteLine("ERROR: VM adapter not initialized.");
            return;
        }
        
        int testsPassed = 0;
        int testsFailed = 0;
        
        try
        {
            // Get the singleton instances from ObjectMemory
            var nilObject = ObjectMemory.NilObject;
            var trueObject = ObjectMemory.TrueObject;
            var falseObject = ObjectMemory.FalseObject;
            
            if (nilObject == null || trueObject == null || falseObject == null)
            {
                Console.WriteLine("ERROR: Singleton objects not initialized in ObjectMemory");
                Console.WriteLine($"  nil: {nilObject}");
                Console.WriteLine($"  true: {trueObject}");
                Console.WriteLine($"  false: {falseObject}");
                return;
            }
            
            // Get their OOPs directly from the objects
            int nilOop = nilObject.Oop;
            int trueOop = trueObject.Oop;
            int falseOop = falseObject.Oop;
            
            Console.WriteLine($"[DEBUG] ObjectMemory.NilObject.Oop = {nilOop}");
            Console.WriteLine($"[DEBUG] ObjectMemory.TrueObject.Oop = {trueOop}");
            Console.WriteLine($"[DEBUG] ObjectMemory.FalseObject.Oop = {falseOop}");
            
            // Check nil's class
            if (nilObject.ClassPointer != null)
            {
                Console.WriteLine($"[DEBUG] nil object's class OOP = {nilObject.ClassPointer.Oop}");
                var undefinedObjectClassOop = adapter.PrimitiveLookupGlobal("UndefinedObject");
                Console.WriteLine($"[DEBUG] UndefinedObject class OOP from lookup = {undefinedObjectClassOop}");
                if (nilObject.ClassPointer.Oop != undefinedObjectClassOop)
                {
                    Console.WriteLine($"WARNING: nil's class pointer ({nilObject.ClassPointer.Oop}) doesn't match UndefinedObject ({undefinedObjectClassOop})");
                }
            }
            
            Console.WriteLine($"Singleton OOPs:");
            Console.WriteLine($"  nil: {nilOop}");
            Console.WriteLine($"  true: {trueOop}");
            Console.WriteLine($"  false: {falseOop}");
            
            // TEST 1: nil isNil should return true
            Console.Write("TEST 1: nil isNil ... ");
            var isNilSelector = adapter.PrimitiveInternSymbol("isNil");
            var result1 = adapter.PrimitiveSendMessage(nilOop, isNilSelector, new int[0]);
            
            // Check if result is true
            if (result1 == trueOop)
            {
                Console.WriteLine($"PASSED - returned true (OOP {result1})");
                testsPassed++;
            }
            else
            {
                Console.WriteLine($"FAILED - returned OOP {result1}, expected {trueOop} (true)");
                testsFailed++;
            }
            
            // TEST 2: nil notNil should return false
            Console.Write("TEST 2: nil notNil ... ");
            var notNilSelector = adapter.PrimitiveInternSymbol("notNil");
            var result2 = adapter.PrimitiveSendMessage(nilOop, notNilSelector, new int[0]);
            
            // Check if result is false
            if (result2 == falseOop)
            {
                Console.WriteLine($"PASSED - returned false (OOP {result2})");
                testsPassed++;
            }
            else
            {
                Console.WriteLine($"FAILED - returned OOP {result2}, expected {falseOop} (false)");
                testsFailed++;
            }
            
            // TEST 3: nil ifNotNil: [42] should return nil
            Console.Write("TEST 3: nil ifNotNil: [block] ... ");
            var ifNotNilSelector = adapter.PrimitiveInternSymbol("ifNotNil:");
            
            // For now, pass a dummy argument (blocks are complex)
            var dummyBlock = adapter.PrimitiveNewSmallInteger(42);
            var result3 = adapter.PrimitiveSendMessage(nilOop, ifNotNilSelector, new[] { dummyBlock });
            
            if (result3 == nilOop)
            {
                Console.WriteLine($"PASSED - returned nil (OOP {result3})");
                testsPassed++;
            }
            else
            {
                Console.WriteLine($"FAILED - returned OOP {result3}, expected {nilOop} (nil)");
                testsFailed++;
            }
            
            // TEST 4: nil deepCopy should return nil
            Console.Write("TEST 4: nil deepCopy ... ");
            var deepCopySelector = adapter.PrimitiveInternSymbol("deepCopy");
            var result4 = adapter.PrimitiveSendMessage(nilOop, deepCopySelector, new int[0]);
            
            if (result4 == nilOop)
            {
                Console.WriteLine($"PASSED - returned nil (OOP {result4})");
                testsPassed++;
            }
            else
            {
                Console.WriteLine($"FAILED - returned OOP {result4}, expected {nilOop} (nil)");
                testsFailed++;
            }
            
            // TEST 5: nil shallowCopy should return nil
            Console.Write("TEST 5: nil shallowCopy ... ");
            var shallowCopySelector = adapter.PrimitiveInternSymbol("shallowCopy");
            var result5 = adapter.PrimitiveSendMessage(nilOop, shallowCopySelector, new int[0]);
            
            if (result5 == nilOop)
            {
                Console.WriteLine($"PASSED - returned nil (OOP {result5})");
                testsPassed++;
            }
            else
            {
                Console.WriteLine($"FAILED - returned OOP {result5}, expected {nilOop} (nil)");
                testsFailed++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            testsFailed++;
        }
        
        Console.WriteLine($"\n=== TEST RESULTS ===");
        Console.WriteLine($"Passed: {testsPassed}");
        Console.WriteLine($"Failed: {testsFailed}");
        Console.WriteLine($"Total:  {testsPassed + testsFailed}");
        
        if (testsFailed == 0)
        {
            Console.WriteLine("\nALL UndefinedObject METHODS WORKING! ✓");
        }
        else
        {
            Console.WriteLine($"\nWARNING: {testsFailed} tests failed.");
        }
    }
}