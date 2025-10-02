namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.Generated.Layouts;

/// <summary>
/// Installs kernel methods for numeric classes including Magnitude, Number, Integer, 
/// SmallInteger, Float, and Character.
/// </summary>
public static class NumericMethodsInstaller
{
    /// <summary>
    /// Installs all numeric-related methods.
    /// </summary>
    public static void Install()
    {
        InstallMagnitudeMethods();
        InstallNumberMethods();
        InstallIntegerMethods();
        InstallSmallIntegerMethods();
        InstallFloatMethods();
        InstallCharacterMethods();
    }
    
    /// <summary>
    /// Installs Magnitude methods.
    /// </summary>
    private static void InstallMagnitudeMethods()
    {
        var magnitudeClassRef = SystemDictionary.MagnitudeClassRef;
        if (magnitudeClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Magnitude instance methods: #<, #>, #<=, #>=, #between:and:, #min:, #max:");
        }
        
        // These are abstract in Magnitude, subclasses must implement <
        
        // > (greater than in terms of <)
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            ">",
            [0x10, 0x70, 0xB2, 0x7C], // arg < self
            [ObjectMemoryNew.InternSymbol("<")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), ">", method1);
        instanceMethodCount++;
        
        // <= (less than or equal)
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "<=",
            [0x70, 0x10, 0xB3, 0x9C, 0x7C], // (self > arg) not
            [ObjectMemoryNew.InternSymbol(">")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), "<=", method2);
        instanceMethodCount++;

        // >= (greater than or equal)
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            ">=",
            [0x70, 0x10, 0xB2, 0x9C, 0x7C], // (self < arg) not
            [ObjectMemoryNew.InternSymbol("<")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), ">=", method3);
        instanceMethodCount++;

        // between:and:
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "between:and:",
            [0x70, 0x10, 0xB5, 0x9D, 0x05, 0x70, 0x11, 0xB5, 0x91, 0x72, 0x7C], // self >= low and: [self <= high]
            [ObjectMemoryNew.InternSymbol(">=")],
            2, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), "between:and:", method4);
        instanceMethodCount++;

        // min:
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "min:",
            [0x70, 0x10, 0xB2, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self < arg ifTrue: [self] ifFalse: [arg]
            [ObjectMemoryNew.InternSymbol("<")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), "min:", method5);
        instanceMethodCount++;

        // max:
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "max:",
            [0x70, 0x10, 0xB3, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self > arg ifTrue: [self] ifFalse: [arg]
            [ObjectMemoryNew.InternSymbol(">")],
            1, 0, 0);
        ObjectMemoryNew.AddMethod(magnitudeClassRef.AsObjectPointer(), "max:", method6);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Magnitude: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Number methods.
    /// </summary>
    private static void InstallNumberMethods()
    {
        var numberClassRef = SystemDictionary.NumberClassRef;
        if (numberClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Number instance methods: #negated, #abs, #positive, #negative, #isZero");
        }
        
        // negated
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            [ObjectMemoryNew.InternSymbol("-")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(numberClassRef.AsObjectPointer(), "negated", method1);
        instanceMethodCount++;

        // abs
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            [ObjectMemoryNew.InternSymbol("<"), ObjectMemoryNew.InternSymbol("negated")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(numberClassRef.AsObjectPointer(), "abs", method2);
        instanceMethodCount++;

        // positive
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "positive",
            [0x70, 0x75, 0xB3, 0x7C], // self > 0
            [ObjectMemoryNew.InternSymbol(">")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(numberClassRef.AsObjectPointer(), "positive", method3);
        instanceMethodCount++;

        // negative
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "negative",
            [0x70, 0x75, 0xB2, 0x7C], // self < 0
            [ObjectMemoryNew.InternSymbol("<")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(numberClassRef.AsObjectPointer(), "negative", method4);
        instanceMethodCount++;

        // isZero
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "isZero",
            [0x70, 0x75, 0xB6, 0x7C], // self = 0
            [ObjectMemoryNew.InternSymbol("=")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(numberClassRef.AsObjectPointer(), "isZero", method5);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Number: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Integer methods.
    /// </summary>
    private static void InstallIntegerMethods()
    {
        var integerClassRef = SystemDictionary.IntegerClassRef;
        if (integerClassRef.IsNil) return;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Integer: 0 instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs SmallInteger methods.
    /// </summary>
    private static void InstallSmallIntegerMethods()
    {
        var smallIntegerClassRef = SystemDictionary.SmallIntegerClassRef;
        if (smallIntegerClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing SmallInteger instance methods: #+, #-, #*, #/, #//, #\\\\, #<, #>, #<=, #>=, #=, #~=, #negated, #abs, #min:, #max:, #between:and:, #to:, #to:do:, #to:by:do:, #timesRepeat:, #asFloat, #printOn:");
        }
        
        // + (addition) - primitive 1
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "+",
            [0x85, 0x10, 0xE0, 0x7C], // super + arg (fallback for primitive 1)
            [],
            1, 0, 1);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "+", method1);
        instanceMethodCount++;
        
        // - (subtraction) - primitive 2
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "-",
            [0x85, 0x10, 0xE1, 0x7C], // super - arg (fallback for primitive 2)
            [],
            1, 0, 2);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "-", method2);
        instanceMethodCount++;

        // * (multiplication) - primitive 9
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "*",
            [0x85, 0x10, 0xE0, 0x7C], // super * arg (fallback for primitive 9)
            [],
            1, 0, 9);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "*", method3);
        instanceMethodCount++;

        // / (division) - primitive 10
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "/",
            [0x85, 0x10, 0xE0, 0x7C], // super / arg (fallback for primitive 10)
            [],
            1, 0, 10);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "/", method4);
        instanceMethodCount++;

        // // (integer division) - primitive 12
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "//",
            [0x85, 0x10, 0xE0, 0x7C], // super // arg (fallback for primitive 12)
            [],
            1, 0, 12);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "//", method5);
        instanceMethodCount++;

        // \\ (modulo) - primitive 11
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "\\\\",
            [0x85, 0x10, 0xE0, 0x7C], // super \\ arg (fallback for primitive 11)
            [],
            1, 0, 11);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "\\\\", method6);
        instanceMethodCount++;

        // < - primitive 3
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "<",
            [0x72, 0x7C], // push false, return (fallback for primitive 3)
            [],
            1, 0, 3);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "<", method7);
        instanceMethodCount++;

        // > - primitive 4
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            ">",
            [0x72, 0x7C], // push false, return (fallback for primitive 4)
            [],
            1, 0, 4);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), ">", method8);
        instanceMethodCount++;

        // <= - primitive 5
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "<=",
            [0x72, 0x7C], // push false, return (fallback for primitive 5)
            [],
            1, 0, 5);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "<=", method9);
        instanceMethodCount++;

        // >= - primitive 6
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            ">=",
            [0x72, 0x7C], // push false, return (fallback for primitive 6)
            [],
            1, 0, 6);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), ">=", method10);
        instanceMethodCount++;

        // = - primitive 7
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 7)
            [],
            1, 0, 7);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "=", method11);
        instanceMethodCount++;

        // ~= - primitive 8
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "~=",
            [0x71, 0x7C], // push true, return (fallback for primitive 8)
            [],
            1, 0, 8);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "~=", method12);
        instanceMethodCount++;

        // negated
        var method13 = ObjectMemoryNew.CreateCompiledMethod(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "negated", method13);
        instanceMethodCount++;

        // abs
        var method14 = ObjectMemoryNew.CreateCompiledMethod(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            [ObjectMemoryNew.InternSymbol("negated")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "abs", method14);
        instanceMethodCount++;

        // asFloat - primitive 40
        var method15 = ObjectMemoryNew.CreateCompiledMethod(
            "asFloat",
            [], // No fallback needed - primitive 40 cannot fail
            [],
            0, 0, 40);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "asFloat", method15);
        instanceMethodCount++;

        // to:do: block iteration
        var method16 = ObjectMemoryNew.CreateCompiledMethod(
            "to:do:",
            [0x70, 0x69, 0x10, 0xB5, 0xAC, 0x09, 0x11, 0x6A, 0xD5, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF4, 0x73, 0x7C],
            [ObjectMemoryNew.InternSymbol("value:")],
            2, 1, 0);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "to:do:", method16);
        instanceMethodCount++;

        // timesRepeat: block iteration
        var method17 = ObjectMemoryNew.CreateCompiledMethod(
            "timesRepeat:",
            [0x76, 0x69, 0x70, 0xB5, 0xAC, 0x07, 0x10, 0xC9, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF6, 0x73, 0x7C],
            [ObjectMemoryNew.InternSymbol("value")],
            1, 1, 0);
        ObjectMemoryNew.AddMethod(smallIntegerClassRef.AsObjectPointer(), "timesRepeat:", method17);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] SmallInteger: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Float methods.
    /// </summary>
    private static void InstallFloatMethods()
    {
        var floatClassRef = SystemDictionary.FloatClassRef;
        if (floatClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Float instance methods: #+, #-, #*, #/, #<, #>, #<=, #>=, #=, #~=, #negated, #abs, #truncate, #rounded, #asFloat, #printOn:");
        }
        
        // + - primitive 41
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "+",
            [0x85, 0x10, 0xE0, 0x7C], // super + arg (fallback for primitive 41)
            [],
            1, 0, 41);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "+", method1);
        instanceMethodCount++;

        // - - primitive 42
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "-",
            [0x85, 0x10, 0xE1, 0x7C], // super - arg (fallback for primitive 42)
            [],
            1, 0, 42);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "-", method2);
        instanceMethodCount++;

        // * - primitive 49
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "*",
            [0x85, 0x10, 0xE0, 0x7C], // super * arg (fallback for primitive 49)
            [],
            1, 0, 49);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "*", method3);
        instanceMethodCount++;

        // / - primitive 50
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "/",
            [0x85, 0x10, 0xE0, 0x7C], // super / arg (fallback for primitive 50)
            [],
            1, 0, 50);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "/", method4);
        instanceMethodCount++;

        // < - primitive 43
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "<",
            [0x72, 0x7C], // push false, return (fallback for primitive 43)
            [],
            1, 0, 43);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "<", method5);
        instanceMethodCount++;

        // > - primitive 44
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            ">",
            [0x72, 0x7C], // push false, return (fallback for primitive 44)
            [],
            1, 0, 44);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), ">", method6);
        instanceMethodCount++;

        // <= - primitive 45
        var method7 = ObjectMemoryNew.CreateCompiledMethod(
            "<=",
            [0x72, 0x7C], // push false, return (fallback for primitive 45)
            [],
            1, 0, 45);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "<=", method7);
        instanceMethodCount++;

        // >= - primitive 46
        var method8 = ObjectMemoryNew.CreateCompiledMethod(
            ">=",
            [0x72, 0x7C], // push false, return (fallback for primitive 46)
            [],
            1, 0, 46);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), ">=", method8);
        instanceMethodCount++;

        // = - primitive 47
        var method9 = ObjectMemoryNew.CreateCompiledMethod(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 47)
            [],
            1, 0, 47);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "=", method9);
        instanceMethodCount++;

        // ~= - primitive 48
        var method10 = ObjectMemoryNew.CreateCompiledMethod(
            "~=",
            [0x71, 0x7C], // push true, return (fallback for primitive 48)
            [],
            1, 0, 48);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "~=", method10);
        instanceMethodCount++;

        // truncate - primitive 51
        var method11 = ObjectMemoryNew.CreateCompiledMethod(
            "truncate",
            [], // No fallback needed - primitive 51 cannot fail
            [],
            0, 0, 51);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "truncate", method11);
        instanceMethodCount++;

        // asFloat (returns self)
        var method12 = ObjectMemoryNew.CreateCompiledMethod(
            "asFloat",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(floatClassRef.AsObjectPointer(), "asFloat", method12);
        instanceMethodCount++;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Float: {instanceMethodCount} instance methods installed");
        }
    }
    
    /// <summary>
    /// Installs Character methods.
    /// </summary>
    private static void InstallCharacterMethods()
    {
        var characterClassRef = SystemDictionary.CharacterClassRef;
        if (characterClassRef.IsNil) return;
        
        var instanceMethodCount = 0;
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine("[COLD START] Installing Character instance methods: #=, #hash, #asInteger, #isLetter, #isDigit, #isAlphaNumeric, #isUppercase, #isLowercase, #isSeparator, #isSpecial");
        }
        
        // asInteger - primitive 171
        var method1 = ObjectMemoryNew.CreateCompiledMethod(
            "asInteger",
            [], // No fallback needed - primitive 171 cannot fail
            [],
            0, 0, 171);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "asInteger", method1);
        instanceMethodCount++;

        // isLetter - checks if character is a letter (A-Z, a-z)
        var method2 = ObjectMemoryNew.CreateCompiledMethod(
            "isLetter",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x41, 0xB5, 0x9D, 0x06, // >= 65 ($A)
             0x68, 0x22, 0x5A, 0xB5, 0x91, 0x71, 0x7C, // <= 90 ($Z) -> true
             0x68, 0x22, 0x61, 0xB5, 0x9D, 0x06, // >= 97 ($a)
             0x68, 0x22, 0x7A, 0xB5, 0x91, 0x71, 0x7C, // <= 122 ($z) -> true
             0x72, 0x7C], // false
            [ObjectMemoryNew.InternSymbol("asInteger"),
             SObjectRef.FromSmallInteger(65),
             ObjectMemoryNew.InternSymbol(">="),
             SObjectRef.FromSmallInteger(90),
             ObjectMemoryNew.InternSymbol("<="),
             SObjectRef.FromSmallInteger(97),
             SObjectRef.FromSmallInteger(122)],
            0, 1, 0);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "isLetter", method2);
        instanceMethodCount++;

        // isDigit - checks if character is a digit (0-9)
        var method3 = ObjectMemoryNew.CreateCompiledMethod(
            "isDigit",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x30, 0xB5, 0x9D, 0x06, // >= 48 ($0)
             0x68, 0x22, 0x39, 0xB5, 0x91, 0x71, 0x7C, // <= 57 ($9) -> true
             0x72, 0x7C], // false
            [ObjectMemoryNew.InternSymbol("asInteger"),
             SObjectRef.FromSmallInteger(48),
             ObjectMemoryNew.InternSymbol(">="),
             SObjectRef.FromSmallInteger(57),
             ObjectMemoryNew.InternSymbol("<=")],
            0, 1, 0);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "isDigit", method3);
        instanceMethodCount++;

        // isAlphaNumeric - letter or digit
        var method4 = ObjectMemoryNew.CreateCompiledMethod(
            "isAlphaNumeric",
            [0x70, 0xD0, 0x99, 0x02, 0x71, 0x7C, // self isLetter ifTrue: [^true]
             0x70, 0xD1, 0x7C], // self isDigit
            [ObjectMemoryNew.InternSymbol("isLetter"),
             ObjectMemoryNew.InternSymbol("isDigit")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "isAlphaNumeric", method4);
        instanceMethodCount++;

        // isSeparator - space, tab, cr, lf, etc.
        var method5 = ObjectMemoryNew.CreateCompiledMethod(
            "isSeparator",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x20, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 32 (space) -> true
             0x68, 0x22, 0x09, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 9 (tab) -> true
             0x68, 0x22, 0x0D, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 13 (cr) -> true
             0x68, 0x22, 0x0A, 0xB6, 0x7C], // = 10 (lf)
            [ObjectMemoryNew.InternSymbol("asInteger"),
             SObjectRef.FromSmallInteger(32),
             ObjectMemoryNew.InternSymbol("="),
             SObjectRef.FromSmallInteger(9),
             SObjectRef.FromSmallInteger(13),
             SObjectRef.FromSmallInteger(10)],
            0, 1, 0);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "isSeparator", method5);
        instanceMethodCount++;

        // isSpecial - special characters used in Smalltalk syntax
        var method6 = ObjectMemoryNew.CreateCompiledMethod(
            "isSpecial",
            [0x21, 0x10, 0xE0, 0x7C], // '+/\\*~<>=@%|&?!' includes: self
            [ObjectMemoryNew.InternSymbol("+/\\*~<>=@%|&?!"),
             ObjectMemoryNew.InternSymbol("includes:")],
            0, 0, 0);
        ObjectMemoryNew.AddMethod(characterClassRef.AsObjectPointer(), "isSpecial", method6);
        instanceMethodCount++;
        
        // Note: Class methods would be installed on Character's metaclass
        // They would include: #value:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Character: {instanceMethodCount} instance methods installed");
        }
    }
}
