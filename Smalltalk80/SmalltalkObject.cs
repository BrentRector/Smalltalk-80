public class SmalltalkObject {
    public SmalltalkClass ClassPointer;
    public Object[] Fields;

    public SmalltalkObject(SmalltalkClass cls, int fieldCount) {
        ClassPointer = cls;
        Fields = new Object[fieldCount];
    }

    public Object GetField(int index) => Fields[index];
    public void SetField(int index, Object value) => Fields[index] = value;

    public static bool IsSmallInteger(Object obj) => obj is int;
    public static int GetSmallIntegerValue(Object obj) => (int)obj;
    public static Object EncodeSmallInteger(int value) => value;
}