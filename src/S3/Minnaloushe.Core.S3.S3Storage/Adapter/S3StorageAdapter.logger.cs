using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.S3.S3Storage.Adapter;

internal static partial class S3StorageAdapterLogger
{
    [LoggerMessage(EventId = 6000, Level = LogLevel.Debug, Message = "Getting stream for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GettingStream(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Successfully retrieved stream for key '{Key}' (size: {Size} bytes)")]
    public static partial void StreamRetrieved(this ILogger<S3StorageAdapter> logger, string Key, long Size);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Error, Message = "Error getting stream for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GetStreamError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Debug, Message = "Getting tags for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GettingTags(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Information, Message = "Retrieved {Count} tags for key '{Key}'")]
    public static partial void TagsRetrieved(this ILogger<S3StorageAdapter> logger, int Count, string Key);

    [LoggerMessage(EventId = 6005, Level = LogLevel.Error, Message = "Error getting tags for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GetTagsError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Debug, Message = "Checking existence of key '{Key}' in bucket '{Bucket}'")]
    public static partial void CheckingExistence(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Debug, Message = "Key '{Key}' not found in bucket '{Bucket}'")]
    public static partial void KeyNotFound(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6008, Level = LogLevel.Error, Message = "Error checking existence of key '{Key}' in bucket '{Bucket}'")]
    public static partial void CheckExistenceError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6009, Level = LogLevel.Error, Message = "Unexpected error checking existence of key '{Key}' in bucket '{Bucket}'")]
    public static partial void UnexpectedExistenceError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6010, Level = LogLevel.Debug, Message = "Getting blob info for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GettingBlobInfo(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Error, Message = "Object not found: key '{Key}' in bucket '{Bucket}'")]
    public static partial void ObjectNotFound(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Error, Message = "Error getting blob info for key '{Key}' from bucket '{Bucket}'")]
    public static partial void GetBlobInfoError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Error, Message = "Unexpected error getting blob info for key '{Key}' from bucket '{Bucket}'")]
    public static partial void UnexpectedBlobInfoError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6014, Level = LogLevel.Debug, Message = "Uploading stream for key '{Key}' to bucket '{Bucket}' with content type '{ContentType}'")]
    public static partial void UploadingStream(this ILogger<S3StorageAdapter> logger, string Key, string Bucket, string ContentType);

    [LoggerMessage(EventId = 6015, Level = LogLevel.Information, Message = "Successfully uploaded stream for key '{Key}'")]
    public static partial void UploadSuccessful(this ILogger<S3StorageAdapter> logger, string Key);

    [LoggerMessage(EventId = 6016, Level = LogLevel.Error, Message = "Error uploading stream for key '{Key}' to bucket '{Bucket}'")]
    public static partial void UploadError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6017, Level = LogLevel.Debug, Message = "Deleting key '{Key}' from bucket '{Bucket}'")]
    public static partial void DeletingKey(this ILogger<S3StorageAdapter> logger, string Key, string Bucket);

    [LoggerMessage(EventId = 6018, Level = LogLevel.Information, Message = "Successfully deleted key '{Key}'")]
    public static partial void DeleteSuccessful(this ILogger<S3StorageAdapter> logger, string Key);

    [LoggerMessage(EventId = 6019, Level = LogLevel.Error, Message = "Error deleting key '{Key}' from bucket '{Bucket}'")]
    public static partial void DeleteError(this ILogger<S3StorageAdapter> logger, Exception exception, string Key, string Bucket);

    [LoggerMessage(EventId = 6020, Level = LogLevel.Debug, Message = "Listing blobs in bucket '{Bucket}' with prefix '{Prefix}'")]
    public static partial void ListingBlobs(this ILogger<S3StorageAdapter> logger, string Bucket, string? Prefix);

    [LoggerMessage(EventId = 6021, Level = LogLevel.Information, Message = "Listed {Count} blobs in bucket '{Bucket}'")]
    public static partial void ListingComplete(this ILogger<S3StorageAdapter> logger, int Count, string Bucket);

    [LoggerMessage(EventId = 6022, Level = LogLevel.Error, Message = "Error listing blobs in bucket '{Bucket}'")]
    public static partial void ListError(this ILogger<S3StorageAdapter> logger, Exception exception, string Bucket);

    [LoggerMessage(EventId = 6023, Level = LogLevel.Debug, Message = "Renaming key '{ExistingKey}' to '{NewKey}' in bucket '{Bucket}' (overwrite={Overwrite})")]
    public static partial void RenamingKey(this ILogger<S3StorageAdapter> logger, string ExistingKey, string NewKey, string Bucket, bool Overwrite);

    [LoggerMessage(EventId = 6024, Level = LogLevel.Warning, Message = "Cannot rename: source key '{ExistingKey}' not found in bucket '{Bucket}'")]
    public static partial void RenameSourceNotFound(this ILogger<S3StorageAdapter> logger, string ExistingKey, string Bucket);

    [LoggerMessage(EventId = 6025, Level = LogLevel.Warning, Message = "Cannot rename: destination key '{NewKey}' already exists in bucket '{Bucket}'")]
    public static partial void RenameDestinationExists(this ILogger<S3StorageAdapter> logger, string NewKey, string Bucket);

    [LoggerMessage(EventId = 6026, Level = LogLevel.Information, Message = "Successfully renamed '{ExistingKey}' to '{NewKey}' in bucket '{Bucket}'")]
    public static partial void RenameSuccessful(this ILogger<S3StorageAdapter> logger, string ExistingKey, string NewKey, string Bucket);

    [LoggerMessage(EventId = 6027, Level = LogLevel.Error, Message = "Failed to rename '{ExistingKey}' to '{NewKey}' in bucket '{Bucket}'")]
    public static partial void RenameFailed(this ILogger<S3StorageAdapter> logger, Exception exception, string ExistingKey, string NewKey, string Bucket);
}
