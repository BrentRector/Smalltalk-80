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
        // Only use instance classes (not metaclasses) since objects are instances of classes
        var classFormatsByNameIndex = new Dictionary<int, int>();
        foreach (var cls in bundle.Classes.Where(c => !c.IsMetaclass))
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

            // Blue Book format word interpretation (per Interpreter-Architecture.md):
            // Bits 15-14: Unused (00)
            // Bits 13-12: Instance Specification (00=no fields, 01=pointers, 10=words, 11=bytes)
            // Bits 11-6:  Fixed fields count (0-63)
            // Bit 5:      Unused
            // Bit 4:      Indexable flag (1 = has indexed portion)
            // Bit 3:      Pointer flag (1 = indexed portion contains pointers)
            // Bits 2-0:   Unused

            var instanceSpec = (classFormat >> 12) & 0x03;
            var fixedFields = (classFormat >> 6) & 0x3F;
            var isIndexable = (classFormat & 0x10) != 0;
            var hasPointers = (classFormat & 0x08) != 0;

            if (obj.FormatHint != null)
            {
                // Determine the base format type from instance specification
                var baseFormatType = instanceSpec switch
                {
                    0x00 => "none",      // No fields (UndefinedObject)
                    0x01 => "pointers",  // Pointer fields
                    0x02 => "words",     // Word-indexable
                    0x03 => "bytes",     // Byte-indexable
                    _ => "unknown"
                };

                // Validate formatHint is compatible with format word
                // The formatHint describes the content type, not the exact instance spec
                // "pointers" = contains object references (instanceSpec 01 or 02 with pointer flag)
                // "words" = contains raw words (instanceSpec 02 without pointer flag)
                // "bytes" = contains bytes (instanceSpec 03)
                // Special hints: "blockContext", "compiledMethod", "custom"
                bool isCompatible = obj.FormatHint switch
                {
                    "pointers" => (instanceSpec == 0x01) || (instanceSpec == 0x02 && hasPointers),
                    "words" => instanceSpec == 0x02 && !hasPointers,
                    "bytes" => instanceSpec == 0x03,
                    "blockContext" => instanceSpec == 0x02 && hasPointers && isIndexable,  // word-indexable with pointers
                    "compiledMethod" => instanceSpec == 0x02 && !hasPointers && isIndexable,  // word-indexable without pointers
                    "custom" => true,  // Custom hint is always allowed
                    _ => false
                };

                if (!isCompatible)
                {
                    result.AddWarning("ObjectFormat",
                        $"Object formatHint '{obj.FormatHint}' is incompatible with class format (instanceSpec={instanceSpec:X}, indexable={isIndexable}, pointers={hasPointers})",
                        obj.LocalId);
                }
            }

            // Validate fixed field count if provided
            // Note: We only check fixed fields, not indexed fields (those are in indexedFields array)
            if (obj.Fields != null)
            {
                var actualFieldCount = obj.Fields.Count;
                var expectedFieldCount = fixedFields;

                if (actualFieldCount != expectedFieldCount)
                {
                    result.AddWarning("ObjectFormat",
                        $"Object has {actualFieldCount} fixed fields but class format declares {expectedFieldCount}",
                        obj.LocalId);
                }
            }
        }

        result.AddInfo($"Validated {bundle.Objects.Length} objects");
        return result;
    }
}
