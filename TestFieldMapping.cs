using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Smalltalk80.Interpreter.Bootstrap;

class TestFieldMapping
{
    static void Main()
    {
        var json = File.ReadAllText("complete_system_final.json");
        var manifest = JsonSerializer.Deserialize<BootstrapManifest>(json);
        
        var fieldLookup = new FieldLookup();
        fieldLookup.RegisterManifest(manifest);
        
        // Test MethodDictionary fields
        Console.WriteLine("MethodDictionary field indices:");
        Console.WriteLine($"  tally: {fieldLookup.GetFieldIndex("MethodDictionary", "tally")}");
        Console.WriteLine($"  valueArray: {fieldLookup.GetFieldIndex("MethodDictionary", "valueArray")}");
        
        // Test IdentityDictionary fields
        Console.WriteLine("IdentityDictionary field indices:");
        Console.WriteLine($"  tally: {fieldLookup.GetFieldIndex("IdentityDictionary", "tally")}");
        Console.WriteLine($"  valueArray: {fieldLookup.GetFieldIndex("IdentityDictionary", "valueArray")}");
    }
}
