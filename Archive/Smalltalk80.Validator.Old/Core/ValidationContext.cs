using System.Text.Json;

namespace Smalltalk80.Validator.Core;

public record EntityRef(string Section, JsonElement Item);

public sealed class ValidationContext
{
    public JsonDocument Bundle { get; }
    public IReadOnlyList<string> Symbols { get; init; }
    public IReadOnlyList<JsonElement> Classes { get; init; }
    public IReadOnlyList<JsonElement> Methods { get; init; }
    public IReadOnlyList<JsonElement> Objects { get; init; }
    public IReadOnlyList<JsonElement> InitExpressions { get; init; }
    public IReadOnlyList<JsonElement> Patches { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<EntityRef>> IndexByLocalId { get; init; }

    private ValidationContext(
        JsonDocument bundle,
        IReadOnlyList<string> symbols,
        IReadOnlyList<JsonElement> classes,
        IReadOnlyList<JsonElement> methods,
        IReadOnlyList<JsonElement> objects,
        IReadOnlyList<JsonElement> initExpressions,
        IReadOnlyList<JsonElement> patches,
        IReadOnlyDictionary<string, IReadOnlyList<EntityRef>> indexByLocalId)
    {
        Bundle = bundle;
        Symbols = symbols;
        Classes = classes;
        Methods = methods;
        Objects = objects;
        InitExpressions = initExpressions;
        Patches = patches;
        IndexByLocalId = indexByLocalId;
    }

    public static ValidationContext Build(JsonDocument doc)
    {
        var root = doc.RootElement;
        return new ValidationContext(
            doc,
            ReadStringArray(root, "symbols"),
            ReadArray(root, "classes"),
            ReadArray(root, "methods"),
            ReadArray(root, "objects"),
            ReadArray(root, "initExpressions"),
            ReadArray(root, "patches"),
            BuildIndexByLocalId(root)
        );
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return Array.Empty<string>();
        var list = new List<string>(a.GetArrayLength());
        foreach (var el in a.EnumerateArray()) list.Add(el.GetString() ?? string.Empty);
        return list;
    }

    private static IReadOnlyList<JsonElement> ReadArray(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var a) || a.ValueKind != JsonValueKind.Array) return Array.Empty<JsonElement>();
        var list = new List<JsonElement>(a.GetArrayLength());
        foreach (var el in a.EnumerateArray()) list.Add(el);
        return list;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<EntityRef>> BuildIndexByLocalId(JsonElement root)
    {
        var map = new Dictionary<string, List<EntityRef>>();
        void AddFromArray(string section)
        {
            if (!root.TryGetProperty(section, out var arr) || arr.ValueKind != JsonValueKind.Array) return;
            foreach (var el in arr.EnumerateArray())
            {
                if (!el.TryGetProperty("localId", out var lidProp) || lidProp.ValueKind != JsonValueKind.String) continue;
                var lid = lidProp.GetString()!;
                if (!map.TryGetValue(lid, out var list)) { list = []; map[lid] = list; }
                list.Add(new EntityRef(section, el));
            }
        }
        AddFromArray("classes");
        AddFromArray("methods");
        AddFromArray("objects");
        AddFromArray("initExpressions");
        AddFromArray("patches");
        var readonlyMap = new Dictionary<string, IReadOnlyList<EntityRef>>(map.Count);
        foreach (var kv in map) readonlyMap[kv.Key] = kv.Value.AsReadOnly();
        return readonlyMap;
    }
}
