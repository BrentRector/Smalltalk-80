using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class ObjectImage {
    public static void Save(string path, List<SmalltalkObject> objects) {
        var json = JsonSerializer.Serialize(objects, new JsonSerializerOptions {
            WriteIndented = true,
            IncludeFields = true
        });
        File.WriteAllText(path, json);
    }

    public static List<SmalltalkObject> Load(string path) {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<List<SmalltalkObject>>(json, new JsonSerializerOptions {
            IncludeFields = true
        }) ?? new List<SmalltalkObject>();
    }
}