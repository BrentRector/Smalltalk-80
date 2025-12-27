namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Public API for file operations used by cold start and other interpreter components.
/// The actual primitive implementations are split across:
/// - FileStreamPrimitives.cs - Core file I/O (open, close, read, write, seek)
/// - DirectoryPrimitives.cs - Directory operations (create, remove, list, enumerate)
/// - FileInfoPrimitives.cs - File metadata/utility (path parsing, delete, move, attributes)
/// - FileHandleManager.cs - Handle management and BOM offset tracking
/// - DirectoryEnumerator.cs - Directory enumeration state
/// - FilePrimitiveHelpers.cs - Shared helper methods
/// </summary>
public static class FileOperations
{
    /// <summary>
    /// Opens a file and returns a handle. Public wrapper for cold start use.
    /// </summary>
    /// <param name="path">File path.</param>
    /// <param name="accessMode">1=Read, 2=Write, 3=ReadWrite.</param>
    /// <param name="shareMode">0=None, 1=Read, 2=Write, 3=ReadWrite.</param>
    /// <param name="createMode">1=CreateNew, 2=Create, 3=Open, 4=OpenOrCreate, 5=Truncate.</param>
    /// <returns>Handle or -1 on error.</returns>
    public static int OpenFileHandle(string path, int accessMode = 1, int shareMode = 1, int createMode = 3)
    {
        return FileHandleManager.OpenFile(path, accessMode, shareMode, createMode);
    }

    /// <summary>
    /// Closes a file handle. Public wrapper for cold start use.
    /// </summary>
    /// <param name="handle">The file handle to close.</param>
    /// <returns>True if closed successfully.</returns>
    public static bool CloseFileHandle(int handle)
    {
        return FileHandleManager.CloseFile(handle);
    }
}
