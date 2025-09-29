namespace Smalltalk80.Interpreter.Core.Objects;

using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Represents a Smalltalk Process - a sequence of contexts that can be scheduled.
/// </summary>
/// <remarks>
/// From the Blue Book page 252:
/// A Process represents a sequence of actions described by context objects.
/// Each process has a priority and can be suspended, resumed, or terminated.
/// 
/// Instance variables:
/// - suspendedContext: The context at which the process was suspended
/// - priority: The process priority (1-8 in standard Smalltalk)
/// - myList: The ProcessorScheduler list this process is in (or nil)
/// </remarks>
public sealed class SProcess : SInstance
{
    // Instance variable indices
    private const int SuspendedContextIndex = 0;
    private const int PriorityIndex = 1;
    private const int MyListIndex = 2;
    
    /// <summary>
    /// Initializes a new instance of the SProcess class.
    /// </summary>
    /// <param name="processClass">The Process class.</param>
    /// <param name="initialContext">The initial context for the process.</param>
    /// <param name="priority">The process priority.</param>
    public SProcess(SClass processClass, SObject initialContext, int priority = 4)
        : base(processClass, 3) // 3 instance variables
    {
        SetSuspendedContext(initialContext);
        SetPriority(SmallInteger.From(priority));
        SetMyList(SNil.Instance);
    }
    
    /// <summary>
    /// Gets or sets the suspended context.
    /// </summary>
    public SObject SuspendedContext
    {
        get => GetInstanceVariable(SuspendedContextIndex) ?? SNil.Instance;
        set => SetInstanceVariable(SuspendedContextIndex, value);
    }
    
    /// <summary>
    /// Gets or sets the process priority.
    /// </summary>
    public SmallInteger Priority
    {
        get => (SmallInteger)(GetInstanceVariable(PriorityIndex) ?? SmallInteger.From(4));
        set => SetInstanceVariable(PriorityIndex, value);
    }
    
    /// <summary>
    /// Gets or sets the process list this process belongs to.
    /// </summary>
    public SObject MyList
    {
        get => GetInstanceVariable(MyListIndex) ?? SNil.Instance;
        set => SetInstanceVariable(MyListIndex, value);
    }
    
    /// <summary>
    /// Sets the suspended context.
    /// </summary>
    private void SetSuspendedContext(SObject context)
    {
        SetInstanceVariable(SuspendedContextIndex, context);
    }
    
    /// <summary>
    /// Sets the priority.
    /// </summary>
    private void SetPriority(SmallInteger priority)
    {
        SetInstanceVariable(PriorityIndex, priority);
    }
    
    /// <summary>
    /// Sets the process list.
    /// </summary>
    private void SetMyList(SObject list)
    {
        SetInstanceVariable(MyListIndex, list);
    }
    
    /// <summary>
    /// Suspends this process at the given context.
    /// </summary>
    public void Suspend(SObject context)
    {
        SetSuspendedContext(context);
    }
    
    /// <summary>
    /// Resumes this process from its suspended context.
    /// </summary>
    public SObject Resume()
    {
        return SuspendedContext;
    }
    
    /// <summary>
    /// Checks if this process is terminated.
    /// </summary>
    public bool IsTerminated()
    {
        return SuspendedContext == SNil.Instance;
    }
    
    /// <summary>
    /// Terminates this process.
    /// </summary>
    public void Terminate()
    {
        SetSuspendedContext(SNil.Instance);
        SetMyList(SNil.Instance);
    }
    
    /// <summary>
    /// Returns a string representation of this process.
    /// </summary>
    public override string ToDisplayString()
    {
        var priorityValue = Priority.Value;
        var status = IsTerminated() ? "terminated" : "suspended";
        return $"Process(priority={priorityValue}, {status})";
    }
}