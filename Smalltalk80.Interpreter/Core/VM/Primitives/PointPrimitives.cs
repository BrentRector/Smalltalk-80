using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Point primitives using new IPrimitive interface.
/// </summary>
public static class PointPrimitives
{
    /// <summary>Primitive 18: Number @ - Fast Point creation</summary>
    public class MakePoint : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var xOop = context.Receiver;
            var yOop = context.Arguments[0];

            // Both coordinates must be SmallIntegers
            if (!xOop.IsSmallInteger || !yOop.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Allocate Point with 2 instance variables
            var pointOOP = context.AllocateObject("Point", 0);

            // Get field indices from layout resolver
            if (context.LayoutResolver == null) return PrimitiveResult.Fail(context.Metadata!);
            int xIndex = context.LayoutResolver.GetFieldIndex("class:Point", "x");
            int yIndex = context.LayoutResolver.GetFieldIndex("class:Point", "y");

            // Store coordinates
            context.HeapManager.SetInstanceVariable(pointOOP, xIndex, xOop);
            context.HeapManager.SetInstanceVariable(pointOOP, yIndex, yOop);

            return PrimitiveResult.Ok(pointOOP);
        }
    }
}
