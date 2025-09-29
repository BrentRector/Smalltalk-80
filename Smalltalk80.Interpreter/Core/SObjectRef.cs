namespace Smalltalk80.Interpreter.Core;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// Represents a reference to a Smalltalk object, which can be either:
/// - A tagged immediate value (SmallInteger or Character)
/// - A pointer to a heap-allocated object
/// 
/// Tagging scheme (on 64-bit systems):
/// - SmallInteger: odd numbers (low bit = 1), value in upper 63 bits
/// - Character: bits 0-1 = 10, character code in bits 2-9
/// - Object pointers: even addresses with bits 0-1 = 00
/// - Nil: special value 0
/// 
/// This is the fundamental type used throughout the VM for maximum performance.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SObjectRef : IEquatable<SObjectRef>
{
    // Tag bits
    private const int SmallIntegerTag = 0x1;  // ...001
    private const int CharacterTag = 0x2;     // ...010
    private const int ObjectTag = 0x0;        // ...000
    private const int TagMask = 0x3;          // Bottom 2 bits
    
    // SmallInteger range (62-bit on 64-bit systems, 30-bit on 32-bit)
    /// <summary>
    /// The minimum value for a SmallInteger.
    /// </summary>
    public static readonly long SmallIntegerMinValue = IntPtr.Size == 8 ? -(1L << 62) : -(1L << 30);
    /// <summary>
    /// The maximum value for a SmallInteger.
    /// </summary>
    public static readonly long SmallIntegerMaxValue = IntPtr.Size == 8 ? (1L << 62) - 1 : (1L << 30) - 1;
    
    // The actual value - either a tagged immediate or an object pointer
    private readonly nint value;
    
    /// <summary>
    /// Creates a reference from a raw value (used internally by the VM).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SObjectRef(nint value) => this.value = value;
    
    /// <summary>
    /// Gets whether this reference is nil.
    /// </summary>
    public bool IsNil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => value == 0;
    }
    
    /// <summary>
    /// Gets whether this reference is a SmallInteger.
    /// </summary>
    public bool IsSmallInteger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask) == SmallIntegerTag;
    }
    
    /// <summary>
    /// Gets whether this reference is a Character.
    /// </summary>
    public bool IsCharacter
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask) == CharacterTag;
    }
    
    /// <summary>
    /// Gets whether this reference is an immediate value (not a heap object).
    /// </summary>
    public bool IsImmediate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask) != ObjectTag || value == 0;
    }
    
    /// <summary>
    /// Gets whether this reference points to a heap object.
    /// </summary>
    public bool IsObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => value != 0 && (value & TagMask) == ObjectTag;
    }
    
    /// <summary>
    /// Creates a nil reference.
    /// </summary>
    public static SObjectRef Nil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(0);
    }
    
    /// <summary>
    /// Creates a reference to a SmallInteger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromSmallInteger(long intValue)
    {
        if (intValue < SmallIntegerMinValue || intValue > SmallIntegerMaxValue)
        {
            throw new OverflowException($"Value {intValue} is out of SmallInteger range");
        }
        
        // Shift left by 1 and set the tag bit
        return new((nint)((intValue << 1) | SmallIntegerTag));
    }
    
    /// <summary>
    /// Creates a reference to a Character.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromCharacter(int charCode)
    {
        if (charCode < 0 || charCode > 0xFFFF)
        {
            throw new ArgumentException($"Character code {charCode} is out of range");
        }
        
        // Shift left by 2 and set the character tag
        return new((nint)((charCode << 2) | CharacterTag));
    }
    
    /// <summary>
    /// Creates a reference to a heap object.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromObject(SObject obj)
    {
        if (obj == null)
        {
            return Nil;
        }
        
        // Get the object's address (this would use GCHandle in real implementation)
        var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
        var ptr = GCHandle.ToIntPtr(handle);
        handle.Free();
        
        // For now, we'll use a simpler approach - store object in a handle table
        var objPtr = ObjectHandleTable.AllocateHandle(obj);
        return new(objPtr);
    }
    
    /// <summary>
    /// Gets the SmallInteger value from this reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long ToSmallInteger()
    {
        if (!IsSmallInteger)
        {
            throw new InvalidOperationException("Not a SmallInteger");
        }
        
        // Arithmetic shift right to preserve sign
        return value >> 1;
    }
    
    /// <summary>
    /// Gets the Character code from this reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToCharacter()
    {
        if (!IsCharacter)
        {
            throw new InvalidOperationException("Not a Character");
        }
        
        return (int)(value >> 2) & 0xFFFF;
    }
    
    /// <summary>
    /// Gets the heap object from this reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObject ToObject()
    {
        if (IsNil)
        {
            return SNil.Instance;
        }
        
        if (!IsObject)
        {
            throw new InvalidOperationException("Not a heap object");
        }
        
        // Retrieve object from handle table
        return ObjectHandleTable.GetObject(value);
    }
    
    /// <summary>
    /// Gets the class of the referenced value.
    /// </summary>
    public SClass GetClass()
    {
        if (IsNil)
        {
            return SystemDictionary.UndefinedObjectClass;
        }
        
        if (IsSmallInteger)
        {
            return SystemDictionary.SmallIntegerClass;
        }
        
        if (IsCharacter)
        {
            return SystemDictionary.CharacterClass;
        }
        
        return ToObject().Class;
    }
    
    /// <summary>
    /// Performs a fast SmallInteger addition without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerAdd(SObjectRef a, SObjectRef b)
    {
        if (!a.IsSmallInteger || !b.IsSmallInteger)
        {
            return null;
        }
        
        // Both are tagged with bit 0 = 1, so adding them gives bit 1 = 1
        // We need to subtract 1 to maintain proper tagging
        var result = a.value + b.value - 1;
        
        // Check for overflow
        var intResult = result >> 1;
        if (intResult < SmallIntegerMinValue || intResult > SmallIntegerMaxValue)
        {
            return null; // Will overflow to LargeInteger
        }
        
        return new(result);
    }
    
    /// <summary>
    /// Performs a fast SmallInteger subtraction without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerSubtract(SObjectRef a, SObjectRef b)
    {
        if (!a.IsSmallInteger || !b.IsSmallInteger)
        {
            return null;
        }
        
        // Subtracting two tagged integers: need to add 1 to maintain tagging
        var result = a.value - b.value + 1;
        
        // Check for overflow
        var intResult = result >> 1;
        if (intResult < SmallIntegerMinValue || intResult > SmallIntegerMaxValue)
        {
            return null; // Will overflow to LargeInteger
        }
        
        return new(result);
    }
    
    /// <summary>
    /// Performs a fast SmallInteger multiplication without allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerMultiply(SObjectRef a, SObjectRef b)
    {
        if (!a.IsSmallInteger || !b.IsSmallInteger)
        {
            return null;
        }
        
        var aValue = a.ToSmallInteger();
        var bValue = b.ToSmallInteger();
        
        // Use checked arithmetic to detect overflow
        try
        {
            checked
            {
                var result = aValue * bValue;
                if (result < SmallIntegerMinValue || result > SmallIntegerMaxValue)
                {
                    return null; // Will overflow to LargeInteger
                }
                return FromSmallInteger(result);
            }
        }
        catch (OverflowException)
        {
            return null; // Will overflow to LargeInteger
        }
    }
    
    /// <summary>
    /// Performs a fast SmallInteger comparison.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FastIntegerLessThan(SObjectRef a, SObjectRef b)
    {
        if (!a.IsSmallInteger || !b.IsSmallInteger)
        {
            throw new InvalidOperationException("Both operands must be SmallIntegers");
        }
        
        // Direct comparison works because of the tagging scheme
        return a.value < b.value;
    }
    
    /// <summary>
    /// Checks for identity (same object or same immediate value).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreIdentical(SObjectRef a, SObjectRef b)
    {
        return a.value == b.value;
    }
    
    /// <summary>
    /// Determines whether this reference equals another object.
    /// </summary>
    public override bool Equals(object? obj) => obj is SObjectRef other && Equals(other);
    
    /// <summary>
    /// Determines whether this reference equals another reference.
    /// </summary>
    public bool Equals(SObjectRef other) => value == other.value;
    
    /// <summary>
    /// Gets the hash code for this reference.
    /// </summary>
    public override int GetHashCode() => value.GetHashCode();
    
    /// <summary>
    /// Determines whether two references are equal.
    /// </summary>
    public static bool operator ==(SObjectRef left, SObjectRef right) => left.value == right.value;
    
    /// <summary>
    /// Determines whether two references are not equal.
    /// </summary>
    public static bool operator !=(SObjectRef left, SObjectRef right) => left.value != right.value;
    
    /// <summary>
    /// Gets the raw value for debugging purposes.
    /// </summary>
    internal nint RawValue => value;
    
    /// <summary>
    /// Returns a string representation of this reference.
    /// </summary>
    public override string ToString()
    {
        if (IsNil) return "nil";
        if (IsSmallInteger) return $"SmallInteger({ToSmallInteger()})";
        if (IsCharacter) return $"Character('{(char)ToCharacter()}')";
        if (IsObject) return $"Object@{value:X}";
        return $"Invalid@{value:X}";
    }
}

/// <summary>
/// Temporary handle table for object references.
/// In production, this would be integrated with the GC.
/// </summary>
internal static class ObjectHandleTable
{
    private static readonly Dictionary<nint, SObject> handles = new();
    private static nint nextHandle = 0x1000; // Start at a safe offset
    
    public static nint AllocateHandle(SObject obj)
    {
        // Ensure alignment for object pointers (must be even)
        var handle = nextHandle;
        nextHandle += 8; // 8-byte aligned
        handles[handle] = obj;
        return handle;
    }
    
    public static SObject GetObject(nint handle)
    {
        if (handles.TryGetValue(handle, out var obj))
        {
            return obj;
        }
        throw new InvalidOperationException($"Invalid object handle: {handle:X}");
    }
    
    public static void FreeHandle(nint handle)
    {
        handles.Remove(handle);
    }
}