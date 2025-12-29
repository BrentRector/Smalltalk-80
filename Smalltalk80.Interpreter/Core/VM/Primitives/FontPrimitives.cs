using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.HeapManager;

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
    // CharacterScanner instance variable indices (inherited from BitBlt + own)
    // BitBlt ivars (0-13): destForm, sourceForm, halftoneForm, combinationRule, destX, destY, width, height, sourceX, sourceY, clipX, clipY, clipWidth, clipHeight
    // CharacterScanner ivars (14-25): lastIndex, xTable, stopConditions, text, textStyle, leftMargin, rightMargin, font, line, runStopIndex, spaceCount, spaceWidth, outputMedium
    private const int BITBLT_DESTX_INDEX = 4;
    private const int BITBLT_WIDTH_INDEX = 6;
    private const int BITBLT_SOURCEX_INDEX = 8;
    private const int SCANNER_LASTINDEX_INDEX = 14;
    private const int SCANNER_XTABLE_INDEX = 15;

    // Stop condition array indices (Smalltalk 1-based)
    private const int END_OF_RUN_INDEX = 257;
    private const int CROSSED_X_INDEX = 258;

    /// <summary>
    /// Primitive 103: scanCharactersFrom:to:in:rightX:stopConditions:displaying:
    ///
    /// The inner loop of text display in CharacterScanner. Scans characters from
    /// startIndex to stopIndex, checking stop conditions and accumulating character widths.
    ///
    /// NOTE: This primitive is currently disabled (always fails) to use the Smalltalk
    /// fallback implementation. The caret positioning bug needs further investigation
    /// as the initial C# implementation caused text display issues.
    ///
    /// Updates receiver's instance variables: lastIndex, destX, sourceX, width
    /// Returns a stop condition symbol from the stops array.
    ///
    /// Arguments:
    ///   0: startIndex (SmallInteger, 1-based)
    ///   1: stopIndex (SmallInteger, 1-based)
    ///   2: sourceString (String/ByteArray)
    ///   3: rightX (SmallInteger) - x position limit
    ///   4: stops (Array) - stop condition symbols to return
    ///   5: displaying (Boolean) - if true, fail to let Smalltalk do copyBits
    /// </summary>
    public class ScanCharacters : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Always fail to use Smalltalk fallback - the C# implementation needs debugging
            // The caret position bug requires further investigation of the Smalltalk code
            return PrimitiveResult.Fail(context.Metadata!);
        }
    }
    /// <summary>
    /// Primitive for StrikeFont>>widthOf: character
    /// Returns the pixel width of a character from the xTable.
    /// This is an optional optimization - Smalltalk code can compute this directly.
    /// </summary>
    public class CharacterWidth : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (context.LayoutResolver == null) return PrimitiveResult.Fail(context.Metadata!);

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
                if (className != "Character") return PrimitiveResult.Fail(context.Metadata!);

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
                return PrimitiveResult.Fail(context.Metadata!);

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
                return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (context.LayoutResolver == null) return PrimitiveResult.Fail(context.Metadata!);

            var stringArg = context.Arguments[0];
            if (stringArg.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

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
                return PrimitiveResult.Fail(context.Metadata!);

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
                    return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (context.LayoutResolver == null) return PrimitiveResult.Fail(context.Metadata!);

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
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);
            if (context.Receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (context.LayoutResolver == null) return PrimitiveResult.Fail(context.Metadata!);

            int descentIdx = context.LayoutResolver.GetFieldIndex("StrikeFont", "descent");
            var descentOOP = context.HeapManager.GetInstanceVariable(context.Receiver, descentIdx);

            return PrimitiveResult.Ok(descentOOP);
        }
    }
}
