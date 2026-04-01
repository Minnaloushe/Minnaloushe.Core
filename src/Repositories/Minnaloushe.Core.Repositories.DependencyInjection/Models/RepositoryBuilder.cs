using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Minnaloushe.Core.Repositories.DependencyInjection.Registries;

namespace Minnaloushe.Core.Repositories.DependencyInjection.Models;

/// <summary>
/// Builder for configuring repository infrastructure.
/// </summary>
/// <param name="Services">The service collection to register services into.</param>
/// <param name="HandlerRegistry">Registry of connection type handlers.</param>
/// <param name="Configuration">The application configuration.</param>
public record RepositoryBuilder(IServiceCollection Services, IRepositoryHandlerRegistry HandlerRegistry, IConfiguration Configuration);
