using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Smalltalk80.Parser;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Validates that all test case files in the systematic parser test suite
/// behave as expected - positive cases should parse and negative cases should fail.
/// </summary>
public class SystematicTestCaseValidation
{
    private static readonly string TestCasesRoot = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "Smalltalk80", "Parser.TestCases");

    private static readonly string FailCasesRoot = Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "Smalltalk80", "Parser.FailCases");

    #region Positive Test Case Validation

    [Theory]
    [MemberData(nameof(GetLevel1TestFiles))]
    public void Level1_BasicClasses_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    [Theory]
    [MemberData(nameof(GetLevel2TestFiles))]
    public void Level2_SimpleMethods_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    [Theory]
    [MemberData(nameof(GetLevel3TestFiles))]
    public void Level3_MethodsWithTemporaries_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    [Theory]
    [MemberData(nameof(GetLevel4TestFiles))]
    public void Level4_InstanceVariables_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    [Theory]
    [MemberData(nameof(GetLevel5TestFiles))]
    public void Level5_Primitives_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    [Theory]
    [MemberData(nameof(GetLevel6TestFiles))]
    public void Level6_ClassMethods_ShouldParseSuccessfully(string filePath)
    {
        ValidatePositiveCase(filePath);
    }

    #endregion

    #region Negative Test Case Validation

    [Theory]
    [MemberData(nameof(GetSystematicFailCases))]
    public void SystematicFailCases_ShouldFailToParse(string filePath)
    {
        ValidateNegativeCase(filePath);
    }

    #endregion

    #region Integration Tests with Runtime Validation

    [Fact]
    public void Level1_EmptyClass_CreatesCorrectRuntimeStructure()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var filePath = GetTestFile("Level1-BasicClasses", "empty_class.st");

        vm.LoadSystemFile(filePath);

        Assert.True(SystemDictionary.TryGetClass("EmptyClass", out var cls));
        Assert.True(SystemDictionary.TryGetClass("Object", out var objCls));
        Assert.Equal(objCls, cls!.Superclass);
        Assert.Empty(cls.InstanceVariables);
        Assert.Empty(cls.ClassVariables);
        Assert.Empty(cls.PoolDictionaries);
        Assert.Equal("Test-Basic", cls.Category);
    }

    [Fact]
    public void Level2_UnaryMethods_CreatesCorrectMethodsInRuntime()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var filePath = GetTestFile("Level2-SimpleMethods", "unary_methods.st");

        vm.LoadSystemFile(filePath);

        Assert.True(SystemDictionary.TryGetClass("ClassWithUnaryMethod", out var cls));
        Assert.True(cls!.MethodDictionary.ContainsKey(new Symbol("getValue")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("getName")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("isReady")));

        // Validate method properties
        var method = cls.MethodDictionary[new Symbol("getValue")];
        Assert.False(method.HasPrimitive);
        Assert.Equal(0, method.TemporariesCount);
    }

    [Fact]
    public void Level3_MethodsWithTemporaries_HasCorrectTemporaryCount()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var filePath = GetTestFile("Level3-MethodsWithTemporaries", "methods_with_temporaries.st");

        vm.LoadSystemFile(filePath);

        Assert.True(SystemDictionary.TryGetClass("ClassWithTemporaries", out var cls));

        var computeMethod = cls!.MethodDictionary[new Symbol("compute")];
        Assert.Equal(2, computeMethod.TemporariesCount); // temp1, temp2

        var processMethod = cls.MethodDictionary[new Symbol("processData:")];
        Assert.Equal(3, processMethod.TemporariesCount); // data (param) + result, index

        var complexMethod = cls.MethodDictionary[new Symbol("complexCalculation:with:")];
        Assert.Equal(5, complexMethod.TemporariesCount); // x, y (params) + intermediate1, intermediate2, final
    }

    [Fact]
    public void Level4_InstanceVariables_AccessibleInMethods()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var filePath = GetTestFile("Level4-InstanceVariables", "point_class.st");

        vm.LoadSystemFile(filePath);

        Assert.True(SystemDictionary.TryGetClass("Point", out var cls));
        Assert.Contains("x", cls!.InstanceVariables);
        Assert.Contains("y", cls.InstanceVariables);

        // Verify accessors exist
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("x")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("x:")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("y")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("y:")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("setX:y:")));
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol("distanceFromOrigin")));
    }

    [Fact]
    public void Level5_Primitives_HasCorrectPrimitiveInfo()
    {
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        var filePath = GetTestFile("Level5-Primitives", "primitive_methods.st");

        vm.LoadSystemFile(filePath);

        Assert.True(SystemDictionary.TryGetClass("PrimitiveClass", out var cls));

        var basicNewMethod = cls!.MethodDictionary[new Symbol("basicNew")];
        Assert.True(basicNewMethod.HasPrimitive);
        Assert.Equal(70, basicNewMethod.PrimitiveIndex);

        var basicAtMethod = cls.MethodDictionary[new Symbol("basicAt:")];
        Assert.True(basicAtMethod.HasPrimitive);
        Assert.Equal(60, basicAtMethod.PrimitiveIndex);

        var basicAtPutMethod = cls.MethodDictionary[new Symbol("basicAt:put:")];
        Assert.True(basicAtPutMethod.HasPrimitive);
        Assert.Equal(61, basicAtPutMethod.PrimitiveIndex);

        var sizeMethod = cls.MethodDictionary[new Symbol("size")];
        Assert.True(sizeMethod.HasPrimitive);
        Assert.Equal(62, sizeMethod.PrimitiveIndex);
    }

    #endregion

    #region Test Data Providers

    public static IEnumerable<object[]> GetLevel1TestFiles()
        => GetTestFiles("Level1-BasicClasses");

    public static IEnumerable<object[]> GetLevel2TestFiles()
        => GetTestFiles("Level2-SimpleMethods");

    public static IEnumerable<object[]> GetLevel3TestFiles()
        => GetTestFiles("Level3-MethodsWithTemporaries");

    public static IEnumerable<object[]> GetLevel4TestFiles()
        => GetTestFiles("Level4-InstanceVariables");

    public static IEnumerable<object[]> GetLevel5TestFiles()
        => GetTestFiles("Level5-Primitives");

    public static IEnumerable<object[]> GetLevel6TestFiles()
        => GetTestFiles("Level6-ClassMethods");

    public static IEnumerable<object[]> GetSystematicFailCases()
        => GetFailCaseFiles("Systematic");

    private static IEnumerable<object[]> GetTestFiles(string levelDir)
    {
        var levelPath = Path.Combine(TestCasesRoot, levelDir);
        if (!Directory.Exists(levelPath))
            return Enumerable.Empty<object[]>();

        return Directory.GetFiles(levelPath, "*.st")
            .Select(f => new object[] { f });
    }

    private static IEnumerable<object[]> GetFailCaseFiles(string subDir)
    {
        var failPath = Path.Combine(FailCasesRoot, subDir);
        if (!Directory.Exists(failPath))
            return Enumerable.Empty<object[]>();

        return Directory.GetFiles(failPath, "*.st")
            .Select(f => new object[] { f });
    }

    #endregion

    #region Helper Methods

    private static void ValidatePositiveCase(string filePath)
    {
        var source = File.ReadAllText(filePath);

        // Should parse without throwing
        var fileNode = ParseFileIn(source);
        Assert.NotNull(fileNode);
        Assert.NotEmpty(fileNode.Chunks);

        // Should load into runtime without errors
        ObjectMemory.Reset();
        var vm = new VirtualMachine();
        vm.LoadSystemFile(filePath);
    }

    private static void ValidateNegativeCase(string filePath)
    {
        var source = File.ReadAllText(filePath);

        // Should fail to parse
        Assert.Throws<InvalidOperationException>(() => ParseFileIn(source));
    }

    private static FileNode ParseFileIn(string source)
    {
        var reader = new StringReader(source);
        var input = new Antlr4.Runtime.AntlrInputStream(reader);
        var lexer = new Smalltalk80.Generated.Smalltalk80Lexer(input);
        var tokens = new Antlr4.Runtime.CommonTokenStream(lexer);
        var parser = new Smalltalk80.Generated.Smalltalk80Parser(tokens);
        parser.RemoveErrorListeners();

        var fileCtx = parser.fileIn();
        if (parser.NumberOfSyntaxErrors > 0)
            throw new InvalidOperationException("Parse errors detected");

        var visitor = new ClassDefBuildingVisitor();
        return (FileNode)visitor.VisitFileIn(fileCtx)!;
    }

    private static string GetTestFile(string levelDir, string fileName)
    {
        return Path.Combine(TestCasesRoot, levelDir, fileName);
    }

    #endregion
}
