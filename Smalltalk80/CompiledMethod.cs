using System;

public class CompiledMethod {
    public byte[] Bytecodes;
    public Object[] Literals;
    public int TemporariesCount;
    public bool HasPrimitive;
    public int PrimitiveIndex;
}