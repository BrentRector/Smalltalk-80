using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Primitives for file system link operations: symbolic links, hard links, and junctions.
/// Uses Windows API via P/Invoke for NTFS link support.
/// </summary>
public static class LinkPrimitives
{
    #region Windows API Constants and Structures

    private const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    private const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x00000001;
    private const uint FILE_SHARE_WRITE = 0x00000002;
    private const uint FILE_SHARE_DELETE = 0x00000004;
    private const uint OPEN_EXISTING = 3;

    private const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;

    private const uint IO_REPARSE_TAG_SYMLINK = 0xA000000C;
    private const uint IO_REPARSE_TAG_MOUNT_POINT = 0xA0000003;

    private const uint FSCTL_GET_REPARSE_POINT = 0x000900A8;
    private const uint FSCTL_SET_REPARSE_POINT = 0x000900A4;
    private const uint FSCTL_DELETE_REPARSE_POINT = 0x000900AC;

    private const int MAX_PATH = 260;
    private const int MAXIMUM_REPARSE_DATA_BUFFER_SIZE = 16384;

    private const uint SYMBOLIC_LINK_FLAG_DIRECTORY = 0x1;
    private const uint SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE = 0x2;

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

    [StructLayout(LayoutKind.Sequential)]
    private struct REPARSE_DATA_BUFFER_HEADER
    {
        public uint ReparseTag;
        public ushort ReparseDataLength;
        public ushort Reserved;
    }

    #endregion

    #region Windows API P/Invoke

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateSymbolicLinkW(
        string lpSymlinkFileName,
        string lpTargetFileName,
        uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateHardLinkW(
        string lpFileName,
        string lpExistingFileName,
        IntPtr lpSecurityAttributes);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFileAttributesW(string lpFileName);

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
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle hFile,
        [Out] char[] lpszFilePath,
        uint cchFilePath,
        uint dwFlags);

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
        IntPtr lpInBuffer,
        uint nInBufferSize,
        [Out] byte[] lpOutBuffer,
        uint nOutBufferSize,
        out uint lpBytesReturned,
        IntPtr lpOverlapped);

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

    #region Primitive 230: CreateSymbolicLink

    /// <summary>
    /// Primitive 230: Create a symbolic link.
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
                uint flags = isDirectory ? SYMBOLIC_LINK_FLAG_DIRECTORY : 0;
                // Try with unprivileged flag first (Windows 10 1607+ with Developer Mode)
                flags |= SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;

                bool success = CreateSymbolicLinkW(linkPath, targetPath, flags);

                if (!success)
                {
                    // Retry without unprivileged flag (requires admin)
                    flags &= ~SYMBOLIC_LINK_FLAG_ALLOW_UNPRIVILEGED_CREATE;
                    success = CreateSymbolicLinkW(linkPath, targetPath, flags);
                }

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
            int pathBufferOffset = 8; // After ReparseTag, ReparseDataLength, Reserved
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

    #region Primitive 233: GetFileAttributes

    /// <summary>
    /// Primitive 233: Get file attributes as integer.
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
                uint attrs = GetFileAttributesW(path);

                if (attrs == 0xFFFFFFFF) // INVALID_FILE_ATTRIBUTES
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)attrs));
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

    #region Primitive 235: GetSymbolicLinkTarget

    /// <summary>
    /// Primitive 235: Get symbolic link target path.
    /// Argument: path (String)
    /// Returns: String target path or nil if not a symbolic link
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
                // Open the reparse point
                using var handle = CreateFileW(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Read the reparse data
                byte[] buffer = new byte[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
                bool success = DeviceIoControl(
                    handle,
                    FSCTL_GET_REPARSE_POINT,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)buffer.Length,
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (!success)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Parse the reparse buffer
                uint reparseTag = BitConverter.ToUInt32(buffer, 0);

                if (reparseTag != IO_REPARSE_TAG_SYMLINK)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Symlink structure after header:
                // SubstituteNameOffset (2), SubstituteNameLength (2)
                // PrintNameOffset (2), PrintNameLength (2)
                // Flags (4)
                // PathBuffer (variable)

                ushort substituteNameOffset = BitConverter.ToUInt16(buffer, 8);
                ushort substituteNameLength = BitConverter.ToUInt16(buffer, 10);
                ushort printNameOffset = BitConverter.ToUInt16(buffer, 12);
                ushort printNameLength = BitConverter.ToUInt16(buffer, 14);

                // Use print name (more human-readable)
                int pathBufferStart = 20; // After header and flags
                string targetPath = System.Text.Encoding.Unicode.GetString(
                    buffer,
                    pathBufferStart + printNameOffset,
                    printNameLength);

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, targetPath));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 236: GetJunctionTarget

    /// <summary>
    /// Primitive 236: Get junction target path.
    /// Argument: path (String)
    /// Returns: String target path or nil if not a junction
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
                // Open the reparse point
                using var handle = CreateFileW(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAG_OPEN_REPARSE_POINT,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Read the reparse data
                byte[] buffer = new byte[MAXIMUM_REPARSE_DATA_BUFFER_SIZE];
                bool success = DeviceIoControl(
                    handle,
                    FSCTL_GET_REPARSE_POINT,
                    IntPtr.Zero,
                    0,
                    buffer,
                    (uint)buffer.Length,
                    out uint bytesReturned,
                    IntPtr.Zero);

                if (!success)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Parse the reparse buffer
                uint reparseTag = BitConverter.ToUInt32(buffer, 0);

                if (reparseTag != IO_REPARSE_TAG_MOUNT_POINT)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                // Mount point structure after header:
                // SubstituteNameOffset (2), SubstituteNameLength (2)
                // PrintNameOffset (2), PrintNameLength (2)
                // PathBuffer (variable)

                ushort substituteNameOffset = BitConverter.ToUInt16(buffer, 8);
                ushort substituteNameLength = BitConverter.ToUInt16(buffer, 10);
                ushort printNameOffset = BitConverter.ToUInt16(buffer, 12);
                ushort printNameLength = BitConverter.ToUInt16(buffer, 14);

                // Use print name (more human-readable, without \??\ prefix)
                int pathBufferStart = 16; // After header (no flags field for mount points)
                string targetPath = System.Text.Encoding.Unicode.GetString(
                    buffer,
                    pathBufferStart + printNameOffset,
                    printNameLength);

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, targetPath));
            }
            catch
            {
                return PrimitiveResult.Ok(context.HeapManager.GetNil());
            }
        }
    }

    #endregion

    #region Primitive 237: GetFinalPath

    /// <summary>
    /// Primitive 237: Get final path name, resolving all links.
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
                // Determine if it's a file or directory
                uint attrs = GetFileAttributesW(path);
                if (attrs == 0xFFFFFFFF)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                uint flags = FILE_FLAG_BACKUP_SEMANTICS;
                if ((attrs & FILE_ATTRIBUTE_REPARSE_POINT) == 0)
                {
                    // Not a reparse point, just return the path as-is
                    string fullPath = Path.GetFullPath(path);
                    return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, fullPath));
                }

                // Open without OPEN_REPARSE_POINT to follow links
                using var handle = CreateFileW(
                    path,
                    GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE | FILE_SHARE_DELETE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    flags,
                    IntPtr.Zero);

                if (handle.IsInvalid)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                char[] buffer = new char[MAX_PATH * 2];
                uint result = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, 0);

                if (result == 0 || result > buffer.Length)
                {
                    return PrimitiveResult.Ok(context.HeapManager.GetNil());
                }

                string finalPath = new string(buffer, 0, (int)result);

                // Remove \\?\ prefix if present
                if (finalPath.StartsWith(@"\\?\"))
                {
                    finalPath = finalPath.Substring(4);
                }

                return PrimitiveResult.Ok(FilePrimitiveHelpers.CreateString(context, finalPath));
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
