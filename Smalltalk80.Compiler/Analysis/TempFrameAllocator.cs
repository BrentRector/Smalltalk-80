namespace Smalltalk80.Compiler.Analysis;

/// <summary>
/// Allocates temp slots in the unified temp frame with slot reuse for sequential blocks.
///
/// UNIFIED TEMP FRAME ARCHITECTURE:
/// All temporaries (method args/temps + ALL nested block args/temps) are allocated in the
/// MethodContext's temp frame using absolute indices. The compiler performs static analysis to:
///
/// 1. Allocate temp slots in the unified frame (maximum simultaneous need, not sum)
/// 2. Reuse slots for sequential non-overlapping blocks
/// 3. Emit absolute indices (0, 1, 2...) directly into bytecode
///
/// Blue Book Chapter 30: BlueBook-Context-Architecture.md Section 4.3
/// </summary>
public class TempFrameAllocator
{
    private int _nextSlot = 0;
    private int _maxSimultaneous = 0;
    private readonly Dictionary<string, int> _allocatedSlots = new();
    private readonly Stack<ScopeInfo> _scopeStack = new();

    private class ScopeInfo
    {
        public HashSet<int> AllocatedSlots { get; } = new();
        public int StartSlot { get; set; }
        public bool IsSequential { get; set; }  // Can reuse slots after exit?
    }

    /// <summary>
    /// Enters a method scope and allocates slots for method arguments and temporaries.
    /// Method scopes never reuse slots (they're the root scope).
    /// </summary>
    /// <param name="argNames">Method argument names</param>
    /// <param name="tempNames">Method temporary variable names</param>
    public void EnterMethodScope(List<string> argNames, List<string> tempNames)
    {
        var scope = new ScopeInfo { StartSlot = _nextSlot, IsSequential = false };
        _scopeStack.Push(scope);

        // Allocate slots for method args
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        // Allocate slots for method temps
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    /// <summary>
    /// Enters a block scope and allocates slots for block arguments and temporaries.
    /// Sequential blocks can reuse slots after they exit.
    /// Nested blocks cannot reuse slots (they're active simultaneously with parent).
    /// </summary>
    /// <param name="argNames">Block parameter names</param>
    /// <param name="tempNames">Block temporary variable names</param>
    /// <param name="isNested">True if block is nested inside another block or will be stored/passed</param>
    public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
    {
        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = !isNested  // Sequential blocks can reuse, nested cannot
        };
        _scopeStack.Push(scope);

        // Allocate slots for block args
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        // Allocate slots for block temps
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    /// <summary>
    /// Exits the current scope.
    /// For sequential blocks, frees slots for reuse by subsequent sequential blocks.
    /// For nested blocks and method scope, slots remain allocated.
    /// </summary>
    public void ExitScope()
    {
        if (_scopeStack.Count == 0)
        {
            throw new InvalidOperationException("Cannot exit scope - no scope is active");
        }

        var scope = _scopeStack.Pop();

        if (scope.IsSequential && scope.AllocatedSlots.Count > 0)
        {
            // Reuse slots from this sequential block
            int minSlot = scope.AllocatedSlots.Min();
            _nextSlot = minSlot;

            // Remove variables from allocation map
            var varsToRemove = _allocatedSlots
                .Where(kvp => scope.AllocatedSlots.Contains(kvp.Value))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var varName in varsToRemove)
            {
                _allocatedSlots.Remove(varName);
            }
        }
    }

    /// <summary>
    /// Gets the absolute slot index for a variable name.
    /// The index is used directly in PUSH_TEMP/STORE_TEMP bytecodes.
    /// </summary>
    /// <param name="varName">Variable name to look up</param>
    /// <returns>Absolute index into the unified temp frame (0-based)</returns>
    /// <exception cref="InvalidOperationException">Variable not found in current scope</exception>
    public int GetSlotIndex(string varName)
    {
        if (_allocatedSlots.TryGetValue(varName, out int slot))
        {
            return slot;
        }

        throw new InvalidOperationException($"Variable '{varName}' not found in temp frame");
    }

    /// <summary>
    /// Tries to get the absolute slot index for a variable name.
    /// Returns true if found, false otherwise.
    /// </summary>
    /// <param name="varName">Variable name to look up</param>
    /// <param name="slotIndex">Output: absolute slot index if found</param>
    /// <returns>True if variable was found, false otherwise</returns>
    public bool TryGetSlotIndex(string varName, out int slotIndex)
    {
        return _allocatedSlots.TryGetValue(varName, out slotIndex);
    }

    /// <summary>
    /// Gets the maximum frame size needed for this method.
    /// This is the maximum simultaneous slot usage, not the sum of all temps.
    /// Used in method header to allocate the MethodContext's indexed slot array.
    /// </summary>
    /// <returns>Maximum number of temp slots needed simultaneously</returns>
    public int GetMaxFrameSize()
    {
        return _maxSimultaneous;
    }

    /// <summary>
    /// Checks if a variable is currently allocated in the temp frame.
    /// </summary>
    /// <param name="varName">Variable name to check</param>
    /// <returns>True if variable is allocated, false otherwise</returns>
    public bool HasVariable(string varName)
    {
        return _allocatedSlots.ContainsKey(varName);
    }

    /// <summary>
    /// Gets the current depth of the scope stack.
    /// Method scope = 1, nested block = 2+
    /// </summary>
    public int ScopeDepth => _scopeStack.Count;
}
