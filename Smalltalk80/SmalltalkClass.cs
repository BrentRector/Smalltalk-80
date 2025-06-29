public class SmalltalkClass {
    public Dictionary<Symbol, CompiledMethod> MethodDictionary = new();
    public SmalltalkClass Superclass;

    public CompiledMethod Lookup(Symbol selector) {
        if (MethodDictionary.TryGetValue(selector, out var method)) return method;
        return Superclass?.Lookup(selector);
    }
}