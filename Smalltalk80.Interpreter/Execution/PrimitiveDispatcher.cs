namespace Smalltalk80.Interpreter.Execution;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;

/// <summary>
/// Dispatches primitive operations (legacy static version).
/// </summary>
public static class PrimitiveDispatcher
{
    /// <summary>
    /// Executes a primitive by index.
    /// </summary>
    /// <returns>The result, or null if the primitive failed.</returns>
    public static SObject? ExecutePrimitive(int primitiveIndex, SObject receiver, SObject[] arguments)
    {
        try
        {
            return primitiveIndex switch
            {
                // SmallInteger arithmetic
                1 => IntegerAdd(receiver, arguments),
                2 => IntegerSubtract(receiver, arguments),
                3 => IntegerLessThan(receiver, arguments),
                4 => IntegerGreaterThan(receiver, arguments),
                5 => IntegerLessOrEqual(receiver, arguments),
                6 => IntegerGreaterOrEqual(receiver, arguments),
                7 => IntegerEqual(receiver, arguments),
                8 => IntegerNotEqual(receiver, arguments),
                9 => IntegerMultiply(receiver, arguments),
                10 => IntegerDivide(receiver, arguments),
                11 => IntegerMod(receiver, arguments),
                12 => IntegerDiv(receiver, arguments),
                13 => IntegerQuo(receiver, arguments),
                14 => IntegerBitAnd(receiver, arguments),
                15 => IntegerBitOr(receiver, arguments),
                16 => IntegerBitXor(receiver, arguments),
                17 => IntegerBitShift(receiver, arguments),
                
                // Float primitives
                40 => IntegerAsFloat(receiver, arguments),
                41 => FloatAdd(receiver, arguments),
                42 => FloatSubtract(receiver, arguments),
                43 => FloatLessThan(receiver, arguments),
                44 => FloatGreaterThan(receiver, arguments),
                45 => FloatLessOrEqual(receiver, arguments),
                46 => FloatGreaterOrEqual(receiver, arguments),
                47 => FloatEqual(receiver, arguments),
                48 => FloatNotEqual(receiver, arguments),
                49 => FloatMultiply(receiver, arguments),
                50 => FloatDivide(receiver, arguments),
                51 => FloatTruncate(receiver, arguments),
                52 => FloatFractionPart(receiver, arguments),
                53 => FloatExponent(receiver, arguments),
                
                // Array/String access
                60 => ArrayAt(receiver, arguments),
                61 => ArrayAtPut(receiver, arguments),
                62 => ArraySize(receiver, arguments),
                63 => StringAt(receiver, arguments),
                64 => StringAtPut(receiver, arguments),
                65 => StringSize(receiver, arguments),
                
                // Object access
                68 => ObjectAt(receiver, arguments),
                69 => ObjectAtPut(receiver, arguments),
                70 => ObjectSize(receiver, arguments),
                71 => ObjectClass(receiver, arguments),
                
                // Object comparison
                110 => ObjectIdentical(receiver, arguments),
                111 => ObjectNotIdentical(receiver, arguments),
                
                _ => null // Unknown primitive
            };
        }
        catch
        {
            // Primitive failed
            return null;
        }
    }
    
    // Integer arithmetic primitives
    private static SObject? IntegerAdd(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a.Add(b);
        }
        return null;
    }
    
    private static SObject? IntegerSubtract(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a.Subtract(b);
        }
        return null;
    }
    
    private static SObject? IntegerLessThan(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a < b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerGreaterThan(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a > b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerLessOrEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a <= b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerGreaterOrEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a >= b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a == b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerNotEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a != b ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? IntegerMultiply(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return a.Multiply(b);
        }
        return null;
    }
    
    private static SObject? IntegerDivide(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            if (b.Value == 0) return null; // Division by zero
            return a.Divide(b);
        }
        return null;
    }
    
    private static SObject? IntegerMod(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            if (b.Value == 0) return null; // Division by zero
            return a.Modulo(b);
        }
        return null;
    }
    
    private static SObject? IntegerDiv(SObject receiver, SObject[] args)
    {
        return IntegerDivide(receiver, args); // Same as divide for integers
    }
    
    private static SObject? IntegerQuo(SObject receiver, SObject[] args)
    {
        return IntegerDivide(receiver, args); // Same as divide for integers
    }
    
    private static SObject? IntegerBitAnd(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return SmallInteger.From(a.Value & b.Value);
        }
        return null;
    }
    
    private static SObject? IntegerBitOr(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return SmallInteger.From(a.Value | b.Value);
        }
        return null;
    }
    
    private static SObject? IntegerBitXor(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            return SmallInteger.From(a.Value ^ b.Value);
        }
        return null;
    }
    
    private static SObject? IntegerBitShift(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger a && args.Length > 0 && args[0] is SmallInteger b)
        {
            var shift = b.Value;
            if (shift > 0)
            {
                return SmallInteger.From(a.Value << shift);
            }
            else
            {
                return SmallInteger.From(a.Value >> -shift);
            }
        }
        return null;
    }
    
    // Float primitives
    private static SObject? IntegerAsFloat(SObject receiver, SObject[] args)
    {
        if (receiver is SmallInteger integer)
        {
            return SFloat.FromInteger(integer);
        }
        return null;
    }
    
    private static SObject? FloatAdd(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.Add(args[0]);
        }
        return null;
    }
    
    private static SObject? FloatSubtract(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.Subtract(args[0]);
        }
        return null;
    }
    
    private static SObject? FloatLessThan(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.CompareTo(args[0]) < 0 ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatGreaterThan(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.CompareTo(args[0]) > 0 ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatLessOrEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.CompareTo(args[0]) <= 0 ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatGreaterOrEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.CompareTo(args[0]) >= 0 ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.Equals(args[0]) ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatNotEqual(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return !f.Equals(args[0]) ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? FloatMultiply(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            return f.Multiply(args[0]);
        }
        return null;
    }
    
    private static SObject? FloatDivide(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f && args.Length > 0)
        {
            try
            {
                return f.Divide(args[0]);
            }
            catch (DivideByZeroException)
            {
                return null; // Primitive failure
            }
        }
        return null;
    }
    
    private static SObject? FloatTruncate(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f)
        {
            try
            {
                return f.Truncate();
            }
            catch (OverflowException)
            {
                return null; // Primitive failure
            }
        }
        return null;
    }
    
    private static SObject? FloatFractionPart(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f)
        {
            return f.FractionPart();
        }
        return null;
    }
    
    private static SObject? FloatExponent(SObject receiver, SObject[] args)
    {
        if (receiver is SFloat f)
        {
            return f.Exponent();
        }
        return null;
    }
    
    // Array primitives
    private static SObject? ArrayAt(SObject receiver, SObject[] args)
    {
        if (receiver is SArray array && args.Length > 0 && args[0] is SmallInteger index)
        {
            return array.At(index.Value) ?? SNil.Instance;
        }
        return null;
    }
    
    private static SObject? ArrayAtPut(SObject receiver, SObject[] args)
    {
        if (receiver is SArray array && args.Length >= 2 && args[0] is SmallInteger index)
        {
            array.AtPut(index.Value, args[1]);
            return args[1];
        }
        return null;
    }
    
    private static SObject? ArraySize(SObject receiver, SObject[] args)
    {
        if (receiver is SArray array)
        {
            return SmallInteger.From(array.Size);
        }
        return null;
    }
    
    // String primitives
    private static SObject? StringAt(SObject receiver, SObject[] args)
    {
        if (receiver is SString str && args.Length > 0 && args[0] is SmallInteger index)
        {
            return str.At(index.Value);
        }
        return null;
    }
    
    private static SObject? StringAtPut(SObject receiver, SObject[] args)
    {
        // Strings are immutable in our implementation
        return null;
    }
    
    private static SObject? StringSize(SObject receiver, SObject[] args)
    {
        if (receiver is SString str)
        {
            return SmallInteger.From(str.Length);
        }
        return null;
    }
    
    // Object primitives
    private static SObject? ObjectAt(SObject receiver, SObject[] args)
    {
        if (receiver is SInstance instance && args.Length > 0 && args[0] is SmallInteger index)
        {
            return instance.GetInstanceVariable(index.Value - 1) ?? SNil.Instance;
        }
        return null;
    }
    
    private static SObject? ObjectAtPut(SObject receiver, SObject[] args)
    {
        if (receiver is SInstance instance && args.Length >= 2 && args[0] is SmallInteger index)
        {
            instance.SetInstanceVariable(index.Value - 1, args[1]);
            return args[1];
        }
        return null;
    }
    
    private static SObject? ObjectSize(SObject receiver, SObject[] args)
    {
        if (receiver is SInstance instance)
        {
            return SmallInteger.From(instance.Class.TotalInstanceVariableCount);
        }
        return null;
    }
    
    private static SObject? ObjectClass(SObject receiver, SObject[] args)
    {
        return receiver.Class;
    }
    
    // Object comparison primitives
    private static SObject? ObjectIdentical(SObject receiver, SObject[] args)
    {
        if (args.Length > 0)
        {
            return ReferenceEquals(receiver, args[0]) ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    private static SObject? ObjectNotIdentical(SObject receiver, SObject[] args)
    {
        if (args.Length > 0)
        {
            return !ReferenceEquals(receiver, args[0]) ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
}