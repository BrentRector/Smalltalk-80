using System;

public class TestProgram
{
    public static void Main()
    {
        // Test if SmalltalkClass constructor with int parameter works
        var cls = new SmalltalkClass(5);
        Console.WriteLine($"Created SmalltalkClass with {cls.Fields?.Length ?? 0} fields");
    }
}
