namespace Smalltalk80.Interpreter.Primitives;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;

/// <summary>
/// Base class for primitive dispatchers.
/// </summary>
public abstract class PrimitiveDispatcherBase
{
    /// <summary>
    /// The interpreter instance for executing bytecode.
    /// </summary>
    protected readonly ContextBasedInterpreter interpreter;
    
    /// <summary>
    /// Initializes a new instance of the PrimitiveDispatcherBase.
    /// </summary>
    protected PrimitiveDispatcherBase(ContextBasedInterpreter interpreter)
    {
        this.interpreter = interpreter;
    }
    
    /// <summary>
    /// Executes a primitive by index.
    /// </summary>
    /// <param name="primitiveIndex">The primitive index.</param>
    /// <param name="receiver">The receiver of the primitive message.</param>
    /// <param name="arguments">The arguments to the primitive.</param>
    /// <returns>The result of the primitive, or null if it failed.</returns>
    public abstract SObject? ExecutePrimitive(int primitiveIndex, SObject receiver, SObject[] arguments);
    
    #region Common Integer Primitives
    
    /// <summary>
    /// Integer addition primitive.
    /// </summary>
    protected SObject? IntegerAdd(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            var result = left.Value + right.Value;
            if (result >= SmallInteger.MinValue && result <= SmallInteger.MaxValue)
            {
                return SObjectRef.FromSmallInteger(result).AsObject();
            }
            // Overflow - would return LargeInteger
        }
        return null;
    }
    
    /// <summary>
    /// Integer subtraction primitive.
    /// </summary>
    protected SObject? IntegerSubtract(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            var result = left.Value - right.Value;
            if (result >= SmallInteger.MinValue && result <= SmallInteger.MaxValue)
            {
                return SObjectRef.FromSmallInteger(result).AsObject();
            }
            // Overflow - would return LargeInteger
        }
        return null;
    }
    
    /// <summary>
    /// Integer less than primitive.
    /// </summary>
    protected SObject? IntegerLessThan(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value < right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer greater than primitive.
    /// </summary>
    protected SObject? IntegerGreaterThan(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value > right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer less or equal primitive.
    /// </summary>
    protected SObject? IntegerLessOrEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value <= right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer greater or equal primitive.
    /// </summary>
    protected SObject? IntegerGreaterOrEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value >= right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer equality primitive.
    /// </summary>
    protected SObject? IntegerEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value == right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer inequality primitive.
    /// </summary>
    protected SObject? IntegerNotEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return left.Value != right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Integer multiplication primitive.
    /// </summary>
    protected SObject? IntegerMultiply(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            var result = left.Value * right.Value;
            if (result >= SmallInteger.MinValue && result <= SmallInteger.MaxValue)
            {
                return SObjectRef.FromSmallInteger(result).AsObject();
            }
            // Overflow - would return LargeInteger
        }
        return null;
    }
    
    /// <summary>
    /// Integer division primitive.
    /// </summary>
    protected SObject? IntegerDivide(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            if (right.Value == 0) return null; // Division by zero
            
            // Smalltalk division rounds toward negative infinity
            var result = Math.Floor((double)left.Value / right.Value);
            if (result >= SmallInteger.MinValue && result <= SmallInteger.MaxValue)
            {
                return SObjectRef.FromSmallInteger((int)result).AsObject();
            }
        }
        return null;
    }
    
    /// <summary>
    /// Integer modulo primitive.
    /// </summary>
    protected SObject? IntegerMod(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            if (right.Value == 0) return null; // Division by zero
            
            // Smalltalk modulo always returns positive result
            var result = left.Value % right.Value;
            if (result < 0)
            {
                result += Math.Abs(right.Value);
            }
            return SObjectRef.FromSmallInteger(result).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer div primitive (integer division).
    /// </summary>
    protected SObject? IntegerDiv(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            if (right.Value == 0) return null; // Division by zero
            
            var result = left.Value / right.Value;
            return SObjectRef.FromSmallInteger(result).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer quo primitive (truncated division).
    /// </summary>
    protected SObject? IntegerQuo(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            if (right.Value == 0) return null; // Division by zero
            
            var result = Math.Truncate((double)left.Value / right.Value);
            return SObjectRef.FromSmallInteger((int)result).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer bitwise AND primitive.
    /// </summary>
    protected SObject? IntegerBitAnd(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return SObjectRef.FromSmallInteger(left.Value & right.Value).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer bitwise OR primitive.
    /// </summary>
    protected SObject? IntegerBitOr(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return SObjectRef.FromSmallInteger(left.Value | right.Value).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer bitwise XOR primitive.
    /// </summary>
    protected SObject? IntegerBitXor(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger left && arguments[0] is SmallInteger right)
        {
            return SObjectRef.FromSmallInteger(left.Value ^ right.Value).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Integer bit shift primitive.
    /// </summary>
    protected SObject? IntegerBitShift(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SmallInteger value && arguments[0] is SmallInteger shift)
        {
            if (shift.Value >= 0)
            {
                return SObjectRef.FromSmallInteger(value.Value << (int)shift.Value).AsObject();
            }
            else
            {
                return SObjectRef.FromSmallInteger(value.Value >> (int)(-shift.Value)).AsObject();
            }
        }
        return null;
    }
    
    #endregion
    
    #region Common Float Primitives
    
    /// <summary>
    /// Integer as float primitive.
    /// </summary>
    protected SObject? IntegerAsFloat(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 0) return null;
        
        if (receiver is SmallInteger intValue)
        {
            return new SFloat((double)intValue.Value);
        }
        return null;
    }
    
    /// <summary>
    /// Float addition primitive.
    /// </summary>
    protected SObject? FloatAdd(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return new SFloat(left.Value + right.Value);
        }
        return null;
    }
    
    /// <summary>
    /// Float subtraction primitive.
    /// </summary>
    protected SObject? FloatSubtract(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return new SFloat(left.Value - right.Value);
        }
        return null;
    }
    
    /// <summary>
    /// Float less than primitive.
    /// </summary>
    protected SObject? FloatLessThan(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return left.Value < right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float greater than primitive.
    /// </summary>
    protected SObject? FloatGreaterThan(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return left.Value > right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float less or equal primitive.
    /// </summary>
    protected SObject? FloatLessOrEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return left.Value <= right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float greater or equal primitive.
    /// </summary>
    protected SObject? FloatGreaterOrEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return left.Value >= right.Value ? STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float equality primitive.
    /// </summary>
    protected SObject? FloatEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return Math.Abs(left.Value - right.Value) < double.Epsilon ? 
                STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float inequality primitive.
    /// </summary>
    protected SObject? FloatNotEqual(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return Math.Abs(left.Value - right.Value) >= double.Epsilon ? 
                STrue.Instance : SFalse.Instance;
        }
        return null;
    }
    
    /// <summary>
    /// Float multiplication primitive.
    /// </summary>
    protected SObject? FloatMultiply(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            return new SFloat(left.Value * right.Value);
        }
        return null;
    }
    
    /// <summary>
    /// Float division primitive.
    /// </summary>
    protected SObject? FloatDivide(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SFloat left && arguments[0] is SFloat right)
        {
            if (Math.Abs(right.Value) < double.Epsilon) return null; // Division by zero
            
            return new SFloat(left.Value / right.Value);
        }
        return null;
    }
    
    #endregion
    
    #region Common Array/String Primitives
    
    /// <summary>
    /// Array at: primitive.
    /// </summary>
    protected SObject? ArrayAt(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SArray array && arguments[0] is SmallInteger index)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            if (idx >= 0 && idx < array.Size)
            {
                return array.At(idx) ;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Array at:put: primitive.
    /// </summary>
    protected SObject? ArrayAtPut(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 2) return null;
        
        if (receiver is SArray array && arguments[0] is SmallInteger index)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            if (idx >= 0 && idx < array.Size)
            {
                array.AtPut(idx, arguments[1]);
                return arguments[1];
            }
        }
        return null;
    }
    
    /// <summary>
    /// Array/String size primitive.
    /// </summary>
    protected SObject? ArraySize(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 0) return null;
        
        return receiver switch
        {
            SArray array => SObjectRef.FromSmallInteger(array.Size).AsObject(),
            SString str => SObjectRef.FromSmallInteger(str.Value.Length).AsObject(),
            _ => null
        };
    }
    
    /// <summary>
    /// String at: primitive.
    /// </summary>
    protected SObject? StringAt(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SString str && arguments[0] is SmallInteger index)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            if (idx >= 0 && idx < str.Value.Length)
            {
                return new Character(str.Value[idx]);
            }
        }
        return null;
    }
    
    /// <summary>
    /// String at:put: primitive.
    /// </summary>
    protected SObject? StringAtPut(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 2) return null;
        
        if (receiver is SString str && arguments[0] is SmallInteger index && arguments[1] is Character ch)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            if (idx >= 0 && idx < str.Value.Length)
            {
                // Note: In Smalltalk, string mutation would modify the original,
                // but since we have immutable strings in C#, we can't actually mutate here.
                // This primitive would need special handling in the interpreter
                // to replace the string object in place.
                var chars = str.Value.ToCharArray();
                chars[idx] = ch.Value;
                // Return the character to indicate success
                return ch;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Object at: primitive (for indexed instance variables).
    /// </summary>
    protected SObject? ObjectAt(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 1) return null;
        
        if (receiver is SInstance instance && arguments[0] is SmallInteger index)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            return instance.GetIndexedVariable(idx).AsObject();
        }
        return null;
    }
    
    /// <summary>
    /// Object at:put: primitive (for indexed instance variables).
    /// </summary>
    protected SObject? ObjectAtPut(SObject receiver, SObject[] arguments)
    {
        if (arguments.Length != 2) return null;
        
        if (receiver is SInstance instance && arguments[0] is SmallInteger index)
        {
            var idx = (int)index.Value - 1; // Smalltalk indices are 1-based
            instance.SetIndexedVariable(idx, SObjectRef.FromObject(arguments[1]));
            return arguments[1];
        }
        return null;
    }
    
    #endregion
}