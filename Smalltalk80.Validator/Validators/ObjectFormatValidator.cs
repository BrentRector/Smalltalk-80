using Smalltalk80.Validator.JsonModels;

namespace Smalltalk80.Validator.Validators;

/// <summary>
/// Validates object formats:
/// - formatHint matches class format word
/// - Field types are consistent with format
/// </summary>
public class ObjectFormatValidator
{
    public ValidationResult Validate(ColdStartBundle bundle)
    {
        var result = new ValidationResult();

        // Build class format lookup by name index
        var classFormatsByNameIndex = new Dictionary<int, int>();
        foreach (var cls in bundle.Classes)
        {
            classFormatsByNameIndex[cls.NameIndex] = cls.Format;
        }

        // Validate each object
        foreach (var obj in bundle.Objects)
        {
            if (!obj.ClassNameIndex.HasValue)
            {
                result.AddWarning("ObjectFormat",
                    $"Object missing classNameIndex",
                    obj.LocalId);
                continue;
            }

            if (!classFormatsByNameIndex.TryGetValue(obj.ClassNameIndex.Value, out var classFormat))
            {
                result.AddError("ObjectFormat",
                    $"Object references unknown class name index {obj.ClassNameIndex.Value}",
                    obj.LocalId);
                continue;
            }

            // Blue Book format word interpretation:
            // Bits 0-1: Format type (0=pointers, 1=words, 2=bytes, 3=unused)
            // Bits 2-7: Fixed field count (for pointer objects)
            // Bits 8-15: Variable field count (for indexable objects)

            var formatType = classFormat & 0x03;
            var fixedFields = (classFormat >> 2) & 0x3F;
            var varFields = (classFormat >> 8) & 0xFF;

            if (obj.FormatHint != null)
            {
                var expectedHint = formatType switch
                {
                    0 => "pointers",
                    1 => "words",
                    2 => "bytes",
                    _ => "unknown"
                };

                if (obj.FormatHint != expectedHint)
                {
                    result.AddWarning("ObjectFormat",
                        $"Object formatHint '{obj.FormatHint}' doesn't match class format type '{expectedHint}'",
                        obj.LocalId);
                }
            }

            // Validate field count if provided
            if (obj.Fields != null)
            {
                var actualFieldCount = obj.Fields.Count;
                var expectedFieldCount = fixedFields + varFields;

                if (actualFieldCount != expectedFieldCount)
                {
                    result.AddWarning("ObjectFormat",
                        $"Object has {actualFieldCount} fields but class format declares {expectedFieldCount} (fixed={fixedFields}, var={varFields})",
                        obj.LocalId);
                }
            }
        }

        result.AddInfo($"Validated {bundle.Objects.Length} objects");
        return result;
    }
}
