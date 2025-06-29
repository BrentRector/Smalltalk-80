using System.Collections.Generic;

public class SmalltalkClass : SmalltalkObject {
    public Dictionary<Symbol, CompiledMethod> MethodDictionary = new();
    public SmalltalkClass? Superclass;

    public SmalltalkClass() : base(null!, 0) { }

    public CompiledMethod? Lookup(Symbol selector) {
        if (MethodDictionary.TryGetValue(selector, out var method)) return method;
        return Superclass?.Lookup(selector);
    }
}