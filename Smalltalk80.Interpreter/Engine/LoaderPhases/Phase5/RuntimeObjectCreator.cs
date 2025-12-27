using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types.Diagnostics;
using Smalltalk80.Interpreter.Core.VM.Graphics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Creates runtime objects required for bootstrap: DefaultTextStyle and Display.
/// These objects are needed by init expressions and GUI operations.
/// </summary>
public class RuntimeObjectCreator(
    Phase5Context context,
    IAssociationManager associationManager,
    RuntimeDictionaryService dictService) : IRuntimeObjectCreator
{
    /// <summary>
    /// Creates the DefaultTextStyle object with properly initialized StrikeFonts.
    /// Uses WinFormsFontBridge to generate real font metrics and glyph bitmaps.
    /// </summary>
    public void CreateDefaultTextStyle()
    {
        // Use the FontObjectBuilder and FontBridgeFactory to create proper fonts
        var fontBuilder = new FontObjectBuilder(context);

        // Use platform-appropriate font bridge (WinForms on Windows, Null otherwise)
        SmalltalkOOP textStyleOOP;
        int lineGrid;
        int baseline;

        try
        {
            using var fontBridge = FontBridgeFactory.Create() as IDisposable;
            var bridge = fontBridge as IFontBridge ?? FontBridgeFactory.CreateNull();

            // Create canonical font sizes (10, 12, 14)
            var fontDescriptors = new[]
            {
                new FontDescriptor("Consolas", 10),
                new FontDescriptor("Consolas", 12),
                new FontDescriptor("Consolas", 14)
            };

            var strikeFontOOPs = new List<SmalltalkOOP>();

            foreach (var descriptor in fontDescriptors)
            {
                // Check if font is available
                if (!bridge.ResolveFace(descriptor))
                {
                    // Fall back to a common font
                    var fallbackDescriptor = new FontDescriptor("Arial", descriptor.SizePixels);
                    if (!bridge.ResolveFace(fallbackDescriptor))
                    {
                        // Use stub font as last resort
                        continue;
                    }
                }

                var fontData = bridge.BuildStrikeFont(descriptor);
                var strikeFontOOP = fontBuilder.CreateStrikeFont(fontData);
                strikeFontOOPs.Add(strikeFontOOP);

                context.Diagnostics?.Emit(new DiagnosticMessageEvent
                {
                    Severity = MessageSeverity.Info,
                    Category = "Phase5",
                    Message = $"Created StrikeFont '{fontData.Name}' (ascent={fontData.Metrics.Ascent}, descent={fontData.Metrics.Descent}, strikeLength={fontData.StrikeLength})",
                    Timestamp = Stopwatch.GetTimestamp(),
                    InstructionCount = 0
                });
            }

            // If we have at least one font, use it
            if (strikeFontOOPs.Count > 0)
            {
                // Use the 12px font metrics for TextStyle (or first available)
                var primaryFontData = bridge.BuildStrikeFont(fontDescriptors[Math.Min(1, fontDescriptors.Length - 1)]);
                lineGrid = primaryFontData.Metrics.Ascent + primaryFontData.Metrics.Descent + primaryFontData.Metrics.Leading;
                baseline = primaryFontData.Metrics.Ascent;

                textStyleOOP = fontBuilder.CreateTextStyle(strikeFontOOPs.ToArray(), lineGrid, baseline);
            }
            else
            {
                // Fall back to stub
                textStyleOOP = CreateStubTextStyle(fontBuilder);
                lineGrid = 14;
                baseline = 10;
            }
        }
        catch (Exception ex)
        {
            // Fall back to stub on any error (e.g., running without GUI)
            context.Diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Warning,
                Category = "Phase5",
                Message = $"Font bridge failed, using stub: {ex.Message}",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });

            textStyleOOP = CreateStubTextStyle(fontBuilder);
            lineGrid = 14;
            baseline = 10;
        }

        // Install the DefaultTextStyle value in ALL Associations that reference it
        // Per Blue Book model: classes with TextConstants pool find it as pool variable,
        // classes WITHOUT TextConstants pool find it as global/undeclared.
        // We must update ALL such Associations to share the same TextStyle value.

        // First, try to find the pool variable Association (DefaultTextStyle@TextConstants)
        bool foundPoolVar = AssociationRegistry.TryGet("DefaultTextStyle", out SmalltalkOOP poolVarAssoc, "TextConstants");

        // Also try to find the global/undeclared Association (DefaultTextStyle with no pool)
        bool foundGlobal = AssociationRegistry.TryGet("DefaultTextStyle", out SmalltalkOOP globalAssoc, poolDictionaryName: null);

        if (!foundPoolVar && !foundGlobal)
        {
            throw new InvalidOperationException("DefaultTextStyle Association not found in AssociationRegistry (neither pool variable nor global)");
        }

        // Update the value in ALL found Associations
        if (foundPoolVar)
        {
            context.Allocator.SetInstanceVariable(poolVarAssoc, 1, textStyleOOP);
            context.InitializedFields.Add((poolVarAssoc, 1));
        }

        if (foundGlobal)
        {
            context.Allocator.SetInstanceVariable(globalAssoc, 1, textStyleOOP);
            context.InitializedFields.Add((globalAssoc, 1));
        }

        // Install in TextConstants pool dictionary (use pool variable Association if available, else global)
        var primaryAssoc = foundPoolVar ? poolVarAssoc : globalAssoc;
        var keySymbolOOP = context.Allocator.GetInstanceVariable(primaryAssoc, 0);

        if (GlobalRegistry.TryGet("TextConstants", out var textConstantsOOP))
        {
            // Install in TextConstants
            dictService.InstallAssociation(textConstantsOOP, primaryAssoc, keySymbolOOP);

            // Remove DefaultTextStyle from Undeclared (with proper hash chain fixup)
            if (GlobalRegistry.TryGet("Undeclared", out var undeclaredOOP))
            {
                dictService.RemoveAssociation(undeclaredOOP, keySymbolOOP);
            }
        }
        else
        {
            throw new InvalidOperationException("TextConstants not found in GlobalRegistry during DefaultTextStyle setup");
        }

        context.Diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase5",
            Message = $"Created DefaultTextStyle (oop={textStyleOOP}) with lineGrid={lineGrid}, baseline={baseline}",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    /// <summary>
    /// Creates a minimal stub TextStyle when real fonts are unavailable.
    /// </summary>
    private SmalltalkOOP CreateStubTextStyle(FontObjectBuilder fontBuilder)
    {
        var nilOOP = context.HeapManager.GetNil();

        // Create minimal stub StrikeFont
        var strikeFontOOP = context.Allocator.AllocateFixedObject("StrikeFont");
        var strikeFontClassOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
        context.Allocator.SetClassOOP(strikeFontOOP, strikeFontClassOOP);

        // Initialize all StrikeFont fields to nil
        int strikeFontInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("StrikeFont", context.Bundle);
        for (int i = 0; i < strikeFontInstVarCount; i++)
        {
            context.Allocator.SetInstanceVariable(strikeFontOOP, i, nilOOP);
            context.InitializedFields.Add((strikeFontOOP, i));
        }

        // Set minimal StrikeFont properties
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "name", InternString("SystemFont"));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "ascent", SmalltalkOOP.FromSmallInteger(10));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "descent", SmalltalkOOP.FromSmallInteger(3));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "maxWidth", SmalltalkOOP.FromSmallInteger(8));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "minAscii", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "maxAscii", SmalltalkOOP.FromSmallInteger(255));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "strikeLength", SmalltalkOOP.FromSmallInteger(2048));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "xOffset", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "raster", SmalltalkOOP.FromSmallInteger(128));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "type", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "emphasis", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "subscript", SmalltalkOOP.FromSmallInteger(3));
        SetInstanceVarByName(strikeFontOOP, "StrikeFont", "superscript", SmalltalkOOP.FromSmallInteger(5));

        // Create TextStyle with stub font
        return fontBuilder.CreateTextStyle(new[] { strikeFontOOP }, 14, 10);
    }

    /// <summary>
    /// Creates the Display global as an instance of DisplayScreen (Form subclass).
    /// </summary>
    public void CreateDisplay()
    {
        var nilOOP = context.HeapManager.GetNil();

        // Step 1: Create offset Point (0@0)
        var pointOOP = context.Allocator.AllocateFixedObject("Point");
        var pointClassOOP = GlobalRegistry.LookupClassOOP("Point");
        context.Allocator.SetClassOOP(pointOOP, pointClassOOP);

        SetInstanceVarByName(pointOOP, "Point", "x", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(pointOOP, "Point", "y", SmalltalkOOP.FromSmallInteger(0));

        // Step 2: Create DisplayBitmap
        int displayWidth = 800;
        int displayHeight = 600;
        int bytesPerRow = displayWidth / 8;
        int totalBytes = bytesPerRow * displayHeight;
        int totalWords = totalBytes / 2;

        var displayBitmapOOP = context.Allocator.AllocateObject("DisplayBitmap", ObjectFormat.WordIndexed, totalWords);
        var displayBitmapClassOOP = GlobalRegistry.LookupClassOOP("DisplayBitmap");
        context.Allocator.SetClassOOP(displayBitmapOOP, displayBitmapClassOOP);

        // Initialize bitmap to zeros
        for (int i = 0; i < totalWords; i++)
        {
            context.HeapManager.SetIndexedWord(displayBitmapOOP, i, 0);
        }

        GlobalRegistry.Register("DisplayBitmap", displayBitmapOOP);

        // Step 3: Create DisplayScreen object
        var displayScreenOOP = context.Allocator.AllocateFixedObject("DisplayScreen");
        var displayScreenClassOOP = GlobalRegistry.LookupClassOOP("DisplayScreen");
        context.Allocator.SetClassOOP(displayScreenOOP, displayScreenClassOOP);

        // Set Form instance variables
        SetInstanceVarByName(displayScreenOOP, "Form", "bits", displayBitmapOOP);
        SetInstanceVarByName(displayScreenOOP, "Form", "width", SmalltalkOOP.FromSmallInteger(800));
        SetInstanceVarByName(displayScreenOOP, "Form", "height", SmalltalkOOP.FromSmallInteger(600));
        SetInstanceVarByName(displayScreenOOP, "Form", "offset", pointOOP);

        // Initialize remaining fields to nil
        int displayScreenInstVarCount = ClassHierarchyTraversal.CalculateTotalInstanceVariableCount("DisplayScreen", context.Bundle);
        for (int i = 0; i < displayScreenInstVarCount; i++)
        {
            if (!context.InitializedFields.Contains((displayScreenOOP, i)))
            {
                context.Allocator.SetInstanceVariable(displayScreenOOP, i, nilOOP);
                context.InitializedFields.Add((displayScreenOOP, i));
            }
        }

        // Step 4: Install Display as global
        var associationOOP = associationManager.GetOrCreateCanonical("Display");
        context.Allocator.SetInstanceVariable(associationOOP, 1, displayScreenOOP);
        context.InitializedFields.Add((associationOOP, 1));

        // Install in SystemDictionary
        var systemDictOOP = GlobalRegistry.SystemDictionaryOOP;
        var displayKeyOOP = context.Allocator.GetInstanceVariable(associationOOP, VMConstants.ASSOCIATION_KEY_INDEX);
        dictService.InstallAssociation(systemDictOOP, associationOOP, displayKeyOOP);

        GlobalRegistry.Register("Display", displayScreenOOP);

        context.Diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase5",
            Message = $"Created Display (800x600, {totalBytes} byte bitmap) as DisplayScreen OOP({displayScreenOOP.RawValue})",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    private void SetInstanceVarByName(SmalltalkOOP objectOOP, string className, string varName, SmalltalkOOP value)
    {
        int index = ClassHierarchyTraversal.FindInstanceVariableIndex(className, varName, context.Bundle);
        if (index >= 0)
        {
            context.Allocator.SetInstanceVariable(objectOOP, index, value);
            context.InitializedFields.Add((objectOOP, index));
        }
    }

    private SmalltalkOOP InternString(string value)
    {
        byte[] stringBytes = System.Text.Encoding.UTF8.GetBytes(value);
        var stringOOP = context.Allocator.AllocateString("String", stringBytes.Length);

        for (int i = 0; i < stringBytes.Length; i++)
        {
            context.Allocator.SetIndexedByte(stringOOP, i, stringBytes[i]);
        }

        var stringClassOOP = GlobalRegistry.LookupClassOOP("String");
        context.Allocator.SetClassOOP(stringOOP, stringClassOOP);

        return stringOOP;
    }
}
