namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Manages open file handles for the interpreter session.
/// Maps Smalltalk integer handles to .NET Stream objects (BufferedStream wrapping FileStream).
/// </summary>
internal static class FileHandleManager
{
    private static readonly Dictionary<int, Stream> _openFiles = [];
    private static readonly Dictionary<int, int> _bomOffsets = []; // Track BOM offset per handle
    private static int _nextHandle = 1;
    private static readonly object _lock = new();
    private const int BufferSize = 4096;

    /// <summary>
    /// Opens a file and returns a handle.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="accessMode">1=Read, 2=Write, 3=ReadWrite.</param>
    /// <param name="shareMode">0=None, 1=Read, 2=Write, 3=ReadWrite.</param>
    /// <param name="createMode">1=CreateNew, 2=Create, 3=Open, 4=OpenOrCreate, 5=Truncate.</param>
    /// <returns>Handle or -1 on error.</returns>
    public static int OpenFile(string path, int accessMode, int shareMode, int createMode)
    {
        lock (_lock)
        {
            try
            {
                FileAccess access = accessMode switch
                {
                    1 => FileAccess.Read,
                    2 => FileAccess.Write,
                    3 => FileAccess.ReadWrite,
                    _ => FileAccess.ReadWrite
                };

                FileShare share = shareMode switch
                {
                    0 => FileShare.None,
                    1 => FileShare.Read,
                    2 => FileShare.Write,
                    3 => FileShare.ReadWrite,
                    _ => FileShare.None
                };

                FileMode mode = createMode switch
                {
                    1 => FileMode.CreateNew,
                    2 => FileMode.Create,
                    3 => FileMode.Open,
                    4 => FileMode.OpenOrCreate,
                    5 => FileMode.Truncate,
                    _ => FileMode.OpenOrCreate
                };

                // Sanitize path for Windows - remove trailing periods which Windows doesn't accept
                var sanitizedPath = path.TrimEnd('.');
                var fileStream = new FileStream(sanitizedPath, mode, access, share);
                // Wrap in BufferedStream for efficient byte-by-byte reads (Smalltalk reads 1 byte at a time)
                var bufferedStream = new BufferedStream(fileStream, BufferSize);
                int handle = _nextHandle++;
                _openFiles[handle] = bufferedStream;

                // Detect UTF-8 BOM (EF BB BF) at start of file
                // If present, store offset of 3 so seeks to position N go to N+3
                // This ensures source positions in CompiledMethods work correctly
                int bomOffset = 0;
                if (fileStream.Length >= 3 && access != FileAccess.Write)
                {
                    byte[] bom = new byte[3];
                    int bytesRead = 0;
                    while (bytesRead < 3)
                    {
                        int read = bufferedStream.Read(bom, bytesRead, 3 - bytesRead);
                        if (read == 0) break; // EOF
                        bytesRead += read;
                    }
                    if (bytesRead == 3 && bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                    {
                        bomOffset = 3;
                        Console.WriteLine($"[FileHandleManager] Detected UTF-8 BOM in {path}, offset={bomOffset}");
                    }
                    // Reset position to start
                    bufferedStream.Position = 0;
                }
                _bomOffsets[handle] = bomOffset;

                return handle;
            }
            catch
            {
                return -1;
            }
        }
    }

    /// <summary>
    /// Gets the Stream for a handle.
    /// </summary>
    public static Stream? GetStream(int handle)
    {
        lock (_lock)
        {
            return _openFiles.TryGetValue(handle, out var stream) ? stream : null;
        }
    }

    /// <summary>
    /// Closes a file handle.
    /// </summary>
    public static bool CloseFile(int handle)
    {
        lock (_lock)
        {
            if (_openFiles.TryGetValue(handle, out var stream))
            {
                stream.Close();
                _openFiles.Remove(handle);
                _bomOffsets.Remove(handle);
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Gets the BOM offset for a handle (3 if UTF-8 BOM detected, 0 otherwise).
    /// Used to adjust seek positions so source file positions work correctly.
    /// </summary>
    public static int GetBomOffset(int handle)
    {
        lock (_lock)
        {
            return _bomOffsets.TryGetValue(handle, out var offset) ? offset : 0;
        }
    }

    /// <summary>
    /// Gets current count of open files.
    /// </summary>
    public static int OpenFileCount
    {
        get { lock (_lock) { return _openFiles.Count; } }
    }

    /// <summary>
    /// Gets the file size for a handle.
    /// Returns -1 if handle is invalid.
    /// </summary>
    public static long GetFileSize(int handle)
    {
        lock (_lock)
        {
            if (_openFiles.TryGetValue(handle, out var stream))
            {
                return stream.Length;
            }
            return -1;
        }
    }
}
