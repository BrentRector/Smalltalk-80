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
    // CharacterScanner ivars (14-26): lastIndex, xTable, stopConditions, text, textStyle, leftMargin, rightMargin, font, line, runStopIndex, spaceCount, spaceWidth, outputMedium
    // CharacterBlockScanner adds (27-32): characterPoint, characterIndex, lastCharacter, lastCharacterExtent, lastSpaceOrTabExtent, nextLeftMargin
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
        // Set to true to enable detailed logging for debugging
        private const bool DebugLogging = true;

        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            // Validate argument count
            if (context.Arguments.Length != 6)
                return PrimitiveResult.Fail(context.Metadata!);

            // Receiver must be a CharacterScanner (not SmallInteger)
            if (context.Receiver.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Get arguments
            var startIndexOOP = context.Arguments[0];
            var stopIndexOOP = context.Arguments[1];
            var sourceStringOOP = context.Arguments[2];
            var rightXOOP = context.Arguments[3];
            var stopsOOP = context.Arguments[4];
            var displayingOOP = context.Arguments[5];

            // Validate integer arguments
            if (!startIndexOOP.IsSmallInteger || !stopIndexOOP.IsSmallInteger || !rightXOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Validate object arguments
            if (sourceStringOOP.IsSmallInteger || stopsOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // If displaying is true, fail to let Smalltalk code handle copyBits
            if (displayingOOP == context.Metadata!.KnownObjects.True)
                return PrimitiveResult.Fail(context.Metadata!);

            int startIndex = startIndexOOP.AsSmallInteger();
            int stopIndex = stopIndexOOP.AsSmallInteger();
            int rightX = rightXOOP.AsSmallInteger();

            // Validate indices
            if (startIndex < 1 || stopIndex < startIndex)
                return PrimitiveResult.Fail(context.Metadata!);

            int stringSize = context.HeapManager.GetIndexedSize(sourceStringOOP);
            if (stopIndex > stringSize)
                return PrimitiveResult.Fail(context.Metadata!);

            // Get receiver's instance variables
            var destXOOP = context.HeapManager.GetInstanceVariable(context.Receiver, BITBLT_DESTX_INDEX);
            var xTableOOP = context.HeapManager.GetInstanceVariable(context.Receiver, SCANNER_XTABLE_INDEX);
            var stopConditionsOOP = context.HeapManager.GetInstanceVariable(context.Receiver, 16); // stopConditions

            if (!destXOOP.IsSmallInteger || xTableOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int destX = destXOOP.AsSmallInteger();
            int initialDestX = destX;

            if (DebugLogging)
            {
                // Extract first few characters of string for debugging
                int previewLen = Math.Min(20, stringSize);
                var chars = new char[previewLen];
                for (int i = 0; i < previewLen; i++)
                    chars[i] = (char)context.HeapManager.GetIndexedByte(sourceStringOOP, i);
                string preview = new string(chars);
                Console.WriteLine($"[Prim103] Start: startIndex={startIndex}, stopIndex={stopIndex}, rightX={rightX}, destX={destX}, str=\"{preview}...\"");
            }

            // Initialize lastIndex
            int lastIndex = startIndex;

            // Main scanning loop
            while (lastIndex <= stopIndex)
            {
                // Get character at lastIndex (0-based index into string)
                int ascii = context.HeapManager.GetIndexedByte(sourceStringOOP, lastIndex - 1);

                // Check stop conditions for this character
                // Smalltalk: (stopConditions at: ascii + 1) - convert to 0-based
                var stopCondition = context.HeapManager.GetIndexedOOP(stopConditionsOOP, ascii);
                if (stopCondition != context.Metadata!.KnownObjects.Nil)
                {
                    // Update instance variables before returning
                    // CRITICAL: Must set destX so stop condition handlers (like CompositionScanner>>space) read correct value
                    context.HeapManager.SetInstanceVariable(context.Receiver, SCANNER_LASTINDEX_INDEX,
                        SmalltalkOOP.FromSmallInteger(lastIndex));
                    context.HeapManager.SetInstanceVariable(context.Receiver, BITBLT_DESTX_INDEX,
                        SmalltalkOOP.FromSmallInteger(destX));

                    // Return stops at: (ascii + 1) - 0-based index is ascii
                    var result = context.HeapManager.GetIndexedOOP(stopsOOP, ascii);

                    if (DebugLogging)
                        Console.WriteLine($"[Prim103] StopCondition: ascii={ascii} char='{(char)ascii}' lastIndex={lastIndex} destX={destX}");

                    return PrimitiveResult.Ok(result);
                }

                // Get character width from xTable
                // Smalltalk: xTable at: ascii + 1 (left edge), xTable at: ascii + 2 (right edge)
                // Convert to 0-based: ascii, ascii + 1
                var sourceXOOP = context.HeapManager.GetIndexedOOP(xTableOOP, ascii);
                var nextXOOP = context.HeapManager.GetIndexedOOP(xTableOOP, ascii + 1);

                if (!sourceXOOP.IsSmallInteger || !nextXOOP.IsSmallInteger)
                    return PrimitiveResult.Fail(context.Metadata!);

                int sourceX = sourceXOOP.AsSmallInteger();
                int width = nextXOOP.AsSmallInteger() - sourceX;
                int nextDestX = destX + width;

                if (DebugLogging && lastIndex <= startIndex + 5)
                    Console.WriteLine($"[Prim103] Char: idx={lastIndex} ascii={ascii} '{(char)ascii}' sourceX={sourceX} width={width} destX={destX} nextDestX={nextDestX}");

                // Check if we've crossed the rightX limit
                if (nextDestX > rightX)
                {
                    // Update receiver's instance variables
                    context.HeapManager.SetInstanceVariable(context.Receiver, SCANNER_LASTINDEX_INDEX,
                        SmalltalkOOP.FromSmallInteger(lastIndex));
                    context.HeapManager.SetInstanceVariable(context.Receiver, BITBLT_DESTX_INDEX,
                        SmalltalkOOP.FromSmallInteger(destX));
                    context.HeapManager.SetInstanceVariable(context.Receiver, BITBLT_SOURCEX_INDEX,
                        SmalltalkOOP.FromSmallInteger(sourceX));
                    context.HeapManager.SetInstanceVariable(context.Receiver, BITBLT_WIDTH_INDEX,
                        SmalltalkOOP.FromSmallInteger(width));

                    if (DebugLogging)
                        Console.WriteLine($"[Prim103] CrossedX: lastIndex={lastIndex} destX={destX} (started at {initialDestX}, rightX={rightX})");

                    // Return stops at: CrossedX (258 in Smalltalk, index 257 in 0-based)
                    var crossedXResult = context.HeapManager.GetIndexedOOP(stopsOOP, CROSSED_X_INDEX - 1);
                    return PrimitiveResult.Ok(crossedXResult);
                }

                // Advance destX and lastIndex
                destX = nextDestX;
                lastIndex++;
            }

            // Reached end of run
            lastIndex = stopIndex;

            context.HeapManager.SetInstanceVariable(context.Receiver, SCANNER_LASTINDEX_INDEX,
                SmalltalkOOP.FromSmallInteger(lastIndex));
            context.HeapManager.SetInstanceVariable(context.Receiver, BITBLT_DESTX_INDEX,
                SmalltalkOOP.FromSmallInteger(destX));

            if (DebugLogging)
                Console.WriteLine($"[Prim103] EndOfRun: lastIndex={lastIndex} destX={destX}");

            // Return stops at: EndOfRun (257 in Smalltalk, index 256 in 0-based)
            var endOfRunResult = context.HeapManager.GetIndexedOOP(stopsOOP, END_OF_RUN_INDEX - 1);
            return PrimitiveResult.Ok(endOfRunResult);
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
