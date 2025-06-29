public static class LegacyImageLoader {
    public static List<SmalltalkObject> LoadImage(string path) {
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
                objectTable[i].ClassPointer = objectTable[classRefIndex] as SmalltalkClass;
            }
        }

        return objectTable;
    }

    private static object DecodePointer(int raw, List<SmalltalkObject> objectTable) {
        if ((raw & 1) == 1) return raw >> 1;
        if (raw == 0) return null;
        int index = raw >> 1;
        return index < objectTable.Count ? objectTable[index] : null;
    }
}