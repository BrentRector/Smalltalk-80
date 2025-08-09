namespace Smalltalk80.Compiler
{
    /// <summary>
    /// Defines the output schema formats for compiled JSON manifests.
    /// </summary>
    public enum OutputSchema
    {
        /// <summary>
        /// Phase 2 format - multiple classes in a single file with a "classes" array.
        /// </summary>
        Phase2,
        
        /// <summary>
        /// Phase 3 format - single class per file with direct class properties.
        /// </summary>
        Phase3
    }
}