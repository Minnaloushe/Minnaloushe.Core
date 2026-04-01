using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;
using Minnaloushe.Core.Toolbox.TestHelpers;

namespace Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

public interface IContainerWrapper
{
    string Host { get; }
    ushort Port { get; }
    string Name { get; }
    string Username { get; }
    string Password { get; }
}

public abstract class ContainerWrapperBase<TBuilderEntity, TContainerEntity, TConfigurationEntity> : IAsyncDisposable, IContainerWrapper
    where TBuilderEntity : ContainerBuilder<TBuilderEntity, TContainerEntity, TConfigurationEntity>
    where TContainerEntity : IContainer
    where TConfigurationEntity : IContainerConfiguration
{
    public TContainerEntity Instance { get; private set; } = default!;
    public string Host { get; private set; } = string.Empty;
    public ushort Port { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Username { get; private set; } = string.Empty;
    public string Password { get; private set; } = string.Empty;

    protected abstract TBuilderEntity CreateBuilder();
    public async Task InitAsync(string name, INetwork network)
    {
        Name = Helpers.UniqueString(name);
        Username = Helpers.UniqueString("username");
        Password = Helpers.UniqueString("password");

        var builder = CreateBuilder();

        builder = InitContainer(builder);

        Instance = builder
            //.WithName(name)
            .WithNetwork(network)
            .WithNetworkAliases(Name)
            //.WithName(Name)
            // Use the official env var names for the default user/password
            .Build();

        await Instance.StartAsync();

        Host = Instance.Hostname;
        Port = Instance.GetMappedPublicPort(ContainerPort);

        await InitializeAfterStart();
    }

    protected virtual Task InitializeAfterStart() => Task.CompletedTask;

    protected abstract TBuilderEntity InitContainer(TBuilderEntity builder);

    protected abstract string ImageName { get; }
    protected abstract ushort ContainerPort { get; }
    public virtual async ValueTask DisposeAsync()
    {
        await Instance.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}

public abstract class ContainerWrapperBase : ContainerWrapperBase<ContainerBuilder, IContainer, IContainerConfiguration>
{
    protected override ContainerBuilder CreateBuilder() => new(Image.FromDefaultRegistry(ImageName));
}