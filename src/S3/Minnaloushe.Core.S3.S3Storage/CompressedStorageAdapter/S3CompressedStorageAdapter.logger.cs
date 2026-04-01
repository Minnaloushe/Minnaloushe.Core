using Microsoft.Extensions.Logging;

namespace Inpx.Processor.S3Storage.Implementations;

internal static partial class S3CompressedStorageAdapterLogger
{
    [LoggerMessage(EventId = 6100, Level = LogLevel.Debug, Message = "GetUncompressedAsync: requesting stream for key '{Key}'")]
    public static partial void RequestingStream(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6101, Level = LogLevel.Error, Message = "GetUncompressedAsync: failed to get stream for key '{Key}'")]
    public static partial void GetStreamFailed(this ILogger logger, Exception exception, string Key);

    [LoggerMessage(EventId = 6102, Level = LogLevel.Debug, Message = "GetUncompressedAsync: key '{Key}' is not marked as compressed; returning raw stream")]
    public static partial void NotCompressed(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6103, Level = LogLevel.Debug, Message = "GetUncompressedAsync: key '{Key}' appears compressed; attempting to open archive")]
    public static partial void OpeningArchive(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6104, Level = LogLevel.Warning, Message = "GetUncompressedAsync: archive for key '{Key}' does not contain '{ContentEntry}'; returning original stream")]
    public static partial void ContentEntryMissing(this ILogger logger, string Key, string ContentEntry);

    [LoggerMessage(EventId = 6105, Level = LogLevel.Debug, Message = "Successfully extracted metadata from compressed stream for Key: {Key}")]
    public static partial void MetadataExtracted(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6106, Level = LogLevel.Error, Message = "Failed to extract metadata from compressed stream for Key: {Key}")]
    public static partial void MetadataExtractionFailed(this ILogger logger, Exception exception, string Key);

    [LoggerMessage(EventId = 6107, Level = LogLevel.Debug, Message = "GetUncompressedAsync: archive for key '{Key}' contains {ContentName} entry; returning entry stream")]
    public static partial void ReturningEntryStream(this ILogger logger, string Key, string ContentName);

    [LoggerMessage(EventId = 6108, Level = LogLevel.Error, Message = "GetUncompressedAsync: error uncompressing stream for key '{Key}' - returning original stream")]
    public static partial void UncompressingError(this ILogger logger, Exception exception, string Key);

    [LoggerMessage(EventId = 6109, Level = LogLevel.Information, Message = "GetUncompressedAsync: archive for key '{Key}' contains '{ContentEntry}' entry; returning entry stream")]
    public static partial void ReturningEntryStreamInfo(this ILogger logger, string Key, string ContentEntry);

    [LoggerMessage(EventId = 6110, Level = LogLevel.Warning, Message = "GetUncompressedAsync: archive for key '{Key}' does not contain '{ContentEntry}'; returning original stream")]
    public static partial void ContentEntryMissingWarning(this ILogger logger, string Key, string ContentEntry);

    [LoggerMessage(EventId = 6111, Level = LogLevel.Debug, Message = "PutCompressedAsync: preparing compressed upload for key '{Key}'")]
    public static partial void PreparingCompressedUpload(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6112, Level = LogLevel.Debug, Message = "PutCompressedAsync: tags for upload: {Tags}")]
    public static partial void UploadTags(this ILogger logger, string Tags);

    [LoggerMessage(EventId = 6113, Level = LogLevel.Debug, Message = "PutCompressedAsync: copying content into archive entry for key '{Key}'")]
    public static partial void CopyingContentToArchive(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6114, Level = LogLevel.Debug, Message = "PutCompressedAsync: upload stream prepared (size={Size} bytes) for key '{Key}'")]
    public static partial void UploadStreamPrepared(this ILogger logger, long Size, string Key);

    [LoggerMessage(EventId = 6115, Level = LogLevel.Information, Message = "PutCompressedAsync: successfully uploaded compressed content for key '{Key}'")]
    public static partial void CompressedUploadSuccessful(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6116, Level = LogLevel.Error, Message = "PutCompressedAsync: failed to upload compressed content for key '{Key}'")]
    public static partial void CompressedUploadFailed(this ILogger logger, Exception exception, string Key);

    [LoggerMessage(EventId = 6117, Level = LogLevel.Debug, Message = "IsCompressed: checking compressed tag for key '{Key}'")]
    public static partial void CheckingCompressedTag(this ILogger logger, string Key);

    [LoggerMessage(EventId = 6118, Level = LogLevel.Debug, Message = "IsCompressed: key '{Key}' compressed = {Compressed}")]
    public static partial void CompressedCheckResult(this ILogger logger, string Key, bool Compressed);
}
