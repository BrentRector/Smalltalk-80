using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

/// <summary>
///     Provides serialization support for saving and loading object graphs.
/// </summary>
public static class ObjectImage
{
    /// <summary>
    ///     Saves the given objects to the specified file path as JSON.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="objects">The list of objects to persist.</param>
    public static void Save(string path, List<SmalltalkObject> objects)
    {
        var json = JsonSerializer.Serialize(objects, new JsonSerializerOptions {
            WriteIndented = true,
            IncludeFields = true
        });
        File.WriteAllText(path, json);
    }

    /// <summary>
    ///     Loads a list of objects from the specified file.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <returns>The deserialized objects.</returns>
    public static List<SmalltalkObject> Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<SmalltalkObject>>(json, new JsonSerializerOptions {
            IncludeFields = true
        }) ?? new List<SmalltalkObject>();
    }
}