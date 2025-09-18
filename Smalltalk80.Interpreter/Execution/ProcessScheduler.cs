using System;
using System.Collections.Generic;
using System.Linq;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Execution;

/// <summary>
/// Implements the Smalltalk-80 ProcessScheduler for managing process execution and context switching.
/// Provides the core functionality for process scheduling, suspension, resumption, and context management.
/// </summary>
/// <remarks>
/// The ProcessScheduler maintains priority-based process queues and coordinates with the VM
/// to save and restore execution contexts. It follows the Blue Book specification where:
/// - Processes are scheduled based on priority (higher priority preempts lower)
/// - Context switching preserves the complete execution state
/// - Both MethodContext and BlockContext are properly saved/restored
/// </remarks>
public sealed class ProcessScheduler
{
    /// <summary>
    /// The virtual machine instance this scheduler operates with.
    /// </summary>
    private readonly VirtualMachine vm;
    
    /// <summary>
    /// The object memory for accessing Smalltalk objects.
    /// </summary>
    private readonly ObjectMemory memory;
    
    /// <summary>
    /// Priority-based queues of ready processes.
    /// Index 0 is priority 1, index 99 is priority 100.
    /// </summary>
    private readonly List<OOP>[] priorityQueues;
    
    /// <summary>
    /// The currently executing process.
    /// </summary>
    private OOP activeProcess = OOP.Nil;
    
    /// <summary>
    /// The OOP of the ProcessorScheduler singleton object.
    /// </summary>
    private OOP processorSchedulerOop = OOP.Nil;
    
    /// <summary>
    /// Maximum priority level supported.
    /// </summary>
    private const int MaxPriority = 100;
    
    /// <summary>
    /// The priority level of system processes (like finalization).
    /// </summary>
    private const int SystemPriority = 90;
    
    /// <summary>
    /// The priority level of user interface processes.
    /// </summary>
    private const int UserInterfacePriority = 70;
    
    /// <summary>
    /// The priority level of normal user processes.
    /// </summary>
    private const int UserPriority = 50;
    
    /// <summary>
    /// The priority level of background processes.
    /// </summary>
    private const int BackgroundPriority = 30;
    
    /// <summary>
    /// Creates a new ProcessScheduler instance.
    /// </summary>
    /// <param name="vm">The virtual machine instance.</param>
    /// <param name="memory">The object memory.</param>
    public ProcessScheduler(VirtualMachine vm, ObjectMemory memory)
    {
        this.vm = vm ?? throw new ArgumentNullException(nameof(vm));
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        
        // Initialize priority queues
        priorityQueues = new List<OOP>[MaxPriority];
        for (int i = 0; i < MaxPriority; i++)
        {
            priorityQueues[i] = new List<OOP>();
        }
    }
    
    /// <summary>
    /// Gets the currently active process.
    /// </summary>
    public OOP ActiveProcess => activeProcess;
    
    /// <summary>
    /// Gets or sets the ProcessorScheduler singleton OOP.
    /// </summary>
    public OOP ProcessorSchedulerOop
    {
        get => processorSchedulerOop;
        set => processorSchedulerOop = value;
    }
    
    /// <summary>
    /// Initializes the scheduler with a process.
    /// </summary>
    /// <param name="initialProcess">The initial process to run.</param>
    public void Initialize(OOP initialProcess)
    {
        if (initialProcess.IsNil)
            throw new ArgumentException("Initial process cannot be nil", nameof(initialProcess));
        
        activeProcess = initialProcess;
        UpdateProcessorSchedulerActiveProcess();
    }
    
    /// <summary>
    /// Sets the active process without performing context switching.
    /// Used during system initialization.
    /// </summary>
    /// <param name="process">The process to set as active.</param>
    public void SetActiveProcess(OOP process)
    {
        activeProcess = process;
        UpdateProcessorSchedulerActiveProcess();
    }
    
    /// <summary>
    /// Suspends the specified process and removes it from scheduling.
    /// </summary>
    /// <param name="process">The process to suspend.</param>
    /// <returns>The suspended context of the process.</returns>
    public OOP SuspendProcess(OOP process)
    {
        if (process.IsNil)
            throw new ArgumentException("Cannot suspend nil process", nameof(process));
        
        var processObj = memory.GetObject(process);
        if (processObj == null || processObj.Fields.Length < 4)
            throw new InvalidOperationException("Invalid process object structure");
        
        // If suspending the active process, save its context first
        if (process.Equals(activeProcess))
        {
            // Save the current execution context
            var contextOop = SaveActiveContext();
            processObj.Fields[1] = contextOop; // suspendedContext
            
            // Switch to another process
            SwitchToNextProcess();
            
            return contextOop;
        }
        else
        {
            // Remove from priority queue if present
            var priority = processObj.Fields[2];
            if (priority.IsSmallInteger)
            {
                var priorityLevel = priority.SmallIntegerValue;
                if (priorityLevel > 0 && priorityLevel <= MaxPriority)
                {
                    priorityQueues[priorityLevel - 1].Remove(process);
                }
            }
            
            // Clear myList field
            processObj.Fields[3] = OOP.Nil;
            
            return processObj.Fields[1]; // Return existing suspendedContext
        }
    }
    
    /// <summary>
    /// Resumes the specified process by adding it to the ready queue.
    /// </summary>
    /// <param name="process">The process to resume.</param>
    public void ResumeProcess(OOP process)
    {
        if (process.IsNil)
            throw new ArgumentException("Cannot resume nil process", nameof(process));
        
        var processObj = memory.GetObject(process);
        if (processObj == null || processObj.Fields.Length < 4)
            throw new InvalidOperationException("Invalid process object structure");
        
        // Check if already on a list
        if (!processObj.Fields[3].IsNil)
            return; // Already scheduled
        
        var priority = processObj.Fields[2];
        if (!priority.IsSmallInteger)
            throw new InvalidOperationException("Invalid process priority");
        
        var priorityLevel = priority.SmallIntegerValue;
        if (priorityLevel < 1 || priorityLevel > MaxPriority)
            throw new ArgumentOutOfRangeException($"Priority {priorityLevel} out of range 1-{MaxPriority}");
        
        // Add to priority queue
        priorityQueues[priorityLevel - 1].Add(process);
        
        // Update myList to indicate it's scheduled
        processObj.Fields[3] = OOP.FromSmallInteger(priorityLevel);
        
        // Check for preemption
        CheckForPreemption(priorityLevel);
    }
    
    /// <summary>
    /// Switches execution to the specified process.
    /// </summary>
    /// <param name="newProcess">The process to switch to.</param>
    public void SwitchToProcess(OOP newProcess)
    {
        if (newProcess.IsNil)
            throw new ArgumentException("Cannot switch to nil process", nameof(newProcess));
        
        if (newProcess.Equals(activeProcess))
            return; // Already active
        
        // Save current process context if there is one
        if (!activeProcess.IsNil)
        {
            var oldProcessObj = memory.GetObject(activeProcess);
            if (oldProcessObj != null && oldProcessObj.Fields.Length > 1)
            {
                oldProcessObj.Fields[1] = SaveActiveContext();
            }
        }
        
        // Load new process context
        var newProcessObj = memory.GetObject(newProcess);
        if (newProcessObj == null || newProcessObj.Fields.Length < 4)
            throw new InvalidOperationException("Invalid process object structure");
        
        var suspendedContext = newProcessObj.Fields[1];
        if (suspendedContext.IsNil)
            throw new InvalidOperationException("Process has no suspended context");
        
        // Restore the context
        RestoreContext(suspendedContext);
        
        // Update active process
        activeProcess = newProcess;
        UpdateProcessorSchedulerActiveProcess();
    }
    
    /// <summary>
    /// Yields execution to allow other processes at the same priority to run.
    /// </summary>
    public void Yield()
    {
        if (activeProcess.IsNil)
            return;
        
        var processObj = memory.GetObject(activeProcess);
        if (processObj == null || processObj.Fields.Length < 3)
            return;
        
        var priority = processObj.Fields[2];
        if (!priority.IsSmallInteger)
            return;
        
        var priorityLevel = priority.SmallIntegerValue;
        if (priorityLevel < 1 || priorityLevel > MaxPriority)
            return;
        
        var queue = priorityQueues[priorityLevel - 1];
        if (queue.Count > 0)
        {
            // Save current context
            processObj.Fields[1] = SaveActiveContext();
            
            // Move current process to end of queue
            queue.Add(activeProcess);
            processObj.Fields[3] = OOP.FromSmallInteger(priorityLevel);
            
            // Switch to next process in queue
            SwitchToNextProcess();
        }
    }
    
    /// <summary>
    /// Creates a context object from the current VM execution state.
    /// </summary>
    /// <returns>An OOP representing the saved context object.</returns>
    public OOP SaveActiveContext()
    {
        var context = vm.GetActiveContext();
        if (context == null)
            return OOP.Nil;
        
        return SaveContextChain(context);
    }
    
    /// <summary>
    /// Restores VM execution state from a context object.
    /// </summary>
    /// <param name="contextOop">The context object to restore from.</param>
    public void RestoreContext(OOP contextOop)
    {
        if (contextOop.IsNil)
            throw new ArgumentException("Cannot restore nil context", nameof(contextOop));
        
        var restoredContext = RestoreContextChain(contextOop);
        if (restoredContext != null)
            vm.SetActiveContext(restoredContext);
    }
    
    /// <summary>
    /// Checks if a higher priority process should preempt the current one.
    /// </summary>
    /// <param name="newPriority">The priority level to check.</param>
    private void CheckForPreemption(int newPriority)
    {
        if (activeProcess.IsNil)
        {
            // No active process, switch immediately
            SwitchToNextProcess();
            return;
        }
        
        var activeProcessObj = memory.GetObject(activeProcess);
        if (activeProcessObj == null || activeProcessObj.Fields.Length < 3)
            return;
        
        var activePriority = activeProcessObj.Fields[2];
        if (!activePriority.IsSmallInteger)
            return;
        
        if (newPriority > activePriority.SmallIntegerValue)
        {
            // Higher priority process should preempt
            // Save current process
            activeProcessObj.Fields[1] = SaveActiveContext();
            
            // Add current process back to its queue
            var currentPriority = activePriority.SmallIntegerValue;
            if (currentPriority > 0 && currentPriority <= MaxPriority)
            {
                priorityQueues[currentPriority - 1].Insert(0, activeProcess);
                activeProcessObj.Fields[3] = OOP.FromSmallInteger(currentPriority);
            }
            
            // Switch to higher priority process
            SwitchToNextProcess();
        }
    }
    
    /// <summary>
    /// Switches to the next available process based on priority.
    /// </summary>
    private void SwitchToNextProcess()
    {
        // Find highest priority non-empty queue
        for (int priority = MaxPriority; priority >= 1; priority--)
        {
            var queue = priorityQueues[priority - 1];
            if (queue.Count > 0)
            {
                var nextProcess = queue[0];
                queue.RemoveAt(0);
                
                // Clear myList since it's no longer on a queue
                var processObj = memory.GetObject(nextProcess);
                if (processObj != null && processObj.Fields.Length > 3)
                {
                    processObj.Fields[3] = OOP.Nil;
                }
                
                SwitchToProcess(nextProcess);
                return;
            }
        }
        
        // No runnable processes - this would normally trigger the idle process
        activeProcess = OOP.Nil;
        UpdateProcessorSchedulerActiveProcess();
    }
    
    /// <summary>
    /// Saves a context chain to object memory.
    /// </summary>
    /// <param name="context">The context to save.</param>
    /// <returns>The OOP of the saved context object.</returns>
    public OOP SaveContextChain(MethodContext context)
    {
        // Determine context type
        var isBlockContext = context is BlockContext;
        var contextClass = vm.GetClass(isBlockContext ? "BlockContext" : "MethodContext");
        
        if (contextClass.IsNil)
            return OOP.Nil;
        
        // Create context object with indexed fields for temps and stack
        var indexedSize = context.Arguments.Length + context.Temporaries.Length + context.Stack.Count;
        var contextOop = memory.AllocateObject(contextClass, 6 + indexedSize);
        var contextObj = memory.GetObject(contextOop);
        
        if (contextObj == null)
            return OOP.Nil;
        
        // Save fixed fields
        if (isBlockContext)
        {
            var blockContext = (BlockContext)context;
            // BlockContext fields: sender, pc, stackp, nargs, startpc, home
            contextObj.Fields[0] = context.Sender != null ? SaveContextChain(context.Sender) : OOP.Nil;
            contextObj.Fields[1] = OOP.FromSmallInteger(context.PC);
            contextObj.Fields[2] = OOP.FromSmallInteger(context.Stack.Count);
            contextObj.Fields[3] = OOP.FromSmallInteger(blockContext.ArgumentCount);
            contextObj.Fields[4] = OOP.FromSmallInteger(blockContext.InitialPC);
            contextObj.Fields[5] = SaveContextChain(blockContext.Home);
        }
        else
        {
            // MethodContext fields: sender, pc, stackp, method, receiver, (unused)
            contextObj.Fields[0] = context.Sender != null ? SaveContextChain(context.Sender) : OOP.Nil;
            contextObj.Fields[1] = OOP.FromSmallInteger(context.PC);
            contextObj.Fields[2] = OOP.FromSmallInteger(context.Stack.Count);
            contextObj.Fields[3] = context.Method;
            contextObj.Fields[4] = context.Receiver;
            contextObj.Fields[5] = OOP.Nil; // Unused field in standard MethodContext
        }
        
        // Save indexed fields (arguments, temporaries, stack)
        int index = 6;
        
        // Save arguments
        foreach (var arg in context.Arguments)
        {
            contextObj.Fields[index++] = arg;
        }
        
        // Save temporaries
        foreach (var temp in context.Temporaries)
        {
            contextObj.Fields[index++] = temp;
        }
        
        // Save stack contents
        var stackItems = new OOP[context.Stack.Count];
        for (int i = context.Stack.Count - 1; i >= 0; i--)
        {
            stackItems[i] = context.Stack.Pop();
        }
        
        // Push them back to restore stack state
        foreach (var item in stackItems)
        {
            context.Stack.Push(item);
            contextObj.Fields[index++] = item;
        }
        
        return contextOop;
    }
    
    /// <summary>
    /// Restores a context chain from object memory.
    /// </summary>
    /// <param name="contextOop">The OOP of the context object.</param>
    /// <returns>The restored MethodContext.</returns>
    public MethodContext? RestoreContextChain(OOP contextOop)
    {
        if (contextOop.IsNil)
            return null;
        
        var contextObj = memory.GetObject(contextOop);
        if (contextObj == null || contextObj.Fields.Length < 6)
            return null;
        
        // Get class to determine context type
        // For now, we'll check field patterns to determine type
        // In a full implementation, we'd track the class through the OOP header
        
        // Check if this is a BlockContext (would need to check class name)
        // For simplicity, we'll check field patterns
        var isBlockContext = !contextObj.Fields[5].IsNil && contextObj.Fields[3].IsSmallInteger;
        
        if (isBlockContext)
        {
            // Restore BlockContext
            var senderOop = contextObj.Fields[0];
            var pc = contextObj.Fields[1].IsSmallInteger ? contextObj.Fields[1].SmallIntegerValue : 0;
            var stackCount = contextObj.Fields[2].IsSmallInteger ? contextObj.Fields[2].SmallIntegerValue : 0;
            var nargs = contextObj.Fields[3].IsSmallInteger ? contextObj.Fields[3].SmallIntegerValue : 0;
            var startpc = contextObj.Fields[4].IsSmallInteger ? contextObj.Fields[4].SmallIntegerValue : 0;
            var homeOop = contextObj.Fields[5];
            
            // Restore home context first
            var home = RestoreContextChain(homeOop);
            if (home == null)
                throw new InvalidOperationException("Invalid home context");
            
            // Extract arguments from indexed fields
            var arguments = new OOP[nargs];
            for (int i = 0; i < nargs && 6 + i < contextObj.Fields.Length; i++)
            {
                arguments[i] = contextObj.Fields[6 + i];
            }
            
            // Calculate temp count and stack size
            var tempCount = Math.Max(0, contextObj.Fields.Length - 6 - nargs - stackCount);
            var stackSize = Math.Max(16, stackCount + 8);
            
            var blockContext = new BlockContext(
                home: home,
                receiver: home.Receiver,
                arguments: arguments,
                initialPC: startpc,
                tempCount: tempCount,
                stackSize: stackSize
            );
            
            // Restore sender
            blockContext.Sender = RestoreContextChain(senderOop);
            blockContext.PC = pc;
            
            // Restore temporaries
            int index = 6 + nargs;
            for (int i = 0; i < tempCount && index < contextObj.Fields.Length - stackCount; i++)
            {
                blockContext.Temporaries[i] = contextObj.Fields[index++];
            }
            
            // Restore stack
            for (int i = 0; i < stackCount && index < contextObj.Fields.Length; i++)
            {
                blockContext.Stack.Push(contextObj.Fields[index++]);
            }
            
            return blockContext;
        }
        else
        {
            // Restore MethodContext
            var senderOop = contextObj.Fields[0];
            var pc = contextObj.Fields[1].IsSmallInteger ? contextObj.Fields[1].SmallIntegerValue : 0;
            var stackCount = contextObj.Fields[2].IsSmallInteger ? contextObj.Fields[2].SmallIntegerValue : 0;
            var method = contextObj.Fields[3];
            var receiver = contextObj.Fields[4];
            
            // Get method header to determine counts
            var methodObj = memory.GetObject(method);
            if (methodObj == null || methodObj.Fields.Length < 1)
                return null;
            
            var header = methodObj.Fields[0];
            if (!header.IsSmallInteger)
                return null;
            
            int headerValue = header.SmallIntegerValue;
            int argCount = (headerValue >> 9) & 0x1F;
            int tempCount = (headerValue >> 14) & 0x1F;
            int stackSize = Math.Max(16, (headerValue >> 19) & 0x3F);
            
            // Extract arguments from indexed fields
            var arguments = new OOP[argCount];
            for (int i = 0; i < argCount && 6 + i < contextObj.Fields.Length; i++)
            {
                arguments[i] = contextObj.Fields[6 + i];
            }
            
            var methodContext = new MethodContext(
                sender: null,
                receiver: receiver,
                method: method,
                arguments: arguments,
                tempCount: tempCount,
                stackSize: stackSize
            );
            
            // Restore sender
            methodContext.Sender = RestoreContextChain(senderOop);
            methodContext.PC = pc;
            
            // Restore temporaries
            int index = 6 + argCount;
            for (int i = 0; i < tempCount && index < contextObj.Fields.Length - stackCount; i++)
            {
                methodContext.Temporaries[i] = contextObj.Fields[index++];
            }
            
            // Restore stack
            for (int i = 0; i < stackCount && index < contextObj.Fields.Length; i++)
            {
                methodContext.Stack.Push(contextObj.Fields[index++]);
            }
            
            return methodContext;
        }
    }
    
    /// <summary>
    /// Updates the ProcessorScheduler object's activeProcess field.
    /// </summary>
    private void UpdateProcessorSchedulerActiveProcess()
    {
        if (processorSchedulerOop.IsNil)
            return;
        
        var schedulerObj = memory.GetObject(processorSchedulerOop);
        if (schedulerObj != null && schedulerObj.Fields.Length > 1)
        {
            schedulerObj.Fields[1] = activeProcess;
        }
    }
    
    /// <summary>
    /// Gets the priority of a process.
    /// </summary>
    /// <param name="process">The process to get priority for.</param>
    /// <returns>The priority level or 0 if invalid.</returns>
    public int GetProcessPriority(OOP process)
    {
        if (process.IsNil)
            return 0;
        
        var processObj = memory.GetObject(process);
        if (processObj == null || processObj.Fields.Length < 3)
            return 0;
        
        var priority = processObj.Fields[2];
        return priority.IsSmallInteger ? priority.SmallIntegerValue : 0;
    }
    
    /// <summary>
    /// Checks if there are any runnable processes.
    /// </summary>
    /// <returns>True if there are runnable processes; otherwise, false.</returns>
    public bool HasRunnableProcesses()
    {
        return priorityQueues.Any(queue => queue.Count > 0) || !activeProcess.IsNil;
    }
    
    /// <summary>
    /// Gets statistics about the scheduler state.
    /// </summary>
    /// <returns>A string describing the scheduler state.</returns>
    public string GetSchedulerStatistics()
    {
        var stats = new System.Text.StringBuilder();
        stats.AppendLine("ProcessScheduler Statistics:");
        stats.AppendLine($"  Active Process: {(activeProcess.IsNil ? "none" : activeProcess.ToString())}");
        
        if (!activeProcess.IsNil)
        {
            var priority = GetProcessPriority(activeProcess);
            stats.AppendLine($"  Active Priority: {priority}");
        }
        
        stats.AppendLine("  Ready Queues:");
        for (int i = MaxPriority - 1; i >= 0; i--)
        {
            if (priorityQueues[i].Count > 0)
            {
                stats.AppendLine($"    Priority {i + 1}: {priorityQueues[i].Count} processes");
            }
        }
        
        return stats.ToString();
    }
}