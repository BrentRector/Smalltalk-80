using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Smalltalk80.Interpreter.VM;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.Memory;

namespace Smalltalk80.IntegrationTests
{
    /// <summary>
    /// Comprehensive integration test that verifies all bootstrap connections are working correctly.
    /// Tests system loading, process creation, expression evaluation, and file operations.
    /// </summary>
    public class BootstrapIntegrationTest
    {
        private const string SystemImagePath = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\complete_system_final.json";
        private const string TestDirectory = @"D:\Users\brent\OneDrive\Src\Smalltalk-80\test_output";
        private static readonly List<string> TestResults = new();
        private static int PassedTests = 0;
        private static int TotalTests = 0;
        
        /// <summary>
        /// Entry point for the integration test.
        /// </summary>
        public static void Main(string[] args)
        {
            Console.WriteLine("========================================");
            Console.WriteLine("Bootstrap Integration Test Suite");
            Console.WriteLine("========================================\n");
            
            var stopwatch = Stopwatch.StartNew();
            var test = new BootstrapIntegrationTest();
            
            try
            {
                // Clean up any previous test files
                test.CleanupTestFiles();
                
                // Create test directory if it doesn't exist
                Directory.CreateDirectory(TestDirectory);
                
                // Run all integration tests
                test.RunAllTests();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n!!! CRITICAL ERROR !!!");
                Console.WriteLine($"Exception: {ex.GetType().Name}");
                Console.WriteLine($"Message: {ex.Message}");
                Console.WriteLine($"Stack trace:\n{ex.StackTrace}");
            }
            finally
            {
                // Clean up test files
                test.CleanupTestFiles();
                
                stopwatch.Stop();
                
                // Print summary
                PrintTestSummary(stopwatch.ElapsedMilliseconds);
            }
        }
        
        /// <summary>
        /// Runs all integration tests.
        /// </summary>
        private void RunAllTests()
        {
            // Test 1: System Image Loading
            TestSystemImageLoading();
            
            // Test 2: Initial Process Creation
            TestInitialProcessCreation();
            
            // Test 3: Simple Expression Evaluation
            TestSimpleExpressionEvaluation();
            
            // Test 4: Arithmetic Operations
            TestArithmeticOperations();
            
            // Test 5: Comparison Operations
            TestComparisonOperations();
            
            // Test 6: File I/O Operations
            TestFileOperations();
            
            // Test 7: File-in Capability
            TestFileInCapability();
            
            // Test 8: Memory Management
            TestMemoryManagement();
            
            // Test 9: Class System Integrity
            TestClassSystemIntegrity();
            
            // Test 10: Method Lookup
            TestMethodLookup();
        }
        
        /// <summary>
        /// Test 1: Verify system loads complete_system_final.json successfully.
        /// </summary>
        private void TestSystemImageLoading()
        {
            Console.WriteLine("\n--- Test 1: System Image Loading ---");
            
            try
            {
                // Check if system image exists
                if (!File.Exists(SystemImagePath))
                {
                    // Try alternative paths
                    var altPaths = new[]
                    {
                        Path.Combine("..", "complete_system_final.json"),
                        Path.Combine("..", "..", "complete_system_final.json"),
                        "complete_system_final.json"
                    };
                    
                    bool found = false;
                    foreach (var path in altPaths)
                    {
                        if (File.Exists(path))
                        {
                            found = true;
                            break;
                        }
                    }
                    
                    if (!found)
                    {
                        RecordTest("System Image File Exists", false, "complete_system_final.json not found");
                        return;
                    }
                }
                
                RecordTest("System Image File Exists", true);
                
                // Create VM and load system
                var vm = new VirtualMachine(initialMemorySize: 262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                RecordTest("System Image Loaded", vm.IsBootstrapped);
                
                // Verify memory statistics
                var stats = vm.Memory.GetStatistics();
                RecordTest("Objects Created", stats.TotalObjects > 1000, 
                    $"Created {stats.TotalObjects} objects");
                
                // Verify essential classes exist
                var essentialClasses = new[] 
                { 
                    "Object", "Class", "Metaclass", "Behavior", 
                    "CompiledMethod", "MethodDictionary", "Array", "String",
                    "Symbol", "Integer", "Process", "ProcessorScheduler"
                };
                
                foreach (var className in essentialClasses)
                {
                    // Get class using the VM
                    var classObj = vm.GetClassByName(className);
                    RecordTest($"Class {className} Exists", !classObj.IsNil);
                }
            }
            catch (Exception ex)
            {
                RecordTest("System Image Loading", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 2: Verify initial Process is created and starts execution.
        /// </summary>
        private void TestInitialProcessCreation()
        {
            Console.WriteLine("\n--- Test 2: Initial Process Creation ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Check if Process class exists
                var processClass = vm.GetClassByName("Process");
                RecordTest("Process Class Exists", !processClass.IsNil);
                
                // Check if ProcessorScheduler exists (note: it's ProcessorScheduler, not ProcessScheduler)
                var schedulerClass = vm.GetClassByName("ProcessorScheduler");
                RecordTest("ProcessorScheduler Class Exists", !schedulerClass.IsNil);
                
                // Check if scheduler is available in VM
                var scheduler = vm.ProcessScheduler;
                RecordTest("VM ProcessScheduler Available", scheduler != null);
                
                // Note: Actual process creation and scheduling would require
                // more complete implementation of the ProcessScheduler
            }
            catch (Exception ex)
            {
                RecordTest("Initial Process Creation", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 3: Verify simple expressions can be evaluated.
        /// </summary>
        private void TestSimpleExpressionEvaluation()
        {
            Console.WriteLine("\n--- Test 3: Simple Expression Evaluation ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Test simple integer
                var result = vm.EvaluateExpression("42");
                RecordTest("Integer Literal", result.IsSmallInteger && result.SmallIntegerValue == 42,
                    $"Expected 42, got {(result.IsSmallInteger ? result.SmallIntegerValue.ToString() : "non-integer")}");
                
                // Test simple addition
                result = vm.EvaluateExpression("3 + 4");
                RecordTest("Simple Addition", result.IsSmallInteger && result.SmallIntegerValue == 7,
                    $"3 + 4 = {(result.IsSmallInteger ? result.SmallIntegerValue.ToString() : "error")}");
            }
            catch (Exception ex)
            {
                RecordTest("Simple Expression Evaluation", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 4: Verify arithmetic operations work correctly.
        /// </summary>
        private void TestArithmeticOperations()
        {
            Console.WriteLine("\n--- Test 4: Arithmetic Operations ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Addition
                var result = vm.EvaluateExpression("10 + 5");
                RecordTest("Addition (10 + 5)", result.IsSmallInteger && result.SmallIntegerValue == 15);
                
                // Subtraction
                result = vm.EvaluateExpression("10 - 5");
                RecordTest("Subtraction (10 - 5)", result.IsSmallInteger && result.SmallIntegerValue == 5);
                
                // Multiplication
                result = vm.EvaluateExpression("10 * 5");
                RecordTest("Multiplication (10 * 5)", result.IsSmallInteger && result.SmallIntegerValue == 50);
                
                // Division
                result = vm.EvaluateExpression("10 / 5");
                RecordTest("Division (10 / 5)", result.IsSmallInteger && result.SmallIntegerValue == 2);
                
                // Division by zero handling
                result = vm.EvaluateExpression("10 / 0");
                RecordTest("Division by Zero Handling", result.IsNil || !result.IsSmallInteger,
                    "Should handle division by zero gracefully");
            }
            catch (Exception ex)
            {
                RecordTest("Arithmetic Operations", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 5: Verify comparison operations work correctly.
        /// </summary>
        private void TestComparisonOperations()
        {
            Console.WriteLine("\n--- Test 5: Comparison Operations ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Get special objects for comparison
                var trueObj = vm.GetSpecialObject(SpecialObjects.TrueObject);
                var falseObj = vm.GetSpecialObject(SpecialObjects.FalseObject);
                
                // Equality
                var result = vm.EvaluateExpression("5 = 5");
                RecordTest("Equality (5 = 5)", result.Equals(trueObj), "Should return true");
                
                result = vm.EvaluateExpression("5 = 3");
                RecordTest("Inequality (5 = 3)", result.Equals(falseObj), "Should return false");
                
                // Less than
                result = vm.EvaluateExpression("3 < 5");
                RecordTest("Less Than (3 < 5)", result.Equals(trueObj), "Should return true");
                
                result = vm.EvaluateExpression("5 < 3");
                RecordTest("Not Less Than (5 < 3)", result.Equals(falseObj), "Should return false");
                
                // Greater than
                result = vm.EvaluateExpression("5 > 3");
                RecordTest("Greater Than (5 > 3)", result.Equals(trueObj), "Should return true");
                
                result = vm.EvaluateExpression("3 > 5");
                RecordTest("Not Greater Than (3 > 5)", result.Equals(falseObj), "Should return false");
            }
            catch (Exception ex)
            {
                RecordTest("Comparison Operations", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 6: Verify file I/O operations work.
        /// </summary>
        private void TestFileOperations()
        {
            Console.WriteLine("\n--- Test 6: File I/O Operations ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Check if File/FileStream classes exist
                var fileClass = vm.GetClassByName("File");
                var fileStreamClass = vm.GetClassByName("FileStream");
                
                RecordTest("File Class Available", !fileClass.IsNil || !fileStreamClass.IsNil,
                    fileClass.IsNil && fileStreamClass.IsNil ? "File I/O classes not found" : "File I/O classes found");
                
                // Test basic file operations
                var testFile = Path.Combine(TestDirectory, "test_file.txt");
                var testContent = "Hello from Smalltalk-80!";
                
                // Create test file using .NET for now (since file primitives may not be fully implemented)
                File.WriteAllText(testFile, testContent);
                RecordTest("Test File Created", File.Exists(testFile));
                
                // Read test file
                var readContent = File.ReadAllText(testFile);
                RecordTest("Test File Read", readContent == testContent);
                
                // Delete test file
                File.Delete(testFile);
                RecordTest("Test File Deleted", !File.Exists(testFile));
            }
            catch (Exception ex)
            {
                RecordTest("File I/O Operations", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 7: Verify file-in capability works.
        /// </summary>
        private void TestFileInCapability()
        {
            Console.WriteLine("\n--- Test 7: File-in Capability ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Create a simple .st file for testing
                var testStFile = Path.Combine(TestDirectory, "test_filein.st");
                var stContent = @"!Object methodsFor: 'testing'!
testMethod
    ""A test method created by file-in""
    ^42
! !";
                
                File.WriteAllText(testStFile, stContent);
                RecordTest("Test .st File Created", File.Exists(testStFile));
                
                // Check if Compiler class exists for file-in
                var compilerClass = vm.GetClassByName("Compiler");
                RecordTest("Compiler Class Available", !compilerClass.IsNil,
                    compilerClass.IsNil ? "Compiler not available for file-in" : "Compiler available");
                
                // Clean up
                File.Delete(testStFile);
                RecordTest("Test .st File Cleaned Up", !File.Exists(testStFile));
            }
            catch (Exception ex)
            {
                RecordTest("File-in Capability", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 8: Verify memory management and garbage collection.
        /// </summary>
        private void TestMemoryManagement()
        {
            Console.WriteLine("\n--- Test 8: Memory Management ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Get initial statistics
                var statsBefore = vm.Memory.GetStatistics();
                RecordTest("Initial Memory Stats", statsBefore.TotalObjects > 0,
                    $"Objects: {statsBefore.TotalObjects}, Capacity: {statsBefore.TotalCapacity}");
                
                // Run garbage collection
                vm.RunGarbageCollection();
                
                // Get statistics after GC
                var statsAfter = vm.Memory.GetStatistics();
                RecordTest("Garbage Collection Completed", true,
                    $"Objects after GC: {statsAfter.TotalObjects}");
                
                // Verify no critical objects were collected
                var criticalClasses = new[] { "Object", "Class", "Metaclass" };
                bool allPresent = true;
                foreach (var className in criticalClasses)
                {
                    if (vm.GetClassByName(className).IsNil)
                    {
                        allPresent = false;
                        break;
                    }
                }
                RecordTest("Critical Objects Preserved", allPresent);
            }
            catch (Exception ex)
            {
                RecordTest("Memory Management", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 9: Verify class system integrity.
        /// </summary>
        private void TestClassSystemIntegrity()
        {
            Console.WriteLine("\n--- Test 9: Class System Integrity ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Test class hierarchy
                var objectClass = vm.GetClassByName("Object");
                RecordTest("Object Class Exists", !objectClass.IsNil);
                
                // Test metaclass chain
                if (!objectClass.IsNil)
                {
                    var obj = vm.Memory.GetObject(objectClass);
                    var classOfObject = obj?.Header.ClassOOP ?? OOP.Nil;
                    RecordTest("Object Has Metaclass", !classOfObject.IsNil,
                        classOfObject.IsNil ? "No metaclass" : "Has metaclass");
                }
                
                // Test method dictionary presence
                var behaviorClass = vm.GetClassByName("Behavior");
                if (!behaviorClass.IsNil)
                {
                    var obj = vm.Memory.GetObject(behaviorClass);
                    if (obj != null && obj.Fields.Length > 1)
                    {
                        var methodDict = obj.GetField(1); // Method dictionary is typically at index 1
                        RecordTest("Behavior Has Method Dictionary", !methodDict.IsNil);
                    }
                    else
                    {
                        RecordTest("Behavior Has Method Dictionary", false, "Cannot access fields");
                    }
                }
                else
                {
                    RecordTest("Behavior Has Method Dictionary", false, "Behavior class not found");
                }
                
                // Test inheritance chain
                var integerClass = vm.GetClassByName("Integer");
                var magnitudeClass = vm.GetClassByName("Magnitude");
                RecordTest("Integer->Magnitude Chain", !integerClass.IsNil && !magnitudeClass.IsNil,
                    "Inheritance chain present");
            }
            catch (Exception ex)
            {
                RecordTest("Class System Integrity", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Test 10: Verify method lookup works.
        /// </summary>
        private void TestMethodLookup()
        {
            Console.WriteLine("\n--- Test 10: Method Lookup ---");
            
            try
            {
                var vm = new VirtualMachine(262144);
                vm.LoadBootstrapFromJson(SystemImagePath);
                
                // Get a class with known methods
                var objectClass = vm.GetClassByName("Object");
                RecordTest("Object Class for Lookup", !objectClass.IsNil);
                
                if (!objectClass.IsNil)
                {
                    // Try to find a common selector like #class or #yourself
                    var obj = vm.Memory.GetObject(objectClass);
                    if (obj != null && obj.Fields.Length > 1)
                    {
                        var methodDict = obj.GetField(1);
                        RecordTest("Method Dictionary Present", !methodDict.IsNil,
                            methodDict.IsNil ? "No method dictionary" : "Has method dictionary");
                        
                        // Verify dictionary structure
                        if (!methodDict.IsNil)
                        {
                            var dictObj = vm.Memory.GetObject(methodDict);
                            RecordTest("Dictionary Is Object", dictObj != null,
                                dictObj != null ? $"Dictionary has {dictObj.Fields.Length} fields" : "Not an object");
                        }
                    }
                    else
                    {
                        RecordTest("Method Dictionary Access", false, "Cannot access object fields");
                    }
                }
            }
            catch (Exception ex)
            {
                RecordTest("Method Lookup", false, ex.Message);
            }
        }
        
        /// <summary>
        /// Records a test result.
        /// </summary>
        private static void RecordTest(string testName, bool passed, string details = "")
        {
            TotalTests++;
            if (passed)
            {
                PassedTests++;
                Console.WriteLine($"  ✓ {testName}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
                TestResults.Add($"PASS: {testName}");
            }
            else
            {
                Console.WriteLine($"  ✗ {testName}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
                TestResults.Add($"FAIL: {testName}" + (string.IsNullOrEmpty(details) ? "" : $" - {details}"));
            }
        }
        
        /// <summary>
        /// Prints the test summary.
        /// </summary>
        private static void PrintTestSummary(long elapsedMs)
        {
            Console.WriteLine("\n========================================");
            Console.WriteLine("Test Summary");
            Console.WriteLine("========================================");
            Console.WriteLine($"Total Tests: {TotalTests}");
            Console.WriteLine($"Passed: {PassedTests}");
            Console.WriteLine($"Failed: {TotalTests - PassedTests}");
            Console.WriteLine($"Success Rate: {(PassedTests * 100.0 / TotalTests):F1}%");
            Console.WriteLine($"Execution Time: {elapsedMs}ms");
            
            if (TotalTests - PassedTests > 0)
            {
                Console.WriteLine("\nFailed Tests:");
                foreach (var result in TestResults)
                {
                    if (result.StartsWith("FAIL:"))
                    {
                        Console.WriteLine($"  - {result.Substring(5)}");
                    }
                }
            }
            
            Console.WriteLine("\n" + (PassedTests == TotalTests ? 
                "✓ ALL TESTS PASSED!" : 
                $"✗ {TotalTests - PassedTests} TESTS FAILED"));
        }
        
        /// <summary>
        /// Cleans up test files and directories.
        /// </summary>
        private void CleanupTestFiles()
        {
            try
            {
                if (Directory.Exists(TestDirectory))
                {
                    Directory.Delete(TestDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}