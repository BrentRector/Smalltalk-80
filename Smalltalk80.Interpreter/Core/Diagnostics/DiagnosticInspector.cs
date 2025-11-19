using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.VM;

namespace Smalltalk80.Interpreter.Core.Diagnostics;

/// <summary>
/// Diagnostic inspection methods for examining heap state and class structures.
/// Used for debugging and verification during development.
/// </summary>
public static class DiagnosticInspector
{
    /// <summary>
    /// Diagnoses Float class variables by comparing expected vs actual values.
    /// </summary>
    public static void DiagnoseFloatClassVariables(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            // Expected values from Float>>initialize source code
            var expectedValues = new Dictionary<string, string>
            {
                { "Pi", "3.14159" },
                { "Halfpi", "1.5708 (Pi / 2.0)" },
                { "Fourthpi", "0.78540 (Pi / 4.0)" },
                { "Twopi", "6.28318 (Pi * 2.0)" },
                { "RadiansPerDegree", "0.01745 (Pi / 180.0)" },
                { "Ln2", "0.693147" },
                { "Sqrt2", "1.41421" },
                { "SinCoefficients", "Array: #(-0.166667 0.00833333 -1.98409e-4 2.7526e-6 -2.39e-8)" },
                { "TanCoefficients", "Array: #(0.333331 0.133392 0.0533741 0.0245651 0.00290052 0.00951681)" },
                { "ExpPCoefficients", "Array: #(28.8756 2525.04)" },
                { "ExpQCoefficients", "Array: #(1.0 375.022 7285.73)" },
                { "LnCoefficients", "Array: #(0.237625 0.285254 0.400006 0.666667 2.0)" }
            };

            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                Console.WriteLine("  Warning: Float class not found in GlobalRegistry");
                return;
            }
            var floatClassOOP = floatClassId;

            // Get the metaclass (Float class)
            var floatMetaclassOOP = heapManager.GetClassOOP(floatClassOOP);

            Console.WriteLine($"Float class: OOP={floatClassOOP} (index={floatClassId})");
            Console.WriteLine($"Float metaclass: OOP={floatMetaclassOOP}");
            Console.WriteLine();

            // Get class variables from SystemDictionary associations
            foreach (var varName in expectedValues.Keys)
            {
                var actual = GetClassVariableValue(heapManager, bundle, varName);
                var expected = expectedValues[varName];

                Console.WriteLine($"{varName}:");
                Console.WriteLine($"  Expected: {expected}");
                Console.WriteLine($"  Actual:   {actual}");
                Console.WriteLine();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Error diagnosing Float class variables: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the value of a Float class variable from the classPool.
    /// </summary>
    public static string GetClassVariableValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, string varName)
    {
        try
        {
            // Get Float class OOP
            if (!GlobalRegistry.TryGet("class:Float", out SmalltalkOOP floatClassId))
            {
                return "Float class not found";
            }
            var floatClassOOP = floatClassId;

            // Float class is an instance of Class, which has instance variables from Behavior/ClassDescription/Class hierarchy:
            // Behavior: superclass, methodDict, format, subclasses
            // ClassDescription: instanceVariables, organization
            // Class: name, classPool, sharedPools
            // So classPool is at index: 4 (Behavior) + 2 (ClassDescription) + 1 (name) = 7

            // Get the classPool instance variable (Dictionary containing class variables)
            var classPoolOOP = heapManager.GetInstanceVariable(floatClassOOP, 7);

            if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
            {
                return "classPool is nil";
            }

            // classPool is a Dictionary with indexed array of associations
            int size = heapManager.GetIndexedSize(classPoolOOP);

            // Check if classPool is empty (no entries yet - they're lazily created)
            int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", bundle);
            if (tallyIndex >= 0)
            {
                var tallyOOP = heapManager.GetInstanceVariable(classPoolOOP, tallyIndex);
                if (tallyOOP.IsSmallInteger && tallyOOP.AsSmallInteger() == 0)
                {
                    return "classPool empty (variable not yet created - awaiting first access)";
                }
            }

            // Search for the variable name association
            for (int i = 0; i < size; i++)
            {
                var assocOOP = heapManager.GetIndexedOOP(classPoolOOP, i);
                if (assocOOP == SpecialObjectsArray.Instance.Nil)
                    continue;

                // Association has key at slot 1, value at slot 2
                var keyOOP = heapManager.GetSlot(assocOOP, 1);
                var keyName = GetSymbolName(heapManager, bundle, keyOOP);

                if (keyName == varName)
                {
                    var valueOOP = heapManager.GetSlot(assocOOP, 2);
                    return FormatValue(heapManager, bundle, valueOOP);
                }
            }

            return $"Not found in Float classPool (searched {size} slots)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Gets the string representation of a Symbol OOP.
    /// </summary>
    public static string GetSymbolName(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP symbolOOP)
    {
        if (symbolOOP.IsSmallInteger)
            return $"SmallInteger({symbolOOP.AsSmallInteger()})";

        try
        {
            // Symbol is a byte-indexed object containing characters
            int size = heapManager.GetIndexedSize(symbolOOP);
            char[] chars = new char[size];
            for (int i = 0; i < size; i++)
            {
                chars[i] = (char)heapManager.GetIndexedByte(symbolOOP, i);
            }
            return new string(chars);
        }
        catch
        {
            return $"<Symbol OOP={symbolOOP}>";
        }
    }

    /// <summary>
    /// Formats an OOP value for display, handling Floats, Arrays, and other types.
    /// </summary>
    public static string FormatValue(ISmalltalkHeapManager heapManager, ColdStartBundle bundle, SmalltalkOOP valueOOP)
    {
        if (valueOOP.IsSmallInteger)
            return $"{valueOOP.AsSmallInteger()}";

        try
        {
            var format = heapManager.GetObjectFormat(valueOOP);

            // Check if it's a Float (ByteIndexed with 8 bytes)
            if (format == ObjectFormat.ByteIndexed && heapManager.GetIndexedSize(valueOOP) == 8)
            {
                byte[] bytes = new byte[8];
                for (int i = 0; i < 8; i++)
                {
                    bytes[i] = heapManager.GetIndexedByte(valueOOP, i);
                }
                double value = BitConverter.ToDouble(bytes, 0);
                return $"{value:F6}";
            }

            // Check if it's an Array
            var classOOP = heapManager.GetClassOOP(valueOOP);
            if (GlobalRegistry.TryGet("class:Array", out SmalltalkOOP arrayClassOOP))
            {
                if (classOOP == arrayClassOOP)
                {
                    int size = heapManager.GetIndexedSize(valueOOP);
                    var elements = new List<string>();
                    for (int i = 0; i < size && i < 10; i++)  // Limit to 10 elements
                    {
                        var elemOOP = heapManager.GetIndexedOOP(valueOOP, i);
                        elements.Add(FormatValue(heapManager, bundle, elemOOP));
                    }
                    if (size > 10)
                        elements.Add("...");
                    return $"Array({size}): #({string.Join(" ", elements)})";
                }
            }

            return $"<Object OOP={valueOOP}, format={format}>";
        }
        catch (Exception ex)
        {
            return $"<Error: {ex.Message}>";
        }
    }

    /// <summary>
    /// Diagnoses Cursor class variables.
    /// </summary>
    public static void DiagnoseCursorClassVariables(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            // Cursor class variables (based on typical Smalltalk-80 Cursor implementation)
            var expectedVariables = new[]
            {
                "Normal", "Read", "Write", "Execute", "Move", "Copy", "Corner", "CrossHair",
                "Marker", "Origin", "Down", "Up", "Frame", "BlankCursor", "CurrentCursor"
            };

            // Get Cursor class OOP
            if (!GlobalRegistry.TryGet("class:Cursor", out SmalltalkOOP cursorClassId))
            {
                Console.WriteLine("  Warning: Cursor class not found in GlobalRegistry");
                return;
            }
            var cursorClassOOP = cursorClassId;

            // Get the metaclass (Cursor class)
            var cursorMetaclassOOP = heapManager.GetClassOOP(cursorClassOOP);

            Console.WriteLine($"Cursor class: OOP={cursorClassOOP} (index={cursorClassId})");
            Console.WriteLine($"Cursor metaclass: OOP={cursorMetaclassOOP}");
            Console.WriteLine();

            // Get classPool instance variable (Dictionary containing class variables)
            var classPoolOOP = heapManager.GetInstanceVariable(cursorClassOOP, 7);

            if (classPoolOOP == SpecialObjectsArray.Instance.Nil)
            {
                Console.WriteLine("  classPool is nil - no class variables created yet");
                return;
            }

            // Check tally to see if dictionary is empty
            int tallyIndex = ClassHierarchyTraversal.FindInstanceVariableIndex("Dictionary", "tally", bundle);
            if (tallyIndex >= 0)
            {
                var tallyOOP = heapManager.GetInstanceVariable(classPoolOOP, tallyIndex);
                if (tallyOOP.IsSmallInteger && tallyOOP.AsSmallInteger() == 0)
                {
                    Console.WriteLine("  classPool empty (tally=0) - no class variables created yet");
                    return;
                }
                else if (tallyOOP.IsSmallInteger)
                {
                    Console.WriteLine($"  classPool contains {tallyOOP.AsSmallInteger()} class variable(s)");
                }
            }

            // Enumerate all class variables found in classPool
            int size = heapManager.GetIndexedSize(classPoolOOP);
            Console.WriteLine($"  Scanning {size} dictionary slots...");
            Console.WriteLine();

            var foundVariables = new List<string>();
            for (int i = 0; i < size; i++)
            {
                var assocOOP = heapManager.GetIndexedOOP(classPoolOOP, i);
                if (assocOOP == SpecialObjectsArray.Instance.Nil)
                    continue;

                // Association has key at slot 1, value at slot 2
                var keyOOP = heapManager.GetSlot(assocOOP, 1);
                var keyName = GetSymbolName(heapManager, bundle, keyOOP);
                var valueOOP = heapManager.GetSlot(assocOOP, 2);
                var value = FormatValue(heapManager, bundle, valueOOP);

                foundVariables.Add(keyName);
                Console.WriteLine($"{keyName}:");
                Console.WriteLine($"  Value: {value}");
                Console.WriteLine();
            }

            // Report on expected vs found
            if (foundVariables.Count == 0)
            {
                Console.WriteLine("  Warning: No class variables found (dictionary has entries but none were readable)");
            }
            else
            {
                Console.WriteLine($"Summary: Found {foundVariables.Count} class variable(s): {string.Join(", ", foundVariables)}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Error diagnosing Cursor class variables: {ex.Message}");
            Console.WriteLine($"     {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Inspects ClassDescription.instanceVariables for several classes.
    /// </summary>
    public static void InspectClassDescriptionInstanceVariables(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            // Test classes: Array, Point, Process (various inheritance depths)
            string[] testClasses = { "Array", "Point", "Process", "Dictionary" };
            var nilOOP = SpecialObjectsArray.Instance.Nil;

            foreach (var className in testClasses)
            {
                Console.WriteLine($"\n{className}:");

                // Look up the class OOP
                if (!GlobalRegistry.TryGet($"class:{className}", out SmalltalkOOP classOOP))
                {
                    Console.WriteLine($"  Warning: Class not found in GlobalRegistry");
                    continue;
                }

                // Check both slot 3 and slot 4 to be sure
                for (int slotIndex = 3; slotIndex <= 4; slotIndex++)
                {
                    var fieldValueOOP = heapManager.GetInstanceVariable(classOOP, slotIndex);

                    Console.WriteLine($"  Slot {slotIndex}: {FormatOOPValue(heapManager, fieldValueOOP, nilOOP)}");

                    // If it's an Array, inspect its contents
                    if (fieldValueOOP != nilOOP && !fieldValueOOP.IsSmallInteger)
                    {
                        try
                        {
                            int arraySize = heapManager.GetIndexedSize(fieldValueOOP);
                            Console.WriteLine($"    Array size: {arraySize}");
                            if (arraySize > 0 && arraySize < 50) // Reasonable size check
                            {
                                for (int i = 0; i < Math.Min(arraySize, 10); i++) // Show first 10 elements
                                {
                                    var elementOOP = heapManager.GetIndexedOOP(fieldValueOOP, i);
                                    string elementStr = FormatOOPValue(heapManager, elementOOP, nilOOP);

                                    // If it's a String, try to read it
                                    if (elementOOP != nilOOP && !elementOOP.IsSmallInteger)
                                    {
                                        try
                                        {
                                            int strLen = heapManager.GetIndexedSize(elementOOP);
                                            if (strLen > 0 && strLen < 100)
                                            {
                                                var bytes = new byte[strLen];
                                                for (int b = 0; b < strLen; b++)
                                                {
                                                    bytes[b] = heapManager.GetIndexedByte(elementOOP, b);
                                                }
                                                string text = System.Text.Encoding.Latin1.GetString(bytes);
                                                elementStr = $"\"{text}\"";
                                            }
                                        }
                                        catch
                                        {
                                            // Not a String, keep generic format
                                        }
                                    }

                                    Console.WriteLine($"      [{i}] = {elementStr}");
                                }
                                if (arraySize > 10)
                                {
                                    Console.WriteLine($"      ... ({arraySize - 10} more elements)");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"    Warning: Error inspecting array: {ex.Message}");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error during diagnostic: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Formats an OOP value for display in diagnostics.
    /// </summary>
    public static string FormatOOPValue(ISmalltalkHeapManager heapManager, SmalltalkOOP oop, SmalltalkOOP nilOOP)
    {
        if (oop == nilOOP)
            return "nil";
        if (oop.IsSmallInteger)
            return $"SmallInteger({oop.AsSmallInteger()})";

        // Try to get object class for better display
        try
        {
            var classOOP = heapManager.GetSlot(oop, 0);
            if (classOOP != nilOOP && !classOOP.IsSmallInteger)
            {
                // STRATEGY 1: Check if THIS object is a Class object itself
                var nameOOP = heapManager.GetInstanceVariable(oop, 6);
                if (nameOOP != nilOOP && !nameOOP.IsSmallInteger)
                {
                    int nameLen = heapManager.GetIndexedSize(nameOOP);
                    if (nameLen > 0 && nameLen < 100)
                    {
                        var bytes = new byte[nameLen];
                        for (int i = 0; i < nameLen; i++)
                        {
                            bytes[i] = heapManager.GetIndexedByte(nameOOP, i);
                        }
                        string className = System.Text.Encoding.Latin1.GetString(bytes);
                        return $"{className}(oop={oop.RawValue})";
                    }
                }

                // STRATEGY 2: Get class name from the class object's name field
                var classNameOOP = heapManager.GetInstanceVariable(classOOP, 6);
                if (classNameOOP != nilOOP && !classNameOOP.IsSmallInteger)
                {
                    int nameLen = heapManager.GetIndexedSize(classNameOOP);
                    if (nameLen > 0 && nameLen < 100)
                    {
                        var bytes = new byte[nameLen];
                        for (int i = 0; i < nameLen; i++)
                        {
                            bytes[i] = heapManager.GetIndexedByte(classNameOOP, i);
                        }
                        string className = System.Text.Encoding.Latin1.GetString(bytes);
                        return $"{className}(oop={oop.RawValue})";
                    }
                }
            }
        }
        catch
        {
            // Fall through to generic display
        }

        return $"OOP({oop.RawValue})";
    }

    /// <summary>
    /// Tests RuntimeHeapAccessor.BuildFieldMap() to verify it can read instance variable names.
    /// </summary>
    public static void TestBuildFieldMap(ISmalltalkHeapManager heapManager)
    {
        try
        {
            var runtimeHeapAccess = new RuntimeHeapAccessor(heapManager);
            Console.WriteLine("Building field map from heap...");

            var startTime = System.Diagnostics.Stopwatch.StartNew();
            var fieldMap = runtimeHeapAccess.BuildFieldMap();
            startTime.Stop();

            Console.WriteLine($"Built field map in {startTime.ElapsedMilliseconds}ms");
            Console.WriteLine($"Found {fieldMap.Count} classes");
            Console.WriteLine();

            // Test specific classes
            string[] testClasses = { "class:Array", "class:Point", "class:Process", "class:Dictionary", "class:MethodContext" };

            foreach (var className in testClasses)
            {
                if (fieldMap.TryGetValue(className, out var fields))
                {
                    Console.WriteLine($"{className}:");
                    if (fields.Count == 0)
                    {
                        Console.WriteLine("  (no instance variables)");
                    }
                    else
                    {
                        for (int i = 0; i < fields.Count; i++)
                        {
                            Console.WriteLine($"  [{i}] {fields[i]}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"{className}: NOT FOUND");
                }
            }

            Console.WriteLine($"\n[OK] BuildFieldMap() successfully read {fieldMap.Count} class layouts from heap");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] BuildFieldMap() failed: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }

    /// <summary>
    /// Inspects the File class>>initialize method's literal frame.
    /// </summary>
    public static void InspectFileInitializeLiterals(ISmalltalkHeapManager heapManager, ColdStartBundle bundle)
    {
        try
        {
            Console.WriteLine("\nSearching for File class>>initialize init expression...");

            // Find the File initialize init expression
            int fileInitIndex = -1;
            for (int i = 0; i < bundle.InitExpressions.Length; i++)
            {
                if (bundle.InitExpressions[i].Source == "File initialize")
                {
                    fileInitIndex = i;
                    break;
                }
            }

            if (fileInitIndex == -1)
            {
                Console.WriteLine("  File initialize init expression not found in bundle");
                return;
            }

            Console.WriteLine($"  Found at index {fileInitIndex}");

            // Get the CompiledMethod OOP from the InitExpressionMethods array
            var initArrayOOP = SpecialObjectsArray.Instance.InitExpressionMethods;
            if (initArrayOOP == SpecialObjectsArray.Instance.Nil)
            {
                Console.WriteLine("  InitExpressionMethods array is nil");
                return;
            }

            var methodOOP = heapManager.GetIndexedOOP(initArrayOOP, fileInitIndex);
            Console.WriteLine($"  Method OOP: {methodOOP}");

            // Get the CompiledMethod's indexed size (header + literals + bytecode array)
            int indexedSize = heapManager.GetIndexedSize(methodOOP);
            Console.WriteLine($"  Indexed size: {indexedSize}");

            // Literals start at indexed slot 1 (slot 0 is header)
            int literalCount = indexedSize - 2; // Subtract header and bytecode array
            Console.WriteLine($"  Literal count: {literalCount}");

            Console.WriteLine("\n  Literals:");
            for (int i = 0; i < literalCount; i++)
            {
                int slotIndex = i + 1; // +1 to skip header
                var literalOOP = heapManager.GetIndexedOOP(methodOOP, slotIndex);

                Console.WriteLine($"    [{i}] OOP={literalOOP}");

                try
                {
                    // Check if it's a SmallInteger
                    if (literalOOP.IsSmallInteger)
                    {
                        Console.WriteLine($"        SmallInteger: {literalOOP.AsSmallInteger()}");
                        continue;
                    }

                    // Check special values
                    if (literalOOP == SpecialObjectsArray.Instance.Nil)
                    {
                        Console.WriteLine($"        nil");
                        continue;
                    }
                    if (literalOOP == SpecialObjectsArray.Instance.True)
                    {
                        Console.WriteLine($"        true");
                        continue;
                    }
                    if (literalOOP == SpecialObjectsArray.Instance.False)
                    {
                        Console.WriteLine($"        false");
                        continue;
                    }

                    // Get object format
                    var format = heapManager.GetObjectFormat(literalOOP);
                    Console.WriteLine($"        Format: {format}");

                    // Get class OOP
                    var classOOP = heapManager.GetSlot(literalOOP, 0);
                    Console.WriteLine($"        Class OOP: {classOOP}");

                    if (format == ObjectFormat.Pointer)
                    {
                        // Check if it's an Association
                        try
                        {
                            var keyOOP = heapManager.GetInstanceVariable(literalOOP, 0);
                            var valueOOP = heapManager.GetInstanceVariable(literalOOP, 1);

                            Console.WriteLine($"        Pointer object with 2 fields:");
                            Console.WriteLine($"          Field[0] (key?): {keyOOP}");
                            Console.WriteLine($"          Field[1] (value?): {valueOOP}");

                            // Try to read key as Symbol
                            if (!keyOOP.IsSmallInteger && keyOOP != SpecialObjectsArray.Instance.Nil)
                            {
                                try
                                {
                                    var keyFormat = heapManager.GetObjectFormat(keyOOP);
                                    if (keyFormat == ObjectFormat.ByteIndexed)
                                    {
                                        int keyLen = heapManager.GetIndexedSize(keyOOP);
                                        if (keyLen > 0 && keyLen < 100)
                                        {
                                            var keyBytes = new byte[keyLen];
                                            for (int b = 0; b < keyLen; b++)
                                            {
                                                keyBytes[b] = heapManager.GetIndexedByte(keyOOP, b);
                                            }
                                            string keyName = System.Text.Encoding.Latin1.GetString(keyBytes);
                                            Console.WriteLine($"          Key is Symbol: '{keyName}'");
                                        }
                                    }
                                }
                                catch { }
                            }

                            // Check if value is SmallInteger
                            if (valueOOP.IsSmallInteger)
                            {
                                Console.WriteLine($"          Value is SmallInteger: {valueOOP.AsSmallInteger()}");
                            }
                            else if (valueOOP == SpecialObjectsArray.Instance.Nil)
                            {
                                Console.WriteLine($"          Value is nil");
                            }
                            else
                            {
                                Console.WriteLine($"          Value is object: {valueOOP}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"        ERROR reading as Association: {ex.Message}");
                        }
                    }
                    else if (format == ObjectFormat.ByteIndexed)
                    {
                        // Probably a Symbol
                        try
                        {
                            int len = heapManager.GetIndexedSize(literalOOP);
                            if (len > 0 && len < 100)
                            {
                                var bytes = new byte[len];
                                for (int b = 0; b < len; b++)
                                {
                                    bytes[b] = heapManager.GetIndexedByte(literalOOP, b);
                                }
                                string name = System.Text.Encoding.Latin1.GetString(bytes);
                                Console.WriteLine($"        ByteIndexed (Symbol?): '{name}'");
                            }
                        }
                        catch { }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"        ERROR inspecting literal: {ex.Message}");
                }
            }

            Console.WriteLine("\n[OK] File initialize literal inspection complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[ERROR] Error inspecting File initialize literals: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }
}
