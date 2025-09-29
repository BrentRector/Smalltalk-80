using Smalltalk80.Interpreter.Core;
using Xunit;

namespace Smalltalk80.Tests;

/// <summary>
/// Unit tests for the SLinkedList class following the Blue Book specification.
/// </summary>
public class TestLinkedList
{
    private readonly SClass _linkedListClass;
    private readonly SClass _processClass;
    private readonly SClass _methodContextClass;

    public TestLinkedList()
    {
        // Create mock classes for testing
        _linkedListClass = new SClass { Name = "LinkedList" };
        _processClass = new SClass { Name = "Process" };
        _methodContextClass = new SClass { Name = "MethodContext" };
    }

    private SProcess CreateProcess()
    {
        var context = new SMethodContext(_methodContextClass);
        return new SProcess(_processClass, context, SProcess.UserSchedulingPriority);
    }

    [Fact]
    public void LinkedList_InitialState_IsEmpty()
    {
        // Arrange & Act
        var list = new SLinkedList(_linkedListClass);

        // Assert
        Assert.True(list.IsEmpty);
        Assert.Equal(SNil.Instance, list.FirstLink);
        Assert.Equal(SNil.Instance, list.LastLink);
        Assert.Equal(0, list.Size());
    }

    [Fact]
    public void LinkedList_AddLast_AddsFirstElement()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process = CreateProcess();

        // Act
        list.AddLast(process);

        // Assert
        Assert.False(list.IsEmpty);
        Assert.Equal(process, list.FirstLink);
        Assert.Equal(process, list.LastLink);
        Assert.Equal(1, list.Size());
        Assert.Equal(list, process.MyList);
        Assert.Equal(SNil.Instance, process.NextLink);
    }

    [Fact]
    public void LinkedList_AddLast_AddsMultipleElements()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var process3 = CreateProcess();

        // Act
        list.AddLast(process1);
        list.AddLast(process2);
        list.AddLast(process3);

        // Assert
        Assert.Equal(3, list.Size());
        Assert.Equal(process1, list.FirstLink);
        Assert.Equal(process3, list.LastLink);
        
        // Check links
        Assert.Equal(process2, process1.NextLink);
        Assert.Equal(process3, process2.NextLink);
        Assert.Equal(SNil.Instance, process3.NextLink);
        
        // Check list references
        Assert.Equal(list, process1.MyList);
        Assert.Equal(list, process2.MyList);
        Assert.Equal(list, process3.MyList);
    }

    [Fact]
    public void LinkedList_RemoveFirst_RemovesOnlyElement()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process = CreateProcess();
        list.AddLast(process);

        // Act
        var removed = list.RemoveFirst();

        // Assert
        Assert.Equal(process, removed);
        Assert.True(list.IsEmpty);
        Assert.Equal(SNil.Instance, list.FirstLink);
        Assert.Equal(SNil.Instance, list.LastLink);
        Assert.Equal(SNil.Instance, process.MyList);
        Assert.Equal(SNil.Instance, process.NextLink);
    }

    [Fact]
    public void LinkedList_RemoveFirst_RemovesFromMultiple()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var process3 = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);
        list.AddLast(process3);

        // Act
        var removed = list.RemoveFirst();

        // Assert
        Assert.Equal(process1, removed);
        Assert.Equal(2, list.Size());
        Assert.Equal(process2, list.FirstLink);
        Assert.Equal(process3, list.LastLink);
        Assert.Equal(SNil.Instance, process1.MyList);
        Assert.Equal(SNil.Instance, process1.NextLink);
    }

    [Fact]
    public void LinkedList_RemoveFirst_FromEmpty_ReturnsNil()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);

        // Act
        var removed = list.RemoveFirst();

        // Assert
        Assert.Equal(SNil.Instance, removed);
        Assert.True(list.IsEmpty);
    }

    [Fact]
    public void LinkedList_Remove_RemovesSpecificProcess()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var process3 = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);
        list.AddLast(process3);

        // Act - Remove middle element
        var removed = list.Remove(process2);

        // Assert
        Assert.Equal(process2, removed);
        Assert.Equal(2, list.Size());
        Assert.Equal(process1, list.FirstLink);
        Assert.Equal(process3, list.LastLink);
        Assert.Equal(process3, process1.NextLink);
        Assert.Equal(SNil.Instance, process2.MyList);
        Assert.Equal(SNil.Instance, process2.NextLink);
    }

    [Fact]
    public void LinkedList_Remove_RemovesFirstElement()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);

        // Act
        var removed = list.Remove(process1);

        // Assert
        Assert.Equal(process1, removed);
        Assert.Equal(1, list.Size());
        Assert.Equal(process2, list.FirstLink);
        Assert.Equal(process2, list.LastLink);
    }

    [Fact]
    public void LinkedList_Remove_RemovesLastElement()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var process3 = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);
        list.AddLast(process3);

        // Act
        var removed = list.Remove(process3);

        // Assert
        Assert.Equal(process3, removed);
        Assert.Equal(2, list.Size());
        Assert.Equal(process1, list.FirstLink);
        Assert.Equal(process2, list.LastLink);
        Assert.Equal(SNil.Instance, process3.NextLink);
    }

    [Fact]
    public void LinkedList_Remove_NotInList_ReturnsNil()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        var notInList = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);

        // Act
        var removed = list.Remove(notInList);

        // Assert
        Assert.Equal(SNil.Instance, removed);
        Assert.Equal(2, list.Size());
    }

    [Fact]
    public void LinkedList_Remove_FromEmpty_ReturnsNil()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process = CreateProcess();

        // Act
        var removed = list.Remove(process);

        // Assert
        Assert.Equal(SNil.Instance, removed);
        Assert.True(list.IsEmpty);
    }

    [Fact]
    public void LinkedList_First_ReturnsFirstWithoutRemoving()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        var process1 = CreateProcess();
        var process2 = CreateProcess();
        
        list.AddLast(process1);
        list.AddLast(process2);

        // Act
        var first = list.First();

        // Assert
        Assert.Equal(process1, first);
        Assert.Equal(2, list.Size()); // Should not remove
        Assert.Equal(process1, list.FirstLink);
    }

    [Fact]
    public void LinkedList_First_EmptyList_ReturnsNil()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);

        // Act
        var first = list.First();

        // Assert
        Assert.Equal(SNil.Instance, first);
    }

    [Fact]
    public void LinkedList_Size_CountsCorrectly()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        
        // Act & Assert - Empty list
        Assert.Equal(0, list.Size());
        
        // Add elements
        list.AddLast(CreateProcess());
        Assert.Equal(1, list.Size());
        
        list.AddLast(CreateProcess());
        Assert.Equal(2, list.Size());
        
        list.AddLast(CreateProcess());
        Assert.Equal(3, list.Size());
        
        // Remove element
        list.RemoveFirst();
        Assert.Equal(2, list.Size());
    }

    [Fact]
    public void LinkedList_AddLast_ThrowsOnNull()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => list.AddLast(null!));
        Assert.Throws<ArgumentNullException>(() => list.AddLast(SNil.Instance as SProcess));
    }

    [Fact]
    public void LinkedList_ToString_ReflectsSize()
    {
        // Arrange
        var list = new SLinkedList(_linkedListClass);
        list.AddLast(CreateProcess());
        list.AddLast(CreateProcess());
        list.AddLast(CreateProcess());

        // Act
        var str = list.ToString();

        // Assert
        Assert.Contains("size=3", str);
    }

    [Fact]
    public void LinkedList_FieldIndices_MatchBlueBook()
    {
        // Assert that field indices match Blue Book specification
        Assert.Equal(0, SLinkedList.FirstLinkIndex);
        Assert.Equal(1, SLinkedList.LastLinkIndex);
    }

    [Fact]
    public void LinkedList_ComplexOperations_MaintainIntegrity()
    {
        // Arrange - Test complex sequence of operations
        var list = new SLinkedList(_linkedListClass);
        var processes = new List<SProcess>();
        
        for (int i = 0; i < 5; i++)
        {
            processes.Add(CreateProcess());
        }

        // Act - Add all processes
        foreach (var p in processes)
        {
            list.AddLast(p);
        }
        Assert.Equal(5, list.Size());

        // Remove first two
        Assert.Equal(processes[0], list.RemoveFirst());
        Assert.Equal(processes[1], list.RemoveFirst());
        Assert.Equal(3, list.Size());

        // Remove from middle
        Assert.Equal(processes[3], list.Remove(processes[3]));
        Assert.Equal(2, list.Size());

        // Add new ones
        var newProcess1 = CreateProcess();
        var newProcess2 = CreateProcess();
        list.AddLast(newProcess1);
        list.AddLast(newProcess2);
        Assert.Equal(4, list.Size());

        // Verify order
        Assert.Equal(processes[2], list.RemoveFirst());
        Assert.Equal(processes[4], list.RemoveFirst());
        Assert.Equal(newProcess1, list.RemoveFirst());
        Assert.Equal(newProcess2, list.RemoveFirst());
        
        // List should be empty
        Assert.True(list.IsEmpty);
        Assert.Equal(0, list.Size());
    }
}