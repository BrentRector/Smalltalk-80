using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Font-related primitives for text rendering operations.
///
/// Note: Most StrikeFont operations in Smalltalk-80 are implemented in Smalltalk
/// using direct instance variable access and xTable lookups. The actual glyph
/// rendering is done via BitBlt (primitive 96).
///
/// These primitives provide optional performance optimizations for text-intensive
/// operations like CharacterScanner.
/// </summary>
public static class FontPrimitives
{
    /// <summary>
    /// Primitive for StrikeFont>>widthOf: character
    /// Returns the pixel width of a character from the xTable.
    /// This is an optional optimization - Smalltalk code can compute this directly.
    /// </summary>
    public class CharacterWidth : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (context.LayoutResolver == null) return PrimitiveResult.Fail();

            // Get character value
            var charArg = context.Arguments[0];
            int charValue;

            if (charArg.IsSmallInteger)
            {
                charValue = charArg.AsSmallInteger();
            }
            else
            {
                // Character object - get its value
                var className = context.HeapManager.GetClassName(charArg);
                if (className != "Character") return PrimitiveResult.Fail();

                // Character stores its value in first inst var
                charValue = context.HeapManager.GetInstanceVariable(charArg, 0).AsSmallInteger();
            }

            // Get StrikeFont fields
            int minAsciiIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "minAscii");
            int maxAsciiIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "maxAscii");
            int xTableIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "xTable");

            var minAsciiOOP = context.HeapManager.GetInstanceVariable(context.Receiver, minAsciiIdx);
            var maxAsciiOOP = context.HeapManager.GetInstanceVariable(context.Receiver, maxAsciiIdx);
            var xTableOOP = context.HeapManager.GetInstanceVariable(context.Receiver, xTableIdx);

            if (!minAsciiOOP.IsSmallInteger || !maxAsciiOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

            int minAscii = minAsciiOOP.AsSmallInteger();
            int maxAscii = maxAsciiOOP.AsSmallInteger();

            // Clamp character to valid range
            if (charValue < minAscii) charValue = minAscii;
            if (charValue > maxAscii) charValue = maxAscii;

            // Get width from xTable
            // xTable[i] gives left edge of char i, width = xTable[i+1] - xTable[i]
            int index = charValue - minAscii;
            var leftEdge = context.HeapManager.GetIndexedOOP(xTableOOP, index);
            var rightEdge = context.HeapManager.GetIndexedOOP(xTableOOP, index + 1);

            if (!leftEdge.IsSmallInteger || !rightEdge.IsSmallInteger)
                return PrimitiveResult.Fail();

            int width = rightEdge.AsSmallInteger() - leftEdge.AsSmallInteger();
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(width));
        }
    }

    /// <summary>
    /// Primitive for StrikeFont>>widthOfString: aString
    /// Returns the total pixel width of a string.
    /// This is an optional optimization - Smalltalk code can iterate characters directly.
    /// </summary>
    public class StringWidth : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (context.LayoutResolver == null) return PrimitiveResult.Fail();

            var stringArg = context.Arguments[0];
            if (stringArg.IsSmallInteger) return PrimitiveResult.Fail();

            // Get string characters
            int stringSize = context.HeapManager.GetIndexedSize(stringArg);

            // Get StrikeFont fields
            int minAsciiIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "minAscii");
            int maxAsciiIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "maxAscii");
            int xTableIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "xTable");

            var minAsciiOOP = context.HeapManager.GetInstanceVariable(context.Receiver, minAsciiIdx);
            var maxAsciiOOP = context.HeapManager.GetInstanceVariable(context.Receiver, maxAsciiIdx);
            var xTableOOP = context.HeapManager.GetInstanceVariable(context.Receiver, xTableIdx);

            if (!minAsciiOOP.IsSmallInteger || !maxAsciiOOP.IsSmallInteger)
                return PrimitiveResult.Fail();

            int minAscii = minAsciiOOP.AsSmallInteger();
            int maxAscii = maxAsciiOOP.AsSmallInteger();

            // Calculate total width
            int totalWidth = 0;
            for (int i = 0; i < stringSize; i++)
            {
                int charValue = context.HeapManager.GetIndexedByte(stringArg, i);

                // Clamp character to valid range
                if (charValue < minAscii) charValue = minAscii;
                if (charValue > maxAscii) charValue = maxAscii;

                // Get width from xTable
                int index = charValue - minAscii;
                var leftEdge = context.HeapManager.GetIndexedOOP(xTableOOP, index);
                var rightEdge = context.HeapManager.GetIndexedOOP(xTableOOP, index + 1);

                if (!leftEdge.IsSmallInteger || !rightEdge.IsSmallInteger)
                    return PrimitiveResult.Fail();

                totalWidth += rightEdge.AsSmallInteger() - leftEdge.AsSmallInteger();
            }

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(totalWidth));
        }
    }

    /// <summary>
    /// Primitive for StrikeFont>>ascent
    /// Returns the font ascent (height above baseline).
    /// Direct field access optimization.
    /// </summary>
    public class FontAscent : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (context.LayoutResolver == null) return PrimitiveResult.Fail();

            int ascentIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "ascent");
            var ascentOOP = context.HeapManager.GetInstanceVariable(context.Receiver, ascentIdx);

            return PrimitiveResult.Ok(ascentOOP);
        }
    }

    /// <summary>
    /// Primitive for StrikeFont>>descent
    /// Returns the font descent (height below baseline).
    /// Direct field access optimization.
    /// </summary>
    public class FontDescent : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail();
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail();
            if (context.LayoutResolver == null) return PrimitiveResult.Fail();

            int descentIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "descent");
            var descentOOP = context.HeapManager.GetInstanceVariable(context.Receiver, descentIdx);

            return PrimitiveResult.Ok(descentOOP);
        }
    }
}
