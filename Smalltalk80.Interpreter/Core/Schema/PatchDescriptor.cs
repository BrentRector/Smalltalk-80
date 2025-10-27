// PatchDescriptor.cs
// Schema for bundle patches - explicit operations to fix forward references and circular structures

using System.Text.Json.Serialization;

namespace Smalltalk80.Interpreter.Core.Schema
{
    /// <summary>
    /// Describes a patch operation to apply during coldstart Phase 5.
    /// Patches resolve forward references and circular structures that
    /// couldn't be set during initial object allocation.
    /// </summary>
    public class PatchDescriptor
    {
        /// <summary>
        /// Type of patch operation.
        /// Valid values: "SetInstanceVariable", "InstallMethodDictionary", "ReplaceLiteral"
        /// </summary>
        [JsonPropertyName("type")]
        public required string Type { get; init; }

        /// <summary>
        /// Target object localId (for SetInstanceVariable patches).
        /// Example: "class:Object"
        /// </summary>
        [JsonPropertyName("targetLocalId")]
        public string? TargetLocalId { get; init; }

        /// <summary>
        /// Field name to set (for SetInstanceVariable patches).
        /// Example: "superclass"
        /// </summary>
        [JsonPropertyName("fieldName")]
        public string? FieldName { get; init; }

        /// <summary>
        /// New value to set (for SetInstanceVariable and ReplaceLiteral patches).
        /// </summary>
        [JsonPropertyName("value")]
        public FieldValue? Value { get; init; }

        /// <summary>
        /// Class localId (for InstallMethodDictionary patches).
        /// Example: "class:Point"
        /// </summary>
        [JsonPropertyName("classLocalId")]
        public string? ClassLocalId { get; init; }

        /// <summary>
        /// Method localId (for ReplaceLiteral patches).
        /// Example: "method:Point>>+"
        /// </summary>
        [JsonPropertyName("methodLocalId")]
        public string? MethodLocalId { get; init; }

        /// <summary>
        /// Literal index to replace (for ReplaceLiteral patches).
        /// 0-based index into method's literal array.
        /// </summary>
        [JsonPropertyName("literalIndex")]
        public int? LiteralIndex { get; init; }
    }
}
