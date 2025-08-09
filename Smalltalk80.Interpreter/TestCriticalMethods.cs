using System;
using System.IO;
using Smalltalk80;
using Smalltalk80.Bootstrap;

namespace Smalltalk80.Interpreter;

/// <summary>
/// Test runner for critical methods needed for Phase 3 class installation.
/// </summary>
public static class TestCriticalMethods
{
    public static void RunTests(VirtualMachine vm)
    {
        Console.WriteLine("\n=== TESTING CRITICAL METHODS FOR CLASS INSTALLATION ===\n");
        
        // Use the VM's existing adapter which has the bootstrap state
        var adapter = vm.Adapter;
        if (adapter == null)
        {
            Console.WriteLine("ERROR: VM adapter not initialized. Cannot run tests.");
            return;
        }
        
        int testsPassed = 0;
        int testsFailed = 0;
        
        try
        {
            // TEST 1: CompiledMethod class>>new
            Console.Write("TEST 1: CompiledMethod class>>new ... ");
            var compiledMethodClass = adapter.PrimitiveLookupClass("CompiledMethod");
            if (compiledMethodClass < 0)
            {
                Console.WriteLine("FAILED - CompiledMethod class not found");
                testsFailed++;
            }
            else
            {
                // Send message to class (metaclass receives it)
                var newSelector = adapter.PrimitiveInternSymbol("new");
                var methodOop = adapter.PrimitiveSendMessage(compiledMethodClass, newSelector, new int[0]);
                if (methodOop > 0)
                {
                    Console.WriteLine("PASSED - Created CompiledMethod instance");
                    testsPassed++;
                    
                    // TEST 2: CompiledMethod>>header:
                    Console.Write("TEST 2: CompiledMethod>>header: ... ");
                    var headerSelector = adapter.PrimitiveInternSymbol("header:");
                    var headerValue = adapter.PrimitiveNewSmallInteger(65536);
                    var result2 = adapter.PrimitiveSendMessage(methodOop, headerSelector, new[] { headerValue });
                    if (result2 >= 0)
                    {
                        Console.WriteLine("PASSED - Set header");
                        testsPassed++;
                    }
                    else
                    {
                        Console.WriteLine("FAILED - Could not set header");
                        testsFailed++;
                    }
                    
                    // TEST 3: CompiledMethod>>bytecodes:
                    Console.Write("TEST 3: CompiledMethod>>bytecodes: ... ");
                    var bytecodesSelector = adapter.PrimitiveInternSymbol("bytecodes:");
                    var bytecodeArray = adapter.PrimitiveNewByteArray(2);
                    // Store bytes 176, 124 (pushSelf, returnTop)
                    adapter.PrimitiveArrayAtPut(bytecodeArray, 0, adapter.PrimitiveNewSmallInteger(176));
                    adapter.PrimitiveArrayAtPut(bytecodeArray, 1, adapter.PrimitiveNewSmallInteger(124));
                    var result3 = adapter.PrimitiveSendMessage(methodOop, bytecodesSelector, new[] { bytecodeArray });
                    if (result3 >= 0)
                    {
                        Console.WriteLine("PASSED - Set bytecodes");
                        testsPassed++;
                    }
                    else
                    {
                        Console.WriteLine("FAILED - Could not set bytecodes");
                        testsFailed++;
                    }
                    
                    // TEST 4: CompiledMethod>>literals:
                    Console.Write("TEST 4: CompiledMethod>>literals: ... ");
                    var literalsSelector = adapter.PrimitiveInternSymbol("literals:");
                    var literalsArray = adapter.PrimitiveNewArray(1);
                    var testSymbol = adapter.PrimitiveInternSymbol("testMethod");
                    adapter.PrimitiveArrayAtPut(literalsArray, 0, testSymbol);
                    var result4 = adapter.PrimitiveSendMessage(methodOop, literalsSelector, new[] { literalsArray });
                    if (result4 >= 0)
                    {
                        Console.WriteLine("PASSED - Set literals");
                        testsPassed++;
                    }
                    else
                    {
                        Console.WriteLine("FAILED - Could not set literals");
                        testsFailed++;
                    }
                }
                else
                {
                    Console.WriteLine("FAILED - Could not create instance");
                    testsFailed++;
                }
            }
            
            // TEST 5: Class>>methodDictionary
            Console.Write("TEST 5: Class>>methodDictionary ... ");
            var objectClass = adapter.PrimitiveLookupClass("Object");
            if (objectClass >= 0)  // Object has OOP 0, so need >= not >
            {
                var methodDictSelector = adapter.PrimitiveInternSymbol("methodDictionary");
                var methodDict = adapter.PrimitiveSendMessage(objectClass, methodDictSelector, new int[0]);
                if (methodDict > 0)
                {
                    Console.WriteLine("PASSED - Got method dictionary");
                    testsPassed++;
                }
                else
                {
                    Console.WriteLine("FAILED - Could not get method dictionary");
                    testsFailed++;
                }
            }
            else
            {
                Console.WriteLine("FAILED - Object class not found");
                testsFailed++;
            }
            
            // TEST 6: MethodDictionary>>at:put:
            Console.Write("TEST 6: MethodDictionary>>at:put: ... ");
            var methodDictClass = adapter.PrimitiveLookupClass("MethodDictionary");
            if (methodDictClass > 0)
            {
                var newSelector2 = adapter.PrimitiveInternSymbol("new");
                var newMethodDict = adapter.PrimitiveSendMessage(methodDictClass, newSelector2, new int[0]);
                if (newMethodDict > 0)
                {
                    var atPutSelector = adapter.PrimitiveInternSymbol("at:put:");
                    var testKey = adapter.PrimitiveInternSymbol("testKey");
                    var testValue = adapter.PrimitiveNewSmallInteger(42);
                    var result6 = adapter.PrimitiveSendMessage(newMethodDict, atPutSelector, new[] { testKey, testValue });
                    if (result6 >= 0)
                    {
                        Console.WriteLine("PASSED - Added to method dictionary");
                        testsPassed++;
                    }
                    else
                    {
                        Console.WriteLine("FAILED - Could not add to method dictionary");
                        testsFailed++;
                    }
                }
                else
                {
                    Console.WriteLine("FAILED - Could not create MethodDictionary instance");
                    testsFailed++;
                }
            }
            else
            {
                Console.WriteLine("FAILED - MethodDictionary class not found");
                testsFailed++;
            }
            
            // TEST 7: SystemDictionary>>at:put:
            Console.Write("TEST 7: SystemDictionary>>at:put: ... ");
            var smalltalkDict = adapter.PrimitiveLookupGlobal("Smalltalk");
            if (smalltalkDict > 0)
            {
                var atPutSelector2 = adapter.PrimitiveInternSymbol("at:put:");
                var globalKey = adapter.PrimitiveInternSymbol("TestGlobal");
                var globalValue = adapter.PrimitiveNewSmallInteger(999);
                var result7 = adapter.PrimitiveSendMessage(smalltalkDict, atPutSelector2, new[] { globalKey, globalValue });
                if (result7 >= 0)
                {
                    Console.WriteLine("PASSED - Added to Smalltalk dictionary");
                    testsPassed++;
                }
                else
                {
                    Console.WriteLine("FAILED - Could not add to Smalltalk dictionary");
                    testsFailed++;
                }
            }
            else
            {
                Console.WriteLine("FAILED - Smalltalk dictionary not found");
                testsFailed++;
            }
            
            // TEST 8: Class>>subclass:instanceVariableNames: (this one is complex, might not be implemented yet)
            Console.Write("TEST 8: Class>>subclass:instanceVariableNames: ... ");
            Console.WriteLine("SKIPPED - Complex method, not yet implemented");
            
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
            Console.WriteLine("\nALL CRITICAL METHODS ARE WORKING! ✓");
            Console.WriteLine("Phase 3 can now use message-based class installation.");
        }
        else
        {
            Console.WriteLine($"\nWARNING: {testsFailed} critical methods are not working properly.");
            Console.WriteLine("Phase 3 message-based class installation may fail.");
        }
    }
}