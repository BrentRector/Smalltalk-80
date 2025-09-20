namespace Smalltalk80.Interpreter.Config
{
    /// <summary>
    /// Global debug options for the Smalltalk-80 interpreter.
    /// </summary>
    public static class DebugOptions
    {
        /// <summary>
        /// Gets or sets whether [DEBUG] output is enabled.
        /// </summary>
        public static bool EnableDebugOutput { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether [MethodInstaller] output is enabled.
        /// </summary>
        public static bool EnableMethodInstallerOutput { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether class/metaclass registration output is enabled.
        /// </summary>
        public static bool EnableClassRegistrationOutput { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether phase detail output (Wired, Set name, etc.) is enabled.
        /// </summary>
        public static bool EnablePhaseDetailOutput { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether method invocation tracing is enabled.
        /// </summary>
        public static bool EnableMethodTrace { get; set; } = false;
        
        /// <summary>
        /// Gets or sets whether bytecode execution tracing is enabled.
        /// </summary>
        public static bool EnableBytecodeTrace { get; set; } = false;
        
        /// <summary>
        /// Enables all debug output categories.
        /// </summary>
        public static void EnableAll()
        {
            EnableDebugOutput = true;
            EnableMethodInstallerOutput = true;
            EnableClassRegistrationOutput = true;
            EnablePhaseDetailOutput = true;
            EnableMethodTrace = true;
            EnableBytecodeTrace = true;
        }
        
        /// <summary>
        /// Disables all debug output categories.
        /// </summary>
        public static void DisableAll()
        {
            EnableDebugOutput = false;
            EnableMethodInstallerOutput = false;
            EnableClassRegistrationOutput = false;
            EnablePhaseDetailOutput = false;
            EnableMethodTrace = false;
            EnableBytecodeTrace = false;
        }
    }
}