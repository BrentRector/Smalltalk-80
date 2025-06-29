
public static class StandardLibrary {
    private static Dictionary<string, SmalltalkObject> globals = new();

    public static void Initialize() {
        var objectClass = new SmalltalkClass();
        objectClass.MethodDictionary[new Symbol("new")] = new CompiledMethod {
            Bytecodes = new byte[] { 124 },
            Literals = new object[] {},
            TemporariesCount = 0,
            HasPrimitive = true,
            PrimitiveIndex = 3
        };

        globals["Object"] = new SmalltalkObject(objectClass, 0);
    }

    public static SmalltalkObject Get(string name) => globals[name];
}
