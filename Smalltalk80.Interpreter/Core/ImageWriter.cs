using System.Text;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Serializes the Smalltalk heap to a binary image file.
/// Uses .NET BinaryWriter for efficient binary I/O.
///
/// Image Format (version 1):
/// - Header: 4-byte magic "STI1", 4-byte version, 4-byte object count
/// - For each object (in OOP order):
///   - 4-byte object table index
///   - 1-byte format (ObjectFormat enum)
///   - 4-byte instance variable count
///   - 4-byte indexed size
///   - 4-byte class OOP raw value
///   - Instance variable data: 4 bytes per OOP
///   - Indexed data: format-specific (OOPs, bytes, or words)
/// </summary>
public class ImageWriter
{
    private const string Magic = "STI1";
    private const int Version = 1;

    private readonly ISmalltalkHeap _heap;

    /// <summary>
    /// Gets or sets the path to the snapshot file.
    /// Set by primitive 156 when beSnapshotFile is called.
    /// </summary>
    public static string? SnapshotFilePath { get; set; }

    public ImageWriter(ISmalltalkHeap heap)
    {
        _heap = heap ?? throw new ArgumentNullException(nameof(heap));
    }

    /// <summary>
    /// Writes the entire heap to the specified file.
    /// </summary>
    public bool WriteImage(string filePath)
    {
        try
        {
            // Collect all objects first to get count
            var objects = _heap.EnumerateObjectTable().ToList();
            int objectCount = objects.Count;

            Console.WriteLine($"[ImageWriter] Writing {objectCount} objects to {filePath}");

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            // Write header
            WriteHeader(writer, objectCount);

            // Write each object
            int written = 0;
            foreach (var oop in objects)
            {
                WriteObject(writer, oop);
                written++;

                // Progress every 10000 objects
                if (written % 10000 == 0)
                {
                    Console.WriteLine($"[ImageWriter] Progress: {written}/{objectCount}");
                }
            }

            Console.WriteLine($"[ImageWriter] Successfully wrote {written} objects ({stream.Position} bytes)");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageWriter] Error writing image: {ex.Message}");
            return false;
        }
    }

    private void WriteHeader(BinaryWriter writer, int objectCount)
    {
        // Magic number (4 bytes ASCII)
        writer.Write(Encoding.ASCII.GetBytes(Magic));

        // Version (4 bytes)
        writer.Write(Version);

        // Object count (4 bytes)
        writer.Write(objectCount);
    }

    private void WriteObject(BinaryWriter writer, SmalltalkOOP oop)
    {
        // Get object metadata
        int objectId = oop.ToObjectTableIndex();
        var format = _heap.GetObjectFormat(oop);
        int instanceVarCount = _heap.GetInstanceVariableCount(oop);
        int indexedSize = _heap.GetIndexedSize(oop);
        var classOOP = _heap.GetClassOOP(oop);

        // Write object header
        writer.Write(objectId);                     // 4 bytes: object table index
        writer.Write((byte)format);                 // 1 byte: format
        writer.Write(instanceVarCount);             // 4 bytes: instance var count
        writer.Write(indexedSize);                  // 4 bytes: indexed size
        writer.Write(classOOP.RawValue);            // 4 bytes: class OOP

        // Write instance variables (always OOPs)
        for (int i = 0; i < instanceVarCount; i++)
        {
            var ivarOOP = _heap.GetInstanceVariable(oop, i);
            writer.Write(ivarOOP.RawValue);
        }

        // Write indexed data based on format
        switch (format)
        {
            case ObjectFormat.Pointer:
                // No indexed data for pure pointer format
                break;

            case ObjectFormat.PointerIndexed:
                // Indexed portion contains OOPs
                for (int i = 0; i < indexedSize; i++)
                {
                    var indexedOOP = _heap.GetIndexedOOP(oop, i);
                    writer.Write(indexedOOP.RawValue);
                }
                break;

            case ObjectFormat.ByteIndexed:
                // Indexed portion contains bytes
                for (int i = 0; i < indexedSize; i++)
                {
                    byte b = _heap.GetIndexedByte(oop, i);
                    writer.Write(b);
                }
                break;

            case ObjectFormat.WordIndexed:
                // Indexed portion contains 16-bit words
                for (int i = 0; i < indexedSize; i++)
                {
                    ushort w = _heap.GetIndexedWord(oop, i);
                    writer.Write(w);
                }
                break;
        }
    }
}
