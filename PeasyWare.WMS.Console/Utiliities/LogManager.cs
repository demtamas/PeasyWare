using Serilog;
using Serilog.Core;

namespace PeasyWare.WMS.Console.Utilities
{
    /// <summary>
    /// Provides a centralized, static class for initializing and configuring the application's logger.
    /// </summary>
    public static class LogManager
    {
        /// <summary>
        /// Configures and creates the global static logger for the entire application.
        /// The logging level is determined by a setting passed from the database.
        /// </summary>
        /// <param name="isDebuggingEnabled">If true, the logger will be set to the 'Debug' level; otherwise, it will be set to 'Information'.</param>
        public static void Initialize(bool isDebuggingEnabled)
        {
            // A LoggingLevelSwitch allows the logging level to be changed dynamically if needed in the future,
            // but here we use it to set the initial level based on our database configuration.
            var levelSwitch = new LoggingLevelSwitch(
                isDebuggingEnabled ? Serilog.Events.LogEventLevel.Debug : Serilog.Events.LogEventLevel.Information
            );

            // Configure the global logger.
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.ControlledBy(levelSwitch) // Sets the minimum level (Debug or Information).
                .WriteTo.Console() // All log events will be written to the console window.
                .WriteTo.File("logs/wms-.log", rollingInterval: RollingInterval.Day) // Creates a new log file each day (e.g., wms-20250711.log).
                .CreateLogger(); // Creates the logger instance.

            if (isDebuggingEnabled)
            {
                Log.Debug("Debugging enabled. Logger initialized with verbose output.");
            }
        }
    }
}
