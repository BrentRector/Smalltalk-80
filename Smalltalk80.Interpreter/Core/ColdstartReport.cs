// ColdstartReport.cs
// Comprehensive reporting infrastructure for coldstart loader diagnostics

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Complete report of coldstart execution metrics, validation results, and failure details.
    /// Serialized to JSON for external analysis.
    /// </summary>
    public class ColdstartReport
    {
        /// <summary>
        /// Bundle version (e.g., "1.0.6") - used to validate compatibility
        /// </summary>
        [JsonPropertyName("bundleVersion")]
        public string BundleVersion { get; set; } = "";

        /// <summary>
        /// Simple hash of bundle content (format: "symbolCount-classCount-methodCount")
        /// </summary>
        [JsonPropertyName("bundleHash")]
        public string BundleHash { get; set; } = "";

        /// <summary>
        /// UTC timestamp when coldstart began
        /// </summary>
        [JsonPropertyName("timestampUtc")]
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Individual phase execution times in milliseconds
        /// </summary>
        [JsonPropertyName("phaseDurationsMs")]
        public Dictionary<string, int> PhaseDurationsMs { get; set; } = new();

        /// <summary>
        /// Validation checks performed during coldstart
        /// </summary>
        [JsonPropertyName("validationChecks")]
        public List<ValidationResult> ValidationChecks { get; set; } = new();

        /// <summary>
        /// Key metrics from coldstart execution
        /// </summary>
        [JsonPropertyName("metrics")]
        public ColdstartMetrics Metrics { get; set; } = new();

        /// <summary>
        /// Failure details if coldstart failed (null if successful)
        /// </summary>
        [JsonPropertyName("failure")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public ColdstartFailure? Failure { get; set; }

        /// <summary>
        /// GC unreachable object samples (first 10 for diagnostics)
        /// </summary>
        [JsonPropertyName("gcUnreachableSamples")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<UnreachableObjectSample>? GCUnreachableSamples { get; set; }

        /// <summary>
        /// Record the duration of a phase execution
        /// </summary>
        public void RecordPhaseDuration(string phaseName, int durationMs)
        {
            PhaseDurationsMs[phaseName] = durationMs;
        }

        /// <summary>
        /// Add a validation check result
        /// </summary>
        public void AddValidation(string phase, string check, bool passed, string? message = null)
        {
            ValidationChecks.Add(new ValidationResult
            {
                Phase = phase,
                Check = check,
                Passed = passed,
                Message = message
            });
        }

        /// <summary>
        /// Add a GC unreachable object sample (max 10 samples kept)
        /// </summary>
        public void AddGCUnreachableSample(uint oopRaw, string format, string? localId = null)
        {
            GCUnreachableSamples ??= new List<UnreachableObjectSample>();
            if (GCUnreachableSamples.Count < 10)
            {
                GCUnreachableSamples.Add(new UnreachableObjectSample
                {
                    OOP = $"0x{oopRaw:X8}",
                    Format = format,
                    LocalId = localId
                });
            }
        }

        /// <summary>
        /// Set failure information when coldstart fails
        /// </summary>
        public void SetFailure(string phase, string invariant, string? localId = null,
            uint oopRaw = 0, int slotIndex = -1, string? expected = null, string? actual = null)
        {
            Failure = new ColdstartFailure
            {
                Phase = phase,
                Invariant = invariant,
                LocalId = localId,
                OOP = oopRaw > 0 ? $"0x{oopRaw:X8}" : null,
                SlotIndex = slotIndex >= 0 ? slotIndex : null,
                Expected = expected,
                Actual = actual
            };
        }

        // Nested metric classes

        /// <summary>
        /// Key metrics collected during coldstart
        /// </summary>
        public class ColdstartMetrics
        {
            [JsonPropertyName("symbolCount")]
            public int SymbolCount { get; set; }

            [JsonPropertyName("classCount")]
            public int ClassCount { get; set; }

            [JsonPropertyName("methodCount")]
            public int MethodCount { get; set; }

            [JsonPropertyName("initMethodCount")]
            public int InitMethodCount { get; set; }

            [JsonPropertyName("canonicalAssociationCount")]
            public int CanonicalAssociationCount { get; set; }

            [JsonPropertyName("ustableCapacity")]
            public int USTableCapacity { get; set; }

            [JsonPropertyName("totalObjectsAllocated")]
            public int TotalObjectsAllocated { get; set; }

            [JsonPropertyName("totalObjectsReachable")]
            public int TotalObjectsReachable { get; set; }

            [JsonPropertyName("gcPassed")]
            public bool GCPassed { get; set; }

            [JsonPropertyName("phase3Metrics")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public Phase3Metrics? Phase3Metrics { get; set; }
        }

        /// <summary>
        /// Detailed metrics from Phase 3 (Object Allocation)
        /// </summary>
        public class Phase3Metrics
        {
            [JsonPropertyName("objectsAllocated")]
            public int ObjectsAllocated { get; set; }

            [JsonPropertyName("classReferencesSet")]
            public int ClassReferencesSet { get; set; }

            [JsonPropertyName("fieldsInitialized")]
            public int FieldsInitialized { get; set; }
        }

        /// <summary>
        /// Single validation check result
        /// </summary>
        public class ValidationResult
        {
            [JsonPropertyName("phase")]
            public string Phase { get; set; } = "";

            [JsonPropertyName("check")]
            public string Check { get; set; } = "";

            [JsonPropertyName("passed")]
            public bool Passed { get; set; }

            [JsonPropertyName("message")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Message { get; set; }
        }

        /// <summary>
        /// Sample of an unreachable object found by GC
        /// </summary>
        public class UnreachableObjectSample
        {
            [JsonPropertyName("oop")]
            public string OOP { get; set; } = "";

            [JsonPropertyName("format")]
            public string Format { get; set; } = "";

            [JsonPropertyName("localId")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? LocalId { get; set; }
        }

        /// <summary>
        /// Details about coldstart failure
        /// </summary>
        public class ColdstartFailure
        {
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

            [JsonPropertyName("slotIndex")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public int? SlotIndex { get; set; }

            [JsonPropertyName("expected")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Expected { get; set; }

            [JsonPropertyName("actual")]
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Actual { get; set; }
        }
    }
}