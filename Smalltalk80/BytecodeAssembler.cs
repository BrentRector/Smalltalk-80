using System;
using System.Collections.Generic;

public class BytecodeAssembler {
    public static byte[] Assemble(string[] lines) {
        var list = new List<byte>();
        foreach (var line in lines) {
            switch (line.Trim()) {
                case "push_temp_0": list.Add(16); break;
                case "push_temp_1": list.Add(17); break;
                case "send_+": list.Add(128); break;
                case "return": list.Add(124); break;
                default: throw new Exception($"Unknown bytecode mnemonic: {line}");
            }
        }
        return list.ToArray();
    }
}