namespace Smalltalk80.Interpreter.ColdStart.MethodInstallers;

using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Memory;

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
        magnitudeClassRef.AddMethod(">", SCompiledMethod.CreateCompiledMethodRef(
            ">",
            [0x10, 0x70, 0xB2, 0x7C], // arg < self
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // <= (less than or equal)
        magnitudeClassRef.AddMethod("<=", SCompiledMethod.CreateCompiledMethodRef(
            "<=",
            [0x70, 0x10, 0xB3, 0x9C, 0x7C], // (self > arg) not
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern(">")),
            1, 0, 0));
        instanceMethodCount++;
        
        // >= (greater than or equal)
        magnitudeClassRef.AddMethod(">=", SCompiledMethod.CreateCompiledMethodRef(
            ">=",
            [0x70, 0x10, 0xB2, 0x9C, 0x7C], // (self < arg) not
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // between:and:
        magnitudeClassRef.AddMethod("between:and:", SCompiledMethod.CreateCompiledMethodRef(
            "between:and:",
            [0x70, 0x10, 0xB5, 0x9D, 0x05, 0x70, 0x11, 0xB5, 0x91, 0x72, 0x7C], // self >= low and: [self <= high]
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern(">=")),
            2, 0, 0));
        instanceMethodCount++;
        
        // min:
        magnitudeClassRef.AddMethod("min:", SCompiledMethod.CreateCompiledMethodRef(
            "min:",
            [0x70, 0x10, 0xB2, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self < arg ifTrue: [self] ifFalse: [arg]
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("<")),
            1, 0, 0));
        instanceMethodCount++;
        
        // max:
        magnitudeClassRef.AddMethod("max:", SCompiledMethod.CreateCompiledMethodRef(
            "max:",
            [0x70, 0x10, 0xB3, 0x99, 0x02, 0x70, 0x7C, 0x10, 0x7C], // self > arg ifTrue: [self] ifFalse: [arg]
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern(">")),
            1, 0, 0));
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
        numberClassRef.AddMethod("negated", SCompiledMethod.CreateCompiledMethodRef(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("-")),
            0, 0, 0));
        instanceMethodCount++;
        
        // abs
        numberClassRef.AddMethod("abs", SCompiledMethod.CreateCompiledMethodRef(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("<"), SSymbol.Intern("negated")),
            0, 0, 0));
        instanceMethodCount++;
        
        // positive
        numberClassRef.AddMethod("positive", SCompiledMethod.CreateCompiledMethodRef(
            "positive",
            [0x70, 0x75, 0xB3, 0x7C], // self > 0
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern(">")),
            0, 0, 0));
        instanceMethodCount++;
        
        // negative
        numberClassRef.AddMethod("negative", SCompiledMethod.CreateCompiledMethodRef(
            "negative",
            [0x70, 0x75, 0xB2, 0x7C], // self < 0
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("<")),
            0, 0, 0));
        instanceMethodCount++;
        
        // isZero
        numberClassRef.AddMethod("isZero", SCompiledMethod.CreateCompiledMethodRef(
            "isZero",
            [0x70, 0x75, 0xB6, 0x7C], // self = 0
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("=")),
            0, 0, 0));
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
        smallIntegerClassRef.AddMethod("+", SCompiledMethod.CreateCompiledMethodRef(
            "+",
            [0x85, 0x10, 0xE0, 0x7C], // super + arg (fallback for primitive 1)
            [],
            1, 0, 1));
        instanceMethodCount++;
        
        // - (subtraction) - primitive 2
        smallIntegerClassRef.AddMethod("-", SCompiledMethod.CreateCompiledMethodRef(
            "-",
            [0x85, 0x10, 0xE1, 0x7C], // super - arg (fallback for primitive 2)
            [],
            1, 0, 2));
        instanceMethodCount++;
        
        // * (multiplication) - primitive 9
        smallIntegerClassRef.AddMethod("*", SCompiledMethod.CreateCompiledMethodRef(
            "*",
            [0x85, 0x10, 0xE0, 0x7C], // super * arg (fallback for primitive 9)
            [],
            1, 0, 9));
        instanceMethodCount++;
        
        // / (division) - primitive 10
        smallIntegerClassRef.AddMethod("/", SCompiledMethod.CreateCompiledMethodRef(
            "/",
            [0x85, 0x10, 0xE0, 0x7C], // super / arg (fallback for primitive 10)
            [],
            1, 0, 10));
        instanceMethodCount++;
        
        // // (integer division) - primitive 12
        smallIntegerClassRef.AddMethod("//", SCompiledMethod.CreateCompiledMethodRef(
            "//",
            [0x85, 0x10, 0xE0, 0x7C], // super // arg (fallback for primitive 12)
            [],
            1, 0, 12));
        instanceMethodCount++;
        
        // \\ (modulo) - primitive 11
        smallIntegerClassRef.AddMethod("\\\\", SCompiledMethod.CreateCompiledMethodRef(
            "\\\\",
            [0x85, 0x10, 0xE0, 0x7C], // super \\ arg (fallback for primitive 11)
            [],
            1, 0, 11));
        instanceMethodCount++;
        
        // < - primitive 3
        smallIntegerClassRef.AddMethod("<", SCompiledMethod.CreateCompiledMethodRef(
            "<",
            [0x72, 0x7C], // push false, return (fallback for primitive 3)
            [],
            1, 0, 3));
        instanceMethodCount++;
        
        // > - primitive 4
        smallIntegerClassRef.AddMethod(">", SCompiledMethod.CreateCompiledMethodRef(
            ">",
            [0x72, 0x7C], // push false, return (fallback for primitive 4)
            [],
            1, 0, 4));
        instanceMethodCount++;
        
        // <= - primitive 5
        smallIntegerClassRef.AddMethod("<=", SCompiledMethod.CreateCompiledMethodRef(
            "<=",
            [0x72, 0x7C], // push false, return (fallback for primitive 5)
            [],
            1, 0, 5));
        instanceMethodCount++;
        
        // >= - primitive 6
        smallIntegerClassRef.AddMethod(">=", SCompiledMethod.CreateCompiledMethodRef(
            ">=",
            [0x72, 0x7C], // push false, return (fallback for primitive 6)
            [],
            1, 0, 6));
        instanceMethodCount++;
        
        // = - primitive 7
        smallIntegerClassRef.AddMethod("=", SCompiledMethod.CreateCompiledMethodRef(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 7)
            [],
            1, 0, 7));
        instanceMethodCount++;
        
        // ~= - primitive 8
        smallIntegerClassRef.AddMethod("~=", SCompiledMethod.CreateCompiledMethodRef(
            "~=",
            [0x71, 0x7C], // push true, return (fallback for primitive 8)
            [],
            1, 0, 8));
        instanceMethodCount++;
        
        // negated
        smallIntegerClassRef.AddMethod("negated", SCompiledMethod.CreateCompiledMethodRef(
            "negated",
            [0x75, 0x70, 0xB1, 0x7C], // 0 - self
            [],
            0, 0, 0));
        instanceMethodCount++;
        
        // abs
        smallIntegerClassRef.AddMethod("abs", SCompiledMethod.CreateCompiledMethodRef(
            "abs",
            [0x70, 0x75, 0xB2, 0x9D, 0x04, 0x70, 0xD4, 0x7C, 0x70], // self < 0 ifTrue: [self negated] ifFalse: [self]
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("negated")),
            0, 0, 0));
        instanceMethodCount++;
        
        // asFloat - primitive 40
        smallIntegerClassRef.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethodRef(
            "asFloat",
            [], // No fallback needed - primitive 40 cannot fail
            [],
            0, 0, 40));
        instanceMethodCount++;
        
        // to:do: block iteration
        smallIntegerClassRef.AddMethod("to:do:", SCompiledMethod.CreateCompiledMethodRef(
            "to:do:",
            [0x70, 0x69, 0x10, 0xB5, 0xAC, 0x09, 0x11, 0x6A, 0xD5, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF4, 0x73, 0x7C],
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value:")),
            2, 1, 0));
        instanceMethodCount++;
        
        // timesRepeat: block iteration
        smallIntegerClassRef.AddMethod("timesRepeat:", SCompiledMethod.CreateCompiledMethodRef(
            "timesRepeat:",
            [0x76, 0x69, 0x70, 0xB5, 0xAC, 0x07, 0x10, 0xC9, 0x6A, 0x76, 0xB0, 0x69, 0xA3, 0xF6, 0x73, 0x7C],
            SCompiledMethod.ConvertLiteralsToRefs(SSymbol.Intern("value")),
            1, 1, 0));
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
        floatClassRef.AddMethod("+", SCompiledMethod.CreateCompiledMethodRef(
            "+",
            [0x85, 0x10, 0xE0, 0x7C], // super + arg (fallback for primitive 41)
            [],
            1, 0, 41));
        instanceMethodCount++;
        
        // - - primitive 42
        floatClassRef.AddMethod("-", SCompiledMethod.CreateCompiledMethodRef(
            "-",
            [0x85, 0x10, 0xE1, 0x7C], // super - arg (fallback for primitive 42)
            [],
            1, 0, 42));
        instanceMethodCount++;
        
        // * - primitive 49
        floatClassRef.AddMethod("*", SCompiledMethod.CreateCompiledMethodRef(
            "*",
            [0x85, 0x10, 0xE0, 0x7C], // super * arg (fallback for primitive 49)
            [],
            1, 0, 49));
        instanceMethodCount++;
        
        // / - primitive 50
        floatClassRef.AddMethod("/", SCompiledMethod.CreateCompiledMethodRef(
            "/",
            [0x85, 0x10, 0xE0, 0x7C], // super / arg (fallback for primitive 50)
            [],
            1, 0, 50));
        instanceMethodCount++;
        
        // < - primitive 43
        floatClassRef.AddMethod("<", SCompiledMethod.CreateCompiledMethodRef(
            "<",
            [0x72, 0x7C], // push false, return (fallback for primitive 43)
            [],
            1, 0, 43));
        instanceMethodCount++;
        
        // > - primitive 44
        floatClassRef.AddMethod(">", SCompiledMethod.CreateCompiledMethodRef(
            ">",
            [0x72, 0x7C], // push false, return (fallback for primitive 44)
            [],
            1, 0, 44));
        instanceMethodCount++;
        
        // <= - primitive 45
        floatClassRef.AddMethod("<=", SCompiledMethod.CreateCompiledMethodRef(
            "<=",
            [0x72, 0x7C], // push false, return (fallback for primitive 45)
            [],
            1, 0, 45));
        instanceMethodCount++;
        
        // >= - primitive 46
        floatClassRef.AddMethod(">=", SCompiledMethod.CreateCompiledMethodRef(
            ">=",
            [0x72, 0x7C], // push false, return (fallback for primitive 46)
            [],
            1, 0, 46));
        instanceMethodCount++;
        
        // = - primitive 47
        floatClassRef.AddMethod("=", SCompiledMethod.CreateCompiledMethodRef(
            "=",
            [0x72, 0x7C], // push false, return (fallback for primitive 47)
            [],
            1, 0, 47));
        instanceMethodCount++;
        
        // ~= - primitive 48
        floatClassRef.AddMethod("~=", SCompiledMethod.CreateCompiledMethodRef(
            "~=",
            [0x71, 0x7C], // push true, return (fallback for primitive 48)
            [],
            1, 0, 48));
        instanceMethodCount++;
        
        // truncate - primitive 51
        floatClassRef.AddMethod("truncate", SCompiledMethod.CreateCompiledMethodRef(
            "truncate",
            [], // No fallback needed - primitive 51 cannot fail
            [],
            0, 0, 51));
        instanceMethodCount++;
        
        // asFloat (returns self)
        floatClassRef.AddMethod("asFloat", SCompiledMethod.CreateCompiledMethodRef(
            "asFloat",
            [0x70, 0x7C], // ^self
            [],
            0, 0, 0));
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
        characterClassRef.AddMethod("asInteger", SCompiledMethod.CreateCompiledMethodRef(
            "asInteger",
            [], // No fallback needed - primitive 171 cannot fail
            [],
            0, 0, 171));
        instanceMethodCount++;
        
        // isLetter - checks if character is a letter (A-Z, a-z)
        characterClassRef.AddMethod("isLetter", SCompiledMethod.CreateCompiledMethodRef(
            "isLetter",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x41, 0xB5, 0x9D, 0x06, // >= 65 ($A)
             0x68, 0x22, 0x5A, 0xB5, 0x91, 0x71, 0x7C, // <= 90 ($Z) -> true
             0x68, 0x22, 0x61, 0xB5, 0x9D, 0x06, // >= 97 ($a)
             0x68, 0x22, 0x7A, 0xB5, 0x91, 0x71, 0x7C, // <= 122 ($z) -> true
             0x72, 0x7C], // false
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("asInteger"),
                new SmallInteger(65),
                SSymbol.Intern(">="),
                new SmallInteger(90),
                SSymbol.Intern("<="),
                new SmallInteger(97),
                new SmallInteger(122)),
            0, 1, 0));
        instanceMethodCount++;
        
        // isDigit - checks if character is a digit (0-9)
        characterClassRef.AddMethod("isDigit", SCompiledMethod.CreateCompiledMethodRef(
            "isDigit",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x30, 0xB5, 0x9D, 0x06, // >= 48 ($0)
             0x68, 0x22, 0x39, 0xB5, 0x91, 0x71, 0x7C, // <= 57 ($9) -> true
             0x72, 0x7C], // false
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("asInteger"),
                new SmallInteger(48),
                SSymbol.Intern(">="),
                new SmallInteger(57),
                SSymbol.Intern("<=")),
            0, 1, 0));
        instanceMethodCount++;
        
        // isAlphaNumeric - letter or digit
        characterClassRef.AddMethod("isAlphaNumeric", SCompiledMethod.CreateCompiledMethodRef(
            "isAlphaNumeric",
            [0x70, 0xD0, 0x99, 0x02, 0x71, 0x7C, // self isLetter ifTrue: [^true]
             0x70, 0xD1, 0x7C], // self isDigit
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("isLetter"),
                SSymbol.Intern("isDigit")),
            0, 0, 0));
        instanceMethodCount++;
        
        // isSeparator - space, tab, cr, lf, etc.
        characterClassRef.AddMethod("isSeparator", SCompiledMethod.CreateCompiledMethodRef(
            "isSeparator",
            [0x70, 0xD0, 0x68, // self asInteger
             0x22, 0x20, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 32 (space) -> true
             0x68, 0x22, 0x09, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 9 (tab) -> true
             0x68, 0x22, 0x0D, 0xB6, 0x99, 0x02, 0x71, 0x7C, // = 13 (cr) -> true
             0x68, 0x22, 0x0A, 0xB6, 0x7C], // = 10 (lf)
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("asInteger"),
                new SmallInteger(32),
                SSymbol.Intern("="),
                new SmallInteger(9),
                new SmallInteger(13),
                new SmallInteger(10)),
            0, 1, 0));
        instanceMethodCount++;
        
        // isSpecial - special characters used in Smalltalk syntax
        characterClassRef.AddMethod("isSpecial", SCompiledMethod.CreateCompiledMethodRef(
            "isSpecial",
            [0x21, 0x10, 0xE0, 0x7C], // '+/\\*~<>=@%|&?!' includes: self
            SCompiledMethod.ConvertLiteralsToRefs(
                SSymbol.Intern("+/\\*~<>=@%|&?!"),
                SSymbol.Intern("includes:")),
            0, 0, 0));
        instanceMethodCount++;
        
        // Note: Class methods would be installed on Character's metaclass
        // They would include: #value:
        
        if (Program.RuntimeOptions?.EnableColdStartLogging ?? false)
        {
            Console.WriteLine($"[COLD START] Character: {instanceMethodCount} instance methods installed");
        }
    }
}
