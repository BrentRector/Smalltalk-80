using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Scheduling;

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
        return PrimitiveResult.Fail(context.Metadata!);
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
        RuntimeServices? runtimeServices,
        IContextManager? contextManager = null,
        int instructionPointer = 0,
        ICacheManager? cacheManager = null,
        IPrimitiveMessageSender? messageSender = null,
        VMMetadata? metadata = null,
        IStackManager? stackManager = null,
        ITimerManager? timerManager = null,
        ILowMemoryManager? lowMemoryManager = null,
        IProcessScheduler? processScheduler = null,
        VirtualMachine? vm = null)
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
                stackManager: stackManager,
                contextManager: contextManager,
                display: ctx.Display,
                runtimeServices: runtimeServices,
                instructionPointer: instructionPointer,
                cacheManager: cacheManager,
                messageSender: messageSender,
                metadata: metadata,
                timerManager: timerManager,
                lowMemoryManager: lowMemoryManager,
                processScheduler: processScheduler,
                legacyContext: ctx,
                vm: vm
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

        // Primitive not found - return nil
        ctx.PrimitiveSuccess = false;
        if (metadata == null)
            throw new InvalidOperationException("VMMetadata required but not provided to ExecuteLegacyExtended");
        return metadata.KnownObjects.Nil;
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
        registry.Register(84, new ControlPrimitives.PerformWithArguments());
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

        // Graphics primitives (96-97)
        registry.Register(96, new GraphicsOperations.CopyBits());
        registry.Register(97, new GraphicsOperations.Snapshot());

        // Time primitives (98-100) - Blue Book specification
        registry.Register(98, new TimeOperations.SecondClockInto());    // Blue Book: Time class secondClockInto:
        registry.Register(99, new TimeOperations.MillisecondClockInto()); // Blue Book: Time class millisecondClockInto:
        registry.Register(100, new TimeOperations.SignalAtMilliseconds()); // Blue Book: ProcessorScheduler signal:atMilliseconds:

        // Display primitives (101-104)
        registry.Register(101, new GraphicsOperations.BeCursor());     // Blue Book: Primitive 101 = beCursor
        registry.Register(102, new GraphicsOperations.BeDisplay());    // Blue Book: Primitive 102 = beDisplay
        registry.Register(103, new FontPrimitives.ScanCharacters());   // Blue Book: Primitive 103 = scanCharactersFrom:to:in:rightX:stopConditions:displaying:
        registry.Register(104, new GraphicsOperations.DrawLoop());     // Blue Book: Primitive 104 = drawLoopX:Y: (Bresenham)

        // String primitives (105-107)
        registry.Register(105, new StringPrimitives.StringReplace());
        registry.Register(106, new StringPrimitives.AsSymbol());
        registry.Register(107, new StringPrimitives.StoreOn());

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
        // File info primitives (path parsing, delete, move, attributes)
        registry.Register(120, new FileInfoPrimitives.PathParse());
        registry.Register(125, new FileInfoPrimitives.FileDelete());
        registry.Register(126, new FileInfoPrimitives.FileMove());
        registry.Register(144, new FileInfoPrimitives.GetFileSize());
        registry.Register(145, new FileInfoPrimitives.GetFilePosition());
        registry.Register(152, new FileInfoPrimitives.ReadFileAsString());
        registry.Register(153, new FileInfoPrimitives.WriteStringToFile());
        registry.Register(154, new FileInfoPrimitives.GetFileAttributes());
        registry.Register(155, new FileInfoPrimitives.FileExists());
        registry.Register(157, new FileInfoPrimitives.FileCopy());

        // File stream primitives (open, close, read, write, seek)
        registry.Register(121, new FileStreamPrimitives.FileOpen());
        registry.Register(122, new FileStreamPrimitives.FileClose());
        registry.Register(123, new FileStreamPrimitives.FileRead());
        registry.Register(124, new FileStreamPrimitives.FileSeek());
        registry.Register(132, new FileStreamPrimitives.FileReadByte());
        registry.Register(134, new FileStreamPrimitives.FileWriteByte());
        registry.Register(136, new FileStreamPrimitives.FileWrite());
        registry.Register(151, new FileStreamPrimitives.FileFlush());
        registry.Register(156, new FileStreamPrimitives.SetSnapshotFile());

        // Directory primitives (create, remove, list, enumerate)
        registry.Register(127, new DirectoryPrimitives.CreateDirectory());
        registry.Register(135, new DirectoryPrimitives.GetDrives());
        registry.Register(137, new DirectoryPrimitives.GetCurrentDirectory());
        registry.Register(138, new DirectoryPrimitives.SetCurrentDirectory());
        registry.Register(140, new DirectoryPrimitives.RemoveDirectory());
        registry.Register(141, new DirectoryPrimitives.FindFirst());
        registry.Register(142, new DirectoryPrimitives.FindNext());
        registry.Register(143, new DirectoryPrimitives.FindClose());
        registry.Register(146, new DirectoryPrimitives.DirectoryList());

        // Primitive 202: bitState - Get button state from simulated hardware register
        // Enables atomic polling of mouse button state for tight loops (PopUpMenu, etc.)
        registry.Register(202, new InputOperations.BitState());

        // Link primitives (230-239) - Windows NTFS link support
        // Symbolic links, hard links, and junctions
        registry.Register(230, new LinkPrimitives.CreateSymbolicLink());
        registry.Register(231, new LinkPrimitives.CreateHardLink());
        registry.Register(232, new LinkPrimitives.CreateJunction());
        registry.Register(233, new LinkPrimitives.GetFileAttributesEx());
        registry.Register(234, new LinkPrimitives.GetReparseTag());
        registry.Register(235, new LinkPrimitives.GetSymbolicLinkTarget());
        registry.Register(236, new LinkPrimitives.GetJunctionTarget());
        registry.Register(237, new LinkPrimitives.GetFinalPath());
        registry.Register(238, new LinkPrimitives.GetHardLinkCount());
        registry.Register(239, new LinkPrimitives.DeleteReparsePoint());

        return registry;
    }
}
