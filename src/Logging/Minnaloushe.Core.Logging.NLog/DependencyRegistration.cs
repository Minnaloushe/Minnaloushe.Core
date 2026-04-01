using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;
using LogLevel = NLog.LogLevel;

namespace Minnaloushe.Core.Logging.NLog;

public static class DependencyRegistration
{
    public static ILoggingBuilder ConfigureNLog(this ILoggingBuilder builder, string configName)
    {
        try
        {
            LogManager.Setup().LoadConfigurationFromFile(configName);
        }
        catch
        {
            // ignore - we'll configure programmatically below
        }

        // Ensure NLog picks up the (possibly new) configuration for existing loggers
        LogManager.ReconfigExistingLoggers();

        builder.ClearProviders();
        builder.AddNLog();

        return builder;
    }


    public static ILoggingBuilder ConfigureNLog(this ILoggingBuilder builder)
    {
        // Must be registered before any Layout string is assigned to a target,
        // because NLog parses layout strings eagerly on assignment.
        LogManager.Setup()
            .SetupExtensions(ext => ext.RegisterLayoutRenderer<CategoryHashLayoutRenderer>("category-hash"));

        var config = new LoggingConfiguration();

        var consoleTarget = new ColoredConsoleTarget("console")
        {
            Layout =
                "${longdate} | " +
                "${level:uppercase=true} | " +
                "${event-id:whenEmpty=0} | " +
                "${logger} | " +
                "${category-hash} | " +
                "${threadid} | " +
                "${taskid:whenEmpty=0} | " +
                "${mdlc:CorrelationId:whenEmpty=-} | " +
                "${message}"
        };

        config.AddTarget(consoleTarget);
        config.AddRule(LogLevel.Info, LogLevel.Fatal, consoleTarget);

        LogManager.Configuration = config;
        // Ensure NLog picks up the (possibly new) configuration for existing loggers
        LogManager.ReconfigExistingLoggers();

        builder.ClearProviders();
        builder.AddNLog();

        return builder;
    }
}