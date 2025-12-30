using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Primitives for file system link operations: symbolic links, hard links, and junctions.
/// Uses .NET APIs where available (.NET 6+), P/Invoke only for operations not in .NET:
/// - Hard links (CreateHardLinkW)
/// - Junctions (DeviceIoControl)
/// - Reparse tag detection (FindFirstFileW)
/// - Hard link count (GetFileInformationByHandle)
/// </summary>
public static class LinkPrimitives
{
    #region Windows API Constants and Structures (for operations not in .NET)

    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;

    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;

    private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;
    private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

    private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;
    private const uint FSCTL_DELETE_REPARSE_POINT = 0x000900AC;

    [StructLayout(LayoutKind.Sequential)]
    private struct BY_HANDLE_FILE_INFORMATION
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct WIN32_FIND_DATA
    {
        public uint dwFileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftCreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME ftLastWriteTime;
        public uint nFileSizeHigh;
        public uint nFileSizeLow;
        public uint dwReserved0;  // This contains the reparse tag
        public uint dwReserved1;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string cFileName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)]
        public string cAlternateFileName;
    }

    #endregion

    #region Windows API P/Invoke (only for operations not available in .NET)

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkW(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFileW(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(
        SafeFileHandle hFile,
        out BY_HANDLE_FILE_INFORMATION lpFileInformation);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr FindFirstFileW(
        string lpFileName,
        out WIN32_FIND_DATA lpFindFileData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FindClose(IntPtr hFindFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        byte[] lpInBuffer,
        uint nInBufferSize,
        IntPtr lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

    #endregion

    #region Primitive 230: CreateSymbolicLink (uses .NET API)

    /// <summary>
    /// Primitive 230: Create a symbolic link using .NET 6+ API.
    /// Arguments: linkPath (String), targetPath (String), isDirectory (Boolean)
    /// Returns: receiver on success, signals error on failure
    /// </summary>
    public class CreateSymbolicLink : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 3) return PrimitiveResult.Fail(context.Metadata!);

            string? linkPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            string? targetPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[1]);
            var isDirOOP = context.Arguments[2];

            if (linkPath == null || targetPath == null) return PrimitiveResult.Fail(context.Metadata!);

            bool isDirectory = isDirOOP == context.Metadata!.KnownObjects.True;

            try
            {
                // Use .NET 6+ API - handles privilege requirements internally
                if (isDirectory)
                {
                    Directory.CreateSymbolicLink(linkPath, targetPath);
                }
                else
                {
                    File.CreateSymbolicLink(linkPath, targetPath);
                }

                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    #endregion

    #region Primitive 231: CreateHardLink

    /// <summary>
    /// Primitive 231: Create a hard link.
    /// Arguments: linkPath (String), existingPath (String)
    /// Returns: receiver on success, signals error on failure
    /// </summary>
    public class CreateHardLink : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            string? linkPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            string? existingPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[1]);

            if (linkPath == null || existingPath == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                bool success = CreateHardLinkW(linkPath, existingPath, IntPtr.Zero);

                if (!success)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    #endregion

    #region Primitive 232: CreateJunction

    /// <summary>
    /// Primitive 232: Create a junction (directory mount point).
    /// Arguments: junctionPath (String), targetPath (String)
    /// Returns: receiver on success, signals error on failure
    /// </summary>
    public class CreateJunction : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2) return PrimitiveResult.Fail(context.Metadata!);

            string? junctionPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            string? targetPath = FilePrimitiveHelpers.ExtractString(context, context.Arguments[1]);

            if (junctionPath == null || targetPath == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // First create the directory for the junction
                if (!Directory.Exists(junctionPath))
                {
                    Directory.CreateDirectory(junctionPath);
                }

                // Prepare the target path in NT format
                string ntTargetPath = @"\??\\" + Path.GetFullPath(targetPath);

                // Build the reparse data buffer for a mount point
                byte[] reparseBuffer = BuildMountPointReparseBuffer(ntTargetPath, targetPath);

                // Open the directory and set the reparse point
                using var handle = CreateFileW(
                    junctionPath,
                    GENERIC_READ | 0x40000000, // GENERIC_WRITE
                    0,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                bool success = DeviceIoControl(
                    handle,
                    FSCTL_SET_REPARSE_POINT,
                    reparseBuffer,
                    (uint)reparseBuffer.Length,
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero);

                if (!success)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }

        private static byte[] BuildMountPointReparseBuffer(string substituteName, string printName)
        {
            byte[] substituteNameBytes = System.Text.Encoding.Unicode.GetBytes(substituteName);
            byte[] printNameBytes = System.Text.Encoding.Unicode.GetBytes(printName);

            // Mount point reparse data structure:
            // - ReparseTag (4 bytes)
            // - ReparseDataLength (2 bytes)
            // - Reserved (2 bytes)
            // - SubstituteNameOffset (2 bytes)
            // - SubstituteNameLength (2 bytes)
            // - PrintNameOffset (2 bytes)
            // - PrintNameLength (2 bytes)
            // - SubstituteName (variable)
            // - PrintName (variable)

            int headerSize = 8;
            int dataSize = 8 + substituteNameBytes.Length + 2 + printNameBytes.Length + 2;

            byte[] buffer = new byte[headerSize + dataSize];

            // ReparseTag = IO_REPARSE_TAG_MOUNT_POINT
            BitConverter.GetBytes(IO_REPARSE_TAG_MOUNT_POINT).CopyTo(buffer, 0);

            // ReparseDataLength
            BitConverter.GetBytes((ushort)dataSize).CopyTo(buffer, 4);

            // Reserved = 0
            BitConverter.GetBytes((ushort)0).CopyTo(buffer, 6);

            int offset = 8;

            // SubstituteNameOffset = 0
            BitConverter.GetBytes((ushort)0).CopyTo(buffer, offset);
            offset += 2;

            // SubstituteNameLength
            BitConverter.GetBytes((ushort)substituteNameBytes.Length).CopyTo(buffer, offset);
            offset += 2;

            // PrintNameOffset
            BitConverter.GetBytes((ushort)(substituteNameBytes.Length + 2)).CopyTo(buffer, offset);
            offset += 2;

            // PrintNameLength
            BitConverter.GetBytes((ushort)printNameBytes.Length).CopyTo(buffer, offset);
            offset += 2;

            // SubstituteName
            substituteNameBytes.CopyTo(buffer, offset);
            offset += substituteNameBytes.Length + 2; // +2 for null terminator

            // PrintName
            printNameBytes.CopyTo(buffer, offset);

            return buffer;
        }
    }

    #endregion

    #region Primitive 233: GetFileAttributes (uses .NET API)

    /// <summary>
    /// Primitive 233: Get file attributes as integer using .NET API.
    /// Argument: path (String)
    /// Returns: Integer attributes or nil if file doesn't exist
    /// </summary>
    public class GetFileAttributesEx : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Use .NET API - works for both files and directories
                FileAttributes attrs = File.GetAttributes(path);
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)attrs));
            }
            catch (FileNotFoundException)
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch (DirectoryNotFoundException)
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 234: GetReparseTag

    /// <summary>
    /// Primitive 234: Get reparse point tag.
    /// Argument: path (String)
    /// Returns: Integer reparse tag or 0 if not a reparse point
    /// </summary>
    public class GetReparseTag : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Use FindFirstFile to get the reparse tag from dwReserved0
                IntPtr findHandle = FindFirstFileW(path, out WIN32_FIND_DATA findData);

                if (findHandle == new IntPtr(-1)) // INVALID_HANDLE_VALUE
                {
                    return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0));
                }

                FindClose(findHandle);

                // dwReserved0 contains the reparse tag if FILE_ATTRIBUTE_REPARSE_POINT is set
                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) != 0)
                {
                    // Return as positive integer (may need LargeInteger for full tag)
                    int tag = (int)findData.dwReserved0;
                    return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(tag));
                }

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0));
            }
            catch
            {
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(0));
            }
        }
    }

    #endregion

    #region Primitive 235: GetSymbolicLinkTarget (uses .NET API)

    /// <summary>
    /// Primitive 235: Get symbolic link target path using .NET API.
    /// Argument: path (String)
    /// Returns: String target path or nil if not a symbolic link
    /// Note: .NET's ResolveLinkTarget works for both symlinks and junctions,
    /// so we first check the reparse tag to ensure it's actually a symlink.
    /// </summary>
    public class GetSymbolicLinkTarget : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // First verify it's a symlink (not a junction) using reparse tag
                IntPtr findHandle = FindFirstFileW(path, out WIN32_FIND_DATA findData);
                if (findHandle == new IntPtr(-1))
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }
                FindClose(findHandle);

                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0 ||
                    findData.dwReserved0 != IO_REPARSE_TAG_SYMLINK)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Use .NET API to get the link target (returnFinalTarget: false for immediate target)
                FileSystemInfo? target;
                if ((findData.dwFileAttributes & 0x10) != 0) // FILE_ATTRIBUTE_DIRECTORY
                {
                    target = Directory.ResolveLinkTarget(path, returnFinalTarget: false);
                }
                else
                {
                    target = File.ResolveLinkTarget(path, returnFinalTarget: false);
                }

                if (target == null)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, target.FullName));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 236: GetJunctionTarget (uses .NET API)

    /// <summary>
    /// Primitive 236: Get junction target path using .NET API.
    /// Argument: path (String)
    /// Returns: String target path or nil if not a junction
    /// Note: .NET's ResolveLinkTarget works for junctions too.
    /// </summary>
    public class GetJunctionTarget : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // First verify it's a junction using reparse tag
                IntPtr findHandle = FindFirstFileW(path, out WIN32_FIND_DATA findData);
                if (findHandle == new IntPtr(-1))
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }
                FindClose(findHandle);

                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0 ||
                    findData.dwReserved0 != IO_REPARSE_TAG_MOUNT_POINT)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Use .NET API - ResolveLinkTarget works for junctions
                FileSystemInfo? target = Directory.ResolveLinkTarget(path, returnFinalTarget: false);

                if (target == null)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, target.FullName));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 237: GetFinalPath (uses .NET API)

    /// <summary>
    /// Primitive 237: Get final path name, resolving all links using .NET API.
    /// Argument: path (String)
    /// Returns: String resolved path or nil on error
    /// </summary>
    public class GetFinalPath : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Check if it's a reparse point at all
                FileAttributes attrs = File.GetAttributes(path);

                if ((attrs & FileAttributes.ReparsePoint) == 0)
                {
                    // Not a reparse point, just return the full path
                    string fullPath = Path.GetFullPath(path);
                    return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, fullPath));
                }

                // Use .NET API with returnFinalTarget: true to follow entire chain
                FileSystemInfo? target;
                if ((attrs & FileAttributes.Directory) != 0)
                {
                    target = Directory.ResolveLinkTarget(path, returnFinalTarget: true);
                }
                else
                {
                    target = File.ResolveLinkTarget(path, returnFinalTarget: true);
                }

                if (target == null)
                {
                    // Link exists but target doesn't - return the target path anyway
                    // Fall back to immediate target
                    if ((attrs & FileAttributes.Directory) != 0)
                    {
                        target = Directory.ResolveLinkTarget(path, returnFinalTarget: false);
                    }
                    else
                    {
                        target = File.ResolveLinkTarget(path, returnFinalTarget: false);
                    }
                }

                if (target == null)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, target.FullName));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 238: GetHardLinkCount

    /// <summary>
    /// Primitive 238: Get number of hard links to a file.
    /// Argument: path (String)
    /// Returns: Integer link count (1 if no additional hard links)
    /// </summary>
    public class GetHardLinkCount : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                using var handle = CreateFileW(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(1));
                }

                if (GetFileInformationByHandle(handle, out BY_HANDLE_FILE_INFORMATION info))
                {
                    return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)info.NumberOfLinks));
                }

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(1));
            }
            catch
            {
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(1));
            }
        }
    }

    #endregion

    #region Primitive 239: DeleteReparsePoint

    /// <summary>
    /// Primitive 239: Delete a reparse point (junction or symbolic link).
    /// Argument: path (String)
    /// Returns: receiver on success, signals error on failure
    /// </summary>
    public class DeleteReparsePoint : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1) return PrimitiveResult.Fail(context.Metadata!);

            string? path = FilePrimitiveHelpers.ExtractString(context, context.Arguments[0]);
            if (path == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // First get the reparse tag
                IntPtr findHandle = FindFirstFileW(path, out WIN32_FIND_DATA findData);
                if (findHandle == new IntPtr(-1))
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }
                FindClose(findHandle);

                if ((findData.dwFileAttributes & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                uint reparseTag = findData.dwReserved0;

                // Open the reparse point for writing
                using var handle = CreateFileW(
                    path,
                    GENERIC_READ | 0x40000000, // GENERIC_WRITE
                    0,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                // Build minimal reparse buffer for deletion (just tag and zero length)
                byte[] reparseBuffer = new byte[8];
                BitConverter.GetBytes(reparseTag).CopyTo(reparseBuffer, 0);
                // ReparseDataLength = 0, Reserved = 0 are already zero

                bool success = DeviceIoControl(
                    handle,
                    FSCTL_DELETE_REPARSE_POINT,
                    reparseBuffer,
                    (uint)reparseBuffer.Length,
                    IntPtr.Zero,
                    0,
                    out _,
                    IntPtr.Zero);

                if (!success)
                {
                    return PrimitiveResult.Fail(context.Metadata!);
                }

                return PrimitiveResult.Ok(context.Receiver);
            }
            catch
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    #endregion
}
