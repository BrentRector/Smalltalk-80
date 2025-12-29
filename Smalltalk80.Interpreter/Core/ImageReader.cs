using System.Text;
using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core;

/// <summary>
/// Deserializes a Smalltalk heap from a binary image file.
/// Restores objects to the same OOP locations for reference preservation.
///
/// Image Format (version 1):
/// - Header (16 bytes):
///   - 4-byte magic "WOST" (Wise Owl Smalltalk)
///   - 4-byte version
///   - 4-byte object count
///   - 4-byte max object ID (highest object table index)
/// - For each object (in OOP order):
///   - 4-byte ObjectOOP (tagged value: objectTableIndex * 2)
///   - 1-byte format (ObjectFormat enum)
///   - 4-byte instance variable count
///   - 4-byte indexed size
///   - 4-byte class OOP (tagged value)
///   - Instance variable data: 4 bytes per OOP (tagged values)
///   - Indexed data: format-specific (OOPs as tagged, bytes, or words)
/// </summary>
public class ImageReader
{
    /// <summary>Expected magic number identifying Wise Owl Smalltalk image files.</summary>
    public const string ExpectedMagic = "WOST";

    /// <summary>Supported image format version.</summary>
    public const int SupportedVersion = 1;

    private readonly ISmalltalkHeap _heap;

    public ImageReader(ISmalltalkHeap heap)
    {
        _heap = heap ?? throw new ArgumentNullException(nameof(heap));
    }

    /// <summary>
    /// Reads an image file and restores objects to the heap.
    /// Returns a detailed result indicating success or failure with user-friendly messages.
    /// </summary>
    public ImageRestoreResult ReadImage(string filePath)
    {
        // Check if file exists
        if (!File.Exists(filePath))
        {
            return ImageRestoreResult.FileNotFound(filePath);
        }

        FileStream? stream = null;
        BinaryReader? reader = null;
        int objectsRead = 0;

        try
        {
            // Open file
            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                reader = new BinaryReader(stream, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return ImageRestoreResult.FileAccessError(filePath, ex);
            }

            // Read and validate header
            var headerResult = ReadAndValidateHeader(reader, filePath);
            if (!headerResult.Success)
            {
                return headerResult.Error!;
            }

            int objectCount = headerResult.ObjectCount;
            int maxObjectId = headerResult.MaxObjectId;

            Console.WriteLine($"[ImageReader] Image version {SupportedVersion}, {objectCount} objects, maxObjectId={maxObjectId}");

            // Prepare heap for restore
            _heap.PrepareForRestore(maxObjectId);

            // Read each object
            for (int i = 0; i < objectCount; i++)
            {
                var loadResult = LoadObject(reader, filePath, i);
                if (!loadResult.Success)
                {
                    return loadResult.Result!;
                }

                objectsRead++;

                // Progress every 10000 objects
                if (objectsRead % 10000 == 0)
                {
                    Console.WriteLine($"[ImageReader] Progress: {objectsRead}/{objectCount}");
                }
            }

            // Rebuild free list for future allocations
            _heap.RebuildFreeList();

            Console.WriteLine($"[ImageReader] Successfully restored {objectsRead} objects");
            return ImageRestoreResult.Succeeded(objectsRead, maxObjectId);
        }
        catch (EndOfStreamException)
        {
            return ImageRestoreResult.TruncatedFile(filePath, objectsRead, 0);
        }
        catch (Exception ex)
        {
            return ImageRestoreResult.UnexpectedError(filePath, ex, objectsRead);
        }
        finally
        {
            reader?.Dispose();
            stream?.Dispose();
        }
    }

    /// <summary>
    /// Reads and validates the image header.
    /// </summary>
    private HeaderReadResult ReadAndValidateHeader(BinaryReader reader, string filePath)
    {
        try
        {
            // Read magic (4 bytes ASCII)
            if (reader.BaseStream.Length < 16)
            {
                return new HeaderReadResult(ImageRestoreResult.TruncatedFile(filePath, 0, 0));
            }

            byte[] magicBytes = reader.ReadBytes(4);
            string magic = Encoding.ASCII.GetString(magicBytes);

            if (magic != ExpectedMagic)
            {
                return new HeaderReadResult(ImageRestoreResult.InvalidFormat(filePath, magic));
            }

            // Read version (4 bytes)
            int version = reader.ReadInt32();
            if (version != SupportedVersion)
            {
                return new HeaderReadResult(ImageRestoreResult.UnsupportedVersion(filePath, version, SupportedVersion));
            }

            // Read object count (4 bytes)
            int objectCount = reader.ReadInt32();

            // Read max object ID (4 bytes)
            int maxObjectId = reader.ReadInt32();

            return new HeaderReadResult(objectCount, maxObjectId);
        }
        catch (EndOfStreamException)
        {
            return new HeaderReadResult(ImageRestoreResult.TruncatedFile(filePath, 0, 0));
        }
    }

    /// <summary>
    /// Loads a single object from the image file.
    /// </summary>
    private LoadObjectResult LoadObject(BinaryReader reader, string filePath, int objectIndex)
    {
        try
        {
            // Read object header
            int objectOOPRaw = reader.ReadInt32();
            var objectOOP = new SmalltalkOOP(objectOOPRaw);
            int objectId = objectOOP.ToObjectTableIndex();

            byte formatByte = reader.ReadByte();
            if (formatByte > 3)
            {
                return new LoadObjectResult(ImageRestoreResult.CorruptedData(
                    filePath, objectIndex, $"Invalid format byte {formatByte}"));
            }
            var format = (ObjectFormat)formatByte;

            int instanceVarCount = reader.ReadInt32();
            int indexedSize = reader.ReadInt32();
            int classOOPRaw = reader.ReadInt32();
            var classOOP = new SmalltalkOOP(classOOPRaw);

            // Validate counts are non-negative
            if (instanceVarCount < 0 || indexedSize < 0)
            {
                return new LoadObjectResult(ImageRestoreResult.CorruptedData(
                    filePath, objectIndex, $"Negative count: instanceVars={instanceVarCount}, indexed={indexedSize}"));
            }

            // Read instance variables
            var instanceVars = new SmalltalkOOP[instanceVarCount];
            for (int i = 0; i < instanceVarCount; i++)
            {
                int ivarRaw = reader.ReadInt32();
                instanceVars[i] = new SmalltalkOOP(ivarRaw);
            }

            // Read indexed data based on format
            object? indexedStorage = null;

            switch (format)
            {
                case ObjectFormat.Pointer:
                    // No indexed data
                    break;

                case ObjectFormat.PointerIndexed:
                    var indexedOops = new SmalltalkOOP[indexedSize];
                    for (int i = 0; i < indexedSize; i++)
                    {
                        int indexedRaw = reader.ReadInt32();
                        indexedOops[i] = new SmalltalkOOP(indexedRaw);
                    }
                    indexedStorage = indexedOops;
                    break;

                case ObjectFormat.ByteIndexed:
                    var bytes = reader.ReadBytes(indexedSize);
                    if (bytes.Length != indexedSize)
                    {
                        return new LoadObjectResult(ImageRestoreResult.TruncatedFile(
                            filePath, objectIndex, objectIndex + 1));
                    }
                    indexedStorage = bytes;
                    break;

                case ObjectFormat.WordIndexed:
                    var words = new ushort[indexedSize];
                    for (int i = 0; i < indexedSize; i++)
                    {
                        words[i] = reader.ReadUInt16();
                    }
                    indexedStorage = words;
                    break;
            }

            // Restore object at its original OOP location
            _heap.RestoreObject(objectId, format, classOOP, instanceVars, indexedStorage);

            return new LoadObjectResult(true);
        }
        catch (EndOfStreamException)
        {
            return new LoadObjectResult(ImageRestoreResult.TruncatedFile(filePath, objectIndex, objectIndex + 1));
        }
        catch (Exception ex)
        {
            return new LoadObjectResult(ImageRestoreResult.CorruptedData(
                filePath, objectIndex, ex.Message));
        }
    }

    /// <summary>
    /// Checks if a file is a valid Smalltalk image by reading its magic number.
    /// </summary>
    public static bool IsImageFile(string filePath)
    {
        if (!File.Exists(filePath))
            return false;

        try
        {
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length < 4)
                return false;

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

    /// <summary>
    /// Internal result type for header reading.
    /// </summary>
    private readonly struct HeaderReadResult
    {
        public readonly bool Success;
        public readonly int ObjectCount;
        public readonly int MaxObjectId;
        public readonly ImageRestoreResult? Error;

        public HeaderReadResult(int objectCount, int maxObjectId)
        {
            Success = true;
            ObjectCount = objectCount;
            MaxObjectId = maxObjectId;
            Error = null;
        }

        public HeaderReadResult(ImageRestoreResult error)
        {
            Success = false;
            ObjectCount = 0;
            MaxObjectId = 0;
            Error = error;
        }
    }

    /// <summary>
    /// Internal result type for object loading.
    /// </summary>
    private readonly struct LoadObjectResult
    {
        public readonly bool Success;
        public readonly ImageRestoreResult? Result;

        public LoadObjectResult(bool success)
        {
            Success = success;
            Result = null;
        }

        public LoadObjectResult(ImageRestoreResult error)
        {
            Success = false;
            Result = error;
        }
    }
}
