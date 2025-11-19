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

            // Format word interpretation per Blue Book Chapter 27:
            // Bit 0:      isPointers  (1=contains pointers, 0=contains values)
            // Bit 1:      isWords     (1=word-addressed, 0=byte-addressed)
            // Bit 2:      isIndexable (1=has indexed fields, 0=fixed size only)
            // Bits 4-14:  fixedFields (number of fixed instance variables, 0-2047)
            bool isPointers = (classFormat & 0x01) != 0;
            bool isWords = (classFormat & 0x02) != 0;
            bool isIndexable = (classFormat & 0x04) != 0;
            var fixedFields = (classFormat >> 4) & 0x7FF;

            var formatHint = obj.GetStringProp("formatHint");
            if (formatHint != null)
            {
                // Validate formatHint is compatible with Blue Book format flags
                bool isCompatible = formatHint switch
                {
                    "pointers" => isPointers,
                    "words" => !isPointers && isWords && isIndexable,
                    "bytes" => !isPointers && !isWords && isIndexable,
                    "blockContext" => isPointers && isIndexable,
                    "compiledMethod" => !isPointers && !isWords && isIndexable,  // ByteIndexed for bytecodes
                    "custom" => true,
                    _ => false
                };

                if (!isCompatible)
                {
                    diagnostics.Add(new Diagnostic(
                        ErrorCodes.ObjectFormatInvalid,
                        Severity.Warning,
                        $"Object formatHint '{formatHint}' is incompatible with class format (isPointers={isPointers}, isWords={isWords}, isIndexable={isIndexable})",
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
