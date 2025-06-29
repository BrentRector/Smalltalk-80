public class NonLocalReturnException : Exception {
    public Object ReturnValue;
    public Context TargetContext;

    public NonLocalReturnException(Object value, Context target) {
        ReturnValue = value;
        TargetContext = target;
    }
}