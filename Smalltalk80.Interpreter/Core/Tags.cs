using System;
using System.Runtime.CompilerServices;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Helper class for tagged immediate values.
    /// SmallIntegers are tagged with LSB = 1 to avoid heap allocation.
    /// Object references have LSB = 0.
    /// Fast and safe across 32/64-bit runtimes.
    /// </summary>
    public static class Tags
    {
        /// <summary>
        /// Tag mask - LSB set for immediates.
        /// Low bit 1 => immediate SmallInteger; low bit 0 => pointer (managed object)
        /// </summary>
        private const long TAG_MASK = 1L;

        /// <summary>
        /// Minimum SmallInteger value that can be tagged.
        /// </summary>
        public const long MinSmallInteger = long.MinValue >> 1;

        /// <summary>
        /// Maximum SmallInteger value that can be tagged.
        /// </summary>
        public const long MaxSmallInteger = long.MaxValue >> 1;

        /// <summary>
        /// Creates a tagged SmallInteger from a value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object FromSmallInteger(long value)
        {
            return (long)((value << 1) | TAG_MASK);
        }

        /// <summary>
        /// Extracts the SmallInteger value from a tagged immediate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long ToSmallInteger(object tagged)
        {
            var lv = (long)tagged;
            return (long)((long)lv >> 1);
        }

        /// <summary>
        /// Tries to unbox a SmallInteger for hot interpreter paths.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool TryUnboxSmallInteger(object v, out long value)
        {
            if (v is long lv && (lv & TAG_MASK) == TAG_MASK)
            {
                value = lv >> 1;
                return true;
            }
            value = 0;
            return false;
        }

        /// <summary>
        /// Boxes a value as either a SmallInteger or object based on flag.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object BoxMaybeSmallInteger(long valueOrObject, bool isImmediate)
        {
            return isImmediate ? FromSmallInteger(valueOrObject) : (object)valueOrObject;
        }

        /// <summary>
        /// Tags an integer value as a SmallInteger immediate with range checking.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object TagInt(long value)
        {
            if (value < MinSmallInteger || value > MaxSmallInteger)
                throw new OverflowException($"Value {value} is out of SmallInteger range");
            
            return FromSmallInteger(value);
        }

        /// <summary>
        /// Tags an integer value without range checking (for performance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object TagIntUnchecked(long value)
        {
            return FromSmallInteger(value);
        }

        /// <summary>
        /// Untags a SmallInteger immediate to get its value.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UntagInt(object tagged)
        {
            if (!IsImmediate(tagged))
                throw new ArgumentException("Object is not a tagged immediate");
            
            return ToSmallInteger(tagged);
        }

        /// <summary>
        /// Untags a SmallInteger immediate without checking (for performance).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long UntagIntUnchecked(object tagged)
        {
            return ToSmallInteger(tagged);
        }

        /// <summary>
        /// Checks if a value is a tagged immediate.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsImmediate(object v)
        {
            return v is long lv && (lv & TAG_MASK) == TAG_MASK;
        }

        /// <summary>
        /// Checks if a value is a SmallInteger.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsSmallInteger(object value)
        {
            return IsImmediate(value);
        }

        /// <summary>
        /// Checks if a value is an object reference (not immediate).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsObject(object value)
        {
            return !IsImmediate(value) && value != null;
        }

        /// <summary>
        /// Gets the class of a value, handling both immediates and objects.
        /// </summary>
        public static SClass ClassOf(object value, Kernel kernel)
        {
            if (value == null || value == Nil.Instance)
                return kernel.UndefinedObjectClass;
            
            if (value == True.Instance)
                return kernel.TrueClass;
            
            if (value == False.Instance)
                return kernel.FalseClass;
            
            if (IsSmallInteger(value))
                return kernel.SmallIntegerClass;
            
            if (value is SObject sobj)
                return sobj.Klass;
            
            throw new ArgumentException($"Unknown value type: {value.GetType()}");
        }

        /// <summary>
        /// Performs SmallInteger addition with overflow checking.
        /// Returns null if overflow would occur.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object AddSmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                return null;
            
            long a = UntagIntUnchecked(left);
            long b = UntagIntUnchecked(right);
            
            try
            {
                checked
                {
                    long result = a + b;
                    if (result >= MinSmallInteger && result <= MaxSmallInteger)
                        return TagIntUnchecked(result);
                }
            }
            catch (OverflowException)
            {
            }
            
            return null; // Overflow - need to create LargeInteger
        }

        /// <summary>
        /// Performs SmallInteger subtraction with overflow checking.
        /// Returns null if overflow would occur.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object SubtractSmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                return null;
            
            long a = UntagIntUnchecked(left);
            long b = UntagIntUnchecked(right);
            
            try
            {
                checked
                {
                    long result = a - b;
                    if (result >= MinSmallInteger && result <= MaxSmallInteger)
                        return TagIntUnchecked(result);
                }
            }
            catch (OverflowException)
            {
            }
            
            return null; // Overflow - need to create LargeInteger
        }

        /// <summary>
        /// Performs SmallInteger multiplication with overflow checking.
        /// Returns null if overflow would occur.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object MultiplySmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                return null;
            
            long a = UntagIntUnchecked(left);
            long b = UntagIntUnchecked(right);
            
            try
            {
                checked
                {
                    long result = a * b;
                    if (result >= MinSmallInteger && result <= MaxSmallInteger)
                        return TagIntUnchecked(result);
                }
            }
            catch (OverflowException)
            {
            }
            
            return null; // Overflow - need to create LargeInteger
        }

        /// <summary>
        /// Performs SmallInteger division.
        /// Returns null if result is not an integer or division by zero.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object DivideSmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                return null;
            
            long a = UntagIntUnchecked(left);
            long b = UntagIntUnchecked(right);
            
            if (b == 0)
                return null; // Division by zero
            
            if (a % b != 0)
                return null; // Result is not an integer - need Float
            
            long result = a / b;
            if (result >= MinSmallInteger && result <= MaxSmallInteger)
                return TagIntUnchecked(result);
            
            return null; // Overflow - need to create LargeInteger
        }

        /// <summary>
        /// Performs SmallInteger modulo.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static object ModSmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                return null;
            
            long a = UntagIntUnchecked(left);
            long b = UntagIntUnchecked(right);
            
            if (b == 0)
                return null; // Division by zero
            
            long result = a % b;
            return TagIntUnchecked(result);
        }

        /// <summary>
        /// Compares two SmallIntegers.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareSmallIntegers(object left, object right)
        {
            if (!IsSmallInteger(left) || !IsSmallInteger(right))
                throw new ArgumentException("Both arguments must be SmallIntegers");
            
            // Can compare tagged values directly since they preserve ordering
            long a = (long)left;
            long b = (long)right;
            return a.CompareTo(b);
        }

        /// <summary>
        /// Checks if two values are identical (same object or same immediate).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool AreIdentical(object left, object right)
        {
            // For immediates, compare the tagged values
            if (IsImmediate(left) && IsImmediate(right))
                return (long)left == (long)right;
            
            // For objects, compare references
            return ReferenceEquals(left, right);
        }
    }
}