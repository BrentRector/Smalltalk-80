# Context Pool Eviction Strategy

**Author**: Claude (Sonnet 4.5)
**Date**: 2025-11-21
**Status**: Planning - Awaiting Decision
**Related**: Context-Caching-Analysis.md

---

## Problem Statement

The current context pooling implementation has a **critical architectural flaw**:

**Current Implementation:**
```csharp
// ContextManager.cs:188-213
public void AddContextToPool(SmalltalkOOP contextOOP)
{
    if (poolForSize.Count < MaxPooledContextsPerSize)
    {
        poolForSize.Push(contextOOP);
        // ❌ NO GC ROOT REGISTRATION
    }
}
```

**The Bug:**
1. Context returns and is added to pool
2. Context is no longer in active sender chain
3. GC runs → marks from roots (nil, true, false, Smalltalk)
4. Pooled context is **not reachable** from any root
5. **GC collects the pooled context**
6. Pool still holds OOP to freed object
7. Heap **reallocates same OOP** to different object (e.g., String)
8. Next pool reuse: `IsValidOOP(oop)` returns `true` (OOP exists)
9. VM tries to use String as MethodContext → **CORRUPTION**

**Why Two-Tier Pool Fails:**
- Cold pool contexts are not GC roots
- GC collects them (correct)
- **Heap reuses the OOP for a different object** (correct)
- No generation counter or tombstone to detect "this OOP changed"
- `IsValidOOP()` only checks if OOP exists, not if it's the same object
- Result: Type confusion and memory corruption

**Conclusion:** Pooled contexts **MUST** be GC roots while pooled.

---

## Constraint: Eviction Is Required

Simply making all pooled contexts GC roots creates a **memory leak**:

```csharp
// ❌ This leaks memory
public void AddContextToPool(SmalltalkOOP contextOOP)
{
    poolForSize.Push(contextOOP);
    heapManager.GC.AddGCRoot(contextOOP);  // Never removed!
}
```

Without eviction:
- Pool fills to max (20 per size)
- All pooled contexts are permanent GC roots
- **Never collected** even if not reused
- Memory grows unbounded over time

**We need:** Pooled contexts as GC roots + eviction mechanism.

---

## Option A: GC-Triggered Pruning (Recommended)

### Overview
Before each GC cycle, prune each pool down to a small size (e.g., 5 contexts per size). Remove evicted contexts from GC roots, allowing them to be collected.

### Implementation

**ContextManager becomes a diagnostic listener:**

```csharp
internal class ContextManager : IContextManager, IDiagnosticListener
{
    private readonly IDiagnosticHub? _diagnostics;
    private readonly Dictionary<int, Stack<SmalltalkOOP>> _contextPools = [];
    private const int MaxPooledContextsPerSize = 20;
    private const int PoolSizeAfterGC = 5;  // Keep 5 most recent per size

    public ContextManager(
        ISmalltalkHeap heapManager,
        VMMetadata metadata,
        IObjectMetadataAccessor helpers,
        IDiagnosticHub? diagnostics = null)
    {
        // ... existing initialization ...

        _diagnostics = diagnostics;

        // Register for ONLY GCStart events (no other events received)
        if (_diagnostics != null)
        {
            _diagnostics.Register(this, DiagnosticEventTypes.GCStart);
        }
    }

    // IDiagnosticListener implementation
    public string Name => "ContextPoolManager";
    public IDiagnosticFilter? Filter => null;

    public void OnEvent(DiagnosticEvent evt)
    {
        // Hub only dispatches registered event types
        // Safe to cast without checking
        var gcStart = (GCStartEvent)evt;
        PruneContextPools();
    }

    private void PruneContextPools()
    {
        foreach (var (size, pool) in _contextPools)
        {
            // Evict oldest contexts, keeping only the most recent
            while (pool.Count > PoolSizeAfterGC)
            {
                var evicted = pool.Pop();
                heapManager.GC.RemoveGCRoot(evicted);  // ✅ Allow GC to collect
            }
        }
    }

    public void AddContextToPool(SmalltalkOOP contextOOP)
    {
        // ... existing safety checks ...

        int size = heapManager.GetIndexedSize(contextOOP);
        if (!_contextPools.TryGetValue(size, out var poolForSize))
        {
            poolForSize = new Stack<SmalltalkOOP>();
            _contextPools[size] = poolForSize;
        }

        if (poolForSize.Count < MaxPooledContextsPerSize)
        {
            poolForSize.Push(contextOOP);
            heapManager.GC.AddGCRoot(contextOOP);  // ✅ Protect from GC
        }
        // else: pool full, don't add (context will be collected)
    }
}
```

**GC Lifecycle:**
```
1. Context returns → AddContextToPool → GC.AddGCRoot
2. Pool has 20 contexts (all GC roots)
3. GC about to run → Emit GCStartEvent
4. ContextManager receives event → PruneContextPools
5. Evict 15 oldest → RemoveGCRoot for each
6. Pool now has 5 contexts (still GC roots)
7. GC runs → collects the 15 evicted contexts
8. After GC, pool rebuilds naturally as methods return
```

### Configuration

```csharp
// Tunable parameters
private const int MaxPooledContextsPerSize = 20;   // Soft limit (can exceed temporarily)
private const int PoolSizeAfterGC = 5;              // Hard limit after pruning

// Alternative configurations:
// Aggressive pruning: MaxPool=50, AfterGC=5   (large pool, aggressive pruning)
// Conservative: MaxPool=10, AfterGC=5         (small pool, gentle pruning)
// No pruning: MaxPool=10, AfterGC=10          (pool never pruned, capped at 10)
```

### Memory Characteristics

**Worst case (before GC):**
- 20 contexts per size
- 8 common sizes (0+0+16, 1+0+16, 0+1+16, 1+1+16, 2+0+16, 0+2+16, 3+0+16, 0+3+16)
- Context size: ~256 bytes average
- Memory: 20 × 8 × 256 = **41 KB**

**Best case (after GC):**
- 5 contexts per size
- Memory: 5 × 8 × 256 = **10 KB**

**Amortized:** 10-41 KB, oscillating with GC cycles.

### Performance Characteristics

**Pool hit rate:**
- Before GC: High (pool at max 20)
- After GC: Lower (pool pruned to 5)
- Amortized: Medium (rebuilds between GCs)

**Eviction overhead:**
- Per GC cycle: 15 RemoveGCRoot calls per size
- Total: ~120 calls per GC (8 sizes × 15 evictions)
- Cost: Negligible (array manipulation)

**Rebuilding:**
- Pool naturally rebuilds as methods return
- No explicit rebuild logic needed
- Hot methods repopulate pool quickly

### Trade-offs

**Pros:**
- ✅ Simple implementation (20 lines)
- ✅ Safe (contexts always GC roots while pooled)
- ✅ Bounded memory (oscillates 10-41 KB)
- ✅ Self-regulating (prunes on memory pressure)
- ✅ No timing/age tracking needed
- ✅ Integrates cleanly with diagnostic system

**Cons:**
- ⚠️ May evict still-useful contexts
- ⚠️ Pool hit rate drops after GC
- ⚠️ Ties pool management to GC frequency
- ⚠️ Discards contexts even if not memory constrained

### Risk Assessment

**Risk level:** LOW
- No complex state machines
- No race conditions (single-threaded VM)
- Well-defined lifecycle (GCStart → prune → GC → rebuild)
- Easy to debug (inspect pool sizes)

---

## Option B: Clear All Pools Before GC

### Overview
Before each GC, completely empty all pools. Remove all contexts from GC roots. Pools rebuild naturally after GC.

### Implementation

```csharp
public void OnEvent(DiagnosticEvent evt)
{
    var gcStart = (GCStartEvent)evt;
    ClearAllPools();
}

private void ClearAllPools()
{
    foreach (var (size, pool) in _contextPools)
    {
        while (pool.Count > 0)
        {
            var oop = pool.Pop();
            heapManager.GC.RemoveGCRoot(oop);
        }
    }
}
```

### Memory Characteristics

**Before GC:** 0-20 contexts per size (0-41 KB)
**After GC:** 0 contexts (0 KB)
**Amortized:** 0-20 KB (rebuilds linearly)

### Performance Characteristics

**Pool hit rate:**
- Before GC: Medium (0-20 contexts)
- After GC: 0% (pool empty)
- Rebuilding phase: 0% until pool repopulates

**Cold start after GC:**
- First 20 method calls after GC → miss
- Then hit rate increases

### Trade-offs

**Pros:**
- ✅ Extremely simple (10 lines)
- ✅ Minimal memory (0 KB after GC)
- ✅ No configuration needed
- ✅ Predictable behavior

**Cons:**
- ⚠️ Pool completely cleared each GC
- ⚠️ Lower average hit rate
- ⚠️ Cold start penalty after every GC
- ⚠️ Wastes contexts that could be reused

### Risk Assessment

**Risk level:** VERY LOW
- Simplest possible implementation
- Cannot leak memory
- Easy to understand and debug

---

## Option C: LRU Eviction on Pool Full

### Overview
Track usage order. When pool reaches max size, evict least-recently-used context. No GC-triggered pruning.

### Implementation

```csharp
private class PooledContext
{
    public SmalltalkOOP OOP;
    public long LastUsedTick;
}

private readonly Dictionary<int, LinkedList<PooledContext>> _contextPools = [];
private const int MaxPooledContextsPerSize = 20;
private long _tick = 0;

public SmalltalkOOP CreateMethodContext(...)
{
    // Try pool first
    if (_contextPools.TryGetValue(stackSize, out var list) && list.Count > 0)
    {
        // MRU at front, LRU at back
        var node = list.First;
        var pooled = node.Value;
        list.RemoveFirst();

        pooled.LastUsedTick = _tick++;
        contextOOP = pooled.OOP;
        // Use context...
    }
    else
    {
        contextOOP = heapManager.AllocateObject(...);
    }

    return contextOOP;
}

public void AddContextToPool(SmalltalkOOP contextOOP, int size)
{
    if (!_contextPools.TryGetValue(size, out var list))
    {
        list = new LinkedList<PooledContext>();
        _contextPools[size] = list;
    }

    if (list.Count >= MaxPooledContextsPerSize)
    {
        // Evict LRU (back of list)
        var evicted = list.Last.Value;
        list.RemoveLast();
        heapManager.GC.RemoveGCRoot(evicted.OOP);  // ✅ Allow collection
    }

    var pooled = new PooledContext { OOP = contextOOP, LastUsedTick = _tick++ };
    list.AddFirst(pooled);  // Add as MRU (front of list)
    heapManager.GC.AddGCRoot(contextOOP);  // ✅ Protect from GC
}
```

### Memory Characteristics

**Steady state:** 20 contexts per size (constant 41 KB)
**Growth:** Linear until max, then constant
**Eviction:** One context evicted per one added (when full)

### Performance Characteristics

**Pool hit rate:**
- Highest of all options (keeps most frequently used)
- No cold start penalty
- No GC-related drops

**LRU overhead:**
- Linked list manipulation: O(1) for add/remove
- Tick increment: O(1)
- Per-allocation overhead: ~10 instructions

### Trade-offs

**Pros:**
- ✅ Best hit rate (LRU keeps hot contexts)
- ✅ Predictable memory (constant 41 KB)
- ✅ No GC-related disruption
- ✅ Fair eviction (least used goes first)

**Cons:**
- ⚠️ More complex (50 lines vs 20)
- ⚠️ Higher memory baseline (no pruning)
- ⚠️ LinkedList allocation overhead
- ⚠️ Tick counter could overflow (low risk)

### Risk Assessment

**Risk level:** MEDIUM
- More moving parts (linked list, tick counter)
- Requires careful testing
- Tick overflow handling needed (wrap at long.MaxValue)

---

## Option D: Age-Based Eviction

### Overview
Track when each context was pooled. Evict contexts older than a threshold (e.g., 5 seconds). Check ages periodically or on allocation.

### Implementation

```csharp
private class PooledContext
{
    public SmalltalkOOP OOP;
    public DateTime PooledAt;
}

private readonly Dictionary<int, Queue<PooledContext>> _contextPools = [];
private readonly TimeSpan MaxPoolAge = TimeSpan.FromSeconds(5);

public void AddContextToPool(SmalltalkOOP contextOOP, int size)
{
    var pooled = new PooledContext
    {
        OOP = contextOOP,
        PooledAt = DateTime.UtcNow
    };

    pool.Enqueue(pooled);
    heapManager.GC.AddGCRoot(contextOOP);
}

private void EvictStaleContexts()
{
    var now = DateTime.UtcNow;

    foreach (var (size, pool) in _contextPools)
    {
        while (pool.Count > 0)
        {
            var candidate = pool.Peek();
            if ((now - candidate.PooledAt) > MaxPoolAge)
            {
                pool.Dequeue();
                heapManager.GC.RemoveGCRoot(candidate.OOP);
            }
            else
            {
                break;  // Queue is ordered by age, stop when we hit recent
            }
        }
    }
}
```

### When to Call EvictStaleContexts()?

**Option D1: On every allocation**
```csharp
public SmalltalkOOP CreateMethodContext(...)
{
    EvictStaleContexts();  // Check before allocating
    // ... try pool ...
}
```
- Overhead: DateTime.UtcNow + queue iteration on every allocation
- Benefit: Immediate eviction

**Option D2: On GC start**
```csharp
public void OnEvent(DiagnosticEvent evt)
{
    var gcStart = (GCStartEvent)evt;
    EvictStaleContexts();
}
```
- Overhead: Only on GC cycles
- Benefit: Similar to Option A but age-based

**Option D3: Periodic timer**
```csharp
private System.Threading.Timer _evictionTimer;

public ContextManager(...)
{
    _evictionTimer = new Timer(
        callback: _ => EvictStaleContexts(),
        state: null,
        dueTime: TimeSpan.FromSeconds(5),
        period: TimeSpan.FromSeconds(5)
    );
}
```
- Overhead: Separate thread
- Risk: Thread safety issues (VM is single-threaded)

### Trade-offs

**Pros:**
- ✅ Evicts truly stale contexts
- ✅ Time-based is intuitive
- ✅ Independent of GC frequency

**Cons:**
- ⚠️ Time-based is non-deterministic (workload-dependent)
- ⚠️ DateTime overhead on every check
- ⚠️ When to check? (allocation, GC, timer)
- ⚠️ Timer option requires thread safety

### Risk Assessment

**Risk level:** MEDIUM-HIGH
- Time-based introduces non-determinism
- Unclear when to run eviction checks
- Timer option incompatible with single-threaded VM

---

## Comparison Matrix

| Metric | Option A: GC Pruning | Option B: GC Clear | Option C: LRU | Option D: Age-Based |
|--------|---------------------|-------------------|---------------|---------------------|
| **Implementation Complexity** | LOW (20 lines) | VERY LOW (10 lines) | MEDIUM (50 lines) | MEDIUM (40 lines) |
| **Memory (steady state)** | 10-41 KB | 0-20 KB | 41 KB | Variable |
| **Pool hit rate** | Medium | Low | High | Medium |
| **Eviction fairness** | FIFO | All | LRU | FIFO (age) |
| **Determinism** | High | High | High | Low (time-based) |
| **Risk level** | LOW | VERY LOW | MEDIUM | MEDIUM-HIGH |
| **Maintenance burden** | LOW | VERY LOW | MEDIUM | MEDIUM |
| **Configuration needed** | 2 constants | 1 constant | 1 constant | 2 constants + when to evict |

---

## Recommendation

**Start with Option A (GC-Triggered Pruning)** for the following reasons:

### Rationale

1. **Safety:** All pooled contexts are GC roots (no use-after-free)
2. **Simplicity:** 20 lines of code, easy to understand
3. **Self-regulating:** Prunes when memory is needed (GC trigger)
4. **Low risk:** Simple state machine, easy to debug
5. **Tunable:** Two constants to adjust behavior
6. **No time dependency:** Deterministic based on GC cycles

### Configuration

```csharp
private const int MaxPooledContextsPerSize = 20;   // Allow burst capacity
private const int PoolSizeAfterGC = 5;              // Keep 5 hottest per size
```

### Migration Path

If Option A proves insufficient, we can evolve:
- **Hit rate too low** → Try Option C (LRU)
- **Memory too high** → Try Option B (Clear all)
- **Need more control** → Add tuning parameters

### Success Criteria

After implementing Option A, measure:
- ✅ No use-after-free crashes
- ✅ Pool hit rate >30%
- ✅ Memory overhead <50 KB
- ✅ All 120 BlockContext tests pass
- ✅ Init expressions run correctly

---

## Implementation Checklist

When implementing Option A:

1. **Add IDiagnosticListener to ContextManager**
   - Implement Name, Filter, OnEvent
   - Register for GCStart only

2. **Update AddContextToPool**
   - Add `heapManager.GC.AddGCRoot(contextOOP)`
   - Keep existing safety checks

3. **Add PruneContextPools method**
   - Iterate all pools
   - Pop contexts down to PoolSizeAfterGC
   - RemoveGCRoot for each evicted

4. **Add configuration constants**
   - MaxPooledContextsPerSize = 20
   - PoolSizeAfterGC = 5

5. **Add instrumentation (optional but recommended)**
   - Track eviction counts
   - Track pool sizes over time
   - Expose via GetStatistics()

6. **Testing**
   - Run all 120 BlockContext tests
   - Run all init expressions
   - Verify no memory leaks (pool doesn't grow unbounded)
   - Verify no crashes (no use-after-free)

---

## Open Questions

1. **Should we add BlockContext pooling too?**
   - Same architecture (hot pool + eviction)
   - All BlockContexts same size (312 bytes)
   - High creation rate (260 per Character initialize)
   - **Recommendation:** Add after MethodContext pooling proven

2. **Should we expose pool statistics?**
   - Hit rate, miss rate, eviction rate
   - Per-size pool utilization
   - **Recommendation:** Yes, via `ContextManager.GetStatistics()`

3. **Should eviction be tunable at runtime?**
   - Allow changing PoolSizeAfterGC dynamically
   - **Recommendation:** Not initially, add if needed

4. **Should we warn if pool hit rate is very low?**
   - e.g., "Warning: Context pool hit rate <10%, consider tuning"
   - **Recommendation:** Not initially, just measure

---

## References

- **Current Implementation:** `Smalltalk80.Interpreter/Core/VM/ContextManager.cs:188-213`
- **GC Implementation:** `Smalltalk80.Interpreter.HeapManager/GarbageCollector.cs`
- **Diagnostic System:** `Smalltalk80.Interpreter.Shared.Types/Diagnostics/IDiagnosticHub.cs`
- **Analysis:** `docs/Context-Caching-Analysis.md`

---

**END OF DOCUMENT**
