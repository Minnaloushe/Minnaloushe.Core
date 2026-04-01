using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;
using RabbitMQ.Client;

namespace Minnaloushe.Core.Tests.Helpers;

public class MqHelpers
{
    public static object CreateConnection(string name,
        string type,
        string? connectionString = null,
        string? serviceKey = null,
        string? serviceName = null,
        string? host = null,
        ushort? port = null,
        int? parallelism = null,
        string? errorHandling = "NackAndDiscard",
        string? username = null,
        string? password = null,
        IContainerWrapper? container = null,
        Dictionary<string, string>? parameters = null)
    {
        return new
        {
            Name = name,
            Type = type,
            ConnectionString = connectionString,
            Host = host ?? container?.Host,
            Port = port ?? container?.Port,
            ServiceKey = serviceKey ?? name,
            ServiceName = serviceName,
            Parallelism = parallelism,
            ErrorHandling = errorHandling,
            Username = username ?? container?.Username,
            Password = password ?? container?.Password,
            Parameters = parameters
        };
    }

    public static object CreateConsumer(string name,
        string connectionName,
        string errorHandling = "NackAndDiscard",
        int parallelism = 1,
        int numPartitions = 1,
        int replicationFactor = 1
        )
    {
        return new
        {
            Name = name,
            ConnectionName = connectionName,
            Parallelism = parallelism,
            ErrorHandling = errorHandling,
            Parameters = new Dictionary<string, string>
            {
                ["NumPartitions"] = numPartitions.ToString(),
                ["ReplicationFactor"] = replicationFactor.ToString()
            }
        };
    }

    public static object CreateAppSettings(object[] connections, object[] consumers)
    {
        return new
        {
            MessageQueues = new
            {
                Connections = connections,
                Consumers = consumers
            },
            AsyncInitializer = new
            {
                Enabled = true,
                Timeout = TimeSpan.FromMinutes(2)
            }
        };
    }
}

public static class RabbitMqHelpers
{
    public static async Task<IConnection> CreateConnectionAsync(RabbitContainerWrapper container)
    {
        var factory = new ConnectionFactory
        {
            HostName = container.Host,
            Port = container.Port,
            UserName = container.Username,
            Password = container.Password
        };

        return await factory.CreateConnectionAsync();
    }

    public static async Task<bool> ExchangeExistsAsync(RabbitContainerWrapper container, string exchangeName)
    {
        try
        {
            await using var connection = await CreateConnectionAsync(container);
            await using var channel = await connection.CreateChannelAsync();

            // Try to declare the exchange passively (check if it exists without creating it)
            await channel.ExchangeDeclarePassiveAsync(exchangeName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static async Task<IChannel> CreateAndBindQueueAsync(RabbitContainerWrapper container, string exchangeName, string queueName)
    {
        var factory = new ConnectionFactory
        {
            HostName = container.Host,
            Port = container.Port,
            UserName = container.Username,
            Password = container.Password
        };

        // Setup consumer BEFORE publishing
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Fanout, durable: true, autoDelete: false);
        await channel.QueueDeclareAsync(queueName, durable: false, exclusive: true, autoDelete: true);
        await channel.QueueBindAsync(queueName, exchangeName, string.Empty);

        return channel;
    }
    public static async Task WaitForExchangeCreation(RabbitContainerWrapper container, string exchangeName, int maxWaitSeconds = 10)
    {
        var maxWait = TimeSpan.FromSeconds(maxWaitSeconds);
        var startTime = DateTime.UtcNow;
        var pollInterval = TimeSpan.FromMilliseconds(200);

        while (DateTime.UtcNow - startTime < maxWait)
        {
            if (await ExchangeExistsAsync(container, exchangeName))
            {
                return;
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException($"Exchange '{exchangeName}' was not created within {maxWaitSeconds} seconds");
    }

    public static async Task EnsureExchangeExistsAsync(RabbitContainerWrapper container, string exchangeName, string exchangeType = ExchangeType.Fanout)
    {
        await using var connection = await CreateConnectionAsync(container);
        await using var channel = await connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: exchangeType,
            durable: true,
            autoDelete: false,
            arguments: null);
    }
}