// SnapshotWriter.cs
// Writes detailed failure snapshots for debugging coldstart failures

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Smalltalk80.Interpreter.Core.HeapManager;
using Smalltalk80.Interpreter.Core.Memory;
using Smalltalk80.Interpreter.Core.Schema;
using Smalltalk80.Interpreter.Core.Memory;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Writes detailed snapshots of heap state when coldstart failures occur.
    /// Captures failed object, related objects, and comprehensive debugging information.
    /// </summary>
    public class SnapshotWriter
    {
        private readonly ISmalltalkHeapManager _heapManager;
        private readonly ColdStartBundle _bundle;
        private readonly IReadOnlyDictionary<string, SmalltalkOOP> _localIdToObjectId;

        /// <summary>
        /// Initialize a new SnapshotWriter with heap access
        /// </summary>
        public SnapshotWriter(
            ISmalltalkHeapManager heapManager,
            ColdStartBundle bundle,
            IReadOnlyDictionary<string, SmalltalkOOP> localIdToObjectId)
        {
            _heapManager = heapManager ?? throw new ArgumentNullException(nameof(heapManager));
            _bundle = bundle ?? throw new ArgumentNullException(nameof(bundle));
            _localIdToObjectId = localIdToObjectId ?? throw new ArgumentNullException(nameof(localIdToObjectId));
        }

        /// <summary>
        /// Write a failure snapshot to disk
        /// </summary>
        public void WriteSnapshot(string bundlePath, ColdstartReport.ColdstartFailure? failure, Exception? exception)
        {
            try
            {
                var snapshot = new FailureSnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    BundlePath = bundlePath,
                    Phase = failure?.Phase ?? "Unknown",
                    Invariant = failure?.Invariant ?? exception?.Message ?? "Unknown error",
                    LocalId = failure?.LocalId,
                    OOP = failure?.OOP,
                    Expected = failure?.Expected,
                    Actual = failure?.Actual,
                    StackTrace = exception?.StackTrace
                };

                // Capture failed object details if we have an OOP
                if (!string.IsNullOrEmpty(failure?.OOP) && failure.OOP.StartsWith("0x"))
                {
                    if (uint.TryParse(failure.OOP.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out uint oopRaw))
                    {
                        var oop = new SmalltalkOOP((int)oopRaw);
                        snapshot.FailedObject = CaptureObjectDetails(oop);

                        // Capture related objects
                        snapshot.RelatedObjects = CaptureRelatedObjects(failure, oop);
                    }
                }

                // Write snapshot to file
                string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                string snapshotPath = Path.Combine(
                    Path.GetDirectoryName(bundlePath) ?? ".",
                    $"coldstart_failure_{timestamp}.json");

                var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions
                {
                    WriteIndented = true,
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                File.WriteAllText(snapshotPath, json);
                Console.WriteLine($"\nFailure snapshot written to: {snapshotPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nWarning: Failed to write snapshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Capture detailed information about an object
        /// </summary>
        private ObjectDetails? CaptureObjectDetails(SmalltalkOOP objectOOP)
        {
            try
            {
                if (objectOOP.IsSmallInteger)
                {
                    return new ObjectDetails
                    {
                        OOP = $"0x{objectOOP.RawValue:X8}",
                        Format = "SmallInteger",
                        Value = objectOOP.AsSmallInteger().ToString()
                    };
                }

                var format = _heapManager.GetObjectFormat(objectOOP);
                var details = new ObjectDetails
                {
                    OOP = $"0x{objectOOP.RawValue:X8}",
                    Format = format.ToString()
                };

                // Get class OOP (stored in slot 0)
                var classOOP = _heapManager.GetClassOOP(objectOOP);
                details.ClassOOP = $"0x{classOOP.RawValue:X8}";

                // Try to get class name
                try
                {
                    details.ClassName = _heapManager.GetClassName(classOOP);
                }
                catch
                {
                    // Class name may not be accessible yet
                }

                // Capture instance variables for pointer objects
                if (format == ObjectFormat.Pointer || format == ObjectFormat.PointerIndexed)
                {
                    int instanceVarCount = _heapManager.GetInstanceVariableCount(objectOOP);
                    details.InstanceVarCount = instanceVarCount;
                    details.InstanceVariables = new Dictionary<int, string>();

                    for (int i = 0; i < instanceVarCount && i < 20; i++) // Limit to 20 for large objects
                    {
                        try
                        {
                            var varOOP = _heapManager.GetInstanceVariable(objectOOP, i);
                            details.InstanceVariables[i] = FormatOOP(varOOP);
                        }
                        catch
                        {
                            details.InstanceVariables[i] = "ERROR";
                        }
                    }
                }

                // Capture indexed size
                if (format == ObjectFormat.PointerIndexed || format == ObjectFormat.ByteIndexed)
                {
                    details.IndexedSize = _heapManager.GetIndexedSize(objectOOP);
                }

                // Special handling for CompiledMethod - include bytecode disassembly
                if (details.ClassName == "CompiledMethod" || IsCompiledMethod(objectOOP))
                {
                    details.BytecodeDisassembly = DisassembleMethod(objectOOP);
                }

                return details;
            }
            catch (Exception ex)
            {
                return new ObjectDetails
                {
                    OOP = $"0x{objectOOP.RawValue:X8}",
                    Format = "ERROR",
                    Error = ex.Message
                };
            }
        }

        /// <summary>
        /// Disassemble bytecodes from a CompiledMethod
        /// </summary>
        private List<string>? DisassembleMethod(SmalltalkOOP methodOOP)
        {
            try
            {
                var result = new List<string>();

                // Get the bytecode array reference (last indexed slot)
                int indexedSize = _heapManager.GetIndexedSize(methodOOP);
                var bytecodeArrayOOP = _heapManager.GetIndexedOOP(methodOOP, indexedSize - 1);

                if (_heapManager.GetObjectFormat(bytecodeArrayOOP) == ObjectFormat.ByteIndexed)
                {
                    int byteCount = _heapManager.GetIndexedSize(bytecodeArrayOOP);
                    for (int i = 0; i < byteCount && i < 100; i++) // Limit to 100 bytes
                    {
                        byte b = _heapManager.GetIndexedByte(bytecodeArrayOOP, i);
                        result.Add($"  {i:D3}: 0x{b:X2}");
                    }

                    if (byteCount > 100)
                    {
                        result.Add($"  ... ({byteCount - 100} more bytes)");
                    }
                }

                return result.Count > 0 ? result : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Capture related objects based on failure context
        /// </summary>
        private Dictionary<string, ObjectDetails>? CaptureRelatedObjects(ColdstartReport.ColdstartFailure failure, SmalltalkOOP failedOOP)
        {
            var related = new Dictionary<string, ObjectDetails>();

            try
            {
                // If this is a method, capture its home class
                if (IsCompiledMethod(failedOOP))
                {
                    // Try to find the class that owns this method
                    string? methodLocalId = failure.LocalId;
                    if (!string.IsNullOrEmpty(methodLocalId))
                    {
                        // Parse class name from method localId (e.g., "method:Point:x")
                        if (methodLocalId.StartsWith("method:"))
                        {
                            var parts = methodLocalId.Split(':');
                            if (parts.Length >= 3)
                            {
                                string className = parts[1];
                                string classLocalId = $"class:{className}";

                                if (_localIdToObjectId.TryGetValue(classLocalId, out var classOOP))
                                {
                                    var classDetails = CaptureObjectDetails(classOOP);
                                    if (classDetails != null)
                                    {
                                        related["homeClass"] = classDetails;
                                    }
                                }
                            }
                        }
                    }
                }

                // If this is a class, capture its metaclass
                if (failure?.LocalId?.StartsWith("class:") == true)
                {
                    var classOOP = _heapManager.GetClassOOP(failedOOP);
                    if (!classOOP.Equals(failedOOP)) // Avoid infinite recursion
                    {
                        var metaclassDetails = CaptureObjectDetails(classOOP);
                        if (metaclassDetails != null)
                        {
                            related["metaclass"] = metaclassDetails;
                        }
                    }
                }

                // If slot index specified, capture the referenced object
                if (failure?.SlotIndex != null)
                {
                    try
                    {
                        var referencedOOP = _heapManager.GetInstanceVariable(failedOOP, failure.SlotIndex.Value);
                        var refDetails = CaptureObjectDetails(referencedOOP);
                        if (refDetails != null)
                        {
                            related[$"slot_{failure.SlotIndex}"] = refDetails;
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return related.Count > 0 ? related : null;
        }

        /// <summary>
        /// Check if an object appears to be a CompiledMethod
        /// </summary>
        private bool IsCompiledMethod(SmalltalkOOP oop)
        {
            try
            {
                if (oop.IsSmallInteger)
                    return false;

                var format = _heapManager.GetObjectFormat(oop);
                if (format != ObjectFormat.PointerIndexed)
                    return false;

                // Check if first indexed element is a SmallInteger (method header)
                if (_heapManager.GetIndexedSize(oop) > 0)
                {
                    var firstElement = _heapManager.GetIndexedOOP(oop, 0);
                    return firstElement.IsSmallInteger;
                }
            }
            catch { }

            return false;
        }

        /// <summary>
        /// Format an OOP value for display
        /// </summary>
        private string FormatOOP(SmalltalkOOP oop)
        {
            if (oop.IsSmallInteger)
            {
                return $"SmallInt({oop.AsSmallInteger()})";
            }
            return $"0x{oop.RawValue:X8}";
        }

        /// <summary>
        /// Complete failure snapshot with all debugging information
        /// </summary>
        public class FailureSnapshot
        {
            [JsonPropertyName("timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonPropertyName("bundlePath")]
            public string BundlePath { get; set; } = "";

            [JsonPropertyName("phase")]
            public string Phase { get; set; } = "";

            [JsonPropertyName("invariant")]
            public string Invariant { get; set; } = "";

            [JsonPropertyName("localId")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? LocalId { get; set; }

            [JsonPropertyName("oop")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? OOP { get; set; }

            [JsonPropertyName("expected")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Expected { get; set; }

            [JsonPropertyName("actual")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Actual { get; set; }

            [JsonPropertyName("failedObject")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public ObjectDetails? FailedObject { get; set; }

            [JsonPropertyName("relatedObjects")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public Dictionary<string, ObjectDetails>? RelatedObjects { get; set; }

            [JsonPropertyName("stackTrace")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? StackTrace { get; set; }
        }

        /// <summary>
        /// Detailed information about a heap object
        /// </summary>
        public class ObjectDetails
        {
            [JsonPropertyName("oop")]
            public string OOP { get; set; } = "";

            [JsonPropertyName("format")]
            public string Format { get; set; } = "";

            [JsonPropertyName("className")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? ClassName { get; set; }

            [JsonPropertyName("classOOP")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? ClassOOP { get; set; }

            [JsonPropertyName("instanceVarCount")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? InstanceVarCount { get; set; }

            [JsonPropertyName("indexedSize")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? IndexedSize { get; set; }

            [JsonPropertyName("instanceVariables")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public Dictionary<int, string>? InstanceVariables { get; set; }

            [JsonPropertyName("bytecodeDisassembly")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public List<string>? BytecodeDisassembly { get; set; }

            [JsonPropertyName("value")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Value { get; set; }

            [JsonPropertyName("error")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Error { get; set; }
        }
    }
}