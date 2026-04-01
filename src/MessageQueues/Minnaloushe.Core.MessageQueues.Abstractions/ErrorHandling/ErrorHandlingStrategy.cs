namespace Minnaloushe.Core.MessageQueues.Abstractions.ErrorHandling;

public enum ErrorHandlingStrategy
{
    NackAndRequeue, //Requeue failed message 
    NackAndDiscard,  // Discard failed message
    Ack, // Acknowledge message even on failure
    DeadLetter, // Send failed message to dead-letter queue
}