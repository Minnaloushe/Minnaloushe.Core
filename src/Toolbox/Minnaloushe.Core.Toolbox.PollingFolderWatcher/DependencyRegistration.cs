using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.Toolbox.PollingFolderWatcher;

public static class DependencyRegistration
{
    //TODO: Add multiple handlers and options support, use same approach as in TelegramClientProvider
    public static IServiceCollection AddPollingFolderWatcher<THandler>(this IServiceCollection services)
        where THandler : class, IFolderWatcherHandler
    {
        services.AddOptions<FolderWatcherOptions>()
            .BindConfiguration(FolderWatcherOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IFileSystemAccessor, FileSystemAccessor>();
        services.AddScoped<IFolderWatcherHandler, THandler>();
        services.AddSingleton<IFolderChangeDetector, FolderChangeDetector>();
        services.AddHostedService<PollingFolderWatcherBackgroundService>();
        return services;
    }
}
