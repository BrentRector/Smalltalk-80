using System.Diagnostics;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// File metadata and utility primitives: path parsing, delete, move, attributes, size, exists.
/// </summary>
public static class FileInfoPrimitives
{
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                string? directory = Path.GetDirectoryName(path) ?? "";
                string fileName = Path.GetFileNameWithoutExtension(path);
                string extension = Path.GetExtension(path);

                // Create Array with 3 elements
                var arrayClassOOP = context.RuntimeServices?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var arrayOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, 3);

                // Create strings for each component
                var dirOOP = FilePrimitiveHelpers.CreateString(context, directory);
                var nameOOP = FilePrimitiveHelpers.CreateString(context, fileName);
                var extOOP = FilePrimitiveHelpers.CreateString(context, extension);

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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
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

            string? oldPath = FilePrimitiveHelpers.ExtractString(context, oldPathOOP);
            string? newPath = FilePrimitiveHelpers.ExtractString(context, newPathOOP);

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
            var sw = Stopwatch.StartNew();
            try
            {
                return ExecuteCore(context);
            }
            finally
            {
                sw.Stop();
                FilePrimitiveDiagnostics.Record("GetFileAttributes", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Try direct path first, then fall back to Data folder for resource files
                string actualPath = path;
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    // Fall back to Data folder (for .form, .sources, etc.)
                    // Use executable directory, not current working directory
                    string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                    string dataPath = Path.Combine(exeDir, "Data", path);

                    if (File.Exists(dataPath) || Directory.Exists(dataPath))
                    {
                        actualPath = dataPath;
                    }
                    else
                    {
                        return PrimitiveResult.Ok(context.HeapManager.GetNil());
                    }
                }

                var info = new FileInfo(actualPath);
                long size = info.Exists ? info.Length : 0;
                int attributes = (int)info.Attributes;
                bool isDirectory = (info.Attributes & FileAttributes.Directory) != 0;

                // For simplicity, return as an Array with known order:
                // [0] = size, [1] = attributes, [2] = isDirectory
                var arrayClassOOP = context.RuntimeServices?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var resultOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, 3);

                // Size (clamp to SmallInteger range)
                if (size > 0x3FFFFFFF) size = 0x3FFFFFFF;
                context.HeapManager.SetIndexedOOP(resultOOP, 0, SmalltalkOOP.FromSmallInteger((int)size));

                // Attributes
                context.HeapManager.SetIndexedOOP(resultOOP, 1, SmalltalkOOP.FromSmallInteger(attributes));

                // IsDirectory
                context.HeapManager.SetIndexedOOP(resultOOP, 2,
                    isDirectory ? context.Metadata!.KnownObjects.True : context.Metadata!.KnownObjects.False);

                return PrimitiveResult.Ok(resultOOP);
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
            var sw = Stopwatch.StartNew();
            try
            {
                return ExecuteCore(context);
            }
            finally
            {
                sw.Stop();
                FilePrimitiveDiagnostics.Record("FileExists", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            var pathOOP = context.Arguments[0];
            if (pathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
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
            var sw = Stopwatch.StartNew();
            try
            {
                return ExecuteCore(context);
            }
            finally
            {
                sw.Stop();
                FilePrimitiveDiagnostics.Record("GetFileSize", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            long size;

            if (context.Arguments.Length == 0)
            {
                // No arguments: get handle from receiver's instance variable slot 1
                // WindowsFileReference layout: slot 0 = pathName, slot 1 = handle, slot 2 = position
                var receiverOOP = context.Receiver;
                var handleOOP = context.HeapManager.GetInstanceVariable(receiverOOP, 1);
                if (!handleOOP.IsSmallInteger)
                {
                    Console.WriteLine($"[Primitive 144] FAIL: handle is not SmallInteger. handleOOP={handleOOP}, receiver={receiverOOP}");
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                int handle = handleOOP.AsSmallInteger();
                var stream = FileHandleManager.GetStream(handle);
                if (stream == null)
                {
                    Console.WriteLine($"[Primitive 144] FAIL: stream not found for handle={handle}. OpenFileCount={FileHandleManager.OpenFileCount}");
                    return PrimitiveResult.Fail(context.Metadata!);
                }
                size = stream.Length;
            }
            else if (context.Arguments.Length == 1)
            {
                var arg = context.Arguments[0];

                if (arg.IsSmallInteger)
                {
                    // Handle is an integer - get size from stream
                    int handle = arg.AsSmallInteger();
                    var stream = FileHandleManager.GetStream(handle);
                    if (stream == null)
                        return PrimitiveResult.Fail(context.Metadata!);
                    size = stream.Length;
                }
                else
                {
                    // Path is a string
                    string? path = FilePrimitiveHelpers.ExtractString(context, arg);
                    if (path == null)
                        return PrimitiveResult.Fail(context.Metadata!);

                    try
                    {
                        var info = new FileInfo(path);
                        if (!info.Exists)
                            return PrimitiveResult.Fail(context.Metadata!);
                        size = info.Length;
                    }
                    catch
                    {
                        return PrimitiveResult.Fail(context.Metadata!);
                    }
                }
            }
            else
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // Clamp to SmallInteger range
            if (size > 0x3FFFFFFF) size = 0x3FFFFFFF;

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
            if (context.Arguments.Length != 1)
                return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            if (!handleOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
                return PrimitiveResult.Fail(context.Metadata!);

            long position = stream.Position;
            if (position > 0x3FFFFFFF) position = 0x3FFFFFFF;

            return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)position));
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                string content = File.ReadAllText(path);
                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, content));
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
            string? content = FilePrimitiveHelpers.ExtractString(context, contentOOP);

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

    /// <summary>
    /// Primitive 157: Copy a file to a new location.
    /// Receiver: WindowsDirectory or any directory object.
    /// Arguments: sourcePath (String), destPath (String).
    /// Returns: receiver on success, fails if copy cannot be performed.
    /// </summary>
    public class FileCopy : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            var sourcePathOOP = context.Arguments[0];
            var destPathOOP = context.Arguments[1];

            if (sourcePathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (destPathOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            string? sourcePath = FilePrimitiveHelpers.ExtractString(context, sourcePathOOP);
            string? destPath = FilePrimitiveHelpers.ExtractString(context, destPathOOP);

            if (sourcePath == null || destPath == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                File.Copy(sourcePath, destPath, overwrite: true);
                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 160: Get path for a Windows special folder.
    /// Receiver: WindowsPath class or any object.
    /// Argument: Symbol identifier for the special folder (#appData, #localAppData, etc.).
    /// Returns: String path or nil if folder unknown or unavailable.
    /// </summary>
    public class GetSpecialFolder : IPrimitive
    {
        private static readonly Dictionary<string, Environment.SpecialFolder> FolderMap = new()
        {
            { "appData", Environment.SpecialFolder.ApplicationData },
            { "localAppData", Environment.SpecialFolder.LocalApplicationData },
            { "userProfile", Environment.SpecialFolder.UserProfile },
            { "personal", Environment.SpecialFolder.Personal },
            { "desktop", Environment.SpecialFolder.Desktop },
            { "programFiles", Environment.SpecialFolder.ProgramFiles },
            { "programFilesX86", Environment.SpecialFolder.ProgramFilesX86 },
            { "commonAppData", Environment.SpecialFolder.CommonApplicationData },
            { "system", Environment.SpecialFolder.System },
            { "windows", Environment.SpecialFolder.Windows },
            { "startup", Environment.SpecialFolder.Startup },
            { "fonts", Environment.SpecialFolder.Fonts },
        };

        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1)
                return PrimitiveResult.Fail(context.Metadata!);

            var symbolOOP = context.Arguments[0];
            if (symbolOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            // Extract symbol name - Symbol is ByteIndexed like String
            string? symbolName = FilePrimitiveHelpers.ExtractString(context, symbolOOP);
            if (symbolName == null)
                return PrimitiveResult.Fail(context.Metadata!);

            // Look up in folder map
            if (!FolderMap.TryGetValue(symbolName, out var folder))
                return PrimitiveResult.Ok(context.HeapManager.GetNil());

            try
            {
                string path = Environment.GetFolderPath(folder);
                if (string.IsNullOrEmpty(path))
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, path));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }
}
