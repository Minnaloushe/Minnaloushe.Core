using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.ManagerWrapper;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Options;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.TempStreamFactory;

namespace Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Extensions;

public static class DependenciesRegistration
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureRecyclableStreams()
        {
            services.AddOptions<StreamOptions>()
                .BindConfiguration(StreamOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddSingleton<IRecyclableMemoryStreamManagerWrapper, RecyclableMemoryStreamManagerWrapper>();

            services.AddTempStreamFactory();

            return services;
        }

        internal IServiceCollection AddTempStreamFactory()
        {
            services.AddSingleton<ITempStreamFactory>(sp =>
            {
                var options = sp.GetRequiredService<IOptions<StreamOptions>>().Value;
                return options.TempStreamType switch
                {
                    TempStreamType.RecyclableMemoryStream => new RecyclableMemoryStreamFactory(
                        sp.GetRequiredService<IRecyclableMemoryStreamManagerWrapper>()),
                    TempStreamType.MemoryStream => new MemoryStreamFactory(),
                    TempStreamType.FileStream => new FileStreamFactory(),
                    _ => throw new NotSupportedException($"Temp stream type {options.TempStreamType} is not supported.")
                };
            });
            return services;
        }
    }
}