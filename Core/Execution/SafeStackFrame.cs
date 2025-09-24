using Core.Abstractions.Execution;
using Core.Memory;

namespace Core.Execution;

/// <summary>
/// Thread-safe stack frame implementation with underflow protection.
/// Implements the stack management rules from Interpreter Architecture.md section 3.
/// </summary>
public sealed class SafeStackFrame : IStackFrame
{
    private readonly List<OOP> _items;
    private readonly int _maxDepth;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new stack frame with the specified maximum depth.
    /// </summary>
    /// <param name="maxDepth">Maximum stack depth (default 1000).</param>
    public SafeStackFrame(int maxDepth = 1000)
    {
        _maxDepth = maxDepth;
        _items = new List<OOP>(Math.Min(32, maxDepth)); // Start with reasonable capacity
    }

    /// <inheritdoc/>
    public int Depth 
    { 
        get 
        { 
            lock (_lock) 
                return _items.Count; 
        } 
    }

    /// <inheritdoc/>
    public bool IsEmpty 
    { 
        get 
        { 
            lock (_lock) 
                return _items.Count == 0; 
        } 
    }

    /// <inheritdoc/>
    public void Push(OOP value)
    {
        lock (_lock)
        {
            if (_items.Count >= _maxDepth)
                throw new InvalidOperationException($"Stack overflow at depth {_items.Count}");
            
            _items.Add(value);
        }
    }

    /// <inheritdoc/>
    public OOP Pop()
    {
        lock (_lock)
        {
            if (_items.Count == 0)
                throw new StackUnderflowException($"Stack underflow - attempted to pop from empty stack");
            
            var value = _items[^1]; // C# 13 index from end
            _items.RemoveAt(_items.Count - 1);
            return value;
        }
    }

    /// <inheritdoc/>
    public OOP Peek()
    {
        lock (_lock)
        {
            if (_items.Count == 0)
                throw new StackUnderflowException($"Stack underflow - attempted to peek at empty stack");
            
            return _items[^1];
        }
    }

    /// <inheritdoc/>
    public bool TryPop(out OOP value)
    {
        lock (_lock)
        {
            if (_items.Count == 0)
            {
                value = OOP.Nil;
                return false;
            }
            
            value = _items[^1];
            _items.RemoveAt(_items.Count - 1);
            return true;
        }
    }

    /// <inheritdoc/>
    public bool TryPeek(out OOP value)
    {
        lock (_lock)
        {
            if (_items.Count == 0)
            {
                value = OOP.Nil;
                return false;
            }
            
            value = _items[^1];
            return true;
        }
    }

    /// <inheritdoc/>
    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }

    /// <inheritdoc/>
    public void Dup()
    {
        lock (_lock)
        {
            if (_items.Count == 0)
                throw new StackUnderflowException("Cannot dup - stack is empty");
            
            if (_items.Count >= _maxDepth)
                throw new InvalidOperationException($"Stack overflow during dup at depth {_items.Count}");
            
            _items.Add(_items[^1]);
        }
    }

    /// <inheritdoc/>
    public OOP[] PopMultiple(int count)
    {
        if (count < 0)
            throw new ArgumentException("Count cannot be negative", nameof(count));
        
        if (count == 0)
            return [];

        lock (_lock)
        {
            if (_items.Count < count)
                throw new StackUnderflowException(
                    $"Stack underflow - attempted to pop {count} items but only {_items.Count} available");
            
            var result = new OOP[count];
            var startIndex = _items.Count - count;
            
            // Copy in reverse order (top of stack first)
            for (int i = 0; i < count; i++)
            {
                result[i] = _items[startIndex + count - 1 - i];
            }
            
            _items.RemoveRange(startIndex, count);
            return result;
        }
    }

    /// <inheritdoc/>
    public void PushMultiple(params OOP[] values)
    {
        if (values == null || values.Length == 0)
            return;

        lock (_lock)
        {
            if (_items.Count + values.Length > _maxDepth)
                throw new InvalidOperationException(
                    $"Stack overflow - pushing {values.Length} items would exceed max depth {_maxDepth}");
            
            _items.AddRange(values);
        }
    }
    
    /// <summary>
    /// Gets a copy of the current stack contents for debugging.
    /// </summary>
    /// <returns>Array of stack contents from bottom to top.</returns>
    public OOP[] ToArray()
    {
        lock (_lock)
        {
            return [.. _items]; // C# 13 collection expression
        }
    }
}