using System.IO;
using System.Text.Json;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for the bootstrap loading process from JSON.
/// </summary>
public sealed class BootstrapTests
{
    /// <summary>
    /// Creates a minimal bootstrap JSON for testing.
    /// </summary>
    private static string CreateMinimalBootstrapJson()
    {
        var bootstrap = new
        {
            phases = new[]
            {
                new
                {
                    phase = 1,
                    description = "Core classes",
                    classes = new[]
                    {
                        new
                        {
                            name = "ProtoObject",
                            superclass = "nil",
                            instanceVariables = Array.Empty<string>(),
                            classVariables = Array.Empty<string>(),
                            poolDictionaries = Array.Empty<string>(),
                            instanceSide = new
                            {
                                methods = new[]
                                {
                                    new
                                    {
                                        selector = "class",
                                        source = "class\n\t<primitive: 111>",
                                        bytecodes = new[] { 112, 207 },
                                        literals = new object[] { 111 }
                                    }
                                }
                            },
                            classSide = new
                            {
                                methods = Array.Empty<object>()
                            }
                        },
                        new
                        {
                            name = "Object",
                            superclass = "ProtoObject",
                            instanceVariables = Array.Empty<string>(),
                            classVariables = Array.Empty<string>(),
                            poolDictionaries = Array.Empty<string>(),
                            instanceSide = new
                            {
                                methods = new[]
                                {
                                    new
                                    {
                                        selector = "isNil",
                                        source = "isNil\n\t^false",
                                        bytecodes = new[] { 122, 124 },
                                        literals = Array.Empty<object>()
                                    }
                                }
                            },
                            classSide = new
                            {
                                methods = Array.Empty<object>()
                            }
                        }
                    }
                }
            }
        };
        
        return JsonSerializer.Serialize(bootstrap, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
    
    /// <summary>
    /// Tests that bootstrap can load from a minimal JSON configuration.
    /// </summary>
    [Fact]
    public void LoadFromJson_MinimalConfiguration_LoadsSuccessfully()
    {
        // Arrange
        var vm = new VirtualMachine();
        var json = CreateMinimalBootstrapJson();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllText(tempFile, json);
            
            // Act
            vm.LoadBootstrapFromJson(tempFile);
            
            // Assert
            vm.IsBootstrapped.Should().BeTrue();
            
            // Verify core classes exist
            var protoObjectClass = vm.GetClassByName("ProtoObject");
            protoObjectClass.Should().NotBe(OOP.Nil);
            
            var objectClass = vm.GetClassByName("Object");
            objectClass.Should().NotBe(OOP.Nil);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    /// <summary>
    /// Tests that initialization expressions are executed during bootstrap.
    /// </summary>
    [Fact]
    public void LoadFromJson_WithInitExpressions_ExecutesExpressions()
    {
        // Arrange
        var vm = new VirtualMachine();
        var bootstrap = new
        {
            phases = new[]
            {
                new
                {
                    phase = 1,
                    description = "Core classes",
                    classes = Array.Empty<object>()
                }
            },
            initExpressions = new[]
            {
                new
                {
                    description = "Test initialization",
                    bytecodes = new[] { 123, 124 }, // pushTrue, returnTop
                    literals = Array.Empty<object>()
                }
            }
        };
        
        var json = JsonSerializer.Serialize(bootstrap, new JsonSerializerOptions 
        { 
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllText(tempFile, json);
            
            // Act & Assert - Should not throw
            var exception = Record.Exception(() => vm.LoadBootstrapFromJson(tempFile));
            exception.Should().BeNull();
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
    
    /// <summary>
    /// Tests that special objects are properly set up after bootstrap.
    /// </summary>
    [Fact]
    public void LoadFromJson_SpecialObjects_AreSetupCorrectly()
    {
        // Arrange
        var vm = new VirtualMachine();
        var json = CreateMinimalBootstrapJson();
        var tempFile = Path.GetTempFileName();
        
        try
        {
            File.WriteAllText(tempFile, json);
            
            // Act
            vm.LoadBootstrapFromJson(tempFile);
            
            // Assert - Check critical special objects
            vm.GetSpecialObject(SpecialObjects.TrueObject).Should().NotBe(OOP.Nil);
            vm.GetSpecialObject(SpecialObjects.FalseObject).Should().NotBe(OOP.Nil);
            vm.GetSpecialObject(SpecialObjects.ProcessorScheduler).Should().NotBe(OOP.Nil);
            vm.GetSpecialObject(SpecialObjects.SystemDictionaryInstance).Should().NotBe(OOP.Nil);
            vm.GetSpecialObject(SpecialObjects.SymbolTable).Should().NotBe(OOP.Nil);
        }
        finally
        {
            // Cleanup
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }
}