using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Smalltalk80.Interpreter.Memory
{
    /// <summary>
    /// Arena layout assumptions for CompiledMethod and other compact objects.
    /// </summary>
    public static class ArenaLayout
    {
        public const int HeaderSize = 8 + 4 + 4; // classPtr + literalCount + numTemps (16 bytes on 64-bit)
        public const int ClassPtrOffset = 0;
        public const int LiteralCountOffset = 8;  // After 8-byte pointer
        public const int NumTempsOffset = 12;      // After 4-byte literal count
        public const int LiteralsOffset = 16;      // Start of literals array
    }

    /// <summary>
    /// Managed handle representing an arena slot index.
    /// </summary>
    public readonly struct ArenaHandle : IEquatable<ArenaHandle>
    {
        public readonly int Index;
        
        public ArenaHandle(int index)
        {
            Index = index;
        }

        public bool IsValid => Index >= 0;

        public override bool Equals(object obj) => obj is ArenaHandle handle && Equals(handle);
        public bool Equals(ArenaHandle other) => Index == other.Index;
        public override int GetHashCode() => Index.GetHashCode();
        public static bool operator ==(ArenaHandle left, ArenaHandle right) => left.Equals(right);
        public static bool operator !=(ArenaHandle left, ArenaHandle right) => !left.Equals(right);
        
        public override string ToString() => $"ArenaHandle({Index})";
    }

    /// <summary>
    /// Minimal arena API for contiguous unmanaged memory allocation.
    /// Thread-safe allocation using Interlocked operations.
    /// </summary>
    public unsafe class Arena : IDisposable
    {
        private IntPtr buffer;
        private readonly int capacity;
        private int freeOffset;
        private bool disposed;

        /// <summary>
        /// Creates a new arena with the specified capacity in bytes.
        /// </summary>
        public Arena(int bytes)
        {
            if (bytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Arena capacity must be positive");
            
            capacity = bytes;
            buffer = Marshal.AllocHGlobal(bytes);
            freeOffset = 0;
            
            // Zero out the memory
            var span = new Span<byte>((void*)buffer, bytes);
            span.Clear();
        }

        /// <summary>
        /// Allocates a block of memory in the arena.
        /// Thread-safe using interlocked operations.
        /// </summary>
        public int Allocate(int bytes)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (bytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(bytes), "Allocation size must be positive");
            
            // Align to 8-byte boundary for better performance
            bytes = (bytes + 7) & ~7;
            
            var offset = Interlocked.Add(ref freeOffset, bytes) - bytes;
            
            if (offset + bytes > capacity)
                throw new OutOfMemoryException($"Arena out of space: requested {bytes}, available {capacity - offset}");
            
            return offset;
        }

        /// <summary>
        /// Writes an IntPtr value at the specified offset.
        /// </summary>
        public void WriteIntPtr(int offset, IntPtr value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + IntPtr.Size > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            Marshal.WriteIntPtr(buffer + offset, value);
        }

        /// <summary>
        /// Writes a 32-bit integer at the specified offset.
        /// </summary>
        public void WriteInt32(int offset, int value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + 4 > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            Marshal.WriteInt32(buffer + offset, value);
        }

        /// <summary>
        /// Writes a 64-bit integer at the specified offset.
        /// </summary>
        public void WriteInt64(int offset, long value)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + 8 > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            Marshal.WriteInt64(buffer + offset, value);
        }

        /// <summary>
        /// Writes a byte array at the specified offset.
        /// </summary>
        public void WriteBytes(int offset, byte[] src)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            
            if (offset < 0 || offset + src.Length > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            Marshal.Copy(src, 0, buffer + offset, src.Length);
        }

        /// <summary>
        /// Reads an IntPtr value from the specified offset.
        /// </summary>
        public IntPtr ReadIntPtr(int offset)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + IntPtr.Size > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            return Marshal.ReadIntPtr(buffer + offset);
        }

        /// <summary>
        /// Reads a 32-bit integer from the specified offset.
        /// </summary>
        public int ReadInt32(int offset)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + 4 > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            return Marshal.ReadInt32(buffer + offset);
        }

        /// <summary>
        /// Reads a 64-bit integer from the specified offset.
        /// </summary>
        public long ReadInt64(int offset)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + 8 > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            return Marshal.ReadInt64(buffer + offset);
        }

        /// <summary>
        /// Reads a byte array from the specified offset.
        /// </summary>
        public byte[] ReadBytes(int offset, int count)
        {
            if (disposed)
                throw new ObjectDisposedException(nameof(Arena));
            
            if (offset < 0 || offset + count > capacity)
                throw new ArgumentOutOfRangeException(nameof(offset));
            
            var result = new byte[count];
            Marshal.Copy(buffer + offset, result, 0, count);
            return result;
        }

        /// <summary>
        /// Gets the base pointer of the arena.
        /// </summary>
        public IntPtr BasePtr
        {
            get
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(Arena));
                return buffer;
            }
        }

        /// <summary>
        /// Gets the total capacity of the arena in bytes.
        /// </summary>
        public int Capacity => capacity;

        /// <summary>
        /// Gets the number of bytes currently allocated.
        /// </summary>
        public int AllocatedBytes => freeOffset;

        /// <summary>
        /// Gets the number of bytes remaining.
        /// </summary>
        public int RemainingBytes => capacity - freeOffset;

        /// <summary>
        /// Disposes the arena and frees unmanaged memory.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (buffer != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(buffer);
                    buffer = IntPtr.Zero;
                }
                disposed = true;
            }
        }

        ~Arena()
        {
            Dispose(false);
        }
    }

    /// <summary>
    /// Managed wrapper that exposes CompiledMethod fields stored in the arena.
    /// Provides safe access to arena-allocated method data.
    /// </summary>
    public unsafe class ArenaCompiledMethod
    {
        private readonly Arena arena;
        private readonly ArenaHandle handle;
        private readonly int baseOffset;

        /// <summary>
        /// Creates a wrapper for an arena-allocated CompiledMethod.
        /// </summary>
        public ArenaCompiledMethod(Arena arena, ArenaHandle handle)
        {
            this.arena = arena ?? throw new ArgumentNullException(nameof(arena));
            
            if (!handle.IsValid)
                throw new ArgumentException("Invalid arena handle", nameof(handle));
            
            this.handle = handle;
            this.baseOffset = handle.Index;
        }

        /// <summary>
        /// Sets the method header information.
        /// </summary>
        public void SetHeader(IntPtr classPtr, int literalCount, int numTemps)
        {
            arena.WriteIntPtr(baseOffset + ArenaLayout.ClassPtrOffset, classPtr);
            arena.WriteInt32(baseOffset + ArenaLayout.LiteralCountOffset, literalCount);
            arena.WriteInt32(baseOffset + ArenaLayout.NumTempsOffset, numTemps);
        }

        /// <summary>
        /// Sets the bytecode for this method.
        /// </summary>
        public void SetBytecodes(byte[] bytecodes)
        {
            if (bytecodes == null)
                throw new ArgumentNullException(nameof(bytecodes));
            
            var bcOffset = baseOffset + ArenaLayout.LiteralsOffset + GetLiteralTableSize();
            arena.WriteBytes(bcOffset, bytecodes);
        }

        /// <summary>
        /// Sets a literal at the specified index.
        /// </summary>
        public void SetLiteral(int index, IntPtr literalPtr)
        {
            var literalCount = GetLiteralCount();
            if (index < 0 || index >= literalCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var literalOffset = baseOffset + ArenaLayout.LiteralsOffset + (index * IntPtr.Size);
            arena.WriteIntPtr(literalOffset, literalPtr);
        }

        /// <summary>
        /// Gets the number of temporaries for this method.
        /// </summary>
        public int GetNumTemps()
        {
            return arena.ReadInt32(baseOffset + ArenaLayout.NumTempsOffset);
        }

        /// <summary>
        /// Gets the number of literals for this method.
        /// </summary>
        public int GetLiteralCount()
        {
            return arena.ReadInt32(baseOffset + ArenaLayout.LiteralCountOffset);
        }

        /// <summary>
        /// Gets a literal at the specified index.
        /// </summary>
        public IntPtr GetLiteral(int index)
        {
            var literalCount = GetLiteralCount();
            if (index < 0 || index >= literalCount)
                throw new ArgumentOutOfRangeException(nameof(index));
            
            var literalOffset = baseOffset + ArenaLayout.LiteralsOffset + (index * IntPtr.Size);
            return arena.ReadIntPtr(literalOffset);
        }

        /// <summary>
        /// Gets the class pointer for this method.
        /// </summary>
        public IntPtr GetClassPtr()
        {
            return arena.ReadIntPtr(baseOffset + ArenaLayout.ClassPtrOffset);
        }

        /// <summary>
        /// Gets the size of the literal table in bytes.
        /// </summary>
        private int GetLiteralTableSize()
        {
            return GetLiteralCount() * IntPtr.Size;
        }

        /// <summary>
        /// Gets a pointer to the bytecode array.
        /// </summary>
        public IntPtr BytecodePtr
        {
            get
            {
                return arena.BasePtr + baseOffset + ArenaLayout.LiteralsOffset + GetLiteralTableSize();
            }
        }

        /// <summary>
        /// Gets the arena handle for this method.
        /// </summary>
        public ArenaHandle Handle => handle;

        /// <summary>
        /// Allocates a new CompiledMethod in the arena.
        /// </summary>
        public static ArenaCompiledMethod Allocate(Arena arena, int literalCount, int bytecodeLength)
        {
            if (arena == null)
                throw new ArgumentNullException(nameof(arena));
            
            if (literalCount < 0)
                throw new ArgumentOutOfRangeException(nameof(literalCount));
            
            if (bytecodeLength < 0)
                throw new ArgumentOutOfRangeException(nameof(bytecodeLength));
            
            // Calculate total size needed
            var totalSize = ArenaLayout.HeaderSize + (literalCount * IntPtr.Size) + bytecodeLength;
            
            // Allocate in arena
            var offset = arena.Allocate(totalSize);
            var handle = new ArenaHandle(offset);
            
            return new ArenaCompiledMethod(arena, handle);
        }
    }
}