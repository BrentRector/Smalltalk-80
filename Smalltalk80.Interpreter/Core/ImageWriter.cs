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

            // Track size statistics
            long totalInstanceVarBytes = 0;
            long totalIndexedBytes = 0;
            var largestObjects = new List<(SmalltalkOOP oop, string className, int indexedSize, ObjectFormat format)>();

            using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            // Write header
            WriteHeader(writer, objectCount);

            // Write each object
            int written = 0;
            foreach (var oop in objects)
            {
                // Calculate sizes for diagnostics
                int instanceVarCount = _heap.GetInstanceVariableCount(oop);
                int indexedSize = _heap.GetIndexedSize(oop);
                var format = _heap.GetObjectFormat(oop);

                totalInstanceVarBytes += instanceVarCount * 4;
                int indexedBytes = format switch
                {
                    ObjectFormat.PointerIndexed => indexedSize * 4,
                    ObjectFormat.ByteIndexed => indexedSize,
                    ObjectFormat.WordIndexed => indexedSize * 2,
                    _ => 0
                };
                totalIndexedBytes += indexedBytes;

                // Track large objects (>10KB indexed data)
                if (indexedBytes > 10000)
                {
                    string className = _heap.GetClassName(oop);
                    largestObjects.Add((oop, className, indexedSize, format));
                }

                WriteObject(writer, oop);
                written++;

                // Progress every 10000 objects
                if (written % 10000 == 0)
                {
                    Console.WriteLine($"[ImageWriter] Progress: {written}/{objectCount}");
                }
            }

            // Report statistics
            Console.WriteLine($"[ImageWriter] Size breakdown:");
            Console.WriteLine($"  Instance variables: {totalInstanceVarBytes:N0} bytes");
            Console.WriteLine($"  Indexed data: {totalIndexedBytes:N0} bytes");
            Console.WriteLine($"  Object headers: {objectCount * 17:N0} bytes (17 bytes each)");
            Console.WriteLine($"  Large objects (>10KB indexed): {largestObjects.Count}");

            // Report top 10 largest objects
            if (largestObjects.Count > 0)
            {
                Console.WriteLine($"[ImageWriter] Top largest objects:");
                foreach (var (oop, className, indexedSize, format) in largestObjects.OrderByDescending(x => x.indexedSize).Take(10))
                {
                    int bytes = format switch
                    {
                        ObjectFormat.PointerIndexed => indexedSize * 4,
                        ObjectFormat.ByteIndexed => indexedSize,
                        ObjectFormat.WordIndexed => indexedSize * 2,
                        _ => 0
                    };

                    // Show content preview for large ByteIndexed objects (String, Symbol, ByteArray)
                    string preview = "";
                    if (format == ObjectFormat.ByteIndexed && indexedSize > 0)
                    {
                        int previewLen = Math.Min(100, indexedSize);
                        var previewBytes = new byte[previewLen];
                        int nonZeroCount = 0;
                        int firstNonZero = -1;
                        for (int i = 0; i < previewLen; i++)
                        {
                            previewBytes[i] = _heap.GetIndexedByte(oop, i);
                            if (previewBytes[i] != 0)
                            {
                                nonZeroCount++;
                                if (firstNonZero < 0) firstNonZero = i;
                            }
                        }

                        // Check if it's all zeros
                        if (nonZeroCount == 0)
                        {
                            // Sample deeper into the object to check if it's truly all zeros
                            int sampleCount = 0;
                            for (int i = 0; i < Math.Min(1000, indexedSize); i += 100)
                            {
                                if (_heap.GetIndexedByte(oop, i) != 0) sampleCount++;
                            }
                            preview = sampleCount == 0
                                ? " [ALL ZEROS - uninitialized buffer?]"
                                : $" [First 100 bytes zero, but has content at deeper offsets]";
                        }
                        else
                        {
                            string text = Encoding.Latin1.GetString(previewBytes).Replace("\n", "\\n").Replace("\r", "\\r");
                            preview = $" Preview: \"{text}\"...";
                        }
                    }

                    Console.WriteLine($"    {className} (oop={oop}): {bytes:N0} bytes ({format}, indexedSize={indexedSize}){preview}");
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
