using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Registry and dispatch for primitive operations.
/// Supports both new IPrimitive interface and legacy PrimitiveFunction for gradual migration.
/// </summary>
public class PrimitiveRegistry : IPrimitiveRegistry
{
    private readonly Dictionary<int, IPrimitive> _primitives = new();
    private readonly Dictionary<int, PrimitiveFunction> _legacyPrimitives = new();

    /// <inheritdoc/>
    public int Count => _primitives.Count + _legacyPrimitives.Count;

    /// <inheritdoc/>
    public void Register(int primitiveNumber, IPrimitive primitive)
    {
        // Remove legacy version if exists (migration)
        _legacyPrimitives.Remove(primitiveNumber);
        _primitives[primitiveNumber] = primitive;
    }

    /// <inheritdoc/>
    public void RegisterLegacy(int primitiveNumber, PrimitiveFunction function)
    {
        // Only register legacy if no new implementation exists
        if (!_primitives.ContainsKey(primitiveNumber))
        {
            _legacyPrimitives[primitiveNumber] = function;
        }
    }

    /// <inheritdoc/>
    public bool IsRegistered(int primitiveNumber)
    {
        return _primitives.ContainsKey(primitiveNumber) || _legacyPrimitives.ContainsKey(primitiveNumber);
    }

    /// <inheritdoc/>
    public PrimitiveResult Execute(int primitiveNumber, PrimitiveExecutionContext context)
    {
        // Try new interface first
        if (_primitives.TryGetValue(primitiveNumber, out var primitive))
        {
            return primitive.Execute(context);
        }

        // Fall back to legacy - need to create a legacy context wrapper
        // This path is for primitives not yet migrated
        if (_legacyPrimitives.TryGetValue(primitiveNumber, out var legacyFunc))
        {
            // We need an IPrimitiveContext to call legacy primitives
            // This will be provided by the caller during transition
            throw new InvalidOperationException(
                $"Primitive {primitiveNumber} is registered as legacy but called via new interface. " +
                "Use ExecuteLegacy() during migration period.");
        }

        // Primitive not found
        return PrimitiveResult.Fail();
    }

    /// <inheritdoc/>
    public SmalltalkOOP ExecuteLegacy(int primitiveNumber, IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Call extended version with no extra dependencies
        return ExecuteLegacyExtended(primitiveNumber, ctx, receiver, args, null, null);
    }

    /// <summary>
    /// Executes a primitive with extended dependencies for new IPrimitive implementations.
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
        IPrimitiveMessageSender? messageSender = null)
    {
        // Try new interface first - adapt to legacy calling convention
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
                messageSender: messageSender
            );

            var result = primitive.Execute(execContext);
            ctx.PrimitiveSuccess = result.Success;

            // Detect if primitive changed the active context and propagate to VM
            // This ensures _contextSwitchedByPrimitive flag gets set properly
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

        // Fall back to legacy
        if (_legacyPrimitives.TryGetValue(primitiveNumber, out var legacyFunc))
        {
            ctx.PrimitiveSuccess = true; // Set success flag before calling
            return legacyFunc(ctx, receiver, args);
        }

        // Primitive not found
        ctx.PrimitiveSuccess = false;
        return SpecialObjectsArray.Instance.Nil;
    }

    /// <summary>
    /// Creates a registry with all legacy primitives registered.
    /// Used during migration period to maintain backward compatibility.
    /// </summary>
    /// <returns>Registry with all legacy primitives from PrimitiveTable.</returns>
    public static PrimitiveRegistry CreateWithLegacyPrimitives()
    {
        var registry = new PrimitiveRegistry();

        // SmallInteger Arithmetic primitives (1-17)
        registry.RegisterLegacy(1, ArithmeticPrimitives.Add);
        registry.RegisterLegacy(2, ArithmeticPrimitives.Subtract);
        registry.RegisterLegacy(3, ArithmeticPrimitives.LessThan);
        registry.RegisterLegacy(4, ArithmeticPrimitives.GreaterThan);
        registry.RegisterLegacy(5, ArithmeticPrimitives.LessOrEqual);
        registry.RegisterLegacy(6, ArithmeticPrimitives.GreaterOrEqual);
        registry.RegisterLegacy(7, ArithmeticPrimitives.Equal);
        registry.RegisterLegacy(8, ArithmeticPrimitives.NotEqual);
        registry.RegisterLegacy(9, ArithmeticPrimitives.Multiply);
        registry.RegisterLegacy(10, ArithmeticPrimitives.Divide);
        registry.RegisterLegacy(11, ArithmeticPrimitives.Mod);
        registry.RegisterLegacy(12, ArithmeticPrimitives.Div);
        registry.RegisterLegacy(13, ArithmeticPrimitives.Quo);
        registry.RegisterLegacy(14, ArithmeticPrimitives.BitAnd);
        registry.RegisterLegacy(15, ArithmeticPrimitives.BitOr);
        registry.RegisterLegacy(16, ArithmeticPrimitives.BitXor);
        registry.RegisterLegacy(17, ArithmeticPrimitives.BitShift);

        // Point primitives (18)
        registry.RegisterLegacy(18, PointPrimitives.MakePoint);

        // LargeInteger primitives (21-37)
        registry.RegisterLegacy(21, LargeIntegerPrimitives.Add);
        registry.RegisterLegacy(22, LargeIntegerPrimitives.Subtract);
        registry.RegisterLegacy(23, LargeIntegerPrimitives.LessThan);
        registry.RegisterLegacy(24, LargeIntegerPrimitives.GreaterThan);
        registry.RegisterLegacy(25, LargeIntegerPrimitives.LessOrEqual);
        registry.RegisterLegacy(26, LargeIntegerPrimitives.GreaterOrEqual);
        registry.RegisterLegacy(27, LargeIntegerPrimitives.Equal);
        registry.RegisterLegacy(28, LargeIntegerPrimitives.NotEqual);
        registry.RegisterLegacy(29, LargeIntegerPrimitives.Multiply);
        registry.RegisterLegacy(30, LargeIntegerPrimitives.Divide);
        registry.RegisterLegacy(31, LargeIntegerPrimitives.Mod);
        registry.RegisterLegacy(32, LargeIntegerPrimitives.Div);
        registry.RegisterLegacy(33, LargeIntegerPrimitives.Quo);
        registry.RegisterLegacy(34, LargeIntegerPrimitives.BitAnd);
        registry.RegisterLegacy(35, LargeIntegerPrimitives.BitOr);
        registry.RegisterLegacy(36, LargeIntegerPrimitives.BitXor);
        registry.RegisterLegacy(37, LargeIntegerPrimitives.BitShift);

        // Float primitives (40-54)
        registry.RegisterLegacy(40, FloatPrimitives.AsFloat);
        registry.RegisterLegacy(41, FloatPrimitives.Add);
        registry.RegisterLegacy(42, FloatPrimitives.Subtract);
        registry.RegisterLegacy(43, FloatPrimitives.LessThan);
        registry.RegisterLegacy(44, FloatPrimitives.GreaterThan);
        registry.RegisterLegacy(45, FloatPrimitives.LessOrEqual);
        registry.RegisterLegacy(46, FloatPrimitives.GreaterOrEqual);
        registry.RegisterLegacy(47, FloatPrimitives.Equal);
        registry.RegisterLegacy(48, FloatPrimitives.NotEqual);
        registry.RegisterLegacy(49, FloatPrimitives.Multiply);
        registry.RegisterLegacy(50, FloatPrimitives.Divide);
        registry.RegisterLegacy(51, FloatPrimitives.Truncated);
        registry.RegisterLegacy(52, FloatPrimitives.FractionPart);
        registry.RegisterLegacy(53, FloatPrimitives.Exponent);
        registry.RegisterLegacy(54, FloatPrimitives.TimesTwoPower);

        // Array/Object primitives (60-78, 110-111)
        registry.RegisterLegacy(60, ObjectPrimitives.At);
        registry.RegisterLegacy(61, ObjectPrimitives.AtPut);
        registry.RegisterLegacy(62, ObjectPrimitives.Size);
        registry.RegisterLegacy(63, ObjectPrimitives.StringAt);
        registry.RegisterLegacy(64, ObjectPrimitives.StringAtPut);
        registry.RegisterLegacy(65, ObjectPrimitives.Next);
        registry.RegisterLegacy(66, ObjectPrimitives.NextPut);
        registry.RegisterLegacy(67, ObjectPrimitives.AtEnd);
        registry.RegisterLegacy(68, ObjectPrimitives.ObjectAt);
        registry.RegisterLegacy(69, ObjectPrimitives.ObjectAtPut);
        registry.RegisterLegacy(70, ObjectPrimitives.New);
        registry.RegisterLegacy(71, ObjectPrimitives.NewWithSize);
        registry.RegisterLegacy(72, ObjectPrimitives.Become);
        registry.RegisterLegacy(73, ObjectPrimitives.InstVarAt);
        registry.RegisterLegacy(74, ObjectPrimitives.InstVarAtPut);
        registry.RegisterLegacy(75, ObjectPrimitives.Hash);
        registry.RegisterLegacy(76, ArithmeticPrimitives.AsObject);
        registry.RegisterLegacy(77, ObjectPrimitives.SomeInstance);
        registry.RegisterLegacy(78, ObjectPrimitives.NextInstance);
        registry.RegisterLegacy(110, ObjectPrimitives.IdentityEquals);
        registry.RegisterLegacy(111, ObjectPrimitives.Class);

        // CompiledMethod primitives (79)
        registry.RegisterLegacy(79, CompiledMethodPrimitives.NewMethodHeader);

        // Control/Block primitives (80-89)
        registry.RegisterLegacy(80, ControlPrimitives.BlockCopy);
        registry.RegisterLegacy(81, ControlPrimitives.Value);
        registry.RegisterLegacy(82, ControlPrimitives.ValueWithArguments);
        registry.RegisterLegacy(83, ControlPrimitives.Perform);
        registry.RegisterLegacy(84, ControlPrimitives.PerformWith);
        registry.RegisterLegacy(85, ControlPrimitives.Signal);
        registry.RegisterLegacy(86, ControlPrimitives.Wait);
        registry.RegisterLegacy(87, ControlPrimitives.Resume);
        registry.RegisterLegacy(88, ControlPrimitives.Suspend);
        registry.RegisterLegacy(89, ControlPrimitives.FlushCache);

        // Input primitives (90-95)
        registry.RegisterLegacy(90, InputPrimitives.MousePoint);
        registry.RegisterLegacy(91, InputPrimitives.CursorLocPut);
        registry.RegisterLegacy(92, InputPrimitives.CursorLink);
        registry.RegisterLegacy(93, InputPrimitives.InputSemaphore);
        registry.RegisterLegacy(94, InputPrimitives.SampleInterval);
        registry.RegisterLegacy(95, InputPrimitives.InputWord);

        // Graphics primitives (96-104)
        registry.RegisterLegacy(96, GraphicsPrimitives.CopyBits);
        registry.RegisterLegacy(97, GraphicsPrimitives.Snapshot);
        registry.RegisterLegacy(98, GraphicsPrimitives.OopsLeft);
        registry.RegisterLegacy(99, GraphicsPrimitives.CoreLeft);
        registry.RegisterLegacy(100, GraphicsPrimitives.TimeWords);
        registry.RegisterLegacy(101, GraphicsPrimitives.BeDisplay);
        registry.RegisterLegacy(102, GraphicsPrimitives.BeCursor);
        registry.RegisterLegacy(103, GraphicsPrimitives.Beep);
        registry.RegisterLegacy(104, GraphicsPrimitives.ClipboardText);

        // String primitives (105-106)
        registry.RegisterLegacy(105, StringPrimitives.StringReplace);
        registry.RegisterLegacy(106, StringPrimitives.AsSymbol);

        // System primitives (112-139)
        registry.RegisterLegacy(112, SystemPrimitives.CoreLeft);
        registry.RegisterLegacy(113, SystemPrimitives.Quit);
        registry.RegisterLegacy(114, SystemPrimitives.ExitToDebugger);
        registry.RegisterLegacy(115, SystemPrimitives.OopsLeft);
        registry.RegisterLegacy(116, SystemPrimitives.SignalAtOopsLeftWordsLeft);
        registry.RegisterLegacy(128, SystemPrimitives.SomeInstance);
        registry.RegisterLegacy(129, SystemPrimitives.NextInstance);
        registry.RegisterLegacy(130, SystemPrimitives.BecomeForward);
        registry.RegisterLegacy(131, SystemPrimitives.InstanceCount);
        registry.RegisterLegacy(133, SystemPrimitives.ForceGC);
        registry.RegisterLegacy(139, SystemPrimitives.NextObject);

        return registry;
    }

    /// <summary>
    /// Creates a registry with new IPrimitive implementations where available.
    /// Falls back to legacy for primitives not yet migrated.
    /// </summary>
    /// <returns>Registry with new primitives enabled.</returns>
    public static PrimitiveRegistry CreateWithNewPrimitives()
    {
        // Start with all legacy primitives
        var registry = CreateWithLegacyPrimitives();

        // Override with new IPrimitive implementations for SmallInteger arithmetic (1-17)
        registry.Register(1, new NewPrimitives.SmallIntegerArithmetic.Add());
        registry.Register(2, new NewPrimitives.SmallIntegerArithmetic.Subtract());
        registry.Register(3, new NewPrimitives.SmallIntegerArithmetic.LessThan());
        registry.Register(4, new NewPrimitives.SmallIntegerArithmetic.GreaterThan());
        registry.Register(5, new NewPrimitives.SmallIntegerArithmetic.LessOrEqual());
        registry.Register(6, new NewPrimitives.SmallIntegerArithmetic.GreaterOrEqual());
        registry.Register(7, new NewPrimitives.SmallIntegerArithmetic.Equal());
        registry.Register(8, new NewPrimitives.SmallIntegerArithmetic.NotEqual());
        registry.Register(9, new NewPrimitives.SmallIntegerArithmetic.Multiply());
        registry.Register(10, new NewPrimitives.SmallIntegerArithmetic.Divide());
        registry.Register(11, new NewPrimitives.SmallIntegerArithmetic.Mod());
        registry.Register(12, new NewPrimitives.SmallIntegerArithmetic.Div());
        registry.Register(13, new NewPrimitives.SmallIntegerArithmetic.Quo());
        registry.Register(14, new NewPrimitives.SmallIntegerArithmetic.BitAnd());
        registry.Register(15, new NewPrimitives.SmallIntegerArithmetic.BitOr());
        registry.Register(16, new NewPrimitives.SmallIntegerArithmetic.BitXor());
        registry.Register(17, new NewPrimitives.SmallIntegerArithmetic.BitShift());

        // Override with new IPrimitive implementations for Float arithmetic (40-54)
        registry.Register(40, new NewPrimitives.FloatArithmetic.AsFloat());
        registry.Register(41, new NewPrimitives.FloatArithmetic.Add());
        registry.Register(42, new NewPrimitives.FloatArithmetic.Subtract());
        registry.Register(43, new NewPrimitives.FloatArithmetic.LessThan());
        registry.Register(44, new NewPrimitives.FloatArithmetic.GreaterThan());
        registry.Register(45, new NewPrimitives.FloatArithmetic.LessOrEqual());
        registry.Register(46, new NewPrimitives.FloatArithmetic.GreaterOrEqual());
        registry.Register(47, new NewPrimitives.FloatArithmetic.Equal());
        registry.Register(48, new NewPrimitives.FloatArithmetic.NotEqual());
        registry.Register(49, new NewPrimitives.FloatArithmetic.Multiply());
        registry.Register(50, new NewPrimitives.FloatArithmetic.Divide());
        registry.Register(51, new NewPrimitives.FloatArithmetic.Truncated());
        registry.Register(52, new NewPrimitives.FloatArithmetic.FractionPart());
        registry.Register(53, new NewPrimitives.FloatArithmetic.Exponent());
        registry.Register(54, new NewPrimitives.FloatArithmetic.TimesTwoPower());

        // Override with new IPrimitive implementations for Object primitives (60-75, 110-111)
        registry.Register(60, new NewPrimitives.SimpleObjectPrimitives.At());
        registry.Register(61, new NewPrimitives.SimpleObjectPrimitives.AtPut());
        registry.Register(62, new NewPrimitives.SimpleObjectPrimitives.Size());
        registry.Register(73, new NewPrimitives.SimpleObjectPrimitives.InstVarAt());
        registry.Register(74, new NewPrimitives.SimpleObjectPrimitives.InstVarAtPut());
        registry.Register(75, new NewPrimitives.SimpleObjectPrimitives.Hash());
        registry.Register(110, new NewPrimitives.SimpleObjectPrimitives.IdentityEquals());
        registry.Register(111, new NewPrimitives.SimpleObjectPrimitives.Class());

        // Override with new IPrimitive implementations for Control primitives (80-89)
        registry.Register(80, new NewPrimitives.ControlPrimitives.BlockCopy());
        registry.Register(81, new NewPrimitives.ControlPrimitives.Value());
        registry.Register(82, new NewPrimitives.ControlPrimitives.ValueWithArguments());
        registry.Register(83, new NewPrimitives.ControlPrimitives.Perform());
        registry.Register(84, new NewPrimitives.ControlPrimitives.PerformWith());
        registry.Register(85, new NewPrimitives.ControlPrimitives.Signal());
        registry.Register(86, new NewPrimitives.ControlPrimitives.Wait());
        registry.Register(87, new NewPrimitives.ControlPrimitives.Resume());
        registry.Register(88, new NewPrimitives.ControlPrimitives.Suspend());
        registry.Register(89, new NewPrimitives.ControlPrimitives.FlushCache());

        // Override with new IPrimitive implementations for LargeInteger arithmetic (21-37)
        registry.Register(21, new NewPrimitives.LargeIntegerArithmetic.Add());
        registry.Register(22, new NewPrimitives.LargeIntegerArithmetic.Subtract());
        registry.Register(23, new NewPrimitives.LargeIntegerArithmetic.LessThan());
        registry.Register(24, new NewPrimitives.LargeIntegerArithmetic.GreaterThan());
        registry.Register(25, new NewPrimitives.LargeIntegerArithmetic.LessOrEqual());
        registry.Register(26, new NewPrimitives.LargeIntegerArithmetic.GreaterOrEqual());
        registry.Register(27, new NewPrimitives.LargeIntegerArithmetic.Equal());
        registry.Register(28, new NewPrimitives.LargeIntegerArithmetic.NotEqual());
        registry.Register(29, new NewPrimitives.LargeIntegerArithmetic.Multiply());
        registry.Register(30, new NewPrimitives.LargeIntegerArithmetic.Divide());
        registry.Register(31, new NewPrimitives.LargeIntegerArithmetic.Mod());
        registry.Register(32, new NewPrimitives.LargeIntegerArithmetic.Div());
        registry.Register(33, new NewPrimitives.LargeIntegerArithmetic.Quo());
        registry.Register(34, new NewPrimitives.LargeIntegerArithmetic.BitAnd());
        registry.Register(35, new NewPrimitives.LargeIntegerArithmetic.BitOr());
        registry.Register(36, new NewPrimitives.LargeIntegerArithmetic.BitXor());
        registry.Register(37, new NewPrimitives.LargeIntegerArithmetic.BitShift());

        // Override with new IPrimitive implementations for remaining Object primitives (63-72, 77-79)
        registry.Register(63, new NewPrimitives.SimpleObjectPrimitives.StringAt());
        registry.Register(64, new NewPrimitives.SimpleObjectPrimitives.StringAtPut());
        registry.Register(65, new NewPrimitives.SimpleObjectPrimitives.Next());
        registry.Register(66, new NewPrimitives.SimpleObjectPrimitives.NextPut());
        registry.Register(67, new NewPrimitives.SimpleObjectPrimitives.AtEnd());
        registry.Register(68, new NewPrimitives.SimpleObjectPrimitives.ObjectAt());
        registry.Register(69, new NewPrimitives.SimpleObjectPrimitives.ObjectAtPut());
        registry.Register(70, new NewPrimitives.SimpleObjectPrimitives.New());
        registry.Register(71, new NewPrimitives.SimpleObjectPrimitives.NewWithSize());
        registry.Register(72, new NewPrimitives.SimpleObjectPrimitives.Become());
        registry.Register(77, new NewPrimitives.SimpleObjectPrimitives.SomeInstance());
        registry.Register(78, new NewPrimitives.SimpleObjectPrimitives.NextInstance());
        registry.Register(79, new NewPrimitives.SimpleObjectPrimitives.NewMethodHeader());

        // Override with new IPrimitive implementations for Input primitives (90-95)
        registry.Register(90, new NewPrimitives.InputOperations.MousePoint());
        registry.Register(91, new NewPrimitives.InputOperations.CursorLocPut());
        registry.Register(92, new NewPrimitives.InputOperations.CursorLink());
        registry.Register(93, new NewPrimitives.InputOperations.InputSemaphore());
        registry.Register(94, new NewPrimitives.InputOperations.SampleInterval());
        registry.Register(95, new NewPrimitives.InputOperations.InputWord());

        // Override with new IPrimitive implementations for Graphics primitives (96-104)
        registry.Register(96, new NewPrimitives.GraphicsOperations.CopyBits());
        registry.Register(97, new NewPrimitives.GraphicsOperations.Snapshot());
        registry.Register(98, new NewPrimitives.GraphicsOperations.OopsLeft());
        registry.Register(99, new NewPrimitives.GraphicsOperations.CoreLeft());
        registry.Register(100, new NewPrimitives.GraphicsOperations.TimeWords());
        registry.Register(101, new NewPrimitives.GraphicsOperations.BeDisplay());
        registry.Register(102, new NewPrimitives.GraphicsOperations.BeCursor());
        registry.Register(103, new NewPrimitives.GraphicsOperations.Beep());
        registry.Register(104, new NewPrimitives.GraphicsOperations.ClipboardText());

        // Override with new IPrimitive implementations for System primitives (112-139)
        registry.Register(112, new NewPrimitives.SystemOperations.CoreLeft());
        registry.Register(113, new NewPrimitives.SystemOperations.Quit());
        registry.Register(114, new NewPrimitives.SystemOperations.ExitToDebugger());
        registry.Register(115, new NewPrimitives.SystemOperations.OopsLeft());
        registry.Register(116, new NewPrimitives.SystemOperations.SignalAtOopsLeftWordsLeft());
        registry.Register(128, new NewPrimitives.SystemOperations.SomeInstance());
        registry.Register(129, new NewPrimitives.SystemOperations.NextInstance());
        registry.Register(130, new NewPrimitives.SystemOperations.BecomeForward());
        registry.Register(131, new NewPrimitives.SystemOperations.InstanceCount());
        registry.Register(133, new NewPrimitives.SystemOperations.ForceGC());
        registry.Register(139, new NewPrimitives.SystemOperations.NextObject());

        return registry;
    }
}
