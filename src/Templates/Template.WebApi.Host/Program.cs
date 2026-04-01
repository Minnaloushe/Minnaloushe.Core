using Minnaloushe.Core.ClientProviders.Minio.Extensions;
using Minnaloushe.Core.Logging.Microsoft;
using Minnaloushe.Core.Logging.NLog;
using Minnaloushe.Core.Logging.Redaction;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.Kafka.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Extensions;
using Minnaloushe.Core.MessageQueues.DependencyInjection.RabbitMq.Vault.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Consumers.Extensions;
using Minnaloushe.Core.MessageQueues.Kafka.Producers.Extensions;
using Minnaloushe.Core.MessageQueues.RabbitMq.Producers;
using Minnaloushe.Core.ReadinessProbe;
using Minnaloushe.Core.Repositories.DependencyInjection.Extensions;
using Minnaloushe.Core.Repositories.Migrations.MongoDb;
using Minnaloushe.Core.Repositories.MongoDb.Extensions;
using Minnaloushe.Core.Repositories.MongoDb.Vault.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Extensions;
using Minnaloushe.Core.Repositories.Postgres.Vault.Extensions;
using Minnaloushe.Core.S3.S3Storage.Extensions;
using Minnaloushe.Core.ServiceDiscovery.Extensions;
using Minnaloushe.Core.Toolbox.ApplicationRoutines;
using Minnaloushe.Core.Toolbox.AsyncInitializer.Extensions;
using Minnaloushe.Core.Toolbox.Cancellation;
using Minnaloushe.Core.Toolbox.PollingFolderWatcher;
using Minnaloushe.Core.Toolbox.RecyclableMemoryStream.Extensions;
using Minnaloushe.Core.VaultOptions.Extensions;
using Minnaloushe.Core.VaultService.Extensions;
using Scalar.AspNetCore;
using Template.WebApi.Host.Consumer;

namespace Template.WebApi.Host;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Logging.ConfigureMicrosoftJsonConsole();
        //builder.Logging.ConfigureNLog();
        builder.Services.AddLogRedaction();
        builder.Logging.EnableRedaction();

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
        builder.Services.AddOpenApi();

        builder.Services.ConfigureRecyclableStreams();
        builder.Services.ConfigureAsyncInitializers();
        builder.Services.AddServiceDiscovery();

        builder.Services.AddVaultClientProvider();
        builder.Services.AddVaultStoredOptions();

        builder.Services.AddApplicationConfiguration();
        builder.Services.AddReadinessProbes();
        //builder.Services.AddKeyedTelegramClientProviders(builder.Configuration);
        builder.Services.AddKeyedMinioClientProviders(builder.Configuration)
            .WithStorageAdapters();

        builder.Services.AddMessageQueues(builder.Configuration)
            .AddRabbitMqClientProviders()
            .AddVaultRabbitMqClientProviders()
            .AddKafkaClientProviders()
            .AddRabbitMqConsumers()
            .AddKafkaConsumers()
            .AddConsumer<TestMessage, TestMessageConsumer>("rabbit-consumer")
            .AddConsumer<TestMessage, TestMessageConsumer>("rabbit-consumer-static")
            .AddConsumer<TestMessage, TestMessageConsumer>("kafka-consumer")
            .AddConsumer<TestBrokenMessage, TestBrokenMessageConsumer>("test-broken-rabbit")
            .AddConsumer<TestBrokenMessage, TestBrokenMessageConsumer>("test-broken-kafka")
            .AddRabbitMqProducers()
            .AddKafkaProducers()
            .AddProducer<TestMessage>("rabbitmq-default", "rabbit-producer")
            .AddProducer<TestMessage>("rabbitmq-static", "rabbit-producer-static")
            .AddProducer<TestMessage>("kafka-default", "kafka-producer")
            .AddProducer<TestBrokenMessage>("rabbitmq-default", "rabbit-broken")
            .AddProducer<TestBrokenMessage>("kafka-default", "kafka-broken")
            .Build();

        //builder.Services.AddMikrotikClientProvider();

        builder.Services.AddRepositories(builder.Configuration)
            .AddMongoDbClientProviders()
            .AddVaultMongoDbClientProviders()
            .AddMongoDbMigrations()
            .AddPostgresDbClientProviders()
            .AddVaultPostgresDbClientProviders()
            .Build();

        builder.Services.AddPollingFolderWatcher<FolderWatcherImplementation>();
        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.MapReadinessProbes();

        app.UseAuthorization();

        app.MapControllers();

        app.UseCancellationContext(opts =>
        {
            opts.UseMiddleware = false;
            opts.RequestTimeout = TimeSpan.FromMinutes(1);
        });

        await app.InvokeAsyncInitializers();

        await app.RunAsync();
    }
}

public class FolderWatcherImplementation(
    ILogger<FolderWatcherImplementation> logger
    ) : IFolderWatcherHandler
{
    public Task HandleFileChange(FileChangedEventArgs args, CancellationToken cancellationToken)
    {
#pragma warning disable CA1873
        logger.LogInformation("New event from polling folder watcher. Event {type} for file {file}", args.EventType, args.FileInfo.FullName);
#pragma warning restore CA1873
        return Task.CompletedTask;
    }
}