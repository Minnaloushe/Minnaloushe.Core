namespace Minnaloushe.Core.MessageQueues.Abstractions;

public interface IMessageEngine
{
    Task RunAsync(CancellationToken serviceStop, CancellationToken processingStop);
}