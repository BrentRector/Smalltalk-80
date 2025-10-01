namespace Smalltalk80.Interpreter.Core.BlueBook;

/// <summary>
/// CRITICAL: Blue Book class layout constants.
///
/// These layouts MUST exactly match the Smalltalk-80 source code definitions.
/// Any changes to the Smalltalk class definitions require corresponding updates here.
///
/// Source: Smalltalk-80 Source/Smalltalk-80.st
///
/// WARNING: Modifying these constants without updating the Smalltalk source (or vice versa)
/// will cause interpreter failures and data corruption.
/// </summary>
public static class BlueBookLayouts
{
    /// <summary>
    /// InstructionStream: Object subclass
    /// instanceVariableNames: 'sender pc'
    ///
    /// Base class for contexts (MethodContext and BlockContext).
    /// </summary>
    public static class InstructionStream
    {
        /// <summary>
        /// Index of the sender context field (MethodContext/BlockContext or nil).
        /// </summary>
        public const int SenderIndex = 0;           // sender context (MethodContext/BlockContext or nil)

        /// <summary>
        /// Index of the program counter field (SmallInteger).
        /// </summary>
        public const int PcIndex = 1;               // program counter (SmallInteger)

        /// <summary>
        /// Total number of fixed instance variables in InstructionStream.
        /// </summary>
        public const int FixedFieldCount = 2;
    }

    /// <summary>
    /// ContextPart: InstructionStream subclass
    /// instanceVariableNames: 'stackp'
    ///
    /// Inherits: sender (0), pc (1)
    /// Adds: stackp (2)
    /// </summary>
    public static class ContextPart
    {
        // Inherited from InstructionStream
        /// <summary>
        /// Index of the sender context field inherited from InstructionStream.
        /// </summary>
        public const int SenderIndex = 0;           // sender context

        /// <summary>
        /// Index of the program counter field inherited from InstructionStream.
        /// </summary>
        public const int PcIndex = 1;               // program counter

        // ContextPart fields
        /// <summary>
        /// Index of the stack pointer field (SmallInteger).
        /// </summary>
        public const int StackpIndex = 2;           // stack pointer (SmallInteger)

        /// <summary>
        /// Total number of fixed instance variables in ContextPart.
        /// </summary>
        public const int FixedFieldCount = 3;
    }

    /// <summary>
    /// MethodContext: ContextPart variableSubclass
    /// instanceVariableNames: 'method receiverMap receiver'
    ///
    /// Inherits: sender (0), pc (1), stackp (2)
    /// Adds: method (3), receiverMap (4), receiver (5)
    /// Variable part: temporaries and stack
    /// </summary>
    public static class MethodContext
    {
        // Inherited from InstructionStream
        /// <summary>
        /// Index of the sender context field inherited from InstructionStream.
        /// </summary>
        public const int SenderIndex = 0;           // sender context

        /// <summary>
        /// Index of the program counter field (SmallInteger) inherited from InstructionStream.
        /// </summary>
        public const int PcIndex = 1;               // program counter (SmallInteger)

        // Inherited from ContextPart
        /// <summary>
        /// Index of the stack pointer field (SmallInteger) inherited from ContextPart.
        /// </summary>
        public const int StackpIndex = 2;           // stack pointer (SmallInteger)

        // MethodContext fields
        /// <summary>
        /// Index of the method field containing the CompiledMethod.
        /// </summary>
        public const int MethodIndex = 3;           // CompiledMethod

        /// <summary>
        /// Index of the receiver map field (for optimization, usually nil).
        /// </summary>
        public const int ReceiverMapIndex = 4;      // receiver map (for optimization, usually nil)

        /// <summary>
        /// Index of the receiver field containing the message receiver.
        /// </summary>
        public const int ReceiverIndex = 5;         // message receiver

        /// <summary>
        /// Total number of fixed instance variables in MethodContext.
        /// </summary>
        public const int FixedFieldCount = 6;
        // Variable part follows: temps and stack
    }

    /// <summary>
    /// BlockContext: ContextPart variableSubclass
    /// instanceVariableNames: 'nargs startpc home'
    ///
    /// Inherits: sender (0), pc (1), stackp (2)
    /// Adds: nargs (3), startpc (4), home (5)
    /// Variable part: copied values and stack
    /// </summary>
    public static class BlockContext
    {
        // Inherited from InstructionStream
        /// <summary>
        /// Index of the sender context field inherited from InstructionStream.
        /// </summary>
        public const int SenderIndex = 0;           // sender context

        /// <summary>
        /// Index of the program counter field (SmallInteger) inherited from InstructionStream.
        /// </summary>
        public const int PcIndex = 1;               // program counter (SmallInteger)

        // Inherited from ContextPart
        /// <summary>
        /// Index of the stack pointer field (SmallInteger) inherited from ContextPart.
        /// </summary>
        public const int StackpIndex = 2;           // stack pointer (SmallInteger)

        // BlockContext fields
        /// <summary>
        /// Index of the number of arguments field (SmallInteger).
        /// </summary>
        public const int NargsIndex = 3;            // number of arguments (SmallInteger)

        /// <summary>
        /// Index of the initial PC field (SmallInteger).
        /// </summary>
        public const int StartpcIndex = 4;          // initial PC (SmallInteger)

        /// <summary>
        /// Index of the home MethodContext field.
        /// </summary>
        public const int HomeIndex = 5;             // home MethodContext

        /// <summary>
        /// Total number of fixed instance variables in BlockContext.
        /// </summary>
        public const int FixedFieldCount = 6;
        // Variable part follows: copied values and stack
    }

    /// <summary>
    /// CompiledMethod: ByteArray variableByteSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed bytes.
    /// Format: Header encoding (varies) + literals (SObjectRef[]) + bytecodes (byte[])
    /// </summary>
    public static class CompiledMethod
    {
        // CompiledMethod has no fixed instance variables
        // All data is in the indexed portion:
        // - Header (encoded in first few bytes - format varies)
        // - Literals (SObjectRef[] - count encoded in header)
        // - Bytecodes (byte[] - remainder of indexed bytes)

        /// <summary>
        /// Total number of fixed instance variables in CompiledMethod.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// Process: Link subclass
    /// instanceVariableNames: 'suspendedContext priority myList'
    ///
    /// Inherits: nextLink (0)
    /// Adds: suspendedContext (1), priority (2), myList (3)
    /// </summary>
    public static class Process
    {
        // Inherited from Link
        /// <summary>
        /// Index of the nextLink field pointing to the next process in list.
        /// </summary>
        public const int NextLinkIndex = 0;         // next process in list

        // Process fields
        /// <summary>
        /// Index of the suspended context field (MethodContext or BlockContext).
        /// </summary>
        public const int SuspendedContextIndex = 1; // suspended MethodContext or BlockContext

        /// <summary>
        /// Index of the priority field (SmallInteger).
        /// </summary>
        public const int PriorityIndex = 2;         // SmallInteger priority

        /// <summary>
        /// Index of the myList field containing the ProcessList this process is in.
        /// </summary>
        public const int MyListIndex = 3;           // ProcessList this process is in

        /// <summary>
        /// Total number of fixed instance variables in Process.
        /// </summary>
        public const int FixedFieldCount = 4;
    }

    /// <summary>
    /// Semaphore: LinkedList subclass
    /// instanceVariableNames: 'excessSignals'
    ///
    /// Inherits: firstLink (0), lastLink (1)
    /// Adds: excessSignals (2)
    /// </summary>
    public static class Semaphore
    {
        // Inherited from LinkedList
        /// <summary>
        /// Index of the firstLink field pointing to the first waiting process.
        /// </summary>
        public const int FirstLinkIndex = 0;        // first waiting process

        /// <summary>
        /// Index of the lastLink field pointing to the last waiting process.
        /// </summary>
        public const int LastLinkIndex = 1;         // last waiting process

        // Semaphore fields
        /// <summary>
        /// Index of the excessSignals field containing the SmallInteger count of signals.
        /// </summary>
        public const int ExcessSignalsIndex = 2;    // SmallInteger count of signals

        /// <summary>
        /// Total number of fixed instance variables in Semaphore.
        /// </summary>
        public const int FixedFieldCount = 3;
    }

    /// <summary>
    /// Dictionary: Set variableSubclass
    /// instanceVariableNames: ''
    ///
    /// Inherits: tally (0)
    /// Variable part: associations (Association[])
    /// </summary>
    public static class Dictionary
    {
        // Inherited from Set
        /// <summary>
        /// Index of the tally field containing the SmallInteger count of elements.
        /// </summary>
        public const int TallyIndex = 0;            // SmallInteger count of elements

        /// <summary>
        /// Total number of fixed instance variables in Dictionary.
        /// </summary>
        public const int FixedFieldCount = 1;
        // Variable part: Association objects
    }

    /// <summary>
    /// IdentityDictionary: Dictionary variableSubclass
    /// instanceVariableNames: 'valueArray'
    ///
    /// Inherits: tally (0)
    /// Adds: valueArray (1)
    /// Variable part: keys (parallel to valueArray)
    /// </summary>
    public static class IdentityDictionary
    {
        // Inherited from Set via Dictionary
        /// <summary>
        /// Index of the tally field containing the SmallInteger count of elements.
        /// </summary>
        public const int TallyIndex = 0;            // SmallInteger count of elements

        // IdentityDictionary fields
        /// <summary>
        /// Index of the valueArray field containing the Array of values (parallel to keys).
        /// </summary>
        public const int ValueArrayIndex = 1;       // Array of values (parallel to keys in variable part)

        /// <summary>
        /// Total number of fixed instance variables in IdentityDictionary.
        /// </summary>
        public const int FixedFieldCount = 2;
        // Variable part: keys (parallel to valueArray)
    }

    /// <summary>
    /// Association: LookupKey subclass
    /// instanceVariableNames: 'value'
    ///
    /// Inherits: key (0)
    /// Adds: value (1)
    /// </summary>
    public static class Association
    {
        // Inherited from LookupKey
        /// <summary>
        /// Index of the key field containing the association key.
        /// </summary>
        public const int KeyIndex = 0;              // association key

        // Association fields
        /// <summary>
        /// Index of the value field containing the association value.
        /// </summary>
        public const int ValueIndex = 1;            // association value

        /// <summary>
        /// Total number of fixed instance variables in Association.
        /// </summary>
        public const int FixedFieldCount = 2;
    }

    /// <summary>
    /// Symbol: String variableByteSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed bytes for character data.
    /// </summary>
    public static class Symbol
    {
        // Symbol has no fixed instance variables
        // All data is in the indexed byte portion (character bytes)

        /// <summary>
        /// Total number of fixed instance variables in Symbol.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// Behavior: Object subclass
    /// instanceVariableNames: 'superclass methodDict format subclasses'
    /// </summary>
    public static class Behavior
    {
        /// <summary>
        /// Index of the superclass field (Behavior or nil).
        /// </summary>
        public const int SuperclassIndex = 0;       // superclass (Behavior or nil)

        /// <summary>
        /// Index of the methodDict field containing the MethodDictionary.
        /// </summary>
        public const int MethodDictIndex = 1;       // MethodDictionary

        /// <summary>
        /// Index of the format field containing the SmallInteger encoding instance format.
        /// </summary>
        public const int FormatIndex = 2;           // SmallInteger encoding instance format

        /// <summary>
        /// Index of the subclasses field containing the Array of subclasses.
        /// </summary>
        public const int SubclassesIndex = 3;       // Array of subclasses

        /// <summary>
        /// Total number of fixed instance variables in Behavior.
        /// </summary>
        public const int FixedFieldCount = 4;
    }

    /// <summary>
    /// ClassDescription: Behavior subclass
    /// instanceVariableNames: 'instanceVariables organization'
    ///
    /// Inherits: superclass (0), methodDict (1), format (2), subclasses (3)
    /// Adds: instanceVariables (4), organization (5)
    /// </summary>
    public static class ClassDescription
    {
        // Inherited from Behavior
        /// <summary>
        /// Index of the superclass field inherited from Behavior.
        /// </summary>
        public const int SuperclassIndex = 0;

        /// <summary>
        /// Index of the methodDict field inherited from Behavior.
        /// </summary>
        public const int MethodDictIndex = 1;

        /// <summary>
        /// Index of the format field inherited from Behavior.
        /// </summary>
        public const int FormatIndex = 2;

        /// <summary>
        /// Index of the subclasses field inherited from Behavior.
        /// </summary>
        public const int SubclassesIndex = 3;

        // ClassDescription fields
        /// <summary>
        /// Index of the instanceVariables field containing a String of space-separated names.
        /// </summary>
        public const int InstanceVariablesIndex = 4;    // String of space-separated names

        /// <summary>
        /// Index of the organization field containing the ClassOrganizer.
        /// </summary>
        public const int OrganizationIndex = 5;         // ClassOrganizer

        /// <summary>
        /// Total number of fixed instance variables in ClassDescription.
        /// </summary>
        public const int FixedFieldCount = 6;
    }

    /// <summary>
    /// Class: ClassDescription subclass
    /// instanceVariableNames: 'name classPool sharedPools'
    ///
    /// Inherits: superclass (0), methodDict (1), format (2), subclasses (3),
    ///          instanceVariables (4), organization (5)
    /// Adds: name (6), classPool (7), sharedPools (8)
    /// </summary>
    public static class Class
    {
        // Inherited from Behavior
        /// <summary>
        /// Index of the superclass field inherited from Behavior.
        /// </summary>
        public const int SuperclassIndex = 0;

        /// <summary>
        /// Index of the methodDict field inherited from Behavior.
        /// </summary>
        public const int MethodDictIndex = 1;

        /// <summary>
        /// Index of the format field inherited from Behavior.
        /// </summary>
        public const int FormatIndex = 2;

        /// <summary>
        /// Index of the subclasses field inherited from Behavior.
        /// </summary>
        public const int SubclassesIndex = 3;

        // Inherited from ClassDescription
        /// <summary>
        /// Index of the instanceVariables field inherited from ClassDescription.
        /// </summary>
        public const int InstanceVariablesIndex = 4;

        /// <summary>
        /// Index of the organization field inherited from ClassDescription.
        /// </summary>
        public const int OrganizationIndex = 5;

        // Class fields
        /// <summary>
        /// Index of the name field containing the Symbol class name.
        /// </summary>
        public const int NameIndex = 6;                 // Symbol (class name)

        /// <summary>
        /// Index of the classPool field containing the Dictionary of class variables.
        /// </summary>
        public const int ClassPoolIndex = 7;            // Dictionary of class variables

        /// <summary>
        /// Index of the sharedPools field containing the Array of shared pool dictionaries.
        /// </summary>
        public const int SharedPoolsIndex = 8;          // Array of shared pool dictionaries

        /// <summary>
        /// Total number of fixed instance variables in Class.
        /// </summary>
        public const int FixedFieldCount = 9;
    }

    /// <summary>
    /// Metaclass: ClassDescription subclass
    /// instanceVariableNames: 'thisClass'
    ///
    /// Inherits: superclass (0), methodDict (1), format (2), subclasses (3),
    ///          instanceVariables (4), organization (5)
    /// Adds: thisClass (6)
    /// </summary>
    public static class Metaclass
    {
        // Inherited from Behavior
        /// <summary>
        /// Index of the superclass field inherited from Behavior.
        /// </summary>
        public const int SuperclassIndex = 0;

        /// <summary>
        /// Index of the methodDict field inherited from Behavior.
        /// </summary>
        public const int MethodDictIndex = 1;

        /// <summary>
        /// Index of the format field inherited from Behavior.
        /// </summary>
        public const int FormatIndex = 2;

        /// <summary>
        /// Index of the subclasses field inherited from Behavior.
        /// </summary>
        public const int SubclassesIndex = 3;

        // Inherited from ClassDescription
        /// <summary>
        /// Index of the instanceVariables field inherited from ClassDescription.
        /// </summary>
        public const int InstanceVariablesIndex = 4;

        /// <summary>
        /// Index of the organization field inherited from ClassDescription.
        /// </summary>
        public const int OrganizationIndex = 5;

        // Metaclass fields
        /// <summary>
        /// Index of the thisClass field containing the Class this is a metaclass of.
        /// </summary>
        public const int ThisClassIndex = 6;            // The Class this is a metaclass of

        /// <summary>
        /// Total number of fixed instance variables in Metaclass.
        /// </summary>
        public const int FixedFieldCount = 7;
    }

    /// <summary>
    /// Array: ArrayedCollection variableSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed pointer slots.
    /// </summary>
    public static class Array
    {
        // Array has no fixed instance variables
        // All data is in the indexed pointer portion (SObjectRef[])

        /// <summary>
        /// Total number of fixed instance variables in Array.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// Character: Magnitude subclass
    /// instanceVariableNames: 'value'
    ///
    /// CRITICAL: Character is NOT an immediate value in Blue Book.
    /// It is a full object with one instance variable holding the ASCII value.
    /// </summary>
    public static class Character
    {
        /// <summary>
        /// Index of the value field containing the SmallInteger ASCII value.
        /// </summary>
        public const int ValueIndex = 0;                // SmallInteger ASCII value

        /// <summary>
        /// Total number of fixed instance variables in Character.
        /// </summary>
        public const int FixedFieldCount = 1;
    }

    /// <summary>
    /// String: ArrayedCollection variableByteSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed byte slots.
    /// </summary>
    public static class String
    {
        // String has no fixed instance variables
        // All data is in the indexed byte portion (byte[])

        /// <summary>
        /// Total number of fixed instance variables in String.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// ByteArray: ArrayedCollection variableByteSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed byte slots.
    /// </summary>
    public static class ByteArray
    {
        // ByteArray has no fixed instance variables
        // All data is in the indexed byte portion (byte[])

        /// <summary>
        /// Total number of fixed instance variables in ByteArray.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// Float: Number variableWordSubclass
    /// instanceVariableNames: ''
    ///
    /// No instance variables - only indexed word slots.
    /// Stores floating-point value as words (implementation-specific encoding).
    /// </summary>
    public static class Float
    {
        // Float has no fixed instance variables
        // All data is in the indexed word portion (ushort[] or similar)
        // The exact encoding is implementation-specific

        /// <summary>
        /// Total number of fixed instance variables in Float.
        /// </summary>
        public const int FixedFieldCount = 0;
    }

    /// <summary>
    /// Link: Object subclass
    /// instanceVariableNames: 'nextLink'
    ///
    /// Base class for linked list elements.
    /// </summary>
    public static class Link
    {
        /// <summary>
        /// Index of the nextLink field pointing to the next Link in list (or nil).
        /// </summary>
        public const int NextLinkIndex = 0;             // next Link in list (or nil)

        /// <summary>
        /// Total number of fixed instance variables in Link.
        /// </summary>
        public const int FixedFieldCount = 1;
    }

    /// <summary>
    /// LinkedList: SequenceableCollection subclass
    /// instanceVariableNames: 'firstLink lastLink'
    ///
    /// Base class for linked lists (used by Semaphore).
    /// </summary>
    public static class LinkedList
    {
        /// <summary>
        /// Index of the firstLink field pointing to the first Link (or nil).
        /// </summary>
        public const int FirstLinkIndex = 0;            // first Link (or nil)

        /// <summary>
        /// Index of the lastLink field pointing to the last Link (or nil).
        /// </summary>
        public const int LastLinkIndex = 1;             // last Link (or nil)

        /// <summary>
        /// Total number of fixed instance variables in LinkedList.
        /// </summary>
        public const int FixedFieldCount = 2;
    }

    /// <summary>
    /// LookupKey: Magnitude subclass
    /// instanceVariableNames: 'key'
    ///
    /// Base class for Association.
    /// </summary>
    public static class LookupKey
    {
        /// <summary>
        /// Index of the key field containing the key object.
        /// </summary>
        public const int KeyIndex = 0;                  // the key object

        /// <summary>
        /// Total number of fixed instance variables in LookupKey.
        /// </summary>
        public const int FixedFieldCount = 1;
    }

    /// <summary>
    /// Set: Collection variableSubclass
    /// instanceVariableNames: 'tally'
    ///
    /// Base class for Dictionary.
    /// Variable part contains elements.
    /// </summary>
    public static class Set
    {
        /// <summary>
        /// Index of the tally field containing the SmallInteger count of elements.
        /// </summary>
        public const int TallyIndex = 0;                // SmallInteger count of elements

        /// <summary>
        /// Total number of fixed instance variables in Set.
        /// </summary>
        public const int FixedFieldCount = 1;
        // Variable part: elements
    }
}