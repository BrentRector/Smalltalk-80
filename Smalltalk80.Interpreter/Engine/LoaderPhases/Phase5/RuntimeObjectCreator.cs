using System.Diagnostics;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.Diagnostics;

namespace Smalltalk80.Interpreter.Engine.LoaderPhases.Phase5;

/// <summary>
/// Creates runtime objects required for bootstrap: DefaultTextStyle and Display.
/// These objects are needed by init expressions and GUI operations.
/// </summary>
public class RuntimeObjectCreator(
    Phase5Context context,
    IAssociationManager associationManager,
    IDictionaryOperations dictionaryOps) : IRuntimeObjectCreator
{
    /// <summary>
    /// Creates a minimal DefaultTextStyle object to support PopUpMenu and other UI features.
    /// </summary>
    public void CreateDefaultTextStyle()
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Step 1: Create minimal stub StrikeFont
        var strikeFontOOP = context.Allocator.AllocateFixedObject("StrikeFont");
        var strikeFontClassOOP = GlobalRegistry.LookupClassOOP("StrikeFont");
        context.Allocator.SetObjectClass(strikeFontOOP, strikeFontClassOOP);

        // Initialize all StrikeFont fields to nil
        int strikeFontInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("StrikeFont", context.Bundle);
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

        // Step 2: Create fontArray with one stub font
        var fontArrayOOP = context.Allocator.AllocateArray("Array", 1);
        context.Allocator.SetIndexedOOP(fontArrayOOP, 0, strikeFontOOP);
        var arrayClassOOP = GlobalRegistry.LookupClassOOP("Array");
        context.Allocator.SetObjectClass(fontArrayOOP, arrayClassOOP);

        // Step 3: Create TextStyle object
        var textStyleOOP = context.Allocator.AllocateFixedObject("TextStyle");
        var textStyleClassOOP = GlobalRegistry.LookupClassOOP("TextStyle");
        context.Allocator.SetObjectClass(textStyleOOP, textStyleClassOOP);

        // Initialize critical TextStyle fields
        SetInstanceVarByName(textStyleOOP, "TextStyle", "fontArray", fontArrayOOP);
        SetInstanceVarByName(textStyleOOP, "TextStyle", "lineGrid", SmalltalkOOP.FromSmallInteger(14));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "baseline", SmalltalkOOP.FromSmallInteger(10));
        SetInstanceVarByName(textStyleOOP, "TextStyle", "alignment", SmalltalkOOP.FromSmallInteger(0));

        // Initialize remaining fields to nil
        int textStyleInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("TextStyle", context.Bundle);
        for (int i = 0; i < textStyleInstVarCount; i++)
        {
            if (!context.InitializedFields.Contains((textStyleOOP, i)))
            {
                context.Allocator.SetInstanceVariable(textStyleOOP, i, nilOOP);
                context.InitializedFields.Add((textStyleOOP, i));
            }
        }

        // Step 4: Update the DefaultTextStyle Association
        var allAssociations = AssociationRegistry.GetAll();
        if (allAssociations.TryGetValue("DefaultTextStyle", out SmalltalkOOP defaultTextStyleAssoc))
        {
            context.Allocator.SetInstanceVariable(defaultTextStyleAssoc, 1, textStyleOOP);
            context.InitializedFields.Add((defaultTextStyleAssoc, 1));

            context.Diagnostics?.Emit(new DiagnosticMessageEvent
            {
                Severity = MessageSeverity.Info,
                Category = "Phase5",
                Message = $"Created DefaultTextStyle (oop={textStyleOOP.RawValue}) with stub StrikeFont",
                Timestamp = Stopwatch.GetTimestamp(),
                InstructionCount = 0
            });
        }
        else
        {
            GlobalRegistry.Register("DefaultTextStyle", textStyleOOP);
        }
    }

    /// <summary>
    /// Creates the Display global as an instance of DisplayScreen (Form subclass).
    /// </summary>
    public void CreateDisplay()
    {
        var nilOOP = SpecialObjectsArray.Instance.Nil;

        // Step 1: Create offset Point (0@0)
        var pointOOP = context.Allocator.AllocateFixedObject("Point");
        var pointClassOOP = GlobalRegistry.LookupClassOOP("Point");
        context.Allocator.SetObjectClass(pointOOP, pointClassOOP);

        SetInstanceVarByName(pointOOP, "Point", "x", SmalltalkOOP.FromSmallInteger(0));
        SetInstanceVarByName(pointOOP, "Point", "y", SmalltalkOOP.FromSmallInteger(0));

        // Step 2: Create DisplayBitmap
        int displayWidth = 640;
        int displayHeight = 480;
        int bytesPerRow = displayWidth / 8;
        int totalBytes = bytesPerRow * displayHeight;
        int totalWords = totalBytes / 2;

        var displayBitmapOOP = context.Allocator.AllocateObject("DisplayBitmap", ObjectFormat.WordIndexed, totalWords);
        var displayBitmapClassOOP = GlobalRegistry.LookupClassOOP("DisplayBitmap");
        context.Allocator.SetObjectClass(displayBitmapOOP, displayBitmapClassOOP);

        // Initialize bitmap to zeros
        for (int i = 0; i < totalWords; i++)
        {
            context.HeapManager.SetIndexedWord(displayBitmapOOP, i, 0);
        }

        GlobalRegistry.Register("DisplayBitmap", displayBitmapOOP);

        // Step 3: Create DisplayScreen object
        var displayScreenOOP = context.Allocator.AllocateFixedObject("DisplayScreen");
        var displayScreenClassOOP = GlobalRegistry.LookupClassOOP("DisplayScreen");
        context.Allocator.SetObjectClass(displayScreenOOP, displayScreenClassOOP);

        // Set Form instance variables
        SetInstanceVarByName(displayScreenOOP, "Form", "bits", displayBitmapOOP);
        SetInstanceVarByName(displayScreenOOP, "Form", "width", SmalltalkOOP.FromSmallInteger(640));
        SetInstanceVarByName(displayScreenOOP, "Form", "height", SmalltalkOOP.FromSmallInteger(480));
        SetInstanceVarByName(displayScreenOOP, "Form", "offset", pointOOP);

        // Initialize remaining fields to nil
        int displayScreenInstVarCount = ClassHierarchyUtils.CalculateTotalInstanceVariableCount("DisplayScreen", context.Bundle);
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
        dictionaryOps.InstallAssociation(systemDictOOP, associationOOP, "Display");

        GlobalRegistry.Register("Display", displayScreenOOP);

        context.Diagnostics?.Emit(new DiagnosticMessageEvent
        {
            Severity = MessageSeverity.Info,
            Category = "Phase5",
            Message = $"Created Display (640x480, {totalBytes} byte bitmap) as DisplayScreen OOP({displayScreenOOP.RawValue})",
            Timestamp = Stopwatch.GetTimestamp(),
            InstructionCount = 0
        });
    }

    private void SetInstanceVarByName(SmalltalkOOP objectOOP, string className, string varName, SmalltalkOOP value)
    {
        int index = ClassHierarchyUtils.FindInstanceVariableIndex(className, varName, context.Bundle);
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
        context.Allocator.SetObjectClass(stringOOP, stringClassOOP);

        return stringOOP;
    }
}
