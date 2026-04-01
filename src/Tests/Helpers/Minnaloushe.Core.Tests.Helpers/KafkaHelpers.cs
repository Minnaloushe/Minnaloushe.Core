using Confluent.Kafka;
using Minnaloushe.Core.Tests.Helpers.ContainerWrappers;

namespace Minnaloushe.Core.Tests.Helpers;

public static class KafkaHelpers
{
    public static IAdminClient CreateAdminClient(KafkaContainerWrapper container)
    {
        var adminConfig = new AdminClientConfig
        {
            BootstrapServers = container.Instance.GetBootstrapAddress(),
            SecurityProtocol = SecurityProtocol.Plaintext,
            SaslMechanism = SaslMechanism.Plain,
            SaslUsername = container.Username,
            SaslPassword = container.Password
        };

        return new AdminClientBuilder(adminConfig).Build();
    }

    public static async Task WaitForTopicCreation(KafkaContainerWrapper container, string topicName, int maxWaitSeconds = 10)
    {
        var maxWait = TimeSpan.FromSeconds(maxWaitSeconds);
        var startTime = DateTime.UtcNow;
        var pollInterval = TimeSpan.FromMilliseconds(200);

        using var adminClient = CreateAdminClient(container);

        while (DateTime.UtcNow - startTime < maxWait)
        {
            try
            {
                var metadata = adminClient.GetMetadata(TimeSpan.FromSeconds(5));
                if (metadata.Topics.Any(t => t.Topic == topicName))
                {
                    return;
                }
            }
            catch
            {
                // Ignore exceptions during polling
            }

            await Task.Delay(pollInterval);
        }

        throw new TimeoutException($"Topic '{topicName}' was not created within {maxWaitSeconds} seconds");
    }
}