public static class SourceCompiler {
    public static CompiledMethod Compile(string source) {
        var tokenizer = new SmalltalkTokenizer(source);
        var parser = new SmalltalkParser(tokenizer);
        return parser.ParseMethod();
    }
}