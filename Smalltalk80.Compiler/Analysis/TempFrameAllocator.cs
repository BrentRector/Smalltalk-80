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
    private int _highestSlotIndex = -1;  // Track highest slot index ever allocated (0-based)
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
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }

        // Allocate slots for method temps
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    /// <summary>
    /// Enters block scope and allocates slots for block args AND temps.
    /// UNIFIED TEMP FRAME: Block arguments NOW allocated in MethodContext!
    /// </summary>
    public void EnterBlockScope(List<string> argNames, List<string> tempNames, bool isNested)
    {
        var scope = new ScopeInfo
        {
            StartSlot = _nextSlot,
            IsSequential = !isNested
        };
        _scopeStack.Push(scope);

        // CRITICAL CHANGE: Allocate block ARGUMENTS in unified frame
        // Previously this was skipped - arguments were stored in BlockContext
        // Now ALL variables go in MethodContext temp frame
        foreach (var argName in argNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[argName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);

            Console.WriteLine($"[TempFrameAllocator] Block arg '{argName}' → slot {slot}");
        }

        // Allocate block temps (same as before)
        foreach (var tempName in tempNames)
        {
            int slot = _nextSlot++;
            _allocatedSlots[tempName] = slot;
            scope.AllocatedSlots.Add(slot);
            _highestSlotIndex = Math.Max(_highestSlotIndex, slot);

            Console.WriteLine($"[TempFrameAllocator] Block temp '{tempName}' → slot {slot}");
        }

        _maxSimultaneous = Math.Max(_maxSimultaneous, _nextSlot);
    }

    /// <summary>
    /// Gets the starting index where current block's arguments are allocated.
    /// Returns -1 if not in block scope or no arguments.
    /// Used by BytecodeCompiler to pass argStartIndex to Primitive 80.
    /// </summary>
    public int GetCurrentBlockArgStartIndex()
    {
        if (_scopeStack.Count == 0)
            return -1;

        var currentScope = _scopeStack.Peek();

        if (currentScope.AllocatedSlots.Count == 0)
            return -1;

        // First slot allocated in this scope = first block argument
        return currentScope.AllocatedSlots.Min();
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
    /// This is the highest slot index ever allocated + 1 (to convert 0-based index to count).
    /// NOTE: We must return the HIGHEST index ever used, not max simultaneous, because:
    /// - Sequential blocks can reuse slots (reset _nextSlot on ExitScope)
    /// - But bytecode contains absolute indices emitted BEFORE slot reuse
    /// - MethodContext must have slots for the HIGHEST index in the bytecode
    /// Used in method header to allocate the MethodContext's indexed slot array.
    /// </summary>
    /// <returns>Highest slot index + 1 (number of slots needed)</returns>
    public int GetMaxFrameSize()
    {
        return _highestSlotIndex + 1;  // Convert 0-based index to count
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

    /// <summary>
    /// Gets a snapshot of all currently allocated variables and their slot indices.
    /// This is for diagnostic purposes only - returns a copy of the internal mapping.
    /// </summary>
    /// <returns>Dictionary mapping variable names to absolute slot indices</returns>
    public Dictionary<string, int> GetAllAllocatedVariables()
    {
        return new Dictionary<string, int>(_allocatedSlots);
    }
}
