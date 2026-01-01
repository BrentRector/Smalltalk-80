using System.Diagnostics;
using Smalltalk80.Interpreter.Core;
using Smalltalk80.Interpreter.Shared.Types;

namespace Smalltalk80.Interpreter.Core.VM.Primitives;

/// <summary>
/// Core file I/O primitives: open, close, read, write, seek.
/// </summary>
public static class FileStreamPrimitives
{
    /// <summary>
    /// Primitive 121: Open a file (CreateFile equivalent).
    /// Supports TWO calling conventions:
    /// 1. WindowsFile>>primOpen:mode: - 2 args: path (String), mode (String like 'r', 'w', 'rw', 'a')
    /// 2. WindowsFileReference>>primOpen:accessMode:shareMode:createMode: - 4 args: path, access, share, create (Integers)
    /// Returns: Integer handle or -1 on failure.
    /// </summary>
    public class FileOpen : IPrimitive
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
                FilePrimitiveDiagnostics.Record("FileOpen", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            int accessMode, shareMode, createMode;
            string? path;

            if (context.Arguments.Length == 2)
            {
                // WindowsFile>>primOpen:mode: - 2 args: path (String), mode (String)
                var pathOOP = context.Arguments[0];
                var modeOOP = context.Arguments[1];

                if (pathOOP.IsSmallInteger || modeOOP.IsSmallInteger)
                    return PrimitiveResult.Fail(context.Metadata!);

                path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
                string? modeStr = FilePrimitiveHelpers.ExtractString(context, modeOOP);

                if (path == null || modeStr == null)
                    return PrimitiveResult.Fail(context.Metadata!);

                // Convert mode string to access/share/create modes
                switch (modeStr.ToLower())
                {
                    case "r":
                        accessMode = 1;  // GENERIC_READ
                        shareMode = 1;   // FILE_SHARE_READ
                        createMode = 3;  // OPEN_EXISTING
                        break;
                    case "w":
                        accessMode = 2;  // GENERIC_WRITE
                        shareMode = 0;   // No sharing
                        createMode = 2;  // CREATE_ALWAYS
                        break;
                    case "rw":
                        accessMode = 3;  // GENERIC_READ | GENERIC_WRITE
                        shareMode = 0;   // No sharing
                        createMode = 3;  // OPEN_EXISTING
                        break;
                    case "a":
                        accessMode = 2;  // GENERIC_WRITE
                        shareMode = 0;   // No sharing
                        createMode = 4;  // OPEN_ALWAYS
                        break;
                    default:
                        return PrimitiveResult.Fail(context.Metadata!);
                }
            }
            else if (context.Arguments.Length == 4)
            {
                // WindowsFileReference>>primOpen:accessMode:shareMode:createMode: - 4 args
                var pathOOP = context.Arguments[0];
                var accessOOP = context.Arguments[1];
                var shareOOP = context.Arguments[2];
                var createOOP = context.Arguments[3];

                if (pathOOP.IsSmallInteger)
                    return PrimitiveResult.Fail(context.Metadata!);
                if (!accessOOP.IsSmallInteger || !shareOOP.IsSmallInteger || !createOOP.IsSmallInteger)
                    return PrimitiveResult.Fail(context.Metadata!);

                path = FilePrimitiveHelpers.ExtractString(context, pathOOP);
                if (path == null)
                    return PrimitiveResult.Fail(context.Metadata!);

                accessMode = accessOOP.AsSmallInteger();
                shareMode = shareOOP.AsSmallInteger();
                createMode = createOOP.AsSmallInteger();
            }
            else
            {
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // For read-only operations (OPEN_EXISTING), check if file exists in Data folder
            // createMode 3 = OPEN_EXISTING (read), createMode 2 = CREATE_ALWAYS (write)
            string actualPath = path;
            if (createMode == 3 && !File.Exists(path))
            {
                // Try Data folder fallback for resource files (.form, .sources, etc.)
                // Use executable directory, not current working directory
                string exeDir = AppDomain.CurrentDomain.BaseDirectory;
                string dataPath = Path.Combine(exeDir, "Data", path);
                if (File.Exists(dataPath))
                {
                    actualPath = dataPath;
                }
            }

            int handle = FileHandleManager.OpenFile(actualPath, accessMode, shareMode, createMode);
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
            var sw = Stopwatch.StartNew();
            try
            {
                return ExecuteCore(context);
            }
            finally
            {
                sw.Stop();
                FilePrimitiveDiagnostics.Record("FileClose", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 1)
                return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            if (!handleOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            FileHandleManager.CloseFile(handle);

            return PrimitiveResult.Ok(context.HeapManager.GetNil());
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
            var sw = Stopwatch.StartNew();
            try
            {
                return ExecuteCore(context);
            }
            finally
            {
                sw.Stop();
                FilePrimitiveDiagnostics.Record("FileRead", sw.ElapsedTicks);
            }
        }

        private PrimitiveResult ExecuteCore(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 3)
                return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            var bufferOOP = context.Arguments[1];
            var countOOP = context.Arguments[2];

            if (!handleOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            if (bufferOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);
            if (!countOOP.IsSmallInteger)
                return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            int count = countOOP.AsSmallInteger();

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
                return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Get buffer size
                int bufferSize = context.HeapManager.GetIndexedSize(bufferOOP);
                if (count > bufferSize) count = bufferSize;

                // Read into temporary buffer
                byte[] tempBuffer = new byte[count];
                int bytesRead = stream.Read(tempBuffer, 0, count);

                // Copy to Smalltalk ByteArray
                for (int i = 0; i < bytesRead; i++)
                {
                    context.HeapManager.SetIndexedByte(bufferOOP, i, tempBuffer[i]);
                }

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(bytesRead));
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
    /// Supports TWO calling conventions:
    /// 1. 2 args: handle (Integer), offset (Integer) - origin defaults to 0 (Begin)
    /// 2. 3 args: handle (Integer), offset (Integer), origin (Integer: 0=Begin, 1=Current, 2=End)
    /// Returns: Integer new position or -1 on failure.
    /// </summary>
    public class FileSeek : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length < 2 || context.Arguments.Length > 3)
            {
                Console.WriteLine($"[Primitive 124] FAIL: wrong argument count {context.Arguments.Length}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var handleOOP = context.Arguments[0];
            var offsetOOP = context.Arguments[1];

            if (!handleOOP.IsSmallInteger)
            {
                Console.WriteLine($"[Primitive 124] FAIL: handle not SmallInteger: {handleOOP}");
                return PrimitiveResult.Fail(context.Metadata!);
            }
            if (!offsetOOP.IsSmallInteger)
            {
                Console.WriteLine($"[Primitive 124] FAIL: offset not SmallInteger: {offsetOOP}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            // Default origin to 0 (Begin) if not provided
            int origin = 0;
            if (context.Arguments.Length == 3)
            {
                var originOOP = context.Arguments[2];
                if (!originOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
                origin = originOOP.AsSmallInteger();
            }

            int handle = handleOOP.AsSmallInteger();
            int offset = offsetOOP.AsSmallInteger();

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
            {
                Console.WriteLine($"[Primitive 124] FAIL: stream not found for handle={handle}. OpenFileCount={FileHandleManager.OpenFileCount}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            try
            {
                SeekOrigin seekOrigin = origin switch
                {
                    0 => SeekOrigin.Begin,
                    1 => SeekOrigin.Current,
                    2 => SeekOrigin.End,
                    _ => SeekOrigin.Begin
                };

                // Get BOM offset once for use in both seek and return value calculation
                int bomOffset = FileHandleManager.GetBomOffset(handle);

                // Apply BOM offset for seeks from beginning
                // This ensures source positions in CompiledMethods (which don't account for BOM) work correctly
                int actualOffset = offset;
                if (seekOrigin == SeekOrigin.Begin && bomOffset > 0)
                {
                    actualOffset = offset + bomOffset;
                }

                long newPosition = stream.Seek(actualOffset, seekOrigin);

                // Return LOGICAL position, not PHYSICAL position
                // This ensures Smalltalk code works with BOM-transparent positions
                // and primitive 132 (read byte) can correctly add BOM offset
                // For ALL seek origins, the returned position is physical, so subtract BOM
                long logicalPosition = bomOffset > 0 ? newPosition - bomOffset : newPosition;

                // Clamp to SmallInteger range
                if (logicalPosition > 0x3FFFFFFF) logicalPosition = 0x3FFFFFFF;
                if (logicalPosition < 0) logicalPosition = 0; // Can happen if seeking before BOM

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger((int)logicalPosition));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Primitive 124] EXCEPTION: handle={handle}, offset={offset}, error={ex.Message}");
                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(-1));
            }
        }
    }

    /// <summary>
    /// Primitive 132: Read a single byte from file at position.
    /// Receiver: WindowsFile instance.
    /// Arguments: handle (Integer), position (Integer).
    /// Returns: Integer byte value (0-255), or -1 on EOF/error.
    /// </summary>
    public class FileReadByte : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 2)
            {
                // Don't log this - happens every time next is called (expected fallback)
                return PrimitiveResult.Fail(context.Metadata!);
            }

            var handleOOP = context.Arguments[0];
            var positionOOP = context.Arguments[1];

            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!positionOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            int position = positionOOP.AsSmallInteger();

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null)
            {
                Console.WriteLine($"[Primitive 132] FAIL: stream not found for handle={handle}");
                return PrimitiveResult.Fail(context.Metadata!);
            }

            try
            {
                // Apply BOM offset so source positions in CompiledMethods work correctly
                int actualPosition = position + FileHandleManager.GetBomOffset(handle);
                stream.Position = actualPosition;
                int byteValue = stream.ReadByte();

                return PrimitiveResult.Ok(SmalltalkOOP.FromSmallInteger(byteValue));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Primitive 132] EXCEPTION: handle={handle}, position={position}, error={ex.Message}");
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }

    /// <summary>
    /// Primitive 134: Write a single byte to file at position.
    /// Receiver: WindowsFile instance.
    /// Arguments: handle (Integer), position (Integer), byte (Integer 0-255).
    /// Returns: the byte value written.
    /// </summary>
    public class FileWriteByte : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 3) return PrimitiveResult.Fail(context.Metadata!);

            var handleOOP = context.Arguments[0];
            var positionOOP = context.Arguments[1];
            var byteOOP = context.Arguments[2];

            if (!handleOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!positionOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);
            if (!byteOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            int handle = handleOOP.AsSmallInteger();
            int position = positionOOP.AsSmallInteger();
            int byteValue = byteOOP.AsSmallInteger();

            if (byteValue < 0 || byteValue > 255) return PrimitiveResult.Fail(context.Metadata!);

            var stream = FileHandleManager.GetStream(handle);
            if (stream == null) return PrimitiveResult.Fail(context.Metadata!);

            try
            {
                // Apply BOM offset for consistency with read operations
                int actualPosition = position + FileHandleManager.GetBomOffset(handle);
                stream.Position = actualPosition;
                stream.WriteByte((byte)byteValue);
                return PrimitiveResult.Ok(byteOOP);
            }
            catch
            {
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
    /// Primitive 156: Set the snapshot file path.
    /// Called by WindowsFile>>beSnapshotFile to register the path for primitive 97.
    /// Receiver: WindowsFile instance with fileName instance variable.
    /// Returns: self on success.
    /// </summary>
    public class SetSnapshotFile : IPrimitive
    {
        public PrimitiveResult Execute(PrimitiveExecutionContext context)
        {
            if (context.Arguments.Length != 0) return PrimitiveResult.Fail(context.Metadata!);

            // Receiver is a WindowsFile with fileName in instance variable
            var receiver = context.Receiver;
            if (receiver.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

            // WindowsFile has fileName as instance variable 0
            // Instance variables: fileName fileDirectory serialNumber lastPageNumber binary readWrite error
            try
            {
                var fileNameOOP = context.HeapManager.GetInstanceVariable(receiver, 0);
                if (fileNameOOP.IsSmallInteger) return PrimitiveResult.Fail(context.Metadata!);

                string? fileName = FilePrimitiveHelpers.ExtractString(context, fileNameOOP);
                if (fileName == null) return PrimitiveResult.Fail(context.Metadata!);

                // Store the snapshot file path for primitive 97
                ImageWriter.SnapshotFilePath = fileName;
                Console.WriteLine($"[Primitive 156] Snapshot file set to: {fileName}");

                return PrimitiveResult.Ok(receiver);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Primitive 156] Error: {ex.Message}");
                return PrimitiveResult.Fail(context.Metadata!);
            }
        }
    }
}
