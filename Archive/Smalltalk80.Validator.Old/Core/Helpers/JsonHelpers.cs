using System.Text.Json;

namespace Smalltalk80.Validator.Core.Helpers;

public static class JsonHelpers
{
    public static string? GetStringProp(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
    }

    public static int? GetIntProp(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number
            ? prop.GetInt32()
            : null;
    }

    public static bool? GetBoolProp(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) &&
               (prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            ? prop.GetBoolean()
            : null;
    }

    public static JsonElement? GetObjectProp(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Object
            ? prop
            : null;
    }

    public static JsonElement? GetArrayProp(this JsonElement el, string name)
    {
        return el.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Array
            ? prop
            : null;
    }

    public static int[] GetIntArray(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<int>();

        var result = new int[arr.GetArrayLength()];
        int i = 0;
        foreach (var item in arr.EnumerateArray())
        {
            result[i++] = item.GetInt32();
        }
        return result;
    }

    public static string[] GetStringArray(this JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();

        var result = new string[arr.GetArrayLength()];
        int i = 0;
        foreach (var item in arr.EnumerateArray())
        {
            result[i++] = item.GetString() ?? string.Empty;
        }
        return result;
    }
}
