using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Registry and dispatch for primitive operations using the IPrimitive interface.
/// </summary>
public class PrimitiveRegistry : IPrimitiveRegistry
{
    private readonly Dictionary<int, IPrimitive> _primitives = new();

    /// <inheritdoc/>
    public int Count => _primitives.Count;

    /// <inheritdoc/>
    public void Register(int primitiveNumber, IPrimitive primitive)
    {
        _primitives[primitiveNumber] = primitive;
    }

    /// <inheritdoc/>
    public bool IsRegistered(int primitiveNumber)
    {
        return _primitives.ContainsKey(primitiveNumber);
    }

    /// <inheritdoc/>
    public PrimitiveResult Execute(int primitiveNumber, PrimitiveExecutionContext context)
    {
        if (_primitives.TryGetValue(primitiveNumber, out var primitive))
        {
            return primitive.Execute(context);
        }

        // Primitive not found
        return PrimitiveResult.Fail();
    }

    /// <inheritdoc/>
    public SmalltalkOOP ExecuteLegacy(int primitiveNumber, IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Call extended version with no extra dependencies
        return ExecuteLegacyExtended(primitiveNumber, ctx, receiver, args, null, null, metadata: null);
    }

    /// <summary>
    /// Executes a primitive with extended dependencies for IPrimitive implementations.
    /// This method adapts the legacy calling convention to the new IPrimitive interface.
    /// </summary>
    public SmalltalkOOP ExecuteLegacyExtended(
        int primitiveNumber,
        IPrimitiveContext ctx,
        SmalltalkOOP receiver,
        SmalltalkOOP[] args,
        RuntimeHeapAccessor? runtimeHeapAccess,
        LayoutResolver? layoutResolver,
        IContextManager? contextManager = null,
        int instructionPointer = 0,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null,
        VMMetadata? metadata = null)
    {
        if (_primitives.TryGetValue(primitiveNumber, out var primitive))
        {
            // Save current active context to detect context switches
            SmalltalkOOP? activeContextBefore = contextManager?.ActiveContext;

            // Create execution context from legacy context with full dependencies
            var execContext = new PrimitiveExecutionContext(
                receiver,
                args,
                ctx.HeapManager,
                platform: ctx.PlatformBridge,
                runtimeHeapAccess: runtimeHeapAccess,
                layoutResolver: layoutResolver,
                contextManager: contextManager,
                instructionPointer: instructionPointer,
                cacheManager: cacheManager,
                messageSender: messageSender,
                metadata: metadata
            );

            var result = primitive.Execute(execContext);
            ctx.PrimitiveSuccess = result.Success;

            // Detect if primitive changed the active context and propagate to VM
            if (contextManager != null && activeContextBefore.HasValue)
            {
                SmalltalkOOP activeContextAfter = contextManager.ActiveContext;
                if (activeContextAfter != activeContextBefore.Value)
                {
                    // Context was switched by primitive - signal to VM via IPrimitiveContext
                    ctx.SetActiveContext(activeContextAfter);
                }
            }

            return result.Value;
        }

        // Primitive not found
        ctx.PrimitiveSuccess = false;
        return heapManager.GetNil();
    }

    /// <summary>
    /// Creates a registry with all IPrimitive implementations.
    /// This is the primary factory method for creating the primitive registry.
    /// </summary>
    /// <returns>Registry with all primitives registered.</returns>
    public static PrimitiveRegistry CreateWithNewPrimitives()
    {
        var registry = new PrimitiveRegistry();

        // SmallInteger arithmetic primitives (1-17)
        registry.Register(1, new SmallIntegerArithmetic.Add());
        registry.Register(2, new SmallIntegerArithmetic.Subtract());
        registry.Register(3, new SmallIntegerArithmetic.LessThan());
        registry.Register(4, new SmallIntegerArithmetic.GreaterThan());
        registry.Register(5, new SmallIntegerArithmetic.LessOrEqual());
        registry.Register(6, new SmallIntegerArithmetic.GreaterOrEqual());
        registry.Register(7, new SmallIntegerArithmetic.Equal());
        registry.Register(8, new SmallIntegerArithmetic.NotEqual());
        registry.Register(9, new SmallIntegerArithmetic.Multiply());
        registry.Register(10, new SmallIntegerArithmetic.Divide());
        registry.Register(11, new SmallIntegerArithmetic.Mod());
        registry.Register(12, new SmallIntegerArithmetic.Div());
        registry.Register(13, new SmallIntegerArithmetic.Quo());
        registry.Register(14, new SmallIntegerArithmetic.BitAnd());
        registry.Register(15, new SmallIntegerArithmetic.BitOr());
        registry.Register(16, new SmallIntegerArithmetic.BitXor());
        registry.Register(17, new SmallIntegerArithmetic.BitShift());

        // Point primitives (18)
        registry.Register(18, new PointPrimitives.MakePoint());

        // LargeInteger arithmetic primitives (21-37)
        registry.Register(21, new LargeIntegerArithmetic.Add());
        registry.Register(22, new LargeIntegerArithmetic.Subtract());
        registry.Register(23, new LargeIntegerArithmetic.LessThan());
        registry.Register(24, new LargeIntegerArithmetic.GreaterThan());
        registry.Register(25, new LargeIntegerArithmetic.LessOrEqual());
        registry.Register(26, new LargeIntegerArithmetic.GreaterOrEqual());
        registry.Register(27, new LargeIntegerArithmetic.Equal());
        registry.Register(28, new LargeIntegerArithmetic.NotEqual());
        registry.Register(29, new LargeIntegerArithmetic.Multiply());
        registry.Register(30, new LargeIntegerArithmetic.Divide());
        registry.Register(31, new LargeIntegerArithmetic.Mod());
        registry.Register(32, new LargeIntegerArithmetic.Div());
        registry.Register(33, new LargeIntegerArithmetic.Quo());
        registry.Register(34, new LargeIntegerArithmetic.BitAnd());
        registry.Register(35, new LargeIntegerArithmetic.BitOr());
        registry.Register(36, new LargeIntegerArithmetic.BitXor());
        registry.Register(37, new LargeIntegerArithmetic.BitShift());

        // Float arithmetic primitives (40-54)
        registry.Register(40, new FloatArithmetic.AsFloat());
        registry.Register(41, new FloatArithmetic.Add());
        registry.Register(42, new FloatArithmetic.Subtract());
        registry.Register(43, new FloatArithmetic.LessThan());
        registry.Register(44, new FloatArithmetic.GreaterThan());
        registry.Register(45, new FloatArithmetic.LessOrEqual());
        registry.Register(46, new FloatArithmetic.GreaterOrEqual());
        registry.Register(47, new FloatArithmetic.Equal());
        registry.Register(48, new FloatArithmetic.NotEqual());
        registry.Register(49, new FloatArithmetic.Multiply());
        registry.Register(50, new FloatArithmetic.Divide());
        registry.Register(51, new FloatArithmetic.Truncated());
        registry.Register(52, new FloatArithmetic.FractionPart());
        registry.Register(53, new FloatArithmetic.Exponent());
        registry.Register(54, new FloatArithmetic.TimesTwoPower());

        // Object primitives (60-79, 110-111)
        registry.Register(60, new SimpleObjectPrimitives.At());
        registry.Register(61, new SimpleObjectPrimitives.AtPut());
        registry.Register(62, new SimpleObjectPrimitives.Size());
        registry.Register(63, new SimpleObjectPrimitives.StringAt());
        registry.Register(64, new SimpleObjectPrimitives.StringAtPut());
        registry.Register(65, new SimpleObjectPrimitives.Next());
        registry.Register(66, new SimpleObjectPrimitives.NextPut());
        registry.Register(67, new SimpleObjectPrimitives.AtEnd());
        registry.Register(68, new SimpleObjectPrimitives.ObjectAt());
        registry.Register(69, new SimpleObjectPrimitives.ObjectAtPut());
        registry.Register(70, new SimpleObjectPrimitives.New());
        registry.Register(71, new SimpleObjectPrimitives.NewWithSize());
        registry.Register(72, new SimpleObjectPrimitives.Become());
        registry.Register(73, new SimpleObjectPrimitives.InstVarAt());
        registry.Register(74, new SimpleObjectPrimitives.InstVarAtPut());
        registry.Register(75, new SimpleObjectPrimitives.Hash());
        registry.Register(76, new MiscArithmetic.AsObject());
        registry.Register(77, new SimpleObjectPrimitives.SomeInstance());
        registry.Register(78, new SimpleObjectPrimitives.NextInstance());
        registry.Register(79, new SimpleObjectPrimitives.NewMethodHeader());
        registry.Register(110, new SimpleObjectPrimitives.IdentityEquals());
        registry.Register(111, new SimpleObjectPrimitives.Class());

        // Control primitives (80-89)
        registry.Register(80, new ControlPrimitives.BlockCopy());
        registry.Register(81, new ControlPrimitives.Value());
        registry.Register(82, new ControlPrimitives.ValueWithArguments());
        registry.Register(83, new ControlPrimitives.Perform());
        registry.Register(84, new ControlPrimitives.PerformWith());
        registry.Register(85, new ControlPrimitives.Signal());
        registry.Register(86, new ControlPrimitives.Wait());
        registry.Register(87, new ControlPrimitives.Resume());
        registry.Register(88, new ControlPrimitives.Suspend());
        registry.Register(89, new ControlPrimitives.FlushCache());

        // Input primitives (90-95)
        registry.Register(90, new InputOperations.MousePoint());
        registry.Register(91, new InputOperations.CursorLocPut());
        registry.Register(92, new InputOperations.CursorLink());
        registry.Register(93, new InputOperations.InputSemaphore());
        registry.Register(94, new InputOperations.SampleInterval());
        registry.Register(95, new InputOperations.InputWord());

        // Graphics primitives (96-104)
        registry.Register(96, new GraphicsOperations.CopyBits());
        registry.Register(97, new GraphicsOperations.Snapshot());
        registry.Register(98, new GraphicsOperations.OopsLeft());
        registry.Register(99, new GraphicsOperations.CoreLeft());
        registry.Register(100, new GraphicsOperations.TimeWords());
        registry.Register(101, new GraphicsOperations.BeDisplay());
        registry.Register(102, new GraphicsOperations.BeCursor());
        registry.Register(103, new GraphicsOperations.Beep());
        registry.Register(104, new GraphicsOperations.ClipboardText());

        // String primitives (105-106)
        registry.Register(105, new StringPrimitives.StringReplace());
        registry.Register(106, new StringPrimitives.AsSymbol());

        // Font primitives (147-150)
        // These provide performance optimizations for text operations.
        // StrikeFont can also compute these values via Smalltalk code using xTable.
        registry.Register(147, new FontPrimitives.CharacterWidth());
        registry.Register(148, new FontPrimitives.StringWidth());
        registry.Register(149, new FontPrimitives.FontAscent());
        registry.Register(150, new FontPrimitives.FontDescent());

        // System primitives (112-139)
        registry.Register(112, new SystemOperations.CoreLeft());
        registry.Register(113, new SystemOperations.Quit());
        registry.Register(114, new SystemOperations.ExitToDebugger());
        registry.Register(115, new SystemOperations.OopsLeft());
        registry.Register(116, new SystemOperations.SignalAtOopsLeftWordsLeft());
        registry.Register(117, new SystemOperations.InstSize());
        registry.Register(128, new SystemOperations.SomeInstance());
        registry.Register(129, new SystemOperations.NextInstance());
        registry.Register(130, new SystemOperations.BecomeForward());
        registry.Register(131, new SystemOperations.InstanceCount());
        registry.Register(133, new SystemOperations.ForceGC());
        registry.Register(139, new SystemOperations.NextObject());

        // File system primitives (120-160)
        // Note: Some numbers from Smalltalk source were relocated to avoid conflicts
        registry.Register(120, new FileOperations.PathParse());
        registry.Register(121, new FileOperations.FileOpen());
        registry.Register(122, new FileOperations.FileClose());
        registry.Register(123, new FileOperations.FileRead());
        registry.Register(124, new FileOperations.FileSeek());
        registry.Register(125, new FileOperations.FileDelete());
        registry.Register(126, new FileOperations.FileMove());
        registry.Register(127, new FileOperations.CreateDirectory());
        registry.Register(135, new FileOperations.GetDrives());
        registry.Register(136, new FileOperations.FileWrite());
        registry.Register(137, new FileOperations.GetCurrentDirectory());
        registry.Register(138, new FileOperations.SetCurrentDirectory());
        registry.Register(140, new FileOperations.RemoveDirectory());
        registry.Register(141, new FileOperations.FindFirst());
        registry.Register(142, new FileOperations.FindNext());
        registry.Register(143, new FileOperations.FindClose());
        registry.Register(144, new FileOperations.GetFileSize());
        registry.Register(145, new FileOperations.GetFilePosition());
        registry.Register(146, new FileOperations.DirectoryList());
        registry.Register(151, new FileOperations.FileFlush());
        registry.Register(152, new FileOperations.ReadFileAsString());
        registry.Register(153, new FileOperations.WriteStringToFile());
        registry.Register(154, new FileOperations.GetFileAttributes());
        registry.Register(155, new FileOperations.FileExists());

        return registry;
    }
}
