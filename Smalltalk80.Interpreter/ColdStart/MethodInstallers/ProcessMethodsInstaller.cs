namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

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
        processClassRef.AddMethod("resume", SCompiledMethod.CreateCompiledMethodRef(
            "resume",
            [], // No fallback needed - primitive 87 cannot fail
            [],
            0, 0, 87));
        instanceMethodCount++;
        
        // suspend - primitive 88
        processClassRef.AddMethod("suspend", SCompiledMethod.CreateCompiledMethodRef(
            "suspend",
            [], // No fallback needed - primitive 88 cannot fail
            [],
            0, 0, 88));
        instanceMethodCount++;
        
        // terminate - primitive 91
        processClassRef.AddMethod("terminate", SCompiledMethod.CreateCompiledMethodRef(
            "terminate",
            [], // No fallback needed - primitive 91 cannot fail
            [],
            0, 0, 91));
        instanceMethodCount++;
        
        // priority
        // ^priority
        processClassRef.AddMethod("priority", SCompiledMethod.CreateCompiledMethodRef(
            "priority",
            [0x01, 0x7C], // Push priority instance var (index 1), return
            [],
            0, 0));
        instanceMethodCount++;
        
        // priority: anInteger
        // priority := anInteger
        processClassRef.AddMethod("priority:", SCompiledMethod.CreateCompiledMethodRef(
            "priority:",
            [0x10, 0x61, 0x70, 0x7C], // Push arg, store in priority (index 1), push self, return
            [],
            1, 0));
        instanceMethodCount++;
        
        // suspendedContext
        // ^suspendedContext
        processClassRef.AddMethod("suspendedContext", SCompiledMethod.CreateCompiledMethodRef(
            "suspendedContext",
            [0x00, 0x7C], // Push suspendedContext instance var (index 0), return
            [],
            0, 0));
        instanceMethodCount++;
        
        // suspendedContext: aContext (private)
        // suspendedContext := aContext
        processClassRef.AddMethod("suspendedContext:", SCompiledMethod.CreateCompiledMethodRef(
            "suspendedContext:",
            [0x10, 0x60, 0x70, 0x7C], // Push arg, store in suspendedContext (index 0), push self, return
            [],
            1, 0));
        instanceMethodCount++;
        
        // offList
        // myList := nil
        processClassRef.AddMethod("offList", SCompiledMethod.CreateCompiledMethodRef(
            "offList",
            [0x71, 0x62, 0x70, 0x7C], // Push nil, store in myList (index 2), push self, return
            [],
            0, 0));
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
        processorSchedulerClassRef.AddMethod("activeProcess", SCompiledMethod.CreateCompiledMethodRef(
            "activeProcess",
            [0x01, 0x7C], // Push activeProcess instance var, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // activePriority  
        // ^activeProcess priority
        var activePriorityLiterals = SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("priority"));
        processorSchedulerClassRef.AddMethod("activePriority", SCompiledMethod.CreateCompiledMethodRef(
            "activePriority",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send priority, return
            activePriorityLiterals,
            0, 0));
        instanceMethodCount++;
        
        // highestPriority
        // ^quiescentProcessLists size
        var highestPriorityLiterals = SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("size"));
        processorSchedulerClassRef.AddMethod("highestPriority", SCompiledMethod.CreateCompiledMethodRef(
            "highestPriority",
            [0x00, 0xD0, 0x7C], // Push quiescentProcessLists, send size, return
            highestPriorityLiterals,
            0, 0));
        instanceMethodCount++;
        
        // yield
        // | semaphore |
        // semaphore := Semaphore new.
        // [semaphore signal] fork.
        // semaphore wait
        var yieldLiterals = SCompiledMethod.ConvertLiteralsToRefs(
            SSymbol.Intern("Semaphore"),
            SSymbol.Intern("new"),
            SSymbol.Intern("signal"),
            SSymbol.Intern("fork"),
            SSymbol.Intern("wait"));
        processorSchedulerClassRef.AddMethod("yield", SCompiledMethod.CreateCompiledMethodRef(
            "yield",
            [0x40, 0xD1, 0x68,      // Push Semaphore, send new, store temp
             0x89, 0x11, 0xD2, 0x7D, // Push block [semaphore signal]
             0xC8, 0xD3,            // Send fork
             0x87,                  // Pop
             0x11, 0xD4, 0x7C],     // Push temp, send wait, return
            yieldLiterals,
            0, 1));  // 0 args, 1 temp
        instanceMethodCount++;
        
        // terminateActive
        // activeProcess terminate
        var terminateActiveLiterals = SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("terminate"));
        processorSchedulerClassRef.AddMethod("terminateActive", SCompiledMethod.CreateCompiledMethodRef(
            "terminateActive",
            [0x01, 0xD0, 0x7C], // Push activeProcess, send terminate, return
            terminateActiveLiterals,
            0, 0));
        instanceMethodCount++;
        
        // Priority name methods - return constants
        // highIOPriority - returns 7
        processorSchedulerClassRef.AddMethod("highIOPriority", SCompiledMethod.CreateCompiledMethodRef(
            "highIOPriority",
            [0x77, 0x7C], // Push 7, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // lowIOPriority - returns 6  
        processorSchedulerClassRef.AddMethod("lowIOPriority", SCompiledMethod.CreateCompiledMethodRef(
            "lowIOPriority",
            [0x76, 0x7C], // Push 6, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // systemBackgroundPriority - returns 2
        processorSchedulerClassRef.AddMethod("systemBackgroundPriority", SCompiledMethod.CreateCompiledMethodRef(
            "systemBackgroundPriority",
            [0x72, 0x7C], // Push 2, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // timingPriority - returns 8
        processorSchedulerClassRef.AddMethod("timingPriority", SCompiledMethod.CreateCompiledMethodRef(
            "timingPriority",
            [0x78, 0x7C], // Push 8, return  
            [],
            0, 0));
        instanceMethodCount++;
        
        // userBackgroundPriority - returns 3
        processorSchedulerClassRef.AddMethod("userBackgroundPriority", SCompiledMethod.CreateCompiledMethodRef(
            "userBackgroundPriority",
            [0x73, 0x7C], // Push 3, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // userInterruptPriority - returns 5
        processorSchedulerClassRef.AddMethod("userInterruptPriority", SCompiledMethod.CreateCompiledMethodRef(
            "userInterruptPriority",
            [0x75, 0x7C], // Push 5, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // userSchedulingPriority - returns 4
        processorSchedulerClassRef.AddMethod("userSchedulingPriority", SCompiledMethod.CreateCompiledMethodRef(
            "userSchedulingPriority",
            [0x74, 0x7C], // Push 4, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // signal:atMilliseconds: - primitive 100
        processorSchedulerClassRef.AddMethod("signal:atMilliseconds:", SCompiledMethod.CreateCompiledMethodRef(
            "signal:atMilliseconds:",
            [0x70, 0xD0, 0x7C], // self primitiveFailed (fallback for primitive 100)
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("primitiveFailed")),
            2, 0, 100));
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
        semaphoreClassRef.AddMethod("signal", SCompiledMethod.CreateCompiledMethodRef(
            "signal",
            [], // No fallback needed - primitive 85 cannot fail
            [],
            0, 0, 85));
        instanceMethodCount++;
        
        // wait - primitive 86
        semaphoreClassRef.AddMethod("wait", SCompiledMethod.CreateCompiledMethodRef(
            "wait",
            [], // No fallback needed - primitive 86 cannot fail
            [],
            0, 0, 86));
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
        linkedListClassRef.AddMethod("isEmpty", SCompiledMethod.CreateCompiledMethodRef(
            "isEmpty",
            [0x00, 0x71, 0xC6, 0x7C], // Push firstLink, push nil, ==, return
            [],
            0, 0));
        instanceMethodCount++;
        
        // addFirst: aLink
        // firstLink := aLink. lastLink ifNil: [lastLink := aLink]. ^aLink
        linkedListClassRef.AddMethod("addFirst:", SCompiledMethod.CreateCompiledMethodRef(
            "addFirst:",
            [0x10, 0x60, 0x01, 0xC7, // Push arg, store in firstLink
             0x01, 0x71, 0x99, 0x04, // Push lastLink, nil check, jump if not nil
             0x10, 0x61,             // Push arg, store in lastLink
             0x10, 0x7C],            // Push arg, return
            [],
            1, 0));
        instanceMethodCount++;
        
        // addLast: aLink
        // isEmpty ifTrue: [firstLink := aLink] ifFalse: [lastLink nextLink: aLink].
        // lastLink := aLink. ^aLink
        linkedListClassRef.AddMethod("addLast:", SCompiledMethod.CreateCompiledMethodRef(
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
            1, 0));
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
        linkedListClassRef.AddMethod("removeFirst", SCompiledMethod.CreateCompiledMethodRef(
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
            0, 1));
        instanceMethodCount++;
        
        // first
        // self emptyCheck. ^firstLink
        linkedListClassRef.AddMethod("first", SCompiledMethod.CreateCompiledMethodRef(
            "first",
            [0x70, 0xD0,             // Push self, send emptyCheck
             0x87,                   // Pop
             0x00, 0x7C],            // Push firstLink, return
            [],
            0, 0));
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
