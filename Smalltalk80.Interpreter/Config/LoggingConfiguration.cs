using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Smalltalk80.Interpreter.Config
{
    /// <summary>
    /// Logging configuration for the Smalltalk-80 interpreter.
    /// Replaces the obsolete DebugOptions with modern ILogger-based approach.
    /// </summary>
    public static class LoggingConfiguration
    {
        /// <summary>
        /// Configure logging categories for the interpreter.
        /// </summary>
        public static void ConfigureLogging(ILoggingBuilder builder, bool verbose = false)
        {
            builder.ClearProviders();
            builder.AddConsole(options =>
            {
                options.FormatterName = "custom";
                options.LogToStandardErrorThreshold = LogLevel.Error;
            });
            
            // Add a custom formatter that uses short category names
            builder.AddConsoleFormatter<CustomConsoleFormatter, CustomConsoleFormatterOptions>(options =>
            {
                options.TimestampFormat = "[HH:mm:ss] ";
                // Set to true to completely hide category names, false to show short names
                options.HideCategoryName = true;
            });

            if (verbose)
            {
                // Enable detailed logging for all categories
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddFilter("Smalltalk80.Interpreter.Bootstrap", LogLevel.Trace);
                builder.AddFilter("Smalltalk80.Interpreter.VM", LogLevel.Debug);
                builder.AddFilter("Smalltalk80.Interpreter.Execution", LogLevel.Debug);
                builder.AddFilter("Core", LogLevel.Debug);
            }
            else
            {
                // Default logging - only important messages
                builder.SetMinimumLevel(LogLevel.Information);
                builder.AddFilter("Smalltalk80.Interpreter.Bootstrap", LogLevel.Information);
                builder.AddFilter("Smalltalk80.Interpreter.VM", LogLevel.Warning);
                builder.AddFilter("Smalltalk80.Interpreter.Execution", LogLevel.Warning);
                builder.AddFilter("Core", LogLevel.Information);
            }
        }

        /// <summary>
        /// Logging event IDs for structured logging.
        /// </summary>
        public static class EventIds
        {
            // Bootstrap events
            public const int BootstrapStarted = 1000;
            public const int BootstrapPhaseCompleted = 1001;
            public const int ClassCreated = 1002;
            public const int MethodInstalled = 1003;
            public const int BootstrapCompleted = 1004;

            // Execution events
            public const int MethodInvoked = 2000;
            public const int BytecodeExecuted = 2001;
            public const int PrimitiveExecuted = 2002;
            public const int MessageSent = 2003;
            public const int ContextSwitched = 2004;

            // Memory events
            public const int GarbageCollectionStarted = 3000;
            public const int GarbageCollectionCompleted = 3001;
            public const int ObjectAllocated = 3002;
            public const int MemoryCompacted = 3003;

            // Debug events
            public const int StackTrace = 9000;
            public const int DebugBreakpoint = 9001;
            public const int InspectorOpened = 9002;
        }

        /// <summary>
        /// Helper methods for common logging patterns.
        /// </summary>
        public static class LogHelpers
        {
            public static void LogMethodCall(ILogger logger, string className, string selector, string[] args)
            {
                if (logger.IsEnabled(LogLevel.Debug))
                {
                    logger.LogDebug(
                        EventIds.MethodInvoked,
                        "Method called: {ClassName}>>{Selector}({Arguments})",
                        className,
                        selector,
                        string.Join(", ", args));
                }
            }

            public static void LogBytecode(ILogger logger, int pc, byte bytecode, string description)
            {
                if (logger.IsEnabled(LogLevel.Trace))
                {
                    logger.LogTrace(
                        EventIds.BytecodeExecuted,
                        "PC {PC:X4}: {Bytecode:X2} - {Description}",
                        pc,
                        bytecode,
                        description);
                }
            }

            public static void LogClassCreation(ILogger logger, string className, string superclassName)
            {
                logger.LogInformation(
                    EventIds.ClassCreated,
                    "Created class {ClassName} with superclass {SuperclassName}",
                    className,
                    superclassName);
            }

            public static void LogBootstrapPhase(ILogger logger, string phaseName, int phaseNumber, int totalPhases)
            {
                logger.LogInformation(
                    EventIds.BootstrapPhaseCompleted,
                    "Bootstrap phase {PhaseNumber}/{TotalPhases}: {PhaseName} completed",
                    phaseNumber,
                    totalPhases,
                    phaseName);
            }
        }
    }
}