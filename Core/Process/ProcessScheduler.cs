using System.Collections.Concurrent;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Core.Process;

/// <summary>
/// Implements Smalltalk process scheduling with priority-based queues.
/// Based on the Blue Book specification chapter on process management.
/// </summary>
public sealed class ProcessScheduler : IProcessScheduler
{
    private readonly IObjectMemory _objectMemory;
    private readonly ILogger<ProcessScheduler> _logger;
    private readonly ProcessQueue[] _priorityQueues;
    private OOP _activeProcess = OOP.Nil;
    private bool _isEnabled = false;
    private OOP _processClass = OOP.Nil;
    private OOP _processListClass = OOP.Nil;

    public ProcessScheduler(IObjectMemory objectMemory, ILogger<ProcessScheduler> logger)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        
        // Initialize priority queues (1-8)
        _priorityQueues = new ProcessQueue[ProcessPriority.Max + 1];
        for (int i = 0; i <= ProcessPriority.Max; i++)
        {
            _priorityQueues[i] = new ProcessQueue(i);
        }
        
        _logger.LogDebug("Process scheduler initialized with {Count} priority levels", ProcessPriority.Max);
    }

    /// <inheritdoc/>
    public OOP ActiveProcess => _activeProcess;

    /// <inheritdoc/>
    public bool IsEnabled => _isEnabled;

    /// <inheritdoc/>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _logger.LogInformation("Process scheduler {State}", enabled ? "enabled" : "disabled");
        
        if (enabled && _activeProcess.IsNil)
        {
            // Create initial process if none exists
            CreateInitialProcess();
        }
    }

    /// <inheritdoc/>
    public OOP CreateProcess(OOP block, int priority)
    {
        if (priority < ProcessPriority.Min || priority > ProcessPriority.Max)
            throw new ArgumentOutOfRangeException(nameof(priority), 
                $"Priority must be between {ProcessPriority.Min} and {ProcessPriority.Max}");

        // In full implementation, allocate Process object
        // For now, use the block itself as process representation
        var process = block; // Simplified
        
        _logger.LogDebug("Created process {Process} at priority {Priority}", process, priority);
        return process;
    }

    /// <inheritdoc/>
    public void Schedule(OOP process)
    {
        if (process.IsNil)
            throw new ArgumentException("Cannot schedule nil process");

        // Get process priority (default to user scheduling)
        var priority = GetProcessPriority(process);
        
        _priorityQueues[priority].Enqueue(process);
        _logger.LogDebug("Scheduled process {Process} at priority {Priority}", process, priority);
        
        // If higher priority than active, consider switching
        if (_isEnabled && priority > GetProcessPriority(_activeProcess))
        {
            ScheduleNext();
        }
    }

    /// <inheritdoc/>
    public void SuspendActiveProcess()
    {
        if (_activeProcess.IsNil)
            return;

        _logger.LogDebug("Suspending active process {Process}", _activeProcess);
        
        // Move to suspended state (not in any queue)
        // In full implementation, set process state field
        
        // Schedule next process
        ScheduleNext();
    }

    /// <inheritdoc/>
    public void Resume(OOP process)
    {
        if (process.IsNil)
            return;

        _logger.LogDebug("Resuming process {Process}", process);
        
        // Add back to appropriate priority queue
        Schedule(process);
    }

    /// <inheritdoc/>
    public void Terminate(OOP process)
    {
        if (process.IsNil)
            return;

        _logger.LogDebug("Terminating process {Process}", process);
        
        // Remove from all queues
        for (int i = ProcessPriority.Min; i <= ProcessPriority.Max; i++)
        {
            _priorityQueues[i].Remove(process);
        }
        
        // If terminating active process, schedule next
        if (process.Equals(_activeProcess))
        {
            _activeProcess = OOP.Nil;
            ScheduleNext();
        }
    }

    /// <inheritdoc/>
    public void Yield()
    {
        if (!_isEnabled || _activeProcess.IsNil)
            return;

        _logger.LogDebug("Process {Process} yielding", _activeProcess);
        
        // Put current process at end of its priority queue
        var priority = GetProcessPriority(_activeProcess);
        _priorityQueues[priority].Enqueue(_activeProcess);
        
        // Schedule next
        ScheduleNext();
    }

    /// <inheritdoc/>
    public bool ScheduleNext()
    {
        if (!_isEnabled)
            return false;

        var previousProcess = _activeProcess;
        
        // Find highest priority process ready to run
        for (int priority = ProcessPriority.Max; priority >= ProcessPriority.Min; priority--)
        {
            if (_priorityQueues[priority].TryDequeue(out var process))
            {
                _activeProcess = process;
                _logger.LogDebug("Scheduled process {Process} from priority {Priority}", 
                    process, priority);
                
                return !process.Equals(previousProcess);
            }
        }
        
        // No processes ready
        _activeProcess = OOP.Nil;
        _logger.LogDebug("No processes ready to run");
        return !previousProcess.IsNil;
    }

    /// <inheritdoc/>
    public IReadOnlyList<OOP> GetProcessesAtPriority(int priority)
    {
        if (priority < ProcessPriority.Min || priority > ProcessPriority.Max)
            return Array.Empty<OOP>();

        return _priorityQueues[priority].GetAll();
    }

    /// <inheritdoc/>
    public void ChangePriority(OOP process, int newPriority)
    {
        if (newPriority < ProcessPriority.Min || newPriority > ProcessPriority.Max)
            throw new ArgumentOutOfRangeException(nameof(newPriority));

        _logger.LogDebug("Changing process {Process} to priority {Priority}", 
            process, newPriority);

        // Remove from all queues
        for (int i = ProcessPriority.Min; i <= ProcessPriority.Max; i++)
        {
            _priorityQueues[i].Remove(process);
        }
        
        // Add to new priority queue
        _priorityQueues[newPriority].Enqueue(process);
        
        // If active process priority changed, might need to reschedule
        if (process.Equals(_activeProcess) && _isEnabled)
        {
            ScheduleNext();
        }
    }

    /// <summary>
    /// Sets the Process class for creating new processes.
    /// </summary>
    public void SetProcessClass(OOP processClass)
    {
        _processClass = processClass;
        _logger.LogDebug("Process class set to {OOP}", processClass);
    }

    private void CreateInitialProcess()
    {
        // Create a dummy initial process at user priority
        _activeProcess = CreateProcess(OOP.Nil, ProcessPriority.Default);
        _logger.LogInformation("Created initial process {Process}", _activeProcess);
    }

    private int GetProcessPriority(OOP process)
    {
        if (process.IsNil)
            return ProcessPriority.Min;

        // In full implementation, read priority from Process object
        // For now, return default
        return ProcessPriority.Default;
    }

    /// <summary>
    /// Thread-safe queue for processes at a specific priority.
    /// </summary>
    private sealed class ProcessQueue
    {
        private readonly ConcurrentQueue<OOP> _queue = new();
        private readonly HashSet<OOP> _contents = new();
        private readonly object _lock = new();
        private readonly int _priority;

        public ProcessQueue(int priority)
        {
            _priority = priority;
        }

        public void Enqueue(OOP process)
        {
            lock (_lock)
            {
                if (!_contents.Contains(process))
                {
                    _queue.Enqueue(process);
                    _contents.Add(process);
                }
            }
        }

        public bool TryDequeue(out OOP process)
        {
            lock (_lock)
            {
                if (_queue.TryDequeue(out process))
                {
                    _contents.Remove(process);
                    return true;
                }
                return false;
            }
        }

        public void Remove(OOP process)
        {
            lock (_lock)
            {
                _contents.Remove(process);
                // Note: Can't efficiently remove from middle of ConcurrentQueue
                // In production, might use different data structure
            }
        }

        public IReadOnlyList<OOP> GetAll()
        {
            lock (_lock)
            {
                return _queue.ToList();
            }
        }
    }
}