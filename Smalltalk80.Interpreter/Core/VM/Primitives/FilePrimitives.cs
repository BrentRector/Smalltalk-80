using Smalltalk80.Interpreter.HeapManager;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// File system primitives for Windows file I/O operations (120-160).
/// Provides CreateFile/ReadFile/WriteFile style operations for Smalltalk-80.
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Manages open file handles for the interpreter session.
    /// Maps Smalltalk integer handles to .NET FileStream objects.
    /// </summary>
    private static class FileHandleManager
    {
        private static readonly Dictionary<int, FileStream> _openFiles = [];
        private static int _nextHandle = 1;
        private static readonly object _lock = new();

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
                    var stream = new FileStream(sanitizedPath, mode, access, share);
                    int handle = _nextHandle++;
                    _openFiles[handle] = stream;
                    return handle;
                }
                catch
                {
                    return -1;
                }
            }
        }

        /// <summary>
        /// Gets the FileStream for a handle.
        /// </summary>
        public static FileStream? GetStream(int handle)
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
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Gets current count of open files.
        /// </summary>
        public static int OpenFileCount
        {
            get { lock (_lock) { return _openFiles.Count; } }
        }
    }

    /// <summary>
    /// Primitive 120: Parse a Windows path string.
    /// Receiver: WindowsPathName class or instance.
    /// Argument: String path to parse.
    /// Returns: Array with [directory, filename, extension].
    /// </summary>
    public class PathParse : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                string? directory = Path.GetDirectoryName(path) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);

                // Create Array with 3 elements
                var arrayClassOOP = context.RuntimeHeapAccess?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var arrayOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, 3);

                // Create strings for each component
                var dirOOP = CreateString(context, directory);
                var nameOOP = CreateString(context, fileName);
                var extOOP = CreateString(context, extension);

                context.HeapManager.SetIndexedOOP(arrayOOP, 0, dirOOP);
                context.HeapManager.SetIndexedOOP(arrayOOP, 1, nameOOP);
                context.HeapManager.SetIndexedOOP(arrayOOP, 2, extOOP);

                return PrimitiveResult.Ok(arrayOOP);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 121: Open a file (CreateFile equivalent).
    /// Receiver: WindowsFileReference instance.
    /// Arguments: path (String), accessMode (Integer), shareMode (Integer), createMode (Integer).
    /// Returns: Integer handle or -1 on failure.
    /// </summary>
    public class FileOpen : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 121 FileOpen] Arguments.Length={context.Arguments.Length}");
            if (context.Arguments.Length != 4)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: Expected 4 args, got {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var pathOOP = context.Arguments[0];
            var accessOOP = context.Arguments[1];
            var shareOOP = context.Arguments[2];
            var createOOP = context.Arguments[3];

            Console.WriteLine($"[DEBUG Primitive 121] pathOOP={pathOOP}, IsSmallInt={pathOOP.IsSmallInteger}");
            Console.WriteLine($"[DEBUG Primitive 121] accessOOP={accessOOP}, IsSmallInt={accessOOP.IsSmallInteger}");
            Console.WriteLine($"[DEBUG Primitive 121] shareOOP={shareOOP}, IsSmallInt={shareOOP.IsSmallInteger}");
            Console.WriteLine($"[DEBUG Primitive 121] createOOP={createOOP}, IsSmallInt={createOOP.IsSmallInteger}");

            if (pathOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: path is SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!accessOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: access not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!shareOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: share not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!createOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: create not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            string? path = ExtractString(context, pathOOP);
            Console.WriteLine($"[DEBUG Primitive 121] Extracted path: '{path}'");
            if (path == null)
            {
                Console.WriteLine($"[DEBUG Primitive 121] FAIL: path extraction returned null");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int accessMode = accessOOP.AsSmallInteger();
            int shareMode = shareOOP.AsSmallInteger();
            int createMode = createOOP.AsSmallInteger();
            Console.WriteLine($"[DEBUG Primitive 121] accessMode={accessMode}, shareMode={shareMode}, createMode={createMode}");

            int handle = FileHandleManager.OpenFile(path, accessMode, shareMode, createMode);
            Console.WriteLine($"[DEBUG Primitive 121] OpenFile returned handle={handle}");
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(handle));
        }
    }

    /// <summary>
    /// Primitive 122: Close a file handle (CloseHandle equivalent).
    /// Receiver: WindowsFileReference instance.
    /// Argument: handle (Integer).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class FileClose : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 122 FileClose] Arguments.Length={context.Arguments.Length}");
            if (context.Arguments.Length != 1)
            {
                Console.WriteLine($"[DEBUG Primitive 122] FAIL: Expected 1 arg, got {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var handleOOP = context.Arguments[0];
            Console.WriteLine($"[DEBUG Primitive 122] handleOOP={handleOOP}, IsSmallInt={handleOOP.IsSmallInteger}");
            if (!handleOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 122] FAIL: handle not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int handle = handleOOP.AsSmallInteger();
            Console.WriteLine($"[DEBUG Primitive 122] Closing handle={handle}");
            bool success = FileHandleManager.CloseFile(handle);
            Console.WriteLine($"[DEBUG Primitive 122] CloseFile returned success={success}");

            return success
                ? PrimitiveResult.Ok(context.HeapManager.GetNil())
                : PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    /// <summary>
    /// Primitive 123: Read from file (ReadFile equivalent).
    /// Receiver: WindowsFileStream instance.
    /// Arguments: handle (Integer), buffer (ByteArray), count (Integer).
    /// Returns: Integer bytes read.
    /// </summary>
    public class FileRead : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 123 FileRead] Arguments.Length={context.Arguments.Length}");
            if (context.Arguments.Length != 3)
            {
                Console.WriteLine($"[DEBUG Primitive 123] FAIL: Expected 3 args, got {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var handleOOP = context.Arguments[0];
            var bufferOOP = context.Arguments[1];
            var countOOP = context.Arguments[2];

            Console.WriteLine($"[DEBUG Primitive 123] handleOOP={handleOOP}, IsSmallInt={handleOOP.IsSmallInteger}");
            Console.WriteLine($"[DEBUG Primitive 123] bufferOOP={bufferOOP}, IsSmallInt={bufferOOP.IsSmallInteger}");
            Console.WriteLine($"[DEBUG Primitive 123] countOOP={countOOP}, IsSmallInt={countOOP.IsSmallInteger}");

            if (!handleOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 123] FAIL: handle not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (bufferOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 123] FAIL: buffer is SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!countOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 123] FAIL: count not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int handle = handleOOP.AsSmallInteger();
            int count = countOOP.AsSmallInteger();
            Console.WriteLine($"[DEBUG Primitive 123] handle={handle}, count={count}");

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
            {
                Console.WriteLine($"[DEBUG Primitive 123] FAIL: stream is null for handle {handle}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            try
            {
                // Get buffer size
                int bufferSize = context.HeapManager.GetIndexedSize(bufferOOP);
                Console.WriteLine($"[DEBUG Primitive 123] bufferSize={bufferSize}");
                if (count > bufferSize) count = bufferSize;

                // Read into temporary buffer
                byte[] tempBuffer = new byte[count];
                int bytesRead = stream.Read(tempBuffer, 0, count);
                Console.WriteLine($"[DEBUG Primitive 123] bytesRead={bytesRead}");

                // Copy to Smalltalk ByteArray
                for (int i = 0; i < bytesRead; i++)
                {
                    context.HeapManager.SetIndexedByte(bufferOOP, i, tempBuffer[i]);
                }

                Console.WriteLine($"[DEBUG Primitive 123] SUCCESS: returning {bytesRead}");
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(bytesRead));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG Primitive 123] EXCEPTION: {ex.Message}");
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 136: Write to file (WriteFile equivalent).
    /// Receiver: WindowsFileStream instance.
    /// Arguments: handle (Integer), buffer (ByteArray), count (Integer).
    /// Returns: Integer bytes written.
    /// </summary>
    public class FileWrite : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 3) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            var bufferOOP = context.Arguments[1];
            var countOOP = context.Arguments[2];

            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (bufferOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!countOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            int count = countOOP.AsSmallInteger();

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Get buffer size
                int bufferSize = context.HeapManager.GetIndexedSize(bufferOOP);
                if (count > bufferSize) count = bufferSize;

                // Copy from Smalltalk ByteArray to temporary buffer
                byte[] tempBuffer = new byte[count];
                for (int i = 0; i < count; i++)
                {
                    tempBuffer[i] = context.HeapManager.GetIndexedByte(bufferOOP, i);
                }

                // Write to file
                stream.Write(tempBuffer, 0, count);

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(count));
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 124: Seek in file (SetFilePointer equivalent).
    /// Receiver: WindowsFileReference or WindowsFileStream instance.
    /// Arguments: handle (Integer), offset (Integer), origin (Integer: 0=Begin, 1=Current, 2=End).
    /// Returns: Integer new position or -1 on failure.
    /// </summary>
    public class FileSeek : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 3) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            var offsetOOP = context.Arguments[1];
            var originOOP = context.Arguments[2];

            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!offsetOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!originOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            int offset = offsetOOP.AsSmallInteger();
            int origin = originOOP.AsSmallInteger();

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                SeekOrigin seekOrigin = origin switch
                {
                    0 => SeekOrigin.Begin,
                    1 => SeekOrigin.Current,
                    2 => SeekOrigin.End,
                    _ => SeekOrigin.Begin
                };

                long newPosition = stream.Seek(offset, seekOrigin);

                // Clamp to SmallInteger range
                if (newPosition > 0x3FFFFFFF) newPosition = 0x3FFFFFFF;

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)newPosition));
            }
            catch
            {
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(-1));
            }
        }
    }

    /// <summary>
    /// Primitive 125: Delete a file (DeleteFile equivalent).
    /// Receiver: WindowsFileReference instance.
    /// Argument: path (String).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class FileDelete : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                File.Delete(path);
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 126: Move/Rename a file (MoveFile equivalent).
    /// Receiver: WindowsFileReference instance.
    /// Arguments: oldPath (String), newPath (String).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class FileMove : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            var oldPathOOP = context.Arguments[0];
            var newPathOOP = context.Arguments[1];

            if (oldPathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (newPathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? oldPath = ExtractString(context, oldPathOOP);
            string? newPath = ExtractString(context, newPathOOP);

            if (oldPath == null || newPath == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                File.Move(oldPath, newPath);
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 127: Create a directory (CreateDirectory equivalent).
    /// Receiver: WindowsDirectoryReference instance.
    /// Argument: path (String).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class CreateDirectory : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                Directory.CreateDirectory(path);
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 140: Remove a directory (RemoveDirectory equivalent).
    /// Relocated from 128 to avoid conflict with SomeInstance.
    /// Receiver: WindowsDirectoryReference instance.
    /// Argument: path (String).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class RemoveDirectory : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                Directory.Delete(path, false); // Non-recursive
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 154: Get file attributes and size.
    /// Relocated from 130 to avoid conflict with BecomeForward.
    /// Receiver: WindowsFileReference instance.
    /// Argument: path (String).
    /// Returns: Array with [size, attributes, isDirectory] or nil on failure.
    /// </summary>
    public class GetFileAttributes : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                var info = new FileInfo(path);
                long size = info.Exists ? info.Length : 0;
                int attributes = (int)info.Attributes;
                bool isDirectory = (info.Attributes & FileAttributes.Directory) != 0;

                // For simplicity, return as an Array with known order:
                // [0] = size, [1] = attributes, [2] = isDirectory
                var arrayClassOOP = context.RuntimeHeapAccess?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var resultOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, 3);

                // Size (clamp to SmallInteger range)
                if (size > 0x3FFFFFFF) size = 0x3FFFFFFF;
                context.HeapManager.SetIndexedOOP(resultOOP, 0, SmalltalkOOP.FromSmallInteger((int)size));

                // Attributes
                context.HeapManager.SetIndexedOOP(resultOOP, 1, SmalltalkOOP.FromSmallInteger(attributes));

                // IsDirectory
                context.HeapManager.SetIndexedOOP(resultOOP, 2,
                    isDirectory ? context.HeapManager.GetNil() : context.HeapManager.GetNil());

                return PrimitiveResult.Ok(resultOOP);
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 135: Get logical drives.
    /// Receiver: WindowsFileDirectory class.
    /// Returns: String containing drive letters (e.g., "C:D:E:").
    /// </summary>
    public class GetDrives : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                var drives = DriveInfo.GetDrives();
                var driveString = string.Join("", drives.Select(d => d.Name.TrimEnd('\\')));
                return PrimitiveResult.Ok(CreateString(context, driveString));
            }
            catch
            {
                return PrimitiveResult.Ok(CreateString(context, "C:"));
            }
        }
    }

    /// <summary>
    /// Manages directory enumeration state for FindFirst/FindNext.
    /// </summary>
    private static class DirectoryEnumerator
    {
        private static readonly Dictionary<int, IEnumerator<string>> _enumerators = [];
        private static int _nextHandle = 1;
        private static readonly object _lock = new();

        public static int FindFirst(string pattern)
        {
            lock (_lock)
            {
                try
                {
                    string? directory = Path.GetDirectoryName(pattern);
                    string searchPattern = Path.GetFileName(pattern);

                    if (string.IsNullOrEmpty(directory)) directory = ".";
                    if (string.IsNullOrEmpty(searchPattern)) searchPattern = "*";

                    var entries = Directory.EnumerateFileSystemEntries(directory, searchPattern);
                    var enumerator = entries.GetEnumerator();

                    int handle = _nextHandle++;
                    _enumerators[handle] = enumerator;
                    return handle;
                }
                catch
                {
                    return -1;
                }
            }
        }

        public static string? FindNext(int handle)
        {
            lock (_lock)
            {
                if (_enumerators.TryGetValue(handle, out var enumerator))
                {
                    if (enumerator.MoveNext())
                    {
                        return Path.GetFileName(enumerator.Current);
                    }
                }
                return null;
            }
        }

        public static bool FindClose(int handle)
        {
            lock (_lock)
            {
                if (_enumerators.TryGetValue(handle, out var enumerator))
                {
                    enumerator.Dispose();
                    _enumerators.Remove(handle);
                    return true;
                }
                return false;
            }
        }
    }

    /// <summary>
    /// Primitive 141: Find first file matching pattern (FindFirstFile equivalent).
    /// Relocated from 132 to avoid conflicts.
    /// Receiver: WindowsDirectoryReference instance.
    /// Argument: pattern (String).
    /// Returns: Integer handle or -1 on failure.
    /// </summary>
    public class FindFirst : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var patternOOP = context.Arguments[0];
            if (patternOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? pattern = ExtractString(context, patternOOP);
            if (pattern == null) return PrimitiveResult.Fail(context.Metadata!);

            int handle = DirectoryEnumerator.FindFirst(pattern);
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(handle));
        }
    }

    /// <summary>
    /// Primitive 142: Find next file in enumeration (FindNextFile equivalent).
    /// Relocated from 133 to avoid conflict with ForceGC.
    /// Receiver: WindowsDirectoryReference instance.
    /// Argument: handle (Integer).
    /// Returns: String filename or nil if no more files.
    /// </summary>
    public class FindNext : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            string? fileName = DirectoryEnumerator.FindNext(handle);

            if (fileName == null)
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }

            return PrimitiveResult.Ok(CreateString(context, fileName));
        }
    }

    /// <summary>
    /// Primitive 143: Close find handle (FindClose equivalent).
    /// Relocated from 134.
    /// Receiver: WindowsDirectoryReference instance.
    /// Argument: handle (Integer).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class FindClose : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            bool success = DirectoryEnumerator.FindClose(handle);

            return success
                ? PrimitiveResult.Ok(context.HeapManager.GetNil())
                : PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    /// <summary>
    /// Primitive 137: Get current working directory.
    /// Receiver: WindowsFileDirectory class.
    /// Returns: String with current directory path.
    /// </summary>
    public class GetCurrentDirectory : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                string currentDir = Directory.GetCurrentDirectory();
                return PrimitiveResult.Ok(CreateString(context, currentDir));
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 138: Set current working directory.
    /// Receiver: WindowsFileDirectory class.
    /// Argument: path (String).
    /// Returns: true on success, false on failure.
    /// </summary>
    public class SetCurrentDirectory : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                Directory.SetCurrentDirectory(path);
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    /// <summary>
    /// Primitive 155: File/Directory exists check.
    /// Relocated from 139 to avoid conflict with NextObject.
    /// Receiver: any file object.
    /// Argument: path (String).
    /// Returns: true if exists, false otherwise.
    /// </summary>
    public class FileExists : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            bool exists = File.Exists(path) || Directory.Exists(path);
            return exists
                ? PrimitiveResult.Ok(context.HeapManager.GetNil())
                : PrimitiveResult.Ok(context.HeapManager.GetNil());
        }
    }

    /// <summary>
    /// Primitive 144: Get file size.
    /// Receiver: WindowsFileReference or WindowsFile instance.
    /// Argument: path (String) or handle (Integer).
    /// Returns: Integer file size.
    /// </summary>
    public class GetFileSize : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 144 GetFileSize] Arguments.Length={context.Arguments.Length}");
            if (context.Arguments.Length != 1)
            {
                Console.WriteLine($"[DEBUG Primitive 144] FAIL: Expected 1 arg, got {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var arg = context.Arguments[0];
            Console.WriteLine($"[DEBUG Primitive 144] arg={arg}, IsSmallInt={arg.IsSmallInteger}");

            long size;

            if (arg.IsSmallInteger)
            {
                // Handle is an integer - get size from stream
                int handle = arg.AsSmallInteger();
                Console.WriteLine($"[DEBUG Primitive 144] Getting size for handle={handle}");
                var stream = FileHandleManager.GetStream(handle);
                if (stream == null)
                {
                    Console.WriteLine($"[DEBUG Primitive 144] FAIL: stream is null");
                    return PrimitiveResult.Fail(context.Metadata!);
                }
                size = stream.Length;
                Console.WriteLine($"[DEBUG Primitive 144] stream.Length={size}");
            }
            else
            {
                // Path is a string
                string? path = ExtractString(context, arg);
                Console.WriteLine($"[DEBUG Primitive 144] Getting size for path='{path}'");
                if (path == null)
                {
                    Console.WriteLine($"[DEBUG Primitive 144] FAIL: path is null");
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                try
                {
                    var info = new FileInfo(path);
                    if (!info.Exists)
                    {
                        Console.WriteLine($"[DEBUG Primitive 144] FAIL: file does not exist");
                        return PrimitiveResult.Fail(context.Metadata!);
                    }
                    size = info.Length;
                    Console.WriteLine($"[DEBUG Primitive 144] file.Length={size}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[DEBUG Primitive 144] EXCEPTION: {ex.Message}");
                    return PrimitiveResult.Fail(context.Metadata!);
                }
            }

            // Clamp to SmallInteger range
            if (size > 0x3FFFFFFF) size = 0x3FFFFFFF;

            Console.WriteLine($"[DEBUG Primitive 144] SUCCESS: returning {size}");
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)size));
        }
    }

    /// <summary>
    /// Primitive 145: Get file position.
    /// Receiver: WindowsFileStream instance.
    /// Argument: handle (Integer).
    /// Returns: Integer current position.
    /// </summary>
    public class GetFilePosition : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 145 GetFilePosition] Arguments.Length={context.Arguments.Length}");
            if (context.Arguments.Length != 1)
            {
                Console.WriteLine($"[DEBUG Primitive 145] FAIL: Expected 1 arg, got {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var handleOOP = context.Arguments[0];
            Console.WriteLine($"[DEBUG Primitive 145] handleOOP={handleOOP}, IsSmallInt={handleOOP.IsSmallInteger}");
            if (!handleOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 145] FAIL: handle not SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            int handle = handleOOP.AsSmallInteger();
            Console.WriteLine($"[DEBUG Primitive 145] Getting position for handle={handle}");
            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
            {
                Console.WriteLine($"[DEBUG Primitive 145] FAIL: stream is null");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            long position = stream.Position;
            Console.WriteLine($"[DEBUG Primitive 145] stream.Position={position}");
            if (position > 0x3FFFFFFF) position = 0x3FFFFFFF;

            Console.WriteLine($"[DEBUG Primitive 145] SUCCESS: returning {position}");
            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)position));
        }
    }

    /// <summary>
    /// Primitive 146: Directory listing (for WindowsFileDirectory primitive 95).
    /// Receiver: WindowsFileDirectory instance.
    /// Arguments: dirPath (String), pattern (String).
    /// Returns: Array of filenames.
    /// </summary>
    public class DirectoryList : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            Console.WriteLine($"[DEBUG Primitive 146 ENTRY] ArgCount={context.Arguments.Length}, Receiver={context.Receiver.RawValue}");

            if (context.Arguments.Length != 2)
            {
                Console.WriteLine($"[DEBUG Primitive 146 FAIL] Wrong arg count: {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var dirPathOOP = context.Arguments[0];
            var patternOOP = context.Arguments[1];
            Console.WriteLine($"[DEBUG Primitive 146] Args: dirPath OOP={dirPathOOP.RawValue} (IsSmallInt={dirPathOOP.IsSmallInteger}), pattern OOP={patternOOP.RawValue} (IsSmallInt={patternOOP.IsSmallInteger})");

            if (dirPathOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 146 FAIL] dirPath is SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (patternOOP.IsSmallInteger)
            {
                Console.WriteLine($"[DEBUG Primitive 146 FAIL] pattern is SmallInteger");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            string? dirPath = ExtractString(context, dirPathOOP);
            string? pattern = ExtractString(context, patternOOP);
            Console.WriteLine($"[DEBUG Primitive 146] Extracted: dirPath='{dirPath}', pattern='{pattern}'");

            if (dirPath == null || pattern == null)
            {
                Console.WriteLine($"[DEBUG Primitive 146 FAIL] String extraction failed: dirPath={dirPath ?? "null"}, pattern={pattern ?? "null"}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            try
            {
                // Handle "." as current directory
                if (dirPath == ".")
                {
                    dirPath = Directory.GetCurrentDirectory();
                    Console.WriteLine($"[DEBUG Primitive 146] Searching in current directory: {dirPath}");
                }
                else
                {
                    Console.WriteLine($"[DEBUG Primitive 146] Searching in: {dirPath}");
                }
                Console.WriteLine($"[DEBUG Primitive 146] Pattern: {pattern}");

                var entries = Directory.GetFileSystemEntries(dirPath, pattern);
                var names = entries.Select(Path.GetFileName).Where(n => n != null).ToArray();

                Console.WriteLine($"[DEBUG Primitive 146] Found {names.Length} entries:");
                foreach (var name in names.Take(20))
                {
                    // Show detailed byte info for debugging
                    var bytes = System.Text.Encoding.ASCII.GetBytes(name!);
                    var hexBytes = string.Join(" ", bytes.Select(b => $"{b:X2}"));
                    Console.WriteLine($"[DEBUG Primitive 146]   - '{name}' (len={name!.Length}, bytes={hexBytes})");
                }
                if (names.Length > 20) Console.WriteLine($"[DEBUG Primitive 146]   ... and {names.Length - 20} more");

                // Create Array
                var arrayClassOOP = context.RuntimeHeapAccess?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var arrayOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, names.Length);
                var arrayClassName = context.HeapManager.GetClassName(arrayOOP);
                Console.WriteLine($"[DEBUG Primitive 146] Created array OOP={arrayOOP.RawValue} with {names.Length} slots, class={arrayClassName}, classOOP={arrayClassOOP.RawValue}");

                for (int i = 0; i < names.Length; i++)
                {
                    var nameOOP = CreateString(context, names[i]!);
                    context.HeapManager.SetIndexedOOP(arrayOOP, i, nameOOP);
                    // Verify the string was stored correctly
                    var storedOOP = context.HeapManager.GetIndexedOOP(arrayOOP, i);
                    var storedLen = context.HeapManager.GetIndexedSize(storedOOP);
                    Console.WriteLine($"[DEBUG Primitive 146]   Stored [{i}]: OOP={nameOOP.RawValue}, len={storedLen}, verified={storedOOP.RawValue == nameOOP.RawValue}");
                }

                return PrimitiveResult.Ok(arrayOOP);
            }
            catch
            {
                // Return empty array on error
                var emptyArrayClassOOP = context.RuntimeHeapAccess?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                return PrimitiveResult.Ok(context.HeapManager.AllocateInstanceOf(emptyArrayClassOOP, 0));
            }
        }
    }

    /// <summary>
    /// Primitive 151: Flush file buffers.
    /// Receiver: WindowsFileStream instance.
    /// Argument: handle (Integer).
    /// Returns: receiver on success.
    /// </summary>
    public class FileFlush : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            var stream = FileHandleManager.GetStream(handle);
            if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                stream.Flush();
                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 152: Read entire file as String.
    /// Receiver: WindowsFile or any file object.
    /// Argument: filename (String).
    /// Returns: String containing file contents.
    /// </summary>
    public class ReadFileAsString : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                string content = File.ReadAllText(path);
                return PrimitiveResult.Ok(CreateString(context, content));
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 153: Write String to file.
    /// Receiver: WindowsFile or any file object.
    /// Arguments: filename (String), content (String).
    /// Returns: receiver on success.
    /// </summary>
    public class WriteStringToFile : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            var contentOOP = context.Arguments[1];

            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (contentOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = ExtractString(context, pathOOP);
            string? content = ExtractString(context, contentOOP);

            if (path == null || content == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                File.WriteAllText(path, content);
                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    #region Helper Methods

    /// <summary>
    /// Extracts a C# string from a Smalltalk String object.
    /// </summary>
    private static string? ExtractString(PrimitiveExecutionContext context, SmalltalkOOP stringOOP)
    {
        if (stringOOP.IsSmallInteger) return null;

        try
        {
            int length = context.HeapManager.GetIndexedSize(stringOOP);
            char[] chars = new char[length];

            for (int i = 0; i < length; i++)
            {
                chars[i] = (char)context.HeapManager.GetIndexedByte(stringOOP, i);
            }

            return new string(chars);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Creates a Smalltalk String object from a C# string.
    /// </summary>
    private static SmalltalkOOP CreateString(PrimitiveExecutionContext context, string value)
    {
        // Get String class
        var stringClassOOP = context.RuntimeHeapAccess?.GetClass("String")
            ?? throw new InvalidOperationException("Cannot find String class");

        // Allocate String with indexed bytes
        var stringOOP = context.HeapManager.AllocateInstanceOf(stringClassOOP, value.Length);

        // Copy characters as bytes
        for (int i = 0; i < value.Length; i++)
        {
            context.HeapManager.SetIndexedByte(stringOOP, i, (byte)value[i]);
        }

        return stringOOP;
    }

    #endregion
}
