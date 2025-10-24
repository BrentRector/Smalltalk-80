using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Point primitive operations (Primitive 18).
/// Handles fast Point creation from SmallIntegers.
/// </summary>
public static class PointPrimitives
{
    // Primitive 18: Point creation via @ (x @ y)
    // Fast path for creating Points when both x and y are SmallIntegers
    public static SmalltalkOOP MakePoint(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Validate argument count
        if (args == null || args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return GlobalRegistry.NilOOP;
        }

        var xOop = receiver;
        var yOop = args[0];

        // Both x and y must be SmallIntegers for primitive to succeed
        if (!xOop.IsSmallInteger || !yOop.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return GlobalRegistry.NilOOP;
        }

        // Get Point class from global registry
        if (!GlobalRegistry.TryGet("class:Point", out SmalltalkOOP pointClassOOP))
        {
            ctx.PrimitiveSuccess = false;
            return GlobalRegistry.NilOOP;
        }

        // Allocate new Point instance (no indexed fields, just x and y instance variables)
        var pointOOP = ctx.AllocateObject("class:Point", 0);

        // Get field indices for x and y
        int xIndex = ctx.GetFieldIndex("class:Point", "x");
        int yIndex = ctx.GetFieldIndex("class:Point", "y");

        // Set instance variables
        ctx.HeapManager.SetInstanceVariable(pointOOP, xIndex, xOop);
        ctx.HeapManager.SetInstanceVariable(pointOOP, yIndex, yOop);

        // Signal success
        ctx.PrimitiveSuccess = true;
        return pointOOP;
    }
}
