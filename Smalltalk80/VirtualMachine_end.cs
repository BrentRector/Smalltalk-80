    
    /// <summary>
    /// Initialize all classes that have a class-side initialize method.
    /// This should be called after all classes and methods have been loaded.
    /// </summary>
    private void InitializeClasses()
    {
        Console.WriteLine("[BOOTSTRAP] Initializing global variables...");
        SystemDictionary.InitializeGlobals();
        
        Console.WriteLine("[BOOTSTRAP] Initializing classes...");
        var initializeSymbol = new Symbol("initialize");
        var initializedCount = 0;

        // Get all classes from the SystemDictionary
        foreach (var (className, cls) in SystemDictionary.AllClasses)
        {
            try
            {
                // Check if this class has a class-side initialize method
                // Class-side methods are now stored in the metaclass's regular MethodDictionary
                if (cls.Metaclass != null && cls.Metaclass.MethodDictionary.ContainsKey(initializeSymbol))
                {
                    Console.WriteLine($"[BOOTSTRAP] Initializing class: {className}");

                    // Verify the class has proper metaclass setup
                    if (cls.ClassPointer == null)
                    {
                        Console.WriteLine($"[BOOTSTRAP] Warning: Class {className} has no class pointer (metaclass). Skipping initialization.");
                        continue;
                    }

                    // Execute the initialization by evaluating "ClassName initialize"
                    EvaluateSource($"{className} initialize");
                    initializedCount++;

                    Console.WriteLine($"[BOOTSTRAP] Successfully initialized class: {className}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[BOOTSTRAP] Warning: Failed to initialize class {className}: {ex.Message}");
            }
        }

        Console.WriteLine($"[BOOTSTRAP] Initialized {initializedCount} classes");
    }
}