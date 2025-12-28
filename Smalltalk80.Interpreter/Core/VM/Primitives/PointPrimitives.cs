using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Point primitives using new IPrimitive interface.
///
/// OPTIMIZATION (2025-12-28): Zero string lookups for Point creation.
/// - Uses cached PointClassOOP instead of string-based class lookup
/// - Uses VMConstants.POINT_X_INDEX/POINT_Y_INDEX instead of LayoutResolver lookup
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

            // Allocate Point using cached class OOP (no string lookup)
            var pointOOP = context.HeapManager.AllocateInstanceOf(context.Metadata!.PointClassOOP, 0);

            // Store coordinates using Blue Book field indices (no LayoutResolver lookup)
            context.HeapManager.SetInstanceVariable(pointOOP, VMConstants.POINT_X_INDEX, xOop);
            context.HeapManager.SetInstanceVariable(pointOOP, VMConstants.POINT_Y_INDEX, yOop);

            return PrimitiveResult.Ok(pointOOP);
        }
    }
}
