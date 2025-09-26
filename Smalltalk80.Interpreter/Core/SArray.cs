using System;
using System.Text;

namespace Smalltalk80.Interpreter.Core
{
    /// <summary>
    /// Smalltalk array - a variable-sized collection of object references.
    /// </summary>
    public class SArray : SObject
    {
        /// <summary>
        /// The indexed elements of this array.
        /// </summary>
        public object[] Elements { get; set; }

        /// <summary>
        /// Creates a new array with the specified size.
        /// </summary>
        public SArray(SClass arrayClass, int size) : base(arrayClass, 0)
        {
            Elements = new object[size];
            // Initialize all elements to nil
            for (int i = 0; i < size; i++)
            {
                Elements[i] = Nil.Instance;
            }
        }

        /// <summary>
        /// Gets or sets an element at the specified index (1-based for Smalltalk).
        /// </summary>
        public object At(int index)
        {
            if (index < 1 || index > Elements.Length)
                throw new IndexOutOfRangeException($"Index {index} out of range 1..{Elements.Length}");
            return Elements[index - 1];
        }

        /// <summary>
        /// Sets an element at the specified index (1-based for Smalltalk).
        /// </summary>
        public void AtPut(int index, object value)
        {
            if (index < 1 || index > Elements.Length)
                throw new IndexOutOfRangeException($"Index {index} out of range 1..{Elements.Length}");
            Elements[index - 1] = value ?? Nil.Instance;
        }

        /// <summary>
        /// Gets the size of this array.
        /// </summary>
        public int Size => Elements.Length;

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            if (Elements.Length == 0)
                return "#()";
            
            var sb = new StringBuilder("#(");
            for (int i = 0; i < Math.Min(Elements.Length, 10); i++)
            {
                if (i > 0) sb.Append(' ');
                var element = Elements[i];
                if (element == null || element == Nil.Instance)
                    sb.Append("nil");
                else
                    sb.Append(element.ToString());
            }
            
            if (Elements.Length > 10)
                sb.Append(" ...");
            
            sb.Append(')');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Smalltalk byte array - a variable-sized collection of bytes.
    /// </summary>
    public class SByteArray : SObject
    {
        /// <summary>
        /// The byte data of this array.
        /// </summary>
        public byte[] Bytes { get; set; }

        /// <summary>
        /// Creates a new byte array with the specified size.
        /// </summary>
        public SByteArray(SClass byteArrayClass, int size) : base(byteArrayClass, 0)
        {
            Bytes = new byte[size];
        }

        /// <summary>
        /// Gets a byte at the specified index (1-based for Smalltalk).
        /// </summary>
        public byte At(int index)
        {
            if (index < 1 || index > Bytes.Length)
                throw new IndexOutOfRangeException($"Index {index} out of range 1..{Bytes.Length}");
            return Bytes[index - 1];
        }

        /// <summary>
        /// Sets a byte at the specified index (1-based for Smalltalk).
        /// </summary>
        public void AtPut(int index, byte value)
        {
            if (index < 1 || index > Bytes.Length)
                throw new IndexOutOfRangeException($"Index {index} out of range 1..{Bytes.Length}");
            Bytes[index - 1] = value;
        }

        /// <summary>
        /// Gets the size of this byte array.
        /// </summary>
        public int Size => Bytes.Length;

        /// <summary>
        /// Returns a string representation for debugging.
        /// </summary>
        public override string ToString()
        {
            if (Bytes.Length == 0)
                return "#[]";
            
            var sb = new StringBuilder("#[");
            for (int i = 0; i < Math.Min(Bytes.Length, 20); i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(Bytes[i]);
            }
            
            if (Bytes.Length > 20)
                sb.Append(" ...");
            
            sb.Append(']');
            return sb.ToString();
        }
    }

    /// <summary>
    /// Smalltalk string - a byte array interpreted as text.
    /// </summary>
    public class SString : SByteArray
    {
        /// <summary>
        /// Creates a new string with the specified size.
        /// </summary>
        public SString(SClass stringClass, int size) : base(stringClass, size)
        {
        }

        /// <summary>
        /// Creates a string from a C# string.
        /// </summary>
        public static SString FromString(SClass stringClass, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            var str = new SString(stringClass, bytes.Length);
            str.Bytes = bytes;
            return str;
        }

        /// <summary>
        /// Converts this Smalltalk string to a C# string.
        /// </summary>
        public string AsString()
        {
            return Encoding.UTF8.GetString(Bytes);
        }

        /// <summary>
        /// Returns a string representation.
        /// </summary>
        public override string ToString()
        {
            return $"'{AsString()}'";
        }
    }
}