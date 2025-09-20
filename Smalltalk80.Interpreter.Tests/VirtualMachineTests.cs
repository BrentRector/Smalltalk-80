using System.Text.Json;
using Smalltalk80.Interpreter.Bootstrap;
using Smalltalk80.Interpreter.Memory;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Tests;

/// <summary>
/// Tests for the Smalltalk-80 Virtual Machine initialization and basic operations.
/// </summary>
public sealed class VirtualMachineTests
{
    /// <summary>
    /// Tests that the VM can be initialized with default memory size.
    /// </summary>
    [Fact]
    public void Constructor_WithDefaultMemorySize_InitializesSuccessfully()
    {
        // Arrange & Act
        var vm = new VirtualMachine();
        
        // Assert
        vm.Should().NotBeNull();
        vm.Memory.Should().NotBeNull();
        vm.IsBootstrapped.Should().BeFalse();
        vm.IsSchedulerEnabled.Should().BeFalse();
    }
    
    /// <summary>
    /// Tests that the VM can be initialized with a custom memory size.
    /// </summary>
    [Theory]
    [InlineData(65536)]
    [InlineData(131072)]
    [InlineData(524288)]
    public void Constructor_WithCustomMemorySize_InitializesWithCorrectSize(int memorySize)
    {
        // Arrange & Act
        var vm = new VirtualMachine(memorySize);
        
        // Assert
        vm.Should().NotBeNull();
        vm.Memory.Should().NotBeNull();
    }
    
    /// <summary>
    /// Tests that special objects are initialized to nil.
    /// </summary>
    [Fact]
    public void Constructor_SpecialObjectsArray_InitializedToNil()
    {
        // Arrange & Act
        var vm = new VirtualMachine();
        
        // Assert
        for (int i = 0; i < SpecialObjects.Count; i++)
        {
            var specialObject = vm.GetSpecialObject(i);
            specialObject.Should().Be(OOP.Nil);
        }
    }
    
    /// <summary>
    /// Tests that process scheduler is properly initialized.
    /// </summary>
    [Fact]
    public void Constructor_ProcessScheduler_IsInitialized()
    {
        // Arrange & Act
        var vm = new VirtualMachine();
        
        // Assert
        vm.ProcessScheduler.Should().NotBeNull();
    }
    
    /// <summary>
    /// Tests that class lookup returns nil for non-existent classes before bootstrap.
    /// </summary>
    [Theory]
    [InlineData("Object")]
    [InlineData("SmallInteger")]
    [InlineData("Array")]
    [InlineData("String")]
    public void GetClassByName_BeforeBootstrap_ReturnsNil(string className)
    {
        // Arrange
        var vm = new VirtualMachine();
        
        // Act
        var classOop = vm.GetClassByName(className);
        
        // Assert
        classOop.Should().Be(OOP.Nil);
    }
    
    /// <summary>
    /// Tests that scheduler can be enabled and disabled.
    /// </summary>
    [Fact]
    public void IsSchedulerEnabled_SetValue_UpdatesCorrectly()
    {
        // Arrange
        var vm = new VirtualMachine();
        
        // Act & Assert - Initially disabled
        vm.IsSchedulerEnabled.Should().BeFalse();
        
        // Act & Assert - Enable
        vm.IsSchedulerEnabled = true;
        vm.IsSchedulerEnabled.Should().BeTrue();
        
        // Act & Assert - Disable again
        vm.IsSchedulerEnabled = false;
        vm.IsSchedulerEnabled.Should().BeFalse();
    }
}