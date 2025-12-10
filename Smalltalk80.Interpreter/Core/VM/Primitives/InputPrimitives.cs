using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Input primitives for mouse, keyboard, and events (90-95) using new IPrimitive interface.
/// </summary>
public static class InputOperations
{
    /// <summary>Primitive 90: mousePoint - Get current mouse position as Point</summary>
    public class MousePoint : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0)
                return PrimitiveResult.Fail(context.Metadata!);

            // Default to origin (0,0) for headless mode
            int x = 0, y = 0;

            // Query platform bridge for native cursor position
            if (context.Platform != null)
            {
                (x, y) = context.Platform.GetMousePosition();
            }

            // Get Point class OOP
            var pointClassOOP = context.RuntimeHeapAccess?.GetClass("Point");
            if (pointClassOOP == null || pointClassOOP == context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Allocate Point instance with 2 instance variables (x, y)
            SmalltalkOOP pointOOP = context.HeapManager.AllocateInstanceOf(pointClassOOP.Value, 0);

            // Set x and y coordinates
            context.HeapManager.SetInstanceVariable(pointOOP, 0, SmalltalkOOP.FromSmallInteger(x));
            context.HeapManager.SetInstanceVariable(pointOOP, 1, SmalltalkOOP.FromSmallInteger(y));

            return PrimitiveResult.Ok(pointOOP);
        }
    }

    /// <summary>Primitive 91: cursorLocPut: - Set cursor position</summary>
    public class CursorLocPut : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1)
                return PrimitiveResult.Fail(context.Metadata!);

            var point = context.Arguments[0];
            if (point.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Extract x and y coordinates from Point instance variables
            SmalltalkOOP xOOP = context.HeapManager.GetInstanceVariable(point, 0);
            SmalltalkOOP yOOP = context.HeapManager.GetInstanceVariable(point, 1);

            if (!xOOP.IsSmallInteger || !yOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int x = xOOP.AsSmallInteger();
            int y = yOOP.AsSmallInteger();

            // Move native cursor via platform bridge
            if (context.Platform != null)
            {
                context.Platform.SetCursorPosition(x, y);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 92: cursorLink: - Link cursor to Form</summary>
    public class CursorLink : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var cursorOOP = context.Arguments[0];
            if (cursorOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // Stub implementation - accept cursor and succeed
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 93: inputSemaphore: - Set input event semaphore</summary>
    public class InputSemaphore : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var semaphoreOOP = context.Arguments[0];

            // SmallIntegers not allowed (except nil)
            if (semaphoreOOP.IsSmallInteger && semaphoreOOP != context.HeapManager.GetNil())
                return PrimitiveResult.Fail(context.Metadata!);

            // Register semaphore with platform event system
            if (context.Platform != null)
            {
                context.Platform.SetInputSemaphore(semaphoreOOP);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 94: sampleInterval: - Set input sampling interval</summary>
    public class SampleInterval : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var intervalOOP = context.Arguments[0];
            if (!intervalOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int intervalMs = intervalOOP.AsSmallInteger();

            // Configure polling interval
            if (context.Platform != null)
            {
                context.Platform.SetSampleInterval(intervalMs);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 95: inputWord - Read next input word (Blue Book format)</summary>
    /// <remarks>
    /// Returns next input word as a packed SmallInteger.
    /// Format: type (bits 12-15) | param (bits 0-11)
    /// Types: 1=mouseX, 2=mouseY, 3=keyDown, 4=keyUp, 6=endOfEvent
    /// Returns 0 if no events available.
    ///
    /// This is the original Blue Book format:
    ///   word _ self primInputWord.
    ///   type _ word bitShift: -12.
    ///   param _ word bitAnd: 4095.
    /// </remarks>
    public class InputWord : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Get packed input word from platform (Blue Book format: type<<12 | param)
            int word = context.Platform?.GetInputWord() ?? 0;
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(word));
        }
    }

    /// <summary>Primitive 202: bitState - Get button state from simulated hardware register</summary>
    /// <remarks>
    /// Reads the current button state atomically from the simulated hardware register.
    /// Returns integer with Blue Book encoding: red=4, yellow=2, blue=1.
    /// This enables tight polling loops (like PopUpMenu) to detect button releases
    /// without requiring InputProcess to run.
    ///
    /// The simulated hardware register is a packed long that the UI thread updates
    /// atomically using Interlocked.Exchange. The Smalltalk thread reads it atomically
    /// using Interlocked.Read. This emulates the memory-mapped registers that the
    /// original Blue Book hardware had.
    ///
    /// Call chain: redButtonPressed -> buttons -> mouseButtons -> bitState -> primitive 202
    /// </remarks>
    public class BitState : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0)
                return PrimitiveResult.Fail(context.Metadata!);

            int buttons = context.Platform?.GetButtonState() ?? 0;
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(buttons));
        }
    }
}
