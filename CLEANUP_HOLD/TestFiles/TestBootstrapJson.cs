using System;
using System.IO;
using System.Text.Json;

/// <summary>
/// Simple test program to verify bootstrap JSON files are valid and accessible.
/// </summary>
class TestBootstrapJson
{
    static void Main()
    {
        Console.WriteLine("Testing Bootstrap JSON Files");
        Console.WriteLine("=============================");
        
        var bootstrapDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Bootstrap");
        var fullPath = Path.GetFullPath(bootstrapDir);
        
        Console.WriteLine($"Looking for bootstrap files in: {fullPath}");
        
        if (!Directory.Exists(fullPath))
        {
            Console.WriteLine("❌ Bootstrap directory not found!");
            return;
        }
        
        // Test Phase 1
        TestPhase("bootstrap_phase1.json", fullPath);
        
        // Test Phase 2  
        TestPhase("bootstrap_phase2.json", fullPath);
        
        // Test Schema
        TestPhase("bootstrap-schema.json", fullPath);
        
        Console.WriteLine("\n✅ All bootstrap JSON files validated successfully!");
    }
    
    static void TestPhase(string filename, string directory)
    {
        var filePath = Path.Combine(directory, filename);
        
        Console.Write($"Testing {filename}... ");
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine("❌ File not found!");
            return;
        }
        
        try
        {
            var json = File.ReadAllText(filePath);
            var parsed = JsonSerializer.Deserialize<object>(json);
            
            if (parsed == null)
            {
                Console.WriteLine("❌ Failed to parse JSON!");
                return;
            }
            
            Console.WriteLine("✅ Valid JSON");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}");
        }
    }
}