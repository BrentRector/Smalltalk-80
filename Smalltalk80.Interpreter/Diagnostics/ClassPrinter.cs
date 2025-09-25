using System.Text;
using Core.Abstractions.Memory;
using Core.Memory;
using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter.Diagnostics;

/// <summary>
/// Pretty prints all classes and their methods with source code and bytecode.
/// </summary>
public class ClassPrinter
{
    private readonly IObjectMemory _objectMemory;
    private readonly ILogger<ClassPrinter> _logger;
    private Dictionary<OOP, string> _classNames = new();
    
    // Bytecode symbolic names from the Blue Book
    private static readonly Dictionary<byte, string> BytecodeNames = new()
    {
        // Stack bytecodes (0-119)
        // Push instance variable
        [0x00] = "pushReceiverVariable0",
        [0x01] = "pushReceiverVariable1",
        [0x02] = "pushReceiverVariable2",
        [0x03] = "pushReceiverVariable3",
        [0x04] = "pushReceiverVariable4",
        [0x05] = "pushReceiverVariable5",
        [0x06] = "pushReceiverVariable6",
        [0x07] = "pushReceiverVariable7",
        [0x08] = "pushReceiverVariable8",
        [0x09] = "pushReceiverVariable9",
        [0x0A] = "pushReceiverVariable10",
        [0x0B] = "pushReceiverVariable11",
        [0x0C] = "pushReceiverVariable12",
        [0x0D] = "pushReceiverVariable13",
        [0x0E] = "pushReceiverVariable14",
        [0x0F] = "pushReceiverVariable15",
        
        // Push temporary variable
        [0x10] = "pushTemporary0",
        [0x11] = "pushTemporary1",
        [0x12] = "pushTemporary2",
        [0x13] = "pushTemporary3",
        [0x14] = "pushTemporary4",
        [0x15] = "pushTemporary5",
        [0x16] = "pushTemporary6",
        [0x17] = "pushTemporary7",
        [0x18] = "pushTemporary8",
        [0x19] = "pushTemporary9",
        [0x1A] = "pushTemporary10",
        [0x1B] = "pushTemporary11",
        [0x1C] = "pushTemporary12",
        [0x1D] = "pushTemporary13",
        [0x1E] = "pushTemporary14",
        [0x1F] = "pushTemporary15",
        
        // Push literal constant
        [0x20] = "pushLiteral0",
        [0x21] = "pushLiteral1",
        [0x22] = "pushLiteral2",
        [0x23] = "pushLiteral3",
        [0x24] = "pushLiteral4",
        [0x25] = "pushLiteral5",
        [0x26] = "pushLiteral6",
        [0x27] = "pushLiteral7",
        [0x28] = "pushLiteral8",
        [0x29] = "pushLiteral9",
        [0x2A] = "pushLiteral10",
        [0x2B] = "pushLiteral11",
        [0x2C] = "pushLiteral12",
        [0x2D] = "pushLiteral13",
        [0x2E] = "pushLiteral14",
        [0x2F] = "pushLiteral15",
        [0x30] = "pushLiteral16",
        [0x31] = "pushLiteral17",
        [0x32] = "pushLiteral18",
        [0x33] = "pushLiteral19",
        [0x34] = "pushLiteral20",
        [0x35] = "pushLiteral21",
        [0x36] = "pushLiteral22",
        [0x37] = "pushLiteral23",
        [0x38] = "pushLiteral24",
        [0x39] = "pushLiteral25",
        [0x3A] = "pushLiteral26",
        [0x3B] = "pushLiteral27",
        [0x3C] = "pushLiteral28",
        [0x3D] = "pushLiteral29",
        [0x3E] = "pushLiteral30",
        [0x3F] = "pushLiteral31",
        
        // Push literal variable (global)
        [0x40] = "pushLiteralVariable0",
        [0x41] = "pushLiteralVariable1",
        [0x42] = "pushLiteralVariable2",
        [0x43] = "pushLiteralVariable3",
        [0x44] = "pushLiteralVariable4",
        [0x45] = "pushLiteralVariable5",
        [0x46] = "pushLiteralVariable6",
        [0x47] = "pushLiteralVariable7",
        [0x48] = "pushLiteralVariable8",
        [0x49] = "pushLiteralVariable9",
        [0x4A] = "pushLiteralVariable10",
        [0x4B] = "pushLiteralVariable11",
        [0x4C] = "pushLiteralVariable12",
        [0x4D] = "pushLiteralVariable13",
        [0x4E] = "pushLiteralVariable14",
        [0x4F] = "pushLiteralVariable15",
        [0x50] = "pushLiteralVariable16",
        [0x51] = "pushLiteralVariable17",
        [0x52] = "pushLiteralVariable18",
        [0x53] = "pushLiteralVariable19",
        [0x54] = "pushLiteralVariable20",
        [0x55] = "pushLiteralVariable21",
        [0x56] = "pushLiteralVariable22",
        [0x57] = "pushLiteralVariable23",
        [0x58] = "pushLiteralVariable24",
        [0x59] = "pushLiteralVariable25",
        [0x5A] = "pushLiteralVariable26",
        [0x5B] = "pushLiteralVariable27",
        [0x5C] = "pushLiteralVariable28",
        [0x5D] = "pushLiteralVariable29",
        [0x5E] = "pushLiteralVariable30",
        [0x5F] = "pushLiteralVariable31",
        
        // Store and pop
        [0x60] = "popIntoReceiverVariable0",
        [0x61] = "popIntoReceiverVariable1",
        [0x62] = "popIntoReceiverVariable2",
        [0x63] = "popIntoReceiverVariable3",
        [0x64] = "popIntoReceiverVariable4",
        [0x65] = "popIntoReceiverVariable5",
        [0x66] = "popIntoReceiverVariable6",
        [0x67] = "popIntoReceiverVariable7",
        
        // Store into temporary
        [0x68] = "popIntoTemporary0",
        [0x69] = "popIntoTemporary1",
        [0x6A] = "popIntoTemporary2",
        [0x6B] = "popIntoTemporary3",
        [0x6C] = "popIntoTemporary4",
        [0x6D] = "popIntoTemporary5",
        [0x6E] = "popIntoTemporary6",
        [0x6F] = "popIntoTemporary7",
        
        // Push special constants
        [0x70] = "pushReceiver",
        [0x71] = "pushTrue",
        [0x72] = "pushFalse",
        [0x73] = "pushNil",
        [0x74] = "pushMinusOne",
        [0x75] = "pushZero",
        [0x76] = "pushOne",
        [0x77] = "pushTwo",
        
        // Return bytecodes (120-125)
        [0x78] = "returnReceiver",
        [0x79] = "returnTrue",
        [0x7A] = "returnFalse",
        [0x7B] = "returnNil",
        [0x7C] = "returnStackTop",
        [0x7D] = "returnStackTopFromBlock",
        
        // Unused
        [0x7E] = "unused126",
        [0x7F] = "unused127",
        
        // Extended bytecodes
        [0x80] = "extendedPushBytecode",
        [0x81] = "extendedStoreBytecode",
        [0x82] = "extendedStoreAndPopBytecode",
        [0x83] = "singleExtendedSendBytecode",
        [0x84] = "doubleExtendedDoAnythingBytecode",
        [0x85] = "singleExtendedSuperBytecode",
        [0x86] = "secondExtendedSendBytecode",
        [0x87] = "pop",
        [0x88] = "duplicate",
        [0x89] = "pushActiveContext",
        
        // Unused 138-143
        [0x8A] = "unused138",
        [0x8B] = "unused139",
        [0x8C] = "unused140",
        [0x8D] = "unused141",
        [0x8E] = "unused142",
        [0x8F] = "unused143",
        
        // Short jump forward
        [0x90] = "shortJumpForward1",
        [0x91] = "shortJumpForward2",
        [0x92] = "shortJumpForward3",
        [0x93] = "shortJumpForward4",
        [0x94] = "shortJumpForward5",
        [0x95] = "shortJumpForward6",
        [0x96] = "shortJumpForward7",
        [0x97] = "shortJumpForward8",
        
        // Short conditional jump forward (false)
        [0x98] = "shortConditionalJumpForward1",
        [0x99] = "shortConditionalJumpForward2",
        [0x9A] = "shortConditionalJumpForward3",
        [0x9B] = "shortConditionalJumpForward4",
        [0x9C] = "shortConditionalJumpForward5",
        [0x9D] = "shortConditionalJumpForward6",
        [0x9E] = "shortConditionalJumpForward7",
        [0x9F] = "shortConditionalJumpForward8",
        
        // Long jumps (160-167)
        [0xA0] = "longJumpForward1",
        [0xA1] = "longJumpForward2",
        [0xA2] = "longJumpForward3",
        [0xA3] = "longJumpForward4",
        [0xA4] = "longJumpBackward1",
        [0xA5] = "longJumpBackward2",
        [0xA6] = "longJumpBackward3",
        [0xA7] = "longJumpBackward4",
        
        // Conditional jumps on true
        [0xA8] = "conditionalJumpOnTrueForward1",
        [0xA9] = "conditionalJumpOnTrueForward2",
        [0xAA] = "conditionalJumpOnTrueForward3",
        [0xAB] = "conditionalJumpOnTrueForward4",
        
        // Conditional jumps on false
        [0xAC] = "conditionalJumpOnFalseForward1",
        [0xAD] = "conditionalJumpOnFalseForward2",
        [0xAE] = "conditionalJumpOnFalseForward3",
        [0xAF] = "conditionalJumpOnFalseForward4",
        
        // Send arithmetic selectors
        [0xB0] = "sendArithmeticAdd",
        [0xB1] = "sendArithmeticSubtract",
        [0xB2] = "sendArithmeticLessThan",
        [0xB3] = "sendArithmeticGreaterThan",
        [0xB4] = "sendArithmeticLessOrEqual",
        [0xB5] = "sendArithmeticGreaterOrEqual",
        [0xB6] = "sendArithmeticEqual",
        [0xB7] = "sendArithmeticNotEqual",
        [0xB8] = "sendArithmeticMultiply",
        [0xB9] = "sendArithmeticDivide",
        [0xBA] = "sendArithmeticMod",
        [0xBB] = "sendArithmeticMakePoint",
        [0xBC] = "sendArithmeticBitShift",
        [0xBD] = "sendArithmeticIntegerDivision",
        [0xBE] = "sendArithmeticBitAnd",
        [0xBF] = "sendArithmeticBitOr",
        
        // Send special selectors
        [0xC0] = "sendAt",
        [0xC1] = "sendAtPut",
        [0xC2] = "sendSize",
        [0xC3] = "sendNext",
        [0xC4] = "sendNextPut",
        [0xC5] = "sendAtEnd",
        [0xC6] = "sendEquivalent",
        [0xC7] = "sendClass",
        [0xC8] = "sendBlockCopy",
        [0xC9] = "sendValue",
        [0xCA] = "sendValueWithArg",
        [0xCB] = "sendDo",
        [0xCC] = "sendNew",
        [0xCD] = "sendNewWithArg",
        [0xCE] = "sendX",
        [0xCF] = "sendY",
        
        // Send literal selector with 0 arguments
        [0xD0] = "sendLiteralSelector0Args0",
        [0xD1] = "sendLiteralSelector0Args1",
        [0xD2] = "sendLiteralSelector0Args2",
        [0xD3] = "sendLiteralSelector0Args3",
        [0xD4] = "sendLiteralSelector0Args4",
        [0xD5] = "sendLiteralSelector0Args5",
        [0xD6] = "sendLiteralSelector0Args6",
        [0xD7] = "sendLiteralSelector0Args7",
        [0xD8] = "sendLiteralSelector0Args8",
        [0xD9] = "sendLiteralSelector0Args9",
        [0xDA] = "sendLiteralSelector0Args10",
        [0xDB] = "sendLiteralSelector0Args11",
        [0xDC] = "sendLiteralSelector0Args12",
        [0xDD] = "sendLiteralSelector0Args13",
        [0xDE] = "sendLiteralSelector0Args14",
        [0xDF] = "sendLiteralSelector0Args15",
        
        // Send literal selector with 1 argument
        [0xE0] = "sendLiteralSelector1Args0",
        [0xE1] = "sendLiteralSelector1Args1",
        [0xE2] = "sendLiteralSelector1Args2",
        [0xE3] = "sendLiteralSelector1Args3",
        [0xE4] = "sendLiteralSelector1Args4",
        [0xE5] = "sendLiteralSelector1Args5",
        [0xE6] = "sendLiteralSelector1Args6",
        [0xE7] = "sendLiteralSelector1Args7",
        [0xE8] = "sendLiteralSelector1Args8",
        [0xE9] = "sendLiteralSelector1Args9",
        [0xEA] = "sendLiteralSelector1Args10",
        [0xEB] = "sendLiteralSelector1Args11",
        [0xEC] = "sendLiteralSelector1Args12",
        [0xED] = "sendLiteralSelector1Args13",
        [0xEE] = "sendLiteralSelector1Args14",
        [0xEF] = "sendLiteralSelector1Args15",
        
        // Send literal selector with 2 arguments
        [0xF0] = "sendLiteralSelector2Args0",
        [0xF1] = "sendLiteralSelector2Args1",
        [0xF2] = "sendLiteralSelector2Args2",
        [0xF3] = "sendLiteralSelector2Args3",
        [0xF4] = "sendLiteralSelector2Args4",
        [0xF5] = "sendLiteralSelector2Args5",
        [0xF6] = "sendLiteralSelector2Args6",
        [0xF7] = "sendLiteralSelector2Args7",
        [0xF8] = "sendLiteralSelector2Args8",
        [0xF9] = "sendLiteralSelector2Args9",
        [0xFA] = "sendLiteralSelector2Args10",
        [0xFB] = "sendLiteralSelector2Args11",
        [0xFC] = "sendLiteralSelector2Args12",
        [0xFD] = "sendLiteralSelector2Args13",
        [0xFE] = "sendLiteralSelector2Args14",
        [0xFF] = "sendLiteralSelector2Args15"
    };

    public ClassPrinter(IObjectMemory objectMemory, ILogger<ClassPrinter> logger)
    {
        _objectMemory = objectMemory ?? throw new ArgumentNullException(nameof(objectMemory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Pretty prints all classes and their methods.
    /// </summary>
    public void PrintAllClasses(IReadOnlyDictionary<string, OOP> registeredClasses)
    {
        _logger.LogInformation("");
        _logger.LogInformation(new string('=', 80));
        _logger.LogInformation("SMALLTALK-80 CLASS HIERARCHY");
        _logger.LogInformation(new string('=', 80));
        _logger.LogInformation("");

        // Get all classes from the system
        var classes = registeredClasses.Values.ToList();
        
        // Create a mapping from OOP to name for easy lookup and store it
        _classNames = registeredClasses.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
        
        foreach (var kvp in registeredClasses.OrderBy(kvp => kvp.Key))
        {
            PrintClass(kvp.Value, kvp.Key, _classNames);
        }
        
        _logger.LogInformation("");
        _logger.LogInformation(new string('=', 80));
        _logger.LogInformation("Total Classes: {Count}", registeredClasses.Count);
        _logger.LogInformation(new string('=', 80));
        _logger.LogInformation("");
    }

    private void PrintClass(OOP classOop, string className, Dictionary<OOP, string> classNames)
    {
        var superclassOop = GetSuperclass(classOop);
        var superclassName = superclassOop.IsNil ? "" : (classNames.TryGetValue(superclassOop, out var name) ? name : $"<unknown:{superclassOop.Value:X8}>");
        
        _logger.LogInformation(new string('-', 60));
        _logger.LogInformation("CLASS: {ClassName}", className);
        if (!string.IsNullOrEmpty(superclassName))
        {
            _logger.LogInformation("  Superclass: {SuperclassName}", superclassName);
        }
        
        // Print instance variables
        var instVars = GetInstanceVariables(classOop);
        if (instVars.Count > 0)
        {
            _logger.LogInformation("  Instance Variables: {InstanceVars}", string.Join(", ", instVars));
        }
        
        // Print class variables
        var classVars = GetClassVariables(classOop);
        if (classVars.Count > 0)
        {
            _logger.LogInformation("  Class Variables: {ClassVars}", string.Join(", ", classVars));
        }
        
        _logger.LogInformation("");
        
        // Print instance methods
        var instanceMethods = GetMethods(classOop, false);
        if (instanceMethods.Count > 0)
        {
            _logger.LogInformation("  INSTANCE METHODS:");
            foreach (var (selector, methodOop) in instanceMethods.OrderBy(m => m.selector))
            {
                PrintMethod(selector, methodOop, "    ");
            }
        }
        
        // Print class methods (from metaclass)
        var metaclassOop = _objectMemory.FetchClassOf(classOop);
        if (!metaclassOop.IsNil && !metaclassOop.IsSmallInteger)
        {
            _logger.LogTrace("Looking for class methods in metaclass {Metaclass} for class {Class}", metaclassOop, classOop);
            var classMethods = GetMethods(metaclassOop, true);  // Pass true for classSide
            if (classMethods.Count > 0)
            {
                _logger.LogInformation("\n  CLASS METHODS:");
                foreach (var (selector, methodOop) in classMethods.OrderBy(m => m.selector))
                {
                    PrintMethod(selector, methodOop, "    ");
                }
            }
            else
            {
                _logger.LogTrace("No class methods found for class {Class}", className);
            }
        }
    }

    private void PrintMethod(string selector, OOP methodOop, string indent)
    {
        _logger.LogInformation("{Indent}{Selector}", indent, selector);
        
        // Get method details
        var (source, bytecodes, literals, primitiveIndex) = GetMethodDetails(methodOop);
        
        // Print source code if available
        if (!string.IsNullOrEmpty(source))
        {
            _logger.LogInformation("{Indent}  Source:", indent);
            var sourceLines = source.Split('\n');
            foreach (var line in sourceLines)
            {
                _logger.LogInformation("{Indent}    {Line}", indent, line);
            }
        }
        
        // Print primitive index if present
        if (primitiveIndex > 0)
        {
            _logger.LogInformation("{Indent}  Primitive: <primitive: {PrimitiveIndex}>", indent, primitiveIndex);
        }
        
        // Always print literals array (even if empty) to help understand bytecode
        _logger.LogInformation("{Indent}  Literals: {Literals}", indent, (literals.Count == 0 ? "none" : ""));
        for (int i = 0; i < literals.Count; i++)
        {
            _logger.LogInformation("{Indent}    [{Index}] {Literal}", indent, i, FormatLiteral(literals[i]));
        }
        
        // Print bytecode
        if (bytecodes.Length > 0)
        {
            _logger.LogInformation("{Indent}  Bytecode ({Length} bytes):", indent, bytecodes.Length);
            PrintBytecode(bytecodes, indent + "    ");
        }
        else
        {
            _logger.LogInformation("{Indent}  Bytecode: <no bytecode>", indent);
        }
        _logger.LogInformation("");
    }

    private void PrintBytecode(byte[] bytecodes, string indent)
    {
        for (int pc = 0; pc < bytecodes.Length; pc++)
        {
            byte bytecode = bytecodes[pc];
            string name = BytecodeNames.TryGetValue(bytecode, out var n) ? n : "unknown";
            
            var logMessage = $"{indent}[{pc:D3}] {name} ({bytecode:X2})";
            
            // Handle extended bytecodes that have additional bytes
            if (IsExtendedBytecode(bytecode) && pc + 1 < bytecodes.Length)
            {
                pc++;
                byte extension = bytecodes[pc];
                logMessage += $" ext:({extension:X2})";
            }
            
            _logger.LogInformation(logMessage);
        }
    }

    private bool IsExtendedBytecode(byte bytecode)
    {
        // Extended bytecodes that consume an additional byte
        return bytecode >= 0x80 && bytecode <= 0x86;
    }

    private string FormatLiteral(OOP literal)
    {
        if (literal.IsSmallInteger)
        {
            return literal.SmallIntegerValue.ToString();
        }
        
        // Try to read as string first (handles both Strings and Symbols)
        var str = GetStringValue(literal);
        if (!string.IsNullOrEmpty(str))
        {
            // Get the class to determine if it's a Symbol or String
            var classOop = _objectMemory.FetchClassOf(literal);
            
            // Check if this is a Symbol (we can't easily get the class name, but Symbols
            // typically have simple selector-like content without spaces or newlines)
            // A more robust check would be to compare classOop with the Symbol class OOP
            // For now, use heuristics based on content
            if (str.All(c => char.IsLetterOrDigit(c) || c == ':' || c == '_' || c == '<' || c == '>' || c == '=' || c == '+' || c == '-' || c == '*' || c == '/' || c == '\\' || c == '@' || c == '~' || c == '|' || c == '&' || c == ',' || c == '%'))
            {
                // Looks like a selector/symbol
                return $"#{str}";
            }
            else if (str.Contains('\n') || str.Contains('\t'))
            {
                // Multi-line or contains tabs, likely a String
                return $"'{str.Replace("\n", "\\n").Replace("\t", "\\t")}'";
            }
            else
            {
                // Default to String
                return $"'{str}'";
            }
        }
        
        // Try to get class name for other types
        var className = GetClassNameOf(literal);
        
        // Check if this is an Association
        if (IsAssociation(literal))
        {
            var key = _objectMemory.FetchPointer(literal, 0);
            var value = _objectMemory.FetchPointer(literal, 1);
            
            // Get the key as a string (should be a Symbol)
            var keyStr = GetStringValue(key);
            if (string.IsNullOrEmpty(keyStr))
            {
                keyStr = $"<{FormatOOP(key)}>";
            }
            
            // Format the value
            string valueStr;
            if (value.IsNil)
            {
                valueStr = "nil";
            }
            else if (value.IsSmallInteger)
            {
                valueStr = value.SmallIntegerValue.ToString();
            }
            else if (_classNames.ContainsKey(value))
            {
                // The value itself is a class object - show its name
                valueStr = _classNames[value];
            }
            else
            {
                // For other objects, show their class
                var valueClass = GetClassNameOf(value);
                valueStr = $"<{valueClass}>";
            }
            
            return $"Association(key: #{keyStr}, value: {valueStr})";
        }
        
        if (className == "Character")
        {
            var charValue = GetCharacterValue(literal);
            return $"${charValue}";
        }
        
        if (className == "Array")
        {
            return $"#(...) [{GetArraySize(literal)} elements]";
        }
        
        return $"<{className}>";
    }

    // Helper methods to interact with ObjectMemory
    
    private OOP GetSuperclass(OOP classOop)
    {
        // Superclass is field 0 in Behavior per Blue Book
        return _objectMemory.FetchPointer(classOop, 0);
    }
    
    
    private List<string> GetInstanceVariables(OOP classOop)
    {
        // Instance variable names are in field 4 of ClassDescription (after superclass, methodDict, format, subclasses)
        try
        {
            var instVarNamesOop = _objectMemory.FetchPointer(classOop, 4);
            if (!instVarNamesOop.IsNil)
            {
                return ParseStringArray(instVarNamesOop);
            }
        }
        catch { }
        return new List<string>();
    }
    
    private List<string> GetClassVariables(OOP classOop)
    {
        // Class variables are in field 7 of Class (after name, comment, classVarNames)
        try
        {
            var classVarNamesOop = _objectMemory.FetchPointer(classOop, 7);
            if (!classVarNamesOop.IsNil)
            {
                return ParseStringArray(classVarNamesOop);
            }
        }
        catch { }
        return new List<string>();
    }
    
    private List<string> ParseStringArray(OOP arrayOop)
    {
        var result = new List<string>();
        try
        {
            var size = _objectMemory.FetchWordLengthOf(arrayOop);
            for (int i = 0; i < size; i++)
            {
                var stringOop = _objectMemory.FetchPointer(arrayOop, i);
                if (!stringOop.IsNil)
                {
                    var str = GetStringValue(stringOop);
                    if (!string.IsNullOrEmpty(str))
                    {
                        result.Add(str);
                    }
                }
            }
        }
        catch { }
        return result;
    }
    
    private List<(string selector, OOP method)> GetMethods(OOP classOop, bool classSide)
    {
        var methods = new List<(string, OOP)>();
        
        try
        {
            // Check if classOop is valid (not nil or SmallInteger)
            if (classOop.IsNil || classOop.IsSmallInteger)
            {
                _logger.LogTrace("Class OOP is nil or SmallInteger, skipping method lookup");
                return methods;
            }
            
            // Method dictionary is in field 1 of Behavior
            var methodDictOop = _objectMemory.FetchPointer(classOop, 1);
            if (methodDictOop.IsNil || methodDictOop.IsSmallInteger)
            {
                _logger.LogTrace("Method dictionary is nil or SmallInteger, class may not be fully initialized");
                return methods;
            }
            
            // MethodDictionary has tally at field 0, array at field 1
            var methodArrayOop = _objectMemory.FetchPointer(methodDictOop, 1);
            if (methodArrayOop.IsNil || methodArrayOop.IsSmallInteger)
            {
                _logger.LogTrace("Method array is nil or SmallInteger");
                return methods;
            }
            
            // The array contains alternating keys (selectors) and values (methods)
            var arraySize = _objectMemory.FetchWordLengthOf(methodArrayOop);
            _logger.LogTrace("Method array size: {Size} for class {Class}", arraySize, classOop);
            
            for (int i = 0; i < arraySize; i += 2)
            {
                var selectorOop = _objectMemory.FetchPointer(methodArrayOop, i);
                if (!selectorOop.IsNil && !selectorOop.IsSmallInteger && i + 1 < arraySize)
                {
                    var methodOop = _objectMemory.FetchPointer(methodArrayOop, i + 1);
                    if (!methodOop.IsNil && !methodOop.IsSmallInteger)
                    {
                        var selector = GetStringValue(selectorOop);
                        if (!string.IsNullOrEmpty(selector))
                        {
                            methods.Add((selector, methodOop));
                            _logger.LogTrace("Found method: {Selector} at index {Index}", selector, i);
                        }
                        else
                        {
                            _logger.LogTrace("Selector at index {Index} has no string value", i);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Error getting methods for class: {Message}", ex.Message);
        }
        
        return methods;
    }
    
    private (string source, byte[] bytecodes, List<OOP> literals, int primitiveIndex) GetMethodDetails(OOP methodOop)
    {
        try
        {
            // CompiledMethod structure per ColdStartBootstrap:
            // Field 0: header (SmallInteger)
            // Field 1: primitive index (SmallInteger)
            // Field 2: literals array (Array)
            // Field 3: bytecodes (ByteArray)
            
            // Get primitive index from field 1
            var primitiveIndexOop = _objectMemory.FetchPointer(methodOop, 1);
            int primitiveIndex = 0;
            if (primitiveIndexOop.IsSmallInteger)
            {
                primitiveIndex = primitiveIndexOop.SmallIntegerValue;
            }
            
            // Get literals from field 2
            var literals = new List<OOP>();
            var literalsArrayOop = _objectMemory.FetchPointer(methodOop, 2);
            if (!literalsArrayOop.IsNil && !literalsArrayOop.IsSmallInteger)
            {
                var literalCount = _objectMemory.FetchWordLengthOf(literalsArrayOop);
                for (int i = 0; i < literalCount; i++)
                {
                    literals.Add(_objectMemory.FetchPointer(literalsArrayOop, i));
                }
            }
            
            // Get bytecodes from field 3 (ByteArray)
            var bytecodes = GetBytecodesFromByteArray(methodOop);
            
            // Try to get source - it might be in the first literal as a String
            string source = "";
            if (literals.Count > 0)
            {
                // Try first literal as source string
                var firstLiteral = literals[0];
                var possibleSource = GetStringValue(firstLiteral);
                if (!string.IsNullOrEmpty(possibleSource))
                {
                    source = possibleSource;
                }
                else if (literals.Count > 1)
                {
                    // Try last literal as an Association
                    var lastLiteral = literals[literals.Count - 1];
                    source = TryGetSourceFromAssociation(lastLiteral);
                }
            }
            
            // For now, if no source is stored, we can show a placeholder
            if (string.IsNullOrEmpty(source))
            {
                source = "";//"<source not available in bootstrap>";
            }
            
            return (source, bytecodes, literals, primitiveIndex);
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Error getting method details: {Message}", ex.Message);
            return ("<error>", Array.Empty<byte>(), new List<OOP>(), 0);
        }
    }
    
    private byte[] GetBytecodesFromByteArray(OOP methodOop)
    {
        var bytecodes = new List<byte>();
        try
        {
            // Bytecodes are in field 3 as a ByteArray
            var byteArrayOop = _objectMemory.FetchPointer(methodOop, 3);
            
            if (!byteArrayOop.IsNil && !byteArrayOop.IsSmallInteger)
            {
                // Get the size of the ByteArray in words and convert to bytes
                var wordCount = _objectMemory.FetchWordLengthOf(byteArrayOop);
                var maxByteCount = wordCount * 4; // 4 bytes per word
                
                // Read each byte until we hit a zero or reach the end
                for (int i = 0; i < maxByteCount; i++)
                {
                    try
                    {
                        var b = _objectMemory.FetchByte(byteArrayOop, i);
                        if (b == 0 && bytecodes.Count > 0)
                        {
                            // Stop at null terminator if we've already read some bytes
                            break;
                        }
                        bytecodes.Add(b);
                    }
                    catch
                    {
                        // Reached end of actual bytes
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogTrace("Error extracting bytecodes: {Message}", ex.Message);
        }
        return bytecodes.ToArray();
    }
    
    private string TryGetSourceFromAssociation(OOP assocOop)
    {
        // Association has key at field 0, value at field 1
        // Source code might be stored as the value
        try
        {
            var valueOop = _objectMemory.FetchPointer(assocOop, 1);
            if (!valueOop.IsNil)
            {
                return GetStringValue(valueOop);
            }
        }
        catch { }
        return "";
    }
    
    private string GetClassNameOf(OOP objectOop)
    {
        var classOop = _objectMemory.FetchClassOf(objectOop);
        
        // Look up the class name in our dictionary
        if (_classNames.TryGetValue(classOop, out var className))
        {
            return className;
        }
        
        // If not found, return a generic class description
        return $"<Class:{classOop.Value:X8}>";
    }
    
    private bool IsAssociation(OOP objectOop)
    {
        // Check if this object's class is Association
        // We can't easily get the Association class OOP without a reference,
        // but we can check if it has exactly 2 fields (key and value)
        try
        {
            var size = _objectMemory.FetchWordLengthOf(objectOop);
            if (size == 2)
            {
                // Further check: Associations typically have a Symbol as key
                var key = _objectMemory.FetchPointer(objectOop, 0);
                var keyStr = GetStringValue(key);
                // If the key looks like a symbol/selector, it's likely an Association
                return !string.IsNullOrEmpty(keyStr);
            }
        }
        catch { }
        return false;
    }
    
    private string FormatOOP(OOP oop)
    {
        if (oop.IsNil) return "nil";
        if (oop.IsSmallInteger) return oop.SmallIntegerValue.ToString();
        return $"OOP({oop.Value:X8})";
    }
    
    private string GetStringValue(OOP stringOop)
    {
        try
        {
            var bytes = new List<byte>();
            
            // Try to read bytes directly - for ByteArrays (including Symbols),
            // we need to read byte by byte until we get an exception or null
            int index = 0;
            while (index < 1000) // Safety limit
            {
                try
                {
                    var b = _objectMemory.FetchByte(stringOop, index);
                    if (b == 0) break; // Stop at null terminator
                    bytes.Add(b);
                    index++;
                }
                catch
                {
                    // Reached end of object
                    break;
                }
            }
            
            if (bytes.Count == 0)
            {
                _logger.LogTrace("GetStringValue: No bytes read from OOP {OOP}", stringOop);
            }
            
            return System.Text.Encoding.UTF8.GetString(bytes.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogTrace("GetStringValue exception for OOP {OOP}: {Message}", stringOop, ex.Message);
            return "";
        }
    }
    
    private char GetCharacterValue(OOP charOop)
    {
        try
        {
            // Character value is typically stored as a SmallInteger
            var valueOop = _objectMemory.FetchPointer(charOop, 0);
            if (valueOop.IsSmallInteger)
            {
                return (char)valueOop.SmallIntegerValue;
            }
        }
        catch { }
        return '?';
    }
    
    private int GetArraySize(OOP arrayOop)
    {
        try
        {
            return _objectMemory.FetchWordLengthOf(arrayOop);
        }
        catch
        {
            return 0;
        }
    }
}