using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

internal static partial class DefaultClientProvidersInitializerLogger
{
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Information,
        Message = "Initializing {Count} section(s)")]
    public static partial void LogInitializingSections(
        this ILogger logger,
        int count);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Debug,
        Message = "Initializing section: {SectionKey}")]
    public static partial void LogInitializingSection(
        this ILogger logger,
        string sectionKey);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Debug,
        Message = "Section initialized: {SectionKey}")]
    public static partial void LogSectionInitialized(
        this ILogger logger,
        string sectionKey);

    [LoggerMessage(
        EventId = 2003,
        Level = LogLevel.Error,
        Message = "Failed to initialize section: {SectionKey}")]
    public static partial void LogSectionInitializationFailed(
        this ILogger logger,
        string sectionKey,
        Exception exception);

    [LoggerMessage(
        EventId = 2004,
        Level = LogLevel.Information,
        Message = "All {Count} section(s) initialized successfully")]
    public static partial void LogAllSectionsInitialized(
        this ILogger logger,
        int count);
}