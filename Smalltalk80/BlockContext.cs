public class BlockContext : Context {
    public Context HomeContext;
    public int StartPC;
    public int ArgumentCount;

    public static BlockContext Create(Context homeContext, int startPC, int argCount, Object[] args) {
        return new BlockContext {
            HomeContext = homeContext,
            StartPC = startPC,
            Sender = null,
            Method = homeContext.Method,
            Receiver = homeContext.Receiver,
            Arguments = args,
            ArgumentCount = argCount,
            Temporaries = new Object[homeContext.Method.TemporariesCount],
            Stack = new Stack<Object>(),
            PC = startPC
        };
    }
}