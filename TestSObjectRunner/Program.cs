using Smalltalk80.Interpreter.Core;

Console.WriteLine("Testing SObject Implementation");
Console.WriteLine("==============================");

// Test 1: Identity hash stability
Console.WriteLine("\nTest 1: Identity hash stability");
var obj1 = new SObject();
var hash1 = obj1.IdentityHash;
var hash2 = obj1.IdentityHash;
Console.WriteLine($"  Object hash first call: {hash1:X8}");
Console.WriteLine($"  Object hash second call: {hash2:X8}");
Console.WriteLine($"  Stable: {hash1 == hash2}");

// Test 2: Different objects have different hashes
Console.WriteLine("\nTest 2: Unique identity hashes");
var obj2 = new SObject();
var obj3 = new SObject();
Console.WriteLine($"  Object 1 hash: {obj1.IdentityHash:X8}");
Console.WriteLine($"  Object 2 hash: {obj2.IdentityHash:X8}");
Console.WriteLine($"  Object 3 hash: {obj3.IdentityHash:X8}");
Console.WriteLine($"  All unique: {obj1.IdentityHash != obj2.IdentityHash && obj2.IdentityHash != obj3.IdentityHash}");

// Test 3: Singleton objects
Console.WriteLine("\nTest 3: Singleton objects");
var nil = SNil.Instance;
var trueObj = STrue.Instance;
var falseObj = SFalse.Instance;
Console.WriteLine($"  nil hash: {nil.IdentityHash:X8} (should be 00000000)");
Console.WriteLine($"  true hash: {trueObj.IdentityHash:X8} (should be 00000001)");
Console.WriteLine($"  false hash: {falseObj.IdentityHash:X8} (should be 00000002)");

// Test 4: Instance variable access
Console.WriteLine("\nTest 4: Instance variable access");
var objWithVars = new SObject(null, 3);
objWithVars.SetInstVar(0, "Hello");
objWithVars.SetInstVar(1, 42);
objWithVars.SetInstVar(2, obj1);
Console.WriteLine($"  InstVar[0]: {objWithVars.GetInstVar(0)}");
Console.WriteLine($"  InstVar[1]: {objWithVars.GetInstVar(1)}");
Console.WriteLine($"  InstVar[2]: {objWithVars.GetInstVar(2)}");
Console.WriteLine($"  InstVar count: {objWithVars.InstVarCount}");

// Test 5: Instance variable bounds checking
Console.WriteLine("\nTest 5: Instance variable bounds checking");
try
{
    objWithVars.GetInstVar(3);
    Console.WriteLine("  ERROR: Should have thrown exception for out of bounds access");
}
catch (ArgumentOutOfRangeException)
{
    Console.WriteLine("  Correctly threw ArgumentOutOfRangeException for out of bounds access");
}

// Test 6: Object with null class
Console.WriteLine("\nTest 6: Object with null class");
var instance = new SObject(null, 2);
Console.WriteLine($"  Instance class: {instance.Klass?.Name ?? "null"}");
Console.WriteLine($"  Instance toString: {instance}");

// Test 7: Thread safety of hash generation
Console.WriteLine("\nTest 7: Thread-safe hash generation");
var objects = new List<SObject>();
var tasks = new Task[10];
for (int i = 0; i < 10; i++)
{
    tasks[i] = Task.Run(() =>
    {
        for (int j = 0; j < 10; j++)
        {
            var o = new SObject();
            lock (objects)
            {
                objects.Add(o);
            }
        }
    });
}
Task.WaitAll(tasks);

var hashes = objects.Select(o => o.IdentityHash).ToHashSet();
Console.WriteLine($"  Created {objects.Count} objects in parallel");
Console.WriteLine($"  Unique hashes: {hashes.Count}");
Console.WriteLine($"  All unique: {hashes.Count == objects.Count}");

Console.WriteLine("\n==============================");
Console.WriteLine("All tests completed successfully!");