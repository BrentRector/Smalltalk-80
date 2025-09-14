using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Smalltalk80.Primitives.IO
{
    /// <summary>
    /// Implements file system primitives for Smalltalk-80 (primitives 142-157).
    /// These primitives enable file I/O operations required for self-hosting.
    /// </summary>
    public static class FileSystemPrimitives
    {
        /// <summary>
        /// Manages file handles for open streams.
        /// </summary>
        private static class FileHandles
        {
            private static int nextHandle = 1;
            private static readonly Dictionary<int, Stream> openStreams = new();
            private static readonly Dictionary<int, string> streamPaths = new();
            private static readonly Dictionary<int, FileMode> streamModes = new();

            /// <summary>
            /// Register a new stream and return its handle.
            /// </summary>
            public static int RegisterStream(Stream stream, string path, FileMode mode)
            {
                var handle = nextHandle++;
                openStreams[handle] = stream;
                streamPaths[handle] = path;
                streamModes[handle] = mode;
                return handle;
            }

            /// <summary>
            /// Get stream by handle.
            /// </summary>
            public static Stream? GetStream(int handle)
            {
                return openStreams.TryGetValue(handle, out var stream) ? stream : null;
            }

            /// <summary>
            /// Get stream path by handle.
            /// </summary>
            public static string? GetPath(int handle)
            {
                return streamPaths.TryGetValue(handle, out var path) ? path : null;
            }

            /// <summary>
            /// Close and unregister a stream.
            /// </summary>
            public static bool CloseStream(int handle)
            {
                if (openStreams.TryGetValue(handle, out var stream))
                {
                    try
                    {
                        stream.Close();
                        stream.Dispose();
                    }
                    catch
                    {
                        // Ignore close errors
                    }
                    
                    openStreams.Remove(handle);
                    streamPaths.Remove(handle);
                    streamModes.Remove(handle);
                    return true;
                }
                return false;
            }

            /// <summary>
            /// Close all open streams (for cleanup).
            /// </summary>
            public static void CloseAllStreams()
            {
                var handles = openStreams.Keys.ToList();
                foreach (var handle in handles)
                {
                    CloseStream(handle);
                }
            }
        }

        /// <summary>
        /// Primitive 142: Open a file stream.
        /// Stack: [receiver, filename, mode] -> [receiver]
        /// </summary>
        public static bool OpenStream(Context context)
        {
            try
            {
                if (context.Stack.Count < 3)
                    return false;

                // Pop arguments in reverse order (mode, filename, receiver)
                var modeObj = context.Stack.Pop() as SmalltalkObject;
                var filenameObj = context.Stack.Pop() as SmalltalkObject;
                var receiver = context.Stack.Pop() as SmalltalkObject;
                
                if (receiver == null || filenameObj == null || modeObj == null)
                    return false;

                // Extract filename string
                var filename = ExtractString(filenameObj);
                if (filename == null)
                    return false;

                // Extract mode symbol
                var mode = ExtractSymbol(modeObj);
                if (mode == null)
                    return false;

                // Normalize path for platform
                filename = NormalizePath(filename);

                // Open file based on mode
                Stream? stream = null;
                FileMode fileMode;

                switch (mode)
                {
                    case "read":
                    case "readOnly":
                        if (!File.Exists(filename))
                            return false;
                        stream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
                        fileMode = FileMode.Open;
                        break;

                    case "write":
                    case "writeOnly":
                        stream = new FileStream(filename, FileMode.Create, FileAccess.Write, FileShare.None);
                        fileMode = FileMode.Create;
                        break;

                    case "readWrite":
                    case "append":
                        stream = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                        fileMode = FileMode.OpenOrCreate;
                        break;

                    default:
                        return false;
                }

                if (stream == null)
                    return false;

                // Register stream and get handle
                var handle = FileHandles.RegisterStream(stream, filename, fileMode);

                // Store handle in receiver's instance variable (assumed to be at index 0)
                if (receiver.Fields.Length > 0)
                {
                    receiver.Fields[0] = handle;
                }

                // Store filename in receiver (assumed to be at index 1)
                if (receiver.Fields.Length > 1)
                {
                    receiver.Fields[1] = filenameObj;
                }

                // Store mode in receiver (assumed to be at index 2)
                if (receiver.Fields.Length > 2)
                {
                    receiver.Fields[2] = modeObj;
                }

                // Push receiver back onto stack
                context.Stack.Push(receiver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 143: Close a file stream.
        /// Arguments: receiver (FileStream with handle)
        /// </summary>
        public static bool CloseStream(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var receiver = context.Stack.Pop() as SmalltalkObject;
                if (receiver == null || receiver.Fields.Length == 0)
                    return false;

                // Get handle from receiver
                if (receiver.Fields[0] is not int handle)
                    return false;

                // Close the stream
                if (!FileHandles.CloseStream(handle))
                    return false;

                // Clear handle in receiver
                receiver.Fields[0] = 0;

                // Push receiver back onto stack
                context.Stack.Push(receiver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 144: Read next character from stream.
        /// Arguments: receiver (FileStream with handle)
        /// Returns: Character or nil at EOF
        /// </summary>
        public static bool ReadNextChar(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var receiver = context.Stack.Pop() as SmalltalkObject;
                if (receiver == null || receiver.Fields.Length == 0)
                    return false;

                // Get handle from receiver
                if (receiver.Fields[0] is not int handle)
                    return false;

                var stream = FileHandles.GetStream(handle);
                if (stream == null || !stream.CanRead)
                    return false;

                // Read one byte
                var byteValue = stream.ReadByte();
                if (byteValue == -1)
                {
                    // EOF - push nil
                    context.Stack.Push(null);
                }
                else
                {
                    // Create Character object
                    var character = CreateCharacter((char)byteValue);
                    context.Stack.Push(character);
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 145: Write a character to stream.
        /// Stack: [receiver, character] -> [receiver]
        /// </summary>
        public static bool WriteChar(Context context)
        {
            try
            {
                if (context.Stack.Count < 2)
                    return false;

                var charObj = context.Stack.Pop();
                var receiver = context.Stack.Pop() as SmalltalkObject;
                if (receiver == null || receiver.Fields.Length == 0)
                    return false;

                // Get handle from receiver
                if (receiver.Fields[0] is not int handle)
                    return false;

                var stream = FileHandles.GetStream(handle);
                if (stream == null || !stream.CanWrite)
                    return false;

                // Extract character value
                byte byteValue;
                if (charObj is char c)
                {
                    byteValue = (byte)c;
                }
                else if (charObj is int i)
                {
                    byteValue = (byte)i;
                }
                else if (charObj is SmalltalkObject obj && obj.Fields.Length > 0 && obj.Fields[0] is int asciiValue)
                {
                    byteValue = (byte)asciiValue;
                }
                else
                {
                    return false;
                }

                // Write byte to stream
                stream.WriteByte(byteValue);
                stream.Flush();

                // Push receiver back onto stack
                context.Stack.Push(receiver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 146: Get size of open file.
        /// Arguments: receiver (FileStream with handle)
        /// Returns: SmallInteger size in bytes
        /// </summary>
        public static bool GetFileSize(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var receiver = context.Stack.Pop() as SmalltalkObject;
                if (receiver == null || receiver.Fields.Length == 0)
                    return false;

                // Get handle from receiver
                if (receiver.Fields[0] is not int handle)
                    return false;

                var stream = FileHandles.GetStream(handle);
                if (stream == null)
                    return false;

                // Get file size
                var size = stream.Length;
                context.Stack.Push((int)Math.Min(size, int.MaxValue));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 147: Set file position.
        /// Stack: [receiver, position] -> [receiver]
        /// </summary>
        public static bool SetPosition(Context context)
        {
            try
            {
                if (context.Stack.Count < 2)
                    return false;

                var positionObj = context.Stack.Pop();
                var receiver = context.Stack.Pop() as SmalltalkObject;
                
                if (receiver == null || receiver.Fields.Length == 0)
                    return false;

                if (positionObj is not int position)
                    return false;

                // Get handle from receiver
                if (receiver.Fields[0] is not int handle)
                    return false;

                var stream = FileHandles.GetStream(handle);
                if (stream == null || !stream.CanSeek)
                    return false;

                // Set position
                stream.Position = position;

                // Push receiver back onto stack
                context.Stack.Push(receiver);
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Primitive 148: Get file size by name.
        /// Arguments: filename (String)
        /// Returns: SmallInteger size or -1 if not found
        /// </summary>
        public static bool GetFileSizeByName(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var filenameObj = context.Stack.Pop() as SmalltalkObject;
                if (filenameObj == null)
                    return false;

                var filename = ExtractString(filenameObj);
                if (filename == null)
                    return false;

                filename = NormalizePath(filename);

                if (File.Exists(filename))
                {
                    var info = new FileInfo(filename);
                    context.Stack.Push((int)Math.Min(info.Length, int.MaxValue));
                }
                else
                {
                    context.Stack.Push(-1);
                }

                return true;
            }
            catch
            {
                context.Stack.Push(-1);
                return true;
            }
        }

        /// <summary>
        /// Primitive 149: Check if file exists.
        /// Arguments: filename (String)
        /// Returns: Boolean
        /// </summary>
        public static bool FileExists(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var filenameObj = context.Stack.Pop() as SmalltalkObject;
                if (filenameObj == null)
                    return false;

                var filename = ExtractString(filenameObj);
                if (filename == null)
                    return false;

                filename = NormalizePath(filename);
                var exists = File.Exists(filename);
                
                context.Stack.Push(exists);
                return true;
            }
            catch
            {
                context.Stack.Push(false);
                return true;
            }
        }

        /// <summary>
        /// Primitive 150: Delete a file.
        /// Arguments: filename (String)
        /// Returns: Boolean success
        /// </summary>
        public static bool DeleteFile(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var filenameObj = context.Stack.Pop() as SmalltalkObject;
                if (filenameObj == null)
                    return false;

                var filename = ExtractString(filenameObj);
                if (filename == null)
                    return false;

                filename = NormalizePath(filename);

                if (File.Exists(filename))
                {
                    File.Delete(filename);
                    context.Stack.Push(true);
                }
                else
                {
                    context.Stack.Push(false);
                }

                return true;
            }
            catch
            {
                context.Stack.Push(false);
                return true;
            }
        }

        /// <summary>
        /// Primitive 151: Rename a file.
        /// Stack: [oldName, newName] -> [Boolean]
        /// </summary>
        public static bool RenameFile(Context context)
        {
            try
            {
                if (context.Stack.Count < 2)
                    return false;

                var newNameObj = context.Stack.Pop() as SmalltalkObject;
                var oldNameObj = context.Stack.Pop() as SmalltalkObject;
                
                if (oldNameObj == null || newNameObj == null)
                    return false;

                var oldName = ExtractString(oldNameObj);
                var newName = ExtractString(newNameObj);
                
                if (oldName == null || newName == null)
                    return false;

                oldName = NormalizePath(oldName);
                newName = NormalizePath(newName);

                if (File.Exists(oldName) && !File.Exists(newName))
                {
                    File.Move(oldName, newName);
                    context.Stack.Push(true);
                }
                else
                {
                    context.Stack.Push(false);
                }

                return true;
            }
            catch
            {
                context.Stack.Push(false);
                return true;
            }
        }

        /// <summary>
        /// Primitive 155: Delete a directory.
        /// Arguments: directory path (String)
        /// Returns: Boolean success
        /// </summary>
        public static bool DeleteDirectory(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var pathObj = context.Stack.Pop() as SmalltalkObject;
                if (pathObj == null)
                    return false;

                var path = ExtractString(pathObj);
                if (path == null)
                    return false;

                path = NormalizePath(path);

                if (Directory.Exists(path))
                {
                    Directory.Delete(path, false); // Don't delete recursively
                    context.Stack.Push(true);
                }
                else
                {
                    context.Stack.Push(false);
                }

                return true;
            }
            catch
            {
                context.Stack.Push(false);
                return true;
            }
        }

        /// <summary>
        /// Primitive 156: Check if path is a directory.
        /// Arguments: path (String)
        /// Returns: Boolean
        /// </summary>
        public static bool IsDirectory(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var pathObj = context.Stack.Pop() as SmalltalkObject;
                if (pathObj == null)
                    return false;

                var path = ExtractString(pathObj);
                if (path == null)
                    return false;

                path = NormalizePath(path);
                var isDir = Directory.Exists(path);
                
                context.Stack.Push(isDir);
                return true;
            }
            catch
            {
                context.Stack.Push(false);
                return true;
            }
        }

        /// <summary>
        /// Primitive 157: Get directory entries.
        /// Arguments: directory path (String)
        /// Returns: Array of String filenames
        /// </summary>
        public static bool GetDirectoryEntries(Context context)
        {
            try
            {
                if (context.Stack.Count < 1)
                    return false;

                var pathObj = context.Stack.Pop() as SmalltalkObject;
                if (pathObj == null)
                    return false;

                var path = ExtractString(pathObj);
                if (path == null)
                    return false;

                path = NormalizePath(path);

                if (!Directory.Exists(path))
                {
                    // Return empty array for non-existent directory
                    var emptyArray = new SmalltalkObject(null, 0);
                    context.Stack.Push(emptyArray);
                    return true;
                }

                // Get all entries (files and directories)
                var files = Directory.GetFiles(path);
                var dirs = Directory.GetDirectories(path);
                var allEntries = new string[files.Length + dirs.Length];
                
                // Add files
                for (int i = 0; i < files.Length; i++)
                {
                    allEntries[i] = Path.GetFileName(files[i]);
                }
                
                // Add directories (with trailing separator to distinguish them)
                for (int i = 0; i < dirs.Length; i++)
                {
                    allEntries[files.Length + i] = Path.GetFileName(dirs[i]) + Path.DirectorySeparatorChar;
                }

                // Create Smalltalk array of strings
                var arrayObj = new SmalltalkObject(null, allEntries.Length);
                for (int i = 0; i < allEntries.Length; i++)
                {
                    arrayObj.Fields[i] = CreateString(allEntries[i]);
                }

                context.Stack.Push(arrayObj);
                return true;
            }
            catch
            {
                // Return empty array on error
                var emptyArray = new SmalltalkObject(null, 0);
                context.Stack.Push(emptyArray);
                return true;
            }
        }

        #region Helper Methods

        /// <summary>
        /// Extract a C# string from a Smalltalk String object.
        /// </summary>
        private static string? ExtractString(SmalltalkObject obj)
        {
            if (obj == null)
                return null;

            // Check if it's a ByteArray or String
            if (obj.Fields == null || obj.Fields.Length == 0)
                return string.Empty;

            var sb = new StringBuilder();
            foreach (var field in obj.Fields)
            {
                if (field is int i)
                {
                    sb.Append((char)i);
                }
                else if (field is char c)
                {
                    sb.Append(c);
                }
                else if (field is byte b)
                {
                    sb.Append((char)b);
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Extract a symbol name from a Smalltalk Symbol object.
        /// </summary>
        private static string? ExtractSymbol(SmalltalkObject obj)
        {
            // Symbols are like strings in structure
            return ExtractString(obj);
        }

        /// <summary>
        /// Create a Smalltalk String object from a C# string.
        /// </summary>
        private static SmalltalkObject CreateString(string str)
        {
            var obj = new SmalltalkObject(null, str.Length);
            for (int i = 0; i < str.Length; i++)
            {
                obj.Fields[i] = (int)str[i];
            }
            return obj;
        }

        /// <summary>
        /// Create a Smalltalk Character object from a char.
        /// </summary>
        private static SmalltalkObject CreateCharacter(char c)
        {
            // Character object with ASCII value in first field
            var obj = new SmalltalkObject(null, 1);
            obj.Fields[0] = (int)c;
            return obj;
        }

        /// <summary>
        /// Normalize file path for current platform.
        /// </summary>
        private static string NormalizePath(string path)
        {
            // Convert forward slashes to platform separator
            path = path.Replace('/', Path.DirectorySeparatorChar);
            // Convert backslashes to platform separator
            path = path.Replace('\\', Path.DirectorySeparatorChar);
            
            // Expand relative paths
            if (!Path.IsPathRooted(path))
            {
                path = Path.Combine(Directory.GetCurrentDirectory(), path);
            }
            
            return Path.GetFullPath(path);
        }

        /// <summary>
        /// Cleanup method to close all streams on shutdown.
        /// </summary>
        public static void Cleanup()
        {
            FileHandles.CloseAllStreams();
        }

        #endregion
    }
}