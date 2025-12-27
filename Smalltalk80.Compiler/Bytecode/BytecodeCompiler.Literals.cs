using System;
using System.Collections.Generic;
using Smalltalk80.Compiler.Generated;
using Smalltalk80.Compiler.Model;

namespace Smalltalk80.Compiler.Bytecode
{
    /// <summary>
    /// Literal compilation methods for the BytecodeCompiler.
    /// Handles number, string, symbol, character, array, and byte array literals.
    /// </summary>
    public partial class BytecodeCompiler
    {
        /// <summary>
        /// Parses a Smalltalk number literal, supporting radix notation (e.g., 8r377, 16rFF),
        /// decimal notation, floating point, and scaled decimals.
        /// </summary>
        /// <remarks>
        /// Smalltalk supports radix notation for integers in any base from 2 to 36:
        /// <list type="bullet">
        ///   <item><description><c>2r1010</c> = 10 (binary)</description></item>
        ///   <item><description><c>8r377</c> = 255 (octal)</description></item>
        ///   <item><description><c>16rFF</c> = 255 (hexadecimal)</description></item>
        ///   <item><description><c>36rZ</c> = 35 (base 36)</description></item>
        /// </list>
        /// Format: [radix]r[digits] where radix is 2-36 and digits use 0-9 and A-Z.
        ///
        /// Scaled decimals use the format: [mantissa]s[scale]
        /// Example: <c>123s2</c> = 1.23 (divide by 10^2)
        /// </remarks>
        /// <param name="text">The number literal text to parse.</param>
        /// <param name="value">The parsed value (int or double) if successful.</param>
        /// <returns>True if parsing succeeded, false otherwise.</returns>
        private static bool TryParseSmalltalkNumber(string text, out object? value)
        {
            value = null;

            // Check for radix notation: <radix>r<digits>
            var rIndex = text.IndexOfAny(['r', 'R']);
            if (rIndex > 0 && rIndex < text.Length - 1)
            {
                var radixPart = text[..rIndex];
                var digitsPart = text[(rIndex + 1)..];

                if (int.TryParse(radixPart, out int radix) && radix >= 2 && radix <= 36)
                {
                    try
                    {
                        int result = Convert.ToInt32(digitsPart, radix);
                        value = result;
                        return true;
                    }
                    catch (Exception ex) when (ex is FormatException or OverflowException or ArgumentException)
                    {
                        // Invalid digit for the specified radix, or number too large
                        return false;
                    }
                }
            }

            // Check for scaled decimal: <digits>s<scale>
            var sIndex = text.IndexOf('s');
            if (sIndex > 0 && sIndex < text.Length - 1)
            {
                var intPart = text[..sIndex];
                var scalePart = text[(sIndex + 1)..];
                if (double.TryParse(intPart, out double intVal) && int.TryParse(scalePart, out int scale))
                {
                    value = intVal / Math.Pow(10, scale);
                    return true;
                }
            }

            // Try parsing as regular integer
            if (int.TryParse(text, out int intResult))
            {
                value = intResult;
                return true;
            }

            // Try parsing as floating point
            if (double.TryParse(text, out double floatResult))
            {
                value = floatResult;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Compiles a literal value to bytecode.
        /// </summary>
        private void CompileLiteral(Smalltalk80Parser.LiteralContext literal)
        {
            if (literal.numberLiteral() != null)
            {
                var text = literal.numberLiteral().GetText();
                if (TryParseSmalltalkNumber(text, out object? numValue))
                {
                    if (numValue is int intVal)
                    {
                        switch (intVal)
                        {
                            case -1:
                                emitter.PushMinusOne();
                                break;
                            case 0:
                                emitter.PushZero();
                                break;
                            case 1:
                                emitter.PushOne();
                                break;
                            case 2:
                                emitter.PushTwo();
                                break;
                            default:
                                emitter.PushLiteralConstant(intVal);
                                break;
                        }
                    }
                    else if (numValue is double floatVal)
                    {
                        emitter.PushLiteralConstant(floatVal);
                    }
                }
            }
            else if (literal.stringConst() != null)
            {
                var text = literal.stringConst().GetText();
                var str = text[1..^1].Replace("''", "'").Replace("!!", "!");
                emitter.PushLiteralConstant(str);
            }
            else if (literal.symbolConst() != null)
            {
                var text = literal.symbolConst().GetText();
                var sym = new Symbol(text[1..]); // Remove #
                emitter.PushLiteralConstant(sym);
            }
            else if (literal.characterLiteral() != null)
            {
                var text = literal.characterLiteral().GetText();
                var ch = text[1]; // Get character after $
                emitter.PushLiteralConstant(ch);
            }
            else if (literal.arrayLiteral() != null)
            {
                var array = literal.arrayLiteral();
                List<object> elements = [];

                foreach (var element in array.arrayElement())
                {
                    elements.Add(ProcessArrayElement(element));
                }

                emitter.PushLiteralConstant(elements.ToArray());
            }
            else if (literal.byteArrayLiteral() != null)
            {
                var byteArray = literal.byteArrayLiteral();
                emitter.PushLiteralConstant(byteArray.GetText());
            }
            else
            {
                var text = literal.GetText();
                switch (text)
                {
                    case "true":
                        emitter.PushTrue();
                        break;
                    case "false":
                        emitter.PushFalse();
                        break;
                    case "nil":
                        emitter.PushNil();
                        break;
                }
            }
        }

        /// <summary>
        /// Processes an array element and returns the appropriate value.
        /// </summary>
        private object ProcessArrayElement(Smalltalk80Parser.ArrayElementContext element)
        {
            if (element.literal() != null)
            {
                var lit = element.literal();
                if (lit.numberLiteral() != null)
                {
                    var text = lit.numberLiteral().GetText();
                    if (TryParseSmalltalkNumber(text, out object? numValue) && numValue != null)
                    {
                        return numValue;
                    }
                    else
                    {
                        return text;
                    }
                }
                else if (lit.stringConst() != null)
                {
                    var str = lit.stringConst().GetText();
                    return str[1..^1].Replace("''", "'").Replace("!!", "!");
                }
                else if (lit.symbolConst() != null)
                {
                    return lit.symbolConst().GetText();
                }
                else if (lit.characterLiteral() != null)
                {
                    var text = lit.characterLiteral().GetText();
                    return text[1];
                }
                else if (lit.arrayLiteral() != null)
                {
                    var nestedArray = lit.arrayLiteral();
                    List<object> nestedElements = [];
                    foreach (var nestedElement in nestedArray.arrayElement())
                    {
                        nestedElements.Add(ProcessArrayElement(nestedElement));
                    }
                    return nestedElements.ToArray();
                }
                else if (lit.byteArrayLiteral() != null)
                {
                    return lit.byteArrayLiteral().GetText();
                }
            }
            else if (element.MINUS() != null && element.numberLiteral() != null)
            {
                var text = element.numberLiteral().GetText();
                if (TryParseSmalltalkNumber(text, out object? numValue) && numValue != null)
                {
                    if (numValue is int intVal)
                    {
                        return -intVal;
                    }
                    else if (numValue is double doubleVal)
                    {
                        return -doubleVal;
                    }
                }
                return "-" + text;
            }
            else if (element.symbolName() != null)
            {
                return "#" + element.symbolName().GetText();
            }
            else if (element.LEFT_PAREN() != null)
            {
                var nestedElements = new List<object>();
                foreach (var nestedElement in element.arrayElement())
                {
                    nestedElements.Add(ProcessArrayElement(nestedElement));
                }
                return nestedElements.ToArray();
            }

            return element.GetText();
        }
    }
}
