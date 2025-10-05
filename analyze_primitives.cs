using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

// Script to analyze primitive implementation status
class PrimitiveAnalyzer
{
    class PrimitiveInfo
    {
        public int Number { get; set; }
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Missing";
        public string FileName { get; set; } = "";
        public List<string> Issues { get; set; } = new();
    }

    static void Main()
    {
        var projectRoot = @"D:\Users\brent\OneDrive\Src\Smalltalk-80";
        var primitivesPath = Path.Combine(projectRoot, "Smalltalk80.Interpreter", "Primitives");

        // Blue Book standard primitives (1-127)
        var bluebookPrimitives = new Dictionary<int, string>
        {
            // Arithmetic primitives (1-17)
            [1] = "SmallInteger +",
            [2] = "SmallInteger -",
            [3] = "SmallInteger <",
            [4] = "SmallInteger >",
            [5] = "SmallInteger <=",
            [6] = "SmallInteger >=",
            [7] = "SmallInteger =",
            [8] = "SmallInteger ~=",
            [9] = "SmallInteger *",
            [10] = "SmallInteger /",
            [11] = "SmallInteger \\\\",
            [12] = "SmallInteger //",
            [13] = "SmallInteger quo:",
            [14] = "SmallInteger bitAnd:",
            [15] = "SmallInteger bitOr:",
            [16] = "SmallInteger bitXor:",
            [17] = "SmallInteger bitShift:",

            // Float primitives (40-59)
            [40] = "Float +",
            [41] = "Float -",
            [42] = "Float <",
            [43] = "Float >",
            [44] = "Float <=",
            [45] = "Float >=",
            [46] = "Float =",
            [47] = "Float ~=",
            [48] = "Float *",
            [49] = "Float /",
            [50] = "Float truncated",
            [51] = "Float fractionPart",
            [52] = "Float exponent",
            [53] = "Float timesTwoPower:",
            [54] = "asFloat",

            // Array/Stream primitives (60-67)
            [60] = "at:",
            [61] = "at:put:",
            [62] = "size",
            [63] = "String at:",
            [64] = "String at:put:",
            [65] = "next",
            [66] = "nextPut:",
            [67] = "atEnd",

            // Storage management (68-79)
            [68] = "objectAt:",
            [69] = "objectAt:put:",
            [70] = "new",
            [71] = "new:",
            [72] = "become:",
            [73] = "instVarAt:",
            [74] = "instVarAt:put:",
            [75] = "identityHash",
            [76] = "someInstance",
            [77] = "nextInstance",
            [78] = "CompiledMethod objectAt:",
            [79] = "CompiledMethod objectAt:put:",

            // Control primitives (80-89)
            [80] = "blockCopy:",
            [81] = "value",
            [82] = "value:",
            [83] = "perform:",
            [84] = "perform:with:",
            [85] = "signal",
            [86] = "wait",
            [87] = "resume",
            [88] = "suspend",
            [89] = "flushCache",

            // I/O primitives (90-109)
            [90] = "mousePoint",
            [91] = "cursorLocPut:",
            [92] = "cursorLink:",
            [93] = "inputSemaphore:",
            [94] = "sampleInterval:",
            [95] = "inputWord",
            [96] = "bitBlt:copyBits",
            [97] = "snapshot:",
            [98] = "timeWordsInto:",
            [99] = "tickWordsInto:",
            [100] = "signalAtTick:",
            [101] = "beDisplay",
            [102] = "scanCharacters",
            [103] = "drawLoopX:Y:",
            [104] = "stringHash",
            [105] = "stringReplace",

            // System primitives (110-127)
            [110] = "==",
            [111] = "class",
            [112] = "coreLeft",
            [113] = "quit",
            [114] = "exitToDebugger",
            [115] = "oopsLeft",
            [116] = "signalAtOopsLeftWordsLeft",
            [117] = "signalAtBytesLeft:",
            [118] = "deferDisplayUpdates:",
            [119] = "forceDisplayUpdate",
            [120] = "primitiveFail",
            [121] = "imageNameGet:",
            [122] = "imageNamePut:",
            [123] = "vmPath",
            [124] = "shortAt:",
            [125] = "shortAt:put:",
            [126] = "profileSemaphore:",
            [127] = "profileStart"
        };

        // Parse PrimitiveRegistry.cs to find what's registered
        var registryPath = Path.Combine(primitivesPath, "PrimitiveRegistry.cs");
        var registeredPrimitives = ParseRegisteredPrimitives(registryPath);

        // Parse all primitive implementation files
        var implementationFiles = Directory.GetFiles(primitivesPath, "*Primitives.cs");
        var implementedMethods = new Dictionary<string, List<string>>();

        foreach (var file in implementationFiles)
        {
            var methods = ParsePrimitiveMethods(file);
            implementedMethods[Path.GetFileName(file)] = methods;
        }

        // Analyze primitive status
        var primitiveInfos = new Dictionary<int, PrimitiveInfo>();

        // Check all Blue Book primitives
        foreach (var kvp in bluebookPrimitives)
        {
            var info = new PrimitiveInfo
            {
                Number = kvp.Key,
                Name = kvp.Value
            };

            if (registeredPrimitives.ContainsKey(kvp.Key))
            {
                var (methodName, fileName) = registeredPrimitives[kvp.Key];
                info.FileName = fileName;

                // Check if method is actually implemented
                bool isImplemented = false;
                foreach (var fileKvp in implementedMethods)
                {
                    if (fileKvp.Value.Contains(methodName))
                    {
                        isImplemented = true;

                        // Check for NotImplemented in the method
                        var fullPath = Path.Combine(primitivesPath, fileKvp.Key);
                        if (CheckForNotImplemented(fullPath, methodName))
                        {
                            info.Status = "Stub";
                            info.Issues.Add("Returns NotImplemented");
                        }
                        else
                        {
                            info.Status = "Implemented";
                        }
                        break;
                    }
                }

                if (!isImplemented && methodName.Contains("TODO"))
                {
                    info.Status = "TODO";
                    info.Issues.Add("Commented out in registry");
                }
                else if (!isImplemented)
                {
                    info.Status = "Registered but Missing";
                    info.Issues.Add("Method not found");
                }
            }

            primitiveInfos[kvp.Key] = info;
        }

        // Check for extra registered primitives (>127)
        foreach (var kvp in registeredPrimitives)
        {
            if (kvp.Key > 127 && !primitiveInfos.ContainsKey(kvp.Key))
            {
                primitiveInfos[kvp.Key] = new PrimitiveInfo
                {
                    Number = kvp.Key,
                    Name = kvp.Value.Item1,
                    Status = "Extension",
                    FileName = kvp.Value.Item2
                };
            }
        }

        // Generate report
        GenerateReport(primitiveInfos, bluebookPrimitives);
    }

    static Dictionary<int, (string methodName, string fileName)> ParseRegisteredPrimitives(string filePath)
    {
        var result = new Dictionary<int, (string, string)>();
        var content = File.ReadAllText(filePath);

        // Match table.Register calls, including commented ones
        var regex = new Regex(@"//?\s*table\.Register\((\d+),\s*""([^""]+)"",\s*(\w+)\.(\w+)\)");
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            int number = int.Parse(match.Groups[1].Value);
            string name = match.Groups[2].Value;
            string className = match.Groups[3].Value;
            string methodName = match.Groups[4].Value;

            result[number] = (methodName, className + ".cs");
        }

        return result;
    }

    static List<string> ParsePrimitiveMethods(string filePath)
    {
        var result = new List<string>();
        var content = File.ReadAllText(filePath);

        // Match public static PrimitiveResult methods
        var regex = new Regex(@"public\s+static\s+PrimitiveResult\s+(\w+)\s*\(");
        var matches = regex.Matches(content);

        foreach (Match match in matches)
        {
            result.Add(match.Groups[1].Value);
        }

        return result;
    }

    static bool CheckForNotImplemented(string filePath, string methodName)
    {
        var content = File.ReadAllText(filePath);

        // Find the method and check if it returns NotImplemented
        var methodPattern = $@"public\s+static\s+PrimitiveResult\s+{methodName}\s*\([^{{]+\{{[^{{]*PrimitiveFailure\.NotImplemented";
        return Regex.IsMatch(content, methodPattern, RegexOptions.Singleline);
    }

    static void GenerateReport(Dictionary<int, PrimitiveInfo> primitiveInfos, Dictionary<int, string> bluebookPrimitives)
    {
        var sb = new StringBuilder();

        sb.AppendLine("# Smalltalk-80 Primitive Implementation Analysis");
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Summary statistics
        var implemented = primitiveInfos.Count(p => p.Value.Status == "Implemented");
        var stubs = primitiveInfos.Count(p => p.Value.Status == "Stub");
        var todos = primitiveInfos.Count(p => p.Value.Status == "TODO");
        var missing = primitiveInfos.Count(p => p.Value.Status == "Missing");
        var bluebookTotal = bluebookPrimitives.Count;

        sb.AppendLine("## Summary");
        sb.AppendLine($"- Blue Book Primitives (1-127): {bluebookTotal}");
        sb.AppendLine($"  - Fully Implemented: {implemented}");
        sb.AppendLine($"  - Stubs (NotImplemented): {stubs}");
        sb.AppendLine($"  - TODOs (Commented): {todos}");
        sb.AppendLine($"  - Missing: {missing}");
        sb.AppendLine($"- Coverage: {(implemented * 100.0 / bluebookTotal):F1}%");
        sb.AppendLine();

        // Detailed table by category
        sb.AppendLine("## Detailed Implementation Status");
        sb.AppendLine();

        // Arithmetic (1-17)
        sb.AppendLine("### Arithmetic Primitives (1-17)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 1; i <= 17; i++)
        {
            if (primitiveInfos.ContainsKey(i))
            {
                var info = primitiveInfos[i];
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // Float (40-54)
        sb.AppendLine("### Float Primitives (40-54)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 40; i <= 54; i++)
        {
            if (bluebookPrimitives.ContainsKey(i))
            {
                var info = primitiveInfos.ContainsKey(i) ? primitiveInfos[i] :
                    new PrimitiveInfo { Number = i, Name = bluebookPrimitives[i], Status = "Missing" };
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // Array/Stream (60-67)
        sb.AppendLine("### Array/Stream Primitives (60-67)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 60; i <= 67; i++)
        {
            if (primitiveInfos.ContainsKey(i))
            {
                var info = primitiveInfos[i];
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // Storage Management (68-79)
        sb.AppendLine("### Storage Management Primitives (68-79)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 68; i <= 79; i++)
        {
            if (bluebookPrimitives.ContainsKey(i))
            {
                var info = primitiveInfos.ContainsKey(i) ? primitiveInfos[i] :
                    new PrimitiveInfo { Number = i, Name = bluebookPrimitives[i], Status = "Missing" };
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // Control (80-89)
        sb.AppendLine("### Control Primitives (80-89)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 80; i <= 89; i++)
        {
            if (bluebookPrimitives.ContainsKey(i))
            {
                var info = primitiveInfos.ContainsKey(i) ? primitiveInfos[i] :
                    new PrimitiveInfo { Number = i, Name = bluebookPrimitives[i], Status = "Missing" };
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // I/O (90-109)
        sb.AppendLine("### I/O Primitives (90-109)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 90; i <= 109; i++)
        {
            if (bluebookPrimitives.ContainsKey(i))
            {
                var info = primitiveInfos.ContainsKey(i) ? primitiveInfos[i] :
                    new PrimitiveInfo { Number = i, Name = bluebookPrimitives[i], Status = "Missing" };
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // System (110-127)
        sb.AppendLine("### System Primitives (110-127)");
        sb.AppendLine("| # | Name | Status | Issues |");
        sb.AppendLine("|---|------|--------|--------|");
        for (int i = 110; i <= 127; i++)
        {
            if (bluebookPrimitives.ContainsKey(i))
            {
                var info = primitiveInfos.ContainsKey(i) ? primitiveInfos[i] :
                    new PrimitiveInfo { Number = i, Name = bluebookPrimitives[i], Status = "Missing" };
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {i} | {info.Name} | {info.Status} | {issues} |");
            }
        }
        sb.AppendLine();

        // Extensions
        var extensions = primitiveInfos.Where(p => p.Key > 127).OrderBy(p => p.Key);
        if (extensions.Any())
        {
            sb.AppendLine("### Extension Primitives (>127)");
            sb.AppendLine("| # | Name | Status | Issues |");
            sb.AppendLine("|---|------|--------|--------|");
            foreach (var kvp in extensions)
            {
                var info = kvp.Value;
                var issues = string.Join("; ", info.Issues);
                sb.AppendLine($"| {kvp.Key} | {info.Name} | {info.Status} | {issues} |");
            }
            sb.AppendLine();
        }

        // Critical missing primitives
        sb.AppendLine("## Critical Missing Primitives");
        var criticalMissing = new[] {
            40, 41, 42, 43, 44, 45, 46, 47, 48, 49, // Float arithmetic
            68, 69, 72, 73, 74, // Object access
            84, 89, // perform:with:, flushCache
            90, 91, 92, 93, 94, 95, 96, 97, 98, 99, 100, 101, 102, 103 // I/O
        };

        sb.AppendLine("The following critical primitives are missing:");
        foreach (var num in criticalMissing)
        {
            if (bluebookPrimitives.ContainsKey(num))
            {
                var info = primitiveInfos.ContainsKey(num) ? primitiveInfos[num] :
                    new PrimitiveInfo { Number = num, Name = bluebookPrimitives[num], Status = "Missing" };
                if (info.Status == "Missing" || info.Status == "TODO")
                {
                    sb.AppendLine($"- #{num}: {bluebookPrimitives[num]}");
                }
            }
        }

        // Write report
        var reportPath = Path.Combine(@"D:\Users\brent\OneDrive\Src\Smalltalk-80", "PRIMITIVE_ANALYSIS.md");
        File.WriteAllText(reportPath, sb.ToString());

        Console.WriteLine($"Report written to: {reportPath}");
        Console.WriteLine($"\nSummary: {implemented}/{bluebookTotal} primitives implemented ({(implemented * 100.0 / bluebookTotal):F1}%)");
    }
}

PrimitiveAnalyzer.Main();