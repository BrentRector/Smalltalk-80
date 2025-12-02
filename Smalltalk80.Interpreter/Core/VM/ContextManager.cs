using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages execution contexts for the Smalltalk-80 VM.
/// Handles context creation, pooling, switching, and chain traversal.
/// Listens for GC events to clear context pools before garbage collection.
/// </summary>
/// <param name="heapManager">The heap manager for memory operations.</param>
/// <param name="metadata">VM metadata containing class OOPs.</param>
/// <param name="helpers">VM helper methods.</param>
/// <param name="diagnostics">Optional diagnostic hub for GC event notifications.</param>
internal class ContextManager(
    ISmalltalkHeap heapManager,
    VMMetadata metadata,
    IObjectMetadataAccessor helpers,
    IDiagnosticHub? diagnostics = null) : IContextManager, IDiagnosticListener
{
    // Diagnostic hub for GC event notifications
    private readonly IDiagnosticHub? _diagnostics = diagnostics;

    // Active context (current MethodContext being executed)
    private SmalltalkOOP _activeContextOOP;

    // Note: The Blue Book Smalltalk-80 did NOT track global context depth.
    // Context memory is managed by garbage collection. With multi-process scheduling,
    // a global depth counter is fundamentally flawed because contexts from all processes
    // are counted together, and terminated processes leave "orphaned" increments.
    // Stack overflow detection should use per-context depth tracking if needed.

    // Context pooling - separate pools for MethodContext and BlockContext by size
    // Key: (classOOP, size), Value: Stack of context OOPs
    private readonly Dictionary<(SmalltalkOOP classOOP, int size), Stack<SmalltalkOOP>> _contextPools = [];
    private const int MaxPooledContextsPerSize = 20;

    // Pool statistics - track hit rate
    private long _poolHits = 0;
    private long _poolMisses = 0;
    private long _poolEvictions = 0;  // Contexts discarded due to pool full

    // Track contexts that have created blocks - these cannot be pooled because
    // the blocks may outlive the method activation and still reference the context
    // via their 'home' field.
    private readonly HashSet<SmalltalkOOP> _contextsWithBlocks = [];
    private long _blockedFromPooling = 0;  // Contexts skipped due to having blocks

    /// <inheritdoc />
    public SmalltalkOOP ActiveContext => _activeContextOOP;

    /// <inheritdoc />
    public void SetActiveContext(SmalltalkOOP contextOOP) => _activeContextOOP = contextOOP;

    /// <inheritdoc />
    public SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
    {
        // Step 1: Validate CompiledMethod exists and parse header
        var header = helpers.ParseMethodHeader(methodOOP);

        // Validate argument count matches
        if (arguments.Length != header.NumArgs)
        {
            throw new InvalidOperationException(
                $"Argument count mismatch: method expects {header.NumArgs} args, got {arguments.Length}");
        }

        // Step 2: Calculate stack size (args + temps + execution stack)
        int executionStackSize = header.NeedsLargeFrame ? 48 : 16;
        int stackSize = header.NumArgs + header.NumTemps + executionStackSize;

        // Step 3: Get MethodContext class from RuntimeSpecialObjects
        var methodContextClassOOP = RuntimeSpecialObjects.Instance.MethodContextClass;

        // Step 4: Try to reuse a pooled context with matching size, otherwise allocate new
        SmalltalkOOP contextOOP;
        // MethodContext has 6 instance variables: sender, pc, stackp, method, lexicalParent, receiver
        // See VMConstants.CONTEXT_* for field indices
        const int METHODCONTEXT_INSTANCE_VAR_COUNT = 6;

        // Check class+size-specific pool for matching context
        var poolKey = (methodContextClassOOP, stackSize);
        if (_contextPools.TryGetValue(poolKey, out var pool) && pool.Count > 0)
        {
            contextOOP = pool.Pop();

            // Verify pooled context is safe to reuse
            if (contextOOP == _activeContextOOP || IsInSenderChain(contextOOP, _activeContextOOP))
            {
                // Active or in sender chain - put it back and allocate new
                pool.Push(contextOOP);
                contextOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, stackSize);
                heapManager.SetClassOOP(contextOOP, methodContextClassOOP);
                _poolMisses++;  // Safety check failed, count as miss
            }
            else if (heapManager.GetClassOOP(contextOOP) != methodContextClassOOP)
            {
                // Class mismatch - pool corruption detected, discard and allocate new
                contextOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, stackSize);
                heapManager.SetClassOOP(contextOOP, methodContextClassOOP);
                _poolMisses++;  // Pool corruption, count as miss
            }
            else
            {
                _poolHits++;  // Successfully reused from pool
            }
        }
        else
        {
            contextOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, stackSize);
            heapManager.SetClassOOP(contextOOP, methodContextClassOOP);
            _poolMisses++;  // Pool empty or doesn't exist
        }

        // Step 5: Get field indices from VMConstants
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int lexicalParentIndex = VMConstants.CONTEXT_LEXICALPARENT_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Step 6: Initialize MethodContext fixed fields
        var callerOOP = callerContextOOP == default ? metadata.KnownObjects.Nil : callerContextOOP;
        heapManager.SetInstanceVariable(contextOOP, senderIndex, callerOOP);
        heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));
        heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));
        heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);
        heapManager.SetInstanceVariable(contextOOP, lexicalParentIndex, metadata.KnownObjects.Nil);
        heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);

        // Step 7: Initialize indexed fields (args + temps + stack)
        for (int i = 0; i < header.NumArgs; i++)
        {
            heapManager.SetIndexedOOP(contextOOP, i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            heapManager.SetIndexedOOP(contextOOP, i, metadata.KnownObjects.Nil);
        }

        // Initialize execution stack slots to nil
        for (int i = header.NumArgs + header.NumTemps; i < stackSize; i++)
        {
            heapManager.SetIndexedOOP(contextOOP, i, metadata.KnownObjects.Nil);
        }

        return contextOOP;
    }

    /// <inheritdoc />
    public SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP)
    {
        var currentOOP = contextOOP;
        int depth = 0;
        const int maxDepth = 100;

        while (depth < maxDepth)
        {
            var contextClassOOP = heapManager.GetClassOOP(currentOOP);
            bool isBlockContext = contextClassOOP == metadata.BlockContextClassOOP;

            if (!isBlockContext)
            {
                return currentOOP;
            }

            currentOOP = heapManager.GetInstanceVariable(currentOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            depth++;
        }

        throw new InvalidOperationException($"FindMethodContext: Max traversal depth exceeded (depth={maxDepth})");
    }

    /// <inheritdoc />
    public bool IsInSenderChain(SmalltalkOOP contextToFind, SmalltalkOOP startContext)
    {
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        var current = startContext;
        var nilOOP = metadata.KnownObjects.Nil;

        for (int i = 0; i < 10000; i++)
        {
            if (current == nilOOP || current == default)
            {
                return false;
            }

            var senderOOP = heapManager.GetInstanceVariable(current, senderIndex);
            if (senderOOP == contextToFind)
            {
                return true;
            }

            current = senderOOP;
        }

        return false;
    }

    /// <inheritdoc />
    public void MarkContextCreatedBlock(SmalltalkOOP contextOOP)
    {
        // Mark this context as having created at least one block.
        // This prevents pooling since the block may outlive this activation
        // and still reference the context via its 'home' field.
        _contextsWithBlocks.Add(contextOOP);
    }

    /// <inheritdoc />
    public void AddContextToPool(SmalltalkOOP contextOOP)
    {
        // NEVER pool the active context
        if (contextOOP == _activeContextOOP)
        {
            return;
        }

        // NEVER pool a context that's in the active context's sender chain
        if (IsInSenderChain(contextOOP, _activeContextOOP))
        {
            return;
        }

        // NEVER pool a context that has created blocks - the blocks may
        // still reference this context via their 'home' field
        if (_contextsWithBlocks.Contains(contextOOP))
        {
            _contextsWithBlocks.Remove(contextOOP);  // Clean up tracking
            _blockedFromPooling++;
            return;
        }

        // Get context class and size for pool key
        SmalltalkOOP classOOP = heapManager.GetClassOOP(contextOOP);
        int size = heapManager.GetIndexedSize(contextOOP);
        var poolKey = (classOOP, size);

        if (!_contextPools.TryGetValue(poolKey, out var poolForSize))
        {
            poolForSize = new Stack<SmalltalkOOP>();
            _contextPools[poolKey] = poolForSize;
        }

        if (poolForSize.Count < MaxPooledContextsPerSize)
        {
            poolForSize.Push(contextOOP);
        }
        else
        {
            _poolEvictions++;  // Pool full, context discarded
        }
    }

    // Track if we added the active context as a temporary root (to remove after GC)
    private SmalltalkOOP _temporaryRootContext;

    /// <summary>
    /// Initializes the ContextManager and registers for GC events.
    /// Must be called after construction to enable pool clearing before GC.
    /// </summary>
    public void Initialize()
    {
        if (_diagnostics != null)
        {
            // Register for both GCStart (to add active context root) and GCEnd (to remove it)
            _diagnostics.Register(this, DiagnosticEventTypes.GCStart | DiagnosticEventTypes.GCEnd);
        }
    }

    // ============================================================
    // IDiagnosticListener Implementation
    // ============================================================

    /// <inheritdoc />
    public string Name => "ContextPoolManager";

    /// <inheritdoc />
    public IDiagnosticFilter? Filter => null;

    /// <inheritdoc />
    public void OnEvent(DiagnosticEvent evt)
    {
        if (evt is GCStartEvent)
        {
            // CRITICAL: Add the active context as a temporary GC root
            // Per Blue Book Chapter 30: GC roots include "the interpreter's stacks" (active context chain)
            // When a process is RUNNING, its suspendedContext is nil, so the active context
            // is NOT reachable from the Smalltalk object graph - only from the interpreter's register.
            // We must explicitly root it or GC will incorrectly collect it.
            _temporaryRootContext = default;
            if (_activeContextOOP != default && _activeContextOOP != metadata.KnownObjects.Nil)
            {
                heapManager.GC.AddGCRoot(_activeContextOOP);
                _temporaryRootContext = _activeContextOOP;
            }

            // Display statistics before clearing pools
            DisplayPoolStatistics();

            ClearAllPools();
        }
        else if (evt is GCEndEvent)
        {
            // Remove the temporary root we added at GCStart
            if (_temporaryRootContext != default)
            {
                heapManager.GC.RemoveGCRoot(_temporaryRootContext);
                _temporaryRootContext = default;
            }
        }
    }

    /// <summary>
    /// Displays pool statistics if any contexts were pooled since last GC.
    /// Shows pool size, hit rate (if there were requests), and eviction count.
    /// Only displays if there are contexts in the pool.
    /// </summary>
    private void DisplayPoolStatistics()
    {
        // Calculate total contexts in all pools
        int totalPooled = 0;
        foreach (var (key, pool) in _contextPools)
        {
            totalPooled += pool.Count;
        }

        // Only display if there are pooled contexts
        if (totalPooled == 0)
            return;

        // Context pool statistics (disabled - too verbose)
        // long totalRequests = _poolHits + _poolMisses;
        // if (totalRequests > 0)
        // {
        //     double hitRate = (_poolHits * 100.0) / totalRequests;
        //     Console.WriteLine($"    Context Pool: {totalPooled} contexts, {_poolHits}/{totalRequests} hits ({hitRate:F1}%), {_poolEvictions} evicted");
        // }
    }

    /// <summary>
    /// Clears all context pools before GC runs.
    /// This prevents use-after-free bugs where GC collects pooled contexts
    /// and heap reallocates the OOP to a different object type.
    /// Pools naturally rebuild as methods return after GC.
    /// Also resets pool statistics and block tracking for next cycle.
    /// </summary>
    private void ClearAllPools()
    {
        foreach (var (key, pool) in _contextPools)
        {
            pool.Clear();
        }

        // Clear block tracking - contexts referenced by live blocks will be
        // re-marked when those blocks are evaluated
        _contextsWithBlocks.Clear();

        // Reset statistics for next cycle
        _poolHits = 0;
        _poolMisses = 0;
        _poolEvictions = 0;
        _blockedFromPooling = 0;
    }
}
