using Core.Abstractions.Execution;
using Core.Abstractions.Memory;
using Core.Abstractions.Primitives;
using Core.Memory;

namespace Core.Primitives;

/// <summary>
/// Implements the primitive table with essential primitives for arithmetic and object creation.
/// Primitives are indexed methods that provide direct system functionality.
/// </summary>
public sealed class PrimitiveTable : IPrimitiveTable
{
    private readonly Dictionary<int, Func<IExecutionContext, IObjectMemory, bool>> _primitives = [];
    private readonly Dictionary<int, string> _primitiveNames = [];
    private readonly IObjectMemory _objectMemory;

    /// <summary>
    /// Initializes a new instance of the PrimitiveTable class with essential primitives.
    /// </summary>
    /// <param name="objectMemory">The object memory for primitive operations.</param>
    public PrimitiveTable(IObjectMemory objectMemory)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        RegisterCorePrimitives();
    }

    /// <inheritdoc/>
    public bool ExecutePrimitive(int primitiveIndex, IExecutionContext context)
    {
        if (_primitives.TryGetValue(primitiveIndex, out var primitive))
        {
            try
            {
                return primitive(context, _objectMemory);
            }
            catch
            {
                // Primitive failed - method will continue with bytecode execution
                return false;
            }
        }

        // Unknown primitive
        return false;
    }

    /// <inheritdoc/>
    public void RegisterPrimitive(int index, Func<IExecutionContext, IObjectMemory, bool> primitive)
    {
        ArgumentNullException.ThrowIfNull(primitive);
        _primitives[index] = primitive;
    }

    /// <inheritdoc/>
    public string? GetPrimitiveName(int index)
    {
        return _primitiveNames.TryGetValue(index, out var name) ? name : null;
    }

    private void RegisterCorePrimitives()
    {
        // SmallInteger arithmetic primitives (1-17)
        RegisterPrimitive(1, PrimitiveSmallIntegerAdd);
        _primitiveNames[1] = "primitiveAdd";
        RegisterPrimitive(2, PrimitiveSmallIntegerSubtract);
        _primitiveNames[2] = "primitiveSubtract";
        RegisterPrimitive(3, PrimitiveSmallIntegerLessThan);
        _primitiveNames[3] = "primitiveLessThan";
        RegisterPrimitive(4, PrimitiveSmallIntegerGreaterThan);
        _primitiveNames[4] = "primitiveGreaterThan";
        RegisterPrimitive(5, PrimitiveSmallIntegerLessOrEqual);
        _primitiveNames[5] = "primitiveLessOrEqual";
        RegisterPrimitive(6, PrimitiveSmallIntegerGreaterOrEqual);
        _primitiveNames[6] = "primitiveGreaterOrEqual";
        RegisterPrimitive(7, PrimitiveSmallIntegerEqual);
        _primitiveNames[7] = "primitiveEqual";
        RegisterPrimitive(8, PrimitiveSmallIntegerNotEqual);
        _primitiveNames[8] = "primitiveNotEqual";
        RegisterPrimitive(9, PrimitiveSmallIntegerMultiply);
        _primitiveNames[9] = "primitiveMultiply";
        RegisterPrimitive(10, PrimitiveSmallIntegerDivide);
        _primitiveNames[10] = "primitiveDivide";
        RegisterPrimitive(11, PrimitiveSmallIntegerMod);
        _primitiveNames[11] = "primitiveMod";
        RegisterPrimitive(12, PrimitiveSmallIntegerDiv);
        _primitiveNames[12] = "primitiveDiv";
        RegisterPrimitive(13, PrimitiveSmallIntegerQuo);
        _primitiveNames[13] = "primitiveQuo";
        RegisterPrimitive(14, PrimitiveSmallIntegerBitAnd);
        _primitiveNames[14] = "primitiveBitAnd";
        RegisterPrimitive(15, PrimitiveSmallIntegerBitOr);
        _primitiveNames[15] = "primitiveBitOr";
        RegisterPrimitive(16, PrimitiveSmallIntegerBitXor);
        _primitiveNames[16] = "primitiveBitXor";
        RegisterPrimitive(17, PrimitiveSmallIntegerBitShift);
        _primitiveNames[17] = "primitiveBitShift";

        // Array/Object access primitives (60-69)
        RegisterPrimitive(60, PrimitiveAt);
        _primitiveNames[60] = "primitiveAt";
        RegisterPrimitive(61, PrimitiveAtPut);
        _primitiveNames[61] = "primitiveAtPut";
        RegisterPrimitive(62, PrimitiveSize);
        _primitiveNames[62] = "primitiveSize";
        RegisterPrimitive(63, PrimitiveStringAt);
        _primitiveNames[63] = "primitiveStringAt";
        RegisterPrimitive(64, PrimitiveStringAtPut);
        _primitiveNames[64] = "primitiveStringAtPut";

        // Object creation primitives (70-79)
        RegisterPrimitive(70, PrimitiveNew);
        _primitiveNames[70] = "primitiveNew";
        RegisterPrimitive(71, PrimitiveNewWithArg);
        _primitiveNames[71] = "primitiveNewWithArg";
        RegisterPrimitive(75, PrimitiveHash);
        _primitiveNames[75] = "primitiveHash";

        // System primitives (110-119)
        RegisterPrimitive(110, PrimitiveIdentical);
        _primitiveNames[110] = "primitiveIdentical";
        RegisterPrimitive(111, PrimitiveClass);
        _primitiveNames[111] = "primitiveClass";
    }

    // SmallInteger arithmetic primitives

    private static bool PrimitiveSmallIntegerAdd(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() + argument.ToSmallInteger();
        
        if (!OOP.CanBeSmallInteger(result))
        {
            return false; // Overflow - fail primitive
        }

        context.Push(OOP.FromSmallInteger((int)result));
        return true;
    }

    private static bool PrimitiveSmallIntegerSubtract(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() - argument.ToSmallInteger();
        
        if (!OOP.CanBeSmallInteger(result))
        {
            return false;
        }

        context.Push(OOP.FromSmallInteger((int)result));
        return true;
    }

    private static bool PrimitiveSmallIntegerLessThan(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() < argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerGreaterThan(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() > argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerLessOrEqual(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() <= argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerGreaterOrEqual(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() >= argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerEqual(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() == argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerNotEqual(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() != argument.ToSmallInteger();
        context.Push(result ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveSmallIntegerMultiply(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        long result = (long)receiver.ToSmallInteger() * argument.ToSmallInteger();
        
        if (!OOP.CanBeSmallInteger(result))
        {
            return false;
        }

        context.Push(OOP.FromSmallInteger((int)result));
        return true;
    }

    private static bool PrimitiveSmallIntegerDivide(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var divisor = argument.ToSmallInteger();
        if (divisor == 0)
        {
            return false; // Division by zero
        }

        var dividend = receiver.ToSmallInteger();
        
        // Check for exact division
        if (dividend % divisor != 0)
        {
            return false; // Not exact - need Fraction
        }

        var result = dividend / divisor;
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerMod(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var divisor = argument.ToSmallInteger();
        if (divisor == 0)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() % divisor;
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerDiv(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var divisor = argument.ToSmallInteger();
        if (divisor == 0)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() / divisor;
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerQuo(IExecutionContext context, IObjectMemory memory)
    {
        // Quotient - truncated division
        return PrimitiveSmallIntegerDiv(context, memory);
    }

    private static bool PrimitiveSmallIntegerBitAnd(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() & argument.ToSmallInteger();
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerBitOr(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() | argument.ToSmallInteger();
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerBitXor(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var result = receiver.ToSmallInteger() ^ argument.ToSmallInteger();
        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    private static bool PrimitiveSmallIntegerBitShift(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);

        if (!receiver.IsSmallInteger || !argument.IsSmallInteger)
        {
            return false;
        }

        var value = receiver.ToSmallInteger();
        var shift = argument.ToSmallInteger();

        var result = shift >= 0 ? value << shift : value >> (-shift);
        
        if (!OOP.CanBeSmallInteger(result))
        {
            return false;
        }

        context.Push(OOP.FromSmallInteger(result));
        return true;
    }

    // Array/Object access primitives

    private static bool PrimitiveAt(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var index = context.GetArgument(0);

        if (!index.IsSmallInteger)
        {
            return false;
        }

        var idx = index.ToSmallInteger() - 1; // Smalltalk uses 1-based indexing
        if (idx < 0)
        {
            return false;
        }

        try
        {
            var value = memory.FetchPointer(receiver, idx);
            context.Push(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PrimitiveAtPut(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var index = context.GetArgument(0);
        var value = context.GetArgument(1);

        if (!index.IsSmallInteger)
        {
            return false;
        }

        var idx = index.ToSmallInteger() - 1;
        if (idx < 0)
        {
            return false;
        }

        try
        {
            memory.StorePointer(receiver, idx, value);
            context.Push(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PrimitiveSize(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var size = memory.FetchWordLengthOf(receiver);
        context.Push(OOP.FromSmallInteger(size));
        return true;
    }

    private static bool PrimitiveStringAt(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var index = context.GetArgument(0);

        if (!index.IsSmallInteger)
        {
            return false;
        }

        var idx = index.ToSmallInteger() - 1;
        if (idx < 0)
        {
            return false;
        }

        try
        {
            var byteValue = memory.FetchByte(receiver, idx);
            context.Push(OOP.FromSmallInteger(byteValue));
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool PrimitiveStringAtPut(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var index = context.GetArgument(0);
        var value = context.GetArgument(1);

        if (!index.IsSmallInteger || !value.IsSmallInteger)
        {
            return false;
        }

        var idx = index.ToSmallInteger() - 1;
        var byteValue = value.ToSmallInteger();
        
        if (idx < 0 || byteValue < 0 || byteValue > 255)
        {
            return false;
        }

        try
        {
            memory.StoreByte(receiver, idx, (byte)byteValue);
            context.Push(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // Object creation primitives

    private static bool PrimitiveNew(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        
        // Get instance specification from class
        var instSpec = memory.FetchPointer(receiver, 2); // Instance specification at index 2
        
        if (!instSpec.IsSmallInteger)
        {
            return false;
        }

        var spec = instSpec.ToSmallInteger();
        var instSize = spec & 0xFFFF; // Lower 16 bits are instance size
        
        var newObject = memory.AllocateObject(receiver, instSize);
        context.Push(newObject);
        return true;
    }

    private static bool PrimitiveNewWithArg(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var sizeArg = context.GetArgument(0);

        if (!sizeArg.IsSmallInteger)
        {
            return false;
        }

        var size = sizeArg.ToSmallInteger();
        if (size < 0)
        {
            return false;
        }

        var newObject = memory.AllocateObject(receiver, size);
        context.Push(newObject);
        return true;
    }

    private static bool PrimitiveHash(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        
        // Simple hash implementation
        var hash = receiver.IsSmallInteger 
            ? receiver.ToSmallInteger() 
            : (int)(receiver.Value >> 1);
        
        context.Push(OOP.FromSmallInteger(hash & 0x0FFFFFFF));
        return true;
    }

    // System primitives

    private static bool PrimitiveIdentical(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var argument = context.GetArgument(0);
        
        var identical = receiver.Value == argument.Value;
        context.Push(identical ? OOP.True : OOP.False);
        return true;
    }

    private static bool PrimitiveClass(IExecutionContext context, IObjectMemory memory)
    {
        var receiver = context.Receiver;
        var classOop = memory.FetchClassOf(receiver);
        context.Push(classOop);
        return true;
    }
}