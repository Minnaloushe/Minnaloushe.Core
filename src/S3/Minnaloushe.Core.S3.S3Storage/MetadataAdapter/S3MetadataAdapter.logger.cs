using Microsoft.Extensions.Logging;

namespace Minnaloushe.Core.S3.S3Storage.MetadataAdapter;

internal static partial class S3MetadataAdapterLogger
{
    [LoggerMessage(EventId = 6100, Level = LogLevel.Debug, Message = "Getting metadata stream for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void GettingMetadataStream(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Information, Message = "Successfully retrieved metadata stream for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void MetadataStreamRetrieved(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Error, Message = "Metadata key '{MetadataKey}' not found for blob '{BlobKey}'")]
    public static partial void MetadataKeyNotFound(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Error, Message = "Error getting metadata stream for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void GetMetadataStreamError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6104, Level = LogLevel.Debug, Message = "Getting metadata object for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void GettingMetadataObject(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Information, Message = "Successfully retrieved and deserialized metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void MetadataObjectRetrieved(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Error, Message = "Failed to deserialize metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void MetadataDeserializationFailed(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Error, Message = "Error getting metadata object for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void GetMetadataObjectError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Debug, Message = "Listing metadata keys for blob '{BlobKey}'")]
    public static partial void ListingMetadataKeys(this ILogger<S3MetadataAdapter> logger, string BlobKey);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Information, Message = "Listed {Count} metadata keys for blob '{BlobKey}'")]
    public static partial void MetadataKeysListed(this ILogger<S3MetadataAdapter> logger, int Count, string BlobKey);

    [LoggerMessage(EventId = 6110, Level = LogLevel.Error, Message = "Error listing metadata keys for blob '{BlobKey}'")]
    public static partial void ListMetadataKeysError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey);

    [LoggerMessage(EventId = 6111, Level = LogLevel.Debug, Message = "Putting metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void PuttingMetadata(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Debug, Message = "Archive exists for blob '{BlobKey}', updating existing archive")]
    public static partial void ArchiveExists(this ILogger<S3MetadataAdapter> logger, string BlobKey);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Debug, Message = "No archive exists for blob '{BlobKey}', creating new archive")]
    public static partial void CreatingNewArchive(this ILogger<S3MetadataAdapter> logger, string BlobKey);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Information, Message = "Successfully stored metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void MetadataStored(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Error, Message = "Error putting metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void PutMetadataError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6116, Level = LogLevel.Debug, Message = "Deleting metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void DeletingMetadata(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6117, Level = LogLevel.Information, Message = "Successfully deleted metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void MetadataDeleted(this ILogger<S3MetadataAdapter> logger, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6118, Level = LogLevel.Error, Message = "Error deleting metadata for blob '{BlobKey}', metadata key '{MetadataKey}'")]
    public static partial void DeleteMetadataError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey, string MetadataKey);

    [LoggerMessage(EventId = 6119, Level = LogLevel.Debug, Message = "Deleting all metadata for blob '{BlobKey}'")]
    public static partial void DeletingAllMetadata(this ILogger<S3MetadataAdapter> logger, string BlobKey);

    [LoggerMessage(EventId = 6120, Level = LogLevel.Information, Message = "Successfully deleted all metadata for blob '{BlobKey}'")]
    public static partial void AllMetadataDeleted(this ILogger<S3MetadataAdapter> logger, string BlobKey);

    [LoggerMessage(EventId = 6121, Level = LogLevel.Error, Message = "Error deleting all metadata for blob '{BlobKey}'")]
    public static partial void DeleteAllMetadataError(this ILogger<S3MetadataAdapter> logger, Exception exception, string BlobKey);
}

