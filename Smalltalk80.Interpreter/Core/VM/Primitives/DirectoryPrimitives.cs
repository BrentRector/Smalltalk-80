using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Directory operation primitives: create, remove, list, enumerate.
/// </summary>
public static class DirectoryPrimitives
{
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
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
                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, driveString));
            }
            catch
            {
                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, "C:"));
            }
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
                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, currentDir));
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

            string? path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
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

            string? pattern = FilePrimitiveHelpers.ExtractString(context, patternOOP);
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

            return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, fileName));
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
    /// Primitive 146: Directory listing (for WindowsFileDirectory primitive 95).
    /// Receiver: WindowsFileDirectory instance.
    /// Arguments: dirPath (String), pattern (String).
    /// Returns: Array of filenames.
    /// </summary>
    public class DirectoryList : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2)
                return PrimitiveResult.Fail(context.Metadata!);

            var dirPathOOP = context.Arguments[0];
            var patternOOP = context.Arguments[1];

            if (dirPathOOP.IsSmallInteger || patternOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            string? dirPath = FilePrimitiveHelpers.ExtractString(context, dirPathOOP);
            string? pattern = FilePrimitiveHelpers.ExtractString(context, patternOOP);

            if (dirPath == null || pattern == null)
                return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Use executable directory, not current working directory
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;

                // Handle "." as current directory - use Data folder for resource files
                if (dirPath == ".")
                {
                    // Try Data folder first, then fall back to executable directory
                    string dataPath = Path.Combine(exeDir, "Data");
                    dirPath = Directory.Exists(dataPath) ? dataPath : exeDir;
                }
                else if (!Directory.Exists(dirPath))
                {
                    // Try Data folder fallback
                    string dataPath = Path.Combine(exeDir, "Data", dirPath);
                    if (Directory.Exists(dataPath))
                        dirPath = dataPath;
                }

                var entries = Directory.GetFileSystemEntries(dirPath, pattern);
                var names = entries.Select(Path.GetFileName).Where(n => n != null).ToArray();

                // Create Array
                var arrayClassOOP = context.RuntimeServices?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                var arrayOOP = context.HeapManager.AllocateInstanceOf(arrayClassOOP, names.Length);

                for (int i = 0; i < names.Length; i++)
                {
                    var nameOOP = FilePrimitiveHelpers.CreateString(context, names[i]!);
                    context.HeapManager.SetIndexedOOP(arrayOOP, i, nameOOP);
                }

                return PrimitiveResult.Ok(arrayOOP);
            }
            catch
            {
                // Return empty array on error
                var emptyArrayClassOOP = context.RuntimeServices?.GetClass("Array")
                    ?? throw new InvalidOperationException("Cannot find Array class");
                return PrimitiveResult.Ok(context.HeapManager.AllocateInstanceOf(emptyArrayClassOOP, 0));
            }
        }
    }
}
