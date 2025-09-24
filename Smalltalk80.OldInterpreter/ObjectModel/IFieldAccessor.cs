using System;

namespace Smalltalk80.Interpreter.ObjectModel
{
    /// <summary>
    /// Interface for centralized field access in Smalltalk objects.
    /// Provides methods to get and set fields by name or index.
    /// </summary>
    public interface IFieldAccessor
    {
        /// <summary>
        /// Gets a field value from an object using the field name.
        /// </summary>
        /// <param name="objectOop">The object to read from.</param>
        /// <param name="className">The class name for field lookup.</param>
        /// <param name="fieldName">The name of the field to read.</param>
        /// <returns>The field value or Nil if not found.</returns>
        OOP GetField(OOP objectOop, string className, string fieldName);
        
        /// <summary>
        /// Sets a field value in an object using the field name.
        /// </summary>
        /// <param name="objectOop">The object to write to.</param>
        /// <param name="className">The class name for field lookup.</param>
        /// <param name="fieldName">The name of the field to write.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetField(OOP objectOop, string className, string fieldName, OOP value);
        
        /// <summary>
        /// Gets a field value by index (for performance-critical code).
        /// </summary>
        /// <param name="objectOop">The object to read from.</param>
        /// <param name="index">The field index.</param>
        /// <returns>The field value or Nil if not found.</returns>
        OOP GetFieldByIndex(OOP objectOop, int index);
        
        /// <summary>
        /// Sets a field value by index (for performance-critical code).
        /// </summary>
        /// <param name="objectOop">The object to write to.</param>
        /// <param name="index">The field index.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>True if successful, false otherwise.</returns>
        bool SetFieldByIndex(OOP objectOop, int index, OOP value);
        
        /// <summary>
        /// Gets the field index for a class and field name.
        /// Can be cached by callers for performance.
        /// </summary>
        /// <param name="className">The class name.</param>
        /// <param name="fieldName">The field name.</param>
        /// <returns>The field index or -1 if not found.</returns>
        int GetFieldIndex(string className, string fieldName);
    }
}