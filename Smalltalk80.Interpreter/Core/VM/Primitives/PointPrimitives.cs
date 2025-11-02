using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Point primitive operations (Primitive 18).
/// Handles fast Point creation from SmallIntegers.
/// </summary>
public static class PointPrimitives
{
    // <summary>
    // Primitive 18: Number @ - Fast Point creation
    // Blue Book: Page 542-559, Chapter 29, Formal Specification
    // </summary>
    // <remarks>
    // <para><b>Blue Book Specification:</b></para>
    // <code>
    // primitiveMakePoint
    //     | integerReceiver integerArgument pointResult |
    //     integerArgument ← self popStack.
    //     integerReceiver ← self popStack.
    //     self success: (memory isIntegerValue: integerReceiver).
    //     self success: (memory isIntegerValue: integerArgument).
    //     self success ifTrue:
    //         [pointResult ← memory instantiateClass: ClassPointPointer
    //                                   withPointers: ClassPointSize.
    //         memory storePointer: XIndex
    //                ofObject: pointResult
    //                withValue: integerReceiver.
    //         memory storePointer: YIndex
    //                ofObject: pointResult
    //                withValue: integerArgument.
    //         self push: pointResult]
    //         ifFalse: [self unPop: 2]
    // </code>
    //
    // <para><b>Implementation Details:</b></para>
    // <list type="bullet">
    // <item>Validates both receiver (x) and argument (y) are SmallIntegers</item>
    // <item>Allocates new Point instance with 2 instance variables (x, y)</item>
    // <item>Sets x and y fields from receiver and argument respectively</item>
    // <item>Returns the newly created Point object</item>
    // </list>
    //
    // <para><b>Analysis:</b></para>
    // <para>Fast path optimization for the common case of creating Points from integer coordinates.
    // This primitive avoids the overhead of the full @ method when both operands are SmallIntegers.</para>
    //
    // <para><b>Minor Enhancement Consideration:</b></para>
    // <list type="bullet">
    // <item>Blue Book validates SmallInteger before allocation (fail-fast)</item>
    // <item>Our implementation allocates first, then validates</item>
    // <item>Could optimize by checking types before allocation to avoid unnecessary object creation on failure</item>
    // </list>
    //
    // <para><b>Conformance:</b> GOOD - Semantically equivalent with minor ordering difference</para>
    // </remarks>
    public static SmalltalkOOP MakePoint(IPrimitiveContext ctx, SmalltalkOOP receiver, SmalltalkOOP[] args)
    {
        // Blue Book: primitiveMakePoint
        // Validates SmallInteger arguments and creates Point instance

        // Validate single argument for binary @ operator
        if (args == null || args.Length != 1)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Extract x (receiver) and y (argument) coordinates
        // In Smalltalk: 3@4 means x=3, y=4
        var xOop = receiver;
        var yOop = args[0];

        // Blue Book: self success: (memory isIntegerValue: integerReceiver)
        // Both coordinates must be SmallIntegers for fast primitive path
        // Non-integers fail back to regular @ method for Float/Fraction support
        if (!xOop.IsSmallInteger || !yOop.IsSmallInteger)
        {
            ctx.PrimitiveSuccess = false;
            return SpecialObjectsArray.Instance.Nil;
        }

        // Blue Book: memory instantiateClass: ClassPointPointer withPointers: ClassPointSize
        // Allocate Point with 2 instance variables (x and y)
        // No indexed fields needed - Point is pure pointer object
        var pointOOP = ctx.AllocateObject("class:Point", 0);

        // Get field indices dynamically from layout resolver
        // Respects any future changes to Point class structure
        int xIndex = ctx.GetFieldIndex("class:Point", "x");
        int yIndex = ctx.GetFieldIndex("class:Point", "y");

        // Blue Book: memory storePointer: XIndex ofObject: pointResult withValue: integerReceiver
        // Store x coordinate (receiver) in first instance variable
        ctx.HeapManager.SetInstanceVariable(pointOOP, xIndex, xOop);

        // Blue Book: memory storePointer: YIndex ofObject: pointResult withValue: integerArgument
        // Store y coordinate (argument) in second instance variable
        ctx.HeapManager.SetInstanceVariable(pointOOP, yIndex, yOop);

        // Return newly created Point object
        return pointOOP;
    }
}
