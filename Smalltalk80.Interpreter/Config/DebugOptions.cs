namespace Smalltalk80.Interpreter.Config;

/// <summary>
/// Global debug options for the Smalltalk-80 interpreter.
/// Controls various debugging and tracing features.
/// </summary>
public static class DebugOptions
{
    /// <summary>
    /// Gets or sets whether [DEBUG] output is enabled.
    /// </summary>
    public static bool EnableDebugOutput { get; set; }
    
    /// <summary>
    /// Gets or sets whether [MethodInstaller] output is enabled.
    /// </summary>
    public static bool EnableMethodInstallerOutput { get; set; }
    
    /// <summary>
    /// Gets or sets whether class/metaclass registration output is enabled.
    /// </summary>
    public static bool EnableClassRegistrationOutput { get; set; }
    
    /// <summary>
    /// Gets or sets whether phase detail output (Wired, Set name, etc.) is enabled.
    /// </summary>
    public static bool EnablePhaseDetailOutput { get; set; }
    
    /// <summary>
    /// Gets or sets whether method invocation tracing is enabled.
    /// </summary>
    public static bool EnableMethodTrace { get; set; }
    
    /// <summary>
    /// Gets or sets whether bytecode execution tracing is enabled.
    /// </summary>
    public static bool EnableBytecodeTrace { get; set; }
    
    /// <summary>
    /// Gets or sets whether VM bytecode execution messages ([VM] Executing bytecode) are enabled.
    /// </summary>
    public static bool EnableVMBytecodeMessages { get; set; }
    
    /// <summary>
    /// Gets or sets whether InitExpr MethodContext debug output is enabled.
    /// </summary>
    public static bool EnableInitExprMethodContext { get; set; }
    
    /// <summary>
    /// Gets or sets whether InitExpr CompiledMethod structure debug output is enabled.
    /// </summary>
    public static bool EnableInitExprCompiledMethod { get; set; }
    
    /// <summary>
    /// Gets or sets whether bytecode mnemonics should be shown.
    /// </summary>
    public static bool ShowBytecodeWithMnemonics { get; set; }
    
    /// <summary>
    /// Gets or sets whether to show enhanced class printing.
    /// </summary>
    public static bool EnableEnhancedClassPrint { get; set; }
    
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
        EnableVMBytecodeMessages = true;
        EnableInitExprMethodContext = true;
        EnableInitExprCompiledMethod = true;
        ShowBytecodeWithMnemonics = true;
        EnableEnhancedClassPrint = true;
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
        EnableVMBytecodeMessages = false;
        EnableInitExprMethodContext = false;
        EnableInitExprCompiledMethod = false;
        ShowBytecodeWithMnemonics = false;
        EnableEnhancedClassPrint = false;
    }
    
    /// <summary>
    /// Sets debug mode by name.
    /// </summary>
    /// <param name="mode">The debug mode to enable.</param>
    public static void SetDebugMode(string mode)
    {
        switch (mode.ToLowerInvariant())
        {
            case "all":
                EnableAll();
                break;
            case "debug":
                EnableDebugOutput = true;
                break;
            case "method":
            case "methodinstaller":
                EnableMethodInstallerOutput = true;
                break;
            case "class":
            case "registration":
                EnableClassRegistrationOutput = true;
                break;
            case "detail":
            case "phasedetail":
                EnablePhaseDetailOutput = true;
                break;
            case "trace":
            case "methodtrace":
                EnableMethodTrace = true;
                break;
            case "bytecode":
            case "bytecodetrace":
                EnableBytecodeTrace = true;
                break;
            case "vmbytecode":
            case "vm":
                EnableVMBytecodeMessages = true;
                break;
            case "initcontext":
            case "initexprcontext":
                EnableInitExprMethodContext = true;
                break;
            case "initmethod":
            case "initexprmethod":
                EnableInitExprCompiledMethod = true;
                break;
            case "mnemonics":
                ShowBytecodeWithMnemonics = true;
                break;
            case "enhanced":
                EnableEnhancedClassPrint = true;
                break;
            case "none":
            case "off":
                DisableAll();
                break;
        }
    }
}