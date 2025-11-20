using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Builds Smalltalk StrikeFont and TextStyle objects from C# font data.
/// Used during cold start to bootstrap the DefaultTextStyle pool variable.
/// </summary>
public class FontObjectBuilder
{
    private readonly Phase5Context _context;

    public FontObjectBuilder(Phase5Context context)
    {
        _context = context;
    }

    /// <summary>
    /// Creates a Smalltalk StrikeFont object from StrikeFontData.
    /// </summary>
    /// <param name="fontData">C# representation of the font.</param>
    /// <returns>OOP of the created StrikeFont.</returns>
    public SmalltalkOOP CreateStrikeFont(StrikeFontData fontData)
    {
        var nilOOP = GlobalRegistry.Get("nil");

        // Allocate StrikeFont object
        var strikeFontOOP = _context.Allocator.AllocateFixedObject("StrikeFont");
        var strikeFontClassOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
        _context.Allocator.SetObjectClass(strikeFontOOP, strikeFontClassOOP);

        // Initialize all fields to nil first
        int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("StrikeFont", _context.Bundle);
        for (int i = 0; i < instVarCount; i++)
        {
            _context.Allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
            _context.InitializedFields.Add((strikeFontOOP, i));
        }

        // Create xTable array (one extra for sentinel)
        var xTableOOP = CreateIntegerArray(fontData.XTable);
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "xTable", xTableOOP);

        // Create glyphs Form
        var glyphsOOP = CreateGlyphsForm(fontData);
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "glyphs", glyphsOOP);

        // Set name
        var nameOOP = CreateString(fontData.Name);
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "name", nameOOP);

        // Create stopConditions array (256 entries, all nil)
        var stopConditionsOOP = CreateStopConditionsArray();
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "stopConditions", stopConditionsOOP);

        // Set numeric fields
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "type", SmalltalkOOP.FromSmallInteger(fontData.Type));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "minAscii", SmalltalkOOP.FromSmallInteger(fontData.MinAscii));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "maxAscii", SmalltalkOOP.FromSmallInteger(fontData.MaxAscii));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "maxWidth", SmalltalkOOP.FromSmallInteger(fontData.Metrics.MaxWidth));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "strikeLength", SmalltalkOOP.FromSmallInteger(fontData.StrikeLength));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "ascent", SmalltalkOOP.FromSmallInteger(fontData.Metrics.Ascent));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "descent", SmalltalkOOP.FromSmallInteger(fontData.Metrics.Descent));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "xOffset", SmalltalkOOP.FromSmallInteger(fontData.XOffset));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "raster", SmalltalkOOP.FromSmallInteger(fontData.Raster));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "subscript", SmalltalkOOP.FromSmallInteger(fontData.Metrics.SubscriptOffset));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "superscript", SmalltalkOOP.FromSmallInteger(fontData.Metrics.SuperscriptOffset));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "emphasis", SmalltalkOOP.FromSmallInteger(fontData.Emphasis));

        return strikeFontOOP;
    }

    /// <summary>
    /// Creates a Smalltalk TextStyle object with the given fonts.
    /// </summary>
    /// <param name="fonts">Array of StrikeFont OOPs.</param>
    /// <param name="lineGrid">Line height in pixels.</param>
    /// <param name="baseline">Baseline position.</param>
    /// <returns>OOP of the created TextStyle.</returns>
    public SmalltalkOOP CreateTextStyle(SmalltalkOOP[] fonts, int lineGrid, int baseline)
    {
        var nilOOP = GlobalRegistry.Get("nil");

        // Allocate TextStyle object
        var textStyleOOP = _context.Allocator.AllocateFixedObject("TextStyle");
        var textStyleClassOOP = GlobalRegistry.LookupClassOOP("TextStyle");
        _context.Allocator.SetObjectClass(textStyleOOP, textStyleClassOOP);

        // Initialize all fields to nil first
        int instVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("TextStyle", _context.Bundle);
        for (int i = 0; i < instVarCount; i++)
        {
            _context.Allocator.SetInstanceVariable(textStyleOOP, i, nilOOP);
            _context.InitializedFields.Add((textStyleOOP, i));
        }

        // Create fontArray
        var fontArrayOOP = _context.Allocator.AllocateArray("Array", fonts.Length);
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _context.Allocator.SetObjectClass(fontArrayOOP, arrayClassOOP);
        for (int i = 0; i < fonts.Length; i++)
        {
            _context.Allocator.SetIndexedOOP(fontArrayOOP, i, fonts[i]);
        }

        // Set TextStyle fields
        SetInstanceVarByName(textStyleOOP, "TextStyle", "fontArray", fontArrayOOP);
        SetInstanceVarByName(textStyleOOP, "TextStyle", "lineGrid", SmalltalkOOP.FromSmallInteger(lineGrid));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "baseline", SmalltalkOOP.FromSmallInteger(baseline));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "alignment", SmalltalkOOP.FromSmallInteger(0)); // left aligned
        SetInstanceVarByName(textStyleOOP, "TextStyle", "firstIndent", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "restIndent", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "rightIndent", SmalltalkOOP.FromSmallInteger(0));

        // Create default tabs array (every 24 pixels)
        var tabsArrayOOP = CreateDefaultTabsArray();
        SetInstanceVarByName(textStyleOOP, "TextStyle", "tabsArray", tabsArrayOOP);

        // marginTabsArray can stay nil
        // outputMedium can stay nil (defaults to #Display)

        return textStyleOOP;
    }

    /// <summary>
    /// Creates a Form object containing the glyph bitmap data.
    /// </summary>
    private SmalltalkOOP CreateGlyphsForm(StrikeFontData fontData)
    {
        // Create the bitmap for glyphs (word-indexed)
        int wordCount = fontData.GlyphsBitmap.Length;
        var formOOP = _context.Allocator.AllocateObject("Form", ObjectFormat.Pointer, 4); // 4 named inst vars
        var formClassOOP = GlobalRegistry.LookupClassOOP("Form");
        _context.Allocator.SetObjectClass(formOOP, formClassOOP);

        // Create WordArray for bits
        var bitsOOP = _context.Allocator.AllocateObject("WordArray", ObjectFormat.WordIndexed, wordCount);
        var wordArrayClassOOP = GlobalRegistry.LookupClassOOP("WordArray");
        _context.Allocator.SetObjectClass(bitsOOP, wordArrayClassOOP);

        // Copy glyph data into WordArray
        for (int i = 0; i < wordCount; i++)
        {
            _context.HeapManager.SetIndexedWord(bitsOOP, i, fontData.GlyphsBitmap[i]);
        }

        // Create offset Point (0@0)
        var offsetOOP = CreatePoint(0, 0);

        // Set Form instance variables
        // Form has: bits, width, height, offset
        SetInstanceVarByName(formOOP, "Form", "bits", bitsOOP);
        SetInstanceVarByName(formOOP, "Form", "width", SmalltalkOOP.FromSmallInteger(fontData.StrikeLength));
        SetInstanceVarByName(formOOP, "Form", "height", SmalltalkOOP.FromSmallInteger(fontData.StrikeHeight));
        SetInstanceVarByName(formOOP, "Form", "offset", offsetOOP);

        return formOOP;
    }

    /// <summary>
    /// Creates an Array of integers from a C# int array.
    /// </summary>
    private SmalltalkOOP CreateIntegerArray(int[] values)
    {
        var arrayOOP = _context.Allocator.AllocateArray("Array", values.Length);
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _context.Allocator.SetObjectClass(arrayOOP, arrayClassOOP);

        for (int i = 0; i < values.Length; i++)
        {
            _context.Allocator.SetIndexedOOP(arrayOOP, i, SmalltalkOOP.FromSmallInteger(values[i]));
        }

        return arrayOOP;
    }

    /// <summary>
    /// Creates a Smalltalk String from a C# string.
    /// </summary>
    private SmalltalkOOP CreateString(string value)
    {
        byte[] bytes = System.Text.Encoding.Latin1.GetBytes(value);
        var stringOOP = _context.Allocator.AllocateString("String", bytes.Length);
        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        _context.Allocator.SetObjectClass(stringOOP, stringClassOOP);

        for (int i = 0; i < bytes.Length; i++)
        {
            _context.Allocator.SetIndexedByte(stringOOP, i, bytes[i]);
        }

        return stringOOP;
    }

    /// <summary>
    /// Creates a Point with the given x and y coordinates.
    /// </summary>
    private SmalltalkOOP CreatePoint(int x, int y)
    {
        var pointOOP = _context.Allocator.AllocateFixedObject("Point");
        var pointClassOOP = GlobalRegistry.LookupClassOOP("Point");
        _context.Allocator.SetObjectClass(pointOOP, pointClassOOP);

        SetInstanceVarByName(pointOOP, "Point", "x", SmalltalkOOP.FromSmallInteger(x));
        SetInstanceVarByName(pointOOP, "Point", "y", SmalltalkOOP.FromSmallInteger(y));

        return pointOOP;
    }

    /// <summary>
    /// Creates the stopConditions array for CharacterScanner.
    /// Needs 258 entries for indices 1-258 (Smalltalk 1-based).
    /// EndOfRun = 257, CrossedX = 258 are used as sentinel indices.
    /// </summary>
    private SmalltalkOOP CreateStopConditionsArray()
    {
        var nilOOP = GlobalRegistry.Get("nil");
        var arrayOOP = _context.Allocator.AllocateArray("Array", 258);
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _context.Allocator.SetObjectClass(arrayOOP, arrayClassOOP);

        for (int i = 0; i < 258; i++)
        {
            _context.Allocator.SetIndexedOOP(arrayOOP, i, nilOOP);
        }

        return arrayOOP;
    }

    /// <summary>
    /// Creates default tab stops array (every 24 pixels).
    /// </summary>
    private SmalltalkOOP CreateDefaultTabsArray()
    {
        int tabCount = 20;
        int tabWidth = 24;
        var arrayOOP = _context.Allocator.AllocateArray("Array", tabCount);
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        _context.Allocator.SetObjectClass(arrayOOP, arrayClassOOP);

        for (int i = 0; i < tabCount; i++)
        {
            int tabStop = (i + 1) * tabWidth;
            _context.Allocator.SetIndexedOOP(arrayOOP, i, SmalltalkOOP.FromSmallInteger(tabStop));
        }

        return arrayOOP;
    }

    private void SetInstanceVarByName(SmalltalkOOP objectOOP, string className, string varName, SmalltalkOOP value)
    {
        int index = ClassHierarchyTraversal.FindInstanceVariableIndex(className, varName, _context.Bundle);
        if (index >= 0)
        {
            _context.Allocator.SetInstanceVariable(objectOOP, index, value);
            _context.InitializedFields.Add((objectOOP, index));
        }
    }
}
