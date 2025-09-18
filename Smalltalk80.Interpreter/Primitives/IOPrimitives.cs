using System;
using System.Collections.Generic;
using System.IO;
using Smalltalk80.Interpreter.ObjectModel;

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
    /// Tracks open file handles for the VM.
    /// </summary>
    private static readonly Dictionary<int, FileStream> openFiles = [];
    private static int nextFileHandle = 1;

    /// <summary>
    /// Allocates a new file handle.
    /// </summary>
    private static int AllocateFileHandle() => nextFileHandle++;

    /// <summary>
    /// Primitive 90: File at:put: (write byte to file).
    /// Writes a byte at a specific position in a file.
    /// </summary>
    /// <param name="receiver">The file handle (SmallInteger).</param>
    /// <param name="arguments">Two arguments: position and byte value.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The byte written or failure.</returns>
    public static PrimitiveResult PrimitiveFileAtPut(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 2)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger || !arguments[1].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        int position = arguments[0].SmallIntegerValue;
        int byteValue = arguments[1].SmallIntegerValue;
        
        if (byteValue < 0 || byteValue > 255)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadArgument);
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileStream.Position = position;
            fileStream.WriteByte((byte)byteValue);
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
    /// <param name="receiver">The file handle (SmallInteger).</param>
    /// <param name="arguments">Single argument: position.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The byte read as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFileAt(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        int position = arguments[0].SmallIntegerValue;
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileStream.Position = position;
            int byteValue = fileStream.ReadByte();
            
            if (byteValue == -1)
                return PrimitiveResult.CreateFailure(PrimitiveFailure.BadIndex);
            
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
    /// <param name="receiver">The file handle (SmallInteger).</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>File size as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFileSize(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            long size = fileStream.Length;
            
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
    /// <param name="receiver">The file handle (SmallInteger).</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>Position as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFilePosition(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            long position = fileStream.Position;
            
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
    /// <param name="receiver">The file handle (SmallInteger).</param>
    /// <param name="arguments">Single argument: new position.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver or failure.</returns>
    public static PrimitiveResult PrimitiveFileSetPosition(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 1)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger || !arguments[0].IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        int newPosition = arguments[0].SmallIntegerValue;
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileStream.Position = newPosition;
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
    /// Opens a file and returns a handle.
    /// </summary>
    /// <param name="receiver">File path as String.</param>
    /// <param name="arguments">Mode argument (read/write).</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>File handle as SmallInteger or failure.</returns>
    public static PrimitiveResult PrimitiveFileOpen(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        // Stub - requires full String support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
    }

    /// <summary>
    /// Primitive 99: File close.
    /// Closes an open file.
    /// </summary>
    /// <param name="receiver">File handle as SmallInteger.</param>
    /// <param name="arguments">No arguments.</param>
    /// <param name="context">Execution context for accessing VM services.</param>
    /// <returns>The receiver or failure.</returns>
    public static PrimitiveResult PrimitiveFileClose(OOP receiver, OOP[] arguments, IPrimitiveContext context)
    {
        if (arguments.Length != 0)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongNumberOfArguments);
        
        if (!receiver.IsSmallInteger)
            return PrimitiveResult.CreateFailure(PrimitiveFailure.WrongArgumentType);
        
        int handle = receiver.SmallIntegerValue;
        
        if (!openFiles.TryGetValue(handle, out var fileStream))
            return PrimitiveResult.CreateFailure(PrimitiveFailure.BadReceiver);
        
        try
        {
            fileStream.Close();
            openFiles.Remove(handle);
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
        // Stub - requires full String support
        return PrimitiveResult.CreateFailure(PrimitiveFailure.NotImplemented);
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