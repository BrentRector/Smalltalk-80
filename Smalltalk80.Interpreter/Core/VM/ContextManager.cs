using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM;

/// <summary>
/// Manages execution contexts for the Smalltalk-80 VM.
/// Handles context creation, pooling, switching, and chain traversal.
/// </summary>
internal class ContextManager : IContextManager
{
    private readonly ISmalltalkHeapManager _heapManager;
    private readonly VMMetadata _metadata;
    private readonly LayoutResolver _layoutResolver;
    private readonly IVMHelpers _helpers;

    // Active context (current MethodContext being executed)
    private SmalltalkOOP _activeContextOOP;

    // Context depth tracking to detect infinite recursion
    private int _activeContextCount = 0;
    private const int MAX_CONTEXT_DEPTH = 5000;

    // Context pooling - reuse recently returned contexts to reduce GC pressure
    private readonly Dictionary<int, Stack<SmalltalkOOP>> _contextPools = new();
    private const int MAX_POOLED_CONTEXTS_PER_SIZE = 20;

    public SmalltalkOOP ActiveContext => _activeContextOOP;
    public int ContextDepth => _activeContextCount;

    public ContextManager(
        ISmalltalkHeapManager heapManager,
        VMMetadata metadata,
        LayoutResolver layoutResolver,
        IVMHelpers helpers)
    {
        _heapManager = heapManager;
        _metadata = metadata;
        _layoutResolver = layoutResolver;
        _helpers = helpers;
    }

    public void SetActiveContext(SmalltalkOOP contextOOP)
    {
        _activeContextOOP = contextOOP;
    }

    public void ResetContextDepth()
    {
        _activeContextCount = 0;
    }

    public void DecrementContextDepth()
    {
        _activeContextCount--;
    }

    public SmalltalkOOP CreateMethodContext(SmalltalkOOP methodOOP, SmalltalkOOP receiverOOP, SmalltalkOOP[] arguments, SmalltalkOOP callerContextOOP = default)
    {
        // Step 1: Validate CompiledMethod exists and parse header
        var header = _helpers.ParseMethodHeader(methodOOP);

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
        int instanceVarCount = _layoutResolver.GetInstanceVariableCount("class:MethodContext");

        // Check size-specific pool for matching context
        if (_contextPools.TryGetValue(stackSize, out var pool) && pool.Count > 0)
        {
            contextOOP = pool.Pop();

            // Check if we accidentally picked the active context
            if (contextOOP == _activeContextOOP || IsInSenderChain(contextOOP, _activeContextOOP))
            {
                pool.Push(contextOOP);
                contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
                _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP);
            }
        }
        else
        {
            contextOOP = _heapManager.AllocateObject(ObjectFormat.PointerIndexed, instanceVarCount, stackSize);
            _heapManager.SetSlot(contextOOP, 0, methodContextClassOOP);
        }

        // Increment context depth and check limit
        _activeContextCount++;

        if (_activeContextCount > MAX_CONTEXT_DEPTH)
        {
            throw new InvalidOperationException(
                $"Context depth limit exceeded: {_activeContextCount} contexts (limit: {MAX_CONTEXT_DEPTH}). " +
                $"This typically indicates infinite recursion or primitive failure causing excessive message sends.");
        }

        // Step 5: Get field indices
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        int pcIndex = VMConstants.CONTEXT_PC_INDEX;
        int stackpIndex = VMConstants.CONTEXT_STACKP_INDEX;
        int methodIndex = VMConstants.CONTEXT_METHOD_INDEX;
        int receiverIndex = VMConstants.CONTEXT_RECEIVER_INDEX;

        // Step 6: Initialize MethodContext fixed fields
        var callerOOP = callerContextOOP == default ? SpecialObjectsArray.Instance.Nil : callerContextOOP;
        _heapManager.SetInstanceVariable(contextOOP, senderIndex, callerOOP);
        _heapManager.SetInstanceVariable(contextOOP, pcIndex, SmalltalkOOP.FromSmallInteger(0));
        _heapManager.SetInstanceVariable(contextOOP, stackpIndex, SmalltalkOOP.FromSmallInteger(header.NumArgs + header.NumTemps));
        _heapManager.SetInstanceVariable(contextOOP, methodIndex, methodOOP);
        _heapManager.SetInstanceVariable(contextOOP, receiverIndex, receiverOOP);

        // Step 7: Initialize indexed fields (args + temps + stack)
        for (int i = 0; i < header.NumArgs; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, arguments[i]);
        }

        // Initialize temps to nil
        for (int i = header.NumArgs; i < header.NumArgs + header.NumTemps; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        // Initialize execution stack slots to nil
        for (int i = header.NumArgs + header.NumTemps; i < stackSize; i++)
        {
            _heapManager.SetIndexedOOP(contextOOP, i, SpecialObjectsArray.Instance.Nil);
        }

        return contextOOP;
    }

    public SmalltalkOOP FindMethodContext(SmalltalkOOP contextOOP)
    {
        var currentOOP = contextOOP;
        int depth = 0;
        const int MAX_DEPTH = 100;

        while (depth < MAX_DEPTH)
        {
            var contextClassOOP = _heapManager.GetClassOOP(currentOOP);
            bool isBlockContext = (contextClassOOP == _metadata.BlockContextClassOOP);

            if (!isBlockContext)
            {
                return currentOOP;
            }

            currentOOP = _heapManager.GetInstanceVariable(currentOOP, VMConstants.BLOCKCONTEXT_HOME_INDEX);
            depth++;
        }

        throw new InvalidOperationException($"FindMethodContext: Max traversal depth exceeded (depth={MAX_DEPTH})");
    }

    public bool IsInSenderChain(SmalltalkOOP contextToFind, SmalltalkOOP startContext)
    {
        int senderIndex = VMConstants.CONTEXT_SENDER_INDEX;
        SmalltalkOOP current = startContext;
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        for (int i = 0; i < 100; i++)
        {
            if (current == nilOOP || current == default)
                return false;

            var senderOOP = _heapManager.GetInstanceVariable(current, senderIndex);
            if (senderOOP == contextToFind)
                return true;

            current = senderOOP;
        }

        return false;
    }

    public void AddContextToPool(SmalltalkOOP contextOOP)
    {
        // NEVER pool the active context
        if (contextOOP == _activeContextOOP)
            return;

        // NEVER pool a context that's in the active context's sender chain
        if (IsInSenderChain(contextOOP, _activeContextOOP))
            return;

        int size = _heapManager.GetIndexedSize(contextOOP);
        if (!_contextPools.TryGetValue(size, out var poolForSize))
        {
            poolForSize = new Stack<SmalltalkOOP>();
            _contextPools[size] = poolForSize;
        }

        if (poolForSize.Count < MAX_POOLED_CONTEXTS_PER_SIZE)
        {
            poolForSize.Push(contextOOP);
        }
    }
}
