using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Delegate type for primitive operations.
/// Primitives always return a SmalltalkOOP. On success, they return the result.
/// On failure, they set ctx.PrimitiveSuccess = false and return Nil (value is ignored by VM).
/// </summary>
public delegate SmalltalkOOP PrimitiveFunction(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args);

/// <summary>
/// Central table mapping primitive numbers to their implementations.
/// Based on the Blue Book Smalltalk-80 primitive specification.
/// </summary>
public static class PrimitiveTable
{
    private static readonly Dictionary<int, PrimitiveFunction> _primitives = new();

    static PrimitiveTable()
    {
        RegisterPrimitives();
    }

    /// <summary>
    /// Executes a primitive by number.
    /// Sets ctx.PrimitiveSuccess = true before calling, primitives set it to false on failure.
    /// Returns the primitive result (or Nil if primitive not found - with PrimitiveSuccess=false).
    /// </summary>
    public static SmalltalkOOP Execute(int primitiveNumber, IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        if (_primitives.TryGetValue(primitiveNumber, out var primitive))
        {
            ctx.PrimitiveSuccess = true; // Set success flag before calling primitive
            return primitive(ctx, receiver, args);
        }

        // Primitive not found - signal failure
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    private static void RegisterPrimitives()
    {
        // SmallInteger Arithmetic primitives (1-17)
        _primitives[1] = ArithmeticPrimitives.Add;
        _primitives[2] = ArithmeticPrimitives.Subtract;
        _primitives[3] = ArithmeticPrimitives.LessThan;
        _primitives[4] = ArithmeticPrimitives.GreaterThan;
        _primitives[5] = ArithmeticPrimitives.LessOrEqual;
        _primitives[6] = ArithmeticPrimitives.GreaterOrEqual;
        _primitives[7] = ArithmeticPrimitives.Equal;
        _primitives[8] = ArithmeticPrimitives.NotEqual;
        _primitives[9] = ArithmeticPrimitives.Multiply;
        _primitives[10] = ArithmeticPrimitives.Divide;
        _primitives[11] = ArithmeticPrimitives.Mod;
        _primitives[12] = ArithmeticPrimitives.Div;        // SmallInteger // (rounds down toward negative infinity)
        _primitives[13] = ArithmeticPrimitives.Quo;        // SmallInteger quo: (truncates toward zero)
        _primitives[14] = ArithmeticPrimitives.BitAnd;     // SmallInteger bitAnd:
        _primitives[15] = ArithmeticPrimitives.BitOr;      // SmallInteger bitOr:
        _primitives[16] = ArithmeticPrimitives.BitXor;     // SmallInteger bitXor:
        _primitives[17] = ArithmeticPrimitives.BitShift;   // SmallInteger bitShift:

        // Point primitives (18)
        _primitives[18] = PointPrimitives.MakePoint;

        // LargeInteger primitives (21-37)
        _primitives[21] = LargeIntegerPrimitives.Add;
        _primitives[22] = LargeIntegerPrimitives.Subtract;
        _primitives[23] = LargeIntegerPrimitives.LessThan;
        _primitives[24] = LargeIntegerPrimitives.GreaterThan;
        _primitives[25] = LargeIntegerPrimitives.LessOrEqual;
        _primitives[26] = LargeIntegerPrimitives.GreaterOrEqual;
        _primitives[27] = LargeIntegerPrimitives.Equal;
        _primitives[28] = LargeIntegerPrimitives.NotEqual;
        _primitives[29] = LargeIntegerPrimitives.Multiply;
        _primitives[30] = LargeIntegerPrimitives.Divide;
        _primitives[31] = LargeIntegerPrimitives.Mod;
        _primitives[32] = LargeIntegerPrimitives.Div;
        _primitives[33] = LargeIntegerPrimitives.Quo;
        _primitives[34] = LargeIntegerPrimitives.BitAnd;
        _primitives[35] = LargeIntegerPrimitives.BitOr;
        _primitives[36] = LargeIntegerPrimitives.BitXor;
        _primitives[37] = LargeIntegerPrimitives.BitShift;

        // Float primitives (40-54)
        _primitives[40] = FloatPrimitives.AsFloat;
        _primitives[41] = FloatPrimitives.Add;
        _primitives[42] = FloatPrimitives.Subtract;
        _primitives[43] = FloatPrimitives.LessThan;
        _primitives[44] = FloatPrimitives.GreaterThan;
        _primitives[45] = FloatPrimitives.LessOrEqual;
        _primitives[46] = FloatPrimitives.GreaterOrEqual;
        _primitives[47] = FloatPrimitives.Equal;
        _primitives[48] = FloatPrimitives.NotEqual;
        _primitives[49] = FloatPrimitives.Multiply;
        _primitives[50] = FloatPrimitives.Divide;
        _primitives[51] = FloatPrimitives.Truncated;
        _primitives[52] = FloatPrimitives.FractionPart;
        _primitives[53] = FloatPrimitives.Exponent;
        _primitives[54] = FloatPrimitives.TimesTwoPower;

        // Array/Object primitives (60-75, 110-111)
        _primitives[60] = ObjectPrimitives.At;
        _primitives[61] = ObjectPrimitives.AtPut;
        _primitives[62] = ObjectPrimitives.Size;
        _primitives[63] = ObjectPrimitives.StringAt;
        _primitives[64] = ObjectPrimitives.StringAtPut;
        _primitives[65] = ObjectPrimitives.Next;
        _primitives[66] = ObjectPrimitives.NextPut;
        _primitives[67] = ObjectPrimitives.AtEnd;
        _primitives[68] = ObjectPrimitives.ObjectAt;  // CompiledMethod header/literal access
        _primitives[69] = ObjectPrimitives.ObjectAtPut;  // CompiledMethod header/literal modification
        _primitives[70] = ObjectPrimitives.New;
        _primitives[71] = ObjectPrimitives.NewWithSize;
        _primitives[72] = ObjectPrimitives.Become;  // Object identity swap (moved from 68)
        _primitives[73] = ObjectPrimitives.InstVarAt;
        _primitives[74] = ObjectPrimitives.InstVarAtPut;
        _primitives[75] = ObjectPrimitives.Hash;  // Object>>hash - returns SmallInteger hash value
        _primitives[110] = ObjectPrimitives.IdentityEquals;
        _primitives[111] = ObjectPrimitives.Class;

        // CompiledMethod primitives (79)
        _primitives[79] = CompiledMethodPrimitives.NewMethodHeader;

        // Control/Block primitives (80-88) - stubs, require BlockContext/Process infrastructure
        _primitives[80] = ControlPrimitives.BlockCopy;
        _primitives[81] = ControlPrimitives.Value;
        _primitives[82] = ControlPrimitives.ValueWithArg;
        _primitives[83] = ControlPrimitives.Perform;
        _primitives[84] = ControlPrimitives.PerformWith;
        _primitives[85] = ControlPrimitives.Signal;
        _primitives[86] = ControlPrimitives.Wait;
        _primitives[87] = ControlPrimitives.Resume;
        _primitives[88] = ControlPrimitives.Suspend;

        // Input primitives (90-95) - stubs, require IDisplayProvider
        _primitives[90] = InputPrimitives.MousePoint;
        _primitives[91] = InputPrimitives.CursorLocPut;
        _primitives[92] = InputPrimitives.CursorLink;
        _primitives[93] = InputPrimitives.InputSemaphore;
        _primitives[94] = InputPrimitives.SampleInterval;
        _primitives[95] = InputPrimitives.InputWord;

        // Graphics primitives (96-104) - stubs, require BitBlt engine
        _primitives[96] = GraphicsPrimitives.CopyBits;
        _primitives[97] = GraphicsPrimitives.Snapshot;
        _primitives[98] = GraphicsPrimitives.OopsLeft;
        _primitives[99] = GraphicsPrimitives.CoreLeft;
        _primitives[100] = GraphicsPrimitives.TimeWords;
        _primitives[101] = GraphicsPrimitives.BeDisplay;
        _primitives[102] = GraphicsPrimitives.BeCursor;
        _primitives[103] = GraphicsPrimitives.Beep;
        _primitives[104] = GraphicsPrimitives.ClipboardText;

        // String primitives (105-106)
        _primitives[105] = StringPrimitives.StringReplace;
        _primitives[106] = StringPrimitives.AsSymbol;

        // System primitives (112-139) - mix of working and stubs
        _primitives[112] = SystemPrimitives.CoreLeft;
        _primitives[113] = SystemPrimitives.Quit;
        _primitives[114] = SystemPrimitives.ExitToDebugger;
        _primitives[115] = SystemPrimitives.OopsLeft;
        _primitives[116] = SystemPrimitives.SignalAtOopsLeftWordsLeft;
        _primitives[128] = SystemPrimitives.SomeInstance;
        _primitives[129] = SystemPrimitives.NextInstance;
        _primitives[130] = SystemPrimitives.BecomeForward;
        _primitives[131] = SystemPrimitives.InstanceCount;
        _primitives[133] = SystemPrimitives.ForceGC;
        _primitives[139] = SystemPrimitives.NextObject;

        // BlockClosure primitives (207)
        _primitives[207] = BlockClosurePrimitives.ValueWithArguments;

        // Note: Primitives 117-127, 132, 134-138 are reserved/not implemented in Blue Book
    }

    /// <summary>
    /// Gets the total number of registered primitives.
    /// </summary>
    public static int Count => _primitives.Count;

    /// <summary>
    /// Checks if a primitive number is registered.
    /// </summary>
    public static bool IsRegistered(int primitiveNumber) => _primitives.ContainsKey(primitiveNumber);
}
