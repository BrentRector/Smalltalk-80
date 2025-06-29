using System;

public class Tracer {
    public static bool Enabled = true;
    public static void Log(string message) {
        if (Enabled) Console.WriteLine("[TRACE] " + message);
    }
}