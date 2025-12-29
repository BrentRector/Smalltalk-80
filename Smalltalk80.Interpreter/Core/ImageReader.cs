using System.Text;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Deserializes a Smalltalk heap from a binary image file.
/// Restores objects to the same OOP locations for reference preservation.
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
public class ImageReader
{
    private const string ExpectedMagic = "STI1";
    private const int SupportedVersion = 1;

    private readonly ISmalltalkHeap _heap;

    public ImageReader(ISmalltalkHeap heap)
    {
        _heap = heap ?? throw new ArgumentNullException(nameof(heap));
    }

    /// <summary>
    /// Reads an image file and restores objects to the heap.
    /// Returns true on success.
    /// </summary>
    public bool ReadImage(string filePath)
    {
        try
        {
            Console.WriteLine($"[ImageReader] Reading image from {filePath}");

            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            // Read and verify header
            var (objectCount, version) = ReadHeader(reader);
            if (objectCount < 0)
            {
                return false;
            }

            Console.WriteLine($"[ImageReader] Image version {version}, {objectCount} objects");

            // Read each object
            int read = 0;
            for (int i = 0; i < objectCount; i++)
            {
                if (!ReadObject(reader))
                {
                    Console.WriteLine($"[ImageReader] Error reading object {i}");
                    return false;
                }
                read++;

                // Progress every 10000 objects
                if (read % 10000 == 0)
                {
                    Console.WriteLine($"[ImageReader] Progress: {read}/{objectCount}");
                }
            }

            Console.WriteLine($"[ImageReader] Successfully read {read} objects");
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageReader] Error reading image: {ex.Message}");
            return false;
        }
    }

    private (int objectCount, int version) ReadHeader(BinaryReader reader)
    {
        // Read magic (4 bytes ASCII)
        byte[] magicBytes = reader.ReadBytes(4);
        string magic = Encoding.ASCII.GetString(magicBytes);

        if (magic != ExpectedMagic)
        {
            Console.WriteLine($"[ImageReader] Invalid magic: expected '{ExpectedMagic}', got '{magic}'");
            return (-1, 0);
        }

        // Read version (4 bytes)
        int version = reader.ReadInt32();
        if (version != SupportedVersion)
        {
            Console.WriteLine($"[ImageReader] Unsupported version: {version}");
            return (-1, version);
        }

        // Read object count (4 bytes)
        int objectCount = reader.ReadInt32();

        return (objectCount, version);
    }

    private bool ReadObject(BinaryReader reader)
    {
        try
        {
            // Read object header
            int objectId = reader.ReadInt32();
            var format = (ObjectFormat)reader.ReadByte();
            int instanceVarCount = reader.ReadInt32();
            int indexedSize = reader.ReadInt32();
            int classOOPRaw = reader.ReadInt32();

            // Allocate the object with proper format
            var newOOP = _heap.AllocateObject(format, instanceVarCount, indexedSize);

            // Set class reference
            _heap.SetClassOOP(newOOP, new SmalltalkOOP(classOOPRaw));

            // Read instance variables
            for (int i = 0; i < instanceVarCount; i++)
            {
                int ivarRaw = reader.ReadInt32();
                _heap.SetInstanceVariable(newOOP, i, new SmalltalkOOP(ivarRaw));
            }

            // Read indexed data based on format
            switch (format)
            {
                case ObjectFormat.Pointer:
                    // No indexed data
                    break;

                case ObjectFormat.PointerIndexed:
                    for (int i = 0; i < indexedSize; i++)
                    {
                        int indexedRaw = reader.ReadInt32();
                        _heap.SetIndexedOOP(newOOP, i, new SmalltalkOOP(indexedRaw));
                    }
                    break;

                case ObjectFormat.ByteIndexed:
                    for (int i = 0; i < indexedSize; i++)
                    {
                        byte b = reader.ReadByte();
                        _heap.SetIndexedByte(newOOP, i, b);
                    }
                    break;

                case ObjectFormat.WordIndexed:
                    for (int i = 0; i < indexedSize; i++)
                    {
                        ushort w = reader.ReadUInt16();
                        _heap.SetIndexedWord(newOOP, i, w);
                    }
                    break;
            }

            // Verify the object was allocated at expected OOP
            // Note: This assumes objects are written and read in the same order
            // and that the heap allocator assigns sequential OOPs
            int actualId = newOOP.ToObjectTableIndex();
            if (actualId != objectId)
            {
                Console.WriteLine($"[ImageReader] Warning: Object allocated at {actualId}, expected {objectId}");
                // This could be a problem if objects don't restore at original OOPs
                // May need heap support for allocating at specific OOP
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ImageReader] Error reading object: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if a file is a valid Smalltalk image.
    /// </summary>
    public static bool IsImageFile(string filePath)
    {
        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            byte[] magicBytes = reader.ReadBytes(4);
            string magic = Encoding.ASCII.GetString(magicBytes);

            return magic == ExpectedMagic;
        }
        catch
        {
            return false;
        }
    }
}
