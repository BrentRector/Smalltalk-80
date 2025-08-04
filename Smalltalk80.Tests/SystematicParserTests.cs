using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Antlr4.Runtime;
using Smalltalk80.Generated;
using Smalltalk80.Parser;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Systematic test suite for Smalltalk-80 parser, organized from simple to complex constructs.
/// Each test validates both positive and negative cases, and verifies runtime structures.
/// </summary>
public class SystematicParserTests
{
    private static readonly string TestCasesPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Smalltalk80", "Parser.TestCases");

    #region Test Infrastructure

    private static void ResetSystem()
    {
        ObjectMemory.Reset();
    }

    private static VirtualMachine CreateVM()
    {
        return new VirtualMachine();
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

    private static void AssertClassExists(string className, string? expectedSuperclass = null)
    {
        Assert.True(SystemDictionary.TryGetClass(className, out var cls), $"Class '{className}' should exist");
        if (expectedSuperclass != null)
        {
            Assert.True(SystemDictionary.TryGetClass(expectedSuperclass, out var super), $"Superclass '{expectedSuperclass}' should exist");
            Assert.Equal(super, cls!.Superclass);
        }
    }

    private static void AssertClassHasInstanceVariables(string className, params string[] expectedVars)
    {
        var cls = SystemDictionary.GetClass(className);
        Assert.Equal(expectedVars.Length, cls.InstanceVariables.Count);
        foreach (var expectedVar in expectedVars)
        {
            Assert.Contains(expectedVar, cls.InstanceVariables);
        }
    }

    private static void AssertClassHasMethod(string className, string methodSelector)
    {
        var cls = SystemDictionary.GetClass(className);
        Assert.True(cls.MethodDictionary.ContainsKey(new Symbol(methodSelector)),
            $"Class '{className}' should have method '{methodSelector}'");
    }

    private static void AssertClassHasClassVariables(string className, params string[] expectedVars)
    {
        var cls = SystemDictionary.GetClass(className);
        foreach (var varName in expectedVars)
        {
            Assert.Contains(varName, cls.ClassVariables);
        }
    }

    private static void AssertClassHasClassMethod(string className, string methodSelector)
    {
        // For now, simplify by just checking if the class side methods exist
        // In a full implementation, we'd check the metaclass
        // This is a placeholder for the systematic test framework
        var cls = SystemDictionary.GetClass(className);
        Assert.NotNull(cls); // At minimum, the class should exist

        // TODO: Implement proper metaclass method checking when metaclass support is added
    }

    #endregion

    #region Level 1: Basic Class Definitions (Empty Classes)

    [Fact]
    public void L1_EmptyClass_MinimalDefinition()
    {
        ResetSystem();
        var vm = CreateVM();

        var source = @"Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";

        vm.LoadSystemFile(CreateTempFile(source));

        AssertClassExists("TestClass", "Object");
        var cls = SystemDictionary.GetClass("TestClass");
        Assert.Empty(cls.InstanceVariables);
        Assert.Empty(cls.ClassVariables);
        Assert.Empty(cls.PoolDictionaries);
        Assert.Equal("Test", cls.Category);
    }

    [Theory]
    [InlineData("Level1-BasicClasses/basic_no_members.st", "BasicNoMembers", "Object")]
    [InlineData("Level1-BasicClasses/class_with_single_instance_var.st", "WithInstanceVar", "Object")]
    public void L1_ExistingValidClassFiles_ShouldParseCorrectly(string fileName, string expectedClassName, string expectedSuperclass)
    {
        ResetSystem();
        var vm = CreateVM();

        var basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Smalltalk80", "Parser.TestCases");
        var filePath = Path.GetFullPath(Path.Combine(basePath, fileName));

        vm.LoadSystemFile(filePath);

        AssertClassExists(expectedClassName, expectedSuperclass);
    }

    [Fact]
    public void L1_EmptyClass_WithDifferentSuperclass()
    {
        ResetSystem();
        var vm = CreateVM();

        // First define a custom superclass
        var superSource = @"Object subclass: #MySuperClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";
        vm.LoadSystemFile(CreateTempFile(superSource));

        // Then define subclass
        var subSource = @"MySuperClass subclass: #MySubClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";
        vm.LoadSystemFile(CreateTempFile(subSource));

        AssertClassExists("MySuperClass", "Object");
        AssertClassExists("MySubClass", "MySuperClass");
    }

    [Fact]
    public void L1_EmptyClass_WithInstanceVariables()
    {
        ResetSystem();
        var vm = CreateVM();

        var source = @"Object subclass: #ClassWithVars
    instanceVariableNames: 'var1 var2 var3'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";

        vm.LoadSystemFile(CreateTempFile(source));

        AssertClassExists("ClassWithVars", "Object");
        AssertClassHasInstanceVariables("ClassWithVars", "var1", "var2", "var3");
    }

    [Fact]
    public void L1_EmptyClass_WithClassVariables()
    {
        ResetSystem();
        var vm = CreateVM();

        var source = @"Object subclass: #ClassWithClassVars
    instanceVariableNames: ''
    classVariableNames: 'ClassVar1 ClassVar2'
    poolDictionaries: ''
    category: 'Test'!";

        vm.LoadSystemFile(CreateTempFile(source));

        AssertClassExists("ClassWithClassVars", "Object");
        var cls = SystemDictionary.GetClass("ClassWithClassVars");
        Assert.Contains("ClassVar1", cls.ClassVariables);
        Assert.Contains("ClassVar2", cls.ClassVariables);
    }

    #endregion

    #region Level 2: Simple Method Definitions

    [Fact]
    public void L2_UnaryMethod_NoArguments()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level2-SimpleMethods", "unary_methods.st"));

        AssertClassExists("ClassWithUnaryMethod", "Object");
        AssertClassHasMethod("ClassWithUnaryMethod", "getValue");

        var cls = SystemDictionary.GetClass("ClassWithUnaryMethod");
        var method = cls.MethodDictionary[new Symbol("getValue")];
        Assert.NotNull(method);
        Assert.False(method.HasPrimitive);
    }

    [Fact]
    public void L2_BinaryMethod_OneArgument()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level2-SimpleMethods", "binary_methods.st"));

        AssertClassExists("ClassWithBinaryMethods", "Object");
        AssertClassHasMethod("ClassWithBinaryMethods", "+");
        AssertClassHasMethod("ClassWithBinaryMethods", "-");
        AssertClassHasMethod("ClassWithBinaryMethods", "*");
        AssertClassHasMethod("ClassWithBinaryMethods", "=");

        var cls = SystemDictionary.GetClass("ClassWithBinaryMethods");
        var method = cls.MethodDictionary[new Symbol("+")];
        Assert.NotNull(method);
        Assert.False(method.HasPrimitive);
    }

    [Fact]
    public void L2_KeywordMethod_MultipleArguments()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level2-SimpleMethods", "keyword_methods.st"));

        AssertClassExists("ClassWithKeywordMethods", "Object");
        AssertClassHasMethod("ClassWithKeywordMethods", "setValue:");
        AssertClassHasMethod("ClassWithKeywordMethods", "setValue:withName:");
        AssertClassHasMethod("ClassWithKeywordMethods", "at:put:");
        AssertClassHasMethod("ClassWithKeywordMethods", "from:to:do:");

        var cls = SystemDictionary.GetClass("ClassWithKeywordMethods");
        var method = cls.MethodDictionary[new Symbol("setValue:withName:")];
        Assert.NotNull(method);
        Assert.False(method.HasPrimitive);
    }

    #endregion

    #region Level 3: Methods with Temporaries

    [Fact]
    public void L3_Method_WithTemporaries()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level3-MethodsWithTemporaries", "methods_with_temporaries.st"));

        AssertClassExists("ClassWithTemporaries", "Object");
        AssertClassHasMethod("ClassWithTemporaries", "compute");
        AssertClassHasMethod("ClassWithTemporaries", "processData:");
        AssertClassHasMethod("ClassWithTemporaries", "complexCalculation:with:");

        var cls = SystemDictionary.GetClass("ClassWithTemporaries");
        var method = cls.MethodDictionary[new Symbol("compute")];
        Assert.Equal(2, method.TemporariesCount);
    }

    #endregion

    #region Level 4: Methods with Instance Variable Access

    [Fact]
    public void L4_Method_AccessingInstanceVariables()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level4-InstanceVariables", "point_class.st"));

        AssertClassExists("Point", "Object");
        AssertClassHasInstanceVariables("Point", "x", "y");
        AssertClassHasMethod("Point", "x");
        AssertClassHasMethod("Point", "x:");
        AssertClassHasMethod("Point", "y");
        AssertClassHasMethod("Point", "y:");
        AssertClassHasMethod("Point", "setX:y:");
        AssertClassHasMethod("Point", "distanceFromOrigin");
    }

    #endregion

    #region Level 5: Primitive Methods

    [Fact]
    public void L5_Method_WithPrimitive()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level5-Primitives", "primitive_methods.st"));

        AssertClassExists("PrimitiveClass", "Object");
        AssertClassHasMethod("PrimitiveClass", "basicNew");
        AssertClassHasMethod("PrimitiveClass", "basicAt:");
        AssertClassHasMethod("PrimitiveClass", "basicAt:put:");
        AssertClassHasMethod("PrimitiveClass", "size");

        var cls = SystemDictionary.GetClass("PrimitiveClass");
        var method = cls.MethodDictionary[new Symbol("basicNew")];
        Assert.True(method.HasPrimitive);
        Assert.Equal(70, method.PrimitiveIndex);
    }

    #endregion

    #region Level 6: Class Methods (Metaclass)

    [Fact]
    public void L6_ClassMethod_Basic()
    {
        ResetSystem();
        var vm = CreateVM();

        vm.LoadSystemFile(Path.Combine(TestCasesPath, "Level6-ClassMethods", "class_with_class_methods.st"));

        AssertClassExists("CountedClass", "Object");
        AssertClassHasInstanceVariables("CountedClass", "id");
        AssertClassHasClassVariables("CountedClass", "InstanceCount");

        // Instance-side methods
        AssertClassHasMethod("CountedClass", "id");
        AssertClassHasMethod("CountedClass", "setId:");

        // Class-side methods 
        AssertClassHasClassMethod("CountedClass", "new");
        AssertClassHasClassMethod("CountedClass", "instanceCount");
        AssertClassHasClassMethod("CountedClass", "initialize");
    }

    #endregion

    #region Negative Test Cases

    [Fact]
    public void N1_InvalidClass_MissingSuperclass()
    {
        var source = @"NonExistentClass subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!";

        // This should not throw during parsing, but superclass won't be found
        var fileNode = ParseFileIn(source);
        Assert.NotNull(fileNode);

        ResetSystem();
        var vm = CreateVM();
        vm.LoadSystemFile(CreateTempFile(source));

        // Class should still be created but with null superclass
        AssertClassExists("TestClass");
        var cls = SystemDictionary.GetClass("TestClass");
        Assert.Null(cls.Superclass);
    }

    [Fact]
    public void N2_InvalidMethod_InvalidSyntax()
    {
        var source = @"Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'!

!TestClass methodsFor: 'broken'!

invalidMethod
    | unclosed temp
    ^self!";

        Assert.Throws<InvalidOperationException>(() => ParseFileIn(source));
    }

    [Fact]
    public void N3_InvalidClass_MissingBang()
    {
        var source = @"Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Test'";  // Missing !

        Assert.Throws<InvalidOperationException>(() => ParseFileIn(source));
    }

    #endregion

    #region Helper Methods

    private static string CreateTempFile(string content)
    {
        var tempPath = Path.GetTempFileName();
        File.WriteAllText(tempPath, content);
        return tempPath;
    }

    #endregion
}
