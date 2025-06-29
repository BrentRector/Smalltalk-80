using System;
using System.Collections.Generic;

public static class StandardLibrary {
    private static Dictionary<string, SmalltalkObject> globals = new();

    public static void Initialize() {
        var objectClass = new SmalltalkClass();
        globals["Object"] = new SmalltalkObject(objectClass, 0);
    }

    public static SmalltalkObject Get(string name) => globals[name];
}