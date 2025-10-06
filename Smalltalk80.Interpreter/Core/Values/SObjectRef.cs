namespace Smalltalk80.Interpreter.Core.Values;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Memory;

/// <summary>
/// A tagged reference that can hold either a Smalltalk object pointer or a SmallInteger immediate value.
/// This is the fundamental type for the VM - all object references go through this.
///
/// STRICT BLUE BOOK COMPLIANCE:
/// Per Smalltalk-80 specification, only SmallInteger is an immediate value.
/// Character, Symbol, and all other types are heap-allocated objects.
///
/// Tagging scheme (32-bit, Blue Book compliant):
/// - SmallInteger: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1 (LSB = 1, 31-bit signed value)
/// - Object Table Index: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx0 (LSB = 0, 31-bit index)
///
/// SmallInteger range: -2^30 to 2^30-1 (31-bit signed)
/// Object Table capacity: 2^31 entries (2,147,483,648 objects)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SObjectRef : IEquatable<SObjectRef>
{
    // The tagged 32-bit value - either an object table index or SmallInteger immediate value
    private readonly int value;

    // Tag bit pattern (Blue Book: only SmallInteger is immediate)
    private const int SmallIntegerTag = 0b1;    // Bit 0 = 1
    private const int ObjectTag = 0b0;          // Bit 0 = 0 (aligned pointers)

    // Tag mask
    private const int TagMask = 0b1;            // Check bit 0 only

    // Cached nil OOP value (set once during cold start, 0 before that)
    private static int _cachedNilOop = 0;

    // Private constructor - use factory methods
    private SObjectRef(int value) => this.value = value;
    
    #region Tag Checking

    /// <summary>
    /// Returns true if this reference is a SmallInteger (LSB = 1).
    /// </summary>
    public bool IsSmallInteger
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask) == SmallIntegerTag;
    }

    /// <summary>
    /// Returns true if this reference is an object pointer (LSB = 0, non-null).
    /// </summary>
    public bool IsObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask) == ObjectTag && value != 0;
    }

    /// <summary>
    /// Returns true if this reference points to nil.
    /// During early cold start (before nil singleton is created): checks if value == 0 (temporary marker).
    /// After cold start: checks if value matches the actual nil singleton OOP.
    /// </summary>
    public bool IsNil
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            // Before nil is created: check for temporary 0 marker
            // After nil is created: check for actual nil OOP
            // IMPORTANT: Use raw 'value' field, NOT AsObjectPointer() to avoid infinite recursion!
            return value == 0 || (_cachedNilOop != 0 && value == _cachedNilOop);
        }
    }

    /// <summary>
    /// Sets the cached nil OOP value. Called once during cold start.
    /// INTERNAL USE ONLY - called by SpecialObjectsArray.Initialize().
    /// </summary>
    internal static void SetNilOop(int nilOop)
    {
        _cachedNilOop = nilOop;
    }

    /// <summary>
    /// Returns true if this reference is a heap object (object pointer including nil).
    /// </summary>
    public bool IsHeapObject
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => !IsSmallInteger;
    }

    /// <summary>
    /// Returns true if this is an immediate value (only SmallInteger in Blue Book).
    /// </summary>
    public bool IsImmediate
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => IsSmallInteger;
    }

    #endregion
    
    #region SmallInteger Operations
    
    /// <summary>
    /// Creates a SmallInteger reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromSmallInteger(int val)
    {
        // Shift left by 1 and set low bit
        int tagged = ((int)val << 1) | SmallIntegerTag;
        return new SObjectRef(tagged);
    }
    
    /// <summary>
    /// Extracts the integer value from a SmallInteger reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AsSmallInteger()
    {
        if (!IsSmallInteger)
            throw new InvalidOperationException("Not a SmallInteger");
        // Arithmetic shift right to preserve sign
        return (int)(value >> 1);
    }
    
    /// <summary>
    /// Checks if an integer value fits in a SmallInteger.
    /// Blue Book: 31-bit signed integer range (-2^30 to 2^30-1).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FitsInSmallInteger(long val)
    {
        // Blue Book: 31-bit signed integer (one bit used for tag)
        // Range: -1,073,741,824 to 1,073,741,823
        const long minValue = -(1L << 30);  // -2^30
        const long maxValue = (1L << 30) - 1;  // 2^30 - 1
        return val >= minValue && val <= maxValue;
    }
    
    /// <summary>
    /// Performs SmallInteger addition with overflow check.
    /// Returns null if overflow would occur.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? AddSmallInteger(SObjectRef other)
    {
        if (!IsSmallInteger || !other.IsSmallInteger)
            return null;

        // Extract the actual integer values
        int a = AsSmallInteger();
        int b = other.AsSmallInteger();

        // Use long to detect overflow (SmallInteger range is -2^30 to 2^30-1)
        long result = (long)a + (long)b;

        // Check if result is within SmallInteger range
        const int MaxSmallInteger = 1073741823;   // 2^30 - 1
        const int MinSmallInteger = -1073741824;  // -2^30

        if (result > MaxSmallInteger || result < MinSmallInteger)
            return null;  // Overflow occurred

        return FromSmallInteger((int)result);
    }
    
    /// <summary>
    /// Performs SmallInteger subtraction with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? SubtractSmallInteger(SObjectRef other)
    {
        if (!IsSmallInteger || !other.IsSmallInteger)
            return null;

        // Extract the actual integer values
        int a = AsSmallInteger();
        int b = other.AsSmallInteger();

        // Use long to detect overflow (SmallInteger range is -2^30 to 2^30-1)
        long result = (long)a - (long)b;

        // Check if result is within SmallInteger range
        const int MaxSmallInteger = 1073741823;   // 2^30 - 1
        const int MinSmallInteger = -1073741824;  // -2^30

        if (result > MaxSmallInteger || result < MinSmallInteger)
            return null;  // Overflow occurred

        return FromSmallInteger((int)result);
    }

    /// <summary>
    /// Fast integer addition for interpreter optimization (instance method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? FastIntegerAdd(SObjectRef other) => AddSmallInteger(other);

    /// <summary>
    /// Fast integer subtraction for interpreter optimization (instance method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? FastIntegerSubtract(SObjectRef other) => SubtractSmallInteger(other);

    /// <summary>
    /// Fast integer multiplication for interpreter optimization (instance method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? FastIntegerMultiply(SObjectRef other)
    {
        if (!IsSmallInteger || !other.IsSmallInteger)
            return null;

        // Extract the actual values
        int a = AsSmallInteger();
        int b = other.AsSmallInteger();

        // Check for overflow using long multiplication
        long result = (long)a * (long)b;

        if (!FitsInSmallInteger(result))
            return null;  // Overflow occurred

        return FromSmallInteger((int)result);
    }

    /// <summary>
    /// Fast integer less-than comparison for interpreter optimization (instance method).
    /// Returns false if either operand is not a SmallInteger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool FastIntegerLessThan(SObjectRef other)
    {
        if (!IsSmallInteger || !other.IsSmallInteger)
            return false;

        return AsSmallInteger() < other.AsSmallInteger();
    }

    /// <summary>
    /// Fast integer addition for interpreter optimization (static method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerAdd(SObjectRef left, SObjectRef right)
    {
        return left.AddSmallInteger(right);
    }

    /// <summary>
    /// Fast integer subtraction for interpreter optimization (static method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerSubtract(SObjectRef left, SObjectRef right)
    {
        return left.SubtractSmallInteger(right);
    }

    /// <summary>
    /// Fast integer multiplication for interpreter optimization (static method).
    /// Returns null if either operand is not a SmallInteger or if overflow occurs.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef? FastIntegerMultiply(SObjectRef left, SObjectRef right)
    {
        return left.FastIntegerMultiply(right);
    }

    /// <summary>
    /// Fast integer less-than comparison for interpreter optimization.
    /// Returns false if either operand is not a SmallInteger.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FastIntegerLessThan(SObjectRef left, SObjectRef right)
    {
        return left.FastIntegerLessThan(right);
    }

    /// <summary>
    /// Checks if two object references are identical (same object).
    /// This is identity comparison, not equality.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AreIdentical(SObjectRef left, SObjectRef right)
    {
        return left.value == right.value;
    }

    #endregion
    
    #region Object Operations

    /// <summary>
    /// Creates an object reference from an OOP (Object-Oriented Pointer).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromObjectPointer(int oop)
    {
        if (oop == 0)
            return Nil;

        // Verify alignment for safety
        if ((oop & TagMask) != 0)
            throw new InvalidOperationException($"Object pointer not 4-byte aligned: 0x{oop:X}");

        return new SObjectRef(oop);
    }

    /// <summary>
    /// Gets the raw OOP (Object-Oriented Pointer) from this reference.
    /// Returns the underlying pointer value for heap objects (including nil).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AsObjectPointer()
    {
        if (!IsObject && !IsNil)
            throw new InvalidOperationException("Not an object reference (might be SmallInteger)");

        return value;
    }

    /// <summary>
    /// Gets the SObject instance for this reference.
    /// Only works for bootstrap objects created during cold start.
    /// This is a compatibility method for the mixed SObject/SObjectRef architecture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObject? AsObject()
    {
        if (IsNil)
            return null;

        if (!IsObject)
            throw new InvalidOperationException("Not an object reference (might be SmallInteger)");

        // Use ObjectMemory to retrieve the object by its OOP
        return ObjectMemory.GetObject(value);
    }

    /// <summary>
    /// Creates an SObjectRef from an SObject instance.
    /// Only works for bootstrap objects created during cold start.
    /// This is a compatibility method for the mixed SObject/SObjectRef architecture.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromObject(SObject? obj)
    {
        if (obj == null)
            return Nil;

        // Use ObjectMemory to get the object's OOP
        int oop = ObjectMemory.GetOop(obj);

        if ((oop & TagMask) != 0)
            throw new InvalidOperationException($"Object pointer not 4-byte aligned: 0x{oop:X}");

        return new SObjectRef(oop);
    }

    #endregion

    #region Object Access Methods (NEW ARCHITECTURE)

    /// <summary>
    /// Gets the class of this object.
    /// For SmallInteger and nil, returns the appropriate class.
    /// For heap objects, delegates to ObjectMemory.
    /// </summary>
    public SObjectRef GetClassRef()
    {
        if (IsNil)
            return SystemDictionary.UndefinedObjectClassRef;

        if (IsSmallInteger)
            return SystemDictionary.SmallIntegerClassRef;

        // Heap object - get from ObjectMemory
        return ObjectMemory.GetClass(value);
    }

    /// <summary>
    /// Gets the format of this object.
    /// Only valid for heap objects.
    /// </summary>
    public ObjectFormat GetFormat()
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have a format");

        return ObjectMemory.GetFormat(value);
    }

    /// <summary>
    /// Gets an instance variable by index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef GetInstanceVariable(int index)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have instance variables");

        return ObjectMemory.GetInstanceVariable(value, index);
    }

    /// <summary>
    /// Sets an instance variable by index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetInstanceVariable(int index, SObjectRef refValue)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have instance variables");

        ObjectMemory.SetInstanceVariable(value, index, refValue);
    }

    /// <summary>
    /// Gets the number of instance variables.
    /// </summary>
    public int GetInstanceVariableCount()
    {
        if (!IsHeapObject)
            return 0;

        return ObjectMemory.GetInstanceVariableCount(value);
    }

    /// <summary>
    /// Gets an indexed byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte GetIndexedByte(int index)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        return ObjectMemory.GetIndexedByte(value, index);
    }

    /// <summary>
    /// Sets an indexed byte.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIndexedByte(int index, byte byteValue)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        ObjectMemory.SetIndexedByte(value, index, byteValue);
    }

    /// <summary>
    /// Gets an indexed word.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ushort GetIndexedWord(int index)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        return ObjectMemory.GetIndexedWord(value, index);
    }

    /// <summary>
    /// Sets an indexed word.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIndexedWord(int index, ushort wordValue)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        ObjectMemory.SetIndexedWord(value, index, wordValue);
    }

    /// <summary>
    /// Gets an indexed pointer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef GetIndexedPointer(int index)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        return ObjectMemory.GetIndexedPointer(value, index);
    }

    /// <summary>
    /// Sets an indexed pointer.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetIndexedPointer(int index, SObjectRef refValue)
    {
        if (!IsHeapObject)
            throw new InvalidOperationException("Only heap objects have indexed data");

        ObjectMemory.SetIndexedPointer(value, index, refValue);
    }

    /// <summary>
    /// Gets the size of the indexed portion.
    /// </summary>
    public int GetIndexedSize()
    {
        if (!IsHeapObject)
            return 0;

        return ObjectMemory.GetIndexedSize(value);
    }

    #endregion
    
    #region Common Values
    
    /// <summary>
    /// The nil reference (null).
    /// </summary>
    public static readonly SObjectRef Nil = new(0);
    
    /// <summary>
    /// SmallInteger zero.
    /// </summary>
    public static readonly SObjectRef Zero = FromSmallInteger(0);
    
    /// <summary>
    /// SmallInteger one.
    /// </summary>
    public static readonly SObjectRef One = FromSmallInteger(1);
    
    /// <summary>
    /// SmallInteger minus one.
    /// </summary>
    public static readonly SObjectRef MinusOne = FromSmallInteger(-1);
    
    #endregion
    
    #region Type Resolution

    // TODO: Add GetClass() and IsKindOf() methods here when needed

    #endregion
    
    #region Equality and Hashing
    
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
    /// Identity comparison (same as equality for immediates).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsIdenticalTo(SObjectRef other) => value == other.value;
    
    #endregion
    
    #region String Conversion
    
    /// <summary>
    /// Returns a string representation of this reference.
    /// </summary>
    public override string ToString()
    {
        if (IsNil)
            return "nil";

        if (IsSmallInteger)
            return AsSmallInteger().ToString();

        if (IsObject)
            return AsObject()?.ToString() ?? "invalid object";

        return $"SObjectRef(0x{value:X})";
    }
    
    #endregion
}