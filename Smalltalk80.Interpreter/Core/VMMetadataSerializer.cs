using System.Text.Json;
using Smalltalk80.Interpreter.Core.VM;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Serializes and deserializes VMMetadata and LayoutResolver to/from a companion file.
/// The metadata file contains OOP references, field indices, and class layouts needed to resume execution.
/// </summary>
public static class VMMetadataSerializer
{
    /// <summary>
    /// Saves VMMetadata and LayoutResolver to a companion file alongside the image.
    /// </summary>
    public static bool Save(VMMetadata metadata, LayoutResolver? layoutResolver, string imagePath)
    {
        try
        {
            string metaPath = GetMetadataPath(imagePath);

            var dto = new VMMetadataDto
            {
                // Field indices
                ClassNameFieldIndex = metadata.ClassNameFieldIndex,
                ClassSuperclassFieldIndex = metadata.ClassSuperclassFieldIndex,
                ClassMethodDictFieldIndex = metadata.ClassMethodDictFieldIndex,
                ClassFormatFieldIndex = metadata.ClassFormatFieldIndex,
                MetaclassThisClassFieldIndex = metadata.MetaclassThisClassFieldIndex,
                MethodDictTallyFieldIndex = metadata.MethodDictTallyFieldIndex,
                MethodDictKeysFieldIndex = metadata.MethodDictKeysFieldIndex,
                MethodDictValuesFieldIndex = metadata.MethodDictValuesFieldIndex,

                // Class OOPs (stored as raw tagged values)
                ClassClassOOP = metadata.ClassClassOOP.RawValue,
                MetaclassClassOOP = metadata.MetaclassClassOOP.RawValue,
                SymbolClassOOP = metadata.SymbolClassOOP.RawValue,
                StringClassOOP = metadata.StringClassOOP.RawValue,
                ArrayClassOOP = metadata.ArrayClassOOP.RawValue,
                ByteArrayClassOOP = metadata.ByteArrayClassOOP.RawValue,
                WordArrayClassOOP = metadata.WordArrayClassOOP.RawValue,
                DisplayBitmapClassOOP = metadata.DisplayBitmapClassOOP.RawValue,
                CompiledMethodClassOOP = metadata.CompiledMethodClassOOP.RawValue,
                SmallIntegerClassOOP = metadata.SmallIntegerClassOOP.RawValue,
                BlockContextClassOOP = metadata.BlockContextClassOOP.RawValue,
                OrderedCollectionClassOOP = metadata.OrderedCollectionClassOOP.RawValue,
                ProcessClassOOP = metadata.ProcessClassOOP.RawValue,
                PointClassOOP = metadata.PointClassOOP.RawValue,
                FloatClassOOP = metadata.FloatClassOOP.RawValue,
                CharacterClassOOP = metadata.CharacterClassOOP.RawValue,

                // Known objects
                NilOOP = metadata.KnownObjects.Nil.RawValue,
                TrueOOP = metadata.KnownObjects.True.RawValue,
                FalseOOP = metadata.KnownObjects.False.RawValue,
                SmalltalkOOP = metadata.KnownObjects.Smalltalk.RawValue,

                // Special objects
                ProcessorSchedulerOOP = metadata.ProcessorScheduler.RawValue,

                // Class layouts from LayoutResolver
                ClassLayouts = layoutResolver?.ExportLayouts() ?? new Dictionary<string, List<string>>()
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(dto, options);
            File.WriteAllText(metaPath, json);

            Console.WriteLine($"[VMMetadata] Saved to {metaPath}");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VMMetadata] Error saving: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads VMMetadata and LayoutResolver from a companion file.
    /// Returns a tuple of (VMMetadata, LayoutResolver) or (null, null) on failure.
    /// </summary>
    public static (VMMetadata? metadata, LayoutResolver? layoutResolver) Load(string imagePath)
    {
        try
        {
            string metaPath = GetMetadataPath(imagePath);

            if (!File.Exists(metaPath))
            {
                Console.WriteLine($"[VMMetadata] Metadata file not found: {metaPath}");
                return (null, null);
            }

            string json = File.ReadAllText(metaPath);
            var dto = JsonSerializer.Deserialize<VMMetadataDto>(json);

            if (dto == null)
            {
                Console.WriteLine("[VMMetadata] Failed to deserialize metadata");
                return (null, null);
            }

            var metadata = new VMMetadata
            {
                // Field indices
                ClassNameFieldIndex = dto.ClassNameFieldIndex,
                ClassSuperclassFieldIndex = dto.ClassSuperclassFieldIndex,
                ClassMethodDictFieldIndex = dto.ClassMethodDictFieldIndex,
                ClassFormatFieldIndex = dto.ClassFormatFieldIndex,
                MetaclassThisClassFieldIndex = dto.MetaclassThisClassFieldIndex,
                MethodDictTallyFieldIndex = dto.MethodDictTallyFieldIndex,
                MethodDictKeysFieldIndex = dto.MethodDictKeysFieldIndex,
                MethodDictValuesFieldIndex = dto.MethodDictValuesFieldIndex,

                // Class OOPs
                ClassClassOOP = new SmalltalkOOP(dto.ClassClassOOP),
                MetaclassClassOOP = new SmalltalkOOP(dto.MetaclassClassOOP),
                SymbolClassOOP = new SmalltalkOOP(dto.SymbolClassOOP),
                StringClassOOP = new SmalltalkOOP(dto.StringClassOOP),
                ArrayClassOOP = new SmalltalkOOP(dto.ArrayClassOOP),
                ByteArrayClassOOP = new SmalltalkOOP(dto.ByteArrayClassOOP),
                WordArrayClassOOP = new SmalltalkOOP(dto.WordArrayClassOOP),
                DisplayBitmapClassOOP = new SmalltalkOOP(dto.DisplayBitmapClassOOP),
                CompiledMethodClassOOP = new SmalltalkOOP(dto.CompiledMethodClassOOP),
                SmallIntegerClassOOP = new SmalltalkOOP(dto.SmallIntegerClassOOP),
                BlockContextClassOOP = new SmalltalkOOP(dto.BlockContextClassOOP),
                OrderedCollectionClassOOP = new SmalltalkOOP(dto.OrderedCollectionClassOOP),
                ProcessClassOOP = new SmalltalkOOP(dto.ProcessClassOOP),
                PointClassOOP = new SmalltalkOOP(dto.PointClassOOP),
                FloatClassOOP = new SmalltalkOOP(dto.FloatClassOOP),
                CharacterClassOOP = new SmalltalkOOP(dto.CharacterClassOOP),

                // Known objects
                KnownObjects = new KnownObjects
                {
                    Nil = new SmalltalkOOP(dto.NilOOP),
                    True = new SmalltalkOOP(dto.TrueOOP),
                    False = new SmalltalkOOP(dto.FalseOOP),
                    Smalltalk = new SmalltalkOOP(dto.SmalltalkOOP)
                },

                // Special objects
                ProcessorScheduler = new SmalltalkOOP(dto.ProcessorSchedulerOOP)
            };

            // Restore LayoutResolver
            var layoutResolver = new LayoutResolver();
            if (dto.ClassLayouts != null)
            {
                layoutResolver.ImportLayouts(dto.ClassLayouts);
            }
            layoutResolver.Freeze();

            Console.WriteLine($"[VMMetadata] Loaded from {metaPath} ({dto.ClassLayouts?.Count ?? 0} class layouts)");
            return (metadata, layoutResolver);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VMMetadata] Error loading: {ex.Message}");
            return (null, null);
        }
    }

    /// <summary>
    /// Gets the metadata file path for a given image path.
    /// </summary>
    public static string GetMetadataPath(string imagePath)
    {
        return Path.ChangeExtension(imagePath, ".meta");
    }
}

/// <summary>
/// Data transfer object for JSON serialization of VMMetadata.
/// </summary>
internal class VMMetadataDto
{
    // Field indices
    public int ClassNameFieldIndex { get; set; }
    public int ClassSuperclassFieldIndex { get; set; }
    public int ClassMethodDictFieldIndex { get; set; }
    public int ClassFormatFieldIndex { get; set; }
    public int MetaclassThisClassFieldIndex { get; set; }
    public int MethodDictTallyFieldIndex { get; set; }
    public int MethodDictKeysFieldIndex { get; set; }
    public int MethodDictValuesFieldIndex { get; set; }

    // Class OOPs (raw tagged values)
    public int ClassClassOOP { get; set; }
    public int MetaclassClassOOP { get; set; }
    public int SymbolClassOOP { get; set; }
    public int StringClassOOP { get; set; }
    public int ArrayClassOOP { get; set; }
    public int ByteArrayClassOOP { get; set; }
    public int WordArrayClassOOP { get; set; }
    public int DisplayBitmapClassOOP { get; set; }
    public int CompiledMethodClassOOP { get; set; }
    public int SmallIntegerClassOOP { get; set; }
    public int BlockContextClassOOP { get; set; }
    public int OrderedCollectionClassOOP { get; set; }
    public int ProcessClassOOP { get; set; }
    public int PointClassOOP { get; set; }
    public int FloatClassOOP { get; set; }
    public int CharacterClassOOP { get; set; }

    // Known objects
    public int NilOOP { get; set; }
    public int TrueOOP { get; set; }
    public int FalseOOP { get; set; }
    public int SmalltalkOOP { get; set; }

    // Special objects
    public int ProcessorSchedulerOOP { get; set; }

    // Class layouts (for LayoutResolver)
    public Dictionary<string, List<string>>? ClassLayouts { get; set; }
}
