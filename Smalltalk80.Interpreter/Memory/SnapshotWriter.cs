using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Smalltalk80.Interpreter.Core;

namespace Smalltalk80.Interpreter.Memory
{
    /// <summary>
    /// Snapshot writer that serializes both managed SObject graph and arena contents
    /// into a single contiguous image file.
    /// </summary>
    public class SnapshotWriter : IDisposable
    {
        private readonly Arena arena;
        private readonly Dictionary<object, int> slotMap = new();
        private readonly List<Action<BinaryWriter>> deferredWrites = new();
        private int nextSlot = 1; // Slot 0 is reserved for nil
        
        /// <summary>
        /// Object types in the image.
        /// </summary>
        private enum ObjectType : byte
        {
            Nil = 0,
            SObject = 1,
            ArenaObject = 2,
            SmallInteger = 3,
            Symbol = 4,
            Array = 5,
            ByteArray = 6,
            CompiledMethod = 7,
            Class = 8
        }

        /// <summary>
        /// Creates a new snapshot writer.
        /// </summary>
        public SnapshotWriter(Arena arena)
        {
            this.arena = arena;
            
            // Reserve slot 0 for nil
            slotMap[Nil.Instance] = 0;
        }

        /// <summary>
        /// Assigns a slot number for an object in the image.
        /// </summary>
        public int AssignSlot(object obj)
        {
            if (obj == null || obj == Nil.Instance)
                return 0;
            
            if (slotMap.TryGetValue(obj, out var slot))
                return slot;
            
            slot = nextSlot++;
            slotMap[obj] = slot;
            return slot;
        }

        /// <summary>
        /// Scans the managed object graph starting from roots and enqueues deferred writes.
        /// </summary>
        public void ScanManagedRoots(IEnumerable<object> roots)
        {
            if (roots == null)
                throw new ArgumentNullException(nameof(roots));
            
            var queue = new Queue<object>(roots);
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            
            while (queue.Count > 0)
            {
                var obj = queue.Dequeue();
                
                if (obj == null || !visited.Add(obj))
                    continue;
                
                // Assign slot
                var slot = AssignSlot(obj);
                
                // Handle different object types
                if (Tags.IsImmediate(obj))
                {
                    // SmallInteger - no fields to traverse
                    deferredWrites.Add(w => WriteSmallInteger(w, slot, Tags.ToSmallInteger(obj)));
                }
                else if (obj is SObject sobj)
                {
                    deferredWrites.Add(w => WriteSObjectBody(w, slot, sobj));
                    
                    // Queue instance variables
                    foreach (var field in sobj.InstVars)
                    {
                        if (field != null && field != Nil.Instance)
                            queue.Enqueue(field);
                    }
                    
                    // Queue class
                    if (sobj.Klass != null)
                        queue.Enqueue(sobj.Klass);
                    
                    // Handle specific types
                    if (obj is SArray array)
                    {
                        foreach (var element in array.Elements)
                        {
                            if (element != null && element != Nil.Instance)
                                queue.Enqueue(element);
                        }
                    }
                    else if (obj is SCompiledMethod method)
                    {
                        foreach (var literal in method.Literals)
                        {
                            if (literal != null && literal != Nil.Instance)
                                queue.Enqueue(literal);
                        }
                    }
                    else if (obj is SClass cls)
                    {
                        if (cls.Superclass != null)
                            queue.Enqueue(cls.Superclass);
                        if (cls.Metaclass != null)
                            queue.Enqueue(cls.Metaclass);
                    }
                }
                else if (obj is ArenaHandle handle)
                {
                    deferredWrites.Add(w => WriteArenaObjectBody(w, slot, handle));
                }
            }
        }

        /// <summary>
        /// Writes a SmallInteger to the image.
        /// </summary>
        private void WriteSmallInteger(BinaryWriter writer, int slot, long value)
        {
            writer.Write(slot);
            writer.Write((byte)ObjectType.SmallInteger);
            writer.Write(value);
        }

        /// <summary>
        /// Writes an SObject body to the image.
        /// </summary>
        private void WriteSObjectBody(BinaryWriter writer, int slot, SObject sobj)
        {
            // Determine object type
            var objectType = ObjectType.SObject;
            if (sobj is SClass)
                objectType = ObjectType.Class;
            else if (sobj is SArray)
                objectType = ObjectType.Array;
            else if (sobj is SByteArray)
                objectType = ObjectType.ByteArray;
            else if (sobj is SCompiledMethod)
                objectType = ObjectType.CompiledMethod;
            else if (sobj is SSymbol)
                objectType = ObjectType.Symbol;
            
            writer.Write(slot);
            writer.Write((byte)objectType);
            writer.Write(sobj.IdentityHash);
            writer.Write(GetSlotOrZero(sobj.Klass));
            writer.Write(sobj.InstVars.Length);
            
            // Write instance variables
            foreach (var field in sobj.InstVars)
            {
                writer.Write(GetSlotOrZero(field));
            }
            
            // Write type-specific data
            if (sobj is SArray array)
            {
                writer.Write(array.Elements.Length);
                foreach (var element in array.Elements)
                {
                    writer.Write(GetSlotOrZero(element));
                }
            }
            else if (sobj is SByteArray byteArray)
            {
                writer.Write(byteArray.Bytes.Length);
                writer.Write(byteArray.Bytes);
            }
            else if (sobj is SString str)
            {
                var bytes = str.Bytes;
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (sobj is SSymbol symbol)
            {
                var bytes = Encoding.UTF8.GetBytes(symbol.Value);
                writer.Write(bytes.Length);
                writer.Write(bytes);
            }
            else if (sobj is SCompiledMethod method)
            {
                writer.Write(method.Header);
                writer.Write(method.PrimitiveIndex);
                writer.Write(method.Literals.Length);
                foreach (var literal in method.Literals)
                {
                    writer.Write(GetSlotOrZero(literal));
                }
                writer.Write(method.Bytecode.Length);
                writer.Write(method.Bytecode);
            }
            else if (sobj is SClass cls)
            {
                writer.Write(GetSlotOrZero(cls.Superclass));
                writer.Write(GetSlotOrZero(cls.Metaclass));
                writer.Write(GetSlotOrZero(cls.ThisClass));
                writer.Write(cls.Name ?? string.Empty);
                writer.Write(cls.InstSize);
                writer.Write((byte)cls.Format);
                writer.Write(cls.InstanceVariables.Length);
                foreach (var varName in cls.InstanceVariables)
                {
                    writer.Write(varName ?? string.Empty);
                }
            }
        }

        /// <summary>
        /// Writes an arena object reference to the image.
        /// </summary>
        private void WriteArenaObjectBody(BinaryWriter writer, int slot, ArenaHandle handle)
        {
            writer.Write(slot);
            writer.Write((byte)ObjectType.ArenaObject);
            writer.Write(handle.Index);
        }

        /// <summary>
        /// Gets the slot number for an object or returns 0 for nil.
        /// </summary>
        private int GetSlotOrZero(object obj)
        {
            if (obj == null || obj == Nil.Instance)
                return 0;
            
            return slotMap.TryGetValue(obj, out var slot) ? slot : 0;
        }

        /// <summary>
        /// Writes the complete image to the output stream.
        /// </summary>
        public void WriteImage(Stream outStream)
        {
            if (outStream == null)
                throw new ArgumentNullException(nameof(outStream));
            
            using var writer = new BinaryWriter(outStream, Encoding.UTF8, leaveOpen: true);
            
            // Write header
            writer.Write(0x534D414C); // Magic: "SMAL"
            writer.Write(1);          // Image version
            writer.Write(nextSlot - 1); // Object count
            
            // Write object table
            foreach (var writeAction in deferredWrites)
            {
                writeAction(writer);
            }
            
            // Write arena data if present
            if (arena != null && arena.AllocatedBytes > 0)
            {
                writer.Write(0x4152454E); // Magic: "AREN"
                writer.Write(arena.AllocatedBytes);
                
                // Copy arena memory to stream
                var arenaBytes = new byte[arena.AllocatedBytes];
                Marshal.Copy(arena.BasePtr, arenaBytes, 0, arenaBytes.Length);
                writer.Write(arenaBytes);
            }
            
            // Write end marker
            writer.Write(0x454E4421); // Magic: "END!"
            
            writer.Flush();
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose - arena is not owned
        }

        /// <summary>
        /// Reference equality comparer for object identity.
        /// </summary>
        private class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();

            public new bool Equals(object x, object y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

    /// <summary>
    /// Snapshot reader that deserializes an image back into memory.
    /// </summary>
    public class SnapshotReader : IDisposable
    {
        private readonly Arena arena;
        private readonly Dictionary<int, object> slotToObject = new();
        private readonly List<Action> patchActions = new();

        /// <summary>
        /// Creates a new snapshot reader.
        /// </summary>
        public SnapshotReader(Arena arena)
        {
            this.arena = arena;
            
            // Slot 0 is always nil
            slotToObject[0] = Nil.Instance;
        }

        /// <summary>
        /// Reads an image from the input stream.
        /// </summary>
        public Dictionary<string, object> ReadImage(Stream inStream)
        {
            if (inStream == null)
                throw new ArgumentNullException(nameof(inStream));
            
            using var reader = new BinaryReader(inStream, Encoding.UTF8, leaveOpen: true);
            
            // Read and verify header
            var magic = reader.ReadInt32();
            if (magic != 0x534D414C) // "SMAL"
                throw new InvalidDataException("Invalid image magic number");
            
            var version = reader.ReadInt32();
            if (version != 1)
                throw new InvalidDataException($"Unsupported image version: {version}");
            
            var objectCount = reader.ReadInt32();
            
            // Phase 1: Create all objects with forward references
            for (int i = 0; i < objectCount; i++)
            {
                ReadObjectEntry(reader);
            }
            
            // Check for arena data
            if (reader.BaseStream.Position < reader.BaseStream.Length - 4)
            {
                var arenaMagic = reader.ReadInt32();
                if (arenaMagic == 0x4152454E) // "AREN"
                {
                    var arenaSize = reader.ReadInt32();
                    var arenaBytes = reader.ReadBytes(arenaSize);
                    
                    // Copy to arena if provided
                    if (arena != null && arenaBytes.Length > 0)
                    {
                        var offset = arena.Allocate(arenaBytes.Length);
                        arena.WriteBytes(offset, arenaBytes);
                    }
                }
            }
            
            // Verify end marker
            var endMagic = reader.ReadInt32();
            if (endMagic != 0x454E4421) // "END!"
                throw new InvalidDataException("Missing or invalid end marker");
            
            // Phase 2: Execute patch actions to fix forward references
            foreach (var patch in patchActions)
            {
                patch();
            }
            
            // Build result dictionary of globals
            var result = new Dictionary<string, object>();
            
            // Find and return key globals
            foreach (var kvp in slotToObject)
            {
                if (kvp.Value is SClass cls && !string.IsNullOrEmpty(cls.Name))
                {
                    result[cls.Name] = cls;
                }
            }
            
            return result;
        }

        /// <summary>
        /// Reads a single object entry from the stream.
        /// </summary>
        private void ReadObjectEntry(BinaryReader reader)
        {
            var slot = reader.ReadInt32();
            var objectType = (ObjectType)reader.ReadByte();
            
            switch (objectType)
            {
                case ObjectType.SmallInteger:
                    var value = reader.ReadInt64();
                    slotToObject[slot] = Tags.FromSmallInteger(value);
                    break;
                    
                case ObjectType.SObject:
                    ReadSObject(reader, slot);
                    break;
                    
                case ObjectType.Array:
                    ReadArray(reader, slot);
                    break;
                    
                case ObjectType.ByteArray:
                    ReadByteArray(reader, slot);
                    break;
                    
                case ObjectType.Symbol:
                    ReadSymbol(reader, slot);
                    break;
                    
                case ObjectType.CompiledMethod:
                    ReadCompiledMethod(reader, slot);
                    break;
                    
                case ObjectType.Class:
                    ReadClass(reader, slot);
                    break;
                    
                case ObjectType.ArenaObject:
                    var arenaIndex = reader.ReadInt32();
                    slotToObject[slot] = new ArenaHandle(arenaIndex);
                    break;
                    
                default:
                    throw new InvalidDataException($"Unknown object type: {objectType}");
            }
        }

        /// <summary>
        /// Reads an SObject from the stream.
        /// </summary>
        private void ReadSObject(BinaryReader reader, int slot)
        {
            var identityHash = reader.ReadInt32();
            var klassSlot = reader.ReadInt32();
            var instVarCount = reader.ReadInt32();
            
            // Create placeholder object
            var obj = new SObject(null, instVarCount);
            slotToObject[slot] = obj;
            
            // Read instance variables (as slots)
            var instVarSlots = new int[instVarCount];
            for (int i = 0; i < instVarCount; i++)
            {
                instVarSlots[i] = reader.ReadInt32();
            }
            
            // Patch later
            patchActions.Add(() =>
            {
                if (klassSlot != 0)
                    obj.Klass = (SClass)slotToObject[klassSlot];
                
                for (int i = 0; i < instVarCount; i++)
                {
                    obj.InstVars[i] = GetObjectForSlot(instVarSlots[i]);
                }
            });
        }

        // Similar read methods for other types...
        private void ReadArray(BinaryReader reader, int slot) { /* Implementation */ }
        private void ReadByteArray(BinaryReader reader, int slot) { /* Implementation */ }
        private void ReadSymbol(BinaryReader reader, int slot) { /* Implementation */ }
        private void ReadCompiledMethod(BinaryReader reader, int slot) { /* Implementation */ }
        private void ReadClass(BinaryReader reader, int slot) { /* Implementation */ }

        /// <summary>
        /// Gets the object for a slot number.
        /// </summary>
        private object GetObjectForSlot(int slot)
        {
            if (slot == 0)
                return Nil.Instance;
            
            return slotToObject.TryGetValue(slot, out var obj) ? obj : Nil.Instance;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose - arena is not owned
        }

        /// <summary>
        /// Object types in the image.
        /// </summary>
        private enum ObjectType : byte
        {
            Nil = 0,
            SObject = 1,
            ArenaObject = 2,
            SmallInteger = 3,
            Symbol = 4,
            Array = 5,
            ByteArray = 6,
            CompiledMethod = 7,
            Class = 8
        }
    }
}