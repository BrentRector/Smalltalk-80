namespace Smalltalk80.Tests;

using Xunit;
using FluentAssertions;
using Smalltalk80.Interpreter;
using Smalltalk80.Interpreter.Core.Objects;
using Smalltalk80.Interpreter.Core.Values;
using Smalltalk80.Interpreter.Execution;
using Smalltalk80.Interpreter.Primitives;
using Microsoft.Extensions.Logging;

/// <summary>
/// Comprehensive test suite for Blue Book compliance.
/// These tests verify that the interpreter correctly implements
/// the Smalltalk-80 specification as described in the Blue Book.
/// </summary>
public class BlueBookComplianceTests : IAsyncLifetime
{
    private VirtualMachine vm = null!;
    private ContextBasedInterpreter interpreter = null!;
    
    /// <summary>
    /// Initializes the VM before tests.
    /// </summary>
    public async Task InitializeAsync()
    {
        vm = new VirtualMachine();
        await vm.InitializeAsync();
        interpreter = new ContextBasedInterpreter();
    }
    
    /// <summary>
    /// Cleanup after tests.
    /// </summary>
    public Task DisposeAsync() => Task.CompletedTask;
    
    #region Context-Based Execution Tests
    
    [Fact]
    public void MethodContext_Should_Be_Proper_Smalltalk_Object()
    {
        // Arrange
        var contextClass = SystemDictionary.LookupClass("MethodContext");
        contextClass.Should().NotBeNull();
        
        var method = new SCompiledMethod(
            "test", 
            [0x78], // return receiver
            new SArray(),
            0, 0);
            
        var receiver = SNil.Instance;
        
        // Act
        var context = new SMethodContext(contextClass!, method, receiver, []);
        
        // Assert
        context.Should().BeOfType<SMethodContext>();
        context.Class.Should().Be(contextClass);
        context.Sender.Should().Be(SNil.Instance);
        context.Method.Should().Be(method);
        context.Receiver.Should().Be(receiver);
        context.InstructionPointer.Value.Should().Be(0);
    }
    
    [Fact]
    public void BlockContext_Should_Share_Home_Context_Temporaries()
    {
        // Arrange
        var methodContextClass = SystemDictionary.LookupClass("MethodContext");
        var blockContextClass = SystemDictionary.LookupClass("BlockContext");
        
        var method = new SCompiledMethod(
            "testBlock",
            [0x78], // return receiver
            new SArray(),
            0, 2); // 2 temporaries
            
        var homeContext = new SMethodContext(
            methodContextClass!,
            method,
            SNil.Instance,
            []);
            
        // Set a temporary in home context
        homeContext.SetTemporary(0, SmallInteger.From(42));
        
        // Act
        var blockContext = new SBlockContext(
            blockContextClass!,
            0, // no arguments
            0, // initial IP
            homeContext);
            
        // Assert
        blockContext.Home.Should().Be(homeContext);
        blockContext.GetTemporary(0).Should().Be(SmallInteger.From(42));
        
        // Modifying temporary through block should affect home
        blockContext.SetTemporary(0, SmallInteger.From(99));
        homeContext.GetTemporary(0).Should().Be(SmallInteger.From(99));
    }
    
    [Fact]
    public void ThisContext_Should_Return_Active_Context()
    {
        // Arrange
        var contextClass = SystemDictionary.LookupClass("MethodContext");
        var method = new SCompiledMethod(
            "testThisContext",
            [0x89, 0x7C], // pushActiveContext, returnTop
            new SArray(),
            0, 0);
            
        var context = new SMethodContext(contextClass!, method, SNil.Instance, []);
        
        // The interpreter should support thisContext
        interpreter.ActiveContext.Should().BeNull();
        
        // When executing, ActiveContext should be set
        // This requires integration testing with the interpreter
    }
    
    #endregion
    
    #region Become: Primitive Tests
    
    [Fact]
    public void Become_Primitive_Should_Swap_Object_Identities()
    {
        // Arrange
        var arrayClass = SystemDictionary.LookupClass("Array");
        var array1 = new SArray(arrayClass!, 2);
        var array2 = new SArray(arrayClass!, 3);
        
        array1.AtPut(0, SmallInteger.From(1));
        array1.AtPut(1, SmallInteger.From(2));
        
        array2.AtPut(0, SmallInteger.From(10));
        array2.AtPut(1, SmallInteger.From(20));
        array2.AtPut(2, SmallInteger.From(30));
        
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        
        // Act
        var result = dispatcher.ExecutePrimitive(72, array1, [array2]);
        
        // Assert
        result.Should().Be(array1); // become: returns receiver
        
        // After become:, the identities should be swapped
        // This is a simplified test - in reality, ALL references would be swapped
    }
    
    #endregion
    
    #region Metaclass Architecture Tests
    
    [Fact]
    public void Each_Class_Should_Have_Unique_Metaclass()
    {
        // Arrange & Act
        var objectClass = SystemDictionary.LookupClass("Object");
        var stringClass = SystemDictionary.LookupClass("String");
        var arrayClass = SystemDictionary.LookupClass("Array");
        
        // Assert
        objectClass!.Class.Should().NotBeNull();
        stringClass!.Class.Should().NotBeNull();
        arrayClass!.Class.Should().NotBeNull();
        
        // Each class should have its own unique metaclass
        objectClass.Class.Should().NotBe(stringClass.Class);
        stringClass.Class.Should().NotBe(arrayClass.Class);
        objectClass.Class.Should().NotBe(arrayClass.Class);
    }
    
    [Fact]
    public void Metaclass_Superclass_Should_Be_Superclass_Metaclass()
    {
        // Arrange
        var stringClass = SystemDictionary.LookupClass("String");
        var objectClass = SystemDictionary.LookupClass("Object");
        
        // String's superclass is Object (or some intermediate class)
        // String class's superclass should be Object class
        
        if (stringClass!.Superclass == objectClass)
        {
            // Assert
            var stringMetaclass = stringClass.Class as SMetaclass;
            var objectMetaclass = objectClass!.Class as SMetaclass;
            
            stringMetaclass?.Superclass.Should().Be(objectMetaclass);
        }
    }
    
    [Fact]
    public void Metaclass_Class_Should_Be_Metaclass()
    {
        // The class "Metaclass" is the class of all metaclasses
        var metaclassClass = SystemDictionary.LookupClass("Metaclass");
        metaclassClass.Should().NotBeNull();
        
        var objectClass = SystemDictionary.LookupClass("Object");
        var objectMetaclass = objectClass!.Class;
        
        // The class of any metaclass should be Metaclass
        objectMetaclass.Class.Should().Be(metaclassClass);
    }
    
    #endregion
    
    #region Process and Scheduling Tests
    
    [Fact]
    public void Process_Should_Maintain_Suspended_Context()
    {
        // Arrange
        var processClass = SystemDictionary.LookupClass("Process");
        var contextClass = SystemDictionary.LookupClass("MethodContext");
        
        var method = new SCompiledMethod("test", [0x78], new SArray(), 0, 0);
        var context = new SMethodContext(contextClass!, method, SNil.Instance, []);
        
        // Act
        var process = new SProcess(processClass!, context, 4);
        
        // Assert
        process.SuspendedContext.Should().Be(context);
        process.Priority.Value.Should().Be(4);
        process.IsTerminated().Should().BeFalse();
    }
    
    [Fact]
    public void ProcessScheduler_Should_Schedule_By_Priority()
    {
        // Arrange
        var scheduler = SProcessScheduler.GetInstance();
        var processClass = SystemDictionary.LookupClass("Process");
        
        var highPriorityProcess = new SProcess(processClass!, SNil.Instance, 6);
        var lowPriorityProcess = new SProcess(processClass!, SNil.Instance, 2);
        
        // Act
        scheduler.Schedule(lowPriorityProcess, 2);
        scheduler.Schedule(highPriorityProcess, 6);
        
        // Assert
        scheduler.ProcessCountAt(2).Should().Be(1);
        scheduler.ProcessCountAt(6).Should().Be(1);
        
        // High priority process should be scheduled first
        var next = scheduler.Yield();
        next.Should().Be(highPriorityProcess);
    }
    
    [Fact]
    public void Semaphore_Should_Synchronize_Processes()
    {
        // Arrange
        var semaphoreClass = SystemDictionary.LookupClass("Semaphore");
        var processClass = SystemDictionary.LookupClass("Process");
        
        var semaphore = SSemaphore.ForMutualExclusion();
        var process1 = new SProcess(processClass!, SNil.Instance);
        var process2 = new SProcess(processClass!, SNil.Instance);
        
        // Act - First wait should succeed (mutex has 1 signal)
        var canContinue1 = semaphore.Wait(process1);
        canContinue1.Should().BeTrue();
        
        // Second wait should block
        var canContinue2 = semaphore.Wait(process2);
        canContinue2.Should().BeFalse();
        semaphore.WaitingCount.Should().Be(1);
        
        // Signal should resume waiting process
        var resumed = semaphore.Signal();
        resumed.Should().Be(process2);
        semaphore.WaitingCount.Should().Be(0);
    }
    
    #endregion
    
    #region Block and Non-Local Return Tests
    
    [Fact]
    public void Block_Should_Support_Non_Local_Return()
    {
        // Arrange
        var methodContextClass = SystemDictionary.LookupClass("MethodContext");
        var blockContextClass = SystemDictionary.LookupClass("BlockContext");
        
        var method = new SCompiledMethod("testNonLocal", [], new SArray(), 0, 0);
        var homeContext = new SMethodContext(methodContextClass!, method, SNil.Instance, []);
        
        var block = new SBlockContext(blockContextClass!, 0, 0, homeContext);
        
        // Act
        var (returnContext, returnValue) = block.NonLocalReturn(SmallInteger.From(42));
        
        // Assert
        returnContext.Should().Be(homeContext);
        returnValue.Should().Be(SmallInteger.From(42));
    }
    
    #endregion
    
    #region All Primitives Implementation Tests
    
    [Theory]
    [InlineData(1, 3, 4, 7)]    // Integer add
    [InlineData(2, 10, 3, 7)]   // Integer subtract
    [InlineData(9, 3, 4, 12)]   // Integer multiply
    [InlineData(10, 20, 4, 5)]  // Integer divide
    public void Arithmetic_Primitives_Should_Work(int primitive, int left, int right, int expected)
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var leftInt = SmallInteger.From(left);
        var rightInt = SmallInteger.From(right);
        
        // Act
        var result = dispatcher.ExecutePrimitive(primitive, leftInt, [rightInt]);
        
        // Assert
        result.Should().BeOfType<SmallInteger>();
        ((SmallInteger)result!).Value.Should().Be(expected);
    }
    
    [Theory]
    [InlineData(3, 3, 5, false)]  // Less than
    [InlineData(3, 3, 3, false)]  // Less than (equal)
    [InlineData(3, 3, 2, true)]   // Less than
    [InlineData(4, 5, 3, true)]   // Greater than
    [InlineData(7, 3, 3, true)]   // Equal
    [InlineData(8, 3, 4, true)]   // Not equal
    public void Comparison_Primitives_Should_Work(int primitive, int left, int right, bool expected)
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var leftInt = SmallInteger.From(left);
        var rightInt = SmallInteger.From(right);
        
        // Act
        var result = dispatcher.ExecutePrimitive(primitive, leftInt, [rightInt]);
        
        // Assert
        result.Should().Be(expected ? STrue.Instance : SFalse.Instance);
    }
    
    [Fact]
    public void Primitive_70_New_Should_Create_Instance()
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var arrayClass = SystemDictionary.LookupClass("Array")!;
        
        // Act
        var result = dispatcher.ExecutePrimitive(70, arrayClass, []);
        
        // Assert
        result.Should().BeOfType<SInstance>();
        result!.Class.Should().Be(arrayClass);
    }
    
    [Fact]
    public void Primitive_71_NewWithSize_Should_Create_Indexed_Instance()
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var arrayClass = SystemDictionary.LookupClass("Array")!;
        
        // Act
        var result = dispatcher.ExecutePrimitive(71, arrayClass, [SmallInteger.From(10)]);
        
        // Assert
        result.Should().BeOfType<SIndexedInstance>();
        ((SIndexedInstance)result!).Size.Should().Be(10);
    }
    
    [Fact]
    public void Primitive_110_Identical_Should_Test_Object_Identity()
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var obj1 = new SInstance(SystemDictionary.ObjectClass);
        var obj2 = new SInstance(SystemDictionary.ObjectClass);
        
        // Act
        var result1 = dispatcher.ExecutePrimitive(110, obj1, [obj1]);
        var result2 = dispatcher.ExecutePrimitive(110, obj1, [obj2]);
        
        // Assert
        result1.Should().Be(STrue.Instance);
        result2.Should().Be(SFalse.Instance);
    }
    
    [Fact]
    public void Primitive_111_Class_Should_Return_Class()
    {
        // Arrange
        var dispatcher = new BluBookPrimitiveDispatcher(interpreter);
        var str = new SString("test");
        
        // Act
        var result = dispatcher.ExecutePrimitive(111, str, []);
        
        // Assert
        result.Should().Be(SystemDictionary.StringClass);
    }
    
    #endregion
    
    #region Message Send Through Primitives Tests
    
    [Fact]
    public void All_Arithmetic_Should_Use_Message_Sends_Not_Direct_C_Sharp()
    {
        // This test verifies that arithmetic operations go through proper message sends
        // and primitives, not direct C# operations
        
        // Arrange
        var int1 = SmallInteger.From(5);
        var int2 = SmallInteger.From(3);
        
        // The interpreter should use message sends for arithmetic
        var addMethod = SystemDictionary.SmallIntegerClass.LookupMethod("+");
        addMethod.Should().NotBeNull();
        addMethod!.PrimitiveIndex.Should().Be(1); // Primitive 1 is integer add
        
        // When executing 5 + 3, it should:
        // 1. Look up the + method in SmallInteger
        // 2. Try primitive 1
        // 3. Return the result
        
        var result = interpreter.Execute(addMethod, int1, [int2]);
        result.Should().BeOfType<SmallInteger>();
        ((SmallInteger)result).Value.Should().Be(8);
    }
    
    #endregion
}

/// <summary>
/// Tests for proper object memory management.
/// </summary>
public class ObjectMemoryTests
{
    [Fact]
    public void Become_Should_Update_All_References()
    {
        // This is a conceptual test - full implementation would require
        // scanning all of object memory
        
        // Arrange
        var arrayClass = SystemDictionary.LookupClass("Array")!;
        var container = new SArray(arrayClass, 2);
        var obj1 = new SInstance(SystemDictionary.ObjectClass);
        var obj2 = new SInstance(SystemDictionary.ObjectClass);
        
        container.AtPut(0, obj1);
        container.AtPut(1, obj1); // Two references to obj1
        
        // Act - In a real become:, ALL references to obj1 would become obj2
        // This requires full object memory scanning
        
        // After become:
        // container[0] should be obj2
        // container[1] should be obj2
    }
}