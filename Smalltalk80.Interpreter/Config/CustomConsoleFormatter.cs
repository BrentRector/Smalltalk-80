using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;
using System.IO;

namespace Smalltalk80.Interpreter.Config
{
    /// <summary>
    /// Custom console formatter that uses short category names to save space.
    /// </summary>
    public sealed class CustomConsoleFormatter : ConsoleFormatter
    {
        private readonly CustomConsoleFormatterOptions _options;

        public CustomConsoleFormatter(IOptions<CustomConsoleFormatterOptions> options)
            : base("custom")
        {
            _options = options.Value;
        }

        public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
        {
            // Write timestamp if configured
            var timestampFormat = _options.TimestampFormat ?? "[HH:mm:ss] ";
            if (!string.IsNullOrEmpty(timestampFormat))
            {
                textWriter.Write(DateTimeOffset.Now.ToString(timestampFormat));
            }

            // Write log level (abbreviated)
            textWriter.Write(GetLogLevelString(logEntry.LogLevel));
            textWriter.Write(": ");

            // Extract short category name (last part after the final dot)
            // Or omit it entirely if HideCategoryName is true
            if (!_options.HideCategoryName)
            {
                var categoryName = logEntry.Category;
                var lastDotIndex = categoryName.LastIndexOf('.');
                var shortName = lastDotIndex >= 0 ? categoryName.Substring(lastDotIndex + 1) : categoryName;
                
                textWriter.Write(shortName);
                // Only show event ID if it's non-zero (i.e., explicitly set)
                if (logEntry.EventId.Id != 0)
                {
                    textWriter.Write("[");
                    textWriter.Write(logEntry.EventId.Id);
                    textWriter.Write("]");
                }
                textWriter.Write(" ");
            }

            // Write the actual message
            textWriter.Write(logEntry.Formatter?.Invoke(logEntry.State, logEntry.Exception));

            // Write exception if present
            if (logEntry.Exception != null)
            {
                textWriter.WriteLine();
                textWriter.Write(logEntry.Exception);
            }

            textWriter.WriteLine();
        }

        private static string GetLogLevelString(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "trce",
            LogLevel.Debug => "dbug",
            LogLevel.Information => "info",
            LogLevel.Warning => "warn",
            LogLevel.Error => "fail",
            LogLevel.Critical => "crit",
            _ => "none"
        };
    }

    /// <summary>
    /// Options for the custom console formatter.
    /// </summary>
    public sealed class CustomConsoleFormatterOptions : ConsoleFormatterOptions
    {
        /// <summary>
        /// When true, completely hides the category name from output.
        /// </summary>
        public bool HideCategoryName { get; set; } = false;
    }
}