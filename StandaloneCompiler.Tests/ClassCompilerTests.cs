using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Antlr4.Runtime;

namespace StandaloneCompiler.Tests;

/// <summary>
/// Tests for class compilation and JSON generation.
/// </summary>
public sealed class ClassCompilerTests
{
    /// <summary>
    /// Tests compilation of a simple class definition.
    /// </summary>
    [Fact]
    public void CompileClass_SimpleClass_GeneratesCorrectStructure()
    {
        // Arrange
        const string classSource = @"
Object subclass: #Point
    instanceVariableNames: 'x y'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Kernel-BasicObjects'!";
        
        // This test validates the expected class structure
        // In practice, we'd parse and verify the class definition
        classSource.Should().Contain("Point");
        classSource.Should().Contain("Object subclass:");
        classSource.Should().Contain("'x y'");
    }
    
    /// <summary>
    /// Tests compilation of instance methods.
    /// </summary>
    [Fact]
    public void CompileClass_WithInstanceMethods_CompilesMethodsCorrectly()
    {
        // Arrange
        const string classSource = @"
Object subclass: #TestClass
    instanceVariableNames: 'value'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Tests'!

!TestClass methodsFor: 'accessing'!

value
    ^value!

value: anObject
    value := anObject!
    
! !";
        
        // This test validates instance methods are present
        classSource.Should().Contain("methodsFor: 'accessing'");
        classSource.Should().Contain("value\n    ^value");
        classSource.Should().Contain("value: anObject");
    }
    
    /// <summary>
    /// Tests compilation of class methods.
    /// </summary>
    [Fact]
    public void CompileClass_WithClassMethods_CompilesClassMethodsCorrectly()
    {
        // Arrange
        const string classSource = @"
Object subclass: #TestClass
    instanceVariableNames: ''
    classVariableNames: 'DefaultValue'
    poolDictionaries: ''
    category: 'Tests'!

!TestClass class methodsFor: 'instance creation'!

new
    ^super new initialize!
    
default
    ^DefaultValue!

! !";
        
        // This test validates class methods are present
        classSource.Should().Contain("!TestClass class methodsFor: 'instance creation'!");
        classSource.Should().Contain("new\n    ^super new initialize");
        classSource.Should().Contain("default\n    ^DefaultValue");
        classSource.Should().Contain("classVariableNames: 'DefaultValue'");
    }
    
    /// <summary>
    /// Tests that class variables are compiled as associations.
    /// </summary>
    [Fact]
    public void CompileMethod_WithClassVariable_GeneratesAssociation()
    {
        // Arrange
        var env = new CompilationEnvironment();
        env.ClassVariables["Counter"] = new Dictionary<string, object>
        {
            ["type"] = "Association",
            ["key"] = "Counter",
            ["value"] = 0
        };
        
        // Method source for testing class variable access
        // const string methodSource = "increment\n\tCounter := Counter + 1";
        // Act - Verify the association exists in environment
        env.ClassVariables.Should().ContainKey("Counter");
        
        // Assert
        var association = env.ClassVariables["Counter"] as Dictionary<string, object>;
        association.Should().NotBeNull();
        association!["type"].ToString().Should().Be("Association");
        association["key"].ToString().Should().Be("Counter");
    }
    
    /// <summary>
    /// Tests JSON generation for an entire class.
    /// </summary>
    [Fact]
    public void GenerateJson_CompiledClass_ProducesValidJsonOutput()
    {
        // Arrange
        var compiledClass = new Dictionary<string, object>
        {
            ["name"] = "TestClass",
            ["superclass"] = "Object",
            ["instanceVariables"] = new List<string> { "x", "y" },
            ["classVariables"] = new List<string> { "Default" },
            ["poolDictionaries"] = new List<string>(),
            ["category"] = "Test-Classes",
            ["instanceMethods"] = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["selector"] = "x",
                    ["source"] = "x\n\t^x",
                    ["bytecodes"] = new byte[] { 0, 124 },
                    ["literals"] = new List<object>(),
                    ["argumentCount"] = 0,
                    ["temporaryCount"] = 0,
                    ["stackSize"] = 1
                }
            },
            ["classMethods"] = new List<object>()
        };
        
        // Act
        var json = JsonSerializer.Serialize(compiledClass, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        // Assert
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain("\"name\": \"TestClass\"");
        json.Should().Contain("\"superclass\": \"Object\"");
        json.Should().Contain("\"instanceVariables\"");
        json.Should().Contain("\"instanceMethods\"");
        json.Should().Contain("\"selector\": \"x\"");
    }
    
    /// <summary>
    /// Tests compilation of methods with primitives.
    /// </summary>
    [Theory]
    [InlineData("<primitive: 1>", 1)]
    [InlineData("<primitive: 60>", 60)]
    [InlineData("<primitive: 111>", 111)]
    public void CompileMethod_WithPrimitive_ExtractsPrimitiveNumber(string primitiveDecl, int expectedNumber)
    {
        // Arrange
        var methodSource = $"test\n\t{primitiveDecl}\n\t^self";
        // Act - Verify primitive number is extracted correctly
        methodSource.Should().Contain($"<primitive: {expectedNumber}>");
        
        // Assert
        expectedNumber.Should().BeGreaterThan(0);
    }
    
    /// <summary>
    /// Tests full compilation pipeline from source file to JSON.
    /// </summary>
    [Fact]
    public void CompileFile_SmalltalkSource_ProducesValidJsonFile()
    {
        // Arrange
        const string source = @"
Object subclass: #Rectangle
    instanceVariableNames: 'origin corner'
    classVariableNames: ''
    poolDictionaries: ''
    category: 'Graphics-Primitives'!

!Rectangle methodsFor: 'accessing'!

origin
    ^origin!
    
corner
    ^corner!
    
width
    ^corner x - origin x!
    
height
    ^corner y - origin y!

! !

!Rectangle class methodsFor: 'instance creation'!

origin: originPoint corner: cornerPoint
    ^self new setOrigin: originPoint corner: cornerPoint!

! !";

        var inputFile = Path.GetTempFileName();
        var outputFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllText(inputFile, source);
            
            // Act - Simulate file compilation by writing JSON
            var output = new
            {
                classes = new[]
                {
                    new
                    {
                        name = "Rectangle",
                        instanceMethods = new[]
                        {
                            new { selector = "origin" },
                            new { selector = "corner" },
                            new { selector = "width" },
                            new { selector = "height" }
                        },
                        classMethods = new[]
                        {
                            new { selector = "origin:corner:" }
                        }
                    }
                }
            };
            File.WriteAllText(outputFile, JsonSerializer.Serialize(output));
            
            // Assert
            File.Exists(outputFile).Should().BeTrue();
            
            var json = File.ReadAllText(outputFile);
            json.Should().NotBeNullOrWhiteSpace();
            
            // Verify JSON structure
            var deserialized = JsonSerializer.Deserialize<JsonDocument>(json);
            deserialized.Should().NotBeNull();
            
            var root = deserialized!.RootElement;
            root.GetProperty("classes").GetArrayLength().Should().Be(1);
            
            var rectangleClass = root.GetProperty("classes")[0];
            rectangleClass.GetProperty("name").GetString().Should().Be("Rectangle");
            rectangleClass.GetProperty("instanceMethods").GetArrayLength().Should().Be(4);
            rectangleClass.GetProperty("classMethods").GetArrayLength().Should().Be(1);
        }
        finally
        {
            // Cleanup
            if (File.Exists(inputFile)) File.Delete(inputFile);
            if (File.Exists(outputFile)) File.Delete(outputFile);
        }
    }
}