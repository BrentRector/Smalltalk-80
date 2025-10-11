using System.Text.Json;
using Smalltalk80.Validator.Core.Diagnostics;
using Smalltalk80.Validator.Core.Helpers;

namespace Smalltalk80.Validator.Core.Validators;

/// <summary>
/// Validates object formats:
/// - formatHint matches class format word
/// - Field types are consistent with format
/// </summary>
public class ObjectFormatValidator : IValidator
{
    public string Id => "ObjectFormatValidator";
    public bool IsHeavy => false;

    public Task<IEnumerable<Diagnostic>> ValidateAsync(ValidationContext ctx, CancellationToken ct = default)
    {
        var diagnostics = new List<Diagnostic>();

        // Build class format lookup by name index
        // Only use instance classes (not metaclasses) since objects are instances of classes
        var classFormatsByNameIndex = new Dictionary<int, int>();
        foreach (var cls in ctx.Classes)
        {
            var isMetaclass = cls.GetBoolProp("isMetaclass") ?? false;
            if (isMetaclass) continue;

            var nameIndex = cls.GetIntProp("nameIndex");
            var format = cls.GetIntProp("format");
            if (nameIndex.HasValue && format.HasValue)
            {
                classFormatsByNameIndex[nameIndex.Value] = format.Value;
            }
        }

        // Validate each object
        foreach (var obj in ctx.Objects)
        {
            var localId = obj.GetStringProp("localId") ?? "unknown";
            var classNameIndex = obj.GetIntProp("classNameIndex");

            if (!classNameIndex.HasValue)
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ObjectFormatInvalid,
                    Severity.Warning,
                    $"Object missing classNameIndex",
                    new[] { new Location("objects", localId) },
                "Object must have classNameIndex"));
                continue;
            }

            if (!classFormatsByNameIndex.TryGetValue(classNameIndex.Value, out var classFormat))
            {
                diagnostics.Add(new Diagnostic(
                    ErrorCodes.ObjectFormatInvalid,
                    Severity.Error,
                    $"Object references unknown class name index {classNameIndex.Value}",
                    new[] { new Location("objects", localId) },
                "Object must reference a valid class"));
                continue;
            }

            // Blue Book format word interpretation
            var instanceSpec = (classFormat >> 12) & 0x03;
            var fixedFields = (classFormat >> 6) & 0x3F;
            var isIndexable = (classFormat & 0x10) != 0;
            var hasPointers = (classFormat & 0x08) != 0;

            var formatHint = obj.GetStringProp("formatHint");
            if (formatHint != null)
            {
                // Validate formatHint is compatible with format word
                bool isCompatible = formatHint switch
                {
                    "pointers" => (instanceSpec == 0x01) || (instanceSpec == 0x02 && hasPointers),
                    "words" => instanceSpec == 0x02 && !hasPointers,
                    "bytes" => instanceSpec == 0x03,
                    "blockContext" => instanceSpec == 0x02 && hasPointers && isIndexable,
                    "compiledMethod" => instanceSpec == 0x02 && !hasPointers && isIndexable,
                    "custom" => true,
                    _ => false
                };

                if (!isCompatible)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.ObjectFormatInvalid,
                        Severity.Warning,
                        $"Object formatHint '{formatHint}' is incompatible with class format (instanceSpec={instanceSpec:X}, indexable={isIndexable}, pointers={hasPointers})",
                        new[] { new Location("objects", localId) },
                "Update formatHint to match class format"));
                }
            }

            // Validate fixed field count if provided
            if (obj.TryGetProperty("fields", out var fieldsArr) && fieldsArr.ValueKind == JsonValueKind.Array)
            {
                var actualFieldCount = fieldsArr.GetArrayLength();
                var expectedFieldCount = fixedFields;

                if (actualFieldCount != expectedFieldCount)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.ObjectFormatInvalid,
                        Severity.Warning,
                        $"Object has {actualFieldCount} fixed fields but class format declares {expectedFieldCount}",
                        new[] { new Location("objects", localId) },
                        $"Update fields array to match class format ({expectedFieldCount} fields)"));
                }
            }
        }

        return Task.FromResult<IEnumerable<Diagnostic>>(diagnostics);
    }
}
