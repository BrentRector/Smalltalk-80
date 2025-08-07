using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
///     Provides the ability to load an old-style Smalltalk image file.
/// </summary>
public static class LegacyImageLoader
{
    /// <summary>
    ///     Loads the image located at the specified path.
    /// </summary>
    /// <param name="path">The image file to read.</param>
    /// <returns>A list of deserialized <see cref="SmalltalkObject"/> instances.</returns>
    public static List<SmalltalkObject> LoadImage(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var stream = new MemoryStream(bytes);
        var reader = new BinaryReader(stream);

        int version = reader.ReadInt32();
        int objectCount = reader.ReadInt32();
        int specialObjectsOffset = reader.ReadInt32();

        var objectTable = new List<SmalltalkObject>();
        var forwardPointers = new List<int>();

        for (int i = 0; i < objectCount; i++) {
            int header = reader.ReadInt32();
            int format = (header >> 8) & 0xFF;
            int classIndex = reader.ReadInt32();
            int size = header & 0xFF;

            var obj = new SmalltalkObject(null, size);
            objectTable.Add(obj);
            forwardPointers.Add(classIndex);

            for (int j = 0; j < size; j++) {
                int raw = reader.ReadInt32();
                obj.Fields[j] = DecodePointer(raw, objectTable);
            }
        }

        for (int i = 0; i < objectTable.Count; i++) {
            int classRefIndex = forwardPointers[i];
            if (classRefIndex >= 0 && classRefIndex < objectTable.Count) {
                var candidate = objectTable[classRefIndex] as SmalltalkClass;
                if (candidate != null) objectTable[i].ClassPointer = candidate;
            }
        }

        return objectTable;
    }

    /// <summary>
    ///     Resolves a raw pointer value from the image into an object reference.
    /// </summary>
    /// <param name="raw">The encoded pointer.</param>
    /// <param name="objectTable">The table of objects being decoded.</param>
    /// <returns>The referenced object or <c>null</c> if the pointer is nil.</returns>
    private static object? DecodePointer(int raw, List<SmalltalkObject> objectTable)
    {
        if ((raw & 1) == 1) return raw >> 1;
        if (raw == 0) return null;
        int index = raw >> 1;
        return index < objectTable.Count ? objectTable[index] : null;
    }
}