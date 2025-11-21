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

    // Context depth tracking to detect infinite recursion
    private int _activeContextCount;
    private const int MaxContextDepth = 5000;

    // Context pooling - separate pools for MethodContext and BlockContext by size
    // Key: (classOOP, size), Value: Stack of context OOPs
    private readonly Dictionary<(SmalltalkOOP classOOP, int size), Stack<SmalltalkOOP>> _contextPools = [];
    private const int MaxPooledContextsPerSize = 20;

    // Pool statistics - track hit rate
    private long _poolHits = 0;
    private long _poolMisses = 0;
    private long _poolEvictions = 0;  // Contexts discarded due to pool full

    /// <inheritdoc />
    public SmalltalkOOP ActiveContext => _activeContextOOP;

    /// <inheritdoc />
    public int ContextDepth => _activeContextCount;

    /// <inheritdoc />
    public void SetActiveContext(SmalltalkOOP contextOOP) => _activeContextOOP = contextOOP;

    /// <inheritdoc />
    public void ResetContextDepth() => _activeContextCount = 0;

    /// <inheritdoc />
    public void DecrementContextDepth() => _activeContextCount--;

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

            // Check if we accidentally picked the active context
            if (contextOOP == _activeContextOOP || IsInSenderChain(contextOOP, _activeContextOOP))
            {
                pool.Push(contextOOP);
                contextOOP = heapManager.AllocateObject(ObjectFormat.PointerIndexed, METHODCONTEXT_INSTANCE_VAR_COUNT, stackSize);
                heapManager.SetClassOOP(contextOOP, methodContextClassOOP);
                _poolMisses++;  // Safety check failed, count as miss
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

        // Increment context depth and check limit
        _activeContextCount++;

        if (_activeContextCount > MaxContextDepth)
        {
            throw new InvalidOperationException(
                $"Context depth limit exceeded: {_activeContextCount} contexts (limit: {MaxContextDepth}). " +
                $"This typically indicates infinite recursion or primitive failure causing excessive message sends.");
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

    /// <summary>
    /// Initializes the ContextManager and registers for GC events.
    /// Must be called after construction to enable pool clearing before GC.
    /// </summary>
    public void Initialize()
    {
        if (_diagnostics != null)
        {
            // Register to receive ONLY GCStart events
            _diagnostics.Register(this, DiagnosticEventTypes.GCStart);
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
        // Hub only dispatches GCStart events (our registered type)
        // Safe to cast without checking
        var gcStart = (GCStartEvent)evt;

        // Display statistics before clearing pools
        DisplayPoolStatistics();

        ClearAllPools();
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

        // Display statistics based on whether there have been requests
        long totalRequests = _poolHits + _poolMisses;
        if (totalRequests > 0)
        {
            // Have requests - show hit rate
            double hitRate = (_poolHits * 100.0) / totalRequests;
            Console.WriteLine($"    Context Pool: {totalPooled} contexts, {_poolHits}/{totalRequests} hits ({hitRate:F1}%), {_poolEvictions} evicted");
        }
        else
        {
            // No requests yet - just show pool size and evictions
            Console.WriteLine($"    Context Pool: {totalPooled} contexts (no requests yet), {_poolEvictions} evicted");
        }
    }

    /// <summary>
    /// Clears all context pools before GC runs.
    /// This prevents use-after-free bugs where GC collects pooled contexts
    /// and heap reallocates the OOP to a different object type.
    /// Pools naturally rebuild as methods return after GC.
    /// Also resets pool statistics for next cycle.
    /// </summary>
    private void ClearAllPools()
    {
        foreach (var (key, pool) in _contextPools)
        {
            pool.Clear();
        }

        // Reset statistics for next cycle
        _poolHits = 0;
        _poolMisses = 0;
        _poolEvictions = 0;
    }
}
