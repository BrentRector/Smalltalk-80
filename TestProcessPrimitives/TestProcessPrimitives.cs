using System;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;
using Smalltalk80.Interpreter.ColdStart;

namespace Smalltalk80.Tests;

/// <summary>
/// Test program to verify Process and Semaphore primitive implementations.
/// </summary>
public static class TestProcessPrimitives
{
    /// <summary>
    /// Main entry point for testing process primitives.
    /// </summary>
    public static void Main(string[] args)
    {
        Console.WriteLine("=== Testing Process and Semaphore Primitives ===");

        try
        {
            // Initialize the VM
            Console.WriteLine("\n1. Initializing VM...");

            // Bootstrap classes (ObjectMemory initializes itself)
            ColdStartBootstrap.Initialize();

            // Initialize process system
            ProcessSystemInitializer.Initialize();

            Console.WriteLine("   VM initialized successfully.");

            // Test Semaphore creation
            Console.WriteLine("\n2. Testing Semaphore creation...");
            var semaphore = CreateTestSemaphore();
            Console.WriteLine($"   Created semaphore (OOP: {semaphore.AsObjectPointer():X})");

            // Test Process creation
            Console.WriteLine("\n3. Testing Process creation...");
            var process1 = CreateTestProcess(3);
            var process2 = CreateTestProcess(5);
            Console.WriteLine($"   Created process 1 with priority 3 (OOP: {process1.AsObjectPointer():X})");
            Console.WriteLine($"   Created process 2 with priority 5 (OOP: {process2.AsObjectPointer():X})");

            // Test Semaphore signal (primitive 85)
            Console.WriteLine("\n4. Testing Semaphore signal (no waiting processes)...");
            TestSemaphoreSignal(semaphore);

            // Test Semaphore wait (primitive 86)
            Console.WriteLine("\n5. Testing Semaphore wait (with excess signals)...");
            TestSemaphoreWait(semaphore);

            // Test Process resume (primitive 87)
            Console.WriteLine("\n6. Testing Process resume...");
            TestProcessResume(process1);

            // Test Process suspend (primitive 88)
            Console.WriteLine("\n7. Testing Process suspend...");
            TestProcessSuspend();

            Console.WriteLine("\n=== All tests completed successfully! ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n!!! Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            Environment.Exit(1);
        }
    }

    /// <summary>
    /// Creates a test semaphore.
    /// </summary>
    private static SObjectRef CreateTestSemaphore()
    {
        var semaphoreClass = SystemDictionary.LookupClassRef("Semaphore");
        if (!semaphoreClass.HasValue || semaphoreClass.Value.IsNil)
            throw new Exception("Semaphore class not found");

        var semaphoreRef = ObjectMemory.CreateInstance(
            semaphoreClass.Value.AsObjectPointer(),
            0); // No indexed slots

        var semaphore = new STSemaphore(semaphoreRef);

        // Initialize instance variables
        semaphore.FirstLink = SObjectRef.Nil;
        semaphore.LastLink = SObjectRef.Nil;
        semaphore.ExcessSignals = SObjectRef.FromSmallInteger(0);

        return semaphoreRef;
    }

    /// <summary>
    /// Creates a test process with specified priority.
    /// </summary>
    private static SObjectRef CreateTestProcess(int priority)
    {
        var processClass = SystemDictionary.LookupClassRef("Process");
        if (!processClass.HasValue || processClass.Value.IsNil)
            throw new Exception("Process class not found");

        var processRef = ObjectMemory.CreateInstance(
            processClass.Value.AsObjectPointer(),
            0); // No indexed slots

        var process = new STProcess(processRef);

        // Initialize instance variables
        process.NextLink = SObjectRef.Nil;
        process.SuspendedContext = SObjectRef.Nil; // Would normally be a context
        process.Priority = SObjectRef.FromSmallInteger(priority);
        process.MyList = SObjectRef.Nil;

        return processRef;
    }

    /// <summary>
    /// Tests semaphore signal primitive.
    /// </summary>
    private static void TestSemaphoreSignal(SObjectRef semaphoreRef)
    {
        var semaphore = new STSemaphore(semaphoreRef);

        Console.WriteLine($"   Before signal: excessSignals = {semaphore.ExcessSignals.AsSmallInteger()}");

        // Simulate calling primitive 85
        var context = new Smalltalk80.Interpreter.Primitives.InterpreterPrimitiveContext(null!);
        var result = Smalltalk80.Interpreter.Primitives.ControlPrimitives.PrimitiveSignal(
            semaphoreRef,
            new SObjectRef[0],
            context);

        if (!result.Success)
            throw new Exception($"Signal primitive failed: {result.FailureReason}");

        Console.WriteLine($"   After signal: excessSignals = {semaphore.ExcessSignals.AsSmallInteger()}");

        if (semaphore.ExcessSignals.AsSmallInteger() != 1)
            throw new Exception("Signal did not increment excessSignals");
    }

    /// <summary>
    /// Tests semaphore wait primitive.
    /// </summary>
    private static void TestSemaphoreWait(SObjectRef semaphoreRef)
    {
        var semaphore = new STSemaphore(semaphoreRef);

        Console.WriteLine($"   Before wait: excessSignals = {semaphore.ExcessSignals.AsSmallInteger()}");

        // Simulate calling primitive 86
        var context = new Smalltalk80.Interpreter.Primitives.InterpreterPrimitiveContext(null!);
        var result = Smalltalk80.Interpreter.Primitives.ControlPrimitives.PrimitiveWait(
            semaphoreRef,
            new SObjectRef[0],
            context);

        if (!result.Success)
            throw new Exception($"Wait primitive failed: {result.FailureReason}");

        Console.WriteLine($"   After wait: excessSignals = {semaphore.ExcessSignals.AsSmallInteger()}");

        if (semaphore.ExcessSignals.AsSmallInteger() != 0)
            throw new Exception("Wait did not decrement excessSignals");
    }

    /// <summary>
    /// Tests process resume primitive.
    /// </summary>
    private static void TestProcessResume(SObjectRef processRef)
    {
        var process = new STProcess(processRef);

        Console.WriteLine($"   Process priority: {process.Priority.AsSmallInteger()}");
        Console.WriteLine($"   Process myList before: {(process.MyList.IsNil ? "nil" : process.MyList.AsObjectPointer().ToString("X"))}");

        // Simulate calling primitive 87
        var context = new Smalltalk80.Interpreter.Primitives.InterpreterPrimitiveContext(null!);
        var result = Smalltalk80.Interpreter.Primitives.ControlPrimitives.PrimitiveResume(
            processRef,
            new SObjectRef[0],
            context);

        if (!result.Success)
            throw new Exception($"Resume primitive failed: {result.FailureReason}");

        Console.WriteLine($"   Process myList after: {(process.MyList.IsNil ? "nil" : process.MyList.AsObjectPointer().ToString("X"))}");
        Console.WriteLine("   Process successfully added to scheduler queue");
    }

    /// <summary>
    /// Tests process suspend primitive.
    /// </summary>
    private static void TestProcessSuspend()
    {
        // Get the active process
        var schedulerRef = SystemDictionary.LookupGlobalRef("Processor");
        if (!schedulerRef.HasValue || schedulerRef.Value.IsNil)
            throw new Exception("ProcessorScheduler not found");

        var scheduler = new STProcessorScheduler(schedulerRef.Value);
        var activeProcess = scheduler.ActiveProcess;

        Console.WriteLine($"   Active process (OOP: {activeProcess.AsObjectPointer():X})");

        // Try to suspend a non-active process (should fail)
        var dummyProcess = CreateTestProcess(2);
        var context = new Smalltalk80.Interpreter.Primitives.InterpreterPrimitiveContext(null!);
        var result = Smalltalk80.Interpreter.Primitives.ControlPrimitives.PrimitiveSuspend(
            dummyProcess,
            new SObjectRef[0],
            context);

        if (result.Success)
            throw new Exception("Suspend should have failed for non-active process");

        Console.WriteLine($"   Correctly rejected suspend of non-active process");
        Console.WriteLine($"   Failure reason: {result.FailureReason}");
    }
}