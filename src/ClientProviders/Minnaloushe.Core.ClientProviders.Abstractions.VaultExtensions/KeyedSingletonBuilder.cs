using Microsoft.Extensions.DependencyInjection;

namespace Minnaloushe.Core.ClientProviders.Abstractions.VaultExtensions;

public record KeyedSingletonBuilder(Type RootType, IServiceCollection Services, IReadOnlyCollection<string> Keys);