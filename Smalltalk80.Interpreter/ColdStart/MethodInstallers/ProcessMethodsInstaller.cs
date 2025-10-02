namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs kernel methods for process-related classes including Process, 
/// ProcessorScheduler, Semaphore, LinkedList, and Link.
/// </summary>
public static class ProcessMethodsInstaller
{
    /// <summary>
    /// Installs all process-related methods.
    /// </summary>
    public static void Install()
    {
        InstallProcessMethods();
        InstallProcessorSchedulerMethods();
        InstallSemaphoreMethods();
        InstallLinkedListMethods();
        InstallLinkMethods();
    }
    
    /// <summary>
    /// Installs Process methods.
    /// </summary>
    private static void InstallProcessMethods()
    {
        var processClassRef = SystemDictionary.ProcessClassRef;
        if (processClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Process instance methods: #resume, #suspend, #terminate, #priority, #priority:, #suspendedContext, #suspendedContext:, #offList");
        }
        
        // resume - primitive 87
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "resume",
            [], // No fallback needed - primitive 87 cannot fail
            [],
            0, 0, 87);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "resume", method1);
        instanceMethodCount++;

        // suspend - primitive 88
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "suspend",
            [], // No fallback needed - primitive 88 cannot fail
            [],
            0, 0, 88);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "suspend", method2);
        instanceMethodCount++;

        // terminate - primitive 91
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "terminate",
            [], // No fallback needed - primitive 91 cannot fail
            [],
            0, 0, 91);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "terminate", method3);
        instanceMethodCount++;

        // priority
        // ^priority
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "priority",
            [0x01, 0x7C], // Push priority instance var (index 1), return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "priority", method4);
        instanceMethodCount++;

        // priority: anInteger
        // priority := anInteger
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "priority:",
            [0x10, 0x61, 0x70, 0x7C], // Push arg, store in priority (index 1), push self, return
            [],
            1, 0);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "priority:", method5);
        instanceMethodCount++;

        // suspendedContext
        // ^suspendedContext
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "suspendedContext",
            [0x00, 0x7C], // Push suspendedContext instance var (index 0), return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "suspendedContext", method6);
        instanceMethodCount++;

        // suspendedContext: aContext (private)
        // suspendedContext := aContext
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "suspendedContext:",
            [0x10, 0x60, 0x70, 0x7C], // Push arg, store in suspendedContext (index 0), push self, return
            [],
            1, 0);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "suspendedContext:", method7);
        instanceMethodCount++;

        // offList
        // myList := nil
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            "offList",
            [0x71, 0x62, 0x70, 0x7C], // Push nil, store in myList (index 2), push self, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processClassRef.AsObjectPointer(), "offList", method8);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Process: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs ProcessorScheduler methods.
    /// </summary>
    private static void InstallProcessorSchedulerMethods()
    {
        var processorSchedulerClassRef = SystemDictionary.ProcessorSchedulerClassRef;
        if (processorSchedulerClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing ProcessorScheduler instance methods");
        }
        
        // activeProcess
        // ^activeProcess
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "activeProcess",
            [0x01, 0x7C], // Push activeProcess instance var, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "activeProcess", method9);
        instanceMethodCount++;

        // activePriority
        // ^activeProcess priority
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "activePriority",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send priority, return
            [ObjectMemoryNew.InternSymbol("priority")],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "activePriority", method10);
        instanceMethodCount++;

        // highestPriority
        // ^quiescentProcessLists size
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "highestPriority",
            [0x00, 0xD0, 0x7C], // Push quiescentProcessLists, send size, return
            [ObjectMemoryNew.InternSymbol("size")],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "highestPriority", method11);
        instanceMethodCount++;

        // yield
        // | semaphore |
        // semaphore := Semaphore new.
        // [semaphore signal] fork.
        // semaphore wait
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "yield",
            [0x40, 0xD1, 0x68,      // Push Semaphore, send new, store temp
             0x89, 0x11, 0xD2, 0x7D, // Push block [semaphore signal]
             0xC8, 0xD3,            // Send fork
             0x87,                  // Pop
             0x11, 0xD4, 0x7C],     // Push temp, send wait, return
            [ObjectMemoryNew.InternSymbol("Semaphore"),
             ObjectMemoryNew.InternSymbol("new"),
             ObjectMemoryNew.InternSymbol("signal"),
             ObjectMemoryNew.InternSymbol("fork"),
             ObjectMemoryNew.InternSymbol("wait")],
            0, 1);  // 0 args, 1 temp
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "yield", method12);
        instanceMethodCount++;

        // terminateActive
        // activeProcess terminate
        var method13 = ObjectMemoryNew.CreateCompiledMethod(
            "terminateActive",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send terminate, return
            [ObjectMemoryNew.InternSymbol("terminate")],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "terminateActive", method13);
        instanceMethodCount++;
        
        // Priority name methods - return constants
        // highIOPriority - returns 7
        var method14 = ObjectMemoryNew.CreateCompiledMethod(
            "highIOPriority",
            [0x77, 0x7C], // Push 7, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "highIOPriority", method14);
        instanceMethodCount++;

        // lowIOPriority - returns 6
        var method15 = ObjectMemoryNew.CreateCompiledMethod(
            "lowIOPriority",
            [0x76, 0x7C], // Push 6, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "lowIOPriority", method15);
        instanceMethodCount++;

        // systemBackgroundPriority - returns 2
        var method16 = ObjectMemoryNew.CreateCompiledMethod(
            "systemBackgroundPriority",
            [0x72, 0x7C], // Push 2, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "systemBackgroundPriority", method16);
        instanceMethodCount++;

        // timingPriority - returns 8
        var method17 = ObjectMemoryNew.CreateCompiledMethod(
            "timingPriority",
            [0x78, 0x7C], // Push 8, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "timingPriority", method17);
        instanceMethodCount++;

        // userBackgroundPriority - returns 3
        var method18 = ObjectMemoryNew.CreateCompiledMethod(
            "userBackgroundPriority",
            [0x73, 0x7C], // Push 3, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "userBackgroundPriority", method18);
        instanceMethodCount++;

        // userInterruptPriority - returns 5
        var method19 = ObjectMemoryNew.CreateCompiledMethod(
            "userInterruptPriority",
            [0x75, 0x7C], // Push 5, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "userInterruptPriority", method19);
        instanceMethodCount++;

        // userSchedulingPriority - returns 4
        var method20 = ObjectMemoryNew.CreateCompiledMethod(
            "userSchedulingPriority",
            [0x74, 0x7C], // Push 4, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "userSchedulingPriority", method20);
        instanceMethodCount++;

        // signal:atMilliseconds: - primitive 100
        var method21 = ObjectMemoryNew.CreateCompiledMethod(
            "signal:atMilliseconds:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 100)
            [ObjectMemoryNew.InternSymbol("primitiveFailed")],
            2, 0, 100);
        ObjectMemoryNew.AddMethod(processorSchedulerClassRef.AsObjectPointer(), "signal:atMilliseconds:", method21);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] ProcessorScheduler: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Semaphore methods.
    /// </summary>
    private static void InstallSemaphoreMethods()
    {
        var semaphoreClassRef = SystemDictionary.SemaphoreClassRef;
        if (semaphoreClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Semaphore instance methods: #signal, #wait");
        }
        
        // signal - primitive 85
        var method22 = ObjectMemoryNew.CreateCompiledMethod(
            "signal",
            [], // No fallback needed - primitive 85 cannot fail
            [],
            0, 0, 85);
        ObjectMemoryNew.AddMethod(semaphoreClassRef.AsObjectPointer(), "signal", method22);
        instanceMethodCount++;

        // wait - primitive 86
        var method23 = ObjectMemoryNew.CreateCompiledMethod(
            "wait",
            [], // No fallback needed - primitive 86 cannot fail
            [],
            0, 0, 86);
        ObjectMemoryNew.AddMethod(semaphoreClassRef.AsObjectPointer(), "wait", method23);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Semaphore: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs LinkedList methods.
    /// </summary>
    private static void InstallLinkedListMethods()
    {
        var linkedListClassRef = SystemDictionary.LinkedListClassRef;
        if (linkedListClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing LinkedList instance methods: #isEmpty, #addFirst:, #addLast:, #removeFirst, #first");
        }
        
        // isEmpty
        // ^firstLink == nil
        var method24 = ObjectMemoryNew.CreateCompiledMethod(
            "isEmpty",
            [0x00, 0x71, 0xC6, 0x7C], // Push firstLink, push nil, ==, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(linkedListClassRef.AsObjectPointer(), "isEmpty", method24);
        instanceMethodCount++;

        // addFirst: aLink
        // firstLink := aLink. lastLink ifNil: [lastLink := aLink]. ^aLink
        var method25 = ObjectMemoryNew.CreateCompiledMethod(
            "addFirst:",
            [0x10, 0x60, 0x01, 0xC7, // Push arg, store in firstLink
             0x01, 0x71, 0x99, 0x04, // Push lastLink, nil check, jump if not nil
             0x10, 0x61,             // Push arg, store in lastLink
             0x10, 0x7C],            // Push arg, return
            [],
            1, 0);
        ObjectMemoryNew.AddMethod(linkedListClassRef.AsObjectPointer(), "addFirst:", method25);
        instanceMethodCount++;

        // addLast: aLink
        // isEmpty ifTrue: [firstLink := aLink] ifFalse: [lastLink nextLink: aLink].
        // lastLink := aLink. ^aLink
        var method26 = ObjectMemoryNew.CreateCompiledMethod(
            "addLast:",
            [0x70, 0xCE,             // Push self, send isEmpty
             0x99, 0x06,             // Jump if false
             0x10, 0x60,             // Push arg, store in firstLink
             0xA4, 0x05,             // Jump to end
             0x01, 0x10, 0xE0,       // Push lastLink, push arg, send nextLink:
             0x87,                   // Pop
             0x10, 0x61,             // Push arg, store in lastLink
             0x10, 0x7C],            // Push arg, return
            [],
            1, 0);
        ObjectMemoryNew.AddMethod(linkedListClassRef.AsObjectPointer(), "addLast:", method26);
        instanceMethodCount++;

        // removeFirst
        // | oldLink |
        // self emptyCheck.
        // oldLink := firstLink.
        // firstLink == lastLink
        //     ifTrue: [firstLink := nil. lastLink := nil]
        //     ifFalse: [firstLink := oldLink nextLink].
        // oldLink nextLink: nil.
        // ^oldLink
        var method27 = ObjectMemoryNew.CreateCompiledMethod(
            "removeFirst",
            [0x70, 0xD0,             // Push self, send emptyCheck
             0x87,                   // Pop
             0x00, 0x68,             // Push firstLink, store temp 0 (oldLink)
             0x00, 0x01, 0xC6,       // Push firstLink, lastLink, ==
             0x99, 0x08,             // Jump if false
             0x71, 0x60,             // nil, store firstLink
             0x71, 0x61,             // nil, store lastLink
             0xA4, 0x06,             // Jump to end
             0x11, 0xCF, 0x60,       // Push oldLink, nextLink, store firstLink
             0x11, 0x71, 0xE0,       // Push oldLink, nil, send nextLink:
             0x87,                   // Pop
             0x11, 0x7C],            // Push oldLink, return
            [],
            0, 1);
        ObjectMemoryNew.AddMethod(linkedListClassRef.AsObjectPointer(), "removeFirst", method27);
        instanceMethodCount++;

        // first
        // self emptyCheck. ^firstLink
        var method28 = ObjectMemoryNew.CreateCompiledMethod(
            "first",
            [0x70, 0xD0,             // Push self, send emptyCheck
             0x87,                   // Pop
             0x00, 0x7C],            // Push firstLink, return
            [],
            0, 0);
        ObjectMemoryNew.AddMethod(linkedListClassRef.AsObjectPointer(), "first", method28);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] LinkedList: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Link methods.
    /// </summary>
    private static void InstallLinkMethods()
    {
        var linkClassRef = SystemDictionary.LinkClassRef;
        if (linkClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Link: 0 instance methods installed");
        }
    }
}
