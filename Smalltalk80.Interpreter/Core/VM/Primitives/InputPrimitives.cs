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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Default to origin (0,0) for headless mode
            int x = 0, y = 0;

            // Query platform bridge for native cursor position
            if (context.Platform != null)
            {
                (x, y) = context.Platform.GetMousePosition();
            }

            // Get Point class OOP
            var pointClassOOP = context.RuntimeHeapAccess?.GetClass("Point");
            if (pointClassOOP == null || pointClassOOP == heapManager.GetNil())
                return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            var point = context.Arguments[0];
            if (point.IsSmallInteger) return PrimitiveResult.Fail();

            // Extract x and y coordinates from Point instance variables
            SmalltalkOOP xOOP = context.HeapManager.GetInstanceVariable(point, 0);
            SmalltalkOOP yOOP = context.HeapManager.GetInstanceVariable(point, 1);

            if (!xOOP.IsSmallInteger || !yOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            var cursorOOP = context.Arguments[0];
            if (cursorOOP.IsSmallInteger) return PrimitiveResult.Fail();

            // Stub implementation - accept cursor and succeed
            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 93: inputSemaphore: - Set input event semaphore</summary>
    public class InputSemaphore : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            var semaphoreOOP = context.Arguments[0];

            // SmallIntegers not allowed (except nil)
            if (semaphoreOOP.IsSmallInteger && semaphoreOOP != heapManager.GetNil())
                return PrimitiveResult.Fail();

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();

            var intervalOOP = context.Arguments[0];
            if (!intervalOOP.IsSmallInteger) return PrimitiveResult.Fail();

            int intervalMs = intervalOOP.AsSmallInteger();

            // Configure polling interval
            if (context.Platform != null)
            {
                context.Platform.SetSampleInterval(intervalMs);
            }

            return PrimitiveResult.Ok(context.Receiver);
        }
    }

    /// <summary>Primitive 95: inputWord - Read raw input event word</summary>
    public class InputWord : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();

            // Default to 0 (no events) for headless mode
            int inputWord = 0;

            // Get next input event from queue
            if (context.Platform != null)
            {
                inputWord = context.Platform.GetInputWord();
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(inputWord));
        }
    }
}
