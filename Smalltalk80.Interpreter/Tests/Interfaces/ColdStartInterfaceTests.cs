using Smalltalk80.Interpreter.Engine;
using Smalltalk80.Interpreter.Engine.LoaderPhases;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Core.HeapManager;

namespace Smalltalk80.Interpreter.Tests.Interfaces;

/// <summary>
/// Unit tests for IPhase and IColdStartLoader interfaces.
/// Tests cold start phase contracts and loader API.
/// </summary>
public class ColdStartInterfaceTests
{
    /// <summary>
    /// Test that all phases implement IPhase correctly.
    /// </summary>
    public static void TestPhaseInterfaceImplementation()
    {
        // Verify all phase classes implement IPhase
        var phaseTypes = new[]
        {
            typeof(Phase1_PreflightAndSymbols),
            typeof(Phase2_ClassInfrastructure),
            typeof(Phase3_ObjectAllocation),
            typeof(Phase4_MethodCompilation),
            typeof(Phase5_GlobalsAndPatches),
            typeof(Phase6_ValidationAndGC),
        };

        foreach (var phaseType in phaseTypes)
        {
            if (!typeof(IPhase).IsAssignableFrom(phaseType))
                throw new Exception($"{phaseType.Name} does not implement IPhase");
        }

        Console.WriteLine("✅ All 6 phases implement IPhase interface");
    }

    /// <summary>
    /// Test IPhase interface members exist.
    /// </summary>
    public static void TestPhaseInterfaceMembers()
    {
        var interfaceType = typeof(IPhase);

        // Check required properties
        var nameProperty = interfaceType.GetProperty("Name");
        var phaseNumberProperty = interfaceType.GetProperty("PhaseNumber");

        if (nameProperty == null)
            throw new Exception("IPhase missing Name property");
        if (phaseNumberProperty == null)
            throw new Exception("IPhase missing PhaseNumber property");

        // Check required methods
        var runMethod = interfaceType.GetMethod("Run");
        var validatePreMethod = interfaceType.GetMethod("ValidatePreconditions");
        var validatePostMethod = interfaceType.GetMethod("ValidatePostconditions");

        if (runMethod == null)
            throw new Exception("IPhase missing Run method");
        if (validatePreMethod == null)
            throw new Exception("IPhase missing ValidatePreconditions method");
        if (validatePostMethod == null)
            throw new Exception("IPhase missing ValidatePostconditions method");

        Console.WriteLine("✅ IPhase interface has all required members");
    }

    /// <summary>
    /// Test IColdStartLoader interface members exist.
    /// </summary>
    public static void TestLoaderInterfaceMembers()
    {
        var interfaceType = typeof(IColdStartLoader);

        // Check required properties
        var layoutResolver = interfaceType.GetProperty("LayoutResolver");
        var localIdToObjectId = interfaceType.GetProperty("LocalIdToObjectId");
        var report = interfaceType.GetProperty("Report");
        var phases = interfaceType.GetProperty("Phases");

        if (layoutResolver == null)
            throw new Exception("IColdStartLoader missing LayoutResolver property");
        if (localIdToObjectId == null)
            throw new Exception("IColdStartLoader missing LocalIdToObjectId property");
        if (report == null)
            throw new Exception("IColdStartLoader missing Report property");
        if (phases == null)
            throw new Exception("IColdStartLoader missing Phases property");

        // Check required methods
        var runMethod = interfaceType.GetMethod("Run");
        if (runMethod == null)
            throw new Exception("IColdStartLoader missing Run method");

        Console.WriteLine("✅ IColdStartLoader interface has all required members");
    }

    /// <summary>
    /// Test ColdStartOrchestrator implements IColdStartLoader.
    /// </summary>
    public static void TestOrchestratorImplementation()
    {
        if (!typeof(IColdStartLoader).IsAssignableFrom(typeof(ColdStartOrchestrator)))
            throw new Exception("ColdStartOrchestrator does not implement IColdStartLoader");

        Console.WriteLine("✅ ColdStartOrchestrator implements IColdStartLoader");
    }

    /// <summary>
    /// Test PhaseValidationResult struct.
    /// </summary>
    public static void TestPhaseValidationResult()
    {
        // Test Ok result
        var okResult = PhaseValidationResult.Ok();
        if (!okResult.Success)
            throw new Exception("Ok result should have Success=true");
        if (okResult.Failures.Count != 0)
            throw new Exception("Ok result should have empty Failures");

        // Test Fail result
        var failures = new List<string> { "Test failure" };
        var failResult = PhaseValidationResult.Fail(failures);
        if (failResult.Success)
            throw new Exception("Fail result should have Success=false");
        if (failResult.Failures.Count != 1)
            throw new Exception("Fail result should have 1 failure");

        Console.WriteLine("✅ PhaseValidationResult struct works correctly");
    }

    /// <summary>
    /// Run all ColdStart interface tests.
    /// </summary>
    public static int RunAllTests()
    {
        int passed = 0;
        int failed = 0;

        var tests = new (string Name, Action Test)[]
        {
            ("Phase Interface Implementation", TestPhaseInterfaceImplementation),
            ("Phase Interface Members", TestPhaseInterfaceMembers),
            ("Loader Interface Members", TestLoaderInterfaceMembers),
            ("Orchestrator Implementation", TestOrchestratorImplementation),
            ("PhaseValidationResult", TestPhaseValidationResult),
        };

        foreach (var (name, test) in tests)
        {
            try
            {
                test();
                passed++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {name}: {ex.Message}");
                failed++;
            }
        }

        Console.WriteLine($"\nColdStart Interface Tests: {passed} passed, {failed} failed");
        return failed;
    }
}
