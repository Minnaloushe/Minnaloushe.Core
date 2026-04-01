using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Minnaloushe.Core.Logging.Microsoft;

public static class DependencyRegistration
{
    public static ILoggingBuilder ConfigureMicrosoftConsole(this ILoggingBuilder builder)
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(LogLevel.Information);
        builder.AddConsole(options => options.FormatterName = MinnalousheConsoleFormatter.FormatterName);
        builder.AddConsoleFormatter<MinnalousheConsoleFormatter, ConsoleFormatterOptions>(
            options => options.IncludeScopes = true);

        return builder;
    }

    public static ILoggingBuilder ConfigureMicrosoftJsonConsole(this ILoggingBuilder builder,
        LogLevel minLevel = LogLevel.Information)
    {
        builder.ClearProviders();
        builder.SetMinimumLevel(minLevel);

        if (System.Diagnostics.Debugger.IsAttached)
        {
            builder.AddConsole();
        }
        else
        {
            builder.AddJsonConsole(opts =>
                {
                    opts.IncludeScopes = true;
                    opts.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffff";
                }
            );
        }

        return builder;
    }
}
