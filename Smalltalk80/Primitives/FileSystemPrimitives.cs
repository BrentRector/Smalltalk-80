using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Smalltalk80;

/// <summary>
/// Implements file system and stream primitives for external I/O operations.
/// </summary>
public static class FileSystemPrimitives
{
    private static readonly Dictionary<int, FileStream> OpenFiles = new();
    private static int nextFileHandle = 1;

    /// <summary>
    /// Primitive 142: Set position in external stream.
    /// </summary>
    public static bool SetPosition(Context ctx)
    {
        if (ctx.Stack.Pop() is not int position || ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        // Get file handle from stream's name field
        if (stream.Fields.Length > 0 && stream.Fields[0] is int handle && OpenFiles.TryGetValue(handle, out var fileStream))
        {
            try
            {
                fileStream.Position = position;
                // Update stream's position field
                if (stream.Fields.Length > 1)
                    stream.Fields[1] = position;
                ctx.Stack.Push(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 143: Close external stream.
    /// </summary>
    public static bool CloseStream(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        // Get file handle from stream's name field
        if (stream.Fields.Length > 0 && stream.Fields[0] is int handle && OpenFiles.TryGetValue(handle, out var fileStream))
        {
            try
            {
                fileStream.Close();
                OpenFiles.Remove(handle);
                // Clear stream's name field to mark as closed
                stream.Fields[0] = null;
                ctx.Stack.Push(stream);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 144: Open external stream.
    /// </summary>
    public static bool OpenStream(Context ctx)
    {
        if (ctx.Stack.Pop() is not string mode || ctx.Stack.Pop() is not string fileName || ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        try
        {
            FileMode fileMode = mode switch
            {
                "r" => FileMode.Open,
                "w" => FileMode.Create,
                "r+" => FileMode.OpenOrCreate,
                _ => FileMode.Open
            };

            FileAccess access = mode switch
            {
                "r" => FileAccess.Read,
                "w" => FileAccess.Write,
                "r+" => FileAccess.ReadWrite,
                _ => FileAccess.Read
            };

            var fileStream = new FileStream(fileName, fileMode, access);
            int handle = nextFileHandle++;
            OpenFiles[handle] = fileStream;

            // Store handle in stream's name field
            if (stream.Fields.Length > 0)
                stream.Fields[0] = handle;
            // Initialize position
            if (stream.Fields.Length > 1)
                stream.Fields[1] = 0;
            // Clear atEnd flag
            if (stream.Fields.Length > 2)
                stream.Fields[2] = false;

            ctx.Stack.Push(stream);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Primitive 145: Read next character from file stream.
    /// </summary>
    public static bool ReadNextChar(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        // Get file handle from stream's name field
        if (stream.Fields.Length > 0 && stream.Fields[0] is int handle && OpenFiles.TryGetValue(handle, out var fileStream))
        {
            try
            {
                int charCode = fileStream.ReadByte();
                if (charCode == -1)
                {
                    // Set atEnd flag
                    if (stream.Fields.Length > 2)
                        stream.Fields[2] = true;
                    ctx.Stack.Push(null); // nil
                    return true;
                }

                // Update position
                if (stream.Fields.Length > 1)
                    stream.Fields[1] = (int)fileStream.Position;

                // Create Character object
                var character = new SmalltalkObject(SystemDictionary.GetClass("Character"), 1);
                character.Fields[0] = charCode;
                ctx.Stack.Push(character);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 146: Write character to file stream.
    /// </summary>
    public static bool WriteChar(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject character || ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        // Get file handle from stream's name field
        if (stream.Fields.Length > 0 && stream.Fields[0] is int handle && OpenFiles.TryGetValue(handle, out var fileStream))
        {
            try
            {
                // Get character code
                if (character.Fields.Length > 0 && character.Fields[0] is int charCode)
                {
                    fileStream.WriteByte((byte)charCode);

                    // Update position
                    if (stream.Fields.Length > 1)
                        stream.Fields[1] = (int)fileStream.Position;

                    ctx.Stack.Push(character);
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 147: Get file size.
    /// </summary>
    public static bool GetFileSize(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject stream)
            return false;

        // Get file handle from stream's name field
        if (stream.Fields.Length > 0 && stream.Fields[0] is int handle && OpenFiles.TryGetValue(handle, out var fileStream))
        {
            try
            {
                ctx.Stack.Push((int)fileStream.Length);
                return true;
            }
            catch
            {
                ctx.Stack.Push(0);
                return true;
            }
        }
        ctx.Stack.Push(0);
        return true;
    }

    /// <summary>
    /// Primitive 148: Get file size by name.
    /// </summary>
    public static bool GetFileSizeByName(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string fileName)
        {
            try
            {
                var info = new FileInfo(fileName);
                ctx.Stack.Push((int)info.Length);
                return true;
            }
            catch
            {
                ctx.Stack.Push(0);
                return true;
            }
        }
        ctx.Stack.Push(0);
        return true;
    }

    /// <summary>
    /// Primitive 149: Get file modification time.
    /// </summary>
    public static bool GetFileModificationTime(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string fileName)
        {
            try
            {
                var info = new FileInfo(fileName);
                // Return seconds since 1901 (Smalltalk epoch)
                var seconds = (long)(info.LastWriteTime - new DateTime(1901, 1, 1)).TotalSeconds;
                ctx.Stack.Push((int)seconds);
                return true;
            }
            catch
            {
                ctx.Stack.Push(null); // nil
                return true;
            }
        }
        ctx.Stack.Push(null); // nil
        return true;
    }

    /// <summary>
    /// Primitive 150: Test if file exists.
    /// </summary>
    public static bool FileExists(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string fileName)
        {
            try
            {
                bool exists = File.Exists(fileName);
                ctx.Stack.Push(exists);
                return true;
            }
            catch
            {
                ctx.Stack.Push(false);
                return true;
            }
        }
        ctx.Stack.Push(false);
        return true;
    }

    /// <summary>
    /// Primitive 151: Test if file is directory.
    /// </summary>
    public static bool IsDirectory(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string fileName)
        {
            try
            {
                bool isDir = Directory.Exists(fileName);
                ctx.Stack.Push(isDir);
                return true;
            }
            catch
            {
                ctx.Stack.Push(false);
                return true;
            }
        }
        ctx.Stack.Push(false);
        return true;
    }

    /// <summary>
    /// Primitive 152: Delete file.
    /// </summary>
    public static bool DeleteFile(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string fileName)
        {
            try
            {
                File.Delete(fileName);
                ctx.Stack.Push(file);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 153: Rename file.
    /// </summary>
    public static bool RenameFile(Context ctx)
    {
        if (ctx.Stack.Pop() is not string newName || ctx.Stack.Pop() is not SmalltalkObject file)
            return false;

        // Get file name from file object
        if (file.Fields.Length > 0 && file.Fields[0] is string oldName)
        {
            try
            {
                File.Move(oldName, newName);
                // Update the file object's name
                file.Fields[0] = newName;
                ctx.Stack.Push(file);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 154: Get directory entries.
    /// </summary>
    public static bool GetDirectoryEntries(Context ctx)
    {
        if (ctx.Stack.Pop() is not SmalltalkObject directory)
            return false;

        // Get directory path from directory object
        if (directory.Fields.Length > 0 && directory.Fields[0] is string dirPath)
        {
            try
            {
                var entries = Directory.GetFileSystemEntries(dirPath)
                    .Select(Path.GetFileName)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .ToArray();

                // Create Array of Strings
                var result = new SmalltalkObject(SystemDictionary.GetClass("Array"), entries.Length);
                for (int i = 0; i < entries.Length; i++)
                {
                    result.Fields[i] = CreateString(entries[i] ?? "");
                }
                ctx.Stack.Push(result);
                return true;
            }
            catch
            {
                // Return empty array on error
                var emptyArray = new SmalltalkObject(SystemDictionary.GetClass("Array"), 0);
                ctx.Stack.Push(emptyArray);
                return true;
            }
        }

        // Return empty array if no path
        var empty = new SmalltalkObject(SystemDictionary.GetClass("Array"), 0);
        ctx.Stack.Push(empty);
        return true;
    }

    /// <summary>
    /// Primitive 155: Create directory.
    /// </summary>
    public static bool CreateDirectory(Context ctx)
    {
        if (ctx.Stack.Pop() is not string dirName || ctx.Stack.Pop() is not SmalltalkObject directory)
            return false;

        // Get directory path from directory object
        if (directory.Fields.Length > 0 && directory.Fields[0] is string dirPath)
        {
            try
            {
                string fullPath = Path.Combine(dirPath, dirName);
                Directory.CreateDirectory(fullPath);
                ctx.Stack.Push(directory);
                return true;
            }
            catch
            {
                return false;
            }
        }
        return false;
    }

    /// <summary>
    /// Primitive 156: Get path separator.
    /// </summary>
    public static bool GetPathSeparator(Context ctx)
    {
        _ = ctx.Stack.Pop(); // directory object

        // Return platform-specific path separator as Character
        var separator = new SmalltalkObject(SystemDictionary.GetClass("Character"), 1);
        separator.Fields[0] = (int)Path.DirectorySeparatorChar;
        ctx.Stack.Push(separator);
        return true;
    }

    /// <summary>
    /// Primitive 157: Get current directory.
    /// </summary>
    public static bool GetCurrentDirectory(Context ctx)
    {
        try
        {
            string currentDir = Directory.GetCurrentDirectory();
            ctx.Stack.Push(CreateString(currentDir));
            return true;
        }
        catch
        {
            ctx.Stack.Push(CreateString("."));
            return true;
        }
    }

    /// <summary>
    /// Helper method to create a Smalltalk String object.
    /// </summary>
    private static SmalltalkObject CreateString(string value)
    {
        var stringObj = new SmalltalkObject(SystemDictionary.GetClass("String"), value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            stringObj.Fields[i] = (int)value[i];
        }
        return stringObj;
    }

    /// <summary>
    /// Clean up resources when shutting down.
    /// </summary>
    public static void Cleanup()
    {
        foreach (var fileStream in OpenFiles.Values)
        {
            try
            {
                fileStream.Close();
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        OpenFiles.Clear();
    }
}
