using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Smalltalk80.Interpreter.ObjectModel;
using Smalltalk80.Interpreter.VM;

namespace Smalltalk80.Interpreter.Primitives;

/// <summary>
/// Input/Output primitives (primitives 90-108).
/// These primitives handle file operations, console I/O, and other external interactions.
/// Most are stubs as full I/O requires OS integration beyond the scope of the base interpreter.
/// Based on Blue Book specification for I/O primitives.
/// </summary>
public static class IOPrimitives
{
    /// <summary>
    /// Represents an open file with its stream and associated Smalltalk FileStream object.
    /// </summary>
    private sealed class FileHandle
    {
        public FileStream Stream { get; set; }
        public string FileName { get; set; }
        public FileAccess Access { get; set; }
        public OOP FileStreamObject { get; set; }

        public FileHandle(FileStream stream, string fileName, FileAccess access, OOP fileStreamObj)
        {
            Stream = stream;
            FileName = fileName;
            Access = access;
            FileStreamObject = fileStreamObj;
        }
    }

    /// <summary>
    /// Tracks open file handles for the VM.
    /// </summary>
    private static readonly Dictionary<int, FileHandle> openFiles = [];
    private static int nextFileHandle = 1;

    /// <summary>
    /// Allocates a new file handle.
    /// </summary>
    private static int AllocateFileHandle() => nextFileHandle++;

    /// <summary>
    /// Primitive 90: File at:put: (write byte to file).
    /// Writes a byte at a specific position in a file.
    /// </summary>
    /// <param name="receiver">The FileStream object.</param>
    /// <param name="arguments">Two arguments: position and byte value.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The byte written or failure.</returns>
    public static PrimitiveResult PrimitiveFileAtPut(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        int position = arguments[0].SmallIntegerValue;
        int byteValue = arguments[1].SmallIntegerValue;
        
        if (byteValue < 0 || byteValue > 255)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        if ((fileHandle.Access & FileAccess.Write) == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileHandle.Stream.Position = position;
            fileHandle.Stream.WriteByte((byte)byteValue);
            
            // Update position in FileStream object
            var obj = context.Memory.GetObject(receiver);
            obj?.SetField(1, OOP.FromSmallInteger((int)fileHandle.Stream.Position));
            
            return arguments[1];
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 91: File at: (read byte from file).
    /// Reads a byte at a specific position in a file.
    /// </summary>
    /// <param name="receiver">The FileStream object.</param>
    /// <param name="arguments">Single argument: position.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The byte read as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFileAt(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        int position = arguments[0].SmallIntegerValue;
        
        if ((fileHandle.Access & FileAccess.Read) == 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileHandle.Stream.Position = position;
            int byteValue = fileHandle.Stream.ReadByte();
            
            if (byteValue == -1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
            
            // Update position in FileStream object
            var obj = context.Memory.GetObject(receiver);
            obj?.SetField(1, OOP.FromSmallInteger((int)fileHandle.Stream.Position));
            
            return OOP.FromSmallInteger(byteValue);
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 92: File size.
    /// Returns the size of a file in bytes.
    /// </summary>
    /// <param name="receiver">The FileStream object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>File size as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFileSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            long size = fileHandle.Stream.Length;
            
            // Check if size fits in SmallInteger
            if (size > int.MaxValue)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            return OOP.FromSmallInteger((int)size);
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 93: File position.
    /// Returns the current position in a file.
    /// </summary>
    /// <param name="receiver">The FileStream object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Position as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFilePosition(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            long position = fileHandle.Stream.Position;
            
            // Check if position fits in SmallInteger
            if (position > int.MaxValue)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            
            return OOP.FromSmallInteger((int)position);
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 94: File position: (set position).
    /// Sets the current position in a file.
    /// </summary>
    /// <param name="receiver">The FileStream object.</param>
    /// <param name="arguments">Single argument: new position.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver or failure.</returns>
    public static PrimitiveResult PrimitiveFileSetPosition(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        int newPosition = arguments[0].SmallIntegerValue;
        
        try
        {
            fileHandle.Stream.Position = newPosition;
            
            // Update position in FileStream object
            var obj = context.Memory.GetObject(receiver);
            obj?.SetField(1, OOP.FromSmallInteger(newPosition));
            
            return receiver;
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 95: Directory lookup.
    /// Lists files in a directory.
    /// </summary>
    /// <param name="receiver">Directory path as String.</param>
    /// <param name="arguments">Pattern argument (optional).</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Array of file names or failure.</returns>
    public static PrimitiveResult PrimitiveDirectoryLookup(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // Stub - directory operations require full String support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 96: Console output.
    /// Writes a character to the console.
    /// </summary>
    /// <param name="receiver">Character value as SmallInteger.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver.</returns>
    public static PrimitiveResult PrimitiveConsoleOutput(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int charValue = receiver.SmallIntegerValue;
        
        if (charValue < 0 || charValue > 255)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        Console.Write((char)charValue);
        return receiver;
    }

    /// <summary>
    /// Primitive 97: Console input.
    /// Reads a character from the console.
    /// </summary>
    /// <param name="receiver">Ignored.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Character value as SmallInteger.</returns>
    public static PrimitiveResult PrimitiveConsoleInput(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        try
        {
            int charValue = Console.Read();
            
            if (charValue == -1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            
            return OOP.FromSmallInteger(charValue & 0xFF);
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 98: File open.
    /// Opens a file and returns a FileStream object.
    /// </summary>
    /// <param name="receiver">File path as String.</param>
    /// <param name="arguments">Mode argument: 'r' for read, 'w' for write, 'rw' for read-write.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>FileStream object or failure.</returns>
    public static PrimitiveResult PrimitiveFileOpen(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Get file path from receiver (String object)
        var filePathString = GetStringFromOOP(receiver, context);
        if (filePathString == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        // Get mode from argument (Symbol or String)
        var modeString = GetStringFromOOP(arguments[0], context);
        if (modeString == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        try
        {
            FileMode fileMode;
            FileAccess fileAccess;
            
            // Parse mode string
            switch (modeString.ToLower())
            {
                case "r":
                case "read":
                    fileMode = FileMode.Open;
                    fileAccess = FileAccess.Read;
                    break;
                case "w":
                case "write":
                    fileMode = FileMode.Create;
                    fileAccess = FileAccess.Write;
                    break;
                case "rw":
                case "readwrite":
                    fileMode = FileMode.OpenOrCreate;
                    fileAccess = FileAccess.ReadWrite;
                    break;
                case "a":
                case "append":
                    fileMode = FileMode.Append;
                    fileAccess = FileAccess.Write;
                    break;
                default:
                    return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
            }
            
            // Open the file
            var fileStream = new FileStream(filePathString, fileMode, fileAccess);
            
            // Create a FileStream object in Smalltalk
            var fileStreamClass = context.GetClass("FileStream");
            if (fileStreamClass.IsNil)
            {
                fileStream.Close();
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            }
            
            // Create instance with proper structure
            // FileStream has instance variables from ExternalStream:
            // collection, position, readLimit, writeLimit, file (from ExternalStream)
            // Plus its own: name
            var fileStreamOop = context.Memory.AllocateObject(fileStreamClass, 6);
            var fileStreamObj = context.Memory.GetObject(fileStreamOop);
            if (fileStreamObj == null)
            {
                fileStream.Close();
                return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
            }
            
            // Allocate file handle
            int handle = AllocateFileHandle();
            var fileHandle = new FileHandle(fileStream, filePathString, fileAccess, fileStreamOop);
            openFiles[handle] = fileHandle;
            
            // Set instance variables
            // Index 0: collection (not used for file streams)
            fileStreamObj.SetField(0, OOP.Nil);
            // Index 1: position
            fileStreamObj.SetField(1, OOP.FromSmallInteger(0));
            // Index 2: readLimit
            fileStreamObj.SetField(2, OOP.FromSmallInteger((int)fileStream.Length));
            // Index 3: writeLimit
            fileStreamObj.SetField(3, OOP.FromSmallInteger((int)fileStream.Length));
            // Index 4: file handle (as SmallInteger)
            fileStreamObj.SetField(4, OOP.FromSmallInteger(handle));
            // Index 5: name (String)
            var nameString = CreateString(context, filePathString);
            fileStreamObj.SetField(5, nameString);
            
            return fileStreamOop;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IOPrimitives] File open failed: {ex.Message}");
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 99: File close.
    /// Closes an open file.
    /// </summary>
    /// <param name="receiver">FileStream object.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver or failure.</returns>
    public static PrimitiveResult PrimitiveFileClose(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        var fileHandle = GetFileHandleFromObject(receiver, context);
        if (fileHandle == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileHandle.Stream.Close();
            
            // Remove from open files dictionary
            var obj = context.Memory.GetObject(receiver);
            if (obj != null)
            {
                var handleOop = obj.GetField(4);
                if (handleOop.IsSmallInteger)
                {
                    openFiles.Remove(handleOop.SmallIntegerValue);
                }
                // Clear the handle field
                obj.SetField(4, OOP.Nil);
            }
            
            return receiver;
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 100: File delete.
    /// Deletes a file.
    /// </summary>
    /// <param name="receiver">File path as String.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver or failure.</returns>
    public static PrimitiveResult PrimitiveFileDelete(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        // Get file path from receiver (String object)
        var filePathString = GetStringFromOOP(receiver, context);
        if (filePathString == null)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        try
        {
            if (File.Exists(filePathString))
            {
                File.Delete(filePathString);
            }
            return receiver;
        }
        catch (Exception)
        {
            return PrimitiveResult.CreateFailure(PrimitiveFailure.InternalError);
        }
    }

    /// <summary>
    /// Primitive 101-108: Additional I/O primitives.
    /// These are typically system-specific or optional.
    /// </summary>
    public static PrimitiveResult PrimitiveIOReserved(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Helper method to get FileHandle from a FileStream object.
    /// </summary>
    private static FileHandle? GetFileHandleFromObject(OOP fileStreamObj, IPrimitiveContext context)
    {
        if (fileStreamObj.IsNil)
            return null;
            
        var obj = context.Memory.GetObject(fileStreamObj);
        if (obj == null)
            return null;
            
        // Get file handle from field 4
        var handleOop = obj.GetField(4);
        if (!handleOop.IsSmallInteger)
            return null;
            
        int handle = handleOop.SmallIntegerValue;
        return openFiles.TryGetValue(handle, out var fileHandle) ? fileHandle : null;
    }
    
    /// <summary>
    /// Helper method to extract a C# string from a Smalltalk String or Symbol OOP.
    /// </summary>
    private static string? GetStringFromOOP(OOP oop, IPrimitiveContext context)
    {
        if (oop.IsNil)
            return null;
            
        var obj = context.Memory.GetObject(oop);
        if (obj == null)
            return null;
            
        // Check if this is a String or Symbol
        var classOop = obj.Header.ClassOOP;
        var classObj = context.Memory.GetObject(classOop);
        if (classObj == null)
            return null;
            
        // For String/Symbol, the bytes are typically stored as the object's indexed fields
        var sb = new StringBuilder();
        for (int i = 0; i < obj.Fields.Length; i++)
        {
            var field = obj.GetField(i);
            if (field.IsSmallInteger)
            {
                sb.Append((char)field.SmallIntegerValue);
            }
        }
        
        return sb.ToString();
    }
    
    /// <summary>
    /// Helper method to create a Smalltalk String from a C# string.
    /// </summary>
    private static OOP CreateString(IPrimitiveContext context, string value)
    {
        var stringClass = context.GetClass("String");
        if (stringClass.IsNil)
            return OOP.Nil;
            
        var stringOop = context.Memory.AllocateObject(stringClass, value.Length);
        var stringObj = context.Memory.GetObject(stringOop);
        if (stringObj == null)
            return OOP.Nil;
        
        // Store each character as a SmallInteger
        for (int i = 0; i < value.Length; i++)
        {
            stringObj.SetField(i, OOP.FromSmallInteger(value[i]));
        }
        
        return stringOop;
    }

    /// <summary>
    /// Registers all I/O primitives in the primitive table.
    /// </summary>
    /// <param name="table">The primitive table to register into.</param>
    public static void RegisterAll(PrimitiveTable table)
    {
        // File operations
        table.Register(90, "primitiveFileAtPut", PrimitiveFileAtPut);
        table.Register(91, "primitiveFileAt", PrimitiveFileAt);
        table.Register(92, "primitiveFileSize", PrimitiveFileSize);
        table.Register(93, "primitiveFilePosition", PrimitiveFilePosition);
        table.Register(94, "primitiveFileSetPosition", PrimitiveFileSetPosition);
        table.Register(95, "primitiveDirectoryLookup", PrimitiveDirectoryLookup);
        
        // Console I/O
        table.Register(96, "primitiveConsoleOutput", PrimitiveConsoleOutput);
        table.Register(97, "primitiveConsoleInput", PrimitiveConsoleInput);
        
        // File management
        table.Register(98, "primitiveFileOpen", PrimitiveFileOpen);
        table.Register(99, "primitiveFileClose", PrimitiveFileClose);
        table.Register(100, "primitiveFileDelete", PrimitiveFileDelete);
        
        // Reserved
        for (int i = 101; i <= 108; i++)
        {
            table.Register(i, $"primitiveIOReserved{i}", PrimitiveIOReserved);
        }
    }
}