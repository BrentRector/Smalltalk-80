using System;
using System.Collections.Generic;
using Smalltalk80;

/// <summary>
/// Provides a dispatcher for built-in primitive operations invoked by compiled methods.
/// </summary>
public static class Primitives
{
    /// <summary>Delegate representing a primitive implementation.</summary>
    /// <param name="context">The current execution context.</param>
    /// <returns><c>true</c> if the primitive handled the call.</returns>
    private delegate bool PrimitiveHandler(Context context);

    /// <summary>Table mapping primitive indices to their handlers.</summary>
    private static readonly Dictionary<int, PrimitiveHandler> Handlers = new()
    {
        // SmallInteger primitives
        [1] = IntegerPrimitives.Add,
        [2] = IntegerPrimitives.Subtract,
        [3] = IntegerPrimitives.LessThan,
        [4] = IntegerPrimitives.GreaterThan,
        [5] = IntegerPrimitives.LessOrEqual,
        [6] = IntegerPrimitives.GreaterOrEqual,
        [7] = IntegerPrimitives.Equals,
        [8] = IntegerPrimitives.NotEqual,
        [9] = IntegerPrimitives.Multiply,
        [10] = IntegerPrimitives.Divide,
        [11] = IntegerPrimitives.Mod,
        [12] = IntegerPrimitives.Div,
        [13] = IntegerPrimitives.Quo,
        [14] = IntegerPrimitives.BitAnd,
        [15] = IntegerPrimitives.BitOr,
        [16] = IntegerPrimitives.BitXor,
        [17] = IntegerPrimitives.BitShift,
        [18] = IntegerPrimitives.MakePoint,

        // LargeInteger primitives
        [21] = LargeIntegerPrimitives.Add,
        [22] = LargeIntegerPrimitives.Subtract,
        [23] = LargeIntegerPrimitives.LessThan,
        [24] = LargeIntegerPrimitives.GreaterThan,
        [25] = LargeIntegerPrimitives.LessOrEqual,
        [26] = LargeIntegerPrimitives.GreaterOrEqual,
        [27] = LargeIntegerPrimitives.Equals,
        [28] = LargeIntegerPrimitives.NotEqual,
        [29] = LargeIntegerPrimitives.Multiply,
        [30] = LargeIntegerPrimitives.Divide,
        [31] = LargeIntegerPrimitives.Mod,
        [32] = LargeIntegerPrimitives.Div,
        [33] = LargeIntegerPrimitives.Quo,
        [34] = LargeIntegerPrimitives.BitAnd,
        [35] = LargeIntegerPrimitives.BitOr,
        [36] = LargeIntegerPrimitives.BitXor,
        [37] = LargeIntegerPrimitives.BitShift,

        // Float primitives
        [40] = FloatPrimitives.AsFloat,
        [41] = FloatPrimitives.Add,
        [42] = FloatPrimitives.Subtract,
        [43] = FloatPrimitives.LessThan,
        [44] = FloatPrimitives.GreaterThan,
        [45] = FloatPrimitives.LessOrEqual,
        [46] = FloatPrimitives.GreaterOrEqual,
        [47] = FloatPrimitives.Equals,
        [48] = FloatPrimitives.NotEqual,
        [49] = FloatPrimitives.Multiply,
        [50] = FloatPrimitives.Divide,
        [51] = FloatPrimitives.Truncated,
        [52] = FloatPrimitives.FractionalPart,
        [53] = FloatPrimitives.Exponent,
        [54] = FloatPrimitives.TimesTwoPower,

        // Array and stream primitives
        [60] = ObjectPrimitives.At,
        [61] = ObjectPrimitives.AtPut,
        [62] = ObjectPrimitives.Size,
        [63] = ArrayPrimitives.StringAt,
        [64] = ArrayPrimitives.StringAtPut,
        [65] = ArrayPrimitives.Next,
        [66] = ArrayPrimitives.NextPut,
        [67] = ArrayPrimitives.AtEnd,
        [68] = StorageManagementPrimitives.ObjectAt,
        [69] = StorageManagementPrimitives.ObjectAtPut,
        [70] = StorageManagementPrimitives.New,
        [71] = StorageManagementPrimitives.NewWithArg,
        [72] = StorageManagementPrimitives.Become,
        [73] = StorageManagementPrimitives.InstVarAt,
        [74] = StorageManagementPrimitives.InstVarAtPut,
        [75] = StorageManagementPrimitives.AsOop,
        [76] = StorageManagementPrimitives.AsObject,
        [77] = StorageManagementPrimitives.SomeInstance,
        [78] = StorageManagementPrimitives.NextInstance,
        [79] = StorageManagementPrimitives.NewMethod,

        // Control primitives
        [80] = ControlPrimitives.BlockCopy,
        [81] = ControlPrimitives.Value,
        [82] = ControlPrimitives.ValueWithArgs,
        [83] = ControlPrimitives.Perform,
        [84] = ControlPrimitives.PerformWithArgs,
        [85] = ControlPrimitives.Signal,
        [86] = ControlPrimitives.Wait,
        [87] = ControlPrimitives.Resume,
        [88] = ControlPrimitives.Suspend,
        [89] = ControlPrimitives.FlushCache,

        // Input/output primitives
        [90] = InputOutputPrimitives.MousePoint,
        [91] = InputOutputPrimitives.CursorLocPut,
        [92] = InputOutputPrimitives.CursorLink,
        [93] = InputOutputPrimitives.InputSemaphore,
        [94] = InputOutputPrimitives.SampleInterval,
        [95] = InputOutputPrimitives.InputWord,
        [96] = InputOutputPrimitives.CopyBits,
        [97] = InputOutputPrimitives.Snapshot,
        [98] = InputOutputPrimitives.TimeWordsInto,
        [99] = InputOutputPrimitives.TickWordsInto,
        [100] = InputOutputPrimitives.SignalAtTick,
        [101] = InputOutputPrimitives.BeCursor,
        [102] = InputOutputPrimitives.BeDisplay,
        [103] = InputOutputPrimitives.ScanCharacters,
        [104] = InputOutputPrimitives.DrawLoop,
        [105] = InputOutputPrimitives.StringReplace,

        // System primitives
        [110] = ObjectPrimitives.Equivalent,
        [111] = ObjectPrimitives.Class,
        [112] = SystemPrimitives.CoreLeft,
        [113] = SystemPrimitives.Quit,
        [114] = SystemPrimitives.ExitToDebugger,
        [115] = SystemPrimitives.OopsLeft,
        [116] = SystemPrimitives.SignalAtOopsLeftWordsLeft,

        // File system primitives
        [142] = FileSystemPrimitives.OpenStream,
        [143] = FileSystemPrimitives.CloseStream,
        [144] = FileSystemPrimitives.ReadNextChar,
        [145] = FileSystemPrimitives.WriteChar,
        [146] = FileSystemPrimitives.GetFileSize,
        [147] = FileSystemPrimitives.SetPosition,
        [148] = FileSystemPrimitives.GetFileSizeByName,
        [149] = FileSystemPrimitives.FileExists,
        [150] = FileSystemPrimitives.DeleteFile,
        [151] = FileSystemPrimitives.RenameFile,
        [152] = FileSystemPrimitives.GetFileModificationTime,
        [153] = FileSystemPrimitives.GetFileModificationTime,
        [154] = FileSystemPrimitives.CreateDirectory,
        [155] = FileSystemPrimitives.DeleteFile,
        [156] = FileSystemPrimitives.IsDirectory,
        [157] = FileSystemPrimitives.GetDirectoryEntries,
    };

    static Primitives()
    {
        // Fill remaining required primitive slots with stubs returning false
        for (var i = 1; i <= 157; i++)
        {
            if (!Handlers.ContainsKey(i))
            {
                Handlers[i] = static _ => false;
            }
        }
    }

    /// <summary>
    /// Executes the primitive with the specified index.
    /// </summary>
    /// <param name="index">The primitive index.</param>
    /// <param name="context">The current execution context.</param>
    /// <returns><c>true</c> if the primitive handled the call; otherwise <c>false</c>.</returns>
    public static bool Execute(int index, Context context)
        => Handlers.TryGetValue(index, out var handler) && handler(context);
}
