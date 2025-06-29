using System;

public class SmalltalkObject {
    public SmalltalkClass ClassPointer;
    public object[] Fields;

    public SmalltalkObject(SmalltalkClass cls, int fieldCount) {
        ClassPointer = cls;
        Fields = new object[fieldCount];
    }

    public object GetField(int index) => Fields[index];
    public void SetField(int index, object value) => Fields[index] = value;
}