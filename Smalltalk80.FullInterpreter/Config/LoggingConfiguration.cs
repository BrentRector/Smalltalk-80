using Microsoft.Extensions.Logging;

namespace Smalltalk80.Interpreter.Config;

/// <summary>
/// Provides centralized logging configuration for the Smalltalk-80 interpreter.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures logging based on runtime options and environment variables.
    /// </summary>
    /// <param name="builder">The logging builder to configure.</param>
    /// <param name="verbose">Whether verbose logging is enabled.</param>
    public static void ConfigureLogging(ILoggingBuilder builder, bool verbose = false)
    {
        // Clear existing providers
        builder.ClearProviders();
        
        // Add console logging with simple formatter
        builder.AddSimpleConsole(options =>
        {
            options.IncludeScopes = false;
            options.TimestampFormat = "[HH:mm:ss] ";
            options.SingleLine = true;
        });
        
        // Set log level based on verbose flag and environment variables
        var defaultLogLevel = LogLevel.Information;
        
        // Check environment variable for log level
        var envLogLevel = Environment.GetEnvironmentVariable("LOGGING__LOGLEVEL__DEFAULT");
        if (!string.IsNullOrEmpty(envLogLevel))
        {
            defaultLogLevel = envLogLevel.ToUpperInvariant() switch
            {
                "TRACE" => LogLevel.Trace,
                "DEBUG" => LogLevel.Debug,
                "INFORMATION" or "INFO" => LogLevel.Information,
                "WARNING" or "WARN" => LogLevel.Warning,
                "ERROR" => LogLevel.Error,
                "CRITICAL" => LogLevel.Critical,
                "NONE" => LogLevel.None,
                _ => LogLevel.Information
            };
        }
        else if (verbose)
        {
            defaultLogLevel = LogLevel.Debug;
        }
        
        builder.SetMinimumLevel(defaultLogLevel);
        
        // Configure specific categories if needed
        builder.AddFilter("Smalltalk80.Interpreter.cold start", verbose ? LogLevel.Debug : LogLevel.Information);
        builder.AddFilter("Smalltalk80.Interpreter.VM", verbose ? LogLevel.Debug : LogLevel.Information);
        builder.AddFilter("Smalltalk80.Interpreter.Memory", verbose ? LogLevel.Debug : LogLevel.Warning);
        builder.AddFilter("Smalltalk80.Interpreter.Execution", verbose ? LogLevel.Debug : LogLevel.Warning);
    }
    
    /// <summary>
    /// Creates a simple console logger factory for standalone use.
    /// </summary>
    /// <param name="verbose">Whether verbose logging is enabled.</param>
    /// <returns>A configured logger factory.</returns>
    public static ILoggerFactory CreateLoggerFactory(bool verbose = false)
    {
        return LoggerFactory.Create(builder => ConfigureLogging(builder, verbose));
    }
}