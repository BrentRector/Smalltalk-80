namespace Smalltalk80.Interpreter.Core.Values;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Smalltalk80.Interpreter.Core.Objects;

/// <summary>
/// A tagged reference that can hold either a Smalltalk object pointer or an immediate value.
/// This is the fundamental type for the VM - all object references go through this.
/// 
/// Tagging scheme (64-bit):
/// - SmallInteger: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1 (odd)
/// - Character:    xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx10 (bits 1-0 = 10) 
/// - Symbol ID:    xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx110 (bits 2-0 = 110)
/// - Object ptr:   xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx00 (even, 8-byte aligned)
/// 
/// Tagging scheme (32-bit):
/// - SmallInteger: xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx1 (odd)
/// - Character:    xxxxxxxxxxxxxxxxxxxxxxxxxxxx10 (bits 1-0 = 10)
/// - Symbol ID:    xxxxxxxxxxxxxxxxxxxxxxxxxxxx110 (bits 2-0 = 110)  
/// - Object ptr:   xxxxxxxxxxxxxxxxxxxxxxxxxxxxxx00 (even, 4-byte aligned)
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct SObjectRef : IEquatable<SObjectRef>
{
    // The tagged value - either an object pointer or immediate value
    private readonly nint value;
    
    // Tag bit patterns
    private const int SmallIntegerTag = 0b001;  // Bit 0 = 1
    private const int CharacterTag = 0b010;     // Bits 1-0 = 10
    private const int SymbolIdTag = 0b110;      // Bits 2-0 = 110
    private const int ObjectTag = 0b000;        // Bits 1-0 = 00 (aligned pointers)
    
    // Tag masks
    private const int TagMask1 = 0b001;         // Check bit 0
    private const int TagMask2 = 0b011;         // Check bits 1-0  
    private const int TagMask3 = 0b111;         // Check bits 2-0
    
    // Private constructor - use factory methods
    private SObjectRef(nint value) => this.value = value;
    
    #region Tag Checking
    
    /// <summary>
    /// Returns true if this reference is a SmallInteger.
    /// </summary>
    public bool IsSmallInteger 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask1) == SmallIntegerTag;
    }
    
    /// <summary>
    /// Returns true if this reference is a Character.
    /// </summary>
    public bool IsCharacter 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask2) == CharacterTag;
    }
    
    /// <summary>
    /// Returns true if this reference is a Symbol ID.
    /// </summary>
    public bool IsSymbolId 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask3) == SymbolIdTag;
    }
    
    /// <summary>
    /// Returns true if this reference is an object pointer.
    /// </summary>
    public bool IsObject 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask2) == ObjectTag && value != 0;
    }
    
    /// <summary>
    /// Returns true if this is nil (null pointer).
    /// </summary>
    public bool IsNil 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => value == 0;
    }
    
    /// <summary>
    /// Returns true if this is any immediate value (not an object).
    /// </summary>
    public bool IsImmediate 
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => (value & TagMask1) != 0 || value == 0;
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
        nint tagged = ((nint)val << 1) | SmallIntegerTag;
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
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool FitsInSmallInteger(long val)
    {
        // SmallInteger range depends on pointer size
        // 64-bit: 62-bit signed integer (-2^61 to 2^61-1)
        // 32-bit: 30-bit signed integer (-2^29 to 2^29-1)
        if (IntPtr.Size == 8)
        {
            const int shiftAmount64 = 62;
            long min = -(1L << (shiftAmount64 - 1));
            long max = (1L << (shiftAmount64 - 1)) - 1;
            return val >= min && val <= max;
        }
        else
        {
            const int shiftAmount32 = 30;
            long min = -(1L << (shiftAmount32 - 1));
            long max = (1L << (shiftAmount32 - 1)) - 1;
            return val >= min && val <= max;
        }
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
            
        // Add tagged values directly (both have low bit set, so result keeps it)
        nint result = value + other.value - SmallIntegerTag;  // Subtract one tag
        
        // Check for overflow by seeing if sign changed incorrectly
        if (((value ^ result) & (other.value ^ result)) < 0)
            return null;  // Overflow occurred
            
        return new SObjectRef(result);
    }
    
    /// <summary>
    /// Performs SmallInteger subtraction with overflow check.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObjectRef? SubtractSmallInteger(SObjectRef other)
    {
        if (!IsSmallInteger || !other.IsSmallInteger)
            return null;
            
        // Subtract tagged values and restore tag
        nint result = value - other.value + SmallIntegerTag;
        
        // Check for overflow
        if (((value ^ other.value) & (value ^ result)) < 0)
            return null;  // Overflow occurred
            
        return new SObjectRef(result);
    }
    
    #endregion
    
    #region Character Operations
    
    /// <summary>
    /// Creates a Character reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromCharacter(char ch)
    {
        // Shift left by 2 and set character tag
        nint tagged = ((nint)ch << 2) | CharacterTag;
        return new SObjectRef(tagged);
    }
    
    /// <summary>
    /// Extracts the character value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public char AsCharacter()
    {
        if (!IsCharacter)
            throw new InvalidOperationException("Not a Character");
        return (char)(value >> 2);
    }
    
    #endregion
    
    #region Symbol ID Operations
    
    /// <summary>
    /// Creates a Symbol ID reference (index into symbol table).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromSymbolId(int symbolId)
    {
        // Shift left by 3 and set symbol tag
        nint tagged = ((nint)symbolId << 3) | SymbolIdTag;
        return new SObjectRef(tagged);
    }
    
    /// <summary>
    /// Extracts the symbol ID.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int AsSymbolId()
    {
        if (!IsSymbolId)
            throw new InvalidOperationException("Not a Symbol ID");
        return (int)(value >> 3);
    }
    
    #endregion
    
    #region Object Operations
    
    /// <summary>
    /// Creates an object reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static SObjectRef FromObject(SObject? obj)
    {
        if (obj == null)
            return Nil;
            
        // Get the object's address as an IntPtr
        var handle = GCHandle.Alloc(obj, GCHandleType.Weak);
        nint address = GCHandle.ToIntPtr(handle);
        handle.Free();
        
        // Ensure it's properly aligned (should already be)
        if ((address & TagMask2) != 0)
            throw new InvalidOperationException("Object not properly aligned");
            
        return new SObjectRef(address);
    }
    
    /// <summary>
    /// Extracts the object reference.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SObject? AsObject()
    {
        if (IsNil)
            return null;
        if (!IsObject)
            throw new InvalidOperationException("Not an object reference");
            
        // Convert address back to object
        var handle = GCHandle.FromIntPtr(value);
        return handle.Target as SObject;
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
    
    /// <summary>
    /// Gets the Smalltalk class of this reference.
    /// </summary>
    public SClass GetClass()
    {
        if (IsNil)
            return SystemDictionary.UndefinedObjectClass;
        if (IsSmallInteger)
            return SystemDictionary.SmallIntegerClass;
        if (IsCharacter)
            return SystemDictionary.CharacterClass;
        if (IsSymbolId)
            return SystemDictionary.SymbolClass;
        
        return AsObject()?.Class ?? SystemDictionary.ObjectClass;
    }
    
    /// <summary>
    /// Returns true if this reference is of the given class or subclass.
    /// </summary>
    public bool IsKindOf(SClass cls)
    {
        var myClass = GetClass();
        while (myClass != null)
        {
            if (myClass == cls)
                return true;
            myClass = myClass.Superclass;
        }
        return false;
    }
    
    #endregion
    
    #region Equality and Hashing
    
    public override bool Equals(object? obj) => obj is SObjectRef other && Equals(other);
    
    public bool Equals(SObjectRef other) => value == other.value;
    
    public override int GetHashCode() => value.GetHashCode();
    
    public static bool operator ==(SObjectRef left, SObjectRef right) => left.value == right.value;
    
    public static bool operator !=(SObjectRef left, SObjectRef right) => left.value != right.value;
    
    /// <summary>
    /// Identity comparison (same as equality for immediates).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsIdenticalTo(SObjectRef other) => value == other.value;
    
    #endregion
    
    #region String Conversion
    
    public override string ToString()
    {
        if (IsNil) return "nil";
        if (IsSmallInteger) return AsSmallInteger().ToString();
        if (IsCharacter) return $"${AsCharacter()}";
        if (IsSymbolId) return $"#Symbol[{AsSymbolId()}]";
        if (IsObject) return AsObject()?.ToString() ?? "invalid object";
        return $"SObjectRef(0x{value:X})";
    }
    
    #endregion
}