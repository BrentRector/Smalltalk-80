using System;

public class Primitives {
    public static bool Execute(int index, Context context) {
        switch (index) {
            case 1: return PrimitiveAdd(context);
            case 2: return PrimitiveSubtract(context);
            default: return false;
        }
    }

    private static bool PrimitiveAdd(Context ctx) {
        Object b = ctx.Stack.Pop();
        Object a = ctx.Stack.Pop();
        if (!(a is int) || !(b is int)) return false;
        ctx.Stack.Push((int)a + (int)b);
        return true;
    }

    private static bool PrimitiveSubtract(Context ctx) {
        Object b = ctx.Stack.Pop();
        Object a = ctx.Stack.Pop();
        if (!(a is int) || !(b is int)) return false;
        ctx.Stack.Push((int)a - (int)b);
        return true;
    }
}